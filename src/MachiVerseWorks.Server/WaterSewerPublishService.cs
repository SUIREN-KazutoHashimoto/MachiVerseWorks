using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal sealed class WaterSewerPublishService(
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
                var targets = connections.CreateSnapshot().Where(static connection =>
                    connection.HandshakeCompleted
                    && connection.NegotiatedVersion.SupportsWaterSewer
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                var snapshot = simulation.Read(static world => world.CreateWaterSewerSnapshot());
                if (snapshot.WaterNodes.Count == 0
                    && snapshot.WaterPipes.Count == 0
                    && snapshot.SewerNodes.Count == 0
                    && snapshot.SewerPipes.Count == 0
                    && snapshot.WaterSources.Count == 0
                    && snapshot.Reservoirs.Count == 0
                    && snapshot.Pumps.Count == 0
                    && snapshot.TreatmentPlants.Count == 0
                    && snapshot.ServicePoints.Count == 0)
                {
                    continue;
                }

                var message = WaterSewerMessageMapper.Create(snapshot);
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
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
