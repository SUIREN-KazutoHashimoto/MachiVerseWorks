namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private void SynchronizePersistentRegionalMaterializationUse(
        GeneratedBuildingId generatedBuildingId,
        GeneratedBuildingUse nextUse,
        int capacity)
    {
        if (!TryResolvePersistentRegionalMaterialization(generatedBuildingId, out var binding)) return;
        if (!TryGetBuildingSnapshot(binding.BuildingId, out var building)) return;

        var nextKind = MapBuildingKind(nextUse);
        if (!_buildings.Update(binding.BuildingId, nextKind, building.Bounds))
            throw new InvalidOperationException($"Materialized Building {binding.BuildingId.Value} disappeared during regional repurposing.");

        var desiredPoiKind = MapDevelopmentPoiKind(nextUse);
        var poiId = binding.PoiId;
        if (poiId is { } existingPoiId && TryGetPoiSnapshot(existingPoiId, out var poi))
        {
            _ = _pois.Update(existingPoiId, desiredPoiKind ?? PoiKind.Generic, poi.Position, binding.BuildingId);
        }
        else if (desiredPoiKind is { } newPoiKind)
        {
            poiId = CreatePoi(Center(building.Bounds), newPoiKind, binding.BuildingId);
        }

        var oldPoiIds = CreatePoiSnapshot()
            .Where(item => item.BuildingId == binding.BuildingId)
            .Select(static item => item.Id)
            .ToHashSet();
        RemovePersistentRegionalEconomicMaterialization(binding.BuildingId, oldPoiIds);

        (CompanyId CompanyId, EstablishmentId EstablishmentId, JobId JobId)? economy = null;
        if (nextUse != GeneratedBuildingUse.Residential)
            economy = MaterializeRegionalEconomicCapacity(binding.BuildingId, poiId, nextUse, Math.Max(1, capacity));

        _persistentRegionalMaterializations[generatedBuildingId] = binding with
        {
            PoiId = poiId,
            CompanyId = economy?.CompanyId,
            EstablishmentId = economy?.EstablishmentId,
            JobId = economy?.JobId,
        };
    }
}
