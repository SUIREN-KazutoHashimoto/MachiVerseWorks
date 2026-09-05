namespace MachiVerseWorks.Simulation;

public readonly record struct InitialMobilitySummary(
    int ParticipantCount,
    int PedestrianCount,
    int VehicleCount);

public sealed partial class SimulationWorld
{
    private const int InitialMobilityRouteCandidateLimit = 64;
    private const double PreferredInitialWalkingDistanceMeters = 100d;
    private const double PreferredInitialVehicleDistanceMeters = 1_000d;
    private const double MinimumInitialWalkingStreetLengthMeters = 25d;
    private const ulong InitialMobilityTripRequestBase = 9_000_000_000UL;
    private readonly HashSet<PedestrianId> _initialMobilityPedestrianIds = [];
    private readonly HashSet<VehicleId> _initialMobilityVehicleIds = [];

    public InitialMobilitySummary SeedInitialMobility(int participantCount) =>
        SeedInitialMobilityCore(participantCount, preferredCenter: null);

    public InitialMobilitySummary SeedInitialMobility(int participantCount, WorldPoint preferredCenter) =>
        SeedInitialMobilityCore(participantCount, preferredCenter);

    private InitialMobilitySummary SeedInitialMobilityCore(int participantCount, WorldPoint? preferredCenter)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(participantCount);
        if (participantCount == 0) return default;

        var roadSnapshot = CreateRoadNetworkSnapshot();
        if (HasRegionalGeneration)
        {
            NormalizeRegionalBuildingAccessOffsets(roadSnapshot);
            roadSnapshot = CreateRoadNetworkSnapshot();
        }

        var pedestrianAccessPoints = CreateInitialPedestrianAccessCandidates(roadSnapshot, preferredCenter);
        var pedestrianPair = pedestrianAccessPoints.Length >= 2
            ? FindInitialWalkingPair(pedestrianAccessPoints)
            : null;
        RouteResult? starterStreetVehicleRoute = null;

        if (pedestrianPair is null && HasRegionalGeneration)
        {
            pedestrianPair = TryCreateInitialStreet(roadSnapshot, preferredCenter, out starterStreetVehicleRoute);
            if (pedestrianPair is not null)
                roadSnapshot = CreateRoadNetworkSnapshot();
        }

        var vehicleAccessPoints = CreateInitialVehicleAccessCandidates(roadSnapshot, preferredCenter);
        var vehicleRoute = starterStreetVehicleRoute
            ?? (vehicleAccessPoints.Length >= 2
                ? FindInitialVehicleRoute(roadSnapshot, vehicleAccessPoints)
                : null);
        if (pedestrianPair is null && vehicleRoute is null) return default;

        var pedestriansCreated = 0;
        var vehiclesCreated = 0;
        if (pedestrianPair is { } pair)
        {
            var pedestrianId = CreatePedestrian(
                new TripRequest(
                    new TripRequestId(InitialMobilityTripRequestBase),
                    pair.Origin,
                    pair.Destination,
                    TravelMode.Foot),
                walkingSpeedMetersPerSecond: 1.4d);
            _initialMobilityPedestrianIds.Add(pedestrianId);
            pedestriansCreated = 1;
        }

        if (vehicleRoute is not null && participantCount > pedestriansCreated)
        {
            var vehicleId = CreateVehicle(vehicleRoute, initialSpeedMetersPerSecond: 4d);
            _initialMobilityVehicleIds.Add(vehicleId);
            vehiclesCreated = 1;
        }
        else if (pedestriansCreated == 0 && vehicleRoute is not null)
        {
            var vehicleId = CreateVehicle(vehicleRoute, initialSpeedMetersPerSecond: 4d);
            _initialMobilityVehicleIds.Add(vehicleId);
            vehiclesCreated = 1;
        }

        return new InitialMobilitySummary(
            pedestriansCreated + vehiclesCreated,
            pedestriansCreated,
            vehiclesCreated);
    }

    private static RoadAccessPointSnapshot[] CreateInitialPedestrianAccessCandidates(
        RoadNetworkSnapshot roadSnapshot,
        WorldPoint? preferredCenter)
    {
        var walkableSegmentIds = roadSnapshot.Segments
            .Where(static segment => segment.Kind != RoadKind.Highway)
            .Select(static segment => segment.Id)
            .ToHashSet();
        var candidates = roadSnapshot.AccessPoints
            .Where(access =>
                access.BuildingId is not null
                && (access.Mode & RoadAccessMode.Foot) != 0
                && walkableSegmentIds.Contains(access.SegmentId))
            .ToArray();
        return OrderInitialAccessCandidates(roadSnapshot, candidates, preferredCenter);
    }

    private static RoadAccessPointSnapshot[] CreateInitialVehicleAccessCandidates(
        RoadNetworkSnapshot roadSnapshot,
        WorldPoint? preferredCenter)
    {
        var candidates = roadSnapshot.AccessPoints
            .Where(static access => access.BuildingId is not null && (access.Mode & RoadAccessMode.Motor) != 0)
            .OrderBy(static access => access.Id.Value)
            .GroupBy(static access => access.SegmentId)
            .Select(static group => group.First())
            .ToArray();
        return OrderInitialAccessCandidates(roadSnapshot, candidates, preferredCenter);
    }

    private static RoadAccessPointSnapshot[] OrderInitialAccessCandidates(
        RoadNetworkSnapshot roadSnapshot,
        RoadAccessPointSnapshot[] candidates,
        WorldPoint? preferredCenter)
    {
        if (preferredCenter is not { } center)
        {
            return candidates
                .OrderBy(static access => access.Id.Value)
                .Take(InitialMobilityRouteCandidateLimit)
                .ToArray();
        }

        var segments = roadSnapshot.Segments.ToDictionary(static segment => segment.Id);
        var nodes = roadSnapshot.Nodes.ToDictionary(static node => node.Id);
        return candidates
            .Select(access => new
            {
                Access = access,
                Distance = TryResolveAccessPosition(access, segments, nodes, out var position)
                    ? Distance2D(position, center)
                    : double.PositiveInfinity,
            })
            .OrderBy(static item => item.Distance)
            .ThenBy(static item => item.Access.Id.Value)
            .Take(InitialMobilityRouteCandidateLimit)
            .Select(static item => item.Access)
            .ToArray();
    }

    private void NormalizeRegionalBuildingAccessOffsets(RoadNetworkSnapshot roadSnapshot)
    {
        var segments = roadSnapshot.Segments.ToDictionary(static segment => segment.Id);
        var nodes = roadSnapshot.Nodes.ToDictionary(static node => node.Id);
        foreach (var access in roadSnapshot.AccessPoints
                     .Where(static item => item.BuildingId is not null)
                     .OrderBy(static item => item.Id.Value))
        {
            if (!segments.TryGetValue(access.SegmentId, out var segment)
                || !nodes.TryGetValue(segment.StartNodeId, out var start)
                || !nodes.TryGetValue(segment.EndNodeId, out var end)
                || !TryGetBuildingSnapshot(access.BuildingId!.Value, out var building)) continue;
            var dx = end.Position.X - start.Position.X;
            var dy = end.Position.Y - start.Position.Y;
            var lengthSquared = (dx * dx) + (dy * dy);
            if (lengthSquared <= double.Epsilon) continue;
            var centerX = (building.Bounds.MinX + building.Bounds.MaxX) * 0.5d;
            var centerY = (building.Bounds.MinY + building.Bounds.MaxY) * 0.5d;
            var offset = Math.Clamp(
                (((centerX - start.Position.X) * dx) + ((centerY - start.Position.Y) * dy)) / lengthSquared,
                0d,
                1d);
            if (Math.Abs(offset - access.SegmentOffset) <= 1e-9d) continue;
            _ = UpdateRoadAccessPoint(access.Id, access.SegmentId, offset, access.BuildingId, access.PoiId, access.Mode);
        }
    }

    private (TripEndpoint Origin, TripEndpoint Destination)? TryCreateInitialStreet(
        RoadNetworkSnapshot roadSnapshot,
        WorldPoint? preferredCenter,
        out RouteResult? vehicleRoute)
    {
        vehicleRoute = null;
        var buildingIds = CreateInitialBuildingCandidates(roadSnapshot, preferredCenter);
        if (buildingIds.Length < 2) return null;

        BuildingId? selectedFirst = null;
        BuildingId? selectedSecond = null;
        WorldPoint selectedFirstCenter = default;
        WorldPoint selectedSecondCenter = default;
        var selectedDistance = double.PositiveInfinity;
        for (var firstIndex = 0; firstIndex < buildingIds.Length - 1; firstIndex++)
        {
            if (!TryGetBuildingSnapshot(buildingIds[firstIndex], out var firstBuilding)) continue;
            var firstCenter = Center(firstBuilding.Bounds);
            for (var secondIndex = firstIndex + 1; secondIndex < buildingIds.Length; secondIndex++)
            {
                if (!TryGetBuildingSnapshot(buildingIds[secondIndex], out var secondBuilding)) continue;
                var secondCenter = Center(secondBuilding.Bounds);
                var distance = Distance2D(firstCenter, secondCenter);
                if (distance < MinimumInitialWalkingStreetLengthMeters || distance >= selectedDistance) continue;
                selectedFirst = buildingIds[firstIndex];
                selectedSecond = buildingIds[secondIndex];
                selectedFirstCenter = firstCenter;
                selectedSecondCenter = secondCenter;
                selectedDistance = distance;
            }
        }
        if (selectedFirst is not { } firstId || selectedSecond is not { } secondId) return null;

        var startPosition = SnapToGround(selectedFirstCenter);
        var endPosition = SnapToGround(selectedSecondCenter);
        var middlePosition = SnapToGround(Midpoint(selectedFirstCenter, selectedSecondCenter));
        var start = CreateRoadNode(startPosition);
        var middle = CreateRoadNode(middlePosition, RoadNodeKind.Intersection);
        var end = CreateRoadNode(endPosition);
        var firstSegment = CreateRoadSegment(start, middle, RoadKind.Local);
        var secondSegment = CreateRoadSegment(middle, end, RoadKind.Local);
        var firstForward = CreateLane(firstSegment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 11d);
        var firstReverse = CreateLane(firstSegment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: 11d);
        var secondForward = CreateLane(secondSegment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 11d);
        var secondReverse = CreateLane(secondSegment, LaneDirection.Reverse, 0, speedLimitMetersPerSecond: 11d);
        _ = CreateLaneConnection(firstForward, secondForward, middle);
        _ = CreateLaneConnection(secondReverse, firstReverse, middle);
        _ = CreateRoadAccessPoint(firstSegment, 0.05d, firstId, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        _ = CreateRoadAccessPoint(secondSegment, 0.95d, secondId, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);

        var origin = TripEndpoint.ForBuilding(firstId);
        var destination = TripEndpoint.ForBuilding(secondId);
        try
        {
            var walkingRoute = FindWalkingRoute(origin, destination);
            if (walkingRoute.Legs.Count == 0 || walkingRoute.TotalLengthMeters <= 1d) return null;
            vehicleRoute = FindRoadRoute(new RouteRequest(
                Interpolate(startPosition, middlePosition, 0.05d),
                Interpolate(middlePosition, endPosition, 0.95d),
                RoutingCostMetric.EstimatedTravelTime));
            if (vehicleRoute.Steps.Count == 0 || vehicleRoute.TotalDistanceMeters <= 1d)
                vehicleRoute = null;
            return (origin, destination);
        }
        catch (InvalidOperationException)
        {
            vehicleRoute = null;
            return null;
        }
    }

    private BuildingId[] CreateInitialBuildingCandidates(
        RoadNetworkSnapshot roadSnapshot,
        WorldPoint? preferredCenter)
    {
        var candidates = roadSnapshot.AccessPoints
            .Where(static access => access.BuildingId is not null)
            .Select(static access => access.BuildingId!.Value)
            .Distinct()
            .ToArray();
        if (preferredCenter is not { } center)
        {
            return candidates
                .OrderBy(static id => id.Value)
                .Take(InitialMobilityRouteCandidateLimit)
                .ToArray();
        }

        return candidates
            .Select(id => new
            {
                Id = id,
                Distance = TryGetBuildingSnapshot(id, out var building)
                    ? Distance2D(Center(building.Bounds), center)
                    : double.PositiveInfinity,
            })
            .OrderBy(static item => item.Distance)
            .ThenBy(static item => item.Id.Value)
            .Take(InitialMobilityRouteCandidateLimit)
            .Select(static item => item.Id)
            .ToArray();
    }

    private (TripEndpoint Origin, TripEndpoint Destination)? FindInitialWalkingPair(RoadAccessPointSnapshot[] accessPoints)
    {
        (TripEndpoint Origin, TripEndpoint Destination)? fallback = null;
        var fallbackDistance = 0d;
        for (var firstIndex = 0; firstIndex < accessPoints.Length - 1; firstIndex++)
        {
            var first = accessPoints[firstIndex];
            var origin = TripEndpoint.ForBuilding(first.BuildingId!.Value);
            for (var secondIndex = firstIndex + 1; secondIndex < accessPoints.Length; secondIndex++)
            {
                var second = accessPoints[secondIndex];
                if (first.BuildingId == second.BuildingId) continue;
                var destination = TripEndpoint.ForBuilding(second.BuildingId!.Value);
                try
                {
                    var route = FindWalkingRoute(origin, destination);
                    if (route.Legs.Count == 0 || route.TotalLengthMeters <= 1d) continue;
                    if (route.TotalLengthMeters >= PreferredInitialWalkingDistanceMeters) return (origin, destination);
                    if (route.TotalLengthMeters > fallbackDistance)
                    {
                        fallback = (origin, destination);
                        fallbackDistance = route.TotalLengthMeters;
                    }
                }
                catch (InvalidOperationException) { }
            }
        }
        return fallback;
    }

    private RouteResult? FindInitialVehicleRoute(RoadNetworkSnapshot roadSnapshot, RoadAccessPointSnapshot[] accessPoints)
    {
        var segments = roadSnapshot.Segments.ToDictionary(static segment => segment.Id);
        var nodes = roadSnapshot.Nodes.ToDictionary(static node => node.Id);
        RouteResult? fallback = null;
        var fallbackDistance = 0d;
        for (var firstIndex = 0; firstIndex < accessPoints.Length - 1; firstIndex++)
        {
            var first = accessPoints[firstIndex];
            if (!TryResolveAccessPosition(first, segments, nodes, out var origin)) continue;
            for (var secondIndex = firstIndex + 1; secondIndex < accessPoints.Length; secondIndex++)
            {
                var second = accessPoints[secondIndex];
                if (first.SegmentId == second.SegmentId) continue;
                if (!TryResolveAccessPosition(second, segments, nodes, out var destination)) continue;
                try
                {
                    var route = FindRoadRoute(new RouteRequest(origin, destination, RoutingCostMetric.EstimatedTravelTime));
                    if (route.Steps.Count == 0 || route.TotalDistanceMeters <= 1d) continue;
                    if (route.TotalDistanceMeters >= PreferredInitialVehicleDistanceMeters) return route;
                    if (route.TotalDistanceMeters > fallbackDistance)
                    {
                        fallback = route;
                        fallbackDistance = route.TotalDistanceMeters;
                    }
                }
                catch (InvalidOperationException) { }
            }
        }
        return fallback;
    }

    private static bool TryResolveAccessPosition(
        RoadAccessPointSnapshot access,
        Dictionary<RoadSegmentId, RoadSegmentSnapshot> segments,
        Dictionary<RoadNodeId, RoadNodeSnapshot> nodes,
        out WorldPoint position)
    {
        position = default;
        if (!segments.TryGetValue(access.SegmentId, out var segment)
            || !nodes.TryGetValue(segment.StartNodeId, out var start)
            || !nodes.TryGetValue(segment.EndNodeId, out var end)) return false;
        position = Interpolate(start.Position, end.Position, Math.Clamp(access.SegmentOffset, 0d, 1d));
        return true;
    }

    private void RetireCompletedInitialMobility()
    {
        foreach (var pedestrianId in _initialMobilityPedestrianIds.ToArray())
        {
            if (!TryGetPedestrianSnapshot(pedestrianId, out var snapshot) || snapshot.State == PedestrianMovementState.Arrived)
            {
                _ = RemovePedestrianCore(pedestrianId);
                _initialMobilityPedestrianIds.Remove(pedestrianId);
            }
        }
        foreach (var vehicleId in _initialMobilityVehicleIds.ToArray())
        {
            if (!TryGetVehicleSnapshot(vehicleId, out var snapshot) || snapshot.State == VehicleMovementState.Arrived)
            {
                _ = RemoveVehicleCore(vehicleId);
                _initialMobilityVehicleIds.Remove(vehicleId);
            }
        }
    }

    private void RetireInitialMobilityForRoadTopologyMutation()
    {
        foreach (var pedestrianId in _initialMobilityPedestrianIds.ToArray())
        {
            _ = RemovePedestrianCore(pedestrianId);
            _initialMobilityPedestrianIds.Remove(pedestrianId);
        }
        foreach (var vehicleId in _initialMobilityVehicleIds.ToArray())
        {
            _ = RemoveVehicleCore(vehicleId);
            _initialMobilityVehicleIds.Remove(vehicleId);
        }
    }

    private void RetireInitialPedestriansForNetworkMutation()
    {
        foreach (var pedestrianId in _initialMobilityPedestrianIds.ToArray())
        {
            _ = RemovePedestrianCore(pedestrianId);
            _initialMobilityPedestrianIds.Remove(pedestrianId);
        }
    }
}
