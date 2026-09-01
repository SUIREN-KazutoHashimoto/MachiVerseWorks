namespace MachiVerseWorks.Simulation;

public readonly record struct RadioSiteId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct SpectrumBandId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct FrequencyBlockId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RadioLinkId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RadioPeerId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct EffectiveRadiatedPower(double Dbm);
public readonly record struct TransmitterPathBudget(EffectiveRadiatedPower EffectiveRadiatedPower, double FeederLossDb, double MiscellaneousLossDb);
public readonly record struct RadioLinkBudget(TransmitterPathBudget Transmitter, double ReceiveAntennaGainDb, double ReceiverSensitivityDbm, double FadeMarginDb);

public enum RadioSiteKind : byte
{
    Macro = 0,
    Micro = 1,
    SmallCell = 2,
    PointToPoint = 3,
    Gateway = 4,
}

public enum RadioLinkState : byte
{
    Healthy = 0,
    Marginal = 1,
    Interfered = 2,
    Unreachable = 3,
    OutOfService = 4,
}

public enum RadioPeerAmbiguityPolicy : byte
{
    Reject = 0,
    LowestSiteId = 1,
}

public readonly record struct SpectrumBand(
    SpectrumBandId Id,
    string Name,
    double MinimumFrequencyMegahertz,
    double MaximumFrequencyMegahertz);

public readonly record struct FrequencyBlock(
    FrequencyBlockId Id,
    SpectrumBandId BandId,
    double CenterFrequencyMegahertz,
    double BandwidthMegahertz);

public readonly record struct RadioSiteSnapshot(
    RadioSiteId Id,
    RadioSiteKind Kind,
    WorldPoint Position,
    double AntennaGainDb,
    double AntennaHeightMeters,
    bool IsInService);

public readonly record struct RadioLinkSnapshot(
    RadioLinkId Id,
    RadioSiteId FromSiteId,
    RadioSiteId ToSiteId,
    FrequencyBlockId FrequencyBlockId,
    double DistanceMeters,
    double PathLossDb,
    double ReceivedPowerDbm,
    double InterferenceDbm,
    double SinrDb,
    double Utilization,
    RadioLinkState State,
    bool IsInService);

public readonly record struct RadioServiceArea(
    RadioSiteId SiteId,
    FrequencyBlockId FrequencyBlockId,
    double RadiusMeters,
    double MinimumSinrDb);

public readonly record struct RadioPeer(
    RadioPeerId Id,
    IReadOnlyList<RadioSiteId> SourceSiteIds,
    IReadOnlyList<RadioSiteId> DestinationSiteIds,
    RadioPeerAmbiguityPolicy AmbiguityPolicy);

public readonly record struct SpectrumConflict(
    FrequencyBlockId FirstBlockId,
    FrequencyBlockId SecondBlockId,
    RadioSiteId FirstSiteId,
    RadioSiteId SecondSiteId,
    string Reason);

public readonly record struct RadioPropagationRequest(
    RadioSiteSnapshot Transmitter,
    RadioSiteSnapshot Receiver,
    FrequencyBlock FrequencyBlock,
    RadioLinkBudget LinkBudget,
    double InterferenceDbm,
    double NoiseFloorDbm,
    double ObstructionLossDb = 0d,
    bool IsLineOfSight = true);

public readonly record struct RadioPropagationResult(
    double DistanceMeters,
    double PathLossDb,
    double ReceivedPowerDbm,
    double InterferenceDbm,
    double SinrDb,
    bool IsReachable);

public interface IRadioPropagationSolver
{
    RadioPropagationResult Solve(RadioPropagationRequest request);
}

public interface IRadioPathCorrection
{
    double CalculateAdditionalLossDb(RadioPropagationRequest request, double distanceMeters);
}

public sealed class NoRadioPathCorrection : IRadioPathCorrection
{
    public double CalculateAdditionalLossDb(RadioPropagationRequest request, double distanceMeters) => 0d;
}

public readonly record struct RadioStatistics(
    int SiteCount,
    int BandCount,
    int FrequencyBlockCount,
    int LinkCount,
    int ServiceAreaCount,
    int ConflictCount,
    int HealthyLinkCount,
    int InterferedLinkCount,
    int UnreachableLinkCount,
    double PeakSpectrumUtilization,
    ulong TickCount);

public sealed record RadioSnapshot(
    RadioStatistics Statistics,
    IReadOnlyList<RadioSiteSnapshot> Sites,
    IReadOnlyList<SpectrumBand> Bands,
    IReadOnlyList<FrequencyBlock> FrequencyBlocks,
    IReadOnlyList<RadioLinkSnapshot> Links,
    IReadOnlyList<RadioServiceArea> ServiceAreas,
    IReadOnlyList<SpectrumConflict> Conflicts,
    IReadOnlyList<RadioAntennaSnapshot>? Antennas = null,
    IReadOnlyList<RadioTransmitterSnapshot>? Transmitters = null,
    IReadOnlyList<RadioReceiverSnapshot>? Receivers = null,
    IReadOnlyList<RadioEmissionSnapshot>? Emissions = null);

public sealed record RadioCheckpoint(
    ulong NextSiteId,
    ulong NextBandId,
    ulong NextFrequencyBlockId,
    ulong NextLinkId,
    ulong NextPeerId,
    IReadOnlyList<SimulationRadioSiteCheckpoint> Sites,
    IReadOnlyList<SpectrumBand> Bands,
    IReadOnlyList<FrequencyBlock> FrequencyBlocks,
    IReadOnlyList<SimulationRadioLinkCheckpoint> Links,
    IReadOnlyList<RadioPeer> Peers,
    ulong NextAntennaId = 1,
    ulong NextTransmitterId = 1,
    ulong NextReceiverId = 1,
    ulong NextEmissionId = 1,
    IReadOnlyList<SimulationRadioAntennaCheckpoint>? Antennas = null,
    IReadOnlyList<SimulationRadioTransmitterCheckpoint>? Transmitters = null,
    IReadOnlyList<SimulationRadioReceiverCheckpoint>? Receivers = null,
    IReadOnlyList<SimulationRadioEmissionCheckpoint>? Emissions = null,
    IReadOnlyList<RadioSiteInfrastructureBinding>? SiteInfrastructure = null,
    IReadOnlyList<RadioLinkEntityBinding>? LinkEntityBindings = null);

public readonly record struct SimulationRadioSiteCheckpoint(
    RadioSiteId Id,
    RadioSiteKind Kind,
    WorldPoint Position,
    double AntennaGainDb,
    double AntennaHeightMeters,
    bool IsInService);

public readonly record struct SimulationRadioLinkCheckpoint(
    RadioLinkId Id,
    RadioSiteId FromSiteId,
    RadioSiteId ToSiteId,
    FrequencyBlockId FrequencyBlockId,
    RadioLinkBudget LinkBudget,
    double Utilization,
    bool IsInService);

public static class RadioDefaults
{
    public const double MinimumSinrDb = 3d;
    public const double MarginalSinrDb = 8d;
    public const double ThermalNoiseFloorDbm = -104d;
    public const double SpectrumConflictDistanceMeters = 2_000d;
    public const double UtilizationEpsilon = 1e-9;
}

internal sealed class RadioSiteState(
    RadioSiteId id,
    RadioSiteKind kind,
    WorldPoint position,
    double antennaGainDb,
    double antennaHeightMeters,
    bool isInService)
{
    public RadioSiteId Id { get; } = id;
    public RadioSiteKind Kind { get; } = kind;
    public WorldPoint Position { get; } = position;
    public double AntennaGainDb { get; } = antennaGainDb;
    public double AntennaHeightMeters { get; } = antennaHeightMeters;
    public bool IsInService { get; set; } = isInService;
}

internal sealed class RadioLinkStateData(
    RadioLinkId id,
    RadioSiteId fromSiteId,
    RadioSiteId toSiteId,
    FrequencyBlockId frequencyBlockId,
    RadioLinkBudget linkBudget,
    double utilization,
    bool isInService)
{
    public RadioLinkId Id { get; } = id;
    public RadioSiteId FromSiteId { get; } = fromSiteId;
    public RadioSiteId ToSiteId { get; } = toSiteId;
    public FrequencyBlockId FrequencyBlockId { get; } = frequencyBlockId;
    public RadioLinkBudget LinkBudget { get; } = linkBudget;
    public double Utilization { get; set; } = utilization;
    public bool IsInService { get; set; } = isInService;
    public RadioPropagationResult Propagation { get; set; }
    public RadioLinkState State { get; set; } = RadioLinkState.Unreachable;
}
