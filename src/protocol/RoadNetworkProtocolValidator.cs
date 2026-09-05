namespace MachiVerseWorks.Protocol;

internal static class RoadNetworkProtocolValidator
{
    public static void ValidateOrThrow(RoadNetworkSnapshotMessage message, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!IsValid(message))
        {
            throw new ArgumentException("Road Network snapshot contains duplicate IDs or dangling topology references.", parameterName);
        }
    }

    public static bool IsValid(RoadNetworkSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Segments);
        ArgumentNullException.ThrowIfNull(message.Lanes);
        ArgumentNullException.ThrowIfNull(message.Connections);
        ArgumentNullException.ThrowIfNull(message.AccessPoints);

        var nodeIds = new HashSet<ulong>(message.Nodes.Count);
        foreach (var node in message.Nodes)
        {
            if (!nodeIds.Add(node.Id)) return false;
        }

        var segmentIds = new HashSet<ulong>(message.Segments.Count);
        foreach (var segment in message.Segments)
        {
            if (!segmentIds.Add(segment.Id) || !nodeIds.Contains(segment.StartNodeId) || !nodeIds.Contains(segment.EndNodeId)) return false;
        }

        var laneIds = new HashSet<ulong>(message.Lanes.Count);
        foreach (var lane in message.Lanes)
        {
            if (!laneIds.Add(lane.Id) || !segmentIds.Contains(lane.SegmentId)) return false;
        }

        var connectionIds = new HashSet<ulong>(message.Connections.Count);
        foreach (var connection in message.Connections)
        {
            if (!connectionIds.Add(connection.Id)
                || !laneIds.Contains(connection.FromLaneId)
                || !laneIds.Contains(connection.ToLaneId)
                || !nodeIds.Contains(connection.ViaNodeId))
            {
                return false;
            }
        }

        var accessPointIds = new HashSet<ulong>(message.AccessPoints.Count);
        foreach (var accessPoint in message.AccessPoints)
        {
            if (!accessPointIds.Add(accessPoint.Id) || !segmentIds.Contains(accessPoint.SegmentId)) return false;
        }

        return true;
    }
}
