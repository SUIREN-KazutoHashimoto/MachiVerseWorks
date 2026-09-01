namespace MachiVerseWorks.Protocol;

public enum ProtocolUtilityNetworkKind : byte
{
    Water = 0,
    Sewer = 1,
}

public enum ProtocolUtilityNodeKind : byte
{
    Source = 0,
    Reservoir = 1,
    Pump = 2,
    Distribution = 3,
    Service = 4,
    Collection = 5,
    Treatment = 6,
}

public enum ProtocolUtilityFacilityKind : byte
{
    WaterSource = 0,
    Reservoir = 1,
    WaterPump = 2,
    SewerPump = 3,
    SewageTreatmentPlant = 4,
}

public enum ProtocolUtilityOperatingState : byte
{
    Online = 0,
    Offline = 1,
}

public enum ProtocolWaterServiceState : byte
{
    Supplied = 0,
    Constrained = 1,
    Unavailable = 2,
}

public enum ProtocolSewerServiceState : byte
{
    Available = 0,
    Constrained = 1,
    Unavailable = 2,
    Overflow = 3,
}

public readonly record struct ProtocolWaterSewerStatistics(
    uint WaterNodeCount,
    uint WaterPipeCount,
    uint SewerNodeCount,
    uint SewerPipeCount,
    uint WaterSourceCount,
    uint ReservoirCount,
    uint PumpCount,
    uint TreatmentPlantCount,
    uint ServicePointCount,
    uint WaterUnavailableCount,
    uint SewerUnavailableCount,
    uint SewerOverflowCount,
    double WaterSupplyCapacityCubicMetersPerDay,
    double WaterDemandCubicMetersPerDay,
    double WaterServedCubicMetersPerDay,
    double WastewaterGeneratedCubicMetersPerDay,
    double WastewaterProcessedCubicMetersPerDay,
    double WastewaterOverflowCubicMetersPerDay,
    ulong TickCount);

public readonly record struct ProtocolUtilityNode(
    ProtocolUtilityNetworkKind NetworkKind,
    ulong NodeId,
    ProtocolUtilityNodeKind Kind,
    double X,
    double Y,
    double Z);

public readonly record struct ProtocolUtilityPipe(
    ProtocolUtilityNetworkKind NetworkKind,
    ulong PipeId,
    ulong FromNodeId,
    ulong ToNodeId,
    double CapacityCubicMetersPerDay,
    bool IsInService);

public readonly record struct ProtocolUtilityFacility(
    ProtocolUtilityFacilityKind Kind,
    ulong FacilityId,
    ulong NodeId,
    ulong PowerLoadId,
    double CapacityCubicMetersPerDay,
    double ThroughputCubicMetersPerDay,
    ProtocolUtilityOperatingState OperatingState);

public readonly record struct ProtocolWaterSewerServicePoint(
    ulong ServicePointId,
    ulong WaterNodeId,
    ulong SewerNodeId,
    ulong BuildingId,
    ulong EstablishmentId,
    double BaseWaterDemandCubicMetersPerDay,
    double WastewaterReturnRatio,
    double WaterDemandCubicMetersPerDay,
    double WaterServedCubicMetersPerDay,
    double WaterUnservedCubicMetersPerDay,
    ProtocolWaterServiceState WaterState,
    double WastewaterGeneratedCubicMetersPerDay,
    double WastewaterProcessedCubicMetersPerDay,
    double WastewaterOverflowCubicMetersPerDay,
    ProtocolSewerServiceState SewerState);

public sealed record WaterSewerSnapshotMessage(
    ProtocolWaterSewerStatistics Statistics,
    IReadOnlyList<ProtocolUtilityNode> Nodes,
    IReadOnlyList<ProtocolUtilityPipe> Pipes,
    IReadOnlyList<ProtocolUtilityFacility> Facilities,
    IReadOnlyList<ProtocolWaterSewerServicePoint> ServicePoints) : IProtocolMessage
{
    public MessageType Type => MessageType.WaterSewerSnapshot;
}
