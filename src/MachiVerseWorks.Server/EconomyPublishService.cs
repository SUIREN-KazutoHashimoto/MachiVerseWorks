using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed class EconomyPublishService(
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
                    && connection.NegotiatedVersion.SupportsEconomy
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                var message = EconomyMessageMapper.Create(simulation.Read(static world => world.CreateEconomySnapshot()));
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
