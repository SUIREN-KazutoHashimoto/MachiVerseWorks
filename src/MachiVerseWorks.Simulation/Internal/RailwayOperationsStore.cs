namespace MachiVerseWorks.Simulation.Internal;

internal sealed class RailwayOperationsStore
{
    private const double MinimumRemainingDistance = 1e-9;
    private const double PlatformWaitDistanceMeters = 8d;
    private const double PlatformAssignmentLookAheadMeters = 180d;

    private readonly Dictionary<TrainFormationId, TrainFormationSnapshot> _formations = [];
    private readonly Dictionary<RailwayRouteId, RouteState> _routes = [];
    private readonly Dictionary<TimetableId, TimetableSnapshot> _timetables = [];
    private readonly Dictionary<RailwayServiceId, ServiceState> _services = [];
    private readonly Dictionary<TrainId, TrainState> _trains = [];
    private readonly List<TrainState> _trainOrder = [];
    private readonly Dictionary<TrackNodeId, TrackNodeSnapshot> _nodes = [];
    private readonly Dictionary<TrackSegmentId, TrackSegmentSnapshot> _segments = [];
    private readonly Dictionary<TrackSegmentId, BlockSectionId> _segmentBlocks = [];
    private readonly Dictionary<PlatformId, PlatformSnapshot> _platforms = [];
    private readonly Dictionary<StationId, List<PlatformSnapshot>> _stationPlatforms = [];
    private readonly Dictionary<DepotId, DepotSnapshot> _depots = [];
    private readonly TrackConnectionSnapshot[] _connections;
    private readonly Dictionary<BlockSectionId, TrainId> _blockOwners = [];
    private readonly Dictionary<PlatformId, TrainId> _platformOwners = [];
    private ulong _nextFormationId = 1;
    private ulong _nextRouteId = 1;
    private ulong _nextTimetableId = 1;
    private ulong _nextServiceId = 1;
    private ulong _nextTrainId = 1;

    public RailwayOperationsStore(RailwayInfrastructureSnapshot infrastructure)
    {
        ArgumentNullException.ThrowIfNull(infrastructure);
        foreach (var node in infrastructure.Nodes) _nodes.Add(node.Id, node);
        foreach (var segment in infrastructure.Segments) _segments.Add(segment.Id, segment);
        foreach (var block in infrastructure.Blocks)
        {
            foreach (var segmentId in block.SegmentIds) _segmentBlocks[segmentId] = block.Id;
        }
        foreach (var platform in infrastructure.Platforms)
        {
            _platforms.Add(platform.Id, platform);
            if (!_stationPlatforms.TryGetValue(platform.StationId, out var list))
            {
                list = [];
                _stationPlatforms.Add(platform.StationId, list);
            }
            list.Add(platform);
        }
        foreach (var depot in infrastructure.Depots) _depots.Add(depot.Id, depot);
        foreach (var list in _stationPlatforms.Values) list.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        _connections = infrastructure.Connections.OrderBy(static connection => connection.Id.Value).ToArray();
    }

    public ulong NextFormationId => _nextFormationId;
    public ulong NextRouteId => _nextRouteId;
    public ulong NextTimetableId => _nextTimetableId;
    public ulong NextServiceId => _nextServiceId;
    public ulong NextTrainId => _nextTrainId;
    public int FormationCount => _formations.Count;
    public int RouteCount => _routes.Count;
    public int TimetableCount => _timetables.Count;
    public int ServiceCount => _services.Count;
    public int TrainCount => _trains.Count;

    public TrainFormationId CreateFormation(double lengthMeters, double maximumSpeedMetersPerSecond, double maximumAccelerationMetersPerSecondSquared, double serviceDecelerationMetersPerSecondSquared, int capacity)
    {
        ValidatePositiveFinite(lengthMeters, nameof(lengthMeters));
        ValidatePositiveFinite(maximumSpeedMetersPerSecond, nameof(maximumSpeedMetersPerSecond));
        ValidatePositiveFinite(maximumAccelerationMetersPerSecondSquared, nameof(maximumAccelerationMetersPerSecondSquared));
        ValidatePositiveFinite(serviceDecelerationMetersPerSecondSquared, nameof(serviceDecelerationMetersPerSecondSquared));
        if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be greater than zero.");
        var id = new TrainFormationId(AllocateId(ref _nextFormationId));
        _formations.Add(id, new TrainFormationSnapshot(id, lengthMeters, maximumSpeedMetersPerSecond, maximumAccelerationMetersPerSecondSquared, serviceDecelerationMetersPerSecondSquared, capacity));
        return id;
    }

    public RailwayRouteId CreateRoute(IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        EnsureIdAvailable(_nextRouteId);
        var id = new RailwayRouteId(_nextRouteId);
        var route = BuildRoute(id, trackSegmentIds);
        _routes.Add(id, route);
        _nextRouteId = checked(_nextRouteId + 1);
        return id;
    }

    public TimetableId CreateTimetable(IReadOnlyList<TimetableStopSnapshot> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Count == 0) throw new ArgumentException("A timetable must contain at least one stop.", nameof(stops));
        var copiedStops = new TimetableStopSnapshot[stops.Count];
        ulong previousDeparture = 0;
        for (var index = 0; index < stops.Count; index++)
        {
            var stop = stops[index];
            if (!_stationPlatforms.ContainsKey(stop.StationId)) throw new ArgumentException($"Station {stop.StationId.Value} has no platforms.", nameof(stops));
            if (stop.PlannedDepartureTick < stop.PlannedArrivalTick) throw new ArgumentException("Planned departure must not precede planned arrival.", nameof(stops));
            if (index > 0 && stop.PlannedArrivalTick < previousDeparture) throw new ArgumentException("Timetable stops must be ordered by nondecreasing planned time.", nameof(stops));
            if (stop.PreferredPlatformId is { } preferred && (!_platforms.TryGetValue(preferred, out var platform) || platform.StationId != stop.StationId))
                throw new ArgumentException("Preferred platform must belong to the stop station.", nameof(stops));
            copiedStops[index] = stop;
            previousDeparture = stop.PlannedDepartureTick;
        }
        var id = new TimetableId(AllocateId(ref _nextTimetableId));
        _timetables.Add(id, new TimetableSnapshot(id, copiedStops));
        return id;
    }

    public RailwayServiceId CreateService(TrainFormationId formationId, RailwayRouteId routeId, TimetableId timetableId, DepotId originDepotId, DepotId destinationDepotId, ulong plannedStartTick)
    {
        if (!_formations.ContainsKey(formationId)) throw new ArgumentException("Formation does not exist.", nameof(formationId));
        if (!_routes.TryGetValue(routeId, out var route)) throw new ArgumentException("Route does not exist.", nameof(routeId));
        if (!_timetables.TryGetValue(timetableId, out var timetable)) throw new ArgumentException("Timetable does not exist.", nameof(timetableId));
        if (!_depots.TryGetValue(originDepotId, out var originDepot)) throw new ArgumentException("Origin depot does not exist.", nameof(originDepotId));
        if (!_depots.TryGetValue(destinationDepotId, out var destinationDepot)) throw new ArgumentException("Destination depot does not exist.", nameof(destinationDepotId));
        if (!ContainsTrack(originDepot.TrackSegmentIds, route.Steps[0].Segment.Id)) throw new ArgumentException("Route must begin on an origin depot track.", nameof(routeId));
        if (!ContainsTrack(destinationDepot.TrackSegmentIds, route.Steps[^1].Segment.Id)) throw new ArgumentException("Route must end on a destination depot track.", nameof(routeId));

        var stopRouteDistances = new double[timetable.Stops.Count];
        double previousDistance = -1d;
        for (var index = 0; index < timetable.Stops.Count; index++)
        {
            var stop = timetable.Stops[index];
            if (!TryFindStopDistance(route, stop, out var stopDistance)) throw new ArgumentException($"Stop station {stop.StationId.Value} has no platform on the route.", nameof(timetableId));
            if (stopDistance <= previousDistance) throw new ArgumentException("Timetable stops must appear in route order.", nameof(timetableId));
            stopRouteDistances[index] = stopDistance;
            previousDistance = stopDistance;
        }

        var id = new RailwayServiceId(AllocateId(ref _nextServiceId));
        _services.Add(id, new ServiceState(id, formationId, routeId, timetableId, originDepotId, destinationDepotId, plannedStartTick, stopRouteDistances));
        return id;
    }

    public TrainId CreateTrain(RailwayServiceId serviceId)
    {
        if (!_services.TryGetValue(serviceId, out var service)) throw new ArgumentException("Service does not exist.", nameof(serviceId));
        if (service.TrainId is not null) throw new InvalidOperationException("Service already has a train.");
        var route = _routes[service.RouteId];
        var formation = _formations[service.FormationId];
        var first = route.Steps[0];
        var id = new TrainId(AllocateId(ref _nextTrainId));
        var train = new TrainState(id, formation.Id, service.Id, route.Id, first.Start, first.Forward, service.OriginDepotId);
        _trains.Add(id, train);
        _trainOrder.Add(train);
        service.TrainId = id;
        return id;
    }

    public void Step(double deltaSeconds, ulong tickCount)
    {
        ValidatePositiveFinite(deltaSeconds, nameof(deltaSeconds));
        for (var index = 0; index < _trainOrder.Count; index++) StepTrain(_trainOrder[index], deltaSeconds, tickCount);
    }

    public RailwayOperationsSnapshot CreateSnapshot()
    {
        var formations = _formations.Values.OrderBy(static value => value.Id.Value).ToArray();
        var routes = _routes.Values.OrderBy(static value => value.Id.Value).Select(static value => value.CreateSnapshot()).ToArray();
        var timetables = _timetables.Values.OrderBy(static value => value.Id.Value).Select(static value => new TimetableSnapshot(value.Id, value.Stops.ToArray())).ToArray();
        var services = _services.Values.OrderBy(static value => value.Id.Value).Select(static value => value.CreateSnapshot()).ToArray();
        var trains = _trains.Values.OrderBy(static value => value.Id.Value).Select(static value => value.CreateSnapshot()).ToArray();
        return new RailwayOperationsSnapshot(formations, routes, timetables, services, trains);
    }

    public TrainSnapshot[] CreateTrainSnapshot() => _trainOrder.Select(static train => train.CreateSnapshot()).ToArray();

    public bool TryGetTrainSnapshot(TrainId id, out TrainSnapshot snapshot)
    {
        if (_trains.TryGetValue(id, out var train))
        {
            snapshot = train.CreateSnapshot();
            return true;
        }

        snapshot = null!;
        return false;
    }

    public void Restore(
        ulong nextFormationId,
        IReadOnlyList<TrainFormationSnapshot> formations,
        ulong nextRouteId,
        IReadOnlyList<RailwayRouteSnapshot> routes,
        ulong nextTimetableId,
        IReadOnlyList<TimetableSnapshot> timetables,
        ulong nextServiceId,
        IReadOnlyList<RailwayServiceSnapshot> services,
        ulong nextTrainId,
        IReadOnlyList<TrainSnapshot> trains)
    {
        ArgumentNullException.ThrowIfNull(formations);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(timetables);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(trains);
        _formations.Clear(); _routes.Clear(); _timetables.Clear(); _services.Clear(); _trains.Clear(); _trainOrder.Clear(); _blockOwners.Clear(); _platformOwners.Clear();

        foreach (var formation in formations) _formations.Add(formation.Id, formation);
        foreach (var route in routes) _routes.Add(route.Id, BuildRoute(route.Id, route.TrackSegmentIds));
        foreach (var timetable in timetables) _timetables.Add(timetable.Id, new TimetableSnapshot(timetable.Id, timetable.Stops.ToArray()));
        foreach (var service in services)
        {
            var timetable = _timetables[service.TimetableId];
            var route = _routes[service.RouteId];
            var distances = new double[timetable.Stops.Count];
            for (var index = 0; index < distances.Length; index++)
            {
                if (!TryFindStopDistance(route, timetable.Stops[index], out distances[index])) throw new InvalidOperationException("Saved timetable stop is not on its route.");
            }
            _services.Add(service.Id, ServiceState.FromSnapshot(service, distances));
        }
        foreach (var snapshotItem in trains)
        {
            var train = TrainState.FromSnapshot(snapshotItem);
            _trains.Add(train.Id, train);
            _trainOrder.Add(train);
            if (train.CurrentBlockId is { } block && !_blockOwners.TryAdd(block, train.Id)) throw new InvalidOperationException("Saved railway operations contain a block ownership conflict.");
            if (train.AssignedPlatformId is { } assigned && !_platformOwners.TryAdd(assigned, train.Id)) throw new InvalidOperationException("Saved railway operations contain a platform ownership conflict.");
            if (train.AssignedPlatformId is { } assignedPlatform && _services.TryGetValue(train.ServiceId, out var assignedService) && assignedService.NextStopIndex < _timetables[assignedService.TimetableId].Stops.Count)
            {
                var stop = _timetables[assignedService.TimetableId].Stops[assignedService.NextStopIndex];
                if (TryGetPlatformRouteDistance(_routes[train.RouteId], assignedPlatform, stop.StationId, out var assignedDistance)) assignedService.StopRouteDistances[assignedService.NextStopIndex] = assignedDistance;
            }
        }
        _trainOrder.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        _nextFormationId = nextFormationId; _nextRouteId = nextRouteId; _nextTimetableId = nextTimetableId; _nextServiceId = nextServiceId; _nextTrainId = nextTrainId;
    }

    private void StepTrain(TrainState train, double deltaSeconds, ulong tickCount)
    {
        train.TickCount = tickCount;
        var service = _services[train.ServiceId];
        var route = _routes[train.RouteId];
        var formation = _formations[train.FormationId];
        var timetable = _timetables[service.TimetableId];

        if (service.State == RailwayServiceState.Completed || train.State == TrainMovementState.Completed) return;
        if (service.State == RailwayServiceState.Planned)
        {
            if (tickCount < service.PlannedStartTick) return;
            var firstBlock = route.Steps[0].BlockId;
            if (firstBlock is { } block && !TryReserveBlock(block, train.Id))
            {
                train.State = TrainMovementState.WaitingForBlock;
                return;
            }
            train.CurrentBlockId = firstBlock;
            train.CurrentDepotId = null;
            train.State = TrainMovementState.Running;
            service.State = RailwayServiceState.Active;
        }

        if (train.State == TrainMovementState.Dwelling)
        {
            if (tickCount < train.DwellDepartureTick) return;
            if (train.CurrentPlatformId is { } occupied) ReleasePlatform(occupied, train.Id);
            train.CurrentPlatformId = null;
            train.AssignedPlatformId = null;
            service.NextStopIndex = checked(service.NextStopIndex + 1);
            train.State = TrainMovementState.Running;
        }

        double? stopDistance = null;
        if (service.NextStopIndex < timetable.Stops.Count)
        {
            var stop = timetable.Stops[service.NextStopIndex];
            var nominalStopDistance = service.StopRouteDistances[service.NextStopIndex];
            var distanceToStop = nominalStopDistance - train.RouteDistanceMeters;
            if (distanceToStop <= PlatformAssignmentLookAheadMeters && train.AssignedPlatformId is null)
            {
                if (TryAssignPlatform(route, stop, train.Id, train.RouteDistanceMeters, out var platformId, out var assignedDistance))
                {
                    train.AssignedPlatformId = platformId;
                    service.StopRouteDistances[service.NextStopIndex] = assignedDistance;
                    nominalStopDistance = assignedDistance;
                }
            }
            if (train.AssignedPlatformId is not null)
            {
                stopDistance = nominalStopDistance;
                if (nominalStopDistance - train.RouteDistanceMeters <= ComputeBrakingDistance(train.SpeedMetersPerSecond, formation.ServiceDecelerationMetersPerSecondSquared) + 30d)
                    train.State = TrainMovementState.ApproachingStation;
            }
            else if (distanceToStop <= PlatformAssignmentLookAheadMeters)
            {
                stopDistance = Math.Max(train.RouteDistanceMeters, nominalStopDistance - PlatformWaitDistanceMeters);
                train.State = TrainMovementState.ApproachingStation;
            }
        }

        var stepIndex = route.FindStepIndex(train.RouteDistanceMeters);
        var step = route.Steps[stepIndex];
        if (step.BlockId != train.CurrentBlockId)
        {
            if (step.BlockId is { } stepBlock && !TryReserveBlock(stepBlock, train.Id))
            {
                train.SpeedMetersPerSecond = 0d;
                train.State = TrainMovementState.WaitingForBlock;
                return;
            }
            if (train.CurrentBlockId is { } previousBlock) ReleaseBlock(previousBlock, train.Id);
            train.CurrentBlockId = step.BlockId;
            if (train.State == TrainMovementState.WaitingForBlock) train.State = TrainMovementState.Running;
        }
        var targetSpeed = Math.Min(formation.MaximumSpeedMetersPerSecond, step.Segment.SpeedLimitMetersPerSecond);
        if (stopDistance is { } targetStop)
        {
            var remaining = Math.Max(0d, targetStop - train.RouteDistanceMeters);
            var stoppingSpeed = Math.Sqrt(Math.Max(0d, 2d * formation.ServiceDecelerationMetersPerSecondSquared * remaining));
            targetSpeed = Math.Min(targetSpeed, stoppingSpeed);
        }

        var oldSpeed = train.SpeedMetersPerSecond;
        var speedDelta = targetSpeed >= oldSpeed
            ? Math.Min(formation.MaximumAccelerationMetersPerSecondSquared * deltaSeconds, targetSpeed - oldSpeed)
            : -Math.Min(formation.ServiceDecelerationMetersPerSecondSquared * deltaSeconds, oldSpeed - targetSpeed);
        var newSpeed = Math.Max(0d, oldSpeed + speedDelta);
        var requestedDistance = Math.Max(0d, (oldSpeed + newSpeed) * 0.5d * deltaSeconds);
        if (requestedDistance <= MinimumRemainingDistance && targetSpeed > 0d) requestedDistance = Math.Min(targetSpeed * deltaSeconds, 0.01d);

        var movementLimit = stopDistance is { } limit ? Math.Max(0d, limit - train.RouteDistanceMeters) : route.LengthMeters - train.RouteDistanceMeters;
        var movement = Math.Min(requestedDistance, movementLimit);
        var blocked = false;
        movement = LimitMovementForBlocks(route, train, movement, ref blocked);
        train.RouteDistanceMeters = Math.Min(route.LengthMeters, train.RouteDistanceMeters + movement);
        route.Sample(train.RouteDistanceMeters, out var position, out var forward);
        train.Position = position;
        train.Forward = forward;
        train.SpeedMetersPerSecond = blocked || movement + MinimumRemainingDistance < requestedDistance ? 0d : newSpeed;
        if (blocked) train.State = TrainMovementState.WaitingForBlock;
        else if (train.State == TrainMovementState.WaitingForBlock) train.State = TrainMovementState.Running;

        if (stopDistance is { } reachedStop && Math.Abs(train.RouteDistanceMeters - reachedStop) <= 1e-7)
        {
            if (train.AssignedPlatformId is null)
            {
                train.SpeedMetersPerSecond = 0d;
                train.State = TrainMovementState.ApproachingStation;
                return;
            }
            ArriveAtStop(train, service, timetable.Stops[service.NextStopIndex], tickCount);
            return;
        }

        if (train.RouteDistanceMeters + MinimumRemainingDistance >= route.LengthMeters)
        {
            if (train.CurrentBlockId is { } block) ReleaseBlock(block, train.Id);
            if (train.AssignedPlatformId is { } platform) ReleasePlatform(platform, train.Id);
            train.CurrentBlockId = null;
            train.CurrentPlatformId = null;
            train.AssignedPlatformId = null;
            train.CurrentDepotId = service.DestinationDepotId;
            train.SpeedMetersPerSecond = 0d;
            train.State = TrainMovementState.Completed;
            service.State = RailwayServiceState.Completed;
        }
    }

    private static void ArriveAtStop(TrainState train, ServiceState service, TimetableStopSnapshot stop, ulong tickCount)
    {
        var arrivalDelay = tickCount > stop.PlannedArrivalTick ? tickCount - stop.PlannedArrivalTick : 0;
        var nextDelayTicks = Math.Max(service.DelayTicks, arrivalDelay);
        var delayedPlannedDeparture = checked(stop.PlannedDepartureTick + nextDelayTicks);
        var minimumDwellDeparture = checked(tickCount + stop.MinimumDwellTicks);
        var dwellDepartureTick = Math.Max(delayedPlannedDeparture, minimumDwellDeparture);
        service.DelayTicks = nextDelayTicks;
        train.DwellDepartureTick = dwellDepartureTick;
        train.CurrentPlatformId = train.AssignedPlatformId;
        train.SpeedMetersPerSecond = 0d;
        train.State = TrainMovementState.Dwelling;
    }

    private double LimitMovementForBlocks(RouteState route, TrainState train, double requestedMovement, ref bool blocked)
    {
        if (requestedMovement <= 0d) return 0d;
        var startDistance = train.RouteDistanceMeters;
        var remaining = requestedMovement;
        var cursor = startDistance;
        while (remaining > MinimumRemainingDistance && cursor < route.LengthMeters - MinimumRemainingDistance)
        {
            var stepIndex = route.FindStepIndex(cursor);
            var step = route.Steps[stepIndex];
            var toBoundary = Math.Max(0d, step.EndDistance - cursor);
            if (remaining <= toBoundary + MinimumRemainingDistance) return requestedMovement;
            if (stepIndex + 1 >= route.Steps.Length) return requestedMovement;
            var nextBlock = route.Steps[stepIndex + 1].BlockId;
            if (nextBlock != step.BlockId && nextBlock is { } block && !TryReserveBlock(block, train.Id))
            {
                blocked = true;
                return Math.Max(0d, cursor + toBoundary - startDistance);
            }
            if (nextBlock != step.BlockId)
            {
                if (train.CurrentBlockId is { } current) ReleaseBlock(current, train.Id);
                train.CurrentBlockId = nextBlock;
            }
            cursor += toBoundary;
            remaining -= toBoundary;
            if (toBoundary <= MinimumRemainingDistance) cursor = Math.Min(route.LengthMeters, cursor + 1e-8);
        }
        return requestedMovement;
    }

    private bool TryAssignPlatform(RouteState route, TimetableStopSnapshot stop, TrainId trainId, double minimumRouteDistance, out PlatformId platformId, out double stopDistance)
    {
        if (stop.PreferredPlatformId is { } preferred
            && TryReserveCandidatePlatform(route, stop.StationId, preferred, trainId, minimumRouteDistance, out stopDistance))
        {
            platformId = preferred;
            return true;
        }

        var candidates = _stationPlatforms[stop.StationId];
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (candidate.Id == stop.PreferredPlatformId) continue;
            if (!TryReserveCandidatePlatform(route, stop.StationId, candidate.Id, trainId, minimumRouteDistance, out stopDistance)) continue;
            platformId = candidate.Id;
            return true;
        }
        platformId = default;
        stopDistance = 0d;
        return false;
    }

    private bool TryReserveCandidatePlatform(RouteState route, StationId stationId, PlatformId platformId, TrainId trainId, double minimumRouteDistance, out double stopDistance)
    {
        if (!TryGetPlatformRouteDistance(route, platformId, stationId, out stopDistance)
            || stopDistance + MinimumRemainingDistance < minimumRouteDistance)
            return false;
        return TryReservePlatform(platformId, trainId);
    }

    private bool TryFindStopDistance(RouteState route, TimetableStopSnapshot stop, out double distance)
    {
        if (stop.PreferredPlatformId is { } preferred && TryGetPlatformRouteDistance(route, preferred, stop.StationId, out distance)) return true;
        var platforms = _stationPlatforms[stop.StationId];
        for (var index = 0; index < platforms.Count; index++)
        {
            if (platforms[index].Id == stop.PreferredPlatformId) continue;
            if (TryGetPlatformRouteDistance(route, platforms[index].Id, stop.StationId, out distance)) return true;
        }
        distance = 0d;
        return false;
    }

    private bool TryGetPlatformRouteDistance(RouteState route, PlatformId platformId, StationId stationId, out double distance)
    {
        if (!_platforms.TryGetValue(platformId, out var platform) || platform.StationId != stationId)
        {
            distance = 0d;
            return false;
        }
        for (var index = 0; index < route.Steps.Length; index++)
        {
            var step = route.Steps[index];
            if (step.Segment.Id != platform.TrackSegmentId) continue;
            var centerOffset = (platform.StartSegmentOffset + platform.EndSegmentOffset) * 0.5d;
            var orientedOffset = step.ForwardFromSegmentStart ? centerOffset : 1d - centerOffset;
            distance = step.StartDistance + (step.LengthMeters * orientedOffset);
            return true;
        }
        distance = 0d;
        return false;
    }

    private RouteState BuildRoute(RailwayRouteId id, IReadOnlyList<TrackSegmentId> segmentIds)
    {
        if (segmentIds.Count == 0) throw new ArgumentException("A railway route must contain at least one track segment.", nameof(segmentIds));
        var copiedIds = segmentIds.ToArray();
        var uniqueSegmentIds = new HashSet<TrackSegmentId>();
        var segments = new TrackSegmentSnapshot[copiedIds.Length];
        for (var index = 0; index < copiedIds.Length; index++)
        {
            if (!uniqueSegmentIds.Add(copiedIds[index])) throw new ArgumentException($"Track segment {copiedIds[index].Value} is repeated in a railway route; repeated segment occurrences are not supported.", nameof(segmentIds));
            if (!_segments.TryGetValue(copiedIds[index], out segments[index])) throw new ArgumentException($"Track segment {copiedIds[index].Value} does not exist.", nameof(segmentIds));
        }

        var entryNodes = new TrackNodeId?[segments.Length];
        var exitNodes = new TrackNodeId?[segments.Length];
        for (var index = 0; index + 1 < segments.Length; index++)
        {
            if (!TryFindConnection(segments[index].Id, segments[index + 1].Id, out var connection)) throw new ArgumentException("Track sequence contains a non-traversable connection.", nameof(segmentIds));
            exitNodes[index] = connection.ViaNodeId;
            entryNodes[index + 1] = connection.ViaNodeId;
        }

        var steps = new RouteStep[segments.Length];
        var cumulative = 0d;
        for (var index = 0; index < segments.Length; index++)
        {
            var segment = segments[index];
            TrackNodeId startNode;
            TrackNodeId endNode;
            if (segments.Length == 1)
            {
                var reverse = segment.Direction == TrackDirection.EndToStart;
                startNode = reverse ? segment.EndNodeId : segment.StartNodeId;
                endNode = reverse ? segment.StartNodeId : segment.EndNodeId;
            }
            else if (index == 0)
            {
                endNode = exitNodes[index]!.Value;
                startNode = OppositeNode(segment, endNode);
            }
            else if (index == segments.Length - 1)
            {
                startNode = entryNodes[index]!.Value;
                endNode = OppositeNode(segment, startNode);
            }
            else
            {
                startNode = entryNodes[index]!.Value;
                endNode = exitNodes[index]!.Value;
                if (startNode == endNode) throw new ArgumentException("Route would reverse on the same track endpoint.", nameof(segmentIds));
            }
            ValidateDirection(segment, startNode, nameof(segmentIds));
            var start = _nodes[startNode].Position;
            var end = _nodes[endNode].Position;
            var dx = end.X - start.X; var dy = end.Y - start.Y; var dz = end.Z - start.Z;
            var length = Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
            if (length <= 0d) throw new ArgumentException("Route contains a zero-length track segment.", nameof(segmentIds));
            var forward = new WorldVector(dx / length, dy / length, dz / length);
            _segmentBlocks.TryGetValue(segment.Id, out var block);
            BlockSectionId? blockId = block.Value == 0 ? null : block;
            steps[index] = new RouteStep(segment, start, end, forward, length, cumulative, cumulative + length, startNode == segment.StartNodeId, blockId);
            cumulative += length;
        }
        return new RouteState(id, copiedIds, steps, cumulative);
    }

    private bool TryFindConnection(TrackSegmentId from, TrackSegmentId to, out TrackConnectionSnapshot connection)
    {
        for (var index = 0; index < _connections.Length; index++)
        {
            var candidate = _connections[index];
            if (candidate.FromSegmentId == from && candidate.ToSegmentId == to)
            {
                connection = candidate;
                return true;
            }
        }
        connection = default;
        return false;
    }

    private static TrackNodeId OppositeNode(TrackSegmentSnapshot segment, TrackNodeId node)
    {
        if (node == segment.StartNodeId) return segment.EndNodeId;
        if (node == segment.EndNodeId) return segment.StartNodeId;
        throw new ArgumentException("Connection node is not incident to the route segment.", nameof(node));
    }

    private static void ValidateDirection(TrackSegmentSnapshot segment, TrackNodeId startNode, string parameterName)
    {
        if (segment.Direction == TrackDirection.Bidirectional) return;
        if (segment.Direction == TrackDirection.StartToEnd && startNode == segment.StartNodeId) return;
        if (segment.Direction == TrackDirection.EndToStart && startNode == segment.EndNodeId) return;
        throw new ArgumentException("Route violates track direction.", parameterName);
    }

    private bool TryReserveBlock(BlockSectionId blockId, TrainId trainId)
    {
        if (_blockOwners.TryGetValue(blockId, out var owner)) return owner == trainId;
        _blockOwners.Add(blockId, trainId);
        return true;
    }

    private void ReleaseBlock(BlockSectionId blockId, TrainId trainId)
    {
        if (_blockOwners.TryGetValue(blockId, out var owner) && owner == trainId) _blockOwners.Remove(blockId);
    }

    private bool TryReservePlatform(PlatformId platformId, TrainId trainId)
    {
        if (_platformOwners.TryGetValue(platformId, out var owner)) return owner == trainId;
        _platformOwners.Add(platformId, trainId);
        return true;
    }

    private void ReleasePlatform(PlatformId platformId, TrainId trainId)
    {
        if (_platformOwners.TryGetValue(platformId, out var owner) && owner == trainId) _platformOwners.Remove(platformId);
    }

    private static bool ContainsTrack(IReadOnlyList<TrackSegmentId> segmentIds, TrackSegmentId target)
    {
        for (var index = 0; index < segmentIds.Count; index++) if (segmentIds[index] == target) return true;
        return false;
    }

    private static double ComputeBrakingDistance(double speed, double deceleration) => (speed * speed) / (2d * deceleration);

    private static void ValidatePositiveFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d) throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and greater than zero.");
    }

    private static void EnsureIdAvailable(ulong nextId)
    {
        if (nextId == 0 || nextId == ulong.MaxValue) throw new InvalidOperationException("Railway operation stable ID capacity is exhausted.");
    }

    private static ulong AllocateId(ref ulong nextId)
    {
        EnsureIdAvailable(nextId);
        var value = nextId;
        nextId = checked(value + 1);
        return value;
    }

    private sealed class RouteState
    {
        public RouteState(RailwayRouteId id, TrackSegmentId[] segmentIds, RouteStep[] steps, double lengthMeters) { Id = id; SegmentIds = segmentIds; Steps = steps; LengthMeters = lengthMeters; }
        public RailwayRouteId Id { get; }
        public TrackSegmentId[] SegmentIds { get; }
        public RouteStep[] Steps { get; }
        public double LengthMeters { get; }
        public RailwayRouteSnapshot CreateSnapshot() => new(Id, SegmentIds.ToArray(), LengthMeters);
        public int FindStepIndex(double routeDistance)
        {
            if (routeDistance >= LengthMeters) return Steps.Length - 1;
            for (var index = 0; index < Steps.Length; index++) if (routeDistance < Steps[index].EndDistance - MinimumRemainingDistance) return index;
            return Steps.Length - 1;
        }
        public void Sample(double routeDistance, out WorldPoint position, out WorldVector forward)
        {
            var index = FindStepIndex(routeDistance);
            var step = Steps[index];
            var local = Math.Clamp(routeDistance - step.StartDistance, 0d, step.LengthMeters);
            position = new WorldPoint(step.Start.X + (step.Forward.X * local), step.Start.Y + (step.Forward.Y * local), step.Start.Z + (step.Forward.Z * local));
            forward = step.Forward;
        }
    }

    private sealed record RouteStep(TrackSegmentSnapshot Segment, WorldPoint Start, WorldPoint End, WorldVector Forward, double LengthMeters, double StartDistance, double EndDistance, bool ForwardFromSegmentStart, BlockSectionId? BlockId);

    private sealed class ServiceState
    {
        public ServiceState(RailwayServiceId id, TrainFormationId formationId, RailwayRouteId routeId, TimetableId timetableId, DepotId originDepotId, DepotId destinationDepotId, ulong plannedStartTick, double[] stopRouteDistances)
        { Id = id; FormationId = formationId; RouteId = routeId; TimetableId = timetableId; OriginDepotId = originDepotId; DestinationDepotId = destinationDepotId; PlannedStartTick = plannedStartTick; StopRouteDistances = stopRouteDistances; }
        public RailwayServiceId Id { get; }
        public TrainFormationId FormationId { get; }
        public RailwayRouteId RouteId { get; }
        public TimetableId TimetableId { get; }
        public DepotId OriginDepotId { get; }
        public DepotId DestinationDepotId { get; }
        public ulong PlannedStartTick { get; }
        public RailwayServiceState State { get; set; }
        public ulong DelayTicks { get; set; }
        public int NextStopIndex { get; set; }
        public TrainId? TrainId { get; set; }
        public double[] StopRouteDistances { get; }
        public RailwayServiceSnapshot CreateSnapshot() => new(Id, FormationId, RouteId, TimetableId, OriginDepotId, DestinationDepotId, PlannedStartTick, State, DelayTicks, NextStopIndex, TrainId);
        public static ServiceState FromSnapshot(RailwayServiceSnapshot snapshot, double[] distances) => new(snapshot.Id, snapshot.FormationId, snapshot.RouteId, snapshot.TimetableId, snapshot.OriginDepotId, snapshot.DestinationDepotId, snapshot.PlannedStartTick, distances) { State = snapshot.State, DelayTicks = snapshot.DelayTicks, NextStopIndex = snapshot.NextStopIndex, TrainId = snapshot.TrainId };
    }

    private sealed class TrainState
    {
        public TrainState(TrainId id, TrainFormationId formationId, RailwayServiceId serviceId, RailwayRouteId routeId, WorldPoint position, WorldVector forward, DepotId depotId)
        { Id = id; FormationId = formationId; ServiceId = serviceId; RouteId = routeId; Position = position; Forward = forward; CurrentDepotId = depotId; State = TrainMovementState.InDepot; }
        public TrainId Id { get; }
        public TrainFormationId FormationId { get; }
        public RailwayServiceId ServiceId { get; }
        public RailwayRouteId RouteId { get; }
        public double RouteDistanceMeters { get; set; }
        public WorldPoint Position { get; set; }
        public WorldVector Forward { get; set; }
        public double SpeedMetersPerSecond { get; set; }
        public TrainMovementState State { get; set; }
        public BlockSectionId? CurrentBlockId { get; set; }
        public PlatformId? CurrentPlatformId { get; set; }
        public PlatformId? AssignedPlatformId { get; set; }
        public DepotId? CurrentDepotId { get; set; }
        public ulong DwellDepartureTick { get; set; }
        public ulong TickCount { get; set; }
        public TrainSnapshot CreateSnapshot() => new(Id, FormationId, ServiceId, RouteId, RouteDistanceMeters, Position, Forward, SpeedMetersPerSecond, State, CurrentBlockId, CurrentPlatformId, AssignedPlatformId, CurrentDepotId, DwellDepartureTick, TickCount);
        public static TrainState FromSnapshot(TrainSnapshot snapshot) => new(snapshot.Id, snapshot.FormationId, snapshot.ServiceId, snapshot.RouteId, snapshot.Position, snapshot.Forward, snapshot.CurrentDepotId ?? default) { RouteDistanceMeters = snapshot.RouteDistanceMeters, SpeedMetersPerSecond = snapshot.SpeedMetersPerSecond, State = snapshot.State, CurrentBlockId = snapshot.CurrentBlockId, CurrentPlatformId = snapshot.CurrentPlatformId, AssignedPlatformId = snapshot.AssignedPlatformId, CurrentDepotId = snapshot.CurrentDepotId, DwellDepartureTick = snapshot.DwellDepartureTick, TickCount = snapshot.TickCount };
    }
}
