namespace MachiVerseWorks.Simulation;

internal sealed class ValidatingWaterSupplySolver : IWaterSupplySolver
{
    private readonly IWaterSupplySolver _inner;

    public ValidatingWaterSupplySolver(IWaterSupplySolver inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public WaterSupplyResult Solve(WaterSupplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _inner.Solve(request) ?? throw new InvalidOperationException("Water supply solver returned no result.");

        var sourceBounds = request.Sources.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var reservoirBounds = request.Reservoirs.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var pumpBounds = request.Pumps.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var loadBounds = request.Loads.ToDictionary(static item => item.Id, static item => item.DemandCubicMetersPerDay);

        WaterSewerSolverValidation.ValidateDispatches(result.Sources, sourceBounds, static item => item.Id, static item => item.OutputCubicMetersPerDay, "Water supply", "source");
        WaterSewerSolverValidation.ValidateDispatches(result.Reservoirs, reservoirBounds, static item => item.Id, static item => item.OutputCubicMetersPerDay, "Water supply", "reservoir");
        WaterSewerSolverValidation.ValidateDispatches(result.Pumps, pumpBounds, static item => item.Id, static item => item.ThroughputCubicMetersPerDay, "Water supply", "pump");
        WaterSewerSolverValidation.ValidateDispatches(result.Loads, loadBounds, static item => item.Id, static item => item.ServedCubicMetersPerDay, "Water supply", "load");
        return result;
    }
}

internal sealed class ValidatingSewerSolver : ISewerSolver
{
    private readonly ISewerSolver _inner;

    public ValidatingSewerSolver(ISewerSolver inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public SewerFlowResult Solve(SewerFlowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _inner.Solve(request) ?? throw new InvalidOperationException("Sewer solver returned no result.");

        var pumpBounds = request.Pumps.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var treatmentBounds = request.Treatments.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var loadBounds = request.Loads.ToDictionary(static item => item.Id, static item => item.GeneratedCubicMetersPerDay);

        WaterSewerSolverValidation.ValidateDispatches(result.Pumps, pumpBounds, static item => item.Id, static item => item.ThroughputCubicMetersPerDay, "Sewer", "pump");
        WaterSewerSolverValidation.ValidateDispatches(result.Treatments, treatmentBounds, static item => item.Id, static item => item.ProcessedCubicMetersPerDay, "Sewer", "treatment");
        WaterSewerSolverValidation.ValidateDispatches(result.Loads, loadBounds, static item => item.Id, static item => item.ProcessedCubicMetersPerDay, "Sewer", "load");
        return result;
    }
}

internal static class WaterSewerSolverValidation
{
    public static void ValidateDispatches<TId, TDispatch>(
        IReadOnlyList<TDispatch>? dispatches,
        IReadOnlyDictionary<TId, double> bounds,
        Func<TDispatch, TId> idSelector,
        Func<TDispatch, double> valueSelector,
        string solverName,
        string dispatchName)
        where TId : notnull
    {
        if (dispatches is null)
            throw new InvalidOperationException($"{solverName} solver returned a null {dispatchName} dispatch collection.");

        var seen = new HashSet<TId>();
        foreach (var dispatch in dispatches)
        {
            var id = idSelector(dispatch);
            if (!seen.Add(id))
                throw new InvalidOperationException($"{solverName} solver returned duplicate {dispatchName} dispatch IDs.");
            if (!bounds.TryGetValue(id, out var maximum))
                throw new InvalidOperationException($"{solverName} solver returned an unknown {dispatchName} dispatch ID.");

            var value = valueSelector(dispatch);
            if (!double.IsFinite(value)
                || value < 0d
                || value > maximum + WaterSewerDefaults.FlowEpsilonCubicMetersPerDay)
            {
                throw new InvalidOperationException($"{solverName} solver returned an invalid {dispatchName} dispatch value.");
            }
        }
    }
}
