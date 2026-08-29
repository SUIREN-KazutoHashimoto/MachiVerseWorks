namespace MachiVerseWorks.Persistence;

public sealed class WorldSaveLimits
{
    public const int DefaultMaximumBytes = 128 * 1024 * 1024;
    public const int DefaultMaximumAgentCount = 1_000_000;
    public const int DefaultMaximumBuildingCount = 1_000_000;
    public const int DefaultMaximumPoiCount = 1_000_000;

    public WorldSaveLimits(
        int maximumBytes = DefaultMaximumBytes,
        int maximumAgentCount = DefaultMaximumAgentCount,
        int maximumBuildingCount = DefaultMaximumBuildingCount,
        int maximumPoiCount = DefaultMaximumPoiCount)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                maximumBytes,
                "Maximum Save Data bytes must be greater than zero.");
        }

        if (maximumAgentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumAgentCount),
                maximumAgentCount,
                "Maximum Agent count must be greater than zero.");
        }

        if (maximumBuildingCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBuildingCount),
                maximumBuildingCount,
                "Maximum Building count must be greater than zero.");
        }

        if (maximumPoiCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumPoiCount),
                maximumPoiCount,
                "Maximum POI count must be greater than zero.");
        }

        MaximumBytes = maximumBytes;
        MaximumAgentCount = maximumAgentCount;
        MaximumBuildingCount = maximumBuildingCount;
        MaximumPoiCount = maximumPoiCount;
    }

    public static WorldSaveLimits Default { get; } = new();

    public int MaximumBytes { get; }

    public int MaximumAgentCount { get; }

    public int MaximumBuildingCount { get; }

    public int MaximumPoiCount { get; }
}
