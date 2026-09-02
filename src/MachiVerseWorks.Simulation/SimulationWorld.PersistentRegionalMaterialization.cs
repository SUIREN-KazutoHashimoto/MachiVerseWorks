namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private PersistentRegionalEvolutionSnapshot ApplyPersistentRegionalWorldChanges(
        PersistentRegionalEvolutionSnapshot previous,
        PersistentRegionalEvolutionSnapshot current)
    {
        current = MaterializeRegionalDevelopment(current);
        current = DetectEmergentSettlements(current);
        current = RecordRegionalRelationChanges(previous, current);
        ApplyRegionalHouseholdMobility(current);
        return current;
    }

    private PersistentRegionalEvolutionSnapshot MaterializeRegionalDevelopment(PersistentRegionalEvolutionSnapshot source)
    {
        if (_regionalGeneration is null || BuildingCount == 0) return source;

        var sourceParcels = _regionalGeneration.Parcels.ToDictionary(static item => item.Id);
        var parcels = source.Parcels.ToArray();
        var buildings = source.Buildings.ToList();
        var events = source.Events.ToList();
        var nextBuildingId = buildings.Count == 0 ? 1UL : checked(buildings.Max(static item => item.BuildingId.Value) + 1UL);
        var nextEventId = events.Count == 0 ? 1UL : checked(events.Max(static item => item.Id.Value) + 1UL);
        var created = 0;

        for (var index = 0; index < parcels.Length && created < 4; index++)
        {
            var parcel = parcels[index];
            if (parcel.BuildingId is not null || parcel.DevelopmentDemand < 0.62d) continue;
            if (!sourceParcels.TryGetValue(parcel.ParcelId, out var generatedParcel)) continue;

            var use = MapDevelopmentUse(generatedParcel.Zone);
            var bounds = NormalizeBuildingBoundsToTerrain(generatedParcel.Bounds);
            var actualBuilding = CreateBuilding(bounds, MapBuildingKind(use));
            PoiId? actualPoi = null;
            var poiKind = MapDevelopmentPoiKind(use);
            if (poiKind is { } kind)
                actualPoi = CreatePoi(Center(bounds), kind, actualBuilding);

            var road = CreateRoadNetworkSnapshot();
            if (road.Segments.Count > 0)
            {
                var segmentIds = road.Segments.Select(static item => item.Id).ToArray();
                var segment = SelectNearestRoadSegment(segmentIds, bounds);
                CreateRoadAccessPoint(segment, 0.5d, actualBuilding, actualPoi, RoadAccessMode.Motor | RoadAccessMode.Foot);
            }

            var capacity = CalculateDevelopmentCapacity(generatedParcel.Zone, bounds);
            if (use != GeneratedBuildingUse.Residential)
                MaterializeRegionalEconomicCapacity(actualBuilding, actualPoi, use, capacity);

            var generatedBuildingId = new GeneratedBuildingId(nextBuildingId++);
            parcels[index] = parcel with
            {
                BuildingId = generatedBuildingId,
                DevelopmentState = ParcelDevelopmentState.Occupied,
            };
            buildings.Add(new BuildingLifecycleState(
                generatedBuildingId,
                parcel.ParcelId,
                use,
                source.CurrentYear,
                source.CurrentYear,
                1d,
                0.35d,
                capacity,
                BuildingLifecycleStatus.Active));
            events.Add(new RegionalEvolutionEvent(
                new RegionalEvolutionEventId(nextEventId++),
                source.CurrentYear,
                RegionalEvolutionEventKind.BuildingConstructed,
                parcel.SettlementId,
                generatedBuildingId,
                $"parcel {parcel.ParcelId.Value} demand {parcel.DevelopmentDemand:F3}"));

            if (use is GeneratedBuildingUse.Residential or GeneratedBuildingUse.MixedUse)
                MaterializeRegionalPopulationGrowth(parcel.SettlementId, actualBuilding, source);
            created++;
        }

        return source with
        {
            Parcels = parcels,
            Buildings = buildings.OrderBy(static item => item.BuildingId.Value).ToArray(),
            Events = events.OrderBy(static item => item.Id.Value).ToArray(),
        };
    }

    private void MaterializeRegionalEconomicCapacity(
        BuildingId buildingId,
        PoiId? poiId,
        GeneratedBuildingUse use,
        int capacity)
    {
        var sector = use switch
        {
            GeneratedBuildingUse.Commercial => IndustrySector.Retail,
            GeneratedBuildingUse.Industrial => IndustrySector.Manufacturing,
            GeneratedBuildingUse.Civic => IndustrySector.Public,
            GeneratedBuildingUse.Transport => IndustrySector.Transport,
            _ => IndustrySector.Services,
        };
        var company = CreateCompany(
            sector,
            initialCashBalance: checked((long)Math.Max(10_000, capacity * 1_000)),
            dailyProductionCapacity: Math.Max(1d, capacity * 0.5d));
        var establishment = CreateEstablishment(company, buildingId, poiId);
        CreateJob(establishment, Math.Max(1, capacity / 4), dailyWage: 120);
    }

    private void MaterializeRegionalPopulationGrowth(
        SettlementId settlementId,
        BuildingId buildingId,
        PersistentRegionalEvolutionSnapshot source)
    {
        var settlement = source.Settlements.FirstOrDefault(item => item.SettlementId == settlementId);
        if (settlement is null || settlement.Trend is not (SettlementTrend.Growing or SettlementTrend.Recovering)) return;

        var household = CreateHousehold(TripEndpoint.ForBuilding(buildingId));
        SetHouseholdCashBalance(household, 5_000);
        var householdSize = Math.Clamp(settlement.Population / 2_000 + 1, 1, 3);
        for (var member = 0; member < householdSize; member++)
        {
            var demographics = new PersonDemographics(
                AgeYears: 20 + ((source.CurrentYear + member * 11) % 43),
                IsEmployed: false,
                IsStudent: false,
                HasPrivateVehicle: (source.CurrentYear + member) % 3 == 0);
            CreatePerson(household, demographics, Array.Empty<DailyActivityWindow>());
        }
    }

    private void ApplyRegionalHouseholdMobility(PersistentRegionalEvolutionSnapshot source)
    {
        if (HouseholdCount == 0 || source.Settlements.Count < 2) return;
        var buildings = CreateBuildingSnapshot();
        var residential = buildings
            .Where(static item => item.Kind is BuildingKind.Residential or BuildingKind.MixedUse)
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        if (residential.Length == 0) return;

        var households = _population.CreateHouseholdCheckpoint().OrderBy(static item => item.Id.Value).ToArray();
        var moved = 0;
        foreach (var household in households)
        {
            if (moved >= 4 || !TryResolveRegionalEndpointPosition(household.Residence, out var position)) break;
            var origin = FindNearestSettlement(source.Settlements, position);
            if (origin is null || origin.Trend is not (SettlementTrend.Declining or SettlementTrend.Dormant)) continue;

            var originScore = RegionalAttractiveness(origin);
            var target = source.Settlements
                .Where(item => item.IsActive && item.SettlementId != origin.SettlementId)
                .OrderByDescending(RegionalAttractiveness)
                .ThenBy(static item => item.SettlementId.Value)
                .FirstOrDefault();
            if (target is null || RegionalAttractiveness(target) < originScore + 0.12d) continue;

            var destination = residential
                .OrderBy(item => Distance2D(Center(item.Bounds), target.Center))
                .ThenBy(static item => item.Id.Value)
                .First();
            var endpoint = TripEndpoint.ForBuilding(destination.Id);
            if (endpoint == household.Residence) continue;
            if (RelocateHousehold(household.Id, endpoint)) moved++;
        }
    }

    private PersistentRegionalEvolutionSnapshot DetectEmergentSettlements(PersistentRegionalEvolutionSnapshot source)
    {
        if (BuildingCount == 0) return source;
        var settlements = source.Settlements.ToList();
        var events = source.Events.ToList();
        var nextSettlementId = settlements.Count == 0 ? 1UL : checked(settlements.Max(static item => item.SettlementId.Value) + 1UL);
        var nextEventId = events.Count == 0 ? 1UL : checked(events.Max(static item => item.Id.Value) + 1UL);
        var roadAccess = CreateRoadNetworkSnapshot().AccessPoints;

        foreach (var building in CreateBuildingSnapshot().OrderBy(static item => item.Id.Value))
        {
            var center = Center(building.Bounds);
            var strongestInfluence = 0d;
            foreach (var settlement in settlements)
            {
                var distance = Distance2D(center, settlement.Center);
                var influence = Math.Clamp(1d - distance / Math.Max(1d, settlement.InfluenceRadiusMeters), 0d, 1d);
                strongestInfluence = Math.Max(strongestInfluence, influence);
            }
            if (strongestInfluence > 0.65d) continue;

            var population = 0;
            for (var personIndex = 0; personIndex < _population.PersonCount; personIndex++)
                if (_population.GetPersonAt(personIndex).Residence.BuildingId == building.Id) population++;

            var jobs = 0;
            for (var establishmentIndex = 0; establishmentIndex < _economyEstablishments.Count; establishmentIndex++)
            {
                var establishment = _economyEstablishments[establishmentIndex];
                if (establishment.BuildingId != building.Id) continue;
                for (var jobIndex = 0; jobIndex < _economyJobs.Count; jobIndex++)
                    if (_economyJobs[jobIndex].EstablishmentId == establishment.Id)
                        jobs = checked(jobs + _economyJobs[jobIndex].RequiredWorkerCount);
            }

            var connectivity = roadAccess.Any(item => item.BuildingId == building.Id) ? 0.8d : 0.2d;
            if (!PersistentRegionalEvolutionEngine.ShouldEmerge(population, jobs, connectivity, strongestInfluence)) continue;

            var service = Math.Clamp(jobs / Math.Max(1d, population), 0d, 1d);
            var accessibility = connectivity;
            var radius = Math.Clamp(500d + Math.Sqrt(Math.Max(1, population)) * 28d + jobs, 500d, 30_000d);
            var density = Math.Clamp(population / Math.Max(1d, Math.PI * radius * radius) * 2_000_000d, 0d, 1d);
            var id = new SettlementId(nextSettlementId++);
            settlements.Add(new SettlementEvolutionState(
                id,
                center,
                population,
                jobs,
                service,
                density,
                accessibility,
                radius,
                PersistentRegionalEvolutionEngine.Classify(population, jobs, service, density, accessibility),
                SettlementTrend.Growing,
                true,
                source.CurrentYear,
                null));
            events.Add(new RegionalEvolutionEvent(
                new RegionalEvolutionEventId(nextEventId++),
                source.CurrentYear,
                RegionalEvolutionEventKind.SettlementEmergence,
                id,
                null,
                $"population {population}, jobs {jobs}, connectivity {connectivity:F2}"));
        }

        return source with
        {
            Settlements = settlements.OrderBy(static item => item.SettlementId.Value).ToArray(),
            Events = events.OrderBy(static item => item.Id.Value).ToArray(),
        };
    }

    private static PersistentRegionalEvolutionSnapshot RecordRegionalRelationChanges(
        PersistentRegionalEvolutionSnapshot previous,
        PersistentRegionalEvolutionSnapshot current)
    {
        var previousKeys = previous.Relations
            .Where(static item => item.IsActive)
            .Select(static item => (item.FromSettlementId, item.ToSettlementId, item.Kind))
            .ToHashSet();
        var currentKeys = current.Relations
            .Where(static item => item.IsActive)
            .Select(static item => (item.FromSettlementId, item.ToSettlementId, item.Kind))
            .ToHashSet();
        if (previousKeys.SetEquals(currentKeys)) return current;

        var events = current.Events.ToList();
        var nextEventId = events.Count == 0 ? 1UL : checked(events.Max(static item => item.Id.Value) + 1UL);
        foreach (var relation in currentKeys.Except(previousKeys).OrderBy(static item => item.FromSettlementId.Value).ThenBy(static item => item.ToSettlementId.Value).ThenBy(static item => item.Kind))
            events.Add(new RegionalEvolutionEvent(new RegionalEvolutionEventId(nextEventId++), current.CurrentYear,
                RegionalEvolutionEventKind.RegionalRelationFormed, relation.FromSettlementId, null,
                $"{relation.Kind}:{relation.FromSettlementId.Value}->{relation.ToSettlementId.Value}"));
        foreach (var relation in previousKeys.Except(currentKeys).OrderBy(static item => item.FromSettlementId.Value).ThenBy(static item => item.ToSettlementId.Value).ThenBy(static item => item.Kind))
            events.Add(new RegionalEvolutionEvent(new RegionalEvolutionEventId(nextEventId++), current.CurrentYear,
                RegionalEvolutionEventKind.RegionalRelationEnded, relation.FromSettlementId, null,
                $"{relation.Kind}:{relation.FromSettlementId.Value}->{relation.ToSettlementId.Value}"));
        return current with { Events = events.OrderBy(static item => item.Id.Value).ToArray() };
    }

    private bool TryResolveRegionalEndpointPosition(TripEndpoint endpoint, out WorldPoint position)
    {
        if (endpoint.BuildingId is { } buildingId && TryGetBuildingSnapshot(buildingId, out var building))
        {
            position = Center(building.Bounds);
            return true;
        }
        if (endpoint.PoiId is { } poiId && TryGetPoiSnapshot(poiId, out var poi))
        {
            position = poi.Position;
            return true;
        }
        position = default;
        return false;
    }

    private static SettlementEvolutionState? FindNearestSettlement(
        IReadOnlyList<SettlementEvolutionState> settlements,
        WorldPoint point)
    {
        SettlementEvolutionState? selected = null;
        var distance = double.PositiveInfinity;
        foreach (var settlement in settlements)
        {
            var candidate = Distance2D(point, settlement.Center);
            if (candidate >= distance) continue;
            distance = candidate;
            selected = settlement;
        }
        return selected;
    }

    private static double RegionalAttractiveness(SettlementEvolutionState settlement)
    {
        var employment = Math.Clamp(settlement.Jobs / Math.Max(1d, settlement.Population), 0d, 1d);
        return Math.Clamp(settlement.ServiceIndex * 0.35d + settlement.Accessibility * 0.35d + employment * 0.3d, 0d, 1d);
    }

    private static GeneratedBuildingUse MapDevelopmentUse(ZoneKind zone) => zone switch
    {
        ZoneKind.Residential => GeneratedBuildingUse.Residential,
        ZoneKind.Commercial => GeneratedBuildingUse.Commercial,
        ZoneKind.Industrial => GeneratedBuildingUse.Industrial,
        ZoneKind.MixedUse => GeneratedBuildingUse.MixedUse,
        ZoneKind.Civic => GeneratedBuildingUse.Civic,
        _ => GeneratedBuildingUse.MixedUse,
    };

    private static PoiKind? MapDevelopmentPoiKind(GeneratedBuildingUse use) => use switch
    {
        GeneratedBuildingUse.Commercial => PoiKind.Retail,
        GeneratedBuildingUse.Industrial => PoiKind.Workplace,
        GeneratedBuildingUse.MixedUse => PoiKind.Service,
        GeneratedBuildingUse.Civic => PoiKind.Service,
        GeneratedBuildingUse.Transport => PoiKind.Transit,
        _ => null,
    };

    private static int CalculateDevelopmentCapacity(ZoneKind zone, WorldVolume bounds)
    {
        var area = Math.Max(1d, (bounds.MaxX - bounds.MinX) * (bounds.MaxY - bounds.MinY));
        var density = zone switch
        {
            ZoneKind.Commercial or ZoneKind.MixedUse => 0.08d,
            ZoneKind.Industrial => 0.04d,
            ZoneKind.Civic => 0.03d,
            _ => 0.05d,
        };
        return Math.Clamp((int)Math.Round(area * density), 1, 10_000);
    }
}
