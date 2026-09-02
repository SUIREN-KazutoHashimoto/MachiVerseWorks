using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class WorldEnvironmentPublishService(
    SimulationRuntime simulation,
    ServerOptions options,
    ClientConnectionRegistry connections) : BackgroundService
{
    private static readonly TimeSpan ClientSendTimeout = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var targets = connections.CreateSnapshot()
                    .Where(static connection => connection.HandshakeCompleted
                        && connection.NegotiatedVersion.SupportsWorldEnvironment
                        && connection.Socket.State == WebSocketState.Open)
                    .Select(connection => connection.TryCaptureSubscription(out var subscription)
                        ? new EnvironmentPublishTarget(connection, subscription.Volume)
                        : null)
                    .Where(static target => target is not null)
                    .Select(static target => target!)
                    .ToArray();
                if (targets.Length == 0) continue;

                var messages = new Dictionary<WorldVolume, WorldEnvironmentSnapshotMessage>();
                foreach (var target in targets)
                {
                    if (!messages.TryGetValue(target.Volume, out var message))
                    {
                        var snapshot = simulation.Read(world => world.CreateDetailedWorldEnvironmentSnapshot(target.Volume));
                        message = WorldEnvironmentMessageMapper.ToProtocol(snapshot);
                        messages.Add(target.Volume, message);
                    }

                    try
                    {
                        using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        sendCancellation.CancelAfter(ClientSendTimeout);
                        _ = await target.Connection.SendAsync(message, target.Connection.NegotiatedVersion, sendCancellation.Token);
                    }
                    catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
                    {
                        target.Connection.Abort();
                        connections.Remove(target.Connection.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private sealed record EnvironmentPublishTarget(ClientConnection Connection, WorldVolume Volume);
}
