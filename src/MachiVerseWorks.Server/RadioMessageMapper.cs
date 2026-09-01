using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class RadioMessageMapper
{
    private const int MaximumDebugEntries = 512;

    public static (RadioSnapshotMessage Radio, SpectrumSnapshotMessage Spectrum) Create(RadioSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var s = snapshot.Statistics;
        var statistics = new ProtocolRadioStatistics(
            checked((uint)s.SiteCount), checked((uint)s.BandCount), checked((uint)s.FrequencyBlockCount), checked((uint)s.LinkCount), checked((uint)s.ServiceAreaCount), checked((uint)s.ConflictCount),
            checked((uint)s.HealthyLinkCount), checked((uint)s.InterferedLinkCount), checked((uint)s.UnreachableLinkCount), s.PeakSpectrumUtilization, s.TickCount);
        var sites = snapshot.Sites.Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioSite(item.Id.Value, (ProtocolRadioSiteKind)item.Kind, item.Position.X, item.Position.Y, item.Position.Z, item.AntennaGainDb, item.AntennaHeightMeters, item.IsInService)).ToArray();
        var siteIds = sites.Select(static item => item.SiteId).ToHashSet();
        var antennas = (snapshot.Antennas ?? []).Where(item => siteIds.Contains(item.SiteId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioAntenna(
                item.Id.Value, item.SiteId.Value,
                item.PositionOffset.X, item.PositionOffset.Y, item.PositionOffset.Z,
                item.Orientation.X, item.Orientation.Y, item.Orientation.Z,
                item.GainDb, (ProtocolRadioAntennaPatternKind)item.PatternKind, item.BeamwidthDegrees, item.FrontToBackRatioDb, item.IsInService)).ToArray();
        var antennaIds = antennas.Select(static item => item.AntennaId).ToHashSet();
        var transmitters = (snapshot.Transmitters ?? []).Where(item => siteIds.Contains(item.SiteId.Value) && antennaIds.Contains(item.AntennaId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioTransmitter(item.Id.Value, item.SiteId.Value, item.AntennaId.Value, item.MaximumTransmitPowerDbm, item.IsInService, item.IsOperational)).ToArray();
        var transmitterIds = transmitters.Select(static item => item.TransmitterId).ToHashSet();
        var receivers = (snapshot.Receivers ?? []).Where(item => siteIds.Contains(item.SiteId.Value) && antennaIds.Contains(item.AntennaId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioReceiver(item.Id.Value, item.SiteId.Value, item.AntennaId.Value, item.MinimumFrequencyMegahertz, item.MaximumFrequencyMegahertz, item.SensitivityDbm, item.IsInService, item.IsOperational)).ToArray();
        var emissions = (snapshot.Emissions ?? []).Where(item => transmitterIds.Contains(item.TransmitterId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioEmission(item.Id.Value, item.TransmitterId.Value, item.ChannelId.Value, item.CenterFrequencyMegahertz, item.BandwidthMegahertz, item.TransmitPowerDbm, item.Utilization, item.IsInService, item.IsOperational)).ToArray();
        var links = snapshot.Links.Where(item => siteIds.Contains(item.FromSiteId.Value) && siteIds.Contains(item.ToSiteId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioLink(item.Id.Value, item.FromSiteId.Value, item.ToSiteId.Value, item.FrequencyBlockId.Value, item.DistanceMeters, item.PathLossDb, item.ReceivedPowerDbm, item.InterferenceDbm, item.SinrDb, item.Utilization, (ProtocolRadioLinkState)item.State, item.IsInService)).ToArray();
        var serviceAreas = snapshot.ServiceAreas.Where(item => siteIds.Contains(item.SiteId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolRadioServiceArea(item.SiteId.Value, item.FrequencyBlockId.Value, item.RadiusMeters, item.MinimumSinrDb)).ToArray();
        var bands = snapshot.Bands.Take(MaximumDebugEntries)
            .Select(static item => new ProtocolSpectrumBand(item.Id.Value, item.Name, item.MinimumFrequencyMegahertz, item.MaximumFrequencyMegahertz)).ToArray();
        var blocks = snapshot.FrequencyBlocks.Take(MaximumDebugEntries)
            .Select(static item => new ProtocolFrequencyBlock(item.Id.Value, item.BandId.Value, item.CenterFrequencyMegahertz, item.BandwidthMegahertz)).ToArray();
        var conflicts = snapshot.Conflicts.Take(MaximumDebugEntries)
            .Select(static item => new ProtocolSpectrumConflict(item.FirstBlockId.Value, item.SecondBlockId.Value, item.FirstSiteId.Value, item.SecondSiteId.Value, item.Reason)).ToArray();
        return (
            new RadioSnapshotMessage(
                statistics,
                Array.AsReadOnly(sites),
                Array.AsReadOnly(antennas),
                Array.AsReadOnly(transmitters),
                Array.AsReadOnly(receivers),
                Array.AsReadOnly(emissions),
                Array.AsReadOnly(links),
                Array.AsReadOnly(serviceAreas)),
            new SpectrumSnapshotMessage(s.TickCount, Array.AsReadOnly(bands), Array.AsReadOnly(blocks), Array.AsReadOnly(conflicts)));
    }
}
