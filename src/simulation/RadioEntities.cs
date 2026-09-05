namespace MachiVerseWorks.Simulation;

public readonly record struct RadioChannelId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RadioAntennaId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RadioTransmitterId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RadioReceiverId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct RadioEmissionId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum RadioAntennaPatternKind : byte
{
    Omnidirectional = 0,
    Directional = 1,
}

public readonly record struct RadioChannel(
    RadioChannelId Id,
    SpectrumBandId BandId,
    double CenterFrequencyMegahertz,
    double BandwidthMegahertz)
{
    public FrequencyBlock ToFrequencyBlock() =>
        new(new FrequencyBlockId(Id.Value), BandId, CenterFrequencyMegahertz, BandwidthMegahertz);
}

public readonly record struct RadioAntennaSnapshot(
    RadioAntennaId Id,
    RadioSiteId SiteId,
    WorldVector PositionOffset,
    WorldVector Orientation,
    double GainDb,
    RadioAntennaPatternKind PatternKind,
    double BeamwidthDegrees,
    double FrontToBackRatioDb,
    bool IsInService);

public readonly record struct RadioTransmitterSnapshot(
    RadioTransmitterId Id,
    RadioSiteId SiteId,
    RadioAntennaId AntennaId,
    double MaximumTransmitPowerDbm,
    bool IsInService,
    bool IsOperational);

public readonly record struct RadioReceiverSnapshot(
    RadioReceiverId Id,
    RadioSiteId SiteId,
    RadioAntennaId AntennaId,
    double MinimumFrequencyMegahertz,
    double MaximumFrequencyMegahertz,
    double SensitivityDbm,
    bool IsInService,
    bool IsOperational);

public readonly record struct RadioEmissionSnapshot(
    RadioEmissionId Id,
    RadioTransmitterId TransmitterId,
    RadioChannelId ChannelId,
    double CenterFrequencyMegahertz,
    double BandwidthMegahertz,
    double TransmitPowerDbm,
    double Utilization,
    bool IsInService,
    bool IsOperational);

public readonly record struct RadioSiteInfrastructureBinding(
    RadioSiteId SiteId,
    BuildingId? BuildingId,
    OpticalBackhaulId? OpticalBackhaulId,
    bool RequiresPower);

public readonly record struct RadioLinkEntityBinding(
    RadioLinkId LinkId,
    RadioEmissionId EmissionId,
    RadioReceiverId ReceiverId);

public readonly record struct SimulationRadioAntennaCheckpoint(
    RadioAntennaId Id,
    RadioSiteId SiteId,
    WorldVector PositionOffset,
    WorldVector Orientation,
    double GainDb,
    RadioAntennaPatternKind PatternKind,
    double BeamwidthDegrees,
    double FrontToBackRatioDb,
    bool IsInService);

public readonly record struct SimulationRadioTransmitterCheckpoint(
    RadioTransmitterId Id,
    RadioSiteId SiteId,
    RadioAntennaId AntennaId,
    double MaximumTransmitPowerDbm,
    bool IsInService);

public readonly record struct SimulationRadioReceiverCheckpoint(
    RadioReceiverId Id,
    RadioSiteId SiteId,
    RadioAntennaId AntennaId,
    double MinimumFrequencyMegahertz,
    double MaximumFrequencyMegahertz,
    double SensitivityDbm,
    bool IsInService);

public readonly record struct SimulationRadioEmissionCheckpoint(
    RadioEmissionId Id,
    RadioTransmitterId TransmitterId,
    RadioChannelId ChannelId,
    double TransmitPowerDbm,
    double Utilization,
    bool IsInService);

internal sealed class RadioAntennaState(
    RadioAntennaId id,
    RadioSiteId siteId,
    WorldVector positionOffset,
    WorldVector orientation,
    double gainDb,
    RadioAntennaPatternKind patternKind,
    double beamwidthDegrees,
    double frontToBackRatioDb,
    bool isInService)
{
    public RadioAntennaId Id { get; } = id;
    public RadioSiteId SiteId { get; } = siteId;
    public WorldVector PositionOffset { get; } = positionOffset;
    public WorldVector Orientation { get; } = orientation;
    public double GainDb { get; } = gainDb;
    public RadioAntennaPatternKind PatternKind { get; } = patternKind;
    public double BeamwidthDegrees { get; } = beamwidthDegrees;
    public double FrontToBackRatioDb { get; } = frontToBackRatioDb;
    public bool IsInService { get; set; } = isInService;
}

internal sealed class RadioTransmitterState(
    RadioTransmitterId id,
    RadioSiteId siteId,
    RadioAntennaId antennaId,
    double maximumTransmitPowerDbm,
    bool isInService)
{
    public RadioTransmitterId Id { get; } = id;
    public RadioSiteId SiteId { get; } = siteId;
    public RadioAntennaId AntennaId { get; } = antennaId;
    public double MaximumTransmitPowerDbm { get; } = maximumTransmitPowerDbm;
    public bool IsInService { get; set; } = isInService;
}

internal sealed class RadioReceiverState(
    RadioReceiverId id,
    RadioSiteId siteId,
    RadioAntennaId antennaId,
    double minimumFrequencyMegahertz,
    double maximumFrequencyMegahertz,
    double sensitivityDbm,
    bool isInService)
{
    public RadioReceiverId Id { get; } = id;
    public RadioSiteId SiteId { get; } = siteId;
    public RadioAntennaId AntennaId { get; } = antennaId;
    public double MinimumFrequencyMegahertz { get; } = minimumFrequencyMegahertz;
    public double MaximumFrequencyMegahertz { get; } = maximumFrequencyMegahertz;
    public double SensitivityDbm { get; } = sensitivityDbm;
    public bool IsInService { get; set; } = isInService;
}

internal sealed class RadioEmissionState(
    RadioEmissionId id,
    RadioTransmitterId transmitterId,
    RadioChannelId channelId,
    double transmitPowerDbm,
    double utilization,
    bool isInService)
{
    public RadioEmissionId Id { get; } = id;
    public RadioTransmitterId TransmitterId { get; } = transmitterId;
    public RadioChannelId ChannelId { get; } = channelId;
    public double TransmitPowerDbm { get; } = transmitPowerDbm;
    public double Utilization { get; set; } = utilization;
    public bool IsInService { get; set; } = isInService;
}
