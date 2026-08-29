namespace MachiVerseWorks.Simulation;

public readonly record struct SimulationTime(ulong TickCount, TimeSpan Elapsed)
{
    internal SimulationTime Advance(TimeSpan duration)
    {
        return new SimulationTime(checked(TickCount + 1), Elapsed + duration);
    }
}
