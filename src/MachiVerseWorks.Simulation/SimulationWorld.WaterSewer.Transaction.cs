namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private void StepWaterSewerTransactional(SimulationTime nextTime)
    {
        var checkpoint = CreateWaterSewerCheckpoint();
        try
        {
            StepWaterSewer(nextTime);
        }
        catch
        {
            RestoreWaterSewer(checkpoint);
            throw;
        }
    }
}
