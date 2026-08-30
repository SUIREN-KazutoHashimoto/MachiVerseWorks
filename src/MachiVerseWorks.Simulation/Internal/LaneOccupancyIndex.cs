namespace MachiVerseWorks.Simulation.Internal;

internal sealed class LaneOccupancyIndex
{
    private readonly Dictionary<LaneId, SortedSet<Entry>> entriesByLane = [];
    private readonly Dictionary<VehicleId, Location> locations = [];
    private static readonly EntryComparer Comparer = new();

    public int Count => locations.Count;

    public void Clear()
    {
        entriesByLane.Clear();
        locations.Clear();
    }

    public void Add(VehicleId id, LaneId laneId, double progressMeters, double lengthMeters, double speedMetersPerSecond)
    {
        Validate(progressMeters, lengthMeters, speedMetersPerSecond);
        if (locations.ContainsKey(id)) throw new InvalidOperationException($"Vehicle {id.Value} is already registered in Lane occupancy.");
        if (!entriesByLane.TryGetValue(laneId, out var set)) { set = new SortedSet<Entry>(Comparer); entriesByLane.Add(laneId, set); }
        var entry = new Entry(progressMeters, id, lengthMeters, speedMetersPerSecond);
        if (!set.Add(entry)) throw new InvalidOperationException($"Vehicle {id.Value} could not be registered in Lane occupancy.");
        locations.Add(id, new Location(laneId, entry));
    }

    public bool Remove(VehicleId id)
    {
        if (!locations.Remove(id, out var location)) return false;
        if (!entriesByLane.TryGetValue(location.LaneId, out var set) || !set.Remove(location.Entry))
            throw new InvalidOperationException($"Vehicle {id.Value} was missing from its Lane occupancy set.");
        if (set.Count == 0) entriesByLane.Remove(location.LaneId);
        return true;
    }

    public bool TryGetLeader(LaneId laneId, double progressMeters, out OccupancyNeighbor leader)
    {
        if (!entriesByLane.TryGetValue(laneId, out var set) || set.Count == 0) { leader = default; return false; }
        var view = set.GetViewBetween(
            new Entry(progressMeters, new VehicleId(0), 0d, 0d),
            new Entry(double.PositiveInfinity, new VehicleId(ulong.MaxValue), double.MaxValue, double.MaxValue));
        foreach (var entry in view)
        {
            leader = new OccupancyNeighbor(entry.Id, entry.ProgressMeters, entry.LengthMeters, entry.SpeedMetersPerSecond);
            return true;
        }
        leader = default;
        return false;
    }

    public bool CanOccupy(LaneId laneId, double progressMeters, double lengthMeters, double minimumGapMeters)
    {
        Validate(progressMeters, lengthMeters, 0d);
        if (!double.IsFinite(minimumGapMeters) || minimumGapMeters < 0d) throw new ArgumentOutOfRangeException(nameof(minimumGapMeters));
        if (!entriesByLane.TryGetValue(laneId, out var set) || set.Count == 0) return true;

        var ahead = set.GetViewBetween(
            new Entry(progressMeters, new VehicleId(0), 0d, 0d),
            new Entry(double.PositiveInfinity, new VehicleId(ulong.MaxValue), double.MaxValue, double.MaxValue));
        foreach (var entry in ahead)
        {
            var gap = entry.ProgressMeters - progressMeters - (entry.LengthMeters + lengthMeters) * 0.5d;
            if (gap < minimumGapMeters) return false;
            break;
        }

        var behind = set.GetViewBetween(
            new Entry(double.NegativeInfinity, new VehicleId(0), 0d, 0d),
            new Entry(progressMeters, new VehicleId(ulong.MaxValue), double.MaxValue, double.MaxValue));
        if (behind.Count > 0)
        {
            var entry = behind.Max;
            var gap = progressMeters - entry.ProgressMeters - (entry.LengthMeters + lengthMeters) * 0.5d;
            if (gap < minimumGapMeters) return false;
        }
        return true;
    }

    private static void Validate(double progressMeters, double lengthMeters, double speedMetersPerSecond)
    {
        if (!double.IsFinite(progressMeters) || progressMeters < 0d) throw new ArgumentOutOfRangeException(nameof(progressMeters));
        if (!double.IsFinite(lengthMeters) || lengthMeters <= 0d) throw new ArgumentOutOfRangeException(nameof(lengthMeters));
        if (!double.IsFinite(speedMetersPerSecond) || speedMetersPerSecond < 0d) throw new ArgumentOutOfRangeException(nameof(speedMetersPerSecond));
    }

    private readonly record struct Entry(double ProgressMeters, VehicleId Id, double LengthMeters, double SpeedMetersPerSecond);
    private readonly record struct Location(LaneId LaneId, Entry Entry);

    private sealed class EntryComparer : IComparer<Entry>
    {
        public int Compare(Entry left, Entry right)
        {
            var progress = left.ProgressMeters.CompareTo(right.ProgressMeters);
            return progress != 0 ? progress : left.Id.Value.CompareTo(right.Id.Value);
        }
    }
}

internal readonly record struct OccupancyNeighbor(VehicleId Id, double ProgressMeters, double LengthMeters, double SpeedMetersPerSecond);
