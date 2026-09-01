namespace MachiVerseWorks.Protocol;

public enum ProtocolGasNodeKind : byte
{
    Source = 0,
    ImportTerminal = 1,
    Storage = 2,
    Distribution = 3,
    Service = 4,
    Regulator = 5,
}

public enum ProtocolGasFacilityKind : byte
{
    Source = 0,
    ImportTerminal = 1,
    Storage = 2,
}

public enum ProtocolGasOperatingState : byte
{
    Online = 0,
    Offline = 1,
}

public enum ProtocolGasDeliveryMode : byte
{
    Piped = 0,
    Delivered = 1,
}

public enum ProtocolGasServiceState : byte
{
    Supplied = 0,
    Constrained = 1,
    Unavailable = 2,
}

public readonly record struct ProtocolGasStatistics(
    uint NodeCount,
    uint PipelineCount,
    uint SourceCount,
    uint ImportTerminalCount,
    uint StorageCount,
    uint ServicePointCount,
    uint PipedServicePointCount,
    uint DeliveredServicePointCount,
    uint UnavailableServicePointCount,
    double SupplyCapacityCubicMetersPerDay,
    double DemandCubicMetersPerDay,
    double ServedCubicMetersPerDay,
    double UnservedCubicMetersPerDay,
    double StoredCubicMeters,
    ulong TickCount);

public readonly record struct ProtocolGasNode(
    ulong NodeId,
    ProtocolGasNodeKind Kind,
    double X,
    double Y,
    double Z);

public readonly record struct ProtocolGasPipeline(
    ulong PipelineId,
    ulong FromNodeId,
    ulong ToNodeId,
    double CapacityCubicMetersPerDay,
    bool IsInService);

public readonly record struct ProtocolGasFacility(
    ProtocolGasFacilityKind Kind,
    ulong FacilityId,
    ulong NodeId,
    double CapacityCubicMetersPerDay,
    double OutputCubicMetersPerDay,
    double StoredCubicMeters,
    ProtocolGasOperatingState OperatingState);

public readonly record struct ProtocolGasServicePoint(
    ulong ServicePointId,
    ulong NodeId,
    ulong BuildingId,
    ulong EstablishmentId,
    ProtocolGasDeliveryMode DeliveryMode,
    ulong CommodityId,
    double BaseDemandCubicMetersPerDay,
    double DemandCubicMetersPerDay,
    double ServedCubicMetersPerDay,
    double UnservedCubicMetersPerDay,
    ProtocolGasServiceState ServiceState);

public sealed record GasSnapshotMessage(
    ProtocolGasStatistics Statistics,
    IReadOnlyList<ProtocolGasNode> Nodes,
    IReadOnlyList<ProtocolGasPipeline> Pipelines,
    IReadOnlyList<ProtocolGasFacility> Facilities,
    IReadOnlyList<ProtocolGasServicePoint> ServicePoints) : IProtocolMessage
{
    public MessageType Type => MessageType.GasSnapshot;
}
