namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private void StepWaterSewerTransactional(SimulationTime nextTime)
    {
        var sourceOutputs = new double[_waterSources.Count];
        for (var index = 0; index < sourceOutputs.Length; index++)
            sourceOutputs[index] = _waterSources[index].OutputCubicMetersPerDay;

        var reservoirOutputs = new double[_reservoirs.Count];
        for (var index = 0; index < reservoirOutputs.Length; index++)
            reservoirOutputs[index] = _reservoirs[index].OutputCubicMetersPerDay;

        var pumpThroughputs = new double[_utilityPumps.Count];
        for (var index = 0; index < pumpThroughputs.Length; index++)
            pumpThroughputs[index] = _utilityPumps[index].ThroughputCubicMetersPerDay;

        var treatmentOutputs = new double[_treatmentPlants.Count];
        for (var index = 0; index < treatmentOutputs.Length; index++)
            treatmentOutputs[index] = _treatmentPlants[index].ProcessedCubicMetersPerDay;

        var servicePoints = new WaterSewerServicePointRollback[_waterSewerServicePoints.Count];
        for (var index = 0; index < servicePoints.Length; index++)
        {
            var point = _waterSewerServicePoints[index];
            servicePoints[index] = new WaterSewerServicePointRollback(
                point.WaterDemandCubicMetersPerDay,
                point.WaterServedCubicMetersPerDay,
                point.WaterUnservedCubicMetersPerDay,
                point.WaterState,
                point.WastewaterGeneratedCubicMetersPerDay,
                point.WastewaterProcessedCubicMetersPerDay,
                point.WastewaterOverflowCubicMetersPerDay,
                point.SewerState);
        }

        try
        {
            StepWaterSewer(nextTime);
        }
        catch
        {
            for (var index = 0; index < sourceOutputs.Length; index++)
                _waterSources[index].OutputCubicMetersPerDay = sourceOutputs[index];
            for (var index = 0; index < reservoirOutputs.Length; index++)
                _reservoirs[index].OutputCubicMetersPerDay = reservoirOutputs[index];
            for (var index = 0; index < pumpThroughputs.Length; index++)
                _utilityPumps[index].ThroughputCubicMetersPerDay = pumpThroughputs[index];
            for (var index = 0; index < treatmentOutputs.Length; index++)
                _treatmentPlants[index].ProcessedCubicMetersPerDay = treatmentOutputs[index];

            for (var index = 0; index < servicePoints.Length; index++)
            {
                var point = _waterSewerServicePoints[index];
                var rollback = servicePoints[index];
                point.WaterDemandCubicMetersPerDay = rollback.WaterDemandCubicMetersPerDay;
                point.WaterServedCubicMetersPerDay = rollback.WaterServedCubicMetersPerDay;
                point.WaterUnservedCubicMetersPerDay = rollback.WaterUnservedCubicMetersPerDay;
                point.WaterState = rollback.WaterState;
                point.WastewaterGeneratedCubicMetersPerDay = rollback.WastewaterGeneratedCubicMetersPerDay;
                point.WastewaterProcessedCubicMetersPerDay = rollback.WastewaterProcessedCubicMetersPerDay;
                point.WastewaterOverflowCubicMetersPerDay = rollback.WastewaterOverflowCubicMetersPerDay;
                point.SewerState = rollback.SewerState;
            }
            throw;
        }
    }

    private readonly record struct WaterSewerServicePointRollback(
        double WaterDemandCubicMetersPerDay,
        double WaterServedCubicMetersPerDay,
        double WaterUnservedCubicMetersPerDay,
        WaterServiceState WaterState,
        double WastewaterGeneratedCubicMetersPerDay,
        double WastewaterProcessedCubicMetersPerDay,
        double WastewaterOverflowCubicMetersPerDay,
        SewerServiceState SewerState);
}
