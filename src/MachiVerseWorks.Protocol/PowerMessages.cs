namespace MachiVerseWorks.Protocol;

public enum ProtocolPowerNodeKind : byte
{
    GeneratorBus = 0,
    Substation = 1,
    Distribution = 2,
    Load = 3,
}

public enum ProtocolGeneratorOperatingState : byte
{
    Online = 0,
    Offline = 1,
}

public enum ProtocolPowerSupplyState : byte
{
    Supplied = 0,
    Constrained = 1,
    Outage = 2,
}

public readonly record struct ProtocolPowerStatistics(
    uint NodeCount,
    uint LineCount,
    uint GeneratorCount,
    uint LoadCount,
    uint OutageLoadCount,
    double GenerationCapacityMegawatts,
    double GenerationOutputMegawatts,
    double DemandMegawatts,
    double ServedMegawatts,
    double UnservedMegawatts,
    ulong TickCount);

public readonly record struct ProtocolPowerNode(
    ulong NodeId,
    ProtocolPowerNodeKind Kind,
    double X,
    double Y,
    double Z);

public readonly record struct ProtocolPowerLine(
    ulong LineId,
    ulong FromNodeId,
    ulong ToNodeId,
    double CapacityMegawatts,
    bool IsInService);

public readonly record struct ProtocolGenerator(
    ulong GeneratorId,
    ulong NodeId,
    double CapacityMegawatts,
    double OutputMegawatts,
    ProtocolGeneratorOperatingState OperatingState);

public readonly record struct ProtocolPowerLoad(
    ulong LoadId,
    ulong NodeId,
    ulong BuildingId,
    ulong EstablishmentId,
    double BaseDemandMegawatts,
    double DemandMegawatts,
    double ServedMegawatts,
    double UnservedMegawatts,
    ProtocolPowerSupplyState SupplyState);

public sealed record PowerSnapshotMessage(
    ProtocolPowerStatistics Statistics,
    IReadOnlyList<ProtocolPowerNode> Nodes,
    IReadOnlyList<ProtocolPowerLine> Lines,
    IReadOnlyList<ProtocolGenerator> Generators,
    IReadOnlyList<ProtocolPowerLoad> Loads) : IProtocolMessage
{
    public MessageType Type => MessageType.PowerSnapshot;
}
