using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed class PersistentRegionalEvolutionPublishService(
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
                var targets = connections.CreateSnapshot().Where(static connection =>
                    connection.HandshakeCompleted
                    && connection.NegotiatedVersion.SupportsPersistentRegionalEvolution
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                var captured = observationSource.CapturePersistentRegionalEvolutionSnapshot();
                if (captured is null) continue;
                var message = PersistentRegionalEvolutionMessageMapper.ToProtocol(captured.Value.Evolution, captured.Value.Interactions);
                var chunks = PersistentRegionalEvolutionProtocolChunker.Split(message);
                foreach (var connection in targets)
                {
                    try
                    {
                        using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        sendCancellation.CancelAfter(ClientSendTimeout);
                        foreach (var chunk in chunks)
                            _ = await connection.SendAsync(chunk, connection.NegotiatedVersion, sendCancellation.Token);
                    }
                    catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException or ArgumentOutOfRangeException)
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
