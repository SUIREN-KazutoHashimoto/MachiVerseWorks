namespace MachiVerseWorks.Simulation;

public readonly record struct OpticalNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct FiberCableId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct OpticalEquipmentId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct OpticalBackhaulId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct OpticalDemandId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum OpticalNodeKind : byte
{
    BackboneGateway = 0,
    CentralOffice = 1,
    Distribution = 2,
    Access = 3,
    Endpoint = 4,
    DataCenter = 5,
}

public enum OpticalEquipmentKind : byte
{
    Olt = 0,
    Onu = 1,
    Splitter = 2,
    Switch = 3,
    Router = 4,
}

public enum OpticalDemandKind : byte
{
    Building = 0,
    Office = 1,
    DataCenter = 2,
    RadioBackhaul = 3,
}

public enum OpticalQualityState : byte
{
    Healthy = 0,
    Congested = 1,
    Degraded = 2,
    Unavailable = 3,
}

public readonly record struct OpticalNodeSnapshot(OpticalNodeId Id, OpticalNodeKind Kind, WorldPoint Position);

public readonly record struct FiberCableSnapshot(
    FiberCableId Id,
    OpticalNodeId FromNodeId,
    OpticalNodeId ToNodeId,
    double CapacityGigabitsPerSecond,
    double LoadGigabitsPerSecond,
    double Utilization,
    bool IsInService,
    bool IsCongested);

public readonly record struct OpticalEquipmentSnapshot(
    OpticalEquipmentId Id,
    OpticalNodeId NodeId,
    OpticalEquipmentKind Kind,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double CapacityGigabitsPerSecond,
    bool RequiresPower,
    bool IsInService,
    bool IsPowered,
    bool IsOperational);

public readonly record struct OpticalBackhaulSnapshot(
    OpticalBackhaulId Id,
    OpticalNodeId NodeId,
    double CapacityGigabitsPerSecond,
    double AllocatedGigabitsPerSecond,
    double Utilization,
    bool IsInService,
    bool IsOperational);

public readonly record struct OpticalDemandSnapshot(
    OpticalDemandId Id,
    OpticalNodeId NodeId,
    OpticalDemandKind Kind,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double BaseDemandGigabitsPerSecond,
    double DemandGigabitsPerSecond,
    double AllocatedGigabitsPerSecond,
    OpticalQualityState QualityState,
    OpticalBackhaulId? BackhaulId,
    IReadOnlyList<FiberCableId> RouteCableIds);

public readonly record struct OpticalStatistics(
    int NodeCount,
    int FiberCableCount,
    int EquipmentCount,
    int BackhaulCount,
    int DemandCount,
    int ConnectedDemandCount,
    int CongestedDemandCount,
    int DegradedDemandCount,
    int UnavailableDemandCount,
    double BackhaulCapacityGigabitsPerSecond,
    double DemandGigabitsPerSecond,
    double AllocatedGigabitsPerSecond,
    double PeakFiberUtilization,
    ulong TickCount);

public sealed record OpticalSnapshot(
    OpticalStatistics Statistics,
    IReadOnlyList<OpticalNodeSnapshot> Nodes,
    IReadOnlyList<FiberCableSnapshot> FiberCables,
    IReadOnlyList<OpticalEquipmentSnapshot> Equipment,
    IReadOnlyList<OpticalBackhaulSnapshot> Backhauls,
    IReadOnlyList<OpticalDemandSnapshot> Demands);

public sealed record OpticalCheckpoint(
    ulong NextNodeId,
    ulong NextFiberCableId,
    ulong NextEquipmentId,
    ulong NextBackhaulId,
    ulong NextDemandId,
    IReadOnlyList<SimulationOpticalNodeCheckpoint> Nodes,
    IReadOnlyList<SimulationFiberCableCheckpoint> FiberCables,
    IReadOnlyList<SimulationOpticalEquipmentCheckpoint> Equipment,
    IReadOnlyList<SimulationOpticalBackhaulCheckpoint> Backhauls,
    IReadOnlyList<SimulationOpticalDemandCheckpoint> Demands);

public readonly record struct SimulationOpticalNodeCheckpoint(OpticalNodeId Id, OpticalNodeKind Kind, WorldPoint Position);

public readonly record struct SimulationFiberCableCheckpoint(
    FiberCableId Id,
    OpticalNodeId FromNodeId,
    OpticalNodeId ToNodeId,
    double CapacityGigabitsPerSecond,
    double LoadGigabitsPerSecond,
    bool IsInService);

public readonly record struct SimulationOpticalEquipmentCheckpoint(
    OpticalEquipmentId Id,
    OpticalNodeId NodeId,
    OpticalEquipmentKind Kind,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double CapacityGigabitsPerSecond,
    bool RequiresPower,
    bool IsInService,
    bool IsPowered);

public readonly record struct SimulationOpticalBackhaulCheckpoint(
    OpticalBackhaulId Id,
    OpticalNodeId NodeId,
    double CapacityGigabitsPerSecond,
    double AllocatedGigabitsPerSecond,
    bool IsInService);

public readonly record struct SimulationOpticalDemandCheckpoint(
    OpticalDemandId Id,
    OpticalNodeId NodeId,
    OpticalDemandKind Kind,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double BaseDemandGigabitsPerSecond,
    double DemandGigabitsPerSecond,
    double AllocatedGigabitsPerSecond,
    OpticalQualityState QualityState,
    OpticalBackhaulId? BackhaulId,
    IReadOnlyList<FiberCableId> RouteCableIds);

public readonly record struct OpticalRoutingNode(OpticalNodeId Id, bool IsAvailable);

public readonly record struct OpticalRoutingCable(
    FiberCableId Id,
    OpticalNodeId FromNodeId,
    OpticalNodeId ToNodeId,
    double CapacityGigabitsPerSecond,
    bool IsInService);

public readonly record struct OpticalRoutingEndpoint(
    OpticalNodeId NodeId,
    double CapacityGigabitsPerSecond,
    bool IsOperational);

public readonly record struct OpticalRoutingBackhaul(
    OpticalBackhaulId Id,
    OpticalNodeId NodeId,
    double CapacityGigabitsPerSecond,
    bool IsOperational);

public readonly record struct OpticalRoutingDemand(
    OpticalDemandId Id,
    OpticalNodeId NodeId,
    double RequestedGigabitsPerSecond,
    byte Priority);

public sealed record OpticalRoutingRequest(
    IReadOnlyList<OpticalRoutingNode> Nodes,
    IReadOnlyList<OpticalRoutingCable> FiberCables,
    IReadOnlyList<OpticalRoutingEndpoint> Endpoints,
    IReadOnlyList<OpticalRoutingBackhaul> Backhauls,
    IReadOnlyList<OpticalRoutingDemand> Demands);

public readonly record struct OpticalDemandRouteResult(
    OpticalDemandId DemandId,
    OpticalBackhaulId? BackhaulId,
    double AllocatedGigabitsPerSecond,
    IReadOnlyList<FiberCableId> RouteCableIds);

public readonly record struct OpticalFiberLoadResult(FiberCableId FiberCableId, double LoadGigabitsPerSecond);
public readonly record struct OpticalBackhaulLoadResult(OpticalBackhaulId BackhaulId, double AllocatedGigabitsPerSecond);

public sealed record OpticalRoutingResult(
    IReadOnlyList<OpticalDemandRouteResult> Demands,
    IReadOnlyList<OpticalFiberLoadResult> FiberCables,
    IReadOnlyList<OpticalBackhaulLoadResult> Backhauls);

public interface IOpticalRoutingSolver
{
    OpticalRoutingResult Solve(OpticalRoutingRequest request);
}

public static class OpticalDefaults
{
    public const double BandwidthEpsilonGigabitsPerSecond = 1e-9;
    public const double CongestionThreshold = 0.85d;
}

internal sealed class OpticalNodeState(OpticalNodeId id, OpticalNodeKind kind, WorldPoint position)
{
    public OpticalNodeId Id { get; } = id;
    public OpticalNodeKind Kind { get; } = kind;
    public WorldPoint Position { get; } = position;
}

internal sealed class FiberCableState(
    FiberCableId id,
    OpticalNodeId fromNodeId,
    OpticalNodeId toNodeId,
    double capacityGigabitsPerSecond,
    bool isInService)
{
    public FiberCableId Id { get; } = id;
    public OpticalNodeId FromNodeId { get; } = fromNodeId;
    public OpticalNodeId ToNodeId { get; } = toNodeId;
    public double CapacityGigabitsPerSecond { get; } = capacityGigabitsPerSecond;
    public double LoadGigabitsPerSecond { get; set; }
    public bool IsInService { get; set; } = isInService;
}

internal sealed class OpticalEquipmentState(
    OpticalEquipmentId id,
    OpticalNodeId nodeId,
    OpticalEquipmentKind kind,
    BuildingId? buildingId,
    EstablishmentId? establishmentId,
    double capacityGigabitsPerSecond,
    bool requiresPower,
    bool isInService)
{
    public OpticalEquipmentId Id { get; } = id;
    public OpticalNodeId NodeId { get; } = nodeId;
    public OpticalEquipmentKind Kind { get; } = kind;
    public BuildingId? BuildingId { get; } = buildingId;
    public EstablishmentId? EstablishmentId { get; } = establishmentId;
    public double CapacityGigabitsPerSecond { get; } = capacityGigabitsPerSecond;
    public bool RequiresPower { get; } = requiresPower;
    public bool IsInService { get; set; } = isInService;
    public bool IsPowered { get; set; } = true;
}

internal sealed class OpticalBackhaulState(
    OpticalBackhaulId id,
    OpticalNodeId nodeId,
    double capacityGigabitsPerSecond,
    bool isInService)
{
    public OpticalBackhaulId Id { get; } = id;
    public OpticalNodeId NodeId { get; } = nodeId;
    public double CapacityGigabitsPerSecond { get; } = capacityGigabitsPerSecond;
    public double AllocatedGigabitsPerSecond { get; set; }
    public bool IsInService { get; set; } = isInService;
}

internal sealed class OpticalDemandState(
    OpticalDemandId id,
    OpticalNodeId nodeId,
    OpticalDemandKind kind,
    BuildingId? buildingId,
    EstablishmentId? establishmentId,
    double baseDemandGigabitsPerSecond)
{
    public OpticalDemandId Id { get; } = id;
    public OpticalNodeId NodeId { get; } = nodeId;
    public OpticalDemandKind Kind { get; } = kind;
    public BuildingId? BuildingId { get; } = buildingId;
    public EstablishmentId? EstablishmentId { get; } = establishmentId;
    public double BaseDemandGigabitsPerSecond { get; } = baseDemandGigabitsPerSecond;
    public double DemandGigabitsPerSecond { get; set; }
    public double AllocatedGigabitsPerSecond { get; set; }
    public OpticalQualityState QualityState { get; set; } = OpticalQualityState.Unavailable;
    public OpticalBackhaulId? BackhaulId { get; set; }
    public IReadOnlyList<FiberCableId> RouteCableIds { get; set; } = Array.Empty<FiberCableId>();
}
