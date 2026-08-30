namespace MachiVerseWorks.Simulation;

public readonly record struct PedestrianNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PedestrianEdgeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PedestrianCrossingId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PedestrianId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct TripRequestId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum TravelMode : byte
{
    Any = 0,
    Foot = 1,
    Motor = 2,
}

public enum PedestrianNodeKind : byte
{
    RoadJunction = 0,
    AccessPoint = 1,
}

public enum PedestrianMovementState : byte
{
    Walking = 0,
    WaitingForCrossing = 1,
    WaitingForOccupancy = 2,
    Arrived = 3,
}

public readonly record struct TripEndpoint(BuildingId? BuildingId, PoiId? PoiId)
{
    public static TripEndpoint ForBuilding(BuildingId buildingId) => new(buildingId, null);
    public static TripEndpoint ForPoi(PoiId poiId) => new(null, poiId);
}

public sealed record TripRequest(
    TripRequestId Id,
    TripEndpoint Origin,
    TripEndpoint Destination,
    TravelMode Mode = TravelMode.Any);

public readonly record struct PedestrianNodeSnapshot(
    PedestrianNodeId Id,
    PedestrianNodeKind Kind,
    WorldPoint Position,
    RoadNodeId? RoadNodeId,
    RoadAccessPointId? RoadAccessPointId);

public readonly record struct PedestrianEdgeSnapshot(
    PedestrianEdgeId Id,
    PedestrianNodeId FirstNodeId,
    PedestrianNodeId SecondNodeId,
    RoadSegmentId RoadSegmentId,
    double LengthMeters);

public readonly record struct PedestrianCrossingSnapshot(
    PedestrianCrossingId Id,
    RoadNodeId RoadNodeId,
    PedestrianEdgeId FirstEdgeId,
    PedestrianEdgeId SecondEdgeId,
    bool IsOpen);

public sealed record PedestrianNetworkSnapshot(
    IReadOnlyList<PedestrianNodeSnapshot> Nodes,
    IReadOnlyList<PedestrianEdgeSnapshot> Edges,
    IReadOnlyList<PedestrianCrossingSnapshot> Crossings);

public readonly record struct PedestrianRouteLeg(
    PedestrianEdgeId EdgeId,
    PedestrianNodeId FromNodeId,
    PedestrianNodeId ToNodeId,
    double LengthMeters);

public sealed record PedestrianRoute(
    PedestrianNodeId StartNodeId,
    PedestrianNodeId EndNodeId,
    double TotalLengthMeters,
    IReadOnlyList<PedestrianRouteLeg> Legs);

public readonly record struct PedestrianSnapshot(
    PedestrianId Id,
    TripRequestId TripRequestId,
    WorldPoint Position,
    WorldVector Velocity,
    double WalkingSpeedMetersPerSecond,
    PedestrianMovementState State,
    ulong TickCount);