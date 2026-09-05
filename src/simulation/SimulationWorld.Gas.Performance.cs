namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private void StepGasOptimized(SimulationTime nextTime)
    {
        if (_gasServicePoints.Count != 0)
        {
            StepGas(nextTime);
            return;
        }

        // With no Gas consumers there is no demand to solve. Keep producer outputs
        // consistent with an empty solve without allocating demand/index/request state.
        foreach (var source in _gasSources) source.OutputCubicMetersPerDay = 0d;
        foreach (var terminal in _gasImportTerminals) terminal.OutputCubicMetersPerDay = 0d;
        foreach (var storage in _gasStorages) storage.OutputCubicMetersPerDay = 0d;
    }
}
