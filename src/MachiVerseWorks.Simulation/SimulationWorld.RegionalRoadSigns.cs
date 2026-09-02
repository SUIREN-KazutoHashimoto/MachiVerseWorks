namespace MachiVerseWorks.Simulation;

public sealed record RegionalRoadSignPlacement(
    RoadSignId Id,
    RoadSignKind Kind,
    WorldPoint Position,
    RoadSegmentId RoadSegmentId,
    LaneId? LaneId,
    GeographicFeatureId? FeatureId,
    SettlementId? DestinationSettlementId,
    HumanToponymId? DestinationNameId,
    string Text);

public sealed partial class SimulationWorld
{
    public IReadOnlyList<RegionalRoadSignPlacement> CreateRegionalRoadSignPlacements()
    {
        if (_regionalGeneration is null)
            throw new InvalidOperationException("Regional generation has not been initialized for this world.");
        if (RoadSegmentCount == 0)
            return [];

        var roads = CreateRoadNetworkSnapshot();
        var nodeById = roads.Nodes.ToDictionary(static node => node.Id);
        var lanesBySegment = roads.Lanes
            .GroupBy(static lane => lane.SegmentId)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static lane => lane.Direction).ThenBy(static lane => lane.Order).ThenBy(static lane => lane.Id.Value).ToArray());
        var settlementById = _regionalGeneration.Settlements.ToDictionary(static settlement => settlement.Id);
        var result = new List<RegionalRoadSignPlacement>(_regionalGeneration.RoadSigns.Count);

        foreach (var sign in _regionalGeneration.RoadSigns.OrderBy(static sign => sign.Id.Value))
        {
            var segment = roads.Segments
                .OrderBy(segment => DistanceToRoadSegment(sign.Position, segment, nodeById))
                .ThenBy(static segment => segment.Id.Value)
                .First();
            LaneId? laneId = null;
            if (lanesBySegment.TryGetValue(segment.Id, out var lanes) && lanes.Length > 0)
                laneId = lanes[0].Id;

            HumanToponymId? destinationNameId = null;
            if (sign.DestinationSettlementId is { } destinationId
                && settlementById.TryGetValue(destinationId, out var destination))
            {
                destinationNameId = destination.NameId;
            }

            result.Add(new RegionalRoadSignPlacement(
                sign.Id,
                sign.Kind,
                sign.Position,
                segment.Id,
                laneId,
                sign.FeatureId,
                sign.DestinationSettlementId,
                destinationNameId,
                sign.Text));
        }
        return result;
    }

    private static double DistanceToRoadSegment(
        WorldPoint point,
        RoadSegmentSnapshot segment,
        IReadOnlyDictionary<RoadNodeId, RoadNodeSnapshot> nodeById)
    {
        var start = nodeById[segment.StartNodeId].Position;
        var end = nodeById[segment.EndNodeId].Position;
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 1e-12)
            return RoadSignDistance2D(point, start);
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0d, 1d);
        return RoadSignDistance2D(point, new WorldPoint(start.X + dx * t, start.Y + dy * t, point.Z));
    }

    private static double RoadSignDistance2D(WorldPoint first, WorldPoint second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
