using MachiVerseWorks.Simulation;

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
    public const int DefaultMaximumHouseholdCount = 1_000_000;
    public const int DefaultMaximumPersonCount = 1_000_000;
    public const int DefaultMaximumVehicleRouteStepCount = 100_000;
    public const int DefaultMaximumPersonScheduleEntryCount = 4_096;
    public const int DefaultMaximumBlockSectionSegmentCount = RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount;
    public const int DefaultMaximumDepotTrackSegmentCount = RailwayInfrastructureLimits.MaximumDepotTrackSegmentCount;
    public const int DefaultMaximumRailwayRouteSegmentCount = 100_000;
    public const int DefaultMaximumTimetableStopCount = 100_000;
    public const int DefaultMaximumTimetableStopTotalCount = 1_000_000;

    private static readonly int PersonNeedKindCount = Enum.GetValues<NeedKind>().Length;

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
        int maximumVehicleCount = DefaultMaximumVehicleCount,
        int maximumHouseholdCount = DefaultMaximumHouseholdCount,
        int maximumPersonCount = DefaultMaximumPersonCount,
        int maximumVehicleRouteStepCount = DefaultMaximumVehicleRouteStepCount,
        int maximumPersonScheduleEntryCount = DefaultMaximumPersonScheduleEntryCount,
        int maximumBlockSectionSegmentCount = DefaultMaximumBlockSectionSegmentCount,
        int maximumDepotTrackSegmentCount = DefaultMaximumDepotTrackSegmentCount,
        int maximumRailwayRouteSegmentCount = DefaultMaximumRailwayRouteSegmentCount,
        int maximumTimetableStopCount = DefaultMaximumTimetableStopCount,
        int maximumTimetableStopTotalCount = DefaultMaximumTimetableStopTotalCount)
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
        MaximumHouseholdCount = RequirePositive(maximumHouseholdCount, nameof(maximumHouseholdCount), "Maximum Household count");
        MaximumPersonCount = RequirePositive(maximumPersonCount, nameof(maximumPersonCount), "Maximum Person count");
        MaximumVehicleRouteStepCount = RequirePositive(maximumVehicleRouteStepCount, nameof(maximumVehicleRouteStepCount), "Maximum Vehicle route step count");
        MaximumPersonScheduleEntryCount = RequirePositive(maximumPersonScheduleEntryCount, nameof(maximumPersonScheduleEntryCount), "Maximum Person schedule entry count");
        MaximumBlockSectionSegmentCount = RequireAtMost(
            maximumBlockSectionSegmentCount,
            RailwayInfrastructureLimits.MaximumBlockSectionSegmentCount,
            nameof(maximumBlockSectionSegmentCount),
            "Maximum BlockSection segment count");
        MaximumDepotTrackSegmentCount = RequireAtMost(
            maximumDepotTrackSegmentCount,
            RailwayInfrastructureLimits.MaximumDepotTrackSegmentCount,
            nameof(maximumDepotTrackSegmentCount),
            "Maximum Depot track segment count");
        MaximumRailwayRouteSegmentCount = RequirePositive(maximumRailwayRouteSegmentCount, nameof(maximumRailwayRouteSegmentCount), "Maximum RailwayRoute segment count");
        MaximumTimetableStopCount = RequirePositive(maximumTimetableStopCount, nameof(maximumTimetableStopCount), "Maximum Timetable stop count");
        MaximumTimetableStopTotalCount = RequirePositive(maximumTimetableStopTotalCount, nameof(maximumTimetableStopTotalCount), "Maximum total Timetable stop count");
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
    public int MaximumHouseholdCount { get; }
    public int MaximumPersonCount { get; }
    public int MaximumVehicleRouteStepCount { get; }
    public int MaximumPersonScheduleEntryCount { get; }
    public int MaximumPersonNeedCount => PersonNeedKindCount;
    public int MaximumBlockSectionSegmentCount { get; }
    public int MaximumDepotTrackSegmentCount { get; }
    public int MaximumRailwayRouteSegmentCount { get; }
    public int MaximumTimetableStopCount { get; }
    public int MaximumTimetableStopTotalCount { get; }

    private static int RequirePositive(int value, string parameterName, string label)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(parameterName, value, $"{label} must be greater than zero.");
        return value;
    }

    private static int RequireAtMost(int value, int maximum, string parameterName, string label)
    {
        _ = RequirePositive(value, parameterName, label);
        if (value > maximum) throw new ArgumentOutOfRangeException(parameterName, value, $"{label} must not exceed the authoritative {maximum}-entry domain limit.");
        return value;
    }
}
