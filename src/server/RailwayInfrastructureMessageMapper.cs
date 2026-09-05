using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class RailwayInfrastructureMessageMapper
{
    public static RailwayInfrastructureSnapshotMessage Create(RailwayInfrastructureSnapshot snapshot, ulong revision, bool isFullSnapshot = true)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new RailwayInfrastructureSnapshotMessage(
            revision,
            isFullSnapshot,
            snapshot.Nodes.Select(static item => new ProtocolTrackNode(item.Id.Value, (byte)item.Kind, item.Position.X, item.Position.Y, item.Position.Z)).ToArray(),
            snapshot.Segments.Select(static item => new ProtocolTrackSegment(item.Id.Value, item.StartNodeId.Value, item.EndNodeId.Value, (ProtocolTrackDirection)item.Direction, item.GaugeMeters, item.SpeedLimitMetersPerSecond, (ProtocolTrackElectrification)item.Electrification, (ProtocolTrackUsage)item.Usage)).ToArray(),
            snapshot.Connections.Select(static item => new ProtocolTrackConnection(item.Id.Value, item.FromSegmentId.Value, item.ToSegmentId.Value, item.ViaNodeId.Value)).ToArray(),
            snapshot.Blocks.Select(static item => new ProtocolBlockSection(item.Id.Value, item.SegmentIds.Select(static id => id.Value).ToArray())).ToArray(),
            snapshot.Stations.Select(static item => new ProtocolStation(item.Id.Value, item.Bounds.MinX, item.Bounds.MinY, item.Bounds.MinZ, item.Bounds.MaxX, item.Bounds.MaxY, item.Bounds.MaxZ)).ToArray(),
            snapshot.Platforms.Select(static item => new ProtocolPlatform(item.Id.Value, item.StationId.Value, item.TrackSegmentId.Value, item.StartSegmentOffset, item.EndSegmentOffset, item.Bounds.MinX, item.Bounds.MinY, item.Bounds.MinZ, item.Bounds.MaxX, item.Bounds.MaxY, item.Bounds.MaxZ)).ToArray(),
            snapshot.PlatformAccessPoints.Select(static item => new ProtocolPlatformAccessPoint(item.Id.Value, item.PlatformId.Value, item.RoadAccessPointId.Value)).ToArray(),
            snapshot.Depots.Select(static item => new ProtocolDepot(item.Id.Value, item.Bounds.MinX, item.Bounds.MinY, item.Bounds.MinZ, item.Bounds.MaxX, item.Bounds.MaxY, item.Bounds.MaxZ, item.TrackSegmentIds.Select(static id => id.Value).ToArray())).ToArray());
    }
}
