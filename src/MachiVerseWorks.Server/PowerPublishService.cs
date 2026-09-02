using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed class PowerPublishService(
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
                    && connection.NegotiatedVersion.SupportsPower
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                var snapshot = observationSource.CapturePowerSnapshot();
                if (snapshot.Nodes.Count == 0
                    && snapshot.Lines.Count == 0
                    && snapshot.Generators.Count == 0
                    && snapshot.Loads.Count == 0)
                {
                    continue;
                }

                var message = PowerMessageMapper.Create(snapshot);
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
