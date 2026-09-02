namespace MachiVerseWorks.Simulation;

public sealed class RegionalGenerator
{
    private readonly WorldEnvironmentGenerator _environment;
    private readonly ulong _worldSeed;

    public RegionalGenerator(WorldEnvironmentGenerator environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _worldSeed = environment.Config.WorldSeed;
    }

    public RegionalGenerationSnapshot Generate(
        WorldVolume volume,
        RegionalGenerationOptions? options = null,
        ulong tickCount = 0)
    {
        if (volume.Width <= 0d || volume.Depth <= 0d)
            throw new ArgumentOutOfRangeException(nameof(volume), "Regional generation requires a non-empty horizontal volume.");

        options ??= new RegionalGenerationOptions();
        var settlementCount = options.ResolveSettlementCount();
        var iterationBudget = options.ResolveIterationBudget();
        var candidates = _environment.SelectSettlementCandidates(volume, Math.Min(64, settlementCount * 4));
        if (candidates.Count == 0)
            throw new InvalidOperationException("The requested region does not contain a viable settlement candidate.");

        var features = _environment.DetectGeographicFeatures(volume, 128);
        var naturalToponyms = features
            .Select(_environment.CreateToponym)
            .ToDictionary(static item => item.FeatureId);

        var selected = SelectOrigins(candidates, settlementCount, volume);
        var toponyms = new List<HumanToponym>();
        var settlements = new List<Settlement>(selected.Count);
        for (var index = 0; index < selected.Count; index++)
        {
            var candidate = selected[index];
            var sample = _environment.Sample(candidate.Center);
            var suitability = EvaluateSuitability(sample, candidate, volume);
            var origin = DetermineOrigin(candidate.Environment, sample);
            var role = DetermineRole(origin, suitability, index);
            var economy = DetermineInitialEconomy(role, origin);
            var source = FindNearestNaturalToponym(candidate.Center, features, naturalToponyms);
            var name = CreateHumanToponym(
                HumanToponymKind.Settlement,
                CreateSettlementName(source?.Name, candidate.Environment, index),
                source,
                source?.FeatureId,
                null,
                Hash(candidate.Center.X, candidate.Center.Y, 0x5101UL));
            toponyms.Add(name);

            var population = CalculateInitialPopulation(suitability.TotalScore, role, index);
            var jobs = Math.Max(12, (int)Math.Round(population * GetJobRatio(role)));
            var influence = Math.Clamp(1_200d + Math.Sqrt(population) * 145d, 1_500d, 35_000d);
            settlements.Add(new Settlement(
                new SettlementId(Hash(candidate.Center.X, candidate.Center.Y, 0x5102UL)),
                sample.Position,
                candidate.Environment,
                origin,
                role,
                economy,
                suitability,
                population,
                jobs,
                influence,
                name.Id));
        }

        var growthEvents = GenerateGrowthEvents(settlements);
        var corridors = GenerateRegionalCorridors(settlements, toponyms, features);
        var districts = GenerateDistricts(settlements, toponyms);
        var parcels = GenerateParcels(settlements, districts);
        var buildings = GenerateBuildings(parcels);
        parcels = AttachBuildings(parcels, buildings);
        var pois = GeneratePois(settlements, districts, buildings, toponyms);
        var roadSigns = GenerateRoadSigns(corridors, settlements, toponyms, features);

        var iterations = 0;
        var quality = EvaluateQuality(settlements, corridors, parcels, buildings);
        while (iterations < iterationBudget)
        {
            iterations++;
            if (quality.RoadConnectivity >= 0.999d
                && quality.Accessibility >= 0.72d
                && quality.CongestionRisk <= 0.42d
                && quality.OverallScore >= GetTargetQuality(options.Preset))
            {
                break;
            }

            if (TryAddSupplementalCorridor(settlements, corridors, toponyms, features, iterations))
            {
                roadSigns = GenerateRoadSigns(corridors, settlements, toponyms, features);
            }
            quality = EvaluateQuality(settlements, corridors, parcels, buildings);
        }

        return new RegionalGenerationSnapshot(
            volume,
            options.Preset,
            _worldSeed,
            iterations,
            settlements.OrderBy(static item => item.Id.Value).ToArray(),
            growthEvents.OrderBy(static item => item.Id.Value).ToArray(),
            corridors.OrderBy(static item => item.Id.Value).ToArray(),
            districts.OrderBy(static item => item.Id.Value).ToArray(),
            parcels.OrderBy(static item => item.Id.Value).ToArray(),
            buildings.OrderBy(static item => item.Id.Value).ToArray(),
            pois.OrderBy(static item => item.Id.Value).ToArray(),
            toponyms.GroupBy(static item => item.Id).Select(static group => group.First()).OrderBy(static item => item.Id.Value).ToArray(),
            roadSigns.OrderBy(static item => item.Id.Value).ToArray(),
            quality,
            tickCount);
    }

    public SettlementSuitability EvaluateSuitability(
        RegionalEnvironmentSample sample,
        SettlementCandidateRegion candidate,
        WorldVolume regionalVolume)
    {
        var flatness = Math.Clamp(1d - sample.TerrainRuggedness, 0d, 1d);
        var waterAccess = Math.Clamp(
            (sample.Hydrology.RiverStrength * 0.55d)
            + ((1d - Math.Min(sample.CoastlineDistanceMeters, 120_000d) / 120_000d) * 0.45d),
            0d,
            1d);
        var transportPotential = Math.Clamp((candidate.TransportScore * 0.65d) + (flatness * 0.35d), 0d, 1d);
        var buildability = sample.Buildability;
        var resourceAccess = Math.Clamp(
            (sample.TerrainRuggedness * 0.30d)
            + (sample.Hydrology.Drainage * 0.25d)
            + (candidate.NaturalScore * 0.45d),
            0d,
            1d);
        var floodRisk = sample.Hydrology.FloodRisk;
        var steepSlopeRisk = sample.TerrainRuggedness;
        var nearestEdge = Math.Min(
            Math.Min(sample.Position.X - regionalVolume.MinX, regionalVolume.MaxX - sample.Position.X),
            Math.Min(sample.Position.Y - regionalVolume.MinY, regionalVolume.MaxY - sample.Position.Y));
        var regionalScale = Math.Max(1d, Math.Min(regionalVolume.Width, regionalVolume.Depth) * 0.5d);
        var isolation = Math.Clamp(1d - (nearestEdge / regionalScale), 0d, 1d) * 0.22d;
        var constructionCost = Math.Clamp(
            (steepSlopeRisk * 0.48d)
            + (floodRisk * 0.28d)
            + ((1d - buildability) * 0.24d),
            0d,
            1d);
        var total = Math.Clamp(
            (flatness * 0.14d)
            + (waterAccess * 0.15d)
            + (transportPotential * 0.19d)
            + (buildability * 0.22d)
            + (resourceAccess * 0.08d)
            + ((1d - floodRisk) * 0.08d)
            + ((1d - steepSlopeRisk) * 0.06d)
            + ((1d - isolation) * 0.04d)
            + ((1d - constructionCost) * 0.04d),
            0d,
            1d);
        return new SettlementSuitability(
            flatness,
            waterAccess,
            transportPotential,
            buildability,
            resourceAccess,
            floodRisk,
            steepSlopeRisk,
            isolation,
            constructionCost,
            total);
    }

    private List<SettlementCandidateRegion> SelectOrigins(
        IReadOnlyList<SettlementCandidateRegion> candidates,
        int targetCount,
        WorldVolume volume)
    {
        var ranked = candidates
            .Select(candidate => (Candidate: candidate, Score: candidate.TotalScore + DeterministicUnit(candidate.Center.X, candidate.Center.Y, 0x5201UL) * 0.025d))
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Candidate.Center.X)
            .ThenBy(static item => item.Candidate.Center.Y)
            .ToArray();

        var selected = new List<SettlementCandidateRegion>(Math.Min(targetCount, ranked.Length));
        var minimumSpacing = Math.Max(
            750d,
            Math.Min(volume.Width, volume.Depth) / Math.Max(4d, Math.Sqrt(targetCount) * 2.8d));

        foreach (var environmentGroup in ranked.GroupBy(static item => item.Candidate.Environment).OrderBy(static group => group.Key))
        {
            if (selected.Count >= targetCount) break;
            var candidate = environmentGroup.First().Candidate;
            if (IsFarEnough(candidate.Center, selected, minimumSpacing * 0.65d)) selected.Add(candidate);
        }

        foreach (var rankedCandidate in ranked)
        {
            if (selected.Count >= targetCount) break;
            if (selected.Contains(rankedCandidate.Candidate)) continue;
            if (IsFarEnough(rankedCandidate.Candidate.Center, selected, minimumSpacing)) selected.Add(rankedCandidate.Candidate);
        }

        foreach (var rankedCandidate in ranked)
        {
            if (selected.Count >= targetCount) break;
            if (!selected.Contains(rankedCandidate.Candidate)) selected.Add(rankedCandidate.Candidate);
        }
        return selected;
    }

    private static bool IsFarEnough(
        WorldPoint point,
        IReadOnlyList<SettlementCandidateRegion> selected,
        double minimumSpacing)
    {
        foreach (var item in selected)
        {
            if (Distance2D(point, item.Center) < minimumSpacing) return false;
        }
        return true;
    }

    private static SettlementOriginKind DetermineOrigin(
        SettlementEnvironmentKind environment,
        RegionalEnvironmentSample sample)
    {
        if (environment == SettlementEnvironmentKind.Island) return SettlementOriginKind.Island;
        if (environment == SettlementEnvironmentKind.Coastal)
        {
            if (sample.Hydrology.RiverStrength > 0.55d) return SettlementOriginKind.Estuary;
            return sample.CoastlineDistanceMeters < 12_000d ? SettlementOriginKind.Bay : SettlementOriginKind.Coastal;
        }
        if (environment == SettlementEnvironmentKind.River) return SettlementOriginKind.RiverPlain;
        if (environment == SettlementEnvironmentKind.Basin) return SettlementOriginKind.Basin;
        if (environment == SettlementEnvironmentKind.Mountain)
            return sample.TerrainRuggedness > 0.62d ? SettlementOriginKind.MountainPass : SettlementOriginKind.Valley;
        if (environment == SettlementEnvironmentKind.Dry) return SettlementOriginKind.ResourceAccess;
        return SettlementOriginKind.InlandPlain;
    }

    private static RegionalRole DetermineRole(
        SettlementOriginKind origin,
        SettlementSuitability suitability,
        int index)
    {
        if (origin is SettlementOriginKind.Estuary or SettlementOriginKind.Bay or SettlementOriginKind.Coastal)
            return RegionalRole.Port;
        if (origin == SettlementOriginKind.MountainPass) return RegionalRole.TransportHub;
        if (origin == SettlementOriginKind.ResourceAccess) return RegionalRole.Resource;
        if (suitability.TransportPotential > 0.76d && index % 3 == 0) return RegionalRole.TransportHub;
        if (suitability.ResourceAccess > 0.72d && index % 2 == 0) return RegionalRole.Industrial;
        if (suitability.TotalScore > 0.72d && index % 4 == 0) return RegionalRole.Administrative;
        if (suitability.WaterAccess > 0.58d) return RegionalRole.Market;
        if (suitability.Flatness > 0.70d) return RegionalRole.Agricultural;
        return RegionalRole.LocalService;
    }

    private static InitialEconomyKind DetermineInitialEconomy(RegionalRole role, SettlementOriginKind origin) => role switch
    {
        RegionalRole.Port => InitialEconomyKind.PortTrade,
        RegionalRole.TransportHub => InitialEconomyKind.Transport,
        RegionalRole.Resource => InitialEconomyKind.ResourceExtraction,
        RegionalRole.Industrial => InitialEconomyKind.Manufacturing,
        RegionalRole.Agricultural => InitialEconomyKind.Agriculture,
        RegionalRole.Market => InitialEconomyKind.Trade,
        RegionalRole.Administrative => InitialEconomyKind.Services,
        _ => origin == SettlementOriginKind.InlandPlain ? InitialEconomyKind.Agriculture : InitialEconomyKind.Subsistence,
    };

    private static int CalculateInitialPopulation(double score, RegionalRole role, int index)
    {
        var roleMultiplier = role switch
        {
            RegionalRole.Administrative => 1.65d,
            RegionalRole.Port => 1.45d,
            RegionalRole.TransportHub => 1.35d,
            RegionalRole.Industrial => 1.25d,
            RegionalRole.Market => 1.15d,
            _ => 1d,
        };
        return Math.Max(80, (int)Math.Round((180d + score * 4_200d + ((index % 5) * 95d)) * roleMultiplier));
    }

    private static double GetJobRatio(RegionalRole role) => role switch
    {
        RegionalRole.Industrial => 0.58d,
        RegionalRole.Port => 0.54d,
        RegionalRole.TransportHub => 0.51d,
        RegionalRole.Administrative => 0.49d,
        RegionalRole.Market => 0.46d,
        _ => 0.38d,
    };

    private List<HistoricalGrowthEvent> GenerateGrowthEvents(IReadOnlyList<Settlement> settlements)
    {
        var result = new List<HistoricalGrowthEvent>(settlements.Count * 5);
        foreach (var settlement in settlements)
        {
            var stages = new List<HistoricalGrowthStage>
            {
                HistoricalGrowthStage.Origin,
                HistoricalGrowthStage.CenterFormation,
                HistoricalGrowthStage.UrbanExpansion,
                HistoricalGrowthStage.Suburbanization,
                HistoricalGrowthStage.Redevelopment,
            };
            if (settlement.Role is RegionalRole.Administrative or RegionalRole.TransportHub or RegionalRole.Port)
                stages.Add(HistoricalGrowthStage.NewCenterFormation);

            for (var sequence = 0; sequence < stages.Count; sequence++)
            {
                var stage = stages[sequence];
                var scale = (sequence + 1d) / stages.Count;
                var offsetAngle = DeterministicUnit(settlement.Center.X, settlement.Center.Y, 0x5300UL + (ulong)sequence) * Math.PI * 2d;
                var offsetDistance = settlement.InfluenceRadiusMeters * 0.06d * sequence;
                var center = _environment.Sample(new WorldPoint(
                    settlement.Center.X + Math.Cos(offsetAngle) * offsetDistance,
                    settlement.Center.Y + Math.Sin(offsetAngle) * offsetDistance,
                    0d)).Position;
                result.Add(new HistoricalGrowthEvent(
                    new GrowthEventId(Hash(settlement.Id.Value, (ulong)sequence, 0x5301UL)),
                    settlement.Id,
                    stage,
                    sequence,
                    center,
                    sequence == 0 ? settlement.Population / 8 : Math.Max(10, (int)Math.Round(settlement.Population * scale / stages.Count)),
                    sequence == 0 ? settlement.Jobs / 8 : Math.Max(4, (int)Math.Round(settlement.Jobs * scale / stages.Count)),
                    GetGrowthReason(stage, settlement.Role)));
            }
        }
        return result;
    }

    private static string GetGrowthReason(HistoricalGrowthStage stage, RegionalRole role) => stage switch
    {
        HistoricalGrowthStage.Origin => "environmental suitability and initial access",
        HistoricalGrowthStage.CenterFormation => "local services and market concentration",
        HistoricalGrowthStage.UrbanExpansion => "population and employment growth",
        HistoricalGrowthStage.Suburbanization => "land pressure and improved accessibility",
        HistoricalGrowthStage.Redevelopment => "accessibility and central land pressure",
        HistoricalGrowthStage.NewCenterFormation => $"polycentric expansion around {role}",
        _ => "historical development",
    };

    private List<RegionalCorridor> GenerateRegionalCorridors(
        IReadOnlyList<Settlement> settlements,
        List<HumanToponym> toponyms,
        IReadOnlyList<GeographicFeature> features)
    {
        var corridors = new List<RegionalCorridor>();
        if (settlements.Count < 2) return corridors;

        var connected = new List<Settlement> { settlements.OrderByDescending(static item => item.Population).First() };
        var remaining = settlements.Where(item => item.Id != connected[0].Id).ToList();
        while (remaining.Count > 0)
        {
            Settlement? bestFrom = null;
            Settlement? bestTo = null;
            var bestCost = double.PositiveInfinity;
            foreach (var from in connected)
            {
                foreach (var to in remaining)
                {
                    var distance = Distance2D(from.Center, to.Center);
                    var midpoint = Midpoint(from.Center, to.Center);
                    var terrain = _environment.Sample(midpoint);
                    var cost = distance * (1d + terrain.TerrainRuggedness * 0.7d + terrain.Hydrology.FloodRisk * 0.35d);
                    if (cost >= bestCost) continue;
                    bestCost = cost;
                    bestFrom = from;
                    bestTo = to;
                }
            }

            if (bestFrom is null || bestTo is null) break;
            corridors.Add(CreateCorridor(bestFrom, bestTo, RegionalCorridorKind.IntercityRoad, corridors.Count, toponyms, features));
            connected.Add(bestTo);
            remaining.Remove(bestTo);
        }

        var hubs = settlements
            .Where(static item => item.Role is RegionalRole.Port or RegionalRole.TransportHub or RegionalRole.Administrative)
            .OrderByDescending(static item => item.Population)
            .Take(4)
            .ToArray();
        for (var index = 1; index < hubs.Length; index++)
        {
            if (!HasPair(corridors, hubs[index - 1].Id, hubs[index].Id))
                corridors.Add(CreateCorridor(hubs[index - 1], hubs[index], RegionalCorridorKind.RegionalRoad, corridors.Count, toponyms, features));
        }

        if (settlements.Count >= 6)
        {
            var railTargets = settlements.OrderByDescending(static item => item.Population).Take(Math.Min(4, settlements.Count)).ToArray();
            for (var index = 1; index < railTargets.Length; index++)
                corridors.Add(CreateCorridor(railTargets[index - 1], railTargets[index], RegionalCorridorKind.Railway, corridors.Count, toponyms, features));
        }
        return corridors;
    }

    private RegionalCorridor CreateCorridor(
        Settlement from,
        Settlement to,
        RegionalCorridorKind kind,
        int ordinal,
        List<HumanToponym> toponyms,
        IReadOnlyList<GeographicFeature> features)
    {
        var midpoint2D = Midpoint(from.Center, to.Center);
        var middle = _environment.Sample(midpoint2D).Position;
        var start = _environment.Sample(from.Center).Position;
        var end = _environment.Sample(to.Center).Position;
        var terrainPenalty = Math.Clamp(
            (_environment.Sample(start).TerrainRuggedness + _environment.Sample(middle).TerrainRuggedness + _environment.Sample(end).TerrainRuggedness) / 3d,
            0d,
            1d);
        var floodPenalty = _environment.Sample(middle).Hydrology.FloodRisk;
        var terrainAdaptation = Math.Clamp(1d - terrainPenalty * 0.72d - floodPenalty * 0.18d, 0d, 1d);
        var distance = Distance2D(start, end);
        var constructionCost = distance * (1d + (1d - terrainAdaptation) * 1.4d) * (kind == RegionalCorridorKind.Railway ? 1.8d : 1d);
        var id = new RegionalCorridorId(Hash(from.Id.Value, to.Id.Value, 0x5400UL + (ulong)kind + (ulong)ordinal));
        var fromName = FindToponymName(toponyms, from.NameId);
        var toName = FindToponymName(toponyms, to.NameId);
        var corridorName = CreateHumanToponym(
            HumanToponymKind.Road,
            kind == RegionalCorridorKind.Railway ? $"{fromName}–{toName} Line" : $"{fromName}–{toName} Road",
            null,
            FindNearestFeatureId(middle, features),
            null,
            Hash(id.Value, 0x5401UL, 0x5402UL));
        toponyms.Add(corridorName);
        return new RegionalCorridor(
            id,
            kind,
            from.Id,
            to.Id,
            new[] { start, middle, end },
            terrainAdaptation,
            constructionCost,
            corridorName.Id);
    }

    private List<District> GenerateDistricts(IReadOnlyList<Settlement> settlements, List<HumanToponym> toponyms)
    {
        var districts = new List<District>(settlements.Count * 3);
        foreach (var settlement in settlements)
        {
            var districtKinds = settlement.Population > 3_000
                ? new[] { DistrictKind.OldTown, DistrictKind.CentralBusiness, DistrictKind.Suburb }
                : new[] { DistrictKind.OldTown, DistrictKind.ResidentialQuarter };
            for (var index = 0; index < districtKinds.Length; index++)
            {
                var kind = districtKinds[index];
                var radius = Math.Clamp(settlement.InfluenceRadiusMeters * (0.12d + index * 0.06d), 300d, 2_400d);
                var angle = index * (Math.PI * 2d / districtKinds.Length);
                var cx = settlement.Center.X + Math.Cos(angle) * radius * 0.45d;
                var cy = settlement.Center.Y + Math.Sin(angle) * radius * 0.45d;
                var ground = _environment.Sample(new WorldPoint(cx, cy, 0d)).Position.Z;
                var id = new DistrictId(Hash(settlement.Id.Value, (ulong)index, 0x5501UL));
                var parentName = settlement.NameId;
                var name = CreateHumanToponym(
                    HumanToponymKind.District,
                    $"{FindToponymName(toponyms, parentName)} {GetDistrictSuffix(kind)}",
                    null,
                    null,
                    parentName,
                    Hash(id.Value, 0x5502UL, 0x5503UL));
                toponyms.Add(name);
                districts.Add(new District(
                    id,
                    settlement.Id,
                    kind,
                    new WorldVolume(cx - radius, cy - radius, ground - 5d, cx + radius, cy + radius, ground + 180d),
                    name.Id,
                    Math.Clamp(settlement.Suitability.TransportPotential * (1d - index * 0.08d), 0d, 1d)));
            }
        }
        return districts;
    }

    private List<Parcel> GenerateParcels(IReadOnlyList<Settlement> settlements, IReadOnlyList<District> districts)
    {
        var settlementById = settlements.ToDictionary(static item => item.Id);
        var parcels = new List<Parcel>(districts.Count * 4);
        foreach (var district in districts)
        {
            var settlement = settlementById[district.SettlementId];
            var midX = (district.Bounds.MinX + district.Bounds.MaxX) * 0.5d;
            var midY = (district.Bounds.MinY + district.Bounds.MaxY) * 0.5d;
            for (var index = 0; index < 4; index++)
            {
                var left = (index & 1) == 0;
                var lower = (index & 2) == 0;
                var minX = left ? district.Bounds.MinX : midX;
                var maxX = left ? midX : district.Bounds.MaxX;
                var minY = lower ? district.Bounds.MinY : midY;
                var maxY = lower ? midY : district.Bounds.MaxY;
                var center = _environment.Sample(new WorldPoint((minX + maxX) * 0.5d, (minY + maxY) * 0.5d, 0d));
                var zone = DetermineZone(district.Kind, index);
                var suitability = Math.Clamp(
                    settlement.Suitability.Buildability * 0.48d
                    + district.Accessibility * 0.30d
                    + (1d - center.Hydrology.FloodRisk) * 0.22d,
                    0d,
                    1d);
                var landValue = Math.Clamp((district.Accessibility * 0.58d) + (suitability * 0.42d), 0d, 1d);
                var id = new ParcelId(Hash(district.Id.Value, (ulong)index, 0x5601UL));
                var state = suitability > 0.47d && zone is not ZoneKind.OpenSpace and not ZoneKind.Agricultural
                    ? ParcelDevelopmentState.Occupied
                    : ParcelDevelopmentState.Vacant;
                parcels.Add(new Parcel(
                    id,
                    district.SettlementId,
                    district.Id,
                    new WorldVolume(minX, minY, center.Position.Z - 2d, maxX, maxY, center.Position.Z + 140d),
                    zone,
                    state,
                    suitability,
                    landValue,
                    null));
            }
        }
        return parcels;
    }

    private List<GeneratedBuilding> GenerateBuildings(IReadOnlyList<Parcel> parcels)
    {
        var buildings = new List<GeneratedBuilding>();
        foreach (var parcel in parcels)
        {
            if (parcel.DevelopmentState == ParcelDevelopmentState.Vacant) continue;
            var marginX = Math.Max(4d, parcel.Bounds.Width * 0.12d);
            var marginY = Math.Max(4d, parcel.Bounds.Depth * 0.12d);
            if (parcel.Bounds.Width <= marginX * 2d || parcel.Bounds.Depth <= marginY * 2d) continue;
            var use = MapBuildingUse(parcel.Zone);
            var floors = Math.Clamp(1 + (int)Math.Round(parcel.LandValue * 8d), 1, 12);
            var ground = parcel.Bounds.MinZ + 2d;
            var height = floors * 3.4d;
            var id = new GeneratedBuildingId(Hash(parcel.Id.Value, (ulong)floors, 0x5701UL));
            var capacity = Math.Max(4, (int)Math.Round((parcel.Bounds.Width * parcel.Bounds.Depth / 120d) * floors));
            buildings.Add(new GeneratedBuilding(
                id,
                parcel.Id,
                use,
                new WorldVolume(
                    parcel.Bounds.MinX + marginX,
                    parcel.Bounds.MinY + marginY,
                    ground,
                    parcel.Bounds.MaxX - marginX,
                    parcel.Bounds.MaxY - marginY,
                    ground + height),
                floors,
                capacity,
                parcel.LandValue > 0.72d ? 4 : 2));
        }
        return buildings;
    }

    private static List<Parcel> AttachBuildings(
        IReadOnlyList<Parcel> parcels,
        IReadOnlyList<GeneratedBuilding> buildings)
    {
        var byParcel = buildings.ToDictionary(static item => item.ParcelId, static item => item.Id);
        return parcels
            .Select(parcel => byParcel.TryGetValue(parcel.Id, out var buildingId) ? parcel with { BuildingId = buildingId } : parcel)
            .ToList();
    }

    private List<GeneratedPoi> GeneratePois(
        IReadOnlyList<Settlement> settlements,
        IReadOnlyList<District> districts,
        IReadOnlyList<GeneratedBuilding> buildings,
        List<HumanToponym> toponyms)
    {
        var result = new List<GeneratedPoi>();
        var parcelBuilding = buildings.ToDictionary(static item => item.ParcelId);
        foreach (var settlement in settlements)
        {
            var kind = settlement.Role switch
            {
                RegionalRole.Port => GeneratedPoiKind.Port,
                RegionalRole.TransportHub => GeneratedPoiKind.Station,
                RegionalRole.Industrial => GeneratedPoiKind.IndustrialHub,
                RegionalRole.Administrative => GeneratedPoiKind.CivicCenter,
                RegionalRole.Market => GeneratedPoiKind.Market,
                _ => GeneratedPoiKind.SettlementCenter,
            };
            var district = districts.First(item => item.SettlementId == settlement.Id);
            var candidateBuilding = parcelBuilding.Values.FirstOrDefault(building =>
                building.Bounds.MinX >= district.Bounds.MinX && building.Bounds.MaxX <= district.Bounds.MaxX
                && building.Bounds.MinY >= district.Bounds.MinY && building.Bounds.MaxY <= district.Bounds.MaxY);
            HumanToponymId? poiNameId = null;
            if (kind == GeneratedPoiKind.Station)
            {
                var stationName = CreateHumanToponym(
                    HumanToponymKind.Station,
                    $"{FindToponymName(toponyms, settlement.NameId)} Station",
                    null,
                    null,
                    settlement.NameId,
                    Hash(settlement.Id.Value, 0x5801UL, 0x5802UL));
                toponyms.Add(stationName);
                poiNameId = stationName.Id;
            }
            result.Add(new GeneratedPoi(
                new GeneratedPoiId(Hash(settlement.Id.Value, (ulong)kind, 0x5803UL)),
                settlement.Id,
                kind,
                settlement.Center,
                candidateBuilding?.Id,
                poiNameId));
        }
        return result;
    }

    private List<RoadSign> GenerateRoadSigns(
        IReadOnlyList<RegionalCorridor> corridors,
        IReadOnlyList<Settlement> settlements,
        IReadOnlyList<HumanToponym> toponyms,
        IReadOnlyList<GeographicFeature> features)
    {
        var settlementById = settlements.ToDictionary(static item => item.Id);
        var signs = new List<RoadSign>(corridors.Count * 2);
        foreach (var corridor in corridors.Where(static item => item.Kind != RegionalCorridorKind.Railway))
        {
            var destination = settlementById[corridor.ToSettlementId];
            var position = corridor.Geometry[corridor.Geometry.Count / 2];
            var sample = _environment.Sample(position);
            signs.Add(new RoadSign(
                new RoadSignId(Hash(corridor.Id.Value, 0x5901UL, 0x5902UL)),
                RoadSignKind.Direction,
                position,
                corridor.Id,
                destination.Id,
                null,
                $"{FindToponymName(toponyms, destination.NameId)} {Math.Max(1, (int)Math.Round(Distance2D(position, destination.Center) / 1_000d))} km"));

            var warning = DetermineWarningSign(sample, position, features);
            if (warning is { } warningValue)
            {
                signs.Add(new RoadSign(
                    new RoadSignId(Hash(corridor.Id.Value, (ulong)warningValue.Kind, 0x5903UL)),
                    warningValue.Kind,
                    position,
                    corridor.Id,
                    null,
                    warningValue.FeatureId,
                    warningValue.Text));
            }
        }
        return signs;
    }

    private static (RoadSignKind Kind, GeographicFeatureId? FeatureId, string Text)? DetermineWarningSign(
        RegionalEnvironmentSample sample,
        WorldPoint position,
        IReadOnlyList<GeographicFeature> features)
    {
        var nearestFeature = features.OrderBy(feature => Distance2D(position, Center(feature.Bounds))).FirstOrDefault();
        if (sample.Hydrology.FloodRisk > 0.68d) return (RoadSignKind.FloodWarning, nearestFeature?.Id, "Flood-prone area");
        if (sample.TerrainRuggedness > 0.72d) return (RoadSignKind.SteepGrade, nearestFeature?.Id, "Steep grade");
        if (nearestFeature?.Type == GeographicFeatureType.Pass) return (RoadSignKind.MountainPass, nearestFeature.Id, "Mountain pass");
        if (nearestFeature?.Type is GeographicFeatureType.River or GeographicFeatureType.Tributary) return (RoadSignKind.RiverCrossing, nearestFeature.Id, "River crossing");
        if (sample.CoastlineDistanceMeters < 8_000d) return (RoadSignKind.CoastalLowland, nearestFeature?.Id, "Coastal lowland");
        return null;
    }

    private RegionalQualityReport EvaluateQuality(
        IReadOnlyList<Settlement> settlements,
        IReadOnlyList<RegionalCorridor> corridors,
        IReadOnlyList<Parcel> parcels,
        IReadOnlyList<GeneratedBuilding> buildings)
    {
        var terrainAdaptation = corridors.Count == 0 ? 0d : corridors.Average(static item => item.TerrainAdaptation);
        var connected = CountReachableSettlements(settlements, corridors);
        var roadConnectivity = settlements.Count <= 1 ? 1d : connected / (double)settlements.Count;
        var averageSlopeCost = Math.Clamp(1d - terrainAdaptation, 0d, 1d);
        var degree = settlements.ToDictionary(static item => item.Id, static _ => 0);
        foreach (var corridor in corridors.Where(static item => item.Kind != RegionalCorridorKind.Railway))
        {
            degree[corridor.FromSettlementId]++;
            degree[corridor.ToSettlementId]++;
        }
        var accessibility = degree.Count == 0 ? 0d : Math.Clamp(degree.Values.Average() / 3d, 0d, 1d);
        var totalPopulation = settlements.Sum(static item => (long)item.Population);
        var capacityProxy = Math.Max(1d, corridors.Count(static item => item.Kind != RegionalCorridorKind.Railway) * 8_000d);
        var congestionRisk = Math.Clamp(totalPopulation / capacityProxy, 0d, 1d);
        var landUseConsistency = parcels.Count == 0 ? 0d : parcels.Average(static item => item.DevelopmentSuitability);
        var floodExposure = settlements.Count == 0 ? 0d : settlements.Average(static item => item.Suitability.FloodRisk);
        var occupiedParcels = parcels.Count(static item => item.BuildingId is not null);
        var urbanCompactness = parcels.Count == 0 ? 0d : occupiedParcels / (double)parcels.Count;
        var polycentricBalance = CalculatePolycentricBalance(settlements);
        return new RegionalQualityReport(
            terrainAdaptation,
            roadConnectivity,
            averageSlopeCost,
            accessibility,
            congestionRisk,
            landUseConsistency,
            floodExposure,
            urbanCompactness,
            polycentricBalance);
    }

    private bool TryAddSupplementalCorridor(
        IReadOnlyList<Settlement> settlements,
        List<RegionalCorridor> corridors,
        List<HumanToponym> toponyms,
        IReadOnlyList<GeographicFeature> features,
        int iteration)
    {
        if (settlements.Count < 3) return false;
        Settlement? bestA = null;
        Settlement? bestB = null;
        var bestScore = double.NegativeInfinity;
        for (var i = 0; i < settlements.Count; i++)
        {
            for (var j = i + 1; j < settlements.Count; j++)
            {
                var a = settlements[i];
                var b = settlements[j];
                if (HasPair(corridors, a.Id, b.Id)) continue;
                var distance = Distance2D(a.Center, b.Center);
                var demand = a.Population + b.Population + a.Jobs + b.Jobs;
                var score = demand / Math.Max(1d, distance);
                if (score <= bestScore) continue;
                bestScore = score;
                bestA = a;
                bestB = b;
            }
        }
        if (bestA is null || bestB is null) return false;
        corridors.Add(CreateCorridor(
            bestA,
            bestB,
            RegionalCorridorKind.RegionalRoad,
            10_000 + iteration + corridors.Count,
            toponyms,
            features));
        return true;
    }

    private static int CountReachableSettlements(
        IReadOnlyList<Settlement> settlements,
        IReadOnlyList<RegionalCorridor> corridors)
    {
        if (settlements.Count == 0) return 0;
        var adjacency = settlements.ToDictionary(static item => item.Id, static _ => new List<SettlementId>());
        foreach (var corridor in corridors)
        {
            adjacency[corridor.FromSettlementId].Add(corridor.ToSettlementId);
            adjacency[corridor.ToSettlementId].Add(corridor.FromSettlementId);
        }
        var visited = new HashSet<SettlementId>();
        var queue = new Queue<SettlementId>();
        queue.Enqueue(settlements[0].Id);
        visited.Add(settlements[0].Id);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var next in adjacency[current])
            {
                if (visited.Add(next)) queue.Enqueue(next);
            }
        }
        return visited.Count;
    }

    private static double CalculatePolycentricBalance(IReadOnlyList<Settlement> settlements)
    {
        if (settlements.Count <= 1) return 1d;
        var total = settlements.Sum(static item => (double)item.Population);
        if (total <= 0d) return 0d;
        var largestShare = settlements.Max(static item => item.Population) / total;
        var equalShare = 1d / settlements.Count;
        return Math.Clamp((1d - largestShare) / (1d - equalShare), 0d, 1d);
    }

    private NaturalToponym? FindNearestNaturalToponym(
        WorldPoint point,
        IReadOnlyList<GeographicFeature> features,
        IReadOnlyDictionary<GeographicFeatureId, NaturalToponym> naturalToponyms)
    {
        GeographicFeature? nearest = null;
        var best = double.PositiveInfinity;
        foreach (var feature in features)
        {
            var distance = Distance2D(point, Center(feature.Bounds));
            if (distance >= best) continue;
            best = distance;
            nearest = feature;
        }
        return nearest is not null && naturalToponyms.TryGetValue(nearest.Id, out var toponym) ? toponym : null;
    }

    private static GeographicFeatureId? FindNearestFeatureId(WorldPoint point, IReadOnlyList<GeographicFeature> features)
    {
        if (features.Count == 0) return null;
        return features.OrderBy(feature => Distance2D(point, Center(feature.Bounds))).First().Id;
    }

    private HumanToponym CreateHumanToponym(
        HumanToponymKind kind,
        string name,
        NaturalToponym? sourceNaturalToponym,
        GeographicFeatureId? sourceFeatureId,
        HumanToponymId? parentHumanToponymId,
        ulong hash)
    {
        return new HumanToponym(
            new HumanToponymId(EnsureNonZero(hash)),
            kind,
            name,
            new HumanToponymProvenance(
                sourceNaturalToponym,
                sourceFeatureId,
                parentHumanToponymId,
                "phase30-regional-v1"));
    }

    private static string CreateSettlementName(string? naturalName, SettlementEnvironmentKind environment, int index)
    {
        if (!string.IsNullOrWhiteSpace(naturalName))
        {
            var stem = naturalName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
            return (index % 3) switch
            {
                0 => stem,
                1 => stem + "ton",
                _ => stem + "stead",
            };
        }
        return environment + " Settlement " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetDistrictSuffix(DistrictKind kind) => kind switch
    {
        DistrictKind.OldTown => "Old Town",
        DistrictKind.CentralBusiness => "Central",
        DistrictKind.StationDistrict => "Station District",
        DistrictKind.IndustrialArea => "Industrial District",
        DistrictKind.Suburb => "Suburb",
        DistrictKind.ResidentialQuarter => "Quarter",
        _ => "District",
    };

    private static ZoneKind DetermineZone(DistrictKind kind, int index) => kind switch
    {
        DistrictKind.CentralBusiness => index == 0 ? ZoneKind.Commercial : ZoneKind.MixedUse,
        DistrictKind.IndustrialArea => ZoneKind.Industrial,
        DistrictKind.Suburb => index == 3 ? ZoneKind.OpenSpace : ZoneKind.Residential,
        DistrictKind.StationDistrict => index <= 1 ? ZoneKind.MixedUse : ZoneKind.Commercial,
        DistrictKind.OldTown => index == 0 ? ZoneKind.Civic : ZoneKind.MixedUse,
        _ => index == 3 ? ZoneKind.OpenSpace : ZoneKind.Residential,
    };

    private static GeneratedBuildingUse MapBuildingUse(ZoneKind zone) => zone switch
    {
        ZoneKind.Commercial => GeneratedBuildingUse.Commercial,
        ZoneKind.Industrial => GeneratedBuildingUse.Industrial,
        ZoneKind.MixedUse => GeneratedBuildingUse.MixedUse,
        ZoneKind.Civic => GeneratedBuildingUse.Civic,
        _ => GeneratedBuildingUse.Residential,
    };

    private static bool HasPair(IReadOnlyList<RegionalCorridor> corridors, SettlementId a, SettlementId b) =>
        corridors.Any(corridor =>
            (corridor.FromSettlementId == a && corridor.ToSettlementId == b)
            || (corridor.FromSettlementId == b && corridor.ToSettlementId == a));

    private static string FindToponymName(IReadOnlyList<HumanToponym> toponyms, HumanToponymId id) =>
        toponyms.First(item => item.Id == id).Name;

    private static double GetTargetQuality(RegionalGenerationQualityPreset preset) => preset switch
    {
        RegionalGenerationQualityPreset.Draft => 0.48d,
        RegionalGenerationQualityPreset.Standard => 0.56d,
        RegionalGenerationQualityPreset.HighQuality => 0.62d,
        _ => 0.56d,
    };

    private static WorldPoint Midpoint(WorldPoint a, WorldPoint b) =>
        new((a.X + b.X) * 0.5d, (a.Y + b.Y) * 0.5d, 0d);

    private static WorldPoint Center(WorldVolume volume) =>
        new((volume.MinX + volume.MaxX) * 0.5d, (volume.MinY + volume.MaxY) * 0.5d, (volume.MinZ + volume.MaxZ) * 0.5d);

    private static double Distance2D(WorldPoint a, WorldPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private ulong Hash(double x, double y, ulong salt)
    {
        var gx = checked((long)Math.Floor(x / Math.Max(1d, _environment.Config.TerrainDetailScaleMeters)));
        var gy = checked((long)Math.Floor(y / Math.Max(1d, _environment.Config.TerrainDetailScaleMeters)));
        return EnsureNonZero(Hash64(unchecked((ulong)gx) ^ RotateLeft(unchecked((ulong)gy), 29) ^ salt ^ _worldSeed));
    }

    private ulong Hash(ulong a, ulong b, ulong salt) => EnsureNonZero(Hash64(a ^ RotateLeft(b, 23) ^ salt ^ _worldSeed));

    private double DeterministicUnit(double x, double y, ulong salt) =>
        (Hash(x, y, salt) >> 11) * (1d / 9_007_199_254_740_992d);

    private static ulong Hash64(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    private static ulong EnsureNonZero(ulong value) => value == 0UL ? 1UL : value;
}
