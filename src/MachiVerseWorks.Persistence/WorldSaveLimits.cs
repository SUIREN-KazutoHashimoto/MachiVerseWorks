namespace MachiVerseWorks.Persistence;

public sealed class WorldSaveLimits
{
    public const int DefaultMaximumBytes = 128 * 1024 * 1024;
    public const int DefaultMaximumAgentCount = 1_000_000;
    public const int DefaultMaximumBuildingCount = 1_000_000;
    public const int DefaultMaximumPoiCount = 1_000_000;
    public const int DefaultMaximumRoadNodeCount = 1_000_000;
    public const int DefaultMaximumRoadSegmentCount = 1_000_000;
    public const int DefaultMaximumLaneCount = 2_000_000;
    public const int DefaultMaximumLaneConnectionCount = 4_000_000;
    public const int DefaultMaximumRoadAccessPointCount = 1_000_000;
    public const int DefaultMaximumPedestrianCount = 1_000_000;
    public const int DefaultMaximumPedestrianCrossingCount = 4_000_000;
    public const int DefaultMaximumVehicleCount = 1_000_000;

    public WorldSaveLimits(
        int maximumBytes = DefaultMaximumBytes,
        int maximumAgentCount = DefaultMaximumAgentCount,
        int maximumBuildingCount = DefaultMaximumBuildingCount,
        int maximumPoiCount = DefaultMaximumPoiCount,
        int maximumRoadNodeCount = DefaultMaximumRoadNodeCount,
        int maximumRoadSegmentCount = DefaultMaximumRoadSegmentCount,
        int maximumLaneCount = DefaultMaximumLaneCount,
        int maximumLaneConnectionCount = DefaultMaximumLaneConnectionCount,
        int maximumRoadAccessPointCount = DefaultMaximumRoadAccessPointCount,
        int maximumPedestrianCount = DefaultMaximumPedestrianCount,
        int maximumPedestrianCrossingCount = DefaultMaximumPedestrianCrossingCount,
        int maximumVehicleCount = DefaultMaximumVehicleCount)
    {
        MaximumBytes = RequirePositive(maximumBytes, nameof(maximumBytes), "Maximum Save Data bytes");
        MaximumAgentCount = RequirePositive(maximumAgentCount, nameof(maximumAgentCount), "Maximum Agent count");
        MaximumBuildingCount = RequirePositive(maximumBuildingCount, nameof(maximumBuildingCount), "Maximum Building count");
        MaximumPoiCount = RequirePositive(maximumPoiCount, nameof(maximumPoiCount), "Maximum POI count");
        MaximumRoadNodeCount = RequirePositive(maximumRoadNodeCount, nameof(maximumRoadNodeCount), "Maximum RoadNode count");
        MaximumRoadSegmentCount = RequirePositive(maximumRoadSegmentCount, nameof(maximumRoadSegmentCount), "Maximum RoadSegment count");
        MaximumLaneCount = RequirePositive(maximumLaneCount, nameof(maximumLaneCount), "Maximum Lane count");
        MaximumLaneConnectionCount = RequirePositive(maximumLaneConnectionCount, nameof(maximumLaneConnectionCount), "Maximum LaneConnection count");
        MaximumRoadAccessPointCount = RequirePositive(maximumRoadAccessPointCount, nameof(maximumRoadAccessPointCount), "Maximum RoadAccessPoint count");
        MaximumPedestrianCount = RequirePositive(maximumPedestrianCount, nameof(maximumPedestrianCount), "Maximum Pedestrian count");
        MaximumPedestrianCrossingCount = RequirePositive(maximumPedestrianCrossingCount, nameof(maximumPedestrianCrossingCount), "Maximum PedestrianCrossing count");
        MaximumVehicleCount = RequirePositive(maximumVehicleCount, nameof(maximumVehicleCount), "Maximum Vehicle count");
    }

    public static WorldSaveLimits Default { get; } = new();
    public int MaximumBytes { get; }
    public int MaximumAgentCount { get; }
    public int MaximumBuildingCount { get; }
    public int MaximumPoiCount { get; }
    public int MaximumRoadNodeCount { get; }
    public int MaximumRoadSegmentCount { get; }
    public int MaximumLaneCount { get; }
    public int MaximumLaneConnectionCount { get; }
    public int MaximumRoadAccessPointCount { get; }
    public int MaximumPedestrianCount { get; }
    public int MaximumPedestrianCrossingCount { get; }
    public int MaximumVehicleCount { get; }

    private static int RequirePositive(int value, string parameterName, string label)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, value, $"{label} must be greater than zero.");
        return value;
    }
}
