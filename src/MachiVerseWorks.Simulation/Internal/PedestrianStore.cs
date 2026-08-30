namespace MachiVerseWorks.Simulation.Internal;

internal sealed class PedestrianStore
{
    private const double OccupancyBinLengthMeters = 0.75d;
    private readonly Dictionary<PedestrianId, PedestrianState> pedestrians = [];
    private readonly List<PedestrianId> orderedIds = [];
    private readonly Dictionary<(PedestrianEdgeId EdgeId, int Bin), PedestrianId> occupancy = [];
    private ulong nextId = 1;

    public int Count => pedestrians.Count;
    public int ActiveCount
    {
        get
        {
            var count = 0;
            foreach (var pedestrian in pedestrians.Values) if (pedestrian.State != PedestrianMovementState.Arrived) count++;
            return count;
        }
    }
    public ulong NextId => nextId;

    public PedestrianId Add(
        TripRequest request,
        PedestrianRoute route,
        double walkingSpeedMetersPerSecond,
        PedestrianNetworkStore network,
        PedestrianSpatialIndex spatialIndex)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(spatialIndex);
        if (request.Id.Value == 0) throw new ArgumentOutOfRangeException(nameof(request), "Trip Request ID must be greater than zero.");
        if (request.Mode is not (TravelMode.Any or TravelMode.Foot)) throw new ArgumentException("Pedestrians require a Foot or Any Trip Request.", nameof(request));
        if (!double.IsFinite(walkingSpeedMetersPerSecond) || walkingSpeedMetersPerSecond <= 0d) throw new ArgumentOutOfRangeException(nameof(walkingSpeedMetersPerSecond));
        if (nextId == ulong.MaxValue) throw new OverflowException("Pedestrian ID capacity has been exhausted.");
        var id = new PedestrianId(nextId++);
        var state = CreateState(id, request, route, walkingSpeedMetersPerSecond, 0, 0d, route.Legs.Count == 0 ? PedestrianMovementState.Arrived : PedestrianMovementState.Walking, network);
        spatialIndex.ValidatePosition(state.Position);
        pedestrians.Add(id, state);
        orderedIds.Add(id);
        spatialIndex.Register(id, state.Position);
        return id;
    }

    public bool Remove(PedestrianId id, PedestrianSpatialIndex spatialIndex)
    {
        ArgumentNullException.ThrowIfNull(spatialIndex);
        if (!pedestrians.Remove(id)) return false;
        if (!spatialIndex.Remove(id)) throw new InvalidOperationException($"Pedestrian {id.Value} was missing from the spatial index during removal.");
        return true;
    }

    public bool TryGetSnapshot(PedestrianId id, ulong tickCount, out PedestrianSnapshot snapshot)
    {
        if (!pedestrians.TryGetValue(id, out var pedestrian)) { snapshot = default; return false; }
        snapshot = ToSnapshot(pedestrian, tickCount);
        return true;
    }

    public PedestrianSnapshot[] CreateSnapshot(WorldVolume volume, PedestrianSpatialIndex spatialIndex, ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(spatialIndex);
        var candidates = spatialIndex.Query(volume);
        candidates.Sort(static (left, right) => left.Value.CompareTo(right.Value));
        var result = new List<PedestrianSnapshot>(candidates.Count);
        foreach (var id in candidates)
        {
            if (!pedestrians.TryGetValue(id, out var pedestrian)) continue;
            if (volume.Contains(pedestrian.Position)) result.Add(ToSnapshot(pedestrian, tickCount));
        }
        return result.ToArray();
    }

    public void Step(double deltaSeconds, PedestrianNetworkStore network, PedestrianSpatialIndex spatialIndex)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(spatialIndex);
        if (pedestrians.Count == 0) return;

        occupancy.Clear();
        foreach (var id in orderedIds)
        {
            if (!pedestrians.TryGetValue(id, out var pedestrian)) continue;
            if (pedestrian.State == PedestrianMovementState.Arrived || pedestrian.Route.Legs.Count == 0) continue;
            var key = GetOccupancyKey(pedestrian.CurrentLeg, pedestrian.ProgressMeters);
            if (!occupancy.TryAdd(key, pedestrian.Id) && occupancy[key].Value > pedestrian.Id.Value) occupancy[key] = pedestrian.Id;
        }

        foreach (var id in orderedIds)
        {
            if (!pedestrians.TryGetValue(id, out var pedestrian)) continue;
            if (pedestrian.State == PedestrianMovementState.Arrived || pedestrian.Route.Legs.Count == 0) continue;
            var oldKey = GetOccupancyKey(pedestrian.CurrentLeg, pedestrian.ProgressMeters);
            if (occupancy.TryGetValue(oldKey, out var oldOwner) && oldOwner == pedestrian.Id) occupancy.Remove(oldKey);
            var oldPosition = pedestrian.Position;
            StepPedestrian(pedestrian, deltaSeconds, network, occupancy);
            if (pedestrian.Position != oldPosition) spatialIndex.Update(pedestrian.Id, pedestrian.Position);
            if (pedestrian.State != PedestrianMovementState.Arrived)
            {
                var newKey = GetOccupancyKey(pedestrian.CurrentLeg, pedestrian.ProgressMeters);
                if (!occupancy.TryGetValue(newKey, out var newOwner) || newOwner == pedestrian.Id || newOwner.Value > pedestrian.Id.Value) occupancy[newKey] = pedestrian.Id;
            }
        }
    }

    public IReadOnlyList<SimulationPedestrianCheckpoint> CreateCheckpoint()
    {
        var result = new List<SimulationPedestrianCheckpoint>(pedestrians.Count);
        foreach (var id in orderedIds)
        {
            if (!pedestrians.TryGetValue(id, out var item)) continue;
            result.Add(new SimulationPedestrianCheckpoint(
                item.Id,
                item.Request.Id,
                item.Request.Origin,
                item.Request.Destination,
                item.Request.Mode,
                item.WalkingSpeedMetersPerSecond,
                item.LegIndex,
                item.ProgressMeters,
                item.State));
        }
        return result.ToArray();
    }

    public void Restore(
        IReadOnlyList<SimulationPedestrianCheckpoint> checkpoints,
        ulong restoredNextId,
        PedestrianNetworkStore network,
        PedestrianSpatialIndex spatialIndex)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(spatialIndex);
        pedestrians.Clear();
        orderedIds.Clear();
        occupancy.Clear();

        var orderedCheckpoints = checkpoints.ToArray();
        Array.Sort(orderedCheckpoints, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        foreach (var checkpoint in orderedCheckpoints)
        {
            var request = new TripRequest(checkpoint.TripRequestId, checkpoint.Origin, checkpoint.Destination, checkpoint.Mode);
            var route = network.FindRoute(checkpoint.Origin, checkpoint.Destination);
            var state = CreateState(checkpoint.Id, request, route, checkpoint.WalkingSpeedMetersPerSecond, checkpoint.LegIndex, checkpoint.ProgressMeters, checkpoint.State, network);
            spatialIndex.ValidatePosition(state.Position);
            pedestrians.Add(checkpoint.Id, state);
            orderedIds.Add(checkpoint.Id);
            spatialIndex.Register(checkpoint.Id, state.Position);
        }
        nextId = restoredNextId;
    }

    public bool ContainsBuildingReference(BuildingId id)
    {
        foreach (var pedestrian in pedestrians.Values)
            if (pedestrian.Request.Origin.BuildingId == id || pedestrian.Request.Destination.BuildingId == id) return true;
        return false;
    }

    public bool ContainsPoiReference(PoiId id)
    {
        foreach (var pedestrian in pedestrians.Values)
            if (pedestrian.Request.Origin.PoiId == id || pedestrian.Request.Destination.PoiId == id) return true;
        return false;
    }

    private static PedestrianState CreateState(PedestrianId id, TripRequest request, PedestrianRoute route, double speed, int legIndex, double progress, PedestrianMovementState movementState, PedestrianNetworkStore network)
    {
        if (id.Value == 0) throw new ArgumentOutOfRangeException(nameof(id));
        if (!double.IsFinite(speed) || speed <= 0d) throw new ArgumentOutOfRangeException(nameof(speed));
        if (!Enum.IsDefined(movementState)) throw new ArgumentOutOfRangeException(nameof(movementState));
        if (route.Legs.Count == 0)
        {
            if (legIndex != 0 || progress != 0d || movementState != PedestrianMovementState.Arrived) throw new ArgumentException("A zero-length pedestrian route must be arrived at leg zero with zero progress.");
            var position = network.GetNodePosition(route.EndNodeId);
            return new PedestrianState(id, request, route, speed, 0, 0d, PedestrianMovementState.Arrived, position, default);
        }
        if (legIndex < 0 || legIndex >= route.Legs.Count) throw new ArgumentOutOfRangeException(nameof(legIndex));
        var leg = route.Legs[legIndex];
        if (!double.IsFinite(progress) || progress < 0d || progress > leg.LengthMeters) throw new ArgumentOutOfRangeException(nameof(progress));
        var positionAtProgress = network.GetRoutePosition(leg, progress);
        var velocity = movementState == PedestrianMovementState.Walking ? network.GetRouteVelocity(leg, speed) : default;
        return new PedestrianState(id, request, route, speed, legIndex, progress, movementState, positionAtProgress, velocity);
    }

    private static void StepPedestrian(PedestrianState pedestrian, double deltaSeconds, PedestrianNetworkStore network, Dictionary<(PedestrianEdgeId EdgeId, int Bin), PedestrianId> occupancy)
    {
        var remainingDistance = pedestrian.WalkingSpeedMetersPerSecond * deltaSeconds;
        pedestrian.State = PedestrianMovementState.Walking;
        var guard = pedestrian.Route.Legs.Count + 1;
        while (remainingDistance > 0d && pedestrian.State != PedestrianMovementState.Arrived && guard-- > 0)
        {
            var leg = pedestrian.CurrentLeg;
            var distanceToEnd = Math.Max(0d, leg.LengthMeters - pedestrian.ProgressMeters);
            if (remainingDistance < distanceToEnd)
            {
                var targetProgress = pedestrian.ProgressMeters + remainingDistance;
                var targetKey = GetOccupancyKey(leg, targetProgress);
                if (occupancy.ContainsKey(targetKey)) { pedestrian.State = PedestrianMovementState.WaitingForOccupancy; pedestrian.Velocity = default; break; }
                pedestrian.ProgressMeters = targetProgress;
                pedestrian.Position = network.GetRoutePosition(leg, pedestrian.ProgressMeters);
                pedestrian.Velocity = network.GetRouteVelocity(leg, pedestrian.WalkingSpeedMetersPerSecond);
                remainingDistance = 0d;
                break;
            }

            pedestrian.ProgressMeters = leg.LengthMeters;
            pedestrian.Position = network.GetRoutePosition(leg, leg.LengthMeters);
            remainingDistance -= distanceToEnd;
            if (pedestrian.LegIndex == pedestrian.Route.Legs.Count - 1)
            {
                pedestrian.State = PedestrianMovementState.Arrived;
                pedestrian.Velocity = default;
                break;
            }

            var nextLeg = pedestrian.Route.Legs[pedestrian.LegIndex + 1];
            if (network.TryGetCrossing(leg.EdgeId, nextLeg.EdgeId, out var crossingId) && !network.IsCrossingOpen(crossingId))
            {
                pedestrian.State = PedestrianMovementState.WaitingForCrossing;
                pedestrian.Velocity = default;
                break;
            }

            var nextKey = GetOccupancyKey(nextLeg, 0d);
            if (occupancy.ContainsKey(nextKey))
            {
                pedestrian.State = PedestrianMovementState.WaitingForOccupancy;
                pedestrian.Velocity = default;
                break;
            }

            pedestrian.LegIndex++;
            pedestrian.ProgressMeters = 0d;
            pedestrian.Position = network.GetRoutePosition(nextLeg, 0d);
            pedestrian.Velocity = network.GetRouteVelocity(nextLeg, pedestrian.WalkingSpeedMetersPerSecond);
        }
    }

    private static (PedestrianEdgeId EdgeId, int Bin) GetOccupancyKey(PedestrianRouteLeg leg, double progress)
    {
        var canonicalProgress = leg.FromNodeId.Value <= leg.ToNodeId.Value
            ? progress
            : leg.LengthMeters - progress;
        canonicalProgress = Math.Clamp(canonicalProgress, 0d, leg.LengthMeters);
        var bin = (int)Math.Floor(canonicalProgress / OccupancyBinLengthMeters);
        return (leg.EdgeId, bin);
    }

    private static PedestrianSnapshot ToSnapshot(PedestrianState pedestrian, ulong tickCount) => new(
        pedestrian.Id,
        pedestrian.Request.Id,
        pedestrian.Position,
        pedestrian.Velocity,
        pedestrian.WalkingSpeedMetersPerSecond,
        pedestrian.State,
        tickCount);

    private sealed class PedestrianState(
        PedestrianId id,
        TripRequest request,
        PedestrianRoute route,
        double walkingSpeedMetersPerSecond,
        int legIndex,
        double progressMeters,
        PedestrianMovementState state,
        WorldPoint position,
        WorldVector velocity)
    {
        public PedestrianId Id { get; } = id;
        public TripRequest Request { get; } = request;
        public PedestrianRoute Route { get; } = route;
        public double WalkingSpeedMetersPerSecond { get; } = walkingSpeedMetersPerSecond;
        public int LegIndex { get; set; } = legIndex;
        public double ProgressMeters { get; set; } = progressMeters;
        public PedestrianMovementState State { get; set; } = state;
        public WorldPoint Position { get; set; } = position;
        public WorldVector Velocity { get; set; } = velocity;
        public PedestrianRouteLeg CurrentLeg => Route.Legs[LegIndex];
    }
}