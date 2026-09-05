using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class RoadNetworkMessageMapper
{
    public static RoadNetworkSnapshotMessage Create(RoadNetworkSnapshot snapshot, ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var nodes = new ProtocolRoadNode[snapshot.Nodes.Count];
        for (var i = 0; i < nodes.Length; i++) { var n = snapshot.Nodes[i]; nodes[i] = new ProtocolRoadNode(n.Id.Value, (ProtocolRoadNodeKind)n.Kind, n.Position.X, n.Position.Y, n.Position.Z); }
        var segments = new ProtocolRoadSegment[snapshot.Segments.Count];
        for (var i = 0; i < segments.Length; i++) { var s = snapshot.Segments[i]; segments[i] = new ProtocolRoadSegment(s.Id.Value, (ProtocolRoadKind)s.Kind, s.StartNodeId.Value, s.EndNodeId.Value); }
        var lanes = new ProtocolLane[snapshot.Lanes.Count];
        for (var i = 0; i < lanes.Length; i++) { var lane = snapshot.Lanes[i]; lanes[i] = new ProtocolLane(lane.Id.Value, lane.SegmentId.Value, (ProtocolLaneDirection)lane.Direction, lane.Order, lane.WidthMeters, lane.SpeedLimitMetersPerSecond); }
        var connections = new ProtocolLaneConnection[snapshot.Connections.Count];
        for (var i = 0; i < connections.Length; i++) { var x = snapshot.Connections[i]; connections[i] = new ProtocolLaneConnection(x.Id.Value, x.FromLaneId.Value, x.ToLaneId.Value, x.ViaNodeId.Value, (ProtocolTurnMovement)x.Movement); }
        var access = new ProtocolRoadAccessPoint[snapshot.AccessPoints.Count];
        for (var i = 0; i < access.Length; i++) { var a = snapshot.AccessPoints[i]; access[i] = new ProtocolRoadAccessPoint(a.Id.Value, a.SegmentId.Value, a.SegmentOffset, a.BuildingId?.Value ?? 0, a.PoiId?.Value ?? 0, (ProtocolRoadAccessMode)a.Mode); }
        return new RoadNetworkSnapshotMessage(tickCount, nodes, segments, lanes, connections, access);
    }
}
