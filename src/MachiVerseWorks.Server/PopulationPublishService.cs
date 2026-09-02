using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal readonly record struct PendingPopulationDelivery(
    ClientConnection Connection,
    ClientInspectionState Inspection,
    EntityInspectionSelection EntityInspection);

internal sealed class PopulationPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationCache cache,
    SnapshotDeliveryScheduler deliveryScheduler,
    EntityInspectionRegistry inspections) : BackgroundService
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
                        .SelectMany(static item => EnumeratePersonIds(item))
                        .ToHashSet();
                    var inspectedVehicleIds = pending
                        .Where(static item => item.EntityInspection.Target is { EntityType: ProtocolEntityType.Vehicle })
                        .Select(static item => item.EntityInspection.Target!.Value.EntityId)
                        .ToHashSet();
                    var inspectedTrainIds = pending
                        .Where(static item => item.EntityInspection.Target is { EntityType: ProtocolEntityType.Train })
                        .Select(static item => item.EntityInspection.Target!.Value.EntityId)
                        .ToHashSet();
                    var requiresRegional = pending.Any(static item => item.EntityInspection.Target is { EntityType: ProtocolEntityType.Settlement or ProtocolEntityType.Parcel or ProtocolEntityType.Building });

                    var snapshot = observationSource.CapturePopulationPublishSnapshot(inspectedIds);
                    var materializedBuildingIds = pending
                        .SelectMany(item => EnumerateMaterializedBuildingIds(item, snapshot))
                        .ToHashSet();
                    var generatedBuildingIds = observationSource.CaptureGeneratedBuildingIds(materializedBuildingIds);
                    var vehicles = observationSource.CaptureVehicleSnapshots(inspectedVehicleIds);
                    var trains = observationSource.CaptureTrainSnapshots(inspectedTrainIds);
                    PersistentRegionalEvolutionSnapshotMessage? regional = null;
                    if (requiresRegional && observationSource.CapturePersistentRegionalEvolutionSnapshot() is { } regionalSource)
                        regional = PersistentRegionalEvolutionMessageMapper.ToProtocol(regionalSource.Evolution, regionalSource.Interactions);

                    var statistics = PopulationMessageMapper.Create(snapshot.Statistics);
                    foreach (var delivery in pending)
                    {
                        deliveryScheduler.StartReserved(
                            delivery.Connection.Id,
                            () => PublishConnectionAsync(
                                delivery,
                                snapshot,
                                statistics,
                                vehicles,
                                trains,
                                generatedBuildingIds,
                                regional,
                                stoppingToken));
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
        inspections.Prune(candidates.Select(static connection => connection.Id).ToHashSet());
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
            var entityInspection = connection.NegotiatedVersion.SupportsEntityInspection
                ? inspections.Capture(connection.Id)
                : default;
            pending.Add(new PendingPopulationDelivery(connection, connection.CaptureInspectionState(), entityInspection));
        }
        return pending.ToArray();
    }

    private async Task PublishConnectionAsync(
        PendingPopulationDelivery delivery,
        PopulationPublishSnapshot snapshot,
        PopulationStatisticsMessage statistics,
        IReadOnlyDictionary<ulong, VehicleSnapshot> vehicles,
        IReadOnlyDictionary<ulong, TrainSnapshot> trains,
        IReadOnlyDictionary<ulong, ulong> generatedBuildingIds,
        PersistentRegionalEvolutionSnapshotMessage? regional,
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
                _ = await connection.SendCachedIfInspectionCurrentAsync(
                    personMessage,
                    connection.NegotiatedVersion,
                    personKey,
                    cache,
                    delivery.Inspection,
                    sendCancellation.Token);
            }

            if (delivery.EntityInspection.Target is { } target
                && inspections.IsCurrent(connection.Id, delivery.EntityInspection))
            {
                var entityMessage = EntityInspectionMessageMapper.Create(
                    target,
                    snapshot,
                    vehicles,
                    trains,
                    generatedBuildingIds,
                    regional);
                _ = await connection.SendIfEntityInspectionCurrentAsync(
                    entityMessage,
                    connection.NegotiatedVersion,
                    inspections,
                    delivery.EntityInspection,
                    sendCancellation.Token);
            }
        }
        catch (Exception exception) when (exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            connection.Abort();
            connections.Remove(connection.Id);
        }
    }

    private static IEnumerable<ulong> EnumeratePersonIds(PendingPopulationDelivery delivery)
    {
        if (delivery.Inspection.PersonId is { } legacyId) yield return legacyId;
        if (delivery.EntityInspection.Target is { EntityType: ProtocolEntityType.Person } target) yield return target.EntityId;
    }

    private static IEnumerable<ulong> EnumerateMaterializedBuildingIds(
        PendingPopulationDelivery delivery,
        PopulationPublishSnapshot snapshot)
    {
        if (delivery.EntityInspection.Target is not { EntityType: ProtocolEntityType.Person } target
            || !snapshot.InspectedPersons.TryGetValue(target.EntityId, out var person))
        {
            yield break;
        }

        var debug = PopulationMessageMapper.Create(person);
        if (debug.ResidenceBuildingId != 0) yield return debug.ResidenceBuildingId;
        if (debug.CurrentBuildingId != 0) yield return debug.CurrentBuildingId;
        if (debug.DestinationBuildingId != 0) yield return debug.DestinationBuildingId;
    }
}
