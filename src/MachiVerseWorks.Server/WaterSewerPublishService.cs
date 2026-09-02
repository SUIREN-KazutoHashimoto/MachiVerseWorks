using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal sealed class WaterSewerPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationDeliveryCoordinator deliveryCoordinator) : BackgroundService
{
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

                var snapshot = observationSource.CaptureWaterSewerSnapshot();
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
                    _ = deliveryCoordinator.TrySchedule(
                        connection,
                        async sendCancellation =>
                        {
                            _ = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation);
                        },
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
