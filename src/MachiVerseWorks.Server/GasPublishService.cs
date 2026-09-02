using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal sealed class GasPublishService(
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
                var targets = connections.CreateSnapshot().Where(static connection => connection.HandshakeCompleted && connection.NegotiatedVersion.SupportsGas && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;
                var snapshots = observationSource.CaptureGasSnapshot();
                var snapshot = snapshots.Gas;
                if (snapshot.Nodes.Count == 0 && snapshot.Pipelines.Count == 0 && snapshot.Sources.Count == 0 && snapshot.ImportTerminals.Count == 0 && snapshot.Storages.Count == 0 && snapshot.ServicePoints.Count == 0) continue;
                var message = GasMessageMapper.Create(snapshot, snapshots.Logistics);
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
