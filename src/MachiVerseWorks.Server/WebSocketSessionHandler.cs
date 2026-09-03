using System.Globalization;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class WebSocketSessionHandler(ClientConnectionRegistry connections, ObservationRequestQueue observationRequests, IObservationSource observationSource, ServerOptions options, ILogger<WebSocketSessionHandler> logger)
{
    private const int ReceiveBufferSize = 8192;
    private const int MaximumFrameLength = ProtocolFrameHeader.Size + (int)ProtocolFrameHeader.MaxPayloadLength;

    public async Task HandleAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(socket);
        ClientConnection? connection = null;
        try
        {
            connection = connections.Register(socket, options.MaximumWebSocketConnections);
            ServerLog.ClientConnected(logger, connection.Id);
            using var helloCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            helloCancellation.CancelAfter(options.HelloTimeout);

            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                byte[]? frame;
                try
                {
                    var receiveToken = connection.HandshakeCompleted ? cancellationToken : helloCancellation.Token;
                    frame = await ReceiveFrameAsync(socket, options.FrameReceiveTimeout, receiveToken);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && !connection.HandshakeCompleted && helloCancellation.IsCancellationRequested)
                {
                    await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.PolicyViolation, "Hello timeout.", options.CloseTimeout, cancellationToken);
                    break;
                }
                if (frame is null) break;
                if (!ObservationProtocolAdapter.TryDeserialize(frame, out var envelope, out var decodeError) || envelope is null)
                {
                    await SendDecodeErrorAsync(connection, decodeError, cancellationToken);
                    await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.InvalidPayloadData, "Invalid protocol frame.", options.CloseTimeout, cancellationToken);
                    break;
                }
                if (!await HandleEnvelopeAsync(connection, envelope, cancellationToken)) break;
            }
        }
        catch (ConnectionLimitExceededException)
        {
            await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.PolicyViolation, "Connection limit reached.", options.CloseTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (WebSocketException exception) when (connection is not null)
        {
            ServerLog.WebSocketEnded(logger, connection.Id, exception);
        }
        finally
        {
            if (connection is not null)
            {
                connections.Remove(connection.Id);
                await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.NormalClosure, "Connection closed.", options.CloseTimeout, cancellationToken);
                connection.Dispose();
                ServerLog.ClientDisconnected(logger, connection.Id);
            }
            else if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.NormalClosure, "Connection closed.", options.CloseTimeout, cancellationToken);
            }
        }
    }

    private async Task<bool> HandleEnvelopeAsync(ClientConnection connection, ProtocolEnvelope envelope, CancellationToken cancellationToken)
    {
        if (!connection.HandshakeCompleted)
        {
            if (envelope.Message is not HelloMessage)
            {
                await SendErrorAsync(connection, ProtocolErrorCode.InvalidRequest, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "helloRequired")], ProtocolVersion.Current, cancellationToken);
                await CloseWithTimeoutAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Hello required.", options.CloseTimeout, cancellationToken);
                return false;
            }
            if (!ProtocolVersion.Current.TryNegotiate(envelope.Version, out var negotiatedVersion))
            {
                await SendErrorAsync(connection, ProtocolErrorCode.UnsupportedProtocolVersion, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.RequestedVersion, envelope.Version.ToString()), new ProtocolErrorParameter(ProtocolErrorParameterKeys.SupportedVersion, ProtocolVersion.Current.ToString())], ProtocolVersion.Current, cancellationToken);
                await CloseWithTimeoutAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Unsupported protocol version.", options.CloseTimeout, cancellationToken);
                return false;
            }
            await connection.SendAsync(new HelloAckMessage(negotiatedVersion, checked((ushort)observationSource.TickRate)), negotiatedVersion, cancellationToken);
            connection.CompleteHandshake(negotiatedVersion);
            return true;
        }

        if (envelope.Version != connection.NegotiatedVersion)
        {
            await SendErrorAsync(connection, ProtocolErrorCode.UnsupportedProtocolVersion, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.RequestedVersion, envelope.Version.ToString())], connection.NegotiatedVersion, cancellationToken);
            await CloseWithTimeoutAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Protocol version changed.", options.CloseTimeout, cancellationToken);
            return false;
        }

        if (!connection.TryConsumeRequest(options.RequestRateLimitPerSecond, options.RequestRateLimitBurst))
        {
            return await RejectRecoverableAsync(connection,
                [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "rateLimited")],
                cancellationToken);
        }

        if (envelope.Message is not IObservationRequestMessage)
        {
            return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.MessageType, ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture))], cancellationToken);
        }

        switch (envelope.Message)
        {
            case SubscribeVolumeMessage subscribeVolume:
                try
                {
                    var volume = new WorldVolume(subscribeVolume.MinX, subscribeVolume.MinY, subscribeVolume.MinZ, subscribeVolume.MaxX, subscribeVolume.MaxY, subscribeVolume.MaxZ);
                    if (!SubscriptionVolumePolicy.TryValidate(volume, observationSource.SpatialCellSize, options.MaximumSubscriptionCellCount, out var detailCode))
                    {
                        return await RejectRecoverableAsync(connection,
                            [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "volume"), new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, detailCode ?? SubscriptionVolumePolicy.OutsideSpatialGridDetailCode)], cancellationToken);
                    }
                    await observationRequests.WriteAsync(new SubscribeVolumeObservationRequest(connection.Id, volume), cancellationToken);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "volume")], cancellationToken);
                }
            case InspectPersonMessage inspectPerson:
                if (!connection.NegotiatedVersion.SupportsPopulation)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.MessageType, ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture))], cancellationToken);
                }
                if (!options.EnablePersonInspection)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "personId"), new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "inspectionDisabled")], cancellationToken);
                }
                if (!observationSource.PersonExists(inspectPerson.PersonId))
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "personId"), new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "personNotFound")], cancellationToken);
                }
                await observationRequests.WriteAsync(new InspectPersonObservationRequest(connection.Id, inspectPerson.PersonId), cancellationToken);
                return true;
            case ClearPersonInspectionMessage:
                if (!connection.NegotiatedVersion.SupportsPersonInspectionClear)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.MessageType, ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture))], cancellationToken);
                }
                await observationRequests.WriteAsync(new ClearPersonInspectionObservationRequest(connection.Id), cancellationToken);
                return true;
            case InspectEntityMessage inspectEntity:
                if (!connection.NegotiatedVersion.SupportsPersistentRegionalEvolution)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.MessageType, ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture))], cancellationToken);
                }
                if (inspectEntity.EntityType == ProtocolEntityType.Person && !options.EnablePersonInspection)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "entityId"), new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "inspectionDisabled")], cancellationToken);
                }
                if (inspectEntity.EntityType == ProtocolEntityType.Person && !observationSource.PersonExists(inspectEntity.EntityId))
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "entityId"), new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "personNotFound")], cancellationToken);
                }
                await observationRequests.WriteAsync(new InspectEntityObservationRequest(connection.Id, inspectEntity.EntityType, inspectEntity.EntityId), cancellationToken);
                return true;
            case ClearEntityInspectionMessage:
                if (!connection.NegotiatedVersion.SupportsPersistentRegionalEvolution)
                {
                    return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.MessageType, ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture))], cancellationToken);
                }
                await observationRequests.WriteAsync(new ClearEntityInspectionObservationRequest(connection.Id), cancellationToken);
                return true;
            default:
                return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.MessageType, ((ushort)envelope.Message.Type).ToString(CultureInfo.InvariantCulture))], cancellationToken);
        }
    }

    private async Task<bool> RejectRecoverableAsync(ClientConnection connection, IReadOnlyList<ProtocolErrorParameter> parameters, CancellationToken cancellationToken)
    {
        await SendErrorAsync(connection, ProtocolErrorCode.InvalidRequest, parameters, connection.NegotiatedVersion, cancellationToken);
        if (connection.RegisterInvalidRequest(options.InvalidRequestStrikeLimit, options.InvalidRequestStrikeWindow)) return true;
        await CloseWithTimeoutAsync(connection.Socket, WebSocketCloseStatus.PolicyViolation, "Too many invalid requests.", options.CloseTimeout, cancellationToken);
        return false;
    }

    private async Task<byte[]?> ReceiveFrameAsync(WebSocket socket, TimeSpan frameReceiveTimeout, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var stream = new MemoryStream();
        CancellationTokenSource? frameCancellation = null;
        try
        {
            while (true)
            {
                var receiveToken = frameCancellation?.Token ?? cancellationToken;
                WebSocketReceiveResult result;
                try
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), receiveToken);
                }
                catch (OperationCanceledException) when (frameCancellation is not null && frameCancellation.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                {
                    await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.PolicyViolation, "Protocol frame completion timeout.", options.CloseTimeout, cancellationToken);
                    return null;
                }
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.InvalidMessageType, "Binary messages required.", options.CloseTimeout, cancellationToken);
                    return null;
                }
                if (stream.Length + result.Count > MaximumFrameLength)
                {
                    await CloseWithTimeoutAsync(socket, WebSocketCloseStatus.MessageTooBig, "Protocol frame too large.", options.CloseTimeout, cancellationToken);
                    return null;
                }
                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage) return stream.ToArray();
                if (frameCancellation is null)
                {
                    frameCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    frameCancellation.CancelAfter(frameReceiveTimeout);
                }
            }
        }
        finally
        {
            frameCancellation?.Dispose();
        }
    }

    private static Task SendDecodeErrorAsync(ClientConnection connection, ProtocolDecodeError decodeError, CancellationToken cancellationToken)
    {
        var code = decodeError switch
        {
            ProtocolDecodeError.UnknownMessageType => ProtocolErrorCode.UnknownMessageType,
            ProtocolDecodeError.InvalidPayload => ProtocolErrorCode.InvalidPayload,
            _ => ProtocolErrorCode.InvalidFrame,
        };
        var version = connection.HandshakeCompleted ? connection.NegotiatedVersion : ProtocolVersion.Current;
        return SendErrorAsync(connection, code, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, decodeError.ToString())], version, cancellationToken);
    }

    private static async Task SendErrorAsync(ClientConnection connection, ProtocolErrorCode code, IReadOnlyList<ProtocolErrorParameter> parameters, ProtocolVersion version, CancellationToken cancellationToken)
    {
        _ = await connection.SendAsync(new ProtocolErrorMessage(code, parameters), version, cancellationToken);
    }

    private static async Task CloseWithTimeoutAsync(WebSocket socket, WebSocketCloseStatus closeStatus, string description, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived)) return;
        using var closeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        closeCancellation.CancelAfter(timeout);
        try
        {
            await socket.CloseAsync(closeStatus, description, closeCancellation.Token);
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException)
        {
            socket.Abort();
        }
    }
}
