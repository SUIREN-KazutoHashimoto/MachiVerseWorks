using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed record DynamicObservationDeliveryPlan(
    SnapshotMessagePlan Agents,
    PedestrianSnapshotMessagePlan Pedestrians,
    VehicleSnapshotMessagePlan Vehicles,
    bool RequiresGenerationResync);

internal readonly record struct StaticObservationDeliveryPlan(
    bool SendRoadSnapshot,
    bool SendRailwaySnapshot);

internal static class ObservationDeliveryPlanner
{
    public static DynamicObservationDeliveryPlan CreateDynamicPlan(
        EntityPublishSnapshot snapshot,
        ClientSubscriptionState subscription,
        ProtocolVersion version,
        ulong observationGeneration)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var requiresGenerationResync = RequiresGenerationResync(subscription, observationGeneration);
        var agentPlan = SnapshotMessagePlanner.Create(
            snapshot.Agents,
            subscription.KnownAgentIds,
            snapshot.TickCount,
            requiresGenerationResync);
        var pedestrianPlan = version.SupportsPedestrians
            ? PedestrianSnapshotMessagePlanner.Create(
                snapshot.Pedestrians,
                subscription.KnownPedestrianIds,
                snapshot.TickCount,
                requiresGenerationResync)
            : new PedestrianSnapshotMessagePlan([], []);
        var vehiclePlan = version.SupportsVehicles
            ? VehicleSnapshotMessagePlanner.Create(
                snapshot.Vehicles,
                subscription.KnownVehicleIds,
                snapshot.TickCount,
                requiresGenerationResync)
            : new VehicleSnapshotMessagePlan([], []);

        return new DynamicObservationDeliveryPlan(agentPlan, pedestrianPlan, vehiclePlan, requiresGenerationResync);
    }

    public static StaticObservationDeliveryPlan CreateStaticPlan(
        ClientSubscriptionState subscription,
        ProtocolVersion version,
        ulong observationGeneration,
        ulong roadRevision,
        ulong railwayRevision)
    {
        return new StaticObservationDeliveryPlan(
            ShouldSendStaticSnapshot(
                version.SupportsRoadNetwork,
                subscription.RoadDelivery,
                subscription.Revision,
                observationGeneration,
                roadRevision),
            ShouldSendStaticSnapshot(
                version.SupportsRailwayInfrastructure,
                subscription.RailwayDelivery,
                subscription.Revision,
                observationGeneration,
                railwayRevision));
    }

    public static bool RequiresGenerationResync(ClientSubscriptionState subscription, ulong observationGeneration)
    {
        return subscription.CommittedDelivery is { } committed
            && committed.ObservationGeneration != observationGeneration;
    }

    public static bool ShouldSendStaticSnapshot(
        bool protocolSupported,
        StaticDeliveryRevision? committedDelivery,
        long subscriptionRevision,
        ulong observationGeneration,
        ulong sourceRevision)
    {
        if (!protocolSupported) return false;
        return committedDelivery is not { } delivered
            || delivered.SubscriptionRevision != subscriptionRevision
            || delivered.ObservationGeneration != observationGeneration
            || delivered.SourceRevision != sourceRevision;
    }

    public static bool ShouldDeliverInspection(ClientInspectionState planned, ClientInspectionState current)
    {
        return planned.PersonId.HasValue && planned == current;
    }
}
