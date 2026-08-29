namespace MachiVerseWorks.Persistence;

public sealed class WorldSaveLimits
{
    public const int DefaultMaximumBytes = 128 * 1024 * 1024;
    public const int DefaultMaximumAgentCount = 1_000_000;

    public WorldSaveLimits(
        int maximumBytes = DefaultMaximumBytes,
        int maximumAgentCount = DefaultMaximumAgentCount)
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

        MaximumBytes = maximumBytes;
        MaximumAgentCount = maximumAgentCount;
    }

    public static WorldSaveLimits Default { get; } = new();

    public int MaximumBytes { get; }

    public int MaximumAgentCount { get; }
}
