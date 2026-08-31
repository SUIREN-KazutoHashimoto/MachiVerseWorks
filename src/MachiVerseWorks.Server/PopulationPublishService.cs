using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal readonly record struct PendingPopulationDelivery(ClientConnection Connection, ulong? InspectedPersonId);

internal sealed class PopulationPublishService(
    SimulationRuntime simulation,
    ServerOptions options,
    ClientConnectionRegistry connections) : BackgroundService
{
    private static readonly TimeSpan ClientSendTimeout = TimeSpan.FromSeconds(5);
    private readonly SnapshotDeliveryScheduler _deliveryScheduler = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _deliveryScheduler.ThrowIfFaulted();
                var pending = CapturePendingDeliveries();
                if (pending.Length == 0) continue;
                try
                {
                    var inspectedIds = pending.Where(static item => item.InspectedPersonId.HasValue).Select(static item => item.InspectedPersonId!.Value).ToHashSet();
                    var snapshot = simulation.CapturePopulationPublishSnapshot(inspectedIds);
                    var statistics = PopulationMessageMapper.Create(snapshot.Statistics);
                    foreach (var delivery in pending)
                    {
                        _deliveryScheduler.StartReserved(delivery.Connection.Id, () => PublishConnectionAsync(delivery, snapshot, statistics, stoppingToken));
                    }
                }
                catch
                {
                    foreach (var delivery in pending) _deliveryScheduler.ReleaseReservation(delivery.Connection.Id);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _deliveryScheduler.ThrowIfFaulted();
        var inFlight = _deliveryScheduler.CreateInFlightSnapshot();
        if (inFlight.Length > 0) await Task.WhenAll(inFlight);
        _deliveryScheduler.ThrowIfFaulted();
    }

    private PendingPopulationDelivery[] CapturePendingDeliveries()
    {
        var candidates = connections.CreateSnapshot();
        var pending = new List<PendingPopulationDelivery>(candidates.Length);
        foreach (var connection in candidates)
        {
            if (!connection.HandshakeCompleted
                || !connection.NegotiatedVersion.SupportsPopulation
                || connection.Socket.State != WebSocketState.Open
                || !_deliveryScheduler.TryReserve(connection.Id))
            {
                continue;
            }
            pending.Add(new PendingPopulationDelivery(connection, connection.TryGetInspectedPersonId(out var personId) ? personId : null));
        }
        return pending.ToArray();
    }

    private async Task PublishConnectionAsync(
        PendingPopulationDelivery delivery,
        PopulationPublishSnapshot snapshot,
        PopulationStatisticsMessage statistics,
        CancellationToken cancellationToken)
    {
        var connection = delivery.Connection;
        try
        {
            await Task.Yield();
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(ClientSendTimeout);
            _ = await connection.SendAsync(statistics, connection.NegotiatedVersion, sendCancellation.Token);
            if (delivery.InspectedPersonId is { } personId && snapshot.InspectedPersons.TryGetValue(personId, out var person))
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
