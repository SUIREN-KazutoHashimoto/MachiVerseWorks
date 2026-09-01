namespace MachiVerseWorks.Protocol;

public enum ProtocolRadioSiteKind : byte { Macro = 0, Micro = 1, SmallCell = 2, PointToPoint = 3, Gateway = 4 }
public enum ProtocolRadioLinkState : byte { Healthy = 0, Marginal = 1, Interfered = 2, Unreachable = 3, OutOfService = 4 }
public enum ProtocolRadioAntennaPatternKind : byte { Omnidirectional = 0, Directional = 1 }

public readonly record struct ProtocolRadioStatistics(uint SiteCount, uint BandCount, uint FrequencyBlockCount, uint LinkCount, uint ServiceAreaCount, uint ConflictCount, uint HealthyLinkCount, uint InterferedLinkCount, uint UnreachableLinkCount, double PeakSpectrumUtilization, ulong TickCount);
public readonly record struct ProtocolRadioSite(ulong SiteId, ProtocolRadioSiteKind Kind, double X, double Y, double Z, double AntennaGainDb, double AntennaHeightMeters, bool IsInService);
public readonly record struct ProtocolRadioAntenna(ulong AntennaId, ulong SiteId, double OffsetX, double OffsetY, double OffsetZ, double OrientationX, double OrientationY, double OrientationZ, double GainDb, ProtocolRadioAntennaPatternKind PatternKind, double BeamwidthDegrees, double FrontToBackRatioDb, bool IsInService);
public readonly record struct ProtocolRadioTransmitter(ulong TransmitterId, ulong SiteId, ulong AntennaId, double MaximumTransmitPowerDbm, bool IsInService, bool IsOperational);
public readonly record struct ProtocolRadioReceiver(ulong ReceiverId, ulong SiteId, ulong AntennaId, double MinimumFrequencyMegahertz, double MaximumFrequencyMegahertz, double SensitivityDbm, bool IsInService, bool IsOperational);
public readonly record struct ProtocolRadioEmission(ulong EmissionId, ulong TransmitterId, ulong ChannelId, double CenterFrequencyMegahertz, double BandwidthMegahertz, double TransmitPowerDbm, double Utilization, bool IsInService, bool IsOperational);
public readonly record struct ProtocolRadioLink(ulong LinkId, ulong FromSiteId, ulong ToSiteId, ulong FrequencyBlockId, double DistanceMeters, double PathLossDb, double ReceivedPowerDbm, double InterferenceDbm, double SinrDb, double Utilization, ProtocolRadioLinkState State, bool IsInService);
public readonly record struct ProtocolRadioServiceArea(ulong SiteId, ulong FrequencyBlockId, double RadiusMeters, double MinimumSinrDb);

public sealed record RadioSnapshotMessage(
    ProtocolRadioStatistics Statistics,
    IReadOnlyList<ProtocolRadioSite> Sites,
    IReadOnlyList<ProtocolRadioAntenna> Antennas,
    IReadOnlyList<ProtocolRadioTransmitter> Transmitters,
    IReadOnlyList<ProtocolRadioReceiver> Receivers,
    IReadOnlyList<ProtocolRadioEmission> Emissions,
    IReadOnlyList<ProtocolRadioLink> Links,
    IReadOnlyList<ProtocolRadioServiceArea> ServiceAreas) : IProtocolMessage
{
    public MessageType Type => MessageType.RadioSnapshot;
}

public readonly record struct ProtocolSpectrumBand(ulong BandId, string Name, double MinimumFrequencyMegahertz, double MaximumFrequencyMegahertz);
public readonly record struct ProtocolFrequencyBlock(ulong FrequencyBlockId, ulong BandId, double CenterFrequencyMegahertz, double BandwidthMegahertz);
public readonly record struct ProtocolSpectrumConflict(ulong FirstBlockId, ulong SecondBlockId, ulong FirstSiteId, ulong SecondSiteId, string Reason);

public sealed record SpectrumSnapshotMessage(ulong TickCount, IReadOnlyList<ProtocolSpectrumBand> Bands, IReadOnlyList<ProtocolFrequencyBlock> FrequencyBlocks, IReadOnlyList<ProtocolSpectrumConflict> Conflicts) : IProtocolMessage
{
    public MessageType Type => MessageType.SpectrumSnapshot;
}
