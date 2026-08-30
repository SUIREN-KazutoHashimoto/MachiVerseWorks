using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class RailwayInfrastructureReadModel
{
    private readonly RailwayInfrastructureSnapshot _snapshot;
    private readonly Dictionary<TrackNodeId, TrackNodeSnapshot> _nodes;
    private readonly Dictionary<TrackSegmentId, TrackSegmentSnapshot> _segments;

    public RailwayInfrastructureReadModel(ulong revision, RailwayInfrastructureSnapshot snapshot)
    {
        Revision = revision;
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _nodes = snapshot.Nodes.ToDictionary(static item => item.Id);
        _segments = snapshot.Segments.ToDictionary(static item => item.Id);
    }

    public ulong Revision { get; }

    public RailwayInfrastructureSnapshot Query(WorldVolume volume)
    {
        var selectedNodes = _snapshot.Nodes.Where(item => volume.Contains(item.Position)).Select(static item => item.Id).ToHashSet();
        var selectedSegments = new HashSet<TrackSegmentId>();
        foreach (var segment in _snapshot.Segments)
        {
            if (!_nodes.TryGetValue(segment.StartNodeId, out var start) || !_nodes.TryGetValue(segment.EndNodeId, out var end))
                throw new InvalidOperationException($"Railway read model segment {segment.Id.Value} references a missing node.");
            if (!SegmentIntersectsVolume(start.Position, end.Position, volume)) continue;
            selectedSegments.Add(segment.Id);
            selectedNodes.Add(segment.StartNodeId);
            selectedNodes.Add(segment.EndNodeId);
        }

        var platforms = _snapshot.Platforms
            .Where(item => VolumesIntersect(item.Bounds, volume) || selectedSegments.Contains(item.TrackSegmentId))
            .ToArray();
        var platformIds = platforms.Select(static item => item.Id).ToHashSet();
        var stationIds = _snapshot.Stations.Where(item => VolumesIntersect(item.Bounds, volume)).Select(static item => item.Id).ToHashSet();
        foreach (var platform in platforms)
        {
            stationIds.Add(platform.StationId);
            selectedSegments.Add(platform.TrackSegmentId);
            if (_segments.TryGetValue(platform.TrackSegmentId, out var segment))
            {
                selectedNodes.Add(segment.StartNodeId);
                selectedNodes.Add(segment.EndNodeId);
            }
        }

        var depots = _snapshot.Depots.Where(item => VolumesIntersect(item.Bounds, volume) || item.TrackSegmentIds.Any(selectedSegments.Contains)).ToArray();
        var nodes = _snapshot.Nodes.Where(item => selectedNodes.Contains(item.Id)).ToArray();
        var segments = _snapshot.Segments.Where(item => selectedSegments.Contains(item.Id)).ToArray();
        var connections = _snapshot.Connections.Where(item => selectedSegments.Contains(item.FromSegmentId) && selectedSegments.Contains(item.ToSegmentId) && selectedNodes.Contains(item.ViaNodeId)).ToArray();
        var blocks = _snapshot.Blocks.Where(item => item.SegmentIds.Any(selectedSegments.Contains)).Select(item => new BlockSectionSnapshot(item.Id, item.SegmentIds.Where(selectedSegments.Contains).ToArray())).ToArray();
        var stations = _snapshot.Stations.Where(item => stationIds.Contains(item.Id)).ToArray();
        var access = _snapshot.PlatformAccessPoints.Where(item => platformIds.Contains(item.PlatformId)).ToArray();
        return new RailwayInfrastructureSnapshot(nodes, segments, connections, blocks, stations, platforms, access, depots);
    }

    private static bool SegmentIntersectsVolume(WorldPoint start, WorldPoint end, WorldVolume volume) =>
        Math.Max(start.X, end.X) >= volume.MinX && Math.Min(start.X, end.X) <= volume.MaxX
        && Math.Max(start.Y, end.Y) >= volume.MinY && Math.Min(start.Y, end.Y) <= volume.MaxY
        && Math.Max(start.Z, end.Z) >= volume.MinZ && Math.Min(start.Z, end.Z) <= volume.MaxZ;

    private static bool VolumesIntersect(WorldVolume first, WorldVolume second) =>
        first.MaxX >= second.MinX && first.MinX <= second.MaxX
        && first.MaxY >= second.MinY && first.MinY <= second.MaxY
        && first.MaxZ >= second.MinZ && first.MinZ <= second.MaxZ;
}
