namespace MachiVerseWorks.Simulation;

public readonly record struct InitialMobilitySummary(
    int ParticipantCount,
    int PedestrianCount,
    int VehicleCount);

public sealed partial class SimulationWorld
{
    private const int InitialMobilityRouteCandidateLimit = 64;
    private const ulong InitialMobilityTripRequestBase = 9_000_000_000UL;

    /// <summary>
    /// Primes a newly materialized regional world with a small amount of street activity
    /// without advancing the entire simulation or inventing Population/Economy state.
    /// </summary>
    public InitialMobilitySummary SeedInitialMobility(int participantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(participantCount);
        if (participantCount == 0) return default;

        var roadSnapshot = CreateRoadNetworkSnapshot();
        var accessPoints = roadSnapshot.AccessPoints
            .Where(static access =>
                access.BuildingId is not null
                && (access.Mode & (RoadAccessMode.Foot | RoadAccessMode.Motor)) == (RoadAccessMode.Foot | RoadAccessMode.Motor))
            .OrderBy(static access => access.Id.Value)
            .Take(InitialMobilityRouteCandidateLimit)
            .ToArray();
        if (accessPoints.Length < 2) return default;

        var pedestrianPair = FindInitialWalkingPair(accessPoints);
        var vehicleRoute = FindInitialVehicleRoute(roadSnapshot, accessPoints);
        if (pedestrianPair is null && vehicleRoute is null) return default;

        var pedestrianTarget = pedestrianPair is null ? 0 : Math.Max(1, participantCount / 2);
        var vehicleTarget = vehicleRoute is null ? 0 : Math.Max(1, participantCount - pedestrianTarget);
        if (pedestrianPair is null) vehicleTarget = participantCount;
        if (vehicleRoute is null) pedestrianTarget = participantCount;

        var pedestriansCreated = 0;
        if (pedestrianPair is { } pair)
        {
            for (var index = 0; index < pedestrianTarget; index++)
            {
                _ = CreatePedestrian(
                    new TripRequest(
                        new TripRequestId(checked(InitialMobilityTripRequestBase + (ulong)index)),
                        pair.Origin,
                        pair.Destination,
                        TravelMode.Foot),
                    walkingSpeedMetersPerSecond: 1.4d + ((index % 3) * 0.1d));
                pedestriansCreated++;
            }
        }

        var vehiclesCreated = 0;
        if (vehicleRoute is not null)
        {
            for (var index = 0; index < vehicleTarget; index++)
            {
                _ = CreateVehicle(
                    vehicleRoute,
                    initialSpeedMetersPerSecond: 4d + (index % 4));
                vehiclesCreated++;
            }
        }

        return new InitialMobilitySummary(
            pedestriansCreated + vehiclesCreated,
            pedestriansCreated,
            vehiclesCreated);
    }

    private (TripEndpoint Origin, TripEndpoint Destination)? FindInitialWalkingPair(
        RoadAccessPointSnapshot[] accessPoints)
    {
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
                    if (route.Legs.Count > 0 && route.TotalLengthMeters > 1d)
                        return (origin, destination);
                }
                catch (InvalidOperationException)
                {
                    // Regional road access can reference a building whose derived pedestrian
                    // access node is unavailable or disconnected. Such endpoints are unsuitable
                    // bootstrap candidates, so continue probing the remaining deterministic set.
                }
            }
        }
        return null;
    }

    private RouteResult? FindInitialVehicleRoute(
        RoadNetworkSnapshot roadSnapshot,
        RoadAccessPointSnapshot[] accessPoints)
    {
        var segments = roadSnapshot.Segments.ToDictionary(static segment => segment.Id);
        var nodes = roadSnapshot.Nodes.ToDictionary(static node => node.Id);

        for (var firstIndex = 0; firstIndex < accessPoints.Length - 1; firstIndex++)
        {
            var first = accessPoints[firstIndex];
            if (!TryResolveAccessPosition(first, segments, nodes, out var origin)) continue;
            for (var secondIndex = firstIndex + 1; secondIndex < accessPoints.Length; secondIndex++)
            {
                var second = accessPoints[secondIndex];
                if (first.SegmentId == second.SegmentId) continue;
                if (!TryResolveAccessPosition(second, segments, nodes, out var destination)) continue;
                var route = FindRoadRoute(new RouteRequest(origin, destination, RoutingCostMetric.EstimatedTravelTime));
                if (route.Steps.Count > 0 && route.TotalDistanceMeters > 1d) return route;
            }
        }
        return null;
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
}
