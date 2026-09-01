namespace MachiVerseWorks.Simulation;

public sealed class SimulationConfig
{
    public const int DefaultTickRate = 30;
    public const int MaximumTickRate = (int)TimeSpan.TicksPerSecond;
    public const double DefaultSpatialCellSize = 64d;

    public SimulationConfig(
        int tickRate = DefaultTickRate,
        ulong seed = 1,
        double spatialCellSize = DefaultSpatialCellSize,
        WorldEnvironmentConfig? worldEnvironment = null)
    {
        if (tickRate is <= 0 or > MaximumTickRate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tickRate),
                tickRate,
                $"Tick rate must be between 1 and {MaximumTickRate}.");
        }

        if (!double.IsFinite(spatialCellSize) || spatialCellSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(spatialCellSize),
                spatialCellSize,
                "Spatial cell size must be finite and greater than zero.");
        }

        TickRate = tickRate;
        Seed = seed;
        SpatialCellSize = spatialCellSize;
        WorldEnvironment = worldEnvironment ?? WorldEnvironmentConfig.CreateDefault(seed);
    }

    public int TickRate { get; }
    public ulong Seed { get; }
    public double SpatialCellSize { get; }
    public WorldEnvironmentConfig WorldEnvironment { get; }
    public double TickDurationSeconds => 1d / TickRate;
    public TimeSpan TickDuration => TimeSpan.FromTicks(SimulationTime.CalculateElapsedTicks(1, TickRate));
}
