namespace MachiVerseWorks.Simulation;

public readonly record struct GasNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GasPipelineId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GasSourceId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GasImportTerminalId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GasStorageId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GasServicePointId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum GasNodeKind : byte
{
    Source = 0,
    ImportTerminal = 1,
    Storage = 2,
    Distribution = 3,
    Service = 4,
    Regulator = 5,
}

public enum GasOperatingState : byte
{
    Online = 0,
    Offline = 1,
}

public enum GasDeliveryMode : byte
{
    Piped = 0,
    Delivered = 1,
}

public enum GasServiceState : byte
{
    Supplied = 0,
    Constrained = 1,
    Unavailable = 2,
}

public readonly record struct GasNodeSnapshot(GasNodeId Id, GasNodeKind Kind, WorldPoint Position);
public readonly record struct GasPipelineSnapshot(GasPipelineId Id, GasNodeId FromNodeId, GasNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct GasSourceSnapshot(GasSourceId Id, GasNodeId NodeId, double CapacityCubicMetersPerDay, double OutputCubicMetersPerDay, GasOperatingState OperatingState);
public readonly record struct GasImportTerminalSnapshot(GasImportTerminalId Id, GasNodeId NodeId, double CapacityCubicMetersPerDay, double OutputCubicMetersPerDay, GasOperatingState OperatingState);
public readonly record struct GasStorageSnapshot(GasStorageId Id, GasNodeId NodeId, double CapacityCubicMeters, double StoredCubicMeters, double ReleaseCapacityCubicMetersPerDay, double OutputCubicMetersPerDay, GasOperatingState OperatingState);

public readonly record struct GasServicePointSnapshot(
    GasServicePointId Id,
    GasNodeId? NodeId,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    GasDeliveryMode DeliveryMode,
    CommodityId? CommodityId,
    double BaseDemandCubicMetersPerDay,
    double DemandCubicMetersPerDay,
    double ServedCubicMetersPerDay,
    double UnservedCubicMetersPerDay,
    GasServiceState ServiceState);

public readonly record struct GasStatistics(
    int NodeCount,
    int PipelineCount,
    int SourceCount,
    int ImportTerminalCount,
    int StorageCount,
    int ServicePointCount,
    int PipedServicePointCount,
    int DeliveredServicePointCount,
    int UnavailableServicePointCount,
    double SupplyCapacityCubicMetersPerDay,
    double DemandCubicMetersPerDay,
    double ServedCubicMetersPerDay,
    double UnservedCubicMetersPerDay,
    double StoredCubicMeters,
    ulong TickCount);

public sealed record GasSnapshot(
    GasStatistics Statistics,
    IReadOnlyList<GasNodeSnapshot> Nodes,
    IReadOnlyList<GasPipelineSnapshot> Pipelines,
    IReadOnlyList<GasSourceSnapshot> Sources,
    IReadOnlyList<GasImportTerminalSnapshot> ImportTerminals,
    IReadOnlyList<GasStorageSnapshot> Storages,
    IReadOnlyList<GasServicePointSnapshot> ServicePoints);

public sealed record GasCheckpoint(
    ulong NextNodeId,
    ulong NextPipelineId,
    ulong NextSourceId,
    ulong NextImportTerminalId,
    ulong NextStorageId,
    ulong NextServicePointId,
    IReadOnlyList<SimulationGasNodeCheckpoint> Nodes,
    IReadOnlyList<SimulationGasPipelineCheckpoint> Pipelines,
    IReadOnlyList<SimulationGasSourceCheckpoint> Sources,
    IReadOnlyList<SimulationGasImportTerminalCheckpoint> ImportTerminals,
    IReadOnlyList<SimulationGasStorageCheckpoint> Storages,
    IReadOnlyList<SimulationGasServicePointCheckpoint> ServicePoints);

public readonly record struct SimulationGasNodeCheckpoint(GasNodeId Id, GasNodeKind Kind, WorldPoint Position);
public readonly record struct SimulationGasPipelineCheckpoint(GasPipelineId Id, GasNodeId FromNodeId, GasNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct SimulationGasSourceCheckpoint(GasSourceId Id, GasNodeId NodeId, double CapacityCubicMetersPerDay, double OutputCubicMetersPerDay, GasOperatingState OperatingState);
public readonly record struct SimulationGasImportTerminalCheckpoint(GasImportTerminalId Id, GasNodeId NodeId, double CapacityCubicMetersPerDay, double OutputCubicMetersPerDay, GasOperatingState OperatingState);
public readonly record struct SimulationGasStorageCheckpoint(GasStorageId Id, GasNodeId NodeId, double CapacityCubicMeters, double StoredCubicMeters, double ReleaseCapacityCubicMetersPerDay, double OutputCubicMetersPerDay, GasOperatingState OperatingState);
public readonly record struct SimulationGasServicePointCheckpoint(
    GasServicePointId Id,
    GasNodeId? NodeId,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    GasDeliveryMode DeliveryMode,
    CommodityId? CommodityId,
    double BaseDemandCubicMetersPerDay,
    double DemandCubicMetersPerDay,
    double ServedCubicMetersPerDay,
    double UnservedCubicMetersPerDay,
    GasServiceState ServiceState);

public readonly record struct GasSupplyNode(GasNodeId Id);
public readonly record struct GasSupplyPipeline(GasPipelineId Id, GasNodeId FromNodeId, GasNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct GasSupplySource(GasSourceId Id, GasNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct GasSupplyImportTerminal(GasImportTerminalId Id, GasNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct GasSupplyStorage(GasStorageId Id, GasNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct GasSupplyLoad(GasServicePointId Id, GasNodeId NodeId, double DemandCubicMetersPerDay);

public sealed record GasSupplyRequest(
    IReadOnlyList<GasSupplyNode> Nodes,
    IReadOnlyList<GasSupplyPipeline> Pipelines,
    IReadOnlyList<GasSupplySource> Sources,
    IReadOnlyList<GasSupplyImportTerminal> ImportTerminals,
    IReadOnlyList<GasSupplyStorage> Storages,
    IReadOnlyList<GasSupplyLoad> Loads);

public readonly record struct GasSourceDispatch(GasSourceId Id, double OutputCubicMetersPerDay);
public readonly record struct GasImportTerminalDispatch(GasImportTerminalId Id, double OutputCubicMetersPerDay);
public readonly record struct GasStorageDispatch(GasStorageId Id, double OutputCubicMetersPerDay);
public readonly record struct GasLoadDispatch(GasServicePointId Id, double ServedCubicMetersPerDay);

public sealed record GasSupplyResult(
    IReadOnlyList<GasSourceDispatch> Sources,
    IReadOnlyList<GasImportTerminalDispatch> ImportTerminals,
    IReadOnlyList<GasStorageDispatch> Storages,
    IReadOnlyList<GasLoadDispatch> Loads);

public interface IGasSupplySolver
{
    GasSupplyResult Solve(GasSupplyRequest request);
}

public static class GasDefaults
{
    public const double FlowEpsilonCubicMetersPerDay = 1e-9;
}

internal sealed class GasNodeState(GasNodeId id, GasNodeKind kind, WorldPoint position)
{
    public GasNodeId Id { get; } = id;
    public GasNodeKind Kind { get; } = kind;
    public WorldPoint Position { get; } = position;
}

internal sealed class GasPipelineState(GasPipelineId id, GasNodeId fromNodeId, GasNodeId toNodeId, double capacityCubicMetersPerDay, bool isInService)
{
    public GasPipelineId Id { get; } = id;
    public GasNodeId FromNodeId { get; } = fromNodeId;
    public GasNodeId ToNodeId { get; } = toNodeId;
    public double CapacityCubicMetersPerDay { get; } = capacityCubicMetersPerDay;
    public bool IsInService { get; set; } = isInService;
}

internal sealed class GasSourceStateData(GasSourceId id, GasNodeId nodeId, double capacityCubicMetersPerDay, GasOperatingState operatingState)
{
    public GasSourceId Id { get; } = id;
    public GasNodeId NodeId { get; } = nodeId;
    public double CapacityCubicMetersPerDay { get; } = capacityCubicMetersPerDay;
    public double OutputCubicMetersPerDay { get; set; }
    public GasOperatingState OperatingState { get; set; } = operatingState;
}

internal sealed class GasImportTerminalStateData(GasImportTerminalId id, GasNodeId nodeId, double capacityCubicMetersPerDay, GasOperatingState operatingState)
{
    public GasImportTerminalId Id { get; } = id;
    public GasNodeId NodeId { get; } = nodeId;
    public double CapacityCubicMetersPerDay { get; } = capacityCubicMetersPerDay;
    public double OutputCubicMetersPerDay { get; set; }
    public GasOperatingState OperatingState { get; set; } = operatingState;
}

internal sealed class GasStorageStateData(GasStorageId id, GasNodeId nodeId, double capacityCubicMeters, double storedCubicMeters, double releaseCapacityCubicMetersPerDay, GasOperatingState operatingState)
{
    public GasStorageId Id { get; } = id;
    public GasNodeId NodeId { get; } = nodeId;
    public double CapacityCubicMeters { get; } = capacityCubicMeters;
    public double StoredCubicMeters { get; set; } = storedCubicMeters;
    public double ReleaseCapacityCubicMetersPerDay { get; } = releaseCapacityCubicMetersPerDay;
    public double OutputCubicMetersPerDay { get; set; }
    public GasOperatingState OperatingState { get; set; } = operatingState;
}

internal sealed class GasServicePointStateData(
    GasServicePointId id,
    GasNodeId? nodeId,
    BuildingId? buildingId,
    EstablishmentId? establishmentId,
    GasDeliveryMode deliveryMode,
    CommodityId? commodityId,
    double baseDemandCubicMetersPerDay)
{
    public GasServicePointId Id { get; } = id;
    public GasNodeId? NodeId { get; } = nodeId;
    public BuildingId? BuildingId { get; } = buildingId;
    public EstablishmentId? EstablishmentId { get; } = establishmentId;
    public GasDeliveryMode DeliveryMode { get; } = deliveryMode;
    public CommodityId? CommodityId { get; } = commodityId;
    public double BaseDemandCubicMetersPerDay { get; } = baseDemandCubicMetersPerDay;
    public double DemandCubicMetersPerDay { get; set; }
    public double ServedCubicMetersPerDay { get; set; }
    public double UnservedCubicMetersPerDay { get; set; }
    public GasServiceState ServiceState { get; set; } = GasServiceState.Unavailable;
}
