using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class PopulationPublishService(
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
                var candidates = connections.CreateSnapshot();
                if (candidates.Length == 0) continue;
                var statistics = PopulationMessageMapper.Create(simulation.CreatePopulationStatistics());
                foreach (var connection in candidates)
                {
                    if (!connection.HandshakeCompleted
                        || !connection.NegotiatedVersion.SupportsPopulation
                        || connection.Socket.State != WebSocketState.Open)
                    {
                        continue;
                    }

                    try
                    {
                        using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        sendCancellation.CancelAfter(ClientSendTimeout);
                        _ = await connection.SendAsync(statistics, connection.NegotiatedVersion, sendCancellation.Token);
                        if (connection.TryGetInspectedPersonId(out var personId)
                            && simulation.TryGetPersonSnapshot(new PersonId(personId), out var person))
                        {
                            sendCancellation.CancelAfter(ClientSendTimeout);
                            _ = await connection.SendAsync(PopulationMessageMapper.Create(person), connection.NegotiatedVersion, sendCancellation.Token);
                        }
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
