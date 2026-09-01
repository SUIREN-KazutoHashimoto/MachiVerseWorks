namespace MachiVerseWorks.Simulation;

internal sealed class ValidatingGasSupplySolver : IGasSupplySolver
{
    private readonly IGasSupplySolver _inner;

    public ValidatingGasSupplySolver(IGasSupplySolver inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public GasSupplyResult Solve(GasSupplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = _inner.Solve(request) ?? throw new InvalidOperationException("Gas supply solver returned no result.");

        var sourceBounds = request.Sources.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var importBounds = request.ImportTerminals.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var storageBounds = request.Storages.ToDictionary(static item => item.Id, static item => item.AvailableCapacityCubicMetersPerDay);
        var loadBounds = request.Loads.ToDictionary(static item => item.Id, static item => item.DemandCubicMetersPerDay);

        ValidateDispatches(result.Sources, sourceBounds, static item => item.Id, static item => item.OutputCubicMetersPerDay, "source");
        ValidateDispatches(result.ImportTerminals, importBounds, static item => item.Id, static item => item.OutputCubicMetersPerDay, "import terminal");
        ValidateDispatches(result.Storages, storageBounds, static item => item.Id, static item => item.OutputCubicMetersPerDay, "storage");
        ValidateDispatches(result.Loads, loadBounds, static item => item.Id, static item => item.ServedCubicMetersPerDay, "load");
        return result;
    }

    private static void ValidateDispatches<TId, TDispatch>(
        IReadOnlyList<TDispatch>? dispatches,
        IReadOnlyDictionary<TId, double> bounds,
        Func<TDispatch, TId> idSelector,
        Func<TDispatch, double> valueSelector,
        string name)
        where TId : notnull
    {
        if (dispatches is null) throw new InvalidOperationException($"Gas supply solver returned a null {name} dispatch collection.");
        var seen = new HashSet<TId>();
        foreach (var dispatch in dispatches)
        {
            var id = idSelector(dispatch);
            if (!seen.Add(id)) throw new InvalidOperationException($"Gas supply solver returned duplicate {name} dispatch IDs.");
            if (!bounds.TryGetValue(id, out var maximum)) throw new InvalidOperationException($"Gas supply solver returned an unknown {name} dispatch ID.");
            var value = valueSelector(dispatch);
            if (!double.IsFinite(value) || value < 0d || value > maximum + GasDefaults.FlowEpsilonCubicMetersPerDay)
                throw new InvalidOperationException($"Gas supply solver returned an invalid {name} dispatch value.");
        }
    }
}

public sealed partial class SimulationWorld
{
    private static void ValidateDeliveredGasCheckpointInvariants(SimulationCheckpoint checkpoint)
    {
        var gas = checkpoint.Economy?.Gas;
        if (gas is null) return;
        var inventories = checkpoint.Economy?.Logistics?.Inventories ?? Array.Empty<SimulationInventoryCheckpoint>();

        foreach (var servicePoint in gas.ServicePoints.Where(static item => item.DeliveryMode == GasDeliveryMode.Delivered))
        {
            if (servicePoint.EstablishmentId is not { } establishmentId || servicePoint.CommodityId is not { } commodityId)
                throw new ArgumentException("Delivered Gas service points must reference an Establishment and Gas commodity.", nameof(checkpoint));
            if (!inventories.Any(item => item.EstablishmentId == establishmentId && item.CommodityId == commodityId && item.Role == InventoryRole.Consumer))
                throw new ArgumentException("Delivered Gas service points require a consumer Logistics inventory for the referenced Establishment and Gas commodity.", nameof(checkpoint));
        }
    }
}
