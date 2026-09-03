namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private PersistentRegionalEvolutionSnapshot ApplyPersistentRegionalRedevelopment(
        PersistentRegionalEvolutionSnapshot source)
    {
        if (source.Buildings.Count == 0) return source;
        var parcels = source.Parcels.ToArray();
        var parcelIndex = parcels
            .Select((item, index) => (item.ParcelId, index))
            .ToDictionary(static item => item.ParcelId, static item => item.index);
        var buildings = source.Buildings.ToArray();
        var events = source.Events.ToList();
        var nextEventId = events.Count == 0 ? 1UL : checked(events.Max(static item => item.Id.Value) + 1UL);
        var settlements = source.Settlements.ToDictionary(static item => item.SettlementId);

        for (var buildingIndex = 0; buildingIndex < buildings.Length; buildingIndex++)
        {
            var building = buildings[buildingIndex];
            if (!parcelIndex.TryGetValue(building.ParcelId, out var index)) continue;
            var parcel = parcels[index];

            if (building.Status == BuildingLifecycleStatus.Demolished)
            {
                RemovePersistentRegionalMaterialization(building.BuildingId);
                if (parcel.BuildingId == building.BuildingId)
                {
                    parcels[index] = parcel with
                    {
                        BuildingId = null,
                        DevelopmentState = ParcelDevelopmentState.Vacant,
                    };
                }
                continue;
            }

            var age = source.CurrentYear - building.BuiltYear;
            if (age < 25 || building.Status is BuildingLifecycleStatus.Abandoned or BuildingLifecycleStatus.Renovating or BuildingLifecycleStatus.Repurposing)
                continue;
            if (parcel.DevelopmentDemand < 0.72d || building.Occupancy >= 0.55d || !settlements.TryGetValue(parcel.SettlementId, out var settlement))
                continue;

            var nextUse = SelectRedevelopmentUse(building.Use, settlement);
            if (nextUse == building.Use) continue;
            SynchronizePersistentRegionalMaterializationUse(building.BuildingId, nextUse, building.Capacity);
            buildings[buildingIndex] = building with
            {
                Use = nextUse,
                Status = BuildingLifecycleStatus.Repurposing,
                Condition = Math.Min(1d, building.Condition + 0.2d),
                Occupancy = Math.Max(0.25d, building.Occupancy),
                LastChangedYear = source.CurrentYear,
            };
            parcels[index] = parcel with { DevelopmentState = ParcelDevelopmentState.Redeveloping };
            events.Add(new RegionalEvolutionEvent(
                new RegionalEvolutionEventId(nextEventId++),
                source.CurrentYear,
                RegionalEvolutionEventKind.BuildingUseChanged,
                parcel.SettlementId,
                building.BuildingId,
                FormattableString.Invariant($"{building.Use}->{nextUse}; demand {parcel.DevelopmentDemand:F3}")));
        }

        return source with
        {
            Parcels = parcels,
            Buildings = buildings,
            Events = PersistentRegionalEvolutionRetention.RetainNewest(events),
        };
    }

    private static GeneratedBuildingUse SelectRedevelopmentUse(
        GeneratedBuildingUse current,
        SettlementEvolutionState settlement)
    {
        var jobsPerResident = settlement.Jobs / Math.Max(1d, settlement.Population);
        if (jobsPerResident < 0.28d)
            return current == GeneratedBuildingUse.Residential ? GeneratedBuildingUse.MixedUse : GeneratedBuildingUse.Residential;
        if (jobsPerResident > 0.75d)
            return current == GeneratedBuildingUse.Commercial ? GeneratedBuildingUse.MixedUse : GeneratedBuildingUse.Commercial;
        return current == GeneratedBuildingUse.MixedUse ? GeneratedBuildingUse.Commercial : GeneratedBuildingUse.MixedUse;
    }
}
