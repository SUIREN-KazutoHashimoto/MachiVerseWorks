namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<GeneratedBuildingId, PersistentRegionalMaterializationBinding> _persistentRegionalMaterializations = [];

    private IReadOnlyList<PersistentRegionalMaterializationBinding> CreatePersistentRegionalMaterializationCheckpoint() =>
        _persistentRegionalMaterializations.Values
            .OrderBy(static item => item.GeneratedBuildingId.Value)
            .ToArray();

    private void RestorePersistentRegionalMaterializations(
        IReadOnlyList<PersistentRegionalMaterializationBinding>? bindings)
    {
        _persistentRegionalMaterializations.Clear();
        if (bindings is null) return;
        foreach (var binding in bindings.OrderBy(static item => item.GeneratedBuildingId.Value))
            _persistentRegionalMaterializations.Add(binding.GeneratedBuildingId, binding);
    }

    private void RemovePersistentRegionalMaterialization(GeneratedBuildingId generatedBuildingId)
    {
        if (!_persistentRegionalMaterializations.TryGetValue(generatedBuildingId, out var binding)) return;

        var replacementBuilding = CreateBuildingSnapshot()
            .Where(item => item.Id != binding.BuildingId)
            .OrderBy(item => Distance2D(Center(item.Bounds),
                TryGetBuildingSnapshot(binding.BuildingId, out var removed) ? Center(removed.Bounds) : Center(item.Bounds)))
            .ThenBy(static item => item.Id.Value)
            .FirstOrDefault();
        if (replacementBuilding.Id.Value == 0)
            throw new InvalidOperationException("A materialized regional Building cannot be demolished without a replacement endpoint for existing Population references.");
        var replacement = TripEndpoint.ForBuilding(replacementBuilding.Id);

        if (binding.RoadAccessPointId is { } accessPointId)
            _ = RemoveRoadAccessPoint(accessPointId);

        RemovePersistentRegionalEconomicMaterialization(binding);

        if (binding.PoiId is { } poiId)
            _population.ReplacePoiReferences(poiId, replacement);
        _population.ReplaceBuildingReferences(binding.BuildingId, replacement);

        if (binding.PoiId is { } linkedPoiId)
            _ = RemovePoi(linkedPoiId);
        _ = RemoveBuilding(binding.BuildingId);
        _persistentRegionalMaterializations.Remove(generatedBuildingId);
    }

    private void RemovePersistentRegionalEconomicMaterialization(PersistentRegionalMaterializationBinding binding)
    {
        if (binding.JobId is { } jobId)
        {
            foreach (var personId in _economyEmployments
                         .Where(item => item.Value.JobId == jobId)
                         .Select(static item => item.Key)
                         .ToArray())
            {
                _economyEmployments.Remove(personId);
            }
            if (_economyJobIndex.Remove(jobId, out var job)) _economyJobs.Remove(job);
        }

        if (binding.EstablishmentId is { } establishmentId
            && _economyEstablishmentIndex.Remove(establishmentId, out var establishment))
        {
            _economyEstablishments.Remove(establishment);
        }

        if (binding.CompanyId is { } companyId
            && !_economyEstablishments.Any(item => item.CompanyId == companyId)
            && _economyCompanyIndex.Remove(companyId, out var company))
        {
            _economyCompanies.Remove(company);
        }
    }
}
