using System.Net.WebSockets;

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
                    .ToArray();

                foreach (var connection in targets)
                {
                    if (!connection.TryCaptureSubscription(out var subscription)) continue;
                    try
                    {
                        var snapshot = simulation.Read(world => world.CreateDetailedWorldEnvironmentSnapshot(subscription.Volume));
                        var message = WorldEnvironmentMessageMapper.ToProtocol(snapshot);
                        using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        sendCancellation.CancelAfter(ClientSendTimeout);
                        _ = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token);
                    }
                    catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
                    {
                        connection.Abort();
                        connections.Remove(connection.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
