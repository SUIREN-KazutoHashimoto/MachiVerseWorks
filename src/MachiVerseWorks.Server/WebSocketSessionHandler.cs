using System.Globalization;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class WebSocketSessionHandler(
    ClientConnectionRegistry connections,
    ClientCommandQueue commandQueue,
    SimulationRuntime simulation,
    ServerOptions options,
    ILogger<WebSocketSessionHandler> logger)
{
    private const int ReceiveBufferSize = 8192;
    private const int MaximumFrameLength = ProtocolFrameHeader.Size + (int)ProtocolFrameHeader.MaxPayloadLength;

    public async Task HandleAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);

        var connection = connections.Register(socket);
        ServerLog.ClientConnected(logger, connection.Id);

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var frame = await ReceiveFrameAsync(socket, cancellationToken);
                if (frame is null)
                {
                    break;
                }

                if (!ProtocolCodec.TryDeserialize(frame, out var envelope, out var decodeError) || envelope is null)
                {
                    await SendDecodeErrorAsync(connection, decodeError, cancellationToken);
                    await CloseSafelyAsync(socket, WebSocketCloseStatus.InvalidPayloadData, "Invalid protocol frame.", cancellationToken);
                    break;
                }

                if (!await HandleEnvelopeAsync(connection, envelope, cancellationToken))
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception)
        {
            ServerLog.WebSocketEnded(logger, connection.Id, exception);
        }
        finally
        {
            connections.Remove(connection.Id);
            await CloseSafelyAsync(socket, WebSocketCloseStatus.NormalClosure, "Connection closed.", CancellationToken.None);
            connection.Dispose();
            ServerLog.ClientDisconnected(logger, connection.Id);
        }
    }

    private async Task<bool> HandleEnvelopeAsync(
        ClientConnection connection,
        ProtocolEnvelope envelope,
        CancellationToken cancellationToken)
    {
        if (!connection.HandshakeCompleted)
        {
            if (envelope.Message is not HelloMessage)
            {
                await SendErrorAsync(
                    connection,
                    ProtocolErrorCode.InvalidRequest,
                    [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "helloRequired")],
                    ProtocolVersion.Current,
                    cancellationToken);
                await CloseSafelyAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Hello required.", cancellationToken);
                return false;
            }

            if (!ProtocolVersion.Current.CanAccept(envelope.Version))
            {
                await SendErrorAsync(
                    connection,
                    ProtocolErrorCode.UnsupportedProtocolVersion,
                    [
                        new ProtocolErrorParameter(
                            ProtocolErrorParameterKeys.RequestedVersion,
                            envelope.Version.ToString()),
                        new ProtocolErrorParameter(
                            ProtocolErrorParameterKeys.SupportedVersion,
                            ProtocolVersion.Current.ToString()),
                    ],
                    ProtocolVersion.Current,
                    cancellationToken);
                await CloseSafelyAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Unsupported protocol version.", cancellationToken);
                return false;
            }

            connection.CompleteHandshake(envelope.Version);
            await connection.SendAsync(
                new HelloAckMessage(ProtocolVersion.Current, checked((ushort)simulation.TickRate)),
                envelope.Version,
                cancellationToken);
            return true;
        }

        if (envelope.Version != connection.NegotiatedVersion)
        {
            await SendErrorAsync(
                connection,
                ProtocolErrorCode.UnsupportedProtocolVersion,
                [new ProtocolErrorParameter(ProtocolErrorParameterKeys.RequestedVersion, envelope.Version.ToString())],
                connection.NegotiatedVersion,
                cancellationToken);
            await CloseSafelyAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Protocol version changed.", cancellationToken);
            return false;
        }

        switch (envelope.Message)
        {
            case SubscribeAreaMessage subscribeArea:
                try
                {
                    var area = new WorldRect(
                        subscribeArea.MinX,
                        subscribeArea.MinY,
                        subscribeArea.MaxX,
                        subscribeArea.MaxY);
                    if (!SubscriptionAreaPolicy.TryValidate(
                        area,
                        options.SpatialCellSize,
                        options.MaximumSubscriptionCellCount,
                        out var detailCode))
                    {
                        await SendErrorAsync(
                            connection,
                            ProtocolErrorCode.InvalidRequest,
                            [
                                new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "area"),
                                new ProtocolErrorParameter(
                                    ProtocolErrorParameterKeys.DetailCode,
                                    detailCode ?? SubscriptionAreaPolicy.OutsideSpatialGridDetailCode),
                            ],
                            connection.NegotiatedVersion,
                            cancellationToken);
                        return true;
                    }

                    await commandQueue.WriteAsync(
                        new SubscribeAreaCommand(connection.Id, area),
                        cancellationToken);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    await SendErrorAsync(
                        connection,
                        ProtocolErrorCode.InvalidRequest,
                        [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "area")],
                        connection.NegotiatedVersion,
                        cancellationToken);
                    return true;
                }

            default:
                await SendErrorAsync(
                    connection,
                    ProtocolErrorCode.InvalidRequest,
                    [
                        new ProtocolErrorParameter(
                            ProtocolErrorParameterKeys.MessageType,
                            ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture)),
                    ],
                    connection.NegotiatedVersion,
                    cancellationToken);
                return true;
        }
    }

    private static async Task<byte[]?> ReceiveFrameAsync(
        WebSocket socket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                await CloseSafelyAsync(socket, WebSocketCloseStatus.InvalidMessageType, "Binary messages required.", cancellationToken);
                return null;
            }

            if (stream.Length + result.Count > MaximumFrameLength)
            {
                await CloseSafelyAsync(socket, WebSocketCloseStatus.MessageTooBig, "Protocol frame too large.", cancellationToken);
                return null;
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return stream.ToArray();
            }
        }
    }

    private static Task SendDecodeErrorAsync(
        ClientConnection connection,
        ProtocolDecodeError decodeError,
        CancellationToken cancellationToken)
    {
        var code = decodeError switch
        {
            ProtocolDecodeError.UnknownMessageType => ProtocolErrorCode.UnknownMessageType,
            ProtocolDecodeError.InvalidPayload => ProtocolErrorCode.InvalidPayload,
            _ => ProtocolErrorCode.InvalidFrame,
        };

        var version = connection.HandshakeCompleted
            ? connection.NegotiatedVersion
            : ProtocolVersion.Current;

        return SendErrorAsync(
            connection,
            code,
            [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, decodeError.ToString())],
            version,
            cancellationToken);
    }

    private static Task SendErrorAsync(
        ClientConnection connection,
        ProtocolErrorCode code,
        IReadOnlyList<ProtocolErrorParameter> parameters,
        ProtocolVersion version,
        CancellationToken cancellationToken)
    {
        return connection.SendAsync(
            new ProtocolErrorMessage(code, parameters),
            version,
            cancellationToken);
    }

    private static async Task CloseSafelyAsync(
        WebSocket socket,
        WebSocketCloseStatus closeStatus,
        string description,
        CancellationToken cancellationToken)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        try
        {
            await socket.CloseAsync(closeStatus, description, cancellationToken);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            socket.Abort();
        }
    }
}
