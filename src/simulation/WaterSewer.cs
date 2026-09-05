namespace MachiVerseWorks.Simulation;

public readonly record struct WaterNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WaterPipeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct SewerNodeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct SewerPipeId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WaterSourceId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct ReservoirId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PumpId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct SewageTreatmentPlantId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct WaterSewerServicePointId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum WaterNodeKind : byte
{
    Source = 0,
    Reservoir = 1,
    Pump = 2,
    Distribution = 3,
    Service = 4,
}

public enum SewerNodeKind : byte
{
    Service = 0,
    Collection = 1,
    Pump = 2,
    Treatment = 3,
}

public enum UtilityOperatingState : byte
{
    Online = 0,
    Offline = 1,
}

public enum PumpNetworkKind : byte
{
    Water = 0,
    Sewer = 1,
}

public enum WaterServiceState : byte
{
    Supplied = 0,
    Constrained = 1,
    Unavailable = 2,
}

public enum SewerServiceState : byte
{
    Available = 0,
    Constrained = 1,
    Unavailable = 2,
    Overflow = 3,
}

public readonly record struct WaterNodeSnapshot(WaterNodeId Id, WaterNodeKind Kind, WorldPoint Position);
public readonly record struct SewerNodeSnapshot(SewerNodeId Id, SewerNodeKind Kind, WorldPoint Position);
public readonly record struct WaterPipeSnapshot(WaterPipeId Id, WaterNodeId FromNodeId, WaterNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct SewerPipeSnapshot(SewerPipeId Id, SewerNodeId FromNodeId, SewerNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct WaterSourceSnapshot(WaterSourceId Id, WaterNodeId NodeId, double CapacityCubicMetersPerDay, double OutputCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct ReservoirSnapshot(ReservoirId Id, WaterNodeId NodeId, double ReleaseCapacityCubicMetersPerDay, double OutputCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct PumpSnapshot(PumpId Id, PumpNetworkKind NetworkKind, WaterNodeId? WaterNodeId, SewerNodeId? SewerNodeId, PowerLoadId? PowerLoadId, double CapacityCubicMetersPerDay, double ThroughputCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct SewageTreatmentPlantSnapshot(SewageTreatmentPlantId Id, SewerNodeId NodeId, PowerLoadId? PowerLoadId, double CapacityCubicMetersPerDay, double ProcessedCubicMetersPerDay, UtilityOperatingState OperatingState);

public readonly record struct WaterSewerServicePointSnapshot(
    WaterSewerServicePointId Id,
    WaterNodeId WaterNodeId,
    SewerNodeId SewerNodeId,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double BaseWaterDemandCubicMetersPerDay,
    double WastewaterReturnRatio,
    double WaterDemandCubicMetersPerDay,
    double WaterServedCubicMetersPerDay,
    double WaterUnservedCubicMetersPerDay,
    WaterServiceState WaterState,
    double WastewaterGeneratedCubicMetersPerDay,
    double WastewaterProcessedCubicMetersPerDay,
    double WastewaterOverflowCubicMetersPerDay,
    SewerServiceState SewerState);

public readonly record struct WaterSewerStatistics(
    int WaterNodeCount,
    int WaterPipeCount,
    int SewerNodeCount,
    int SewerPipeCount,
    int WaterSourceCount,
    int ReservoirCount,
    int PumpCount,
    int TreatmentPlantCount,
    int ServicePointCount,
    int WaterUnavailableCount,
    int SewerUnavailableCount,
    int SewerOverflowCount,
    double WaterSupplyCapacityCubicMetersPerDay,
    double WaterDemandCubicMetersPerDay,
    double WaterServedCubicMetersPerDay,
    double WastewaterGeneratedCubicMetersPerDay,
    double WastewaterProcessedCubicMetersPerDay,
    double WastewaterOverflowCubicMetersPerDay,
    ulong TickCount);

public sealed record WaterSewerSnapshot(
    WaterSewerStatistics Statistics,
    IReadOnlyList<WaterNodeSnapshot> WaterNodes,
    IReadOnlyList<WaterPipeSnapshot> WaterPipes,
    IReadOnlyList<SewerNodeSnapshot> SewerNodes,
    IReadOnlyList<SewerPipeSnapshot> SewerPipes,
    IReadOnlyList<WaterSourceSnapshot> WaterSources,
    IReadOnlyList<ReservoirSnapshot> Reservoirs,
    IReadOnlyList<PumpSnapshot> Pumps,
    IReadOnlyList<SewageTreatmentPlantSnapshot> TreatmentPlants,
    IReadOnlyList<WaterSewerServicePointSnapshot> ServicePoints);

public sealed record WaterSewerCheckpoint(
    ulong NextWaterNodeId,
    ulong NextWaterPipeId,
    ulong NextSewerNodeId,
    ulong NextSewerPipeId,
    ulong NextWaterSourceId,
    ulong NextReservoirId,
    ulong NextPumpId,
    ulong NextTreatmentPlantId,
    ulong NextServicePointId,
    IReadOnlyList<SimulationWaterNodeCheckpoint> WaterNodes,
    IReadOnlyList<SimulationWaterPipeCheckpoint> WaterPipes,
    IReadOnlyList<SimulationSewerNodeCheckpoint> SewerNodes,
    IReadOnlyList<SimulationSewerPipeCheckpoint> SewerPipes,
    IReadOnlyList<SimulationWaterSourceCheckpoint> WaterSources,
    IReadOnlyList<SimulationReservoirCheckpoint> Reservoirs,
    IReadOnlyList<SimulationPumpCheckpoint> Pumps,
    IReadOnlyList<SimulationSewageTreatmentPlantCheckpoint> TreatmentPlants,
    IReadOnlyList<SimulationWaterSewerServicePointCheckpoint> ServicePoints);

public readonly record struct SimulationWaterNodeCheckpoint(WaterNodeId Id, WaterNodeKind Kind, WorldPoint Position);
public readonly record struct SimulationWaterPipeCheckpoint(WaterPipeId Id, WaterNodeId FromNodeId, WaterNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct SimulationSewerNodeCheckpoint(SewerNodeId Id, SewerNodeKind Kind, WorldPoint Position);
public readonly record struct SimulationSewerPipeCheckpoint(SewerPipeId Id, SewerNodeId FromNodeId, SewerNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct SimulationWaterSourceCheckpoint(WaterSourceId Id, WaterNodeId NodeId, double CapacityCubicMetersPerDay, double OutputCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct SimulationReservoirCheckpoint(ReservoirId Id, WaterNodeId NodeId, double ReleaseCapacityCubicMetersPerDay, double OutputCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct SimulationPumpCheckpoint(PumpId Id, PumpNetworkKind NetworkKind, WaterNodeId? WaterNodeId, SewerNodeId? SewerNodeId, PowerLoadId? PowerLoadId, double CapacityCubicMetersPerDay, double ThroughputCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct SimulationSewageTreatmentPlantCheckpoint(SewageTreatmentPlantId Id, SewerNodeId NodeId, PowerLoadId? PowerLoadId, double CapacityCubicMetersPerDay, double ProcessedCubicMetersPerDay, UtilityOperatingState OperatingState);
public readonly record struct SimulationWaterSewerServicePointCheckpoint(
    WaterSewerServicePointId Id,
    WaterNodeId WaterNodeId,
    SewerNodeId SewerNodeId,
    BuildingId? BuildingId,
    EstablishmentId? EstablishmentId,
    double BaseWaterDemandCubicMetersPerDay,
    double WastewaterReturnRatio,
    double WaterDemandCubicMetersPerDay,
    double WaterServedCubicMetersPerDay,
    double WaterUnservedCubicMetersPerDay,
    WaterServiceState WaterState,
    double WastewaterGeneratedCubicMetersPerDay,
    double WastewaterProcessedCubicMetersPerDay,
    double WastewaterOverflowCubicMetersPerDay,
    SewerServiceState SewerState);

public readonly record struct WaterSupplyNode(WaterNodeId Id);
public readonly record struct WaterSupplyPipe(WaterPipeId Id, WaterNodeId FromNodeId, WaterNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct WaterSupplySource(WaterSourceId Id, WaterNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct WaterSupplyReservoir(ReservoirId Id, WaterNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct WaterSupplyPump(PumpId Id, WaterNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct WaterSupplyLoad(WaterSewerServicePointId Id, WaterNodeId NodeId, double DemandCubicMetersPerDay);
public readonly record struct WaterSourceDispatch(WaterSourceId Id, double OutputCubicMetersPerDay);
public readonly record struct ReservoirDispatch(ReservoirId Id, double OutputCubicMetersPerDay);
public readonly record struct PumpDispatch(PumpId Id, double ThroughputCubicMetersPerDay);
public readonly record struct WaterLoadDispatch(WaterSewerServicePointId Id, double ServedCubicMetersPerDay);

public sealed record WaterSupplyRequest(
    IReadOnlyList<WaterSupplyNode> Nodes,
    IReadOnlyList<WaterSupplyPipe> Pipes,
    IReadOnlyList<WaterSupplySource> Sources,
    IReadOnlyList<WaterSupplyReservoir> Reservoirs,
    IReadOnlyList<WaterSupplyPump> Pumps,
    IReadOnlyList<WaterSupplyLoad> Loads);

public sealed record WaterSupplyResult(
    IReadOnlyList<WaterSourceDispatch> Sources,
    IReadOnlyList<ReservoirDispatch> Reservoirs,
    IReadOnlyList<PumpDispatch> Pumps,
    IReadOnlyList<WaterLoadDispatch> Loads);

public interface IWaterSupplySolver
{
    WaterSupplyResult Solve(WaterSupplyRequest request);
}

public readonly record struct SewerFlowNode(SewerNodeId Id);
public readonly record struct SewerFlowPipe(SewerPipeId Id, SewerNodeId FromNodeId, SewerNodeId ToNodeId, double CapacityCubicMetersPerDay, bool IsInService);
public readonly record struct SewerFlowPump(PumpId Id, SewerNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct SewerFlowTreatment(SewageTreatmentPlantId Id, SewerNodeId NodeId, double AvailableCapacityCubicMetersPerDay);
public readonly record struct SewerFlowLoad(WaterSewerServicePointId Id, SewerNodeId NodeId, double GeneratedCubicMetersPerDay);
public readonly record struct SewerTreatmentDispatch(SewageTreatmentPlantId Id, double ProcessedCubicMetersPerDay);
public readonly record struct SewerLoadDispatch(WaterSewerServicePointId Id, double ProcessedCubicMetersPerDay);

public sealed record SewerFlowRequest(
    IReadOnlyList<SewerFlowNode> Nodes,
    IReadOnlyList<SewerFlowPipe> Pipes,
    IReadOnlyList<SewerFlowPump> Pumps,
    IReadOnlyList<SewerFlowTreatment> Treatments,
    IReadOnlyList<SewerFlowLoad> Loads);

public sealed record SewerFlowResult(
    IReadOnlyList<PumpDispatch> Pumps,
    IReadOnlyList<SewerTreatmentDispatch> Treatments,
    IReadOnlyList<SewerLoadDispatch> Loads);

public interface ISewerSolver
{
    SewerFlowResult Solve(SewerFlowRequest request);
}

public static class WaterSewerDefaults
{
    public const double FlowEpsilonCubicMetersPerDay = 1e-9;
    public const double WastewaterReturnRatio = 0.9d;
}
