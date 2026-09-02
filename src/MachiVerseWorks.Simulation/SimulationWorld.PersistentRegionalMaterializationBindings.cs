namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<GeneratedBuildingId, PersistentRegionalMaterializationBinding> _persistentRegionalMaterializations = [];

    private PersistentRegionalMaterializationBinding[] CreatePersistentRegionalMaterializationCheckpoint() =>
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

    private bool TryResolvePersistentRegionalMaterialization(
        GeneratedBuildingId generatedBuildingId,
        out PersistentRegionalMaterializationBinding binding)
    {
        if (_persistentRegionalMaterializations.TryGetValue(generatedBuildingId, out binding!))
            return true;
        if (_regionalGeneration is null)
        {
            binding = null!;
            return false;
        }

        var generated = _regionalGeneration.Buildings.FirstOrDefault(item => item.Id == generatedBuildingId);
        if (generated is null)
        {
            binding = null!;
            return false;
        }

        var expectedBounds = NormalizeBuildingBoundsToTerrain(generated.Bounds);
        var expectedKind = MapBuildingKind(generated.Use);
        var actualBuilding = CreateBuildingSnapshot()
            .Where(item => item.Kind == expectedKind && item.Bounds == expectedBounds)
            .OrderBy(static item => item.Id.Value)
            .FirstOrDefault();
        if (actualBuilding.Id.Value == 0)
        {
            binding = null!;
            return false;
        }

        var poi = CreatePoiSnapshot()
            .Where(item => item.BuildingId == actualBuilding.Id)
            .OrderBy(static item => item.Id.Value)
            .FirstOrDefault();
        PoiId? poiId = poi.Id.Value == 0 ? null : poi.Id;

        var accessPoint = CreateRoadNetworkSnapshot().AccessPoints
            .Where(item => item.BuildingId == actualBuilding.Id)
            .OrderBy(static item => item.Id.Value)
            .FirstOrDefault();
        RoadAccessPointId? roadAccessPointId = accessPoint.Id.Value == 0 ? null : accessPoint.Id;

        var establishment = _economyEstablishments
            .Where(item => item.BuildingId == actualBuilding.Id
                || (poiId is { } linkedPoiId && item.PoiId == linkedPoiId))
            .OrderBy(static item => item.Id.Value)
            .FirstOrDefault();
        var jobId = establishment is null
            ? (JobId?)null
            : _economyJobs
                .Where(item => item.EstablishmentId == establishment.Id)
                .OrderBy(static item => item.Id.Value)
                .Select(static item => (JobId?)item.Id)
                .FirstOrDefault();

        binding = new PersistentRegionalMaterializationBinding(
            generatedBuildingId,
            actualBuilding.Id,
            poiId,
            roadAccessPointId,
            establishment?.CompanyId,
            establishment?.Id,
            jobId);
        _persistentRegionalMaterializations.Add(generatedBuildingId, binding);
        return true;
    }

    private void RemovePersistentRegionalMaterialization(GeneratedBuildingId generatedBuildingId)
    {
        if (!TryResolvePersistentRegionalMaterialization(generatedBuildingId, out var binding)) return;
        if (!TryGetBuildingSnapshot(binding.BuildingId, out var removedBuilding))
        {
            _persistentRegionalMaterializations.Remove(generatedBuildingId);
            return;
        }

        var removedCenter = Center(removedBuilding.Bounds);
        var replacementBuilding = CreateBuildingSnapshot()
            .Where(item => item.Id != binding.BuildingId)
            .OrderBy(item => Distance2D(Center(item.Bounds), removedCenter))
            .ThenBy(static item => item.Id.Value)
            .FirstOrDefault();
        if (replacementBuilding.Id.Value == 0)
            throw new InvalidOperationException("A materialized regional Building cannot be demolished without a replacement endpoint for existing Population references.");
        var replacement = TripEndpoint.ForBuilding(replacementBuilding.Id);

        var poiIds = CreatePoiSnapshot()
            .Where(item => item.BuildingId == binding.BuildingId)
            .OrderBy(static item => item.Id.Value)
            .Select(static item => item.Id)
            .ToArray();
        var poiIdSet = poiIds.ToHashSet();
        var roadAccessPointIds = CreateRoadNetworkSnapshot().AccessPoints
            .Where(item => item.BuildingId == binding.BuildingId
                || (item.PoiId is { } poiId && poiIdSet.Contains(poiId)))
            .OrderBy(static item => item.Id.Value)
            .Select(static item => item.Id)
            .ToArray();
        foreach (var accessPointId in roadAccessPointIds)
            _ = RemoveRoadAccessPoint(accessPointId);

        RemovePersistentRegionalEconomicMaterialization(binding.BuildingId, poiIdSet);

        foreach (var poiId in poiIds)
            _population.ReplacePoiReferences(poiId, replacement);
        _population.ReplaceBuildingReferences(binding.BuildingId, replacement);

        foreach (var poiId in poiIds)
            _ = RemovePoi(poiId);
        _ = RemoveBuilding(binding.BuildingId);
        _persistentRegionalMaterializations.Remove(generatedBuildingId);
    }

    private void RemovePersistentRegionalEconomicMaterialization(
        BuildingId buildingId,
        HashSet<PoiId> poiIds)
    {
        var establishments = _economyEstablishments
            .Where(item => item.BuildingId == buildingId
                || (item.PoiId is { } poiId && poiIds.Contains(poiId)))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        if (establishments.Length == 0) return;

        var establishmentIds = establishments.Select(static item => item.Id).ToHashSet();
        var jobs = _economyJobs
            .Where(item => establishmentIds.Contains(item.EstablishmentId))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        var jobIds = jobs.Select(static item => item.Id).ToHashSet();
        foreach (var personId in _economyEmployments
                     .Where(item => jobIds.Contains(item.Value.JobId))
                     .Select(static item => item.Key)
                     .ToArray())
        {
            _economyEmployments.Remove(personId);
        }

        foreach (var job in jobs)
        {
            _economyJobIndex.Remove(job.Id);
            _economyJobs.Remove(job);
        }

        var companyIds = establishments
            .Select(static item => item.CompanyId)
            .Distinct()
            .OrderBy(static item => item.Value)
            .ToArray();
        foreach (var establishment in establishments)
        {
            _economyEstablishmentIndex.Remove(establishment.Id);
            _economyEstablishments.Remove(establishment);
        }

        foreach (var companyId in companyIds)
        {
            if (_economyEstablishments.Any(item => item.CompanyId == companyId)) continue;
            if (_economyCompanyIndex.Remove(companyId, out var company))
                _economyCompanies.Remove(company);
        }
    }
}
