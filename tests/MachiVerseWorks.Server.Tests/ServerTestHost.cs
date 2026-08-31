using System.Globalization;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MachiVerseWorks.Server.Tests;

internal sealed class ServerTestHost : IAsyncDisposable
{
    private bool _stopped;

    private ServerTestHost(WebApplication app, Uri httpAddress)
    {
        App = app;
        HttpAddress = httpAddress;
    }

    public WebApplication App { get; }
    public Uri HttpAddress { get; }

    public static async Task<ServerTestHost> StartAsync(
        int initialAgentCount = 4,
        int tickRate = 30,
        int snapshotRate = 30,
        double spawnHalfExtent = 5d,
        IReadOnlyDictionary<string, string?>? additionalConfiguration = null)
    {
        var app = ServerApplication.Build([], builder =>
        {
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:ListenAddress"] = "127.0.0.1",
                ["Server:Port"] = "0",
                ["Server:SnapshotRate"] = snapshotRate.ToString(CultureInfo.InvariantCulture),
                ["Simulation:TickRate"] = tickRate.ToString(CultureInfo.InvariantCulture),
                ["Simulation:InitialAgentCount"] = initialAgentCount.ToString(CultureInfo.InvariantCulture),
                ["Simulation:SpawnVolume:MinX"] = (-spawnHalfExtent).ToString(CultureInfo.InvariantCulture),
                ["Simulation:SpawnVolume:MinY"] = (-spawnHalfExtent).ToString(CultureInfo.InvariantCulture),
                ["Simulation:SpawnVolume:MinZ"] = (-spawnHalfExtent).ToString(CultureInfo.InvariantCulture),
                ["Simulation:SpawnVolume:MaxX"] = spawnHalfExtent.ToString(CultureInfo.InvariantCulture),
                ["Simulation:SpawnVolume:MaxY"] = spawnHalfExtent.ToString(CultureInfo.InvariantCulture),
                ["Simulation:SpawnVolume:MaxZ"] = spawnHalfExtent.ToString(CultureInfo.InvariantCulture),
            });
            if (additionalConfiguration is not null) builder.Configuration.AddInMemoryCollection(additionalConfiguration);
        });

        await app.StartAsync();
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>() ?? throw new InvalidOperationException("Kestrel did not expose server addresses.");
        return new ServerTestHost(app, new Uri(addresses.Addresses.Single()));
    }

    public HttpClient CreateHttpClient() => new() { BaseAddress = HttpAddress };

    public async Task<ClientWebSocket> ConnectWebSocketAsync(string? origin = null)
    {
        var webSocket = new ClientWebSocket();
        if (origin is not null) webSocket.Options.SetRequestHeader("Origin", origin);
        var builder = new UriBuilder(HttpAddress) { Scheme = HttpAddress.Scheme == "https" ? "wss" : "ws", Path = "/ws" };
        await webSocket.ConnectAsync(builder.Uri, CancellationToken.None);
        return webSocket;
    }

    public static Task SendAsync(ClientWebSocket socket, IProtocolMessage message, ProtocolVersion? version = null)
    {
        var resolvedVersion = version ?? ProtocolVersion.Current;
        var frame = message switch
        {
            InspectPersonMessage inspectPerson => PopulationProtocolCodec.Serialize(inspectPerson, resolvedVersion),
            ClearPersonInspectionMessage clearInspection => PersonInspectionProtocolCodec.Serialize(clearInspection, resolvedVersion),
            _ => ProtocolCodec.Serialize(message, resolvedVersion),
        };
        return socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, true, CancellationToken.None);
    }

    public static async Task<ProtocolEnvelope> ReceiveAsync(ClientWebSocket socket, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        var buffer = new byte[4096];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation.Token);
            if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Server closed the WebSocket before a protocol message was received.");
            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage) break;
        }

        var frame = stream.ToArray();
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out var headerError))
            throw new InvalidOperationException($"Server returned an invalid protocol frame: {headerError}.");

        ProtocolEnvelope? envelope;
        ProtocolDecodeError error;
        bool decoded;
        if (header.MessageType is MessageType.PopulationStatistics or MessageType.PersonDebug)
        {
            decoded = PopulationProtocolCodec.TryDeserialize(frame, out envelope, out error);
        }
        else if (header.MessageType == MessageType.RailwayInfrastructureSnapshot)
        {
            decoded = RailwayInfrastructureProtocolCodec.TryDeserialize(frame, out var railway, out error);
            envelope = decoded ? new ProtocolEnvelope(header.Version, railway) : null;
        }
        else if (header.MessageType == MessageType.RailwayOperationsSnapshot)
        {
            decoded = RailwayOperationsProtocolCodec.TryDeserialize(frame, out var railwayOperations, out error);
            envelope = decoded ? new ProtocolEnvelope(header.Version, railwayOperations) : null;
        }
        else if (header.MessageType == MessageType.MultimodalTransitSnapshot)
        {
            decoded = MultimodalTransitProtocolCodec.TryDeserialize(frame, out var multimodalTransit, out error);
            envelope = decoded ? new ProtocolEnvelope(header.Version, multimodalTransit) : null;
        }
        else
        {
            decoded = ProtocolCodec.TryDeserialize(frame, out envelope, out error);
        }

        if (!decoded || envelope is null) throw new InvalidOperationException($"Server returned an invalid protocol frame: {error}.");
        return envelope;
    }

    public static async Task HandshakeAsync(ClientWebSocket socket)
    {
        await SendAsync(socket, new HelloMessage(), ProtocolVersion.Current);
        var envelope = await ReceiveAsync(socket, TimeSpan.FromSeconds(3));
        if (envelope.Message is not HelloAckMessage) throw new InvalidOperationException("Server did not return HelloAck.");
    }

    public async Task StopAsync()
    {
        if (_stopped) return;
        _stopped = true;
        await App.StopAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await App.DisposeAsync();
    }
}
