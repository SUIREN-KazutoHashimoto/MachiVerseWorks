namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private const int MaximumRegionalMaterializedPopulation = 1_000_000;
    private const int MaximumRegionalMaterializedJobs = 1_000_000;
    private const int MaximumRegionalToponymLength = 160;

    private RegionalGenerator? _regionalGenerator;
    private RegionalGenerationSnapshot? _regionalGeneration;

    public bool HasRegionalGeneration => _regionalGeneration is not null;

    public RegionalGenerationSnapshot GenerateRegionalGeneration(
        WorldVolume volume,
        RegionalGenerationOptions? options = null)
    {
        if (_regionalGeneration is not null)
            throw new InvalidOperationException("Regional generation has already been initialized for this world.");

        var generated = RegionalGenerator.Generate(volume, options, Time.TickCount);
        generated = new RegionalGenerationEnricher(EnvironmentGenerator).Enrich(generated);
        _regionalGeneration = DetachRegionalSnapshot(generated);
        return DetachRegionalSnapshot(_regionalGeneration);
    }

    public RegionalGenerationSnapshot CreateRegionalGenerationSnapshot()
    {
        if (_regionalGeneration is null)
            throw new InvalidOperationException("Regional generation has not been initialized for this world.");
        return DetachRegionalSnapshot(_regionalGeneration);
    }

    public bool TryCreateRegionalGenerationSnapshot(out RegionalGenerationSnapshot? snapshot)
    {
        if (_regionalGeneration is null)
        {
            snapshot = null;
            return false;
        }
        snapshot = DetachRegionalSnapshot(_regionalGeneration);
        return true;
    }

    private RegionalGenerator RegionalGenerator =>
        _regionalGenerator ??= new RegionalGenerator(EnvironmentGenerator);

    private RegionalGenerationCheckpoint? CreateRegionalGenerationCheckpoint() =>
        _regionalGeneration is null ? null : new RegionalGenerationCheckpoint(DetachRegionalSnapshot(_regionalGeneration));

    private void RestoreRegionalGeneration(RegionalGenerationCheckpoint? checkpoint)
    {
        _regionalGeneration = checkpoint is null ? null : DetachRegionalSnapshot(checkpoint.Snapshot);
        _regionalGenerator = new RegionalGenerator(EnvironmentGenerator);
    }

    private static RegionalGenerationSnapshot DetachRegionalSnapshot(RegionalGenerationSnapshot source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source with
        {
            Settlements = source.Settlements.ToArray(),
            GrowthEvents = source.GrowthEvents.ToArray(),
            Corridors = source.Corridors
                .Select(static item => item with { Geometry = item.Geometry.ToArray() })
                .ToArray(),
            Districts = source.Districts.ToArray(),
            Parcels = source.Parcels.ToArray(),
            Buildings = source.Buildings.ToArray(),
            Pois = source.Pois.ToArray(),
            Toponyms = source.Toponyms
                .Select(static item => item with
                {
                    Provenance = item.Provenance with
                    {
                        SourceNaturalToponym = item.Provenance.SourceNaturalToponym is null
                            ? null
                            : item.Provenance.SourceNaturalToponym with
                            {
                                Provenance = item.Provenance.SourceNaturalToponym.Provenance with { },
                            },
                    },
                })
                .ToArray(),
            RoadSigns = source.RoadSigns.ToArray(),
            Quality = source.Quality with { },
        };
    }

    private static void ValidateRegionalGenerationCheckpoint(SimulationCheckpoint checkpoint)
    {
        var regional = checkpoint.Economy?.RegionalGeneration;
        if (regional is null) return;
        ArgumentNullException.ThrowIfNull(regional.Snapshot);
        var snapshot = regional.Snapshot;
        if (!Enum.IsDefined(snapshot.Preset)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
        if (snapshot.WorldSeed == 0UL) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional world seed must be greater than zero.");
        var expectedSeed = checkpoint.Economy?.WorldEnvironment?.Config.WorldSeed ?? checkpoint.Seed;
        if (snapshot.WorldSeed != expectedSeed)
            throw new ArgumentException("Regional generation seed does not match the authoritative world seed.", nameof(checkpoint));
        if (snapshot.Iterations < 0 || snapshot.Iterations > 32)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional generation iteration count is invalid.");
        ValidateRegionalVolume(snapshot.Volume, "Regional generation volume", nameof(checkpoint));

        ArgumentNullException.ThrowIfNull(snapshot.Settlements);
        ArgumentNullException.ThrowIfNull(snapshot.GrowthEvents);
        ArgumentNullException.ThrowIfNull(snapshot.Corridors);
        ArgumentNullException.ThrowIfNull(snapshot.Districts);
        ArgumentNullException.ThrowIfNull(snapshot.Parcels);
        ArgumentNullException.ThrowIfNull(snapshot.Buildings);
        ArgumentNullException.ThrowIfNull(snapshot.Pois);
        ArgumentNullException.ThrowIfNull(snapshot.Toponyms);
        ArgumentNullException.ThrowIfNull(snapshot.RoadSigns);
        ArgumentNullException.ThrowIfNull(snapshot.Quality);

        if (snapshot.Settlements.Count > 64
            || snapshot.GrowthEvents.Count > 1_024
            || snapshot.Corridors.Count > 512
            || snapshot.Districts.Count > 512
            || snapshot.Parcels.Count > 4_096
            || snapshot.Buildings.Count > 4_096
            || snapshot.Pois.Count > 1_024
            || snapshot.Toponyms.Count > 4_096
            || snapshot.RoadSigns.Count > 4_096)
        {
            throw new ArgumentException("Regional generation checkpoint exceeds bounded collection limits.", nameof(checkpoint));
        }

        var toponymIds = ValidateRegionalIds(snapshot.Toponyms.Select(static item => item.Id.Value), "Human toponym");
        var settlementIds = ValidateRegionalIds(snapshot.Settlements.Select(static item => item.Id.Value), "Settlement");
        var growthEventIds = ValidateRegionalIds(snapshot.GrowthEvents.Select(static item => item.Id.Value), "Historical growth event");
        var corridorIds = ValidateRegionalIds(snapshot.Corridors.Select(static item => item.Id.Value), "Regional corridor");
        _ = ValidateRegionalIds(snapshot.Districts.Select(static item => item.Id.Value), "District");
        _ = ValidateRegionalIds(snapshot.Parcels.Select(static item => item.Id.Value), "Parcel");
        _ = ValidateRegionalIds(snapshot.Buildings.Select(static item => item.Id.Value), "Generated building");
        _ = ValidateRegionalIds(snapshot.Pois.Select(static item => item.Id.Value), "Generated POI");
        var districtById = snapshot.Districts.ToDictionary(static item => item.Id);
        var parcelById = snapshot.Parcels.ToDictionary(static item => item.Id);
        var buildingById = snapshot.Buildings.ToDictionary(static item => item.Id);
        _ = ValidateRegionalIds(snapshot.RoadSigns.Select(static item => item.Id.Value), "Road sign");

        foreach (var toponym in snapshot.Toponyms)
        {
            ArgumentNullException.ThrowIfNull(toponym);
            ArgumentNullException.ThrowIfNull(toponym.Provenance);
            if (!Enum.IsDefined(toponym.Kind)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            if (string.IsNullOrWhiteSpace(toponym.Name) || toponym.Name.Length > MaximumRegionalToponymLength)
                throw new ArgumentException("Human toponym names must be non-empty and bounded.", nameof(checkpoint));
            if (string.IsNullOrWhiteSpace(toponym.Provenance.GeneratorKey) || toponym.Provenance.GeneratorKey.Length > 128)
                throw new ArgumentException("Human toponym provenance generator keys must be non-empty and bounded.", nameof(checkpoint));
            if (toponym.Provenance.ParentHumanToponymId is { } parentId && !toponymIds.Contains(parentId.Value))
                throw new ArgumentException("Human toponym references a missing parent toponym.", nameof(checkpoint));
            if (toponym.Provenance.SourceNaturalToponym is { } natural)
            {
                if (natural.Id.Value == 0UL || natural.FeatureId.Value == 0UL
                    || string.IsNullOrWhiteSpace(natural.Name) || natural.Name.Length > MaximumRegionalToponymLength)
                    throw new ArgumentException("Human toponym contains invalid or overlong natural-name provenance.", nameof(checkpoint));
                if (toponym.Provenance.SourceFeatureId is { } sourceFeatureId && sourceFeatureId != natural.FeatureId)
                    throw new ArgumentException("Human toponym natural-name provenance has mismatched feature references.", nameof(checkpoint));
            }
        }

        ValidateAcyclicParentGraph(snapshot.Toponyms.Select(static item => (item.Id, item.Provenance.ParentHumanToponymId)), "Human toponym", nameof(checkpoint));

        long totalPopulation = 0;
        long totalJobs = 0;
        foreach (var settlement in snapshot.Settlements)
        {
            ArgumentNullException.ThrowIfNull(settlement);
            ValidatePoint(settlement.Center);
            if (!Enum.IsDefined(settlement.Environment)
                || !Enum.IsDefined(settlement.Origin)
                || !Enum.IsDefined(settlement.Role)
                || !Enum.IsDefined(settlement.InitialEconomy))
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            if (!toponymIds.Contains(settlement.NameId.Value))
                throw new ArgumentException("Settlement references a missing human toponym.", nameof(checkpoint));
            if (settlement.Population < 0 || settlement.Population > MaximumRegionalMaterializedPopulation
                || settlement.Jobs < 0 || settlement.Jobs > MaximumRegionalMaterializedJobs
                || !double.IsFinite(settlement.InfluenceRadiusMeters) || settlement.InfluenceRadiusMeters <= 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            totalPopulation += settlement.Population;
            totalJobs += settlement.Jobs;
            if (totalPopulation > MaximumRegionalMaterializedPopulation || totalJobs > MaximumRegionalMaterializedJobs)
                throw new ArgumentException("Regional generation scalar population/jobs exceed the bounded materialization budget.", nameof(checkpoint));
            ValidateSuitability(settlement.Suitability, checkpoint);
        }

        foreach (var growthEvent in snapshot.GrowthEvents)
        {
            ArgumentNullException.ThrowIfNull(growthEvent);
            if (!growthEventIds.Contains(growthEvent.Id.Value) || !settlementIds.Contains(growthEvent.SettlementId.Value))
                throw new ArgumentException("Historical growth event references invalid state.", nameof(checkpoint));
            if (!Enum.IsDefined(growthEvent.Stage) || growthEvent.Sequence < 0 || growthEvent.PopulationDelta < 0 || growthEvent.JobDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidatePoint(growthEvent.Center);
            if (string.IsNullOrWhiteSpace(growthEvent.Reason) || growthEvent.Reason.Length > 256)
                throw new ArgumentException("Historical growth reason must be non-empty and bounded.", nameof(checkpoint));
        }

        foreach (var corridor in snapshot.Corridors)
        {
            ArgumentNullException.ThrowIfNull(corridor);
            if (!corridorIds.Contains(corridor.Id.Value)
                || !settlementIds.Contains(corridor.FromSettlementId.Value)
                || !settlementIds.Contains(corridor.ToSettlementId.Value)
                || corridor.FromSettlementId == corridor.ToSettlementId)
                throw new ArgumentException("Regional corridor references invalid settlements.", nameof(checkpoint));
            if (!Enum.IsDefined(corridor.Kind)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ArgumentNullException.ThrowIfNull(corridor.Geometry);
            if (corridor.Geometry.Count < 2 || corridor.Geometry.Count > 256)
                throw new ArgumentException("Regional corridor geometry must be bounded and contain at least two points.", nameof(checkpoint));
            foreach (var point in corridor.Geometry) ValidatePoint(point);
            ValidateUnit(corridor.TerrainAdaptation, nameof(checkpoint));
            if (!double.IsFinite(corridor.ConstructionCost) || corridor.ConstructionCost < 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            if (corridor.NameId is { } corridorNameId && !toponymIds.Contains(corridorNameId.Value))
                throw new ArgumentException("Regional corridor references a missing name.", nameof(checkpoint));
        }

        foreach (var district in snapshot.Districts)
        {
            ArgumentNullException.ThrowIfNull(district);
            if (!settlementIds.Contains(district.SettlementId.Value) || !toponymIds.Contains(district.NameId.Value))
                throw new ArgumentException("District references invalid settlement or name state.", nameof(checkpoint));
            if (!Enum.IsDefined(district.Kind)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidateRegionalVolume(district.Bounds, "District bounds", nameof(checkpoint));
            ValidateUnit(district.Accessibility, nameof(checkpoint));
        }

        foreach (var parcel in snapshot.Parcels)
        {
            ArgumentNullException.ThrowIfNull(parcel);
            if (!settlementIds.Contains(parcel.SettlementId.Value) || !districtById.TryGetValue(parcel.DistrictId, out var district) || district.SettlementId != parcel.SettlementId)
                throw new ArgumentException("Parcel references an invalid or cross-Settlement district.", nameof(checkpoint));
            if (!Enum.IsDefined(parcel.Zone) || !Enum.IsDefined(parcel.DevelopmentState))
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidateRegionalVolume(parcel.Bounds, "Parcel bounds", nameof(checkpoint));
            ValidateUnit(parcel.DevelopmentSuitability, nameof(checkpoint));
            ValidateUnit(parcel.LandValue, nameof(checkpoint));
            if (parcel.BuildingId is { } parcelBuildingId
                && (!buildingById.TryGetValue(parcelBuildingId, out var building) || building.ParcelId != parcel.Id))
                throw new ArgumentException("Parcel and generated building references are not reciprocal.", nameof(checkpoint));
        }

        var occupiedParcels = new HashSet<ParcelId>();
        foreach (var building in snapshot.Buildings)
        {
            ArgumentNullException.ThrowIfNull(building);
            if (!parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.BuildingId != building.Id || !occupiedParcels.Add(building.ParcelId))
                throw new ArgumentException("Generated building ownership is missing, duplicated, or not reciprocal.", nameof(checkpoint));
            ValidateRegionalVolume(building.Bounds, "Generated building bounds", nameof(checkpoint));
            if (!ContainsHorizontal(parcel.Bounds, building.Bounds))
                throw new ArgumentException("Generated building bounds must be contained by its Parcel.", nameof(checkpoint));
            if (!Enum.IsDefined(building.Use) || building.Floors <= 0 || building.Floors > 256 || building.Capacity < 0 || building.HistoricalStage < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        foreach (var poi in snapshot.Pois)
        {
            ArgumentNullException.ThrowIfNull(poi);
            if (!settlementIds.Contains(poi.SettlementId.Value) || !Enum.IsDefined(poi.Kind))
                throw new ArgumentException("Generated POI references invalid settlement state.", nameof(checkpoint));
            ValidatePoint(poi.Position);
            if (poi.BuildingId is { } poiBuildingId)
            {
                if (!buildingById.TryGetValue(poiBuildingId, out var building) || !parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.SettlementId != poi.SettlementId)
                    throw new ArgumentException("Generated POI references a Building in a different Settlement hierarchy.", nameof(checkpoint));
            }
            if (poi.NameId is { } poiNameId && !toponymIds.Contains(poiNameId.Value))
                throw new ArgumentException("Generated POI references a missing name.", nameof(checkpoint));
        }

        foreach (var sign in snapshot.RoadSigns)
        {
            ArgumentNullException.ThrowIfNull(sign);
            if (!Enum.IsDefined(sign.Kind) || !corridorIds.Contains(sign.CorridorId.Value))
                throw new ArgumentException("Road sign references invalid corridor state.", nameof(checkpoint));
            if (sign.DestinationSettlementId is { } destinationId && !settlementIds.Contains(destinationId.Value))
                throw new ArgumentException("Road sign references a missing destination settlement.", nameof(checkpoint));
            ValidatePoint(sign.Position);
            if (string.IsNullOrWhiteSpace(sign.Text) || sign.Text.Length > 256)
                throw new ArgumentException("Road sign text must be non-empty and bounded.", nameof(checkpoint));
        }

        ValidateQuality(snapshot.Quality, checkpoint);
    }

    private static void ValidateRegionalVolume(WorldVolume volume, string label, string parameterName)
    {
        if (!double.IsFinite(volume.MinX) || !double.IsFinite(volume.MinY) || !double.IsFinite(volume.MinZ)
            || !double.IsFinite(volume.MaxX) || !double.IsFinite(volume.MaxY) || !double.IsFinite(volume.MaxZ)
            || volume.MaxX <= volume.MinX || volume.MaxY <= volume.MinY || volume.MaxZ < volume.MinZ)
            throw new ArgumentException($"{label} must be finite, ordered, and have positive horizontal area.", parameterName);
    }

    private static bool ContainsHorizontal(WorldVolume outer, WorldVolume inner) =>
        inner.MinX >= outer.MinX && inner.MaxX <= outer.MaxX && inner.MinY >= outer.MinY && inner.MaxY <= outer.MaxY;

    private static HashSet<ulong> ValidateRegionalIds(IEnumerable<ulong> ids, string name)
    {
        var result = new HashSet<ulong>();
        foreach (var id in ids)
        {
            if (id == 0UL || !result.Add(id))
                throw new ArgumentException($"{name} IDs must be unique and greater than zero.", nameof(ids));
        }
        return result;
    }

    private static void ValidateSuitability(SettlementSuitability value, SimulationCheckpoint checkpoint)
    {
        ValidateUnit(value.Flatness, nameof(checkpoint));
        ValidateUnit(value.WaterAccess, nameof(checkpoint));
        ValidateUnit(value.TransportPotential, nameof(checkpoint));
        ValidateUnit(value.Buildability, nameof(checkpoint));
        ValidateUnit(value.ResourceAccess, nameof(checkpoint));
        ValidateUnit(value.FloodRisk, nameof(checkpoint));
        ValidateUnit(value.SteepSlopeRisk, nameof(checkpoint));
        ValidateUnit(value.Isolation, nameof(checkpoint));
        ValidateUnit(value.ConstructionCost, nameof(checkpoint));
        ValidateUnit(value.TotalScore, nameof(checkpoint));
    }

    private static void ValidateQuality(RegionalQualityReport value, SimulationCheckpoint checkpoint)
    {
        ValidateUnit(value.TerrainAdaptation, nameof(checkpoint));
        ValidateUnit(value.RoadConnectivity, nameof(checkpoint));
        ValidateUnit(value.AverageSlopeCost, nameof(checkpoint));
        ValidateUnit(value.Accessibility, nameof(checkpoint));
        ValidateUnit(value.CongestionRisk, nameof(checkpoint));
        ValidateUnit(value.LandUseConsistency, nameof(checkpoint));
        ValidateUnit(value.FloodExposure, nameof(checkpoint));
        ValidateUnit(value.UrbanCompactness, nameof(checkpoint));
        ValidateUnit(value.PolycentricBalance, nameof(checkpoint));
    }

    private static void ValidateUnit(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d || value > 1d)
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be finite and between zero and one.");
    }
}
