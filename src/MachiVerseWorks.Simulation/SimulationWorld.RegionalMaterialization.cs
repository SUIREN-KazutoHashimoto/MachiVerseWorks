namespace MachiVerseWorks.Simulation;

public sealed record RegionalMaterializationSummary(
    int RoadNodeCount,
    int RoadSegmentCount,
    int LaneCount,
    int LaneConnectionCount,
    int BuildingCount,
    int PoiCount,
    int HouseholdCount,
    int PersonCount,
    int CompanyCount,
    int EstablishmentCount,
    int JobCount,
    int EmploymentCount);

public enum RegionalInfrastructureKind : byte
{
    Railway = 0,
    Power = 1,
    Water = 2,
    Sewer = 3,
    Gas = 4,
    Optical = 5,
    Radio = 6,
}

public readonly record struct RegionalInfrastructureConstraintResult(
    RegionalInfrastructureKind Kind,
    bool IsAllowed,
    TerrainConstraintResult Terrain,
    SettlementId? NearestSettlementId,
    double SettlementDistanceMeters,
    string Reason);

public sealed partial class SimulationWorld
{
    public RegionalGenerationSnapshot InitializeRegionalWorld(
        WorldVolume volume,
        RegionalGenerationOptions? options,
        out RegionalMaterializationSummary materialization)
    {
        EnsureRegionalMaterializationTargetIsEmpty();
        var snapshot = GenerateRegionalGeneration(volume, options);
        materialization = MaterializeRegionalGeneration();
        return snapshot;
    }

    public RegionalMaterializationSummary MaterializeRegionalGeneration()
    {
        if (_regionalGeneration is null)
            throw new InvalidOperationException("Regional generation has not been initialized for this world.");
        EnsureRegionalMaterializationTargetIsEmpty();

        var beforeRoadNodes = RoadNodeCount;
        var beforeRoadSegments = RoadSegmentCount;
        var beforeLanes = LaneCount;
        var beforeLaneConnections = LaneConnectionCount;
        var beforeBuildings = BuildingCount;
        var beforePois = PoiCount;
        var beforeHouseholds = HouseholdCount;
        var beforePersons = PersonCount;
        var beforeCompanies = CompanyCount;
        var beforeEstablishments = EstablishmentCount;
        var beforeJobs = JobCount;
        var beforeEmployments = EmploymentCount;

        var roadSegmentsBySettlement = MaterializeRegionalRoadNetwork(_regionalGeneration);
        var actualBuildings = MaterializeRegionalBuildings(_regionalGeneration);
        var actualPois = MaterializeRegionalPois(_regionalGeneration, actualBuildings);
        CreateRegionalRoadAccessPoints(_regionalGeneration, actualBuildings, actualPois, roadSegmentsBySettlement);
        MaterializeRegionalPopulationAndEconomy(_regionalGeneration, actualBuildings, actualPois);

        return new RegionalMaterializationSummary(
            RoadNodeCount - beforeRoadNodes,
            RoadSegmentCount - beforeRoadSegments,
            LaneCount - beforeLanes,
            LaneConnectionCount - beforeLaneConnections,
            BuildingCount - beforeBuildings,
            PoiCount - beforePois,
            HouseholdCount - beforeHouseholds,
            PersonCount - beforePersons,
            CompanyCount - beforeCompanies,
            EstablishmentCount - beforeEstablishments,
            JobCount - beforeJobs,
            EmploymentCount - beforeEmployments);
    }

    public RegionalInfrastructureConstraintResult EvaluateRegionalInfrastructureConstraint(
        WorldVolume footprint,
        RegionalInfrastructureKind kind)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Regional infrastructure kind is not defined.");

        var terrainKind = kind == RegionalInfrastructureKind.Railway
            ? TerrainConstraintKind.Railway
            : TerrainConstraintKind.Generic;
        var terrain = EvaluateTerrainConstraint(footprint, terrainKind);
        SettlementId? nearestSettlement = null;
        var nearestDistance = double.PositiveInfinity;
        if (_regionalGeneration is not null)
        {
            var center = new WorldPoint(
                (footprint.MinX + footprint.MaxX) * 0.5d,
                (footprint.MinY + footprint.MaxY) * 0.5d,
                (footprint.MinZ + footprint.MaxZ) * 0.5d);
            foreach (var settlement in _regionalGeneration.Settlements)
            {
                var distance = Distance2D(center, settlement.Center);
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                nearestSettlement = settlement.Id;
            }
        }

        var allowed = terrain.IsAllowed;
        var reason = terrain.Reason;
        if (kind is RegionalInfrastructureKind.Water or RegionalInfrastructureKind.Sewer or RegionalInfrastructureKind.Gas or RegionalInfrastructureKind.Optical
            && terrain.IntersectsVoid)
        {
            allowed = false;
            reason = "Subsurface utility placement cannot intersect unsupported terrain voids.";
        }
        if (kind is RegionalInfrastructureKind.Power or RegionalInfrastructureKind.Radio
            && terrain.MaximumSlopeDegrees > 55d)
        {
            allowed = false;
            reason = "Surface infrastructure slope exceeds the regional generation constraint.";
        }

        return new RegionalInfrastructureConstraintResult(
            kind,
            allowed,
            terrain,
            nearestSettlement,
            nearestSettlement is null ? double.PositiveInfinity : nearestDistance,
            reason);
    }

    private void EnsureRegionalMaterializationTargetIsEmpty()
    {
        if (RoadNodeCount != 0
            || RoadSegmentCount != 0
            || LaneCount != 0
            || BuildingCount != 0
            || PoiCount != 0
            || HouseholdCount != 0
            || PersonCount != 0
            || CompanyCount != 0
            || EstablishmentCount != 0
            || JobCount != 0
            || EmploymentCount != 0)
        {
            throw new InvalidOperationException(
                "Regional generation can only be materialized into an empty initial urban/population/economy state.");
        }
    }

    private Dictionary<SettlementId, List<RoadSegmentId>> MaterializeRegionalRoadNetwork(RegionalGenerationSnapshot snapshot)
    {
        var roadCorridors = snapshot.Corridors
            .Where(static item => item.Kind != RegionalCorridorKind.Railway)
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        var degree = snapshot.Settlements.ToDictionary(static item => item.Id, static _ => 0);
        foreach (var corridor in roadCorridors)
        {
            degree[corridor.FromSettlementId]++;
            degree[corridor.ToSettlementId]++;
        }

        var nodeBySettlement = new Dictionary<SettlementId, RoadNodeId>();
        foreach (var settlement in snapshot.Settlements.OrderBy(static item => item.Id.Value))
        {
            var kind = degree[settlement.Id] > 1 ? RoadNodeKind.Intersection : RoadNodeKind.Endpoint;
            nodeBySettlement.Add(settlement.Id, CreateRoadNode(SnapToGround(settlement.Center), kind));
        }

        var segmentBindings = new List<MaterializedRegionalSegment>(roadCorridors.Length * 2);
        var segmentsBySettlement = snapshot.Settlements.ToDictionary(static item => item.Id, static _ => new List<RoadSegmentId>());
        foreach (var corridor in roadCorridors)
        {
            var fromNode = nodeBySettlement[corridor.FromSettlementId];
            var toNode = nodeBySettlement[corridor.ToSettlementId];
            var middleSource = corridor.Geometry[corridor.Geometry.Count / 2];
            var middle = SnapToGround(middleSource);
            var middleNode = CreateRoadNode(middle, RoadNodeKind.Intersection);
            var roadKind = MapRoadKind(corridor.Kind);
            var first = CreateRoadSegment(fromNode, middleNode, roadKind);
            var second = CreateRoadSegment(middleNode, toNode, roadKind);
            var speed = roadKind switch
            {
                RoadKind.Highway => 27.7777777778d,
                RoadKind.Arterial => 22.2222222222d,
                RoadKind.Collector => 16.6666666667d,
                _ => 13.8888888889d,
            };
            var firstForward = CreateLane(first, LaneDirection.Forward, 0, 3.5d, speed);
            var firstReverse = CreateLane(first, LaneDirection.Reverse, 0, 3.5d, speed);
            var secondForward = CreateLane(second, LaneDirection.Forward, 0, 3.5d, speed);
            var secondReverse = CreateLane(second, LaneDirection.Reverse, 0, 3.5d, speed);
            segmentBindings.Add(new MaterializedRegionalSegment(first, fromNode, middleNode, firstForward, firstReverse));
            segmentBindings.Add(new MaterializedRegionalSegment(second, middleNode, toNode, secondForward, secondReverse));
            segmentsBySettlement[corridor.FromSettlementId].Add(first);
            segmentsBySettlement[corridor.FromSettlementId].Add(second);
            segmentsBySettlement[corridor.ToSettlementId].Add(first);
            segmentsBySettlement[corridor.ToSettlementId].Add(second);
        }

        foreach (var node in segmentBindings
                     .SelectMany(static item => new[] { item.StartNodeId, item.EndNodeId })
                     .Distinct()
                     .OrderBy(static item => item.Value))
        {
            if (!TryGetRoadNodeSnapshot(node, out var nodeSnapshot) || nodeSnapshot.Kind != RoadNodeKind.Intersection) continue;
            var incoming = new List<(RoadSegmentId SegmentId, LaneId LaneId)>();
            var outgoing = new List<(RoadSegmentId SegmentId, LaneId LaneId)>();
            foreach (var segment in segmentBindings)
            {
                if (segment.EndNodeId == node)
                {
                    incoming.Add((segment.SegmentId, segment.ForwardLaneId));
                    outgoing.Add((segment.SegmentId, segment.ReverseLaneId));
                }
                if (segment.StartNodeId == node)
                {
                    incoming.Add((segment.SegmentId, segment.ReverseLaneId));
                    outgoing.Add((segment.SegmentId, segment.ForwardLaneId));
                }
            }
            foreach (var from in incoming.OrderBy(static item => item.LaneId.Value))
            {
                foreach (var to in outgoing.OrderBy(static item => item.LaneId.Value))
                {
                    if (from.SegmentId == to.SegmentId) continue;
                    CreateLaneConnection(from.LaneId, to.LaneId, node, TurnMovement.Unspecified);
                }
            }
        }

        return segmentsBySettlement;
    }

    private Dictionary<GeneratedBuildingId, BuildingId> MaterializeRegionalBuildings(RegionalGenerationSnapshot snapshot)
    {
        var result = new Dictionary<GeneratedBuildingId, BuildingId>();
        foreach (var generated in snapshot.Buildings.OrderBy(static item => item.Id.Value))
        {
            var bounds = NormalizeBuildingBoundsToTerrain(generated.Bounds);
            result.Add(generated.Id, CreateBuilding(bounds, MapBuildingKind(generated.Use)));
        }
        return result;
    }

    private Dictionary<GeneratedPoiId, PoiId> MaterializeRegionalPois(
        RegionalGenerationSnapshot snapshot,
        IReadOnlyDictionary<GeneratedBuildingId, BuildingId> actualBuildings)
    {
        var result = new Dictionary<GeneratedPoiId, PoiId>();
        foreach (var generated in snapshot.Pois.OrderBy(static item => item.Id.Value))
        {
            BuildingId? buildingId = null;
            var position = generated.Position;
            if (generated.BuildingId is { } generatedBuildingId
                && actualBuildings.TryGetValue(generatedBuildingId, out var actualBuildingId))
            {
                buildingId = actualBuildingId;
                if (TryGetBuildingSnapshot(actualBuildingId, out var building))
                {
                    position = Center(building.Bounds);
                }
            }
            else
            {
                position = SnapToGround(position);
            }
            result.Add(generated.Id, CreatePoi(position, MapPoiKind(generated.Kind), buildingId));
        }
        return result;
    }

    private void CreateRegionalRoadAccessPoints(
        RegionalGenerationSnapshot snapshot,
        IReadOnlyDictionary<GeneratedBuildingId, BuildingId> actualBuildings,
        IReadOnlyDictionary<GeneratedPoiId, PoiId> actualPois,
        IReadOnlyDictionary<SettlementId, List<RoadSegmentId>> roadSegmentsBySettlement)
    {
        var parcelById = snapshot.Parcels.ToDictionary(static item => item.Id);
        var poiByBuilding = snapshot.Pois
            .Where(static item => item.BuildingId is not null)
            .GroupBy(static item => item.BuildingId!.Value)
            .ToDictionary(static group => group.Key, static group => group.First().Id);
        foreach (var generated in snapshot.Buildings.OrderBy(static item => item.Id.Value))
        {
            if (!actualBuildings.TryGetValue(generated.Id, out var actualBuildingId)) continue;
            var parcel = parcelById[generated.ParcelId];
            if (!roadSegmentsBySettlement.TryGetValue(parcel.SettlementId, out var candidates) || candidates.Count == 0) continue;
            var segmentId = SelectNearestRoadSegment(candidates, generated.Bounds);
            PoiId? poiId = null;
            if (poiByBuilding.TryGetValue(generated.Id, out var generatedPoiId)
                && actualPois.TryGetValue(generatedPoiId, out var actualPoiId))
            {
                poiId = actualPoiId;
            }
            CreateRoadAccessPoint(segmentId, 0.5d, actualBuildingId, poiId, RoadAccessMode.Motor | RoadAccessMode.Foot);
        }
    }

    private void MaterializeRegionalPopulationAndEconomy(
        RegionalGenerationSnapshot snapshot,
        IReadOnlyDictionary<GeneratedBuildingId, BuildingId> actualBuildings,
        IReadOnlyDictionary<GeneratedPoiId, PoiId> actualPois)
    {
        var parcelById = snapshot.Parcels.ToDictionary(static item => item.Id);
        var generatedBuildingsBySettlement = snapshot.Buildings
            .GroupBy(item => parcelById[item.ParcelId].SettlementId)
            .ToDictionary(static group => group.Key, static group => group.OrderBy(static item => item.Id.Value).ToArray());
        var actualPoiBySettlement = snapshot.Pois
            .Where(item => actualPois.ContainsKey(item.Id))
            .GroupBy(static item => item.SettlementId)
            .ToDictionary(static group => group.Key, group => group.Select(item => actualPois[item.Id]).ToArray());

        foreach (var settlement in snapshot.Settlements.OrderBy(static item => item.Id.Value))
        {
            if (!generatedBuildingsBySettlement.TryGetValue(settlement.Id, out var generatedSettlementBuildings)
                || generatedSettlementBuildings.Length == 0)
            {
                var ground = SnapToGround(settlement.Center);
                var fallbackBounds = new WorldVolume(
                    ground.X - 20d,
                    ground.Y - 20d,
                    ground.Z,
                    ground.X + 20d,
                    ground.Y + 20d,
                    ground.Z + 12d);
                var fallback = CreateBuilding(fallbackBounds, BuildingKind.MixedUse);
                MaterializeSettlementPopulationAndJobs(settlement, new[] { fallback });
                continue;
            }

            var settlementBuildings = generatedSettlementBuildings
                .Select(item => actualBuildings[item.Id])
                .ToArray();
            MaterializeSettlementPopulationAndJobs(settlement, settlementBuildings);
        }
    }

    private void MaterializeSettlementPopulationAndJobs(
        Settlement settlement,
        IReadOnlyList<BuildingId> settlementBuildings)
    {
        var residential = settlementBuildings
            .Where(id => TryGetBuildingSnapshot(id, out var building)
                && building.Kind is BuildingKind.Residential or BuildingKind.MixedUse)
            .ToArray();
        if (residential.Length == 0) residential = settlementBuildings.ToArray();

        var workplaces = settlementBuildings
            .Where(id => TryGetBuildingSnapshot(id, out var building)
                && building.Kind is BuildingKind.Commercial or BuildingKind.Industrial or BuildingKind.Civic or BuildingKind.MixedUse)
            .ToArray();
        if (workplaces.Length == 0) workplaces = settlementBuildings.ToArray();
        var workplace = workplaces[0];
        var company = CreateCompany(
            MapIndustrySector(settlement.InitialEconomy),
            initialCashBalance: checked((long)Math.Max(10_000d, settlement.Jobs * 2_000d)),
            dailyProductionCapacity: Math.Max(1d, settlement.Jobs * 0.75d));
        var establishment = CreateEstablishment(company, workplace);
        var job = CreateJob(establishment, Math.Max(1, settlement.Jobs), dailyWage: 120);

        var remainingPopulation = settlement.Population;
        var personOrdinal = 0;
        var employmentTarget = Math.Min(settlement.Population, settlement.Jobs);
        while (remainingPopulation > 0)
        {
            var householdSize = Math.Min(3, remainingPopulation);
            var residenceBuilding = residential[(personOrdinal / 3) % residential.Length];
            var household = CreateHousehold(TripEndpoint.ForBuilding(residenceBuilding));
            SetHouseholdCashBalance(household, 5_000);
            for (var member = 0; member < householdSize; member++)
            {
                var isEmployed = personOrdinal < employmentTarget;
                var schedule = isEmployed
                    ? new[]
                    {
                        new DailyActivityWindow(
                            ActivityKind.Work,
                            EconomyDefaults.WorkStartMinuteOfDay,
                            EconomyDefaults.WorkEndMinuteOfDay,
                            TripEndpoint.ForBuilding(workplace),
                            ActivityPriority.High),
                    }
                    : Array.Empty<DailyActivityWindow>();
                var demographics = new PersonDemographics(
                    AgeYears: 18 + (personOrdinal % 48),
                    IsEmployed: isEmployed,
                    IsStudent: !isEmployed && personOrdinal % 5 == 0,
                    HasPrivateVehicle: personOrdinal % 3 == 0);
                var person = CreatePerson(household, demographics, schedule);
                if (isEmployed) AssignEmployment(person, job);
                personOrdinal++;
                remainingPopulation--;
            }
        }
    }

    private RoadSegmentId SelectNearestRoadSegment(IReadOnlyList<RoadSegmentId> candidates, WorldVolume buildingBounds)
    {
        var buildingCenter = Center(buildingBounds);
        var selected = candidates[0];
        var bestDistance = double.PositiveInfinity;
        foreach (var candidate in candidates.Distinct().OrderBy(static item => item.Value))
        {
            if (!TryGetRoadSegmentSnapshot(candidate, out var segment)
                || !TryGetRoadNodeSnapshot(segment.StartNodeId, out var start)
                || !TryGetRoadNodeSnapshot(segment.EndNodeId, out var end))
            {
                continue;
            }
            var distance = Distance2D(buildingCenter, Midpoint(start.Position, end.Position));
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            selected = candidate;
        }
        return selected;
    }

    private WorldVolume NormalizeBuildingBoundsToTerrain(WorldVolume source)
    {
        var centerX = (source.MinX + source.MaxX) * 0.5d;
        var centerY = (source.MinY + source.MaxY) * 0.5d;
        var ground = QueryTerrainSurface(centerX, centerY).Position.Z;
        var height = Math.Max(3d, source.Height);
        return new WorldVolume(source.MinX, source.MinY, ground, source.MaxX, source.MaxY, ground + height);
    }

    private static RoadKind MapRoadKind(RegionalCorridorKind kind) => kind switch
    {
        RegionalCorridorKind.PrimaryRoad => RoadKind.Collector,
        RegionalCorridorKind.RegionalRoad => RoadKind.Arterial,
        RegionalCorridorKind.IntercityRoad => RoadKind.Highway,
        _ => RoadKind.Local,
    };

    private static BuildingKind MapBuildingKind(GeneratedBuildingUse use) => use switch
    {
        GeneratedBuildingUse.Residential => BuildingKind.Residential,
        GeneratedBuildingUse.Commercial => BuildingKind.Commercial,
        GeneratedBuildingUse.Industrial => BuildingKind.Industrial,
        GeneratedBuildingUse.MixedUse => BuildingKind.MixedUse,
        GeneratedBuildingUse.Civic => BuildingKind.Civic,
        _ => BuildingKind.Generic,
    };

    private static PoiKind MapPoiKind(GeneratedPoiKind kind) => kind switch
    {
        GeneratedPoiKind.Station => PoiKind.Transit,
        GeneratedPoiKind.Market => PoiKind.Retail,
        GeneratedPoiKind.CivicCenter => PoiKind.Service,
        GeneratedPoiKind.IndustrialHub => PoiKind.Workplace,
        GeneratedPoiKind.Port => PoiKind.Transit,
        _ => PoiKind.Service,
    };

    private static IndustrySector MapIndustrySector(InitialEconomyKind kind) => kind switch
    {
        InitialEconomyKind.Agriculture => IndustrySector.Generic,
        InitialEconomyKind.Trade => IndustrySector.Retail,
        InitialEconomyKind.Manufacturing => IndustrySector.Manufacturing,
        InitialEconomyKind.PortTrade => IndustrySector.Transport,
        InitialEconomyKind.Transport => IndustrySector.Transport,
        InitialEconomyKind.ResourceExtraction => IndustrySector.Manufacturing,
        InitialEconomyKind.Services => IndustrySector.Services,
        _ => IndustrySector.Generic,
    };

    private static WorldPoint Center(WorldVolume volume) => new(
        (volume.MinX + volume.MaxX) * 0.5d,
        (volume.MinY + volume.MaxY) * 0.5d,
        (volume.MinZ + volume.MaxZ) * 0.5d);

    private static WorldPoint Midpoint(WorldPoint first, WorldPoint second) => new(
        (first.X + second.X) * 0.5d,
        (first.Y + second.Y) * 0.5d,
        (first.Z + second.Z) * 0.5d);

    private static double Distance2D(WorldPoint first, WorldPoint second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt((x * x) + (y * y));
    }

    private sealed record MaterializedRegionalSegment(
        RoadSegmentId SegmentId,
        RoadNodeId StartNodeId,
        RoadNodeId EndNodeId,
        LaneId ForwardLaneId,
        LaneId ReverseLaneId);
}
