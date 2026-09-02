using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal sealed class RadioPublishService(
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
                var targets = connections.CreateSnapshot().Where(static connection => connection.HandshakeCompleted && connection.NegotiatedVersion.SupportsRadio && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;
                var snapshot = observationSource.CaptureRadioSnapshot();
                if (snapshot.Sites.Count == 0 && snapshot.Links.Count == 0 && snapshot.Bands.Count == 0 && snapshot.FrequencyBlocks.Count == 0) continue;
                var messages = RadioMessageMapper.Create(snapshot);
                foreach (var connection in targets)
                {
                    _ = deliveryCoordinator.TrySchedule(
                        connection,
                        stoppingToken,
                        async sendCancellation =>
                        {
                            _ = await connection.SendAsync(messages.Radio, connection.NegotiatedVersion, sendCancellation);
                            _ = await connection.SendAsync(messages.Spectrum, connection.NegotiatedVersion, sendCancellation);
                        });
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
