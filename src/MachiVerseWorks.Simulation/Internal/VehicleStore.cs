namespace MachiVerseWorks.Simulation.Internal;

internal sealed class VehicleStore
{
    private const double QueueSpeedThresholdMetersPerSecond = 0.5d;
    private readonly Dictionary<VehicleId, VehicleState> vehicles = [];
    private readonly List<VehicleId> orderedIds = [];
    private readonly LaneOccupancyIndex occupancy = new();
    private ulong nextId = 1;

    public int Count => vehicles.Count;
    public int ActiveCount
    {
        get
        {
            var count = 0;
            foreach (var vehicle in vehicles.Values) if (vehicle.State != VehicleMovementState.Arrived) count++;
            return count;
        }
    }
    public ulong NextId => nextId;

    public VehicleId Add(
        IReadOnlyList<RouteLaneStep> route,
        VehicleDimensions dimensions,
        VehiclePerformance performance,
        double initialSpeedMetersPerSecond,
        RoadTrafficTopology topology)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(topology);
        ValidateDimensions(dimensions);
        ValidatePerformance(performance);
        if (!double.IsFinite(initialSpeedMetersPerSecond) || initialSpeedMetersPerSecond < 0d) throw new ArgumentOutOfRangeException(nameof(initialSpeedMetersPerSecond));
        topology.ValidateRoute(route);
        if (nextId == ulong.MaxValue) throw new OverflowException("Vehicle ID capacity has been exhausted.");
        var routeSteps = route.ToArray();
        var id = new VehicleId(nextId);
        var firstLane = topology.GetLane(routeSteps[0].LaneId);
        var boundedInitialSpeed = Math.Min(initialSpeedMetersPerSecond, Math.Min(performance.MaximumSpeedMetersPerSecond, firstLane.Snapshot.SpeedLimitMetersPerSecond));
        var state = CreateState(id, routeSteps, dimensions, performance, 0, 0d, boundedInitialSpeed, VehicleMovementState.Driving, topology);
        var laneProgress = topology.GetLaneTravelProgress(state.CurrentStep.LaneId, state.SegmentOffset);
        if (!occupancy.CanOccupy(state.CurrentStep.LaneId, laneProgress, dimensions.LengthMeters, performance.MinimumGapMeters))
            throw new InvalidOperationException("Vehicle spawn position does not have a safe Lane gap.");
        vehicles.Add(id, state);
        orderedIds.Add(id);
        occupancy.Add(id, state.CurrentStep.LaneId, laneProgress, dimensions.LengthMeters, state.SpeedMetersPerSecond);
        nextId++;
        return id;
    }

    public bool Remove(VehicleId id)
    {
        if (!vehicles.TryGetValue(id, out var vehicle)) return false;
        if (vehicle.State != VehicleMovementState.Arrived && !occupancy.Remove(id))
            throw new InvalidOperationException($"Vehicle {id.Value} was missing from Lane occupancy during removal.");
        if (!orderedIds.Remove(id)) throw new InvalidOperationException($"Vehicle {id.Value} was missing from deterministic iteration order during removal.");
        return vehicles.Remove(id);
    }

    public bool TryGetSnapshot(VehicleId id, ulong tickCount, out VehicleSnapshot snapshot)
    {
        if (!vehicles.TryGetValue(id, out var vehicle)) { snapshot = default; return false; }
        snapshot = ToSnapshot(vehicle, tickCount);
        return true;
    }

    public VehicleSnapshot[] CreateSnapshot(WorldVolume volume, ulong tickCount)
    {
        var result = new List<VehicleSnapshot>();
        foreach (var id in orderedIds)
        {
            var vehicle = vehicles[id];
            if (!volume.Contains(vehicle.Position)) continue;
            result.Add(ToSnapshot(vehicle, tickCount));
        }
        return result.ToArray();
    }

    public VehicleSnapshot[] CreateAllSnapshots(ulong tickCount)
    {
        var result = new VehicleSnapshot[orderedIds.Count];
        for (var index = 0; index < orderedIds.Count; index++) result[index] = ToSnapshot(vehicles[orderedIds[index]], tickCount);
        return result;
    }

    public void Step(
        double deltaSeconds,
        ulong tickCount,
        RoadTrafficTopology topology,
        IntersectionControlStore intersectionControl)
    {
        ArgumentNullException.ThrowIfNull(topology);
        ArgumentNullException.ThrowIfNull(intersectionControl);
        if (!double.IsFinite(deltaSeconds) || deltaSeconds <= 0d) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));

        var intents = CollectIntersectionIntents(deltaSeconds, topology);
        intersectionControl.PrepareTick(tickCount, intents);
        if (vehicles.Count == 0) return;

        foreach (var id in orderedIds)
        {
            var vehicle = vehicles[id];
            if (vehicle.State == VehicleMovementState.Arrived) continue;
            if (!occupancy.Remove(id)) throw new InvalidOperationException($"Vehicle {id.Value} was missing from Lane occupancy before update.");
            StepVehicle(vehicle, deltaSeconds, topology, occupancy, intersectionControl);
            ValidateState(vehicle, topology);
            if (vehicle.State == VehicleMovementState.Arrived) continue;
            var laneProgress = topology.GetLaneTravelProgress(vehicle.CurrentStep.LaneId, vehicle.SegmentOffset);
            if (!occupancy.CanOccupy(vehicle.CurrentStep.LaneId, laneProgress, vehicle.Dimensions.LengthMeters, 0d))
                throw new InvalidOperationException($"Vehicle {id.Value} would overlap another Vehicle after update.");
            occupancy.Add(id, vehicle.CurrentStep.LaneId, laneProgress, vehicle.Dimensions.LengthMeters, vehicle.SpeedMetersPerSecond);
        }
    }

    public TrafficMetrics CreateMetrics(RoadTrafficTopology topology)
    {
        ArgumentNullException.ThrowIfNull(topology);
        var active = 0;
        var queued = 0;
        var speedSum = 0d;
        foreach (var vehicle in vehicles.Values)
        {
            if (vehicle.State == VehicleMovementState.Arrived) continue;
            active++;
            speedSum += vehicle.SpeedMetersPerSecond;
            if (vehicle.State == VehicleMovementState.WaitingForTraffic || vehicle.SpeedMetersPerSecond <= QueueSpeedThresholdMetersPerSecond) queued++;
        }
        var kilometers = topology.TotalLaneLengthMeters / 1000d;
        return new TrafficMetrics(
            vehicles.Count,
            active,
            kilometers,
            kilometers > 0d ? active / kilometers : 0d,
            active > 0 ? speedSum / active : 0d,
            queued);
    }

    public IReadOnlyList<SimulationVehicleCheckpoint> CreateCheckpoint()
    {
        var result = new SimulationVehicleCheckpoint[orderedIds.Count];
        for (var index = 0; index < orderedIds.Count; index++)
        {
            var vehicle = vehicles[orderedIds[index]];
            result[index] = new SimulationVehicleCheckpoint(
                vehicle.Id,
                vehicle.Dimensions,
                vehicle.Performance,
                vehicle.RouteSteps,
                vehicle.RouteStepIndex,
                vehicle.RouteProgressMeters,
                vehicle.SpeedMetersPerSecond,
                vehicle.State);
        }
        return result;
    }

    public void Restore(IReadOnlyList<SimulationVehicleCheckpoint> checkpoints, ulong restoredNextId, RoadTrafficTopology topology)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        ArgumentNullException.ThrowIfNull(topology);
        vehicles.Clear();
        orderedIds.Clear();
        occupancy.Clear();
        var ordered = checkpoints.ToArray();
        Array.Sort(ordered, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        foreach (var checkpoint in ordered)
        {
            var route = checkpoint.RouteSteps?.ToArray() ?? throw new ArgumentException("Vehicle checkpoint is missing Route steps.", nameof(checkpoints));
            topology.ValidateRoute(route);
            var state = CreateState(
                checkpoint.Id,
                route,
                checkpoint.Dimensions,
                checkpoint.Performance,
                checkpoint.RouteStepIndex,
                checkpoint.RouteProgressMeters,
                checkpoint.SpeedMetersPerSecond,
                checkpoint.State,
                topology);
            if (state.State != VehicleMovementState.Arrived)
            {
                var laneProgress = topology.GetLaneTravelProgress(state.CurrentStep.LaneId, state.SegmentOffset);
                if (!occupancy.CanOccupy(state.CurrentStep.LaneId, laneProgress, state.Dimensions.LengthMeters, 0d))
                    throw new ArgumentException($"Vehicle checkpoint {checkpoint.Id.Value} overlaps another Vehicle.", nameof(checkpoints));
                occupancy.Add(state.Id, state.CurrentStep.LaneId, laneProgress, state.Dimensions.LengthMeters, state.SpeedMetersPerSecond);
            }
            vehicles.Add(state.Id, state);
            orderedIds.Add(state.Id);
        }
        nextId = restoredNextId;
    }

    private List<IntersectionEntryIntent> CollectIntersectionIntents(double deltaSeconds, RoadTrafficTopology topology)
    {
        var result = new List<IntersectionEntryIntent>();
        foreach (var id in orderedIds)
        {
            var vehicle = vehicles[id];
            if (vehicle.State == VehicleMovementState.Arrived || vehicle.RouteStepIndex >= vehicle.RouteSteps.Length - 1) continue;
            var step = vehicle.CurrentStep;
            if (step.ExitConnectionId is not { } connectionId) continue;

            var requestedAdvance = CalculateRequestedAdvance(vehicle, deltaSeconds, topology, occupancy);
            var remaining = Math.Max(0d, step.DistanceMeters - vehicle.RouteProgressMeters);
            if (requestedAdvance + 1e-9 < remaining) continue;

            var next = vehicle.RouteSteps[vehicle.RouteStepIndex + 1];
            var targetLaneProgress = topology.GetLaneTravelProgress(next.LaneId, next.StartSegmentOffset);
            var downstreamAvailable = occupancy.CanOccupy(
                next.LaneId,
                targetLaneProgress,
                vehicle.Dimensions.LengthMeters,
                vehicle.Performance.MinimumGapMeters);
            result.Add(new IntersectionEntryIntent(vehicle.Id, connectionId, downstreamAvailable));
        }
        return result;
    }

    private static double CalculateRequestedAdvance(
        VehicleState vehicle,
        double deltaSeconds,
        RoadTrafficTopology topology,
        LaneOccupancyIndex occupancyIndex)
    {
        var lane = topology.GetLane(vehicle.CurrentStep.LaneId);
        var targetSpeed = Math.Min(vehicle.Performance.MaximumSpeedMetersPerSecond, lane.Snapshot.SpeedLimitMetersPerSecond);
        var laneProgress = topology.GetLaneTravelProgress(vehicle.CurrentStep.LaneId, vehicle.SegmentOffset);
        var maximumAdvance = double.PositiveInfinity;
        if (occupancyIndex.TryGetLeader(vehicle.CurrentStep.LaneId, laneProgress, out var leader))
        {
            var bumperGap = leader.ProgressMeters - laneProgress - (leader.LengthMeters + vehicle.Dimensions.LengthMeters) * 0.5d;
            var freeGap = Math.Max(0d, bumperGap - vehicle.Performance.MinimumGapMeters);
            targetSpeed = Math.Min(targetSpeed, leader.SpeedMetersPerSecond + freeGap / vehicle.Performance.TimeHeadwaySeconds);
            maximumAdvance = freeGap;
        }
        var speed = ApproachSpeed(vehicle.SpeedMetersPerSecond, targetSpeed, vehicle.Performance, deltaSeconds);
        var requestedAdvance = Math.Max(0d, speed * deltaSeconds);
        return double.IsFinite(maximumAdvance) ? Math.Min(requestedAdvance, maximumAdvance) : requestedAdvance;
    }

    private static void StepVehicle(
        VehicleState vehicle,
        double deltaSeconds,
        RoadTrafficTopology topology,
        LaneOccupancyIndex occupancyIndex,
        IntersectionControlStore intersectionControl)
    {
        if (vehicle.State == VehicleMovementState.Arrived) { vehicle.SpeedMetersPerSecond = 0d; UpdatePose(vehicle, topology); return; }
        vehicle.State = VehicleMovementState.Driving;
        var requestedAdvance = CalculateRequestedAdvance(vehicle, deltaSeconds, topology, occupancyIndex);
        if (requestedAdvance <= 1e-9)
        {
            vehicle.SpeedMetersPerSecond = 0d;
            vehicle.State = VehicleMovementState.WaitingForTraffic;
            UpdatePose(vehicle, topology);
            return;
        }

        var lane = topology.GetLane(vehicle.CurrentStep.LaneId);
        var targetSpeed = Math.Min(vehicle.Performance.MaximumSpeedMetersPerSecond, lane.Snapshot.SpeedLimitMetersPerSecond);
        var speed = ApproachSpeed(vehicle.SpeedMetersPerSecond, targetSpeed, vehicle.Performance, deltaSeconds);
        var moved = AdvanceRoute(vehicle, requestedAdvance, topology, occupancyIndex, intersectionControl);
        if (vehicle.State == VehicleMovementState.Arrived || vehicle.State == VehicleMovementState.WaitingForTraffic)
        {
            vehicle.SpeedMetersPerSecond = 0d;
        }
        else if (moved <= 1e-9)
        {
            vehicle.SpeedMetersPerSecond = 0d;
            vehicle.State = VehicleMovementState.WaitingForTraffic;
        }
        else
        {
            vehicle.SpeedMetersPerSecond = Math.Min(speed, moved / deltaSeconds);
        }
        UpdatePose(vehicle, topology);
    }

    private static double AdvanceRoute(
        VehicleState vehicle,
        double distanceMeters,
        RoadTrafficTopology topology,
        LaneOccupancyIndex occupancyIndex,
        IntersectionControlStore intersectionControl)
    {
        var original = distanceMeters;
        var guard = vehicle.RouteSteps.Length + 1;
        while (distanceMeters > 1e-9 && vehicle.State != VehicleMovementState.Arrived && guard-- > 0)
        {
            var step = vehicle.CurrentStep;
            var remaining = Math.Max(0d, step.DistanceMeters - vehicle.RouteProgressMeters);
            if (distanceMeters < remaining - 1e-9)
            {
                vehicle.RouteProgressMeters += distanceMeters;
                distanceMeters = 0d;
                break;
            }

            vehicle.RouteProgressMeters = step.DistanceMeters;
            distanceMeters = Math.Max(0d, distanceMeters - remaining);
            if (vehicle.RouteStepIndex == vehicle.RouteSteps.Length - 1)
            {
                vehicle.State = VehicleMovementState.Arrived;
                break;
            }

            var next = vehicle.RouteSteps[vehicle.RouteStepIndex + 1];
            var targetLaneProgress = topology.GetLaneTravelProgress(next.LaneId, next.StartSegmentOffset);
            if (!occupancyIndex.CanOccupy(next.LaneId, targetLaneProgress, vehicle.Dimensions.LengthMeters, vehicle.Performance.MinimumGapMeters))
            {
                vehicle.State = VehicleMovementState.WaitingForTraffic;
                break;
            }

            var laneChanged = step.SegmentId == next.SegmentId && step.LaneId != next.LaneId;
            if (!laneChanged
                && step.ExitConnectionId is { } connectionId
                && !intersectionControl.IsEntryGranted(vehicle.Id, connectionId))
            {
                vehicle.State = VehicleMovementState.WaitingForTraffic;
                break;
            }

            vehicle.RouteStepIndex++;
            vehicle.RouteProgressMeters = 0d;
            vehicle.State = laneChanged ? VehicleMovementState.ChangingLane : VehicleMovementState.Driving;
        }
        return original - distanceMeters;
    }

    private static double ApproachSpeed(double current, double target, VehiclePerformance performance, double deltaSeconds)
    {
        if (target >= current) return Math.Min(target, current + performance.MaximumAccelerationMetersPerSecondSquared * deltaSeconds);
        return Math.Max(target, current - performance.ComfortableDecelerationMetersPerSecondSquared * deltaSeconds);
    }

    private static VehicleState CreateState(
        VehicleId id,
        RouteLaneStep[] routeSteps,
        VehicleDimensions dimensions,
        VehiclePerformance performance,
        int routeStepIndex,
        double routeProgressMeters,
        double speedMetersPerSecond,
        VehicleMovementState state,
        RoadTrafficTopology topology)
    {
        if (id.Value == 0) throw new ArgumentOutOfRangeException(nameof(id));
        ValidateDimensions(dimensions);
        ValidatePerformance(performance);
        topology.ValidateRoute(routeSteps);
        if (routeStepIndex < 0 || routeStepIndex >= routeSteps.Length) throw new ArgumentOutOfRangeException(nameof(routeStepIndex));
        var step = routeSteps[routeStepIndex];
        if (!double.IsFinite(routeProgressMeters) || routeProgressMeters < 0d || routeProgressMeters > step.DistanceMeters + 1e-9) throw new ArgumentOutOfRangeException(nameof(routeProgressMeters));
        if (!double.IsFinite(speedMetersPerSecond) || speedMetersPerSecond < 0d) throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (state == VehicleMovementState.Arrived)
        {
            if (routeStepIndex != routeSteps.Length - 1 || Math.Abs(routeProgressMeters - step.DistanceMeters) > 1e-8)
                throw new ArgumentException("An arrived Vehicle must be at the end of its final Route step.");
            if (speedMetersPerSecond > 1e-9) throw new ArgumentException("An arrived Vehicle must have zero speed.");
        }
        var vehicle = new VehicleState(id, routeSteps, dimensions, performance, routeStepIndex, routeProgressMeters, speedMetersPerSecond, state);
        UpdatePose(vehicle, topology);
        return vehicle;
    }

    private static void UpdatePose(VehicleState vehicle, RoadTrafficTopology topology)
    {
        var step = vehicle.CurrentStep;
        var lane = topology.GetLane(step.LaneId);
        vehicle.SegmentOffset = topology.GetSegmentOffset(step, vehicle.RouteProgressMeters);
        vehicle.Position = topology.GetPosition(step.LaneId, vehicle.SegmentOffset);
        vehicle.Forward = lane.Forward;
        vehicle.Velocity = vehicle.State == VehicleMovementState.Arrived || vehicle.SpeedMetersPerSecond <= 0d
            ? default
            : new WorldVector(lane.Forward.X * vehicle.SpeedMetersPerSecond, lane.Forward.Y * vehicle.SpeedMetersPerSecond, lane.Forward.Z * vehicle.SpeedMetersPerSecond);
    }

    private static void ValidateState(VehicleState vehicle, RoadTrafficTopology topology)
    {
        var step = vehicle.CurrentStep;
        var lane = topology.GetLane(step.LaneId);
        if (vehicle.RouteProgressMeters < -1e-9 || vehicle.RouteProgressMeters > step.DistanceMeters + 1e-9)
            throw new InvalidOperationException($"Vehicle {vehicle.Id.Value} Route progress is outside its Lane step.");
        var segmentOffset = topology.GetSegmentOffset(step, vehicle.RouteProgressMeters);
        if (segmentOffset < -1e-9 || segmentOffset > 1d + 1e-9)
            throw new InvalidOperationException($"Vehicle {vehicle.Id.Value} segment offset is outside Lane geometry.");
        var directionDelta = lane.Snapshot.Direction == LaneDirection.Forward
            ? step.EndSegmentOffset - step.StartSegmentOffset
            : step.StartSegmentOffset - step.EndSegmentOffset;
        if (directionDelta < -1e-12) throw new InvalidOperationException($"Vehicle {vehicle.Id.Value} Route travels against Lane direction.");
        if (!double.IsFinite(vehicle.SpeedMetersPerSecond) || vehicle.SpeedMetersPerSecond < 0d)
            throw new InvalidOperationException($"Vehicle {vehicle.Id.Value} has an invalid speed.");
        if (vehicle.State == VehicleMovementState.Arrived && vehicle.SpeedMetersPerSecond > 1e-9)
            throw new InvalidOperationException($"Vehicle {vehicle.Id.Value} is arrived but still moving.");
    }

    private static void ValidateDimensions(VehicleDimensions dimensions)
    {
        if (!double.IsFinite(dimensions.LengthMeters) || dimensions.LengthMeters <= 0d
            || !double.IsFinite(dimensions.WidthMeters) || dimensions.WidthMeters <= 0d
            || !double.IsFinite(dimensions.HeightMeters) || dimensions.HeightMeters <= 0d)
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Vehicle dimensions must be finite and greater than zero.");
    }

    private static void ValidatePerformance(VehiclePerformance performance)
    {
        if (!double.IsFinite(performance.MaximumSpeedMetersPerSecond) || performance.MaximumSpeedMetersPerSecond <= 0d
            || !double.IsFinite(performance.MaximumAccelerationMetersPerSecondSquared) || performance.MaximumAccelerationMetersPerSecondSquared <= 0d
            || !double.IsFinite(performance.ComfortableDecelerationMetersPerSecondSquared) || performance.ComfortableDecelerationMetersPerSecondSquared <= 0d
            || !double.IsFinite(performance.MinimumGapMeters) || performance.MinimumGapMeters < 0d
            || !double.IsFinite(performance.TimeHeadwaySeconds) || performance.TimeHeadwaySeconds <= 0d)
            throw new ArgumentOutOfRangeException(nameof(performance), "Vehicle performance values are invalid.");
    }

    private static VehicleSnapshot ToSnapshot(VehicleState vehicle, ulong tickCount) => new(
        vehicle.Id,
        vehicle.CurrentStep.LaneId,
        vehicle.RouteStepIndex,
        vehicle.SegmentOffset,
        vehicle.RouteProgressMeters,
        vehicle.Position,
        vehicle.Velocity,
        vehicle.Forward,
        vehicle.SpeedMetersPerSecond,
        vehicle.Dimensions,
        vehicle.State,
        tickCount);

    private sealed class VehicleState(
        VehicleId id,
        RouteLaneStep[] routeSteps,
        VehicleDimensions dimensions,
        VehiclePerformance performance,
        int routeStepIndex,
        double routeProgressMeters,
        double speedMetersPerSecond,
        VehicleMovementState state)
    {
        public VehicleId Id { get; } = id;
        public RouteLaneStep[] RouteSteps { get; } = routeSteps;
        public VehicleDimensions Dimensions { get; } = dimensions;
        public VehiclePerformance Performance { get; } = performance;
        public int RouteStepIndex { get; set; } = routeStepIndex;
        public double RouteProgressMeters { get; set; } = routeProgressMeters;
        public double SegmentOffset { get; set; }
        public WorldPoint Position { get; set; }
        public WorldVector Velocity { get; set; }
        public WorldVector Forward { get; set; }
        public double SpeedMetersPerSecond { get; set; } = speedMetersPerSecond;
        public VehicleMovementState State { get; set; } = state;
        public RouteLaneStep CurrentStep => RouteSteps[RouteStepIndex];
    }
}