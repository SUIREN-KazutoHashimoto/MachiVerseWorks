namespace MachiVerseWorks.Simulation;

internal sealed class SettlementSpatialIndex
{
    private sealed record Node(SettlementEvolutionState Settlement, int Axis, Node? Left, Node? Right);

    private readonly Node? _root;

    public SettlementSpatialIndex(IReadOnlyList<SettlementEvolutionState> settlements)
    {
        ArgumentNullException.ThrowIfNull(settlements);
        _root = Build(settlements.ToArray(), depth: 0);
    }

    public SettlementEvolutionState? FindNearest(WorldPoint point)
    {
        SettlementEvolutionState? best = null;
        var bestDistanceSquared = double.PositiveInfinity;
        Search(_root, point, ref best, ref bestDistanceSquared);
        return best;
    }

    private static Node? Build(SettlementEvolutionState[] items, int depth)
    {
        if (items.Length == 0) return null;
        var axis = depth & 1;
        Array.Sort(items, (left, right) =>
        {
            var primary = axis == 0
                ? left.Center.X.CompareTo(right.Center.X)
                : left.Center.Y.CompareTo(right.Center.Y);
            return primary != 0 ? primary : left.SettlementId.Value.CompareTo(right.SettlementId.Value);
        });
        var middle = items.Length / 2;
        return new Node(
            items[middle],
            axis,
            Build(items[..middle], depth + 1),
            Build(items[(middle + 1)..], depth + 1));
    }

    private static void Search(
        Node? node,
        WorldPoint point,
        ref SettlementEvolutionState? best,
        ref double bestDistanceSquared)
    {
        if (node is null) return;
        var dx = point.X - node.Settlement.Center.X;
        var dy = point.Y - node.Settlement.Center.Y;
        var distanceSquared = dx * dx + dy * dy;
        if (distanceSquared < bestDistanceSquared
            || (distanceSquared == bestDistanceSquared
                && (best is null || node.Settlement.SettlementId.Value < best.SettlementId.Value)))
        {
            best = node.Settlement;
            bestDistanceSquared = distanceSquared;
        }

        var delta = node.Axis == 0 ? dx : dy;
        var near = delta <= 0d ? node.Left : node.Right;
        var far = delta <= 0d ? node.Right : node.Left;
        Search(near, point, ref best, ref bestDistanceSquared);
        if (delta * delta <= bestDistanceSquared)
            Search(far, point, ref best, ref bestDistanceSquared);
    }
}
