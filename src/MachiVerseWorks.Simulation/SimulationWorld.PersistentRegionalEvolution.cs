namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private PersistentRegionalEvolutionSnapshot? _persistentRegionalEvolution;
    private PersistentRegionalEvolutionOptions _persistentRegionalEvolutionOptions = new();
    private ulong _persistentRegionalEvolutionTickCount;
    private ulong _nextPersistentRegionalRelationId = 1UL;

    public bool HasPersistentRegionalEvolution => _persistentRegionalEvolution is not null;

    public void ConfigurePersistentRegionalEvolution(PersistentRegionalEvolutionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (_persistentRegionalEvolution is not null)
            throw new InvalidOperationException("Persistent regional evolution has already been initialized.");
        _persistentRegionalEvolutionOptions = options;
    }

    public PersistentRegionalEvolutionSnapshot CreatePersistentRegionalEvolutionSnapshot()
    {
        EnsurePersistentRegionalEvolution();
        return CapturePersistentRegionalEvolutionSnapshot();
    }

    public bool TryCreatePersistentRegionalEvolutionSnapshot(out PersistentRegionalEvolutionSnapshot? snapshot)
    {
        if (_regionalGeneration is null)
        {
            snapshot = null;
            return false;
        }
        EnsurePersistentRegionalEvolution();
        snapshot = CapturePersistentRegionalEvolutionSnapshot();
        return true;
    }

    public void AdvancePersistentRegionalEvolutionYears(int years)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(years);
        EnsurePersistentRegionalEvolution();
        for (var year = 0; year < years; year++)
        {
            var previous = _persistentRegionalEvolution!;
            var next = PersistentRegionalEvolutionEngine.AdvanceYears(
                previous, _regionalGeneration!, 1, CreateRegionalEvolutionDrivers);
            next = ApplyPersistentRegionalRedevelopment(next);
            next = ApplyPersistentRegionalWorldChangesWithoutRelationRecording(next);
            next = RecalculatePersistentRegionalSpatialState(next);
            next = PersistentRegionalEvolutionEngine.RefreshDerivedCollections(next);
            next = RecalculatePersistentRegionalRelations(previous, next);
            _persistentRegionalEvolution = RecordRegionalRelationChanges(previous, next);
        }
    }

    private void StepPersistentRegionalEvolution(SimulationTime nextTime)
    {
        if (_regionalGeneration is null) return;
        EnsurePersistentRegionalEvolution();
        var targetYear = checked((int)Math.Min(int.MaxValue, nextTime.TickCount / _persistentRegionalEvolutionOptions.TicksPerYear));
        var years = targetYear - _persistentRegionalEvolution!.CurrentYear;
        for (var year = 0; year < years; year++)
        {
            var previous = _persistentRegionalEvolution;
            var next = PersistentRegionalEvolutionEngine.AdvanceYears(
                previous, _regionalGeneration, 1, CreateRegionalEvolutionDrivers);
            next = ApplyPersistentRegionalRedevelopment(next);
            next = ApplyPersistentRegionalWorldChangesWithoutRelationRecording(next);
            next = RecalculatePersistentRegionalSpatialState(next);
            next = PersistentRegionalEvolutionEngine.RefreshDerivedCollections(next);
            next = RecalculatePersistentRegionalRelations(previous, next);
            _persistentRegionalEvolution = RecordRegionalRelationChanges(previous, next);
        }
        _persistentRegionalEvolutionTickCount = nextTime.TickCount;
    }

    private RegionalEvolutionDrivers CreateRegionalEvolutionDrivers(SettlementEvolutionState settlement)
    {
        var totalRequiredWorkers = 0;
        for (var index = 0; index < _economyJobs.Count; index++)
            totalRequiredWorkers = checked(totalRequiredWorkers + _economyJobs[index].RequiredWorkerCount);
        var employmentRatio = totalRequiredWorkers == 0 ? 0.5 : Math.Clamp((double)_economyEmployments.Count / totalRequiredWorkers, 0d, 1d);
        var establishmentPressure = Math.Clamp(_economyEstablishments.Count / Math.Max(1d, _persistentRegionalEvolution?.Settlements.Count ?? 1) / 20d, 0d, 1d);
        var localPopulationPressure = Math.Clamp(settlement.Population / 20_000d, 0d, 1d);
        var localJobPressure = Math.Clamp(settlement.Jobs / 10_000d, 0d, 1d);
        var networkConnectivity = MeasureRegionalConnectivity(settlement);
        var logistics = CreateLogisticsSnapshot();
        var logisticsPressure = Math.Clamp(
            logistics.Statistics.InTransitShipmentCount / Math.Max(1d, _persistentRegionalEvolution?.Settlements.Count ?? 1) / 8d,
            0d,
            1d);
        return new RegionalEvolutionDrivers(
            Math.Clamp(localPopulationPressure * 0.7 + employmentRatio * 0.3, 0d, 1d),
            Math.Clamp(localJobPressure * 0.65 + establishmentPressure * 0.35, 0d, 1d),
            Math.Clamp(settlement.ServiceIndex * 0.8 + establishmentPressure * 0.2, 0d, 1d),
            Math.Clamp(logisticsPressure * 0.6d + networkConnectivity * 0.4d, 0d, 1d),
            networkConnectivity,
            Math.Clamp(1d - _regionalGeneration!.Quality.CongestionRisk * 0.5 - _regionalGeneration.Quality.FloodExposure * 0.25, 0d, 1d));
    }

    private void EnsurePersistentRegionalEvolution()
    {
        if (_persistentRegionalEvolution is not null) return;
        if (_regionalGeneration is null)
            throw new InvalidOperationException("Regional generation must be initialized before persistent regional evolution.");
        var currentYear = checked((int)Math.Min(int.MaxValue, Time.TickCount / _persistentRegionalEvolutionOptions.TicksPerYear));
        var initialized = PersistentRegionalEvolutionEngine.Initialize(_regionalGeneration, currentYear);
        EnsurePersistentRegionalRelationIdFloor(initialized.Relations);
        var spatial = RecalculatePersistentRegionalSpatialState(initialized);
        spatial = PersistentRegionalEvolutionEngine.RefreshDerivedCollections(spatial);
        _persistentRegionalEvolution = RecalculatePersistentRegionalRelations(initialized, spatial);
        _persistentRegionalEvolutionTickCount = Time.TickCount;
    }

    private PersistentRegionalEvolutionSnapshot CapturePersistentRegionalEvolutionSnapshot() =>
        DetachPersistentRegionalEvolution(_persistentRegionalEvolution!) with { TickCount = _persistentRegionalEvolutionTickCount };

    private PersistentRegionalEvolutionCheckpoint? CreatePersistentRegionalEvolutionCheckpoint() =>
        _persistentRegionalEvolution is null
            ? null
            : new PersistentRegionalEvolutionCheckpoint(
                CapturePersistentRegionalEvolutionSnapshot(),
                _persistentRegionalEvolutionOptions.TicksPerYear,
                _nextPersistentRegionalRelationId,
                CreatePersistentRegionalMaterializationCheckpoint());

    private void RestorePersistentRegionalEvolution(PersistentRegionalEvolutionCheckpoint? checkpoint)
    {
        if (checkpoint is null)
        {
            _persistentRegionalEvolution = null;
            _persistentRegionalEvolutionOptions = new PersistentRegionalEvolutionOptions();
            _persistentRegionalEvolutionTickCount = 0;
            _nextPersistentRegionalRelationId = 1UL;
            RestorePersistentRegionalMaterializations(null);
            return;
        }

        _persistentRegionalEvolutionOptions = new PersistentRegionalEvolutionOptions(checkpoint.TicksPerYear);
        _persistentRegionalEvolution = DetachPersistentRegionalEvolution(checkpoint.Snapshot);
        _persistentRegionalEvolutionTickCount = checkpoint.Snapshot.TickCount;
        _nextPersistentRegionalRelationId = checkpoint.NextRelationId;
        RestorePersistentRegionalMaterializations(checkpoint.MaterializedBuildings);
    }

    private static PersistentRegionalEvolutionSnapshot DetachPersistentRegionalEvolution(PersistentRegionalEvolutionSnapshot source) => source with
    {
        Settlements = source.Settlements.ToArray(),
        Parcels = source.Parcels.ToArray(),
        Buildings = source.Buildings.ToArray(),
        ServiceCatchments = source.ServiceCatchments.ToArray(),
        InfrastructureDemands = source.InfrastructureDemands.ToArray(),
        Relations = source.Relations.ToArray(),
        Events = source.Events.ToArray(),
    };

    private static void ValidatePersistentRegionalEvolutionCheckpoint(SimulationCheckpoint checkpoint)
    {
        var evolution = checkpoint.Economy?.RegionalEvolution;
        if (evolution is null) return;
        if (evolution.TicksPerYear == 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional evolution TicksPerYear must be greater than zero.");
        var snapshot = evolution.Snapshot ?? throw new ArgumentException("Regional evolution snapshot is required.", nameof(checkpoint));
        if (snapshot.CurrentYear < 0 || snapshot.TickCount > checkpoint.TickCount)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional evolution time is invalid.");
        if (snapshot.Settlements.Count > 256 || snapshot.Parcels.Count > 16_384 || snapshot.Buildings.Count > 16_384 || snapshot.Events.Count > 262_144)
            throw new ArgumentException("Regional evolution checkpoint exceeds bounded collection limits.", nameof(checkpoint));

        var settlementIds = new HashSet<SettlementId>();
        foreach (var settlement in snapshot.Settlements)
        {
            if (settlement.SettlementId.Value == 0 || !settlementIds.Add(settlement.SettlementId))
                throw new ArgumentException("Regional evolution settlement IDs must be unique and non-zero.", nameof(checkpoint));
            if (settlement.Population < 0 || settlement.Jobs < 0 || settlement.EstablishedYear > snapshot.CurrentYear)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidatePoint(settlement.Center);
            ValidateUnit(settlement.ServiceIndex, nameof(checkpoint));
            ValidateUnit(settlement.Density, nameof(checkpoint));
            ValidateUnit(settlement.Accessibility, nameof(checkpoint));
            if (!double.IsFinite(settlement.InfluenceRadiusMeters) || settlement.InfluenceRadiusMeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        var parcelIds = new HashSet<ParcelId>();
        var parcelBuildingIds = new Dictionary<ParcelId, GeneratedBuildingId?>();
        foreach (var parcel in snapshot.Parcels)
        {
            if (parcel.ParcelId.Value == 0 || !parcelIds.Add(parcel.ParcelId))
                throw new ArgumentException("Regional evolution parcel IDs must be unique and non-zero.", nameof(checkpoint));
            if (!settlementIds.Contains(parcel.SettlementId))
                throw new ArgumentException("Regional evolution parcel references a missing settlement.", nameof(checkpoint));
            ValidateUnit(parcel.DevelopmentDemand, nameof(checkpoint));
            ValidateUnit(parcel.LandValue, nameof(checkpoint));
            parcelBuildingIds.Add(parcel.ParcelId, parcel.BuildingId);
        }

        var buildingIds = new HashSet<GeneratedBuildingId>();
        var buildingParcels = new Dictionary<GeneratedBuildingId, ParcelId>();
        foreach (var building in snapshot.Buildings)
        {
            if (building.BuildingId.Value == 0 || !buildingIds.Add(building.BuildingId))
                throw new ArgumentException("Regional evolution Building IDs must be unique and non-zero.", nameof(checkpoint));
            if (!parcelIds.Contains(building.ParcelId))
                throw new ArgumentException("Regional evolution Building references a missing Parcel.", nameof(checkpoint));
            ValidateUnit(building.Condition, nameof(checkpoint));
            ValidateUnit(building.Occupancy, nameof(checkpoint));
            if (building.BuiltYear > snapshot.CurrentYear || building.LastChangedYear > snapshot.CurrentYear || building.Capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            buildingParcels.Add(building.BuildingId, building.ParcelId);
        }

        foreach (var parcel in snapshot.Parcels)
        {
            if (parcel.BuildingId is not { } buildingId) continue;
            if (!buildingParcels.TryGetValue(buildingId, out var buildingParcel) || buildingParcel != parcel.ParcelId)
                throw new ArgumentException("Regional evolution Parcel/Building references are inconsistent.", nameof(checkpoint));
        }

        var relationIds = new HashSet<RegionalRelationId>();
        ulong maximumRelationId = 0;
        foreach (var relation in snapshot.Relations)
        {
            if (relation.Id.Value == 0 || !relationIds.Add(relation.Id)
                || !settlementIds.Contains(relation.FromSettlementId)
                || !settlementIds.Contains(relation.ToSettlementId)
                || relation.FromSettlementId == relation.ToSettlementId)
                throw new ArgumentException("Regional evolution relation is invalid.", nameof(checkpoint));
            ValidateUnit(relation.Strength, nameof(checkpoint));
            maximumRelationId = Math.Max(maximumRelationId, relation.Id.Value);
        }
        if (evolution.NextRelationId == 0 || evolution.NextRelationId <= maximumRelationId)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional evolution next Relation ID must exceed all stored Relation IDs.");

        var actualBuildingIds = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var actualPoiIds = checkpoint.Pois.Select(static item => item.Id).ToHashSet();
        var materializedGeneratedIds = new HashSet<GeneratedBuildingId>();
        foreach (var binding in evolution.MaterializedBuildings ?? Array.Empty<PersistentRegionalMaterializationBinding>())
        {
            if (!buildingIds.Contains(binding.GeneratedBuildingId) || !materializedGeneratedIds.Add(binding.GeneratedBuildingId)
                || binding.BuildingId.Value == 0 || !actualBuildingIds.Contains(binding.BuildingId)
                || (binding.PoiId is { } poiId && !actualPoiIds.Contains(poiId)))
                throw new ArgumentException("Regional evolution materialization binding is invalid.", nameof(checkpoint));
        }

        ulong previousEventId = 0;
        foreach (var item in snapshot.Events.OrderBy(x => x.Id.Value))
        {
            if (item.Id.Value == 0 || item.Id.Value <= previousEventId || item.Year > snapshot.CurrentYear || !settlementIds.Contains(item.SettlementId))
                throw new ArgumentException("Regional evolution history is invalid.", nameof(checkpoint));
            if (item.BuildingId is { } eventBuildingId && !buildingIds.Contains(eventBuildingId))
                throw new ArgumentException("Regional evolution history references a missing Building.", nameof(checkpoint));
            previousEventId = item.Id.Value;
        }
    }
}
