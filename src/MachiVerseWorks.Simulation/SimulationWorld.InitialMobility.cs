namespace MachiVerseWorks.Simulation;

public readonly record struct InitialMobilitySummary(
    int ParticipantCount,
    int PedestrianCount,
    int VehicleCount);

public sealed partial class SimulationWorld
{
    private const int InitialMobilityRouteCandidateLimit = 64;
    private const double PreferredInitialWalkingDistanceMeters = 100d;
    private const double PreferredInitialVehicleDistanceMeters = 300d;
    private const ulong InitialMobilityTripRequestBase = 9_000_000_000UL;
    private readonly HashSet<PedestrianId> _initialMobilityPedestrianIds = [];
    private readonly HashSet<VehicleId> _initialMobilityVehicleIds = [];

    /// <summary>
    /// Primes a newly materialized regional world with a small amount of transient street activity
    /// without advancing the entire simulation or inventing Population/Economy state. Bootstrap
    /// mobility is retired after arrival and may be retired early when road topology is edited.
    /// </summary>
    public InitialMobilitySummary SeedInitialMobility(int participantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(participantCount);
        if (participantCount == 0) return default;

        var roadSnapshot = CreateRoadNetworkSnapshot();
        var walkableSegmentIds = roadSnapshot.Segments
            .Where(static segment => segment.Kind != RoadKind.Highway)
            .Select(static segment => segment.Id)
            .ToHashSet();

        // The derived pedestrian network intentionally excludes Highway segments. Keep one
        // deterministic building access per usable segment so equal-offset access points on the
        // same segment do not dominate the candidate set with zero-length walking trips.
        var pedestrianAccessPoints = roadSnapshot.AccessPoints
            .Where(access =>
                access.BuildingId is not null
                && (access.Mode & RoadAccessMode.Foot) != 0
                && walkableSegmentIds.Contains(access.SegmentId))
            .OrderBy(static access => access.Id.Value)
            .GroupBy(static access => access.SegmentId)
            .Select(static group => group.First())
            .Take(InitialMobilityRouteCandidateLimit)
            .ToArray();

        var vehicleAccessPoints = roadSnapshot.AccessPoints
            .Where(static access => access.BuildingId is not null && (access.Mode & RoadAccessMode.Motor) != 0)
            .OrderBy(static access => access.Id.Value)
            .GroupBy(static access => access.SegmentId)
            .Select(static group => group.First())
            .Take(InitialMobilityRouteCandidateLimit)
            .ToArray();

        var pedestrianPair = pedestrianAccessPoints.Length >= 2
            ? FindInitialWalkingPair(pedestrianAccessPoints)
            : null;
        var vehicleRoute = vehicleAccessPoints.Length >= 2
            ? FindInitialVehicleRoute(roadSnapshot, vehicleAccessPoints)
            : null;
        if (pedestrianPair is null && vehicleRoute is null) return default;

        var pedestriansCreated = 0;
        var vehiclesCreated = 0;

        // A single entity per mode is enough to prove that the authoritative runtime is publishing
        // real street activity. Reusing the same route for multiple vehicles would overlap their
        // initial lane occupancy, so bootstrap intentionally does not synthesize a traffic queue.
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

    private (TripEndpoint Origin, TripEndpoint Destination)? FindInitialWalkingPair(
        RoadAccessPointSnapshot[] accessPoints)
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
                    if (route.TotalLengthMeters >= PreferredInitialWalkingDistanceMeters)
                        return (origin, destination);
                    if (route.TotalLengthMeters > fallbackDistance)
                    {
                        fallback = (origin, destination);
                        fallbackDistance = route.TotalLengthMeters;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Separate Regional road components can both expose Foot access points without
                    // a walkable route between them. Continue probing deterministic candidates.
                }
            }
        }
        return fallback;
    }

    private RouteResult? FindInitialVehicleRoute(
        RoadNetworkSnapshot roadSnapshot,
        RoadAccessPointSnapshot[] accessPoints)
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
                catch (InvalidOperationException)
                {
                    // Separate Regional road components can both have motor access points without
                    // a drivable route between them. Continue probing the deterministic candidates.
                }
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
            || !nodes.TryGetValue(segment.EndNodeId, out var end))
        {
            return false;
        }

        var offset = Math.Clamp(access.SegmentOffset, 0d, 1d);
        position = new WorldPoint(
            start.Position.X + ((end.Position.X - start.Position.X) * offset),
            start.Position.Y + ((end.Position.Y - start.Position.Y) * offset),
            start.Position.Z + ((end.Position.Z - start.Position.Z) * offset));
        return true;
    }

    private void RetireCompletedInitialMobility()
    {
        foreach (var pedestrianId in _initialMobilityPedestrianIds.ToArray())
        {
            if (!TryGetPedestrianSnapshot(pedestrianId, out var snapshot)
                || snapshot.State == PedestrianMovementState.Arrived)
            {
                _ = RemovePedestrianCore(pedestrianId);
                _initialMobilityPedestrianIds.Remove(pedestrianId);
            }
        }

        foreach (var vehicleId in _initialMobilityVehicleIds.ToArray())
        {
            if (!TryGetVehicleSnapshot(vehicleId, out var snapshot)
                || snapshot.State == VehicleMovementState.Arrived)
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
