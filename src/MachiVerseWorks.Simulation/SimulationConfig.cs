namespace MachiVerseWorks.Simulation;

public sealed class SimulationConfig
{
    public const int DefaultTickRate = 30;
    public const double DefaultSpatialCellSize = 64d;

    public SimulationConfig(
        int tickRate = DefaultTickRate,
        ulong seed = 1,
        double spatialCellSize = DefaultSpatialCellSize)
    {
        if (tickRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Tick rate must be greater than zero.");
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
    }

    public int TickRate { get; }

    public ulong Seed { get; }

    public double SpatialCellSize { get; }

    public double TickDurationSeconds => 1d / TickRate;

    public TimeSpan TickDuration => TimeSpan.FromSeconds(TickDurationSeconds);
}
