namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public PersistentRegionalEvolutionSnapshot? CreatePersistentRegionalEvolutionInspectionSnapshot(
        SettlementId? settlementId = null,
        ParcelId? parcelId = null,
        GeneratedBuildingId? buildingId = null)
    {
        EnsurePersistentRegionalEvolution();
        var source = _persistentRegionalEvolution!;
        if ((settlementId is null ? 0 : 1) + (parcelId is null ? 0 : 1) + (buildingId is null ? 0 : 1) != 1)
            throw new ArgumentException("Exactly one persistent regional entity must be selected for inspection.");

        SettlementEvolutionState[] settlements = [];
        ParcelEvolutionState[] parcels = [];
        BuildingLifecycleState[] buildings = [];
        ServiceCatchment[] catchments = [];
        InfrastructureDemandSignal[] demands = [];
        RegionalRelation[] relations = [];
        RegionalEvolutionEvent[] events = [];

        if (settlementId is { } selectedSettlementId)
        {
            var settlement = source.Settlements.FirstOrDefault(item => item.SettlementId == selectedSettlementId);
            if (settlement is null) return null;
            settlements = [settlement];
            catchments = source.ServiceCatchments.Where(item => item.SettlementId == selectedSettlementId).ToArray();
            demands = source.InfrastructureDemands.Where(item => item.SettlementId == selectedSettlementId).ToArray();
            relations = source.Relations
                .Where(item => item.FromSettlementId == selectedSettlementId || item.ToSettlementId == selectedSettlementId)
                .OrderByDescending(static item => item.Strength)
                .ThenBy(static item => item.Id.Value)
                .Take(32)
                .ToArray();
            events = source.Events
                .Where(item => item.SettlementId == selectedSettlementId)
                .OrderByDescending(static item => item.Year)
                .ThenByDescending(static item => item.Id.Value)
                .Take(16)
                .OrderBy(static item => item.Id.Value)
                .ToArray();
        }
        else if (parcelId is { } selectedParcelId)
        {
            var parcel = source.Parcels.FirstOrDefault(item => item.ParcelId == selectedParcelId);
            if (parcel is null) return null;
            parcels = [parcel];
            if (parcel.BuildingId is { } parcelBuildingId)
            {
                events = source.Events
                    .Where(item => item.BuildingId == parcelBuildingId)
                    .OrderByDescending(static item => item.Year)
                    .ThenByDescending(static item => item.Id.Value)
                    .Take(16)
                    .OrderBy(static item => item.Id.Value)
                    .ToArray();
            }
        }
        else if (buildingId is { } selectedBuildingId)
        {
            var building = source.Buildings.FirstOrDefault(item => item.BuildingId == selectedBuildingId);
            if (building is null) return null;
            buildings = [building];
            var parcel = source.Parcels.FirstOrDefault(item => item.ParcelId == building.ParcelId);
            if (parcel is not null) parcels = [parcel];
            events = source.Events
                .Where(item => item.BuildingId == selectedBuildingId)
                .OrderByDescending(static item => item.Year)
                .ThenByDescending(static item => item.Id.Value)
                .Take(16)
                .OrderBy(static item => item.Id.Value)
                .ToArray();
        }

        return new PersistentRegionalEvolutionSnapshot(
            source.CurrentYear,
            _persistentRegionalEvolutionTickCount,
            settlements,
            parcels,
            buildings,
            catchments,
            demands,
            relations,
            events);
    }
}
