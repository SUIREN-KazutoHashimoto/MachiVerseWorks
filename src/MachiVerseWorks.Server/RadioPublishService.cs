using System.Net.WebSockets;

namespace MachiVerseWorks.Server;

internal sealed class RadioPublishService(
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
                var targets = connections.CreateSnapshot().Where(static connection => connection.HandshakeCompleted && connection.NegotiatedVersion.SupportsRadio && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;
                var snapshot = simulation.Read(static world => world.CreateRadioSnapshot());
                if (snapshot.Sites.Count == 0 && snapshot.Links.Count == 0 && snapshot.Bands.Count == 0 && snapshot.FrequencyBlocks.Count == 0) continue;
                var messages = RadioMessageMapper.Create(snapshot);
                foreach (var connection in targets)
                {
                    try
                    {
                        using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        sendCancellation.CancelAfter(ClientSendTimeout);
                        _ = await connection.SendAsync(messages.Radio, connection.NegotiatedVersion, sendCancellation.Token);
                        sendCancellation.CancelAfter(ClientSendTimeout);
                        _ = await connection.SendAsync(messages.Spectrum, connection.NegotiatedVersion, sendCancellation.Token);
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
