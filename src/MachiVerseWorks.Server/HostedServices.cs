using System.Globalization;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationTickService(SimulationRuntime simulation, ILogger<SimulationTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SimulationTickStarted(logger, simulation.TickRate);
        using var timer = new PeriodicTimer(simulation.TickInterval);
        try { while (await timer.WaitForNextTickAsync(stoppingToken)) simulation.Step(); }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        ServerLog.SimulationTickStopped(logger, simulation.TickCount);
    }
}

internal sealed record SnapshotMessagePlan(IReadOnlyList<IProtocolMessage> Messages, HashSet<ulong> CurrentAgentIds);
internal static class SnapshotMessagePlanner
{
    public static SnapshotMessagePlan Create(AgentSnapshot[] snapshots, IReadOnlySet<ulong> knownAgentIds, ulong tickCount, bool forceFullSnapshot = false)
    {
        ArgumentNullException.ThrowIfNull(snapshots); ArgumentNullException.ThrowIfNull(knownAgentIds);
        var orderedSnapshots = snapshots.ToArray();
        Array.Sort(orderedSnapshots, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var current = new HashSet<ulong>(orderedSnapshots.Length); var messages = new List<IProtocolMessage>(orderedSnapshots.Length + knownAgentIds.Count);
        if (forceFullSnapshot)
        {
            var removals = knownAgentIds.ToArray();
            Array.Sort(removals);
            foreach (var id in removals) messages.Add(new AgentRemoveMessage(id, tickCount));
            foreach (var snapshot in orderedSnapshots)
            {
                var id = snapshot.Id.Value; current.Add(id);
                messages.Add(new AgentSpawnMessage(id, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount));
            }
            return new SnapshotMessagePlan(messages, current);
        }
        foreach (var snapshot in orderedSnapshots)
        {
            var id = snapshot.Id.Value; current.Add(id);
            messages.Add(knownAgentIds.Contains(id) ? new AgentUpdateMessage(id, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount) : new AgentSpawnMessage(id, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount));
        }
        foreach (var id in knownAgentIds) if (!current.Contains(id)) messages.Add(new AgentRemoveMessage(id, tickCount));
        return new SnapshotMessagePlan(messages, current);
    }
}

internal sealed record PedestrianSnapshotMessagePlan(IReadOnlyList<IProtocolMessage> Messages, HashSet<ulong> CurrentPedestrianIds);
internal static class PedestrianSnapshotMessagePlanner
{
    public static PedestrianSnapshotMessagePlan Create(PedestrianSnapshot[] snapshots, IReadOnlySet<ulong> knownPedestrianIds, ulong tickCount, bool forceFullSnapshot = false)
    {
        ArgumentNullException.ThrowIfNull(snapshots); ArgumentNullException.ThrowIfNull(knownPedestrianIds);
        var orderedSnapshots = snapshots.ToArray();
        Array.Sort(orderedSnapshots, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var current = new HashSet<ulong>(orderedSnapshots.Length); var messages = new List<IProtocolMessage>(orderedSnapshots.Length + knownPedestrianIds.Count);
        if (forceFullSnapshot)
        {
            var removals = knownPedestrianIds.ToArray();
            Array.Sort(removals);
            foreach (var id in removals) messages.Add(new PedestrianRemoveMessage(id, tickCount));
            foreach (var snapshot in orderedSnapshots)
            {
                var id = snapshot.Id.Value; current.Add(id); var state = (ProtocolPedestrianMovementState)snapshot.State;
                messages.Add(new PedestrianSpawnMessage(id, snapshot.TripRequestId.Value, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.WalkingSpeedMetersPerSecond, state, snapshot.TickCount));
            }
            return new PedestrianSnapshotMessagePlan(messages, current);
        }
        foreach (var snapshot in orderedSnapshots)
        {
            var id = snapshot.Id.Value; current.Add(id); var state = (ProtocolPedestrianMovementState)snapshot.State;
            messages.Add(knownPedestrianIds.Contains(id)
                ? new PedestrianUpdateMessage(id, snapshot.TripRequestId.Value, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.WalkingSpeedMetersPerSecond, state, snapshot.TickCount)
                : new PedestrianSpawnMessage(id, snapshot.TripRequestId.Value, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.WalkingSpeedMetersPerSecond, state, snapshot.TickCount));
        }
        foreach (var id in knownPedestrianIds) if (!current.Contains(id)) messages.Add(new PedestrianRemoveMessage(id, tickCount));
        return new PedestrianSnapshotMessagePlan(messages, current);
    }
}

internal static class RoadSnapshotMessagePlanner
{
    public const string TooLargeDetailCode = "roadSnapshotTooLarge";
    public static IProtocolMessage Create(RoadNetworkSnapshot snapshot, ulong tickCount)
    {
        var message = RoadNetworkMessageMapper.Create(snapshot, tickCount);
        if (ProtocolCodec.FitsSingleFrame(message)) return message;
        return new ProtocolErrorMessage(ProtocolErrorCode.InvalidRequest,
        [
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "volume"),
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, TooLargeDetailCode),
            new ProtocolErrorParameter("payloadBytes", ProtocolCodec.GetPayloadLength(message).ToString(CultureInfo.InvariantCulture)),
            new ProtocolErrorParameter("maximumPayloadBytes", ProtocolFrameHeader.MaxPayloadLength.ToString(CultureInfo.InvariantCulture)),
        ]);
    }
}

internal static class RailwayOperationsSnapshotMessagePlanner
{
    public const string TooLargeDetailCode = "railwayOperationsSnapshotTooLarge";

    public static IProtocolMessage Create(RailwayOperationsSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payloadLength = RailwayOperationsProtocolCodec.GetPayloadLength(message);
        if ((ulong)payloadLength <= ProtocolFrameHeader.MaxPayloadLength) return message;
        return new ProtocolErrorMessage(ProtocolErrorCode.InvalidRequest,
        [
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "volume"),
            new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, TooLargeDetailCode),
            new ProtocolErrorParameter("payloadBytes", payloadLength.ToString(CultureInfo.InvariantCulture)),
            new ProtocolErrorParameter("maximumPayloadBytes", ProtocolFrameHeader.MaxPayloadLength.ToString(CultureInfo.InvariantCulture)),
        ]);
    }
}

internal readonly record struct PendingSnapshotDelivery(ClientConnection Connection, ClientSubscriptionState Subscription);

internal sealed class SnapshotPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationCache cache,
    SnapshotDeliveryScheduler deliveryScheduler,
    E2eMetrics metrics,
    ILogger<SnapshotPublishService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SnapshotPublisherStarted(logger, options.SnapshotRate);
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
                    var captureVolume = CalculateCaptureVolume(pending);
                    var publishSnapshot = observationSource.CapturePublishSnapshot(captureVolume);
                    SchedulePublish(publishSnapshot, pending, stoppingToken);
                }
                catch { ReleaseReservations(pending); throw; }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        deliveryScheduler.ThrowIfFaulted();
        var inFlight = deliveryScheduler.CreateInFlightSnapshot();
        if (inFlight.Length > 0) await Task.WhenAll(inFlight);
        deliveryScheduler.ThrowIfFaulted(); ServerLog.SnapshotPublisherStopped(logger);
    }

    private PendingSnapshotDelivery[] CapturePendingDeliveries()
    {
        var candidates = connections.CreateSnapshot(); var pending = new List<PendingSnapshotDelivery>(candidates.Length);
        foreach (var connection in candidates)
        {
            if (!connection.HandshakeCompleted || connection.Socket.State != WebSocketState.Open || !connection.TryCaptureSubscription(out var subscription)) continue;
            if (!deliveryScheduler.TryReserve(connection.Id)) continue;
            pending.Add(new PendingSnapshotDelivery(connection, subscription));
        }
        return pending.ToArray();
    }

    private static WorldVolume CalculateCaptureVolume(PendingSnapshotDelivery[] pending)
    {
        if (pending.Length == 0) throw new ArgumentException("At least one pending delivery is required.", nameof(pending));
        var first = pending[0].Subscription.Volume;
        var minX = first.MinX; var minY = first.MinY; var minZ = first.MinZ;
        var maxX = first.MaxX; var maxY = first.MaxY; var maxZ = first.MaxZ;
        for (var index = 1; index < pending.Length; index++)
        {
            var volume = pending[index].Subscription.Volume;
            minX = Math.Min(minX, volume.MinX); minY = Math.Min(minY, volume.MinY); minZ = Math.Min(minZ, volume.MinZ);
            maxX = Math.Max(maxX, volume.MaxX); maxY = Math.Max(maxY, volume.MaxY); maxZ = Math.Max(maxZ, volume.MaxZ);
        }
        return new WorldVolume(minX, minY, minZ, maxX, maxY, maxZ);
    }

    private void SchedulePublish(SimulationPublishSnapshot publishSnapshot, PendingSnapshotDelivery[] pending, CancellationToken cancellationToken)
    {
        foreach (var delivery in pending) deliveryScheduler.StartReserved(delivery.Connection.Id, () => PublishConnectionAsync(delivery.Connection, delivery.Subscription, publishSnapshot, cancellationToken));
    }
    private void ReleaseReservations(PendingSnapshotDelivery[] pending) { foreach (var delivery in pending) deliveryScheduler.ReleaseReservation(delivery.Connection.Id); }

    private async Task PublishConnectionAsync(ClientConnection connection, ClientSubscriptionState subscription, SimulationPublishSnapshot publishSnapshot, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            var revision = new ObservationRevision(publishSnapshot.ObservationGeneration, publishSnapshot.ObservationRevision);
            var volumeIdentity = ObservationCacheIdentity.ForVolume(subscription.Volume);
            var snapshot = cache.GetOrCreateSpatial(
                new SpatialObservationCacheKey(SpatialObservationKind.Entities, subscription.Volume, revision),
                () => publishSnapshot.QueryEntities(subscription.Volume));
            var dynamicPlan = ObservationDeliveryPlanner.CreateDynamicPlan(snapshot, subscription, connection.NegotiatedVersion, publishSnapshot.ObservationGeneration);
            var agentPlan = dynamicPlan.Agents;
            var pedestrianPlan = dynamicPlan.Pedestrians;
            var vehiclePlan = dynamicPlan.Vehicles;
            var staticPlan = ObservationDeliveryPlanner.CreateStaticPlan(
                subscription,
                connection.NegotiatedVersion,
                publishSnapshot.ObservationGeneration,
                publishSnapshot.RoadNetwork.Revision,
                publishSnapshot.RailwayInfrastructure.Revision);
            var intersectionMessages = connection.NegotiatedVersion.SupportsIntersectionControl ? snapshot.Intersections.Select(IntersectionControlMessageMapper.Create).ToArray() : [];
            IProtocolMessage? railwayOperationsMessage = null;
            if (connection.NegotiatedVersion.SupportsRailwayOperations)
            {
                var mappedRailwayOperations = RailwayOperationsMessageMapper.Create(publishSnapshot.RailwayOperations, snapshot.Trains, snapshot.TickCount);
                railwayOperationsMessage = RailwayOperationsSnapshotMessagePlanner.Create(mappedRailwayOperations);
            }
            var multimodalTransitMessage = connection.NegotiatedVersion.SupportsMultimodalTransit && (publishSnapshot.MultimodalTransit.Lines.Length > 0 || publishSnapshot.MultimodalTransit.Vehicles.Length > 0)
                ? MultimodalTransitMessageMapper.Create(publishSnapshot.MultimodalTransit, snapshot.TickCount)
                : null;

            IProtocolMessage? roadMessage = null; var roadStateHandled = false;
            if (staticPlan.SendRoadSnapshot)
            {
                var roadRevision = new ObservationRevision(publishSnapshot.ObservationGeneration, publishSnapshot.RoadNetwork.Revision);
                var roadSnapshot = cache.GetOrCreateStatic(
                    new StaticObservationCacheKey(StaticObservationKind.Road, subscription.Volume, roadRevision),
                    () => publishSnapshot.RoadNetwork.Query(subscription.Volume));
                roadMessage = RoadSnapshotMessagePlanner.Create(roadSnapshot, snapshot.TickCount); roadStateHandled = true;
            }

            IReadOnlyList<RailwayInfrastructureSnapshotMessage> railwayMessages = []; var railwayStateHandled = false;
            if (staticPlan.SendRailwaySnapshot)
            {
                var railwayRevision = new ObservationRevision(publishSnapshot.ObservationGeneration, publishSnapshot.RailwayInfrastructure.Revision);
                var railwaySnapshot = cache.GetOrCreateStatic(
                    new StaticObservationCacheKey(StaticObservationKind.Railway, subscription.Volume, railwayRevision),
                    () => publishSnapshot.RailwayInfrastructure.Query(subscription.Volume));
                var railwayMessage = RailwayInfrastructureMessageMapper.Create(railwaySnapshot, publishSnapshot.RailwayInfrastructure.Revision);
                railwayMessages = RailwayInfrastructureProtocolChunker.Split(railwayMessage); railwayStateHandled = true;
            }

            long bytes = 0; double encodeTimeMs = 0; double sendTimeMs = 0;
            var messageCount = agentPlan.Messages.Count + pedestrianPlan.Messages.Count + vehiclePlan.Messages.Count + intersectionMessages.Length + (roadMessage is null ? 0 : 1) + railwayMessages.Count + (railwayOperationsMessage is null ? 0 : 1) + (multimodalTransitMessage is null ? 0 : 1);
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
            foreach (var message in agentPlan.Messages) { var sent = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token); bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs; }
            foreach (var message in pedestrianPlan.Messages) { var sent = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token); bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs; }
            foreach (var message in vehiclePlan.Messages) { var sent = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token); bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs; }
            for (var index = 0; index < intersectionMessages.Length; index++)
            {
                var key = new EncodedObservationCacheKey("intersection", connection.NegotiatedVersion, revision, ObservationCacheIdentity.ForChunk(volumeIdentity, index));
                var sent = await connection.SendCachedAsync(intersectionMessages[index], connection.NegotiatedVersion, key, cache, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            if (roadMessage is not null)
            {
                var key = new EncodedObservationCacheKey($"road:{publishSnapshot.RoadNetwork.Revision}", connection.NegotiatedVersion, revision, volumeIdentity);
                var sent = await connection.SendCachedAsync(roadMessage, connection.NegotiatedVersion, key, cache, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            for (var index = 0; index < railwayMessages.Count; index++)
            {
                var railwayRevision = new ObservationRevision(publishSnapshot.ObservationGeneration, publishSnapshot.RailwayInfrastructure.Revision);
                var key = new EncodedObservationCacheKey("railway", connection.NegotiatedVersion, railwayRevision, ObservationCacheIdentity.ForChunk(volumeIdentity, index), IsStatic: true);
                var sent = await connection.SendCachedAsync(railwayMessages[index], connection.NegotiatedVersion, key, cache, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            if (railwayOperationsMessage is not null)
            {
                var key = new EncodedObservationCacheKey("railway-operations", connection.NegotiatedVersion, revision, volumeIdentity);
                var sent = await connection.SendCachedAsync(railwayOperationsMessage, connection.NegotiatedVersion, key, cache, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            if (multimodalTransitMessage is not null)
            {
                var key = new EncodedObservationCacheKey("multimodal-transit", connection.NegotiatedVersion, revision, "global");
                var sent = await connection.SendCachedAsync(multimodalTransitMessage, connection.NegotiatedVersion, key, cache, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }

            connection.TryReplaceKnownEntityIds(
                subscription.Revision,
                publishSnapshot.ObservationGeneration,
                publishSnapshot.ObservationRevision,
                agentPlan.CurrentAgentIds,
                pedestrianPlan.CurrentPedestrianIds,
                vehiclePlan.CurrentVehicleIds);
            if (roadStateHandled) connection.TryMarkRoadSnapshotDelivered(subscription.Revision, publishSnapshot.ObservationGeneration, publishSnapshot.RoadNetwork.Revision);
            if (railwayStateHandled) connection.TryMarkRailwaySnapshotDelivered(subscription.Revision, publishSnapshot.ObservationGeneration, publishSnapshot.RailwayInfrastructure.Revision);
            metrics.RecordSnapshotDelivery(snapshot.Agents.Length, snapshot.Pedestrians.Length, snapshot.Vehicles.Length, snapshot.Trains.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);
            var entityCount = checked(snapshot.Agents.Length + snapshot.Pedestrians.Length + snapshot.Vehicles.Length + snapshot.Trains.Length);
            ServerLog.SnapshotDeliveryMetrics(logger, connection.Id, snapshot.Agents.Length, snapshot.Pedestrians.Length, snapshot.Vehicles.Length, snapshot.Trains.Length, entityCount, messageCount, bytes, encodeTimeMs, sendTimeMs);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            if (!cancellationToken.IsCancellationRequested) ServerLog.SnapshotDeliveryStopped(logger, connection.Id, exception);
            connection.Abort(); connections.Remove(connection.Id);
        }
        catch (Exception exception) { ServerLog.UnexpectedSnapshotDeliveryFailure(logger, connection.Id, exception); throw; }
    }
}
