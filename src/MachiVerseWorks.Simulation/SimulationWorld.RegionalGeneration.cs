namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
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
        if (snapshot.Volume.Width <= 0d || snapshot.Volume.Depth <= 0d)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional generation volume must be non-empty.");

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
        var districtIds = ValidateRegionalIds(snapshot.Districts.Select(static item => item.Id.Value), "District");
        var parcelIds = ValidateRegionalIds(snapshot.Parcels.Select(static item => item.Id.Value), "Parcel");
        var buildingIds = ValidateRegionalIds(snapshot.Buildings.Select(static item => item.Id.Value), "Generated building");
        _ = ValidateRegionalIds(snapshot.Pois.Select(static item => item.Id.Value), "Generated POI");
        _ = ValidateRegionalIds(snapshot.RoadSigns.Select(static item => item.Id.Value), "Road sign");

        foreach (var toponym in snapshot.Toponyms)
        {
            ArgumentNullException.ThrowIfNull(toponym);
            ArgumentNullException.ThrowIfNull(toponym.Provenance);
            if (!Enum.IsDefined(toponym.Kind)) throw new ArgumentOutOfRangeException(nameof(checkpoint));
            if (string.IsNullOrWhiteSpace(toponym.Name) || toponym.Name.Length > 160)
                throw new ArgumentException("Human toponym names must be non-empty and bounded.", nameof(checkpoint));
            if (string.IsNullOrWhiteSpace(toponym.Provenance.GeneratorKey) || toponym.Provenance.GeneratorKey.Length > 128)
                throw new ArgumentException("Human toponym provenance generator keys must be non-empty and bounded.", nameof(checkpoint));
            if (toponym.Provenance.ParentHumanToponymId is { } parentId && !toponymIds.Contains(parentId.Value))
                throw new ArgumentException("Human toponym references a missing parent toponym.", nameof(checkpoint));
            if (toponym.Provenance.SourceNaturalToponym is { } natural)
            {
                if (natural.Id.Value == 0UL || natural.FeatureId.Value == 0UL || string.IsNullOrWhiteSpace(natural.Name))
                    throw new ArgumentException("Human toponym contains invalid natural-name provenance.", nameof(checkpoint));
                if (toponym.Provenance.SourceFeatureId is { } sourceFeatureId && sourceFeatureId != natural.FeatureId)
                    throw new ArgumentException("Human toponym natural-name provenance has mismatched feature references.", nameof(checkpoint));
            }
        }

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
            if (settlement.Population < 0 || settlement.Jobs < 0 || !double.IsFinite(settlement.InfluenceRadiusMeters) || settlement.InfluenceRadiusMeters <= 0d)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
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
            ValidateUnit(district.Accessibility, nameof(checkpoint));
        }

        foreach (var parcel in snapshot.Parcels)
        {
            ArgumentNullException.ThrowIfNull(parcel);
            if (!settlementIds.Contains(parcel.SettlementId.Value) || !districtIds.Contains(parcel.DistrictId.Value))
                throw new ArgumentException("Parcel references invalid settlement or district state.", nameof(checkpoint));
            if (!Enum.IsDefined(parcel.Zone) || !Enum.IsDefined(parcel.DevelopmentState))
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidateUnit(parcel.DevelopmentSuitability, nameof(checkpoint));
            ValidateUnit(parcel.LandValue, nameof(checkpoint));
            if (parcel.BuildingId is { } parcelBuildingId && !buildingIds.Contains(parcelBuildingId.Value))
                throw new ArgumentException("Parcel references a missing generated building.", nameof(checkpoint));
        }

        foreach (var building in snapshot.Buildings)
        {
            ArgumentNullException.ThrowIfNull(building);
            if (!parcelIds.Contains(building.ParcelId.Value))
                throw new ArgumentException("Generated building references a missing parcel.", nameof(checkpoint));
            if (!Enum.IsDefined(building.Use) || building.Floors <= 0 || building.Floors > 256 || building.Capacity < 0 || building.HistoricalStage < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        foreach (var poi in snapshot.Pois)
        {
            ArgumentNullException.ThrowIfNull(poi);
            if (!settlementIds.Contains(poi.SettlementId.Value) || !Enum.IsDefined(poi.Kind))
                throw new ArgumentException("Generated POI references invalid settlement state.", nameof(checkpoint));
            ValidatePoint(poi.Position);
            if (poi.BuildingId is { } poiBuildingId && !buildingIds.Contains(poiBuildingId.Value))
                throw new ArgumentException("Generated POI references a missing building.", nameof(checkpoint));
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
