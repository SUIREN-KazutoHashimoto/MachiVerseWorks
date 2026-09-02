using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed class LogisticsPublishService(
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
                    && connection.NegotiatedVersion.SupportsLogistics
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                var message = LogisticsMessageMapper.Create(observationSource.CaptureLogisticsSnapshot());
                foreach (var connection in targets)
                {
                    _ = deliveryCoordinator.TrySchedule(
                        connection,
                        stoppingToken,
                        async sendCancellation =>
                        {
                            _ = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation);
                        });
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
