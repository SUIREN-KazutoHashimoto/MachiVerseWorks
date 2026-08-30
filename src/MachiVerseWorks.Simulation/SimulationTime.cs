namespace MachiVerseWorks.Simulation;

public readonly record struct SimulationTime(ulong TickCount, TimeSpan Elapsed)
{
    internal SimulationTime Advance(int tickRate)
    {
        var nextTickCount = checked(TickCount + 1);
        return new SimulationTime(nextTickCount, TimeSpan.FromTicks(CalculateElapsedTicks(nextTickCount, tickRate)));
    }

    internal static long CalculateElapsedTicks(ulong tickCount, int tickRate)
    {
        if (tickRate is <= 0 or > SimulationConfig.MaximumTickRate)
        {
            throw new ArgumentOutOfRangeException(nameof(tickRate));
        }

        var elapsedTicks = (UInt128)tickCount * (ulong)TimeSpan.TicksPerSecond / (uint)tickRate;
        if (elapsedTicks > long.MaxValue)
        {
            throw new OverflowException("Simulation elapsed time exceeds the TimeSpan range.");
        }

        return (long)elapsedTicks;
    }
}
