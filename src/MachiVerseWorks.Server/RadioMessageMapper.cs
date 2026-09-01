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
            new RadioSnapshotMessage(statistics, Array.AsReadOnly(sites), Array.AsReadOnly(links), Array.AsReadOnly(serviceAreas)),
            new SpectrumSnapshotMessage(s.TickCount, Array.AsReadOnly(bands), Array.AsReadOnly(blocks), Array.AsReadOnly(conflicts)));
    }
}
