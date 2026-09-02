namespace MachiVerseWorks.Simulation;

public sealed record RegionalParcelSuitabilityFactors(
    double RoadAccess,
    double ParcelSize,
    double SlopeSafety,
    double FloodSafety,
    double LandValueFit,
    double UseFit,
    double TotalScore,
    double LandValue);

public sealed class RegionalParcelSuitabilityEvaluator
{
    private readonly WorldEnvironmentGenerator _environment;

    public RegionalParcelSuitabilityEvaluator(WorldEnvironmentGenerator environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    public RegionalParcelSuitabilityFactors Evaluate(
        WorldVolume parcelBounds,
        District district,
        Settlement settlement,
        ZoneKind zone,
        IReadOnlyList<RegionalCorridor> corridors)
    {
        ArgumentNullException.ThrowIfNull(district);
        ArgumentNullException.ThrowIfNull(settlement);
        ArgumentNullException.ThrowIfNull(corridors);
        if (!Enum.IsDefined(zone)) throw new ArgumentOutOfRangeException(nameof(zone));

        var center = Center(parcelBounds);
        var environment = _environment.Sample(center);
        var nearestRoadDistance = corridors
            .Where(static corridor => corridor.Kind != RegionalCorridorKind.Railway && corridor.Geometry.Count >= 2)
            .Select(corridor => DistanceToPolyline(center, corridor.Geometry))
            .DefaultIfEmpty(12_000d)
            .Min();
        var roadAccess = Math.Clamp(1d - (nearestRoadDistance / 8_000d), 0d, 1d);
        var area = Math.Max(1d, parcelBounds.Width * parcelBounds.Depth);
        var preferredArea = zone switch
        {
            ZoneKind.Industrial => 180_000d,
            ZoneKind.Commercial => 75_000d,
            ZoneKind.Civic => 90_000d,
            ZoneKind.MixedUse => 65_000d,
            ZoneKind.Agricultural => 320_000d,
            ZoneKind.OpenSpace => 220_000d,
            _ => 55_000d,
        };
        var parcelSize = Math.Clamp(1d - (Math.Abs(area - preferredArea) / Math.Max(preferredArea, area)), 0d, 1d);
        var slopeSafety = Math.Clamp(1d - environment.TerrainRuggedness, 0d, 1d);
        var floodSafety = Math.Clamp(1d - environment.Hydrology.FloodRisk, 0d, 1d);
        var useFit = CalculateUseFit(zone, district.Kind, settlement.Role);
        var landValue = Math.Clamp(
            district.Accessibility * 0.35d
            + roadAccess * 0.25d
            + settlement.Suitability.TotalScore * 0.20d
            + slopeSafety * 0.10d
            + floodSafety * 0.10d,
            0d,
            1d);
        var landValueFit = zone switch
        {
            ZoneKind.Commercial or ZoneKind.MixedUse or ZoneKind.Civic => landValue,
            ZoneKind.Industrial => Math.Clamp(0.55d + roadAccess * 0.30d - landValue * 0.10d, 0d, 1d),
            ZoneKind.Agricultural or ZoneKind.OpenSpace => Math.Clamp(1d - landValue * 0.45d, 0d, 1d),
            _ => Math.Clamp(0.45d + landValue * 0.55d, 0d, 1d),
        };
        var total = Math.Clamp(
            roadAccess * 0.22d
            + parcelSize * 0.12d
            + slopeSafety * 0.18d
            + floodSafety * 0.18d
            + landValueFit * 0.15d
            + useFit * 0.15d,
            0d,
            1d);
        return new RegionalParcelSuitabilityFactors(
            roadAccess,
            parcelSize,
            slopeSafety,
            floodSafety,
            landValueFit,
            useFit,
            total,
            landValue);
    }

    private static double CalculateUseFit(ZoneKind zone, DistrictKind district, RegionalRole role)
    {
        if (zone == ZoneKind.Industrial)
            return role is RegionalRole.Industrial or RegionalRole.Resource or RegionalRole.Port ? 1d : 0.58d;
        if (zone is ZoneKind.Commercial or ZoneKind.MixedUse)
            return district is DistrictKind.CentralBusiness or DistrictKind.StationDistrict or DistrictKind.OldTown ? 1d : 0.72d;
        if (zone == ZoneKind.Residential)
            return district is DistrictKind.Suburb or DistrictKind.ResidentialQuarter ? 1d : 0.76d;
        if (zone == ZoneKind.Civic)
            return role == RegionalRole.Administrative || district == DistrictKind.OldTown ? 1d : 0.68d;
        return 0.82d;
    }

    private static double DistanceToPolyline(WorldPoint point, IReadOnlyList<WorldPoint> geometry)
    {
        var best = double.PositiveInfinity;
        for (var index = 1; index < geometry.Count; index++)
            best = Math.Min(best, DistanceToSegment(point, geometry[index - 1], geometry[index]));
        return best;
    }

    private static double DistanceToSegment(WorldPoint point, WorldPoint start, WorldPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 1e-12) return Distance2D(point, start);
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0d, 1d);
        return Distance2D(point, new WorldPoint(start.X + dx * t, start.Y + dy * t, point.Z));
    }

    private static WorldPoint Center(WorldVolume volume) => new(
        (volume.MinX + volume.MaxX) * 0.5d,
        (volume.MinY + volume.MaxY) * 0.5d,
        (volume.MinZ + volume.MaxZ) * 0.5d);

    private static double Distance2D(WorldPoint first, WorldPoint second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }
}

public sealed class RegionalGenerationEnricher
{
    private readonly WorldEnvironmentGenerator _environment;
    private readonly RegionalParcelSuitabilityEvaluator _parcelSuitability;
    private readonly RegionalRoadContextAnalyzer _roadContext;

    public RegionalGenerationEnricher(WorldEnvironmentGenerator environment)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _parcelSuitability = new RegionalParcelSuitabilityEvaluator(environment);
        _roadContext = new RegionalRoadContextAnalyzer(environment);
    }

    public RegionalGenerationSnapshot Enrich(RegionalGenerationSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var districts = source.Districts.ToList();
        var parcels = source.Parcels.ToList();
        var buildings = source.Buildings.ToList();
        var toponyms = source.Toponyms.ToList();
        var roadSigns = source.RoadSigns.ToList();
        var features = _environment.DetectGeographicFeatures(source.Volume, 128);

        AddRoleDistricts(source, districts, parcels, buildings, toponyms);
        AddRoadContextArtifacts(source, features, toponyms, roadSigns);

        var landUseConsistency = parcels.Count == 0 ? 0d : parcels.Average(static parcel => parcel.DevelopmentSuitability);
        var developedCount = parcels.Count(static parcel => parcel.DevelopmentState is ParcelDevelopmentState.Occupied or ParcelDevelopmentState.Redeveloping);
        var urbanCompactness = parcels.Count == 0 ? 0d : developedCount / (double)parcels.Count;
        var quality = source.Quality with
        {
            LandUseConsistency = landUseConsistency,
            UrbanCompactness = urbanCompactness,
        };

        return source with
        {
            Districts = districts.OrderBy(static item => item.Id.Value).ToArray(),
            Parcels = parcels.OrderBy(static item => item.Id.Value).ToArray(),
            Buildings = buildings.OrderBy(static item => item.Id.Value).ToArray(),
            Toponyms = toponyms
                .GroupBy(static item => item.Id)
                .Select(static group => group.First())
                .OrderBy(static item => item.Id.Value)
                .ToArray(),
            RoadSigns = roadSigns
                .GroupBy(static item => item.Id)
                .Select(static group => group.First())
                .OrderBy(static item => item.Id.Value)
                .ToArray(),
            Quality = quality,
        };
    }

    private void AddRoleDistricts(
        RegionalGenerationSnapshot source,
        List<District> districts,
        List<Parcel> parcels,
        List<GeneratedBuilding> buildings,
        List<HumanToponym> toponyms)
    {
        var settlementName = source.Settlements.ToDictionary(
            static settlement => settlement.Id,
            settlement => toponyms.First(item => item.Id == settlement.NameId));
        foreach (var settlement in source.Settlements.OrderBy(static item => item.Id.Value))
        {
            var desired = GetDesiredRoleDistricts(settlement);
            foreach (var kind in desired)
            {
                if (districts.Any(item => item.SettlementId == settlement.Id && item.Kind == kind)) continue;
                var ordinal = (int)kind + 7;
                var radius = Math.Clamp(settlement.InfluenceRadiusMeters * 0.18d, 420d, 2_800d);
                var angle = ToUnit(Hash64(settlement.Id.Value ^ ((ulong)kind << 32) ^ source.WorldSeed)) * Math.PI * 2d;
                var distance = radius * 0.72d;
                var cx = settlement.Center.X + Math.Cos(angle) * distance;
                var cy = settlement.Center.Y + Math.Sin(angle) * distance;
                var ground = _environment.Sample(new WorldPoint(cx, cy, 0d)).Position.Z;
                var districtId = new DistrictId(EnsureNonZero(Hash64(settlement.Id.Value ^ ((ulong)kind << 40) ^ 0xD157A1C7UL ^ source.WorldSeed)));
                var parent = settlementName[settlement.Id];
                var name = new HumanToponym(
                    new HumanToponymId(EnsureNonZero(Hash64(districtId.Value ^ 0x70F0A11UL ^ source.WorldSeed))),
                    HumanToponymKind.District,
                    $"{parent.Name} {GetDistrictSuffix(kind)}",
                    new HumanToponymProvenance(
                        parent.Provenance.SourceNaturalToponym,
                        parent.Provenance.SourceFeatureId,
                        parent.Id,
                        "phase30-district-enrichment-v1"));
                toponyms.Add(name);
                var district = new District(
                    districtId,
                    settlement.Id,
                    kind,
                    new WorldVolume(cx - radius, cy - radius, ground - 5d, cx + radius, cy + radius, ground + 180d),
                    name.Id,
                    Math.Clamp(settlement.Suitability.TransportPotential * 0.72d + settlement.Suitability.TotalScore * 0.28d, 0d, 1d));
                districts.Add(district);
                AddDistrictParcelsAndBuildings(source, settlement, district, ordinal, parcels, buildings);
            }
        }
    }

    private void AddDistrictParcelsAndBuildings(
        RegionalGenerationSnapshot source,
        Settlement settlement,
        District district,
        int ordinal,
        List<Parcel> parcels,
        List<GeneratedBuilding> buildings)
    {
        var subdivision = RegionalBlockSubdivision.Subdivide(district, source.Corridors);
        for (var index = 0; index < subdivision.Blocks.Count; index++)
        {
            var block = subdivision.Blocks[index];
            var zone = DetermineZone(district.Kind, index);
            var factors = _parcelSuitability.Evaluate(block, district, settlement, zone, source.Corridors);
            var state = DetermineDevelopmentState(district.Kind, zone, factors, settlement, index);
            var parcelId = new ParcelId(EnsureNonZero(Hash64(
                district.Id.Value
                ^ ((ulong)(ordinal + 1) << 28)
                ^ ((ulong)(index + 1) << 44)
                ^ source.WorldSeed)));
            GeneratedBuildingId? buildingId = null;
            if (state is ParcelDevelopmentState.Occupied or ParcelDevelopmentState.Redeveloping)
            {
                buildingId = new GeneratedBuildingId(EnsureNonZero(Hash64(parcelId.Value ^ 0xB011D1A6UL ^ source.WorldSeed)));
            }
            var parcel = new Parcel(
                parcelId,
                settlement.Id,
                district.Id,
                block,
                zone,
                state,
                factors.TotalScore,
                factors.LandValue,
                buildingId);
            parcels.Add(parcel);
            if (buildingId is { } id)
                buildings.Add(CreateBuilding(parcel, id, state));
        }
    }

    private static GeneratedBuilding CreateBuilding(
        Parcel parcel,
        GeneratedBuildingId id,
        ParcelDevelopmentState state)
    {
        var marginX = Math.Max(4d, parcel.Bounds.Width * 0.12d);
        var marginY = Math.Max(4d, parcel.Bounds.Depth * 0.12d);
        var usableWidth = Math.Max(8d, parcel.Bounds.Width - marginX * 2d);
        var usableDepth = Math.Max(8d, parcel.Bounds.Depth - marginY * 2d);
        var minX = parcel.Bounds.MinX + Math.Min(marginX, Math.Max(0d, (parcel.Bounds.Width - usableWidth) * 0.5d));
        var minY = parcel.Bounds.MinY + Math.Min(marginY, Math.Max(0d, (parcel.Bounds.Depth - usableDepth) * 0.5d));
        var maxX = Math.Min(parcel.Bounds.MaxX, minX + usableWidth);
        var maxY = Math.Min(parcel.Bounds.MaxY, minY + usableDepth);
        var floors = Math.Clamp(1 + (int)Math.Round(parcel.LandValue * 9d), 1, 14);
        var ground = parcel.Bounds.MinZ + 2d;
        var historicalStage = state == ParcelDevelopmentState.Redeveloping ? 4 : parcel.LandValue > 0.72d ? 3 : 2;
        var capacity = Math.Max(4, (int)Math.Round((usableWidth * usableDepth / 120d) * floors));
        return new GeneratedBuilding(
            id,
            parcel.Id,
            MapBuildingUse(parcel.Zone),
            new WorldVolume(minX, minY, ground, maxX, maxY, ground + floors * 3.4d),
            floors,
            capacity,
            historicalStage);
    }

    private void AddRoadContextArtifacts(
        RegionalGenerationSnapshot source,
        IReadOnlyList<GeographicFeature> features,
        List<HumanToponym> toponyms,
        List<RoadSign> roadSigns)
    {
        var settlementById = source.Settlements.ToDictionary(static item => item.Id);
        foreach (var corridor in source.Corridors
                     .Where(static item => item.Kind != RegionalCorridorKind.Railway)
                     .OrderBy(static item => item.Id.Value))
        {
            var context = _roadContext.Analyze(corridor, features);
            if (context.CrossesWater)
                AddUniqueToponym(toponyms, RegionalStructureNaming.CreateBridgeName(corridor, context, toponyms));
            if (context.RequiresTunnel)
                AddUniqueToponym(toponyms, RegionalStructureNaming.CreateTunnelName(corridor, context, toponyms));

            var requiredKinds = RegionalRoadSignRule.DetermineRequiredSigns(context)
                .Append(RoadSignKind.PlaceName)
                .Distinct()
                .ToArray();
            foreach (var kind in requiredKinds)
            {
                if (roadSigns.Any(item => item.CorridorId == corridor.Id && item.Kind == kind)) continue;
                var position = corridor.Geometry[corridor.Geometry.Count / 2];
                var destination = settlementById[context.DestinationSettlementId];
                var destinationName = toponyms.First(item => item.Id == destination.NameId).Name;
                var featureId = kind is RoadSignKind.Direction or RoadSignKind.PlaceName ? null : context.FeatureId;
                roadSigns.Add(new RoadSign(
                    new RoadSignId(EnsureNonZero(Hash64(corridor.Id.Value ^ ((ulong)kind << 48) ^ 0x516E5A17UL ^ source.WorldSeed))),
                    kind,
                    position,
                    corridor.Id,
                    kind is RoadSignKind.Direction or RoadSignKind.PlaceName ? destination.Id : null,
                    featureId,
                    CreateSignText(kind, destinationName, Distance2D(position, destination.Center))));
            }
        }
    }

    private static ParcelDevelopmentState DetermineDevelopmentState(
        DistrictKind districtKind,
        ZoneKind zone,
        RegionalParcelSuitabilityFactors factors,
        Settlement settlement,
        int ordinal)
    {
        if (zone is ZoneKind.OpenSpace or ZoneKind.Agricultural) return ParcelDevelopmentState.Vacant;
        var demand = Math.Clamp((settlement.Population / 7_500d) * 0.58d + (settlement.Jobs / 4_000d) * 0.42d, 0d, 1d);
        if (districtKind == DistrictKind.OldTown && factors.LandValue >= 0.74d && ordinal == 0)
            return ParcelDevelopmentState.Redeveloping;
        if (factors.TotalScore >= 0.62d && demand >= 0.30d)
            return ParcelDevelopmentState.Occupied;
        var probe = new Parcel(
            new ParcelId(1),
            settlement.Id,
            new DistrictId(1),
            new WorldVolume(0d, 0d, 0d, 1d, 1d, 1d),
            zone,
            ParcelDevelopmentState.Vacant,
            factors.TotalScore,
            factors.LandValue,
            null);
        var decision = RegionalDevelopmentRule.Evaluate(probe, demand, 0);
        return decision.Build ? ParcelDevelopmentState.Developing : ParcelDevelopmentState.Vacant;
    }

    private static DistrictKind[] GetDesiredRoleDistricts(Settlement settlement)
    {
        var result = new List<DistrictKind>();
        if (settlement.Role is RegionalRole.TransportHub or RegionalRole.Port)
            result.Add(DistrictKind.StationDistrict);
        if (settlement.Role is RegionalRole.Industrial or RegionalRole.Resource)
            result.Add(DistrictKind.IndustrialArea);
        if (settlement.Population >= 2_500)
            result.Add(DistrictKind.Suburb);
        if (settlement.Population >= 4_000 || settlement.Role == RegionalRole.Administrative)
            result.Add(DistrictKind.CentralBusiness);
        return result.Distinct().ToArray();
    }

    private static ZoneKind DetermineZone(DistrictKind kind, int index) => kind switch
    {
        DistrictKind.CentralBusiness => index == 0 ? ZoneKind.Commercial : ZoneKind.MixedUse,
        DistrictKind.IndustrialArea => index == 3 ? ZoneKind.OpenSpace : ZoneKind.Industrial,
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

    private static string GetDistrictSuffix(DistrictKind kind) => kind switch
    {
        DistrictKind.CentralBusiness => "Central",
        DistrictKind.StationDistrict => "Station District",
        DistrictKind.IndustrialArea => "Industrial District",
        DistrictKind.Suburb => "Suburb",
        DistrictKind.ResidentialQuarter => "Quarter",
        _ => "Old Town",
    };

    private static string CreateSignText(RoadSignKind kind, string destinationName, double distanceMeters) => kind switch
    {
        RoadSignKind.Direction => $"{destinationName} {Math.Max(1, (int)Math.Round(distanceMeters / 1_000d))} km",
        RoadSignKind.PlaceName => destinationName,
        RoadSignKind.SteepGrade => "Steep grade",
        RoadSignKind.SharpCurve => "Sharp curve",
        RoadSignKind.FloodWarning => "Flood-prone area",
        RoadSignKind.RiverCrossing => "River crossing",
        RoadSignKind.MountainPass => "Mountain pass",
        RoadSignKind.Tunnel => "Tunnel ahead",
        RoadSignKind.CoastalLowland => "Coastal lowland",
        _ => destinationName,
    };

    private static void AddUniqueToponym(List<HumanToponym> toponyms, HumanToponym toponym)
    {
        if (toponyms.All(item => item.Id != toponym.Id)) toponyms.Add(toponym);
    }

    private static double Distance2D(WorldPoint first, WorldPoint second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }

    private static ulong Hash64(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong EnsureNonZero(ulong value) => value == 0UL ? 1UL : value;
    private static double ToUnit(ulong value) => (value >> 11) * (1d / 9_007_199_254_740_992d);
}
