namespace MachiVerseWorks.Protocol;

internal static class RailwayInfrastructureProtocolValidator
{
    public static void ValidateIdentity(RailwayInfrastructureSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Segments);
        ArgumentNullException.ThrowIfNull(message.Connections);
        ArgumentNullException.ThrowIfNull(message.Blocks);
        ArgumentNullException.ThrowIfNull(message.Stations);
        ArgumentNullException.ThrowIfNull(message.Platforms);
        ArgumentNullException.ThrowIfNull(message.PlatformAccessPoints);
        ArgumentNullException.ThrowIfNull(message.Depots);

        ValidateUniqueIds(message.Nodes.Select(static item => item.Id), "TrackNode");
        ValidateUniqueIds(message.Segments.Select(static item => item.Id), "TrackSegment");
        ValidateUniqueIds(message.Connections.Select(static item => item.Id), "TrackConnection");
        ValidateUniqueIds(message.Blocks.Select(static item => item.Id), "BlockSection");
        ValidateUniqueIds(message.Stations.Select(static item => item.Id), "Station");
        ValidateUniqueIds(message.Platforms.Select(static item => item.Id), "Platform");
        ValidateUniqueIds(message.PlatformAccessPoints.Select(static item => item.Id), "PlatformAccessPoint");
        ValidateUniqueIds(message.Depots.Select(static item => item.Id), "Depot");
        foreach (var block in message.Blocks)
        {
            ArgumentNullException.ThrowIfNull(block);
            ArgumentNullException.ThrowIfNull(block.SegmentIds);
            ValidateUniqueIds(block.SegmentIds, $"BlockSection {block.Id} segment");
        }
        foreach (var depot in message.Depots)
        {
            ArgumentNullException.ThrowIfNull(depot);
            ArgumentNullException.ThrowIfNull(depot.TrackSegmentIds);
            ValidateUniqueIds(depot.TrackSegmentIds, $"Depot {depot.Id} track segment");
        }
    }

    public static void ValidateAggregate(RailwayInfrastructureSnapshotMessage message)
    {
        ValidateIdentity(message);
        var nodeIds = message.Nodes.Select(static item => item.Id).ToHashSet();
        var segments = message.Segments.ToDictionary(static item => item.Id);
        var stationIds = message.Stations.Select(static item => item.Id).ToHashSet();
        var platformIds = message.Platforms.Select(static item => item.Id).ToHashSet();

        foreach (var segment in message.Segments)
        {
            if (!nodeIds.Contains(segment.StartNodeId) || !nodeIds.Contains(segment.EndNodeId))
                throw new ArgumentOutOfRangeException(nameof(message), $"TrackSegment {segment.Id} references a missing TrackNode.");
        }
        foreach (var connection in message.Connections)
        {
            if (!segments.TryGetValue(connection.FromSegmentId, out var from)
                || !segments.TryGetValue(connection.ToSegmentId, out var to)
                || !nodeIds.Contains(connection.ViaNodeId)
                || !IsIncident(from, connection.ViaNodeId)
                || !IsIncident(to, connection.ViaNodeId))
                throw new ArgumentOutOfRangeException(nameof(message), $"TrackConnection {connection.Id} contains dangling or non-incident topology.");
        }
        foreach (var block in message.Blocks)
            if (block.SegmentIds.Any(id => !segments.ContainsKey(id)))
                throw new ArgumentOutOfRangeException(nameof(message), $"BlockSection {block.Id} references a missing TrackSegment.");
        foreach (var platform in message.Platforms)
            if (!stationIds.Contains(platform.StationId) || !segments.ContainsKey(platform.TrackSegmentId))
                throw new ArgumentOutOfRangeException(nameof(message), $"Platform {platform.Id} references a missing Station or TrackSegment.");
        foreach (var accessPoint in message.PlatformAccessPoints)
            if (!platformIds.Contains(accessPoint.PlatformId))
                throw new ArgumentOutOfRangeException(nameof(message), $"PlatformAccessPoint {accessPoint.Id} references a missing Platform.");
        foreach (var depot in message.Depots)
            if (depot.TrackSegmentIds.Any(id => !segments.ContainsKey(id)))
                throw new ArgumentOutOfRangeException(nameof(message), $"Depot {depot.Id} references a missing TrackSegment.");
    }

    private static void ValidateUniqueIds(IEnumerable<ulong> ids, string label)
    {
        var seen = new HashSet<ulong>();
        foreach (var id in ids)
            if (id == 0 || !seen.Add(id))
                throw new ArgumentOutOfRangeException(nameof(ids), $"{label} IDs must be unique and greater than zero.");
    }

    private static bool IsIncident(ProtocolTrackSegment segment, ulong nodeId) => segment.StartNodeId == nodeId || segment.EndNodeId == nodeId;
}
