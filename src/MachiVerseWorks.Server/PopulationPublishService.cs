using System.Net.WebSockets;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal readonly record struct PendingPopulationDelivery(ClientConnection Connection, ClientInspectionState Inspection);

internal sealed class PopulationPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationCache cache,
    SnapshotDeliveryScheduler deliveryScheduler) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                deliveryScheduler.ThrowIfFaulted();
                var pending = CapturePendingDeliveries();
                if (pending.Length == 0) continue;
                try
                {
                    var inspectedIds = pending
                        .Where(static item => item.Inspection.PersonId.HasValue)
                        .Select(static item => item.Inspection.PersonId!.Value)
                        .ToHashSet();
                    var snapshot = observationSource.CapturePopulationPublishSnapshot(inspectedIds);
                    var statistics = PopulationMessageMapper.Create(snapshot.Statistics);
                    foreach (var delivery in pending)
                    {
                        deliveryScheduler.StartReserved(delivery.Connection.Id, () => PublishConnectionAsync(delivery, snapshot, statistics, stoppingToken));
                    }
                }
                catch
                {
                    foreach (var delivery in pending) deliveryScheduler.ReleaseReservation(delivery.Connection.Id);
                    throw;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        deliveryScheduler.ThrowIfFaulted();
        var inFlight = deliveryScheduler.CreateInFlightSnapshot();
        if (inFlight.Length > 0) await Task.WhenAll(inFlight);
        deliveryScheduler.ThrowIfFaulted();
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
                || !deliveryScheduler.TryReserve(connection.Id, ObservationDeliveryLane.Population))
            {
                continue;
            }
            pending.Add(new PendingPopulationDelivery(connection, connection.CaptureInspectionState()));
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
            var revision = new ObservationRevision(snapshot.ObservationGeneration, snapshot.ObservationRevision);
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
            var statisticsKey = new EncodedObservationCacheKey("population-statistics", connection.NegotiatedVersion, revision, "global");
            _ = await connection.SendCachedAsync(statistics, connection.NegotiatedVersion, statisticsKey, cache, sendCancellation.Token);

            var currentInspection = connection.CaptureInspectionState();
            if (ObservationDeliveryPlanner.ShouldDeliverInspection(delivery.Inspection, currentInspection)
                && delivery.Inspection.PersonId is { } personId
                && snapshot.InspectedPersons.TryGetValue(personId, out var person))
            {
                var personMessage = cache.GetOrCreateEntity(
                    new EntityObservationCacheKey(EntityObservationKind.Person, personId, revision),
                    () => PopulationMessageMapper.Create(person));
                var personKey = new EncodedObservationCacheKey("person", connection.NegotiatedVersion, revision, ObservationCacheIdentity.ForEntity(personId));
                _ = await connection.SendCachedAsync(personMessage, connection.NegotiatedVersion, personKey, cache, sendCancellation.Token);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            connection.Abort();
            connections.Remove(connection.Id);
        }
    }
}
