namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public bool TryGetRegionalGenerationSourceTick(out ulong sourceTick)
    {
        if (_regionalGeneration is null)
        {
            sourceTick = 0;
            return false;
        }

        sourceTick = _regionalGeneration.TickCount;
        return true;
    }
}
