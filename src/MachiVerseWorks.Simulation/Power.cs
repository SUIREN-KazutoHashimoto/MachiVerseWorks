namespace MachiVerseWorks.Simulation;

public readonly record struct PowerNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PowerLineId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct GeneratorId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PowerLoadId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum PowerNodeKind : byte
{
    GeneratorBus = 0,
    Substation = 1,
    Distribution = 2,
    Load = 3,
}

public enum GeneratorOperatingState : byte
{
    Online = 0,
    Offline = 1,
}

public enum PowerSupplyState : byte
{
    Supplied = 0,
    Constrained = 1,
    Outage = 2,
}

public readonly record struct PowerNodeSnapshot(PowerNodeId Id, PowerNodeKind Kind, WorldPoint Position);

public readonly record struct PowerLineSnapshot(
    PowerLineId Id,
    PowerNodeId FromNodeId,
    PowerNodeId ToNodeId,
    double CapacityMegawatts,
    bool IsInService);

public readonly record struct GeneratorSnapshot(
    GeneratorId Id,
    PowerNodeId NodeId,
    double CapacityMegawatts,
    double OutputMegawatts,
    GeneratorOperatingState OperatingState);

public readonly record struct PowerLoadSnapshot(
    PowerLoadId Id,
    PowerNodeId NodeId,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double BaseDemandMegawatts,
    double DemandMegawatts,
    double ServedMegawatts,
    double UnservedMegawatts,
    PowerSupplyState SupplyState);

public readonly record struct PowerStatistics(
    int NodeCount,
    int LineCount,
    int GeneratorCount,
    int LoadCount,
    int OutageLoadCount,
    double GenerationCapacityMegawatts,
    double GenerationOutputMegawatts,
    double DemandMegawatts,
    double ServedMegawatts,
    double UnservedMegawatts,
    ulong TickCount);

public sealed record PowerSnapshot(
    PowerStatistics Statistics,
    IReadOnlyList<PowerNodeSnapshot> Nodes,
    IReadOnlyList<PowerLineSnapshot> Lines,
    IReadOnlyList<GeneratorSnapshot> Generators,
    IReadOnlyList<PowerLoadSnapshot> Loads);

public sealed record PowerCheckpoint(
    ulong NextNodeId,
    ulong NextLineId,
    ulong NextGeneratorId,
    ulong NextLoadId,
    IReadOnlyList<SimulationPowerNodeCheckpoint> Nodes,
    IReadOnlyList<SimulationPowerLineCheckpoint> Lines,
    IReadOnlyList<SimulationGeneratorCheckpoint> Generators,
    IReadOnlyList<SimulationPowerLoadCheckpoint> Loads);

public readonly record struct SimulationPowerNodeCheckpoint(PowerNodeId Id, PowerNodeKind Kind, WorldPoint Position);
public readonly record struct SimulationPowerLineCheckpoint(PowerLineId Id, PowerNodeId FromNodeId, PowerNodeId ToNodeId, double CapacityMegawatts, bool IsInService);
public readonly record struct SimulationGeneratorCheckpoint(GeneratorId Id, PowerNodeId NodeId, double CapacityMegawatts, double OutputMegawatts, GeneratorOperatingState OperatingState);
public readonly record struct SimulationPowerLoadCheckpoint(
    PowerLoadId Id,
    PowerNodeId NodeId,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double BaseDemandMegawatts,
    double DemandMegawatts,
    double ServedMegawatts,
    double UnservedMegawatts,
    PowerSupplyState SupplyState);

public readonly record struct PowerDispatchNode(PowerNodeId Id);
public readonly record struct PowerDispatchLine(PowerLineId Id, PowerNodeId FromNodeId, PowerNodeId ToNodeId, double CapacityMegawatts, bool IsInService);
public readonly record struct PowerDispatchGenerator(GeneratorId Id, PowerNodeId NodeId, double AvailableCapacityMegawatts);
public readonly record struct PowerDispatchLoad(PowerLoadId Id, PowerNodeId NodeId, double DemandMegawatts);
public readonly record struct PowerGeneratorDispatch(GeneratorId GeneratorId, double OutputMegawatts);
public readonly record struct PowerLoadDispatch(PowerLoadId LoadId, double ServedMegawatts);

public sealed record PowerDispatchRequest(
    IReadOnlyList<PowerDispatchNode> Nodes,
    IReadOnlyList<PowerDispatchLine> Lines,
    IReadOnlyList<PowerDispatchGenerator> Generators,
    IReadOnlyList<PowerDispatchLoad> Loads);

public sealed record PowerDispatchResult(
    IReadOnlyList<PowerGeneratorDispatch> Generators,
    IReadOnlyList<PowerLoadDispatch> Loads);

public interface IPowerDispatchSolver
{
    PowerDispatchResult Solve(PowerDispatchRequest request);
}

public static class PowerDefaults
{
    public const double SupplyEpsilonMegawatts = 1e-9;
}
