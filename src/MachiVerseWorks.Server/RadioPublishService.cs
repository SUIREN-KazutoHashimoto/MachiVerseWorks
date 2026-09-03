using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed class RadioPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationDeliveryCoordinator deliveryCoordinator,
    ILogger<RadioPublishService> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogInvalidSnapshot = LoggerMessage.Define(
        LogLevel.Warning,
        new EventId(1, nameof(LogInvalidSnapshot)),
        "Radio observation could not be encoded within the protocol contract; this publish cycle was skipped.");

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

                (RadioSnapshotMessage Radio, SpectrumSnapshotMessage Spectrum) messages;
                try
                {
                    messages = RadioMessageMapper.Create(snapshot);
                    _ = RadioProtocolCodec.GetSerializedLength(messages.Radio, ProtocolVersion.Current);
                    _ = RadioProtocolCodec.GetSerializedLength(messages.Spectrum, ProtocolVersion.Current);
                }
                catch (Exception exception) when (exception is ArgumentException or OverflowException)
                {
                    LogInvalidSnapshot(logger, exception);
                    continue;
                }

                foreach (var connection in targets)
                {
                    _ = deliveryCoordinator.TrySchedule(
                        connection,
                        ObservationDeliveryLane.Radio,
                        messages.Radio,
                        messages.Spectrum,
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
