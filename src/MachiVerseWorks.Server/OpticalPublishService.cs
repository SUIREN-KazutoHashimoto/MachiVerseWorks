using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal sealed class OpticalPublishService(
    IObservationSource observationSource,
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
                var targets = connections.CreateSnapshot().Where(static connection => connection.HandshakeCompleted && connection.NegotiatedVersion.SupportsOptical && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;
                var snapshot = observationSource.CaptureOpticalSnapshot();
                if (snapshot.Nodes.Count == 0 && snapshot.FiberCables.Count == 0 && snapshot.Equipment.Count == 0 && snapshot.Backhauls.Count == 0 && snapshot.Demands.Count == 0) continue;
                var message = OpticalMessageMapper.Create(snapshot);
                foreach (var connection in targets)
                {
                    try
                    {
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
