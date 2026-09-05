namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private const int MaximumPersistentRegionalDerivedItems = 65_536;
    private const int MaximumPersistentRegionalReasonLength = 256;

    private PersistentRegionalEvolutionSnapshot? _persistentRegionalEvolution;
    private PersistentRegionalEvolutionOptions _persistentRegionalEvolutionOptions = new();
    private ulong _persistentRegionalEvolutionTickCount;
    private ulong _nextPersistentRegionalRelationId = 1UL;

    private readonly record struct RegionalEvolutionGlobalDrivers(
        double EmploymentRatio,
        double EstablishmentPressure,
        double LogisticsPressure,
        double InfrastructureCapacity);

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
            var globalDrivers = CreateRegionalEvolutionGlobalDrivers();
            var next = PersistentRegionalEvolutionEngine.AdvanceYears(
                previous, _regionalGeneration!, 1, settlement => CreateRegionalEvolutionDrivers(settlement, globalDrivers));
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
            var globalDrivers = CreateRegionalEvolutionGlobalDrivers();
            var next = PersistentRegionalEvolutionEngine.AdvanceYears(
                previous, _regionalGeneration, 1, settlement => CreateRegionalEvolutionDrivers(settlement, globalDrivers));
            next = ApplyPersistentRegionalRedevelopment(next);
            next = ApplyPersistentRegionalWorldChangesWithoutRelationRecording(next);
            next = RecalculatePersistentRegionalSpatialState(next);
            next = PersistentRegionalEvolutionEngine.RefreshDerivedCollections(next);
            next = RecalculatePersistentRegionalRelations(previous, next);
            _persistentRegionalEvolution = RecordRegionalRelationChanges(previous, next);
        }
        _persistentRegionalEvolutionTickCount = nextTime.TickCount;
    }

    private RegionalEvolutionGlobalDrivers CreateRegionalEvolutionGlobalDrivers()
    {
        long totalRequiredWorkers = 0;
        for (var index = 0; index < _economyJobs.Count; index++)
            totalRequiredWorkers = checked(totalRequiredWorkers + _economyJobs[index].RequiredWorkerCount);
        var employmentRatio = totalRequiredWorkers == 0 ? 0.5 : Math.Clamp(_economyEmployments.Count / (double)totalRequiredWorkers, 0d, 1d);
        var settlementCount = Math.Max(1, _persistentRegionalEvolution?.Settlements.Count ?? 1);
        var establishmentPressure = Math.Clamp(_economyEstablishments.Count / (double)settlementCount / 20d, 0d, 1d);
        var logistics = CreateLogisticsSnapshot();
        var logisticsPressure = Math.Clamp(logistics.Statistics.InTransitShipmentCount / (double)settlementCount / 8d, 0d, 1d);
        var infrastructureCapacity = Math.Clamp(
            1d - _regionalGeneration!.Quality.CongestionRisk * 0.5 - _regionalGeneration.Quality.FloodExposure * 0.25,
            0d,
            1d);
        return new RegionalEvolutionGlobalDrivers(employmentRatio, establishmentPressure, logisticsPressure, infrastructureCapacity);
    }

    private RegionalEvolutionDrivers CreateRegionalEvolutionDrivers(
        SettlementEvolutionState settlement,
        RegionalEvolutionGlobalDrivers global)
    {
        var localPopulationPressure = Math.Clamp(settlement.Population / 20_000d, 0d, 1d);
        var localJobPressure = Math.Clamp(settlement.Jobs / 10_000d, 0d, 1d);
        var networkConnectivity = MeasureRegionalConnectivity(settlement);
        return new RegionalEvolutionDrivers(
            Math.Clamp(localPopulationPressure * 0.7 + global.EmploymentRatio * 0.3, 0d, 1d),
            Math.Clamp(localJobPressure * 0.65 + global.EstablishmentPressure * 0.35, 0d, 1d),
            Math.Clamp(settlement.ServiceIndex * 0.8 + global.EstablishmentPressure * 0.2, 0d, 1d),
            Math.Clamp(global.LogisticsPressure * 0.6d + networkConnectivity * 0.4d, 0d, 1d),
            networkConnectivity,
            global.InfrastructureCapacity);
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
        if (snapshot.Settlements.Count > PersistentRegionalEvolutionLimits.MaximumSettlementCount
            || snapshot.Parcels.Count > 16_384
            || snapshot.Buildings.Count > 16_384
            || snapshot.ServiceCatchments.Count > MaximumPersistentRegionalDerivedItems
            || snapshot.InfrastructureDemands.Count > MaximumPersistentRegionalDerivedItems
            || snapshot.Relations.Count > MaximumPersistentRegionalDerivedItems
            || snapshot.Events.Count > PersistentRegionalEvolutionLimits.MaximumEventCount)
            throw new ArgumentException("Regional evolution checkpoint exceeds bounded collection limits.", nameof(checkpoint));

        var settlementIds = new HashSet<SettlementId>();
        foreach (var settlement in snapshot.Settlements)
        {
            if (settlement.SettlementId.Value == 0 || !settlementIds.Add(settlement.SettlementId))
                throw new ArgumentException("Regional evolution settlement IDs must be unique and non-zero.", nameof(checkpoint));
            if (!Enum.IsDefined(settlement.Scale) || !Enum.IsDefined(settlement.Trend)
                || settlement.Population < 0 || settlement.Jobs < 0 || settlement.EstablishedYear > snapshot.CurrentYear
                || (settlement.DormantSinceYear is { } dormantYear && dormantYear > snapshot.CurrentYear))
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            ValidatePoint(settlement.Center);
            ValidateUnit(settlement.ServiceIndex, nameof(checkpoint));
            ValidateUnit(settlement.Density, nameof(checkpoint));
            ValidateUnit(settlement.Accessibility, nameof(checkpoint));
            if (!double.IsFinite(settlement.InfluenceRadiusMeters) || settlement.InfluenceRadiusMeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
        }

        var parcelIds = new HashSet<ParcelId>();
        foreach (var parcel in snapshot.Parcels)
        {
            if (parcel.ParcelId.Value == 0 || !parcelIds.Add(parcel.ParcelId))
                throw new ArgumentException("Regional evolution parcel IDs must be unique and non-zero.", nameof(checkpoint));
            if (!settlementIds.Contains(parcel.SettlementId) || !Enum.IsDefined(parcel.DevelopmentState))
                throw new ArgumentException("Regional evolution parcel references invalid state.", nameof(checkpoint));
            ValidateUnit(parcel.DevelopmentDemand, nameof(checkpoint));
            ValidateUnit(parcel.LandValue, nameof(checkpoint));
        }

        var buildingIds = new HashSet<GeneratedBuildingId>();
        var buildingById = new Dictionary<GeneratedBuildingId, BuildingLifecycleState>();
        var buildingParcels = new Dictionary<GeneratedBuildingId, ParcelId>();
        foreach (var building in snapshot.Buildings)
        {
            if (building.BuildingId.Value == 0 || !buildingIds.Add(building.BuildingId))
                throw new ArgumentException("Regional evolution Building IDs must be unique and non-zero.", nameof(checkpoint));
            if (!parcelIds.Contains(building.ParcelId) || !Enum.IsDefined(building.Use) || !Enum.IsDefined(building.Status))
                throw new ArgumentException("Regional evolution Building references invalid state.", nameof(checkpoint));
            ValidateUnit(building.Condition, nameof(checkpoint));
            ValidateUnit(building.Occupancy, nameof(checkpoint));
            if (building.BuiltYear > snapshot.CurrentYear || building.LastChangedYear > snapshot.CurrentYear || building.Capacity < 0)
                throw new ArgumentOutOfRangeException(nameof(checkpoint));
            buildingById.Add(building.BuildingId, building);
            buildingParcels.Add(building.BuildingId, building.ParcelId);
        }

        foreach (var parcel in snapshot.Parcels)
        {
            if (parcel.BuildingId is not { } buildingId) continue;
            if (!buildingParcels.TryGetValue(buildingId, out var buildingParcel) || buildingParcel != parcel.ParcelId)
                throw new ArgumentException("Regional evolution Parcel/Building references are inconsistent.", nameof(checkpoint));
        }

        foreach (var item in snapshot.ServiceCatchments)
        {
            if (!settlementIds.Contains(item.SettlementId) || !Enum.IsDefined(item.Kind)
                || !double.IsFinite(item.RadiusMeters) || item.RadiusMeters <= 0d)
                throw new ArgumentException("Regional evolution service catchment is invalid.", nameof(checkpoint));
            ValidateUnit(item.Coverage, nameof(checkpoint));
        }
        foreach (var item in snapshot.InfrastructureDemands)
        {
            if (!settlementIds.Contains(item.SettlementId) || !Enum.IsDefined(item.Kind)
                || string.IsNullOrWhiteSpace(item.Reason) || item.Reason.Length > MaximumPersistentRegionalReasonLength)
                throw new ArgumentException("Regional evolution infrastructure demand is invalid.", nameof(checkpoint));
            ValidateUnit(item.Demand, nameof(checkpoint));
        }

        var relationIds = new HashSet<RegionalRelationId>();
        ulong maximumRelationId = 0;
        foreach (var relation in snapshot.Relations)
        {
            if (relation.Id.Value == 0 || !relationIds.Add(relation.Id)
                || !settlementIds.Contains(relation.FromSettlementId)
                || !settlementIds.Contains(relation.ToSettlementId)
                || relation.FromSettlementId == relation.ToSettlementId
                || !Enum.IsDefined(relation.Kind)
                || relation.SinceYear > snapshot.CurrentYear)
                throw new ArgumentException("Regional evolution relation is invalid.", nameof(checkpoint));
            ValidateUnit(relation.Strength, nameof(checkpoint));
            maximumRelationId = Math.Max(maximumRelationId, relation.Id.Value);
        }
        if (evolution.NextRelationId == 0 || evolution.NextRelationId <= maximumRelationId)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Regional evolution next Relation ID must exceed all stored Relation IDs.");

        var actualBuildingById = checkpoint.Buildings.ToDictionary(static item => item.Id);
        var actualPoiById = checkpoint.Pois.ToDictionary(static item => item.Id);
        var roadAccessById = checkpoint.RoadAccessPoints.ToDictionary(static item => item.Id);
        var companiesById = (checkpoint.Economy?.Companies ?? []).ToDictionary(static item => item.Id);
        var establishmentsById = (checkpoint.Economy?.Establishments ?? []).ToDictionary(static item => item.Id);
        var jobsById = (checkpoint.Economy?.Jobs ?? []).ToDictionary(static item => item.Id);
        var regionalGeneration = checkpoint.Economy?.RegionalGeneration?.Snapshot;
        var baselineGeneratedIds = regionalGeneration?.Buildings.Select(static item => item.Id).ToHashSet() ?? [];
        var generatedParcelById = regionalGeneration?.Parcels.ToDictionary(static item => item.Id) ?? [];
        var materializedGeneratedIds = new HashSet<GeneratedBuildingId>();
        var materializedActualBuildingIds = new HashSet<BuildingId>();
        foreach (var binding in evolution.MaterializedBuildings ?? Array.Empty<PersistentRegionalMaterializationBinding>())
        {
            if (!buildingById.TryGetValue(binding.GeneratedBuildingId, out var lifecycle)
                || !materializedGeneratedIds.Add(binding.GeneratedBuildingId)
                || binding.BuildingId.Value == 0
                || !materializedActualBuildingIds.Add(binding.BuildingId)
                || !actualBuildingById.TryGetValue(binding.BuildingId, out var actualBuilding)
                || actualBuilding.Kind != MapBuildingKind(lifecycle.Use))
                throw new ArgumentException("Regional evolution materialization binding is invalid or aliases another generated Building.", nameof(checkpoint));

            if (generatedParcelById.TryGetValue(lifecycle.ParcelId, out var generatedParcel)
                && !ContainsHorizontal(generatedParcel.Bounds, actualBuilding.Bounds))
                throw new ArgumentException("Regional evolution materialization target is outside its generated Parcel.", nameof(checkpoint));

            if (binding.PoiId is { } poiId
                && (!actualPoiById.TryGetValue(poiId, out var poi) || poi.BuildingId != binding.BuildingId))
                throw new ArgumentException("Regional evolution materialization POI does not belong to its Building.", nameof(checkpoint));

            if (binding.RoadAccessPointId is { } accessPointId
                && (!roadAccessById.TryGetValue(accessPointId, out var accessPoint)
                    || (accessPoint.BuildingId != binding.BuildingId && (binding.PoiId is null || accessPoint.PoiId != binding.PoiId))))
                throw new ArgumentException("Regional evolution materialization RoadAccessPoint does not belong to its Building/POI.", nameof(checkpoint));

            if (binding.CompanyId is { } companyId && !companiesById.ContainsKey(companyId))
                throw new ArgumentException("Regional evolution materialization references a missing Company.", nameof(checkpoint));
            if (binding.EstablishmentId is { } establishmentId)
            {
                if (!establishmentsById.TryGetValue(establishmentId, out var establishment)
                    || (establishment.BuildingId != binding.BuildingId && (binding.PoiId is null || establishment.PoiId != binding.PoiId))
                    || (binding.CompanyId is { } ownerCompanyId && establishment.CompanyId != ownerCompanyId))
                    throw new ArgumentException("Regional evolution materialization Establishment ownership is invalid.", nameof(checkpoint));
            }
            if (binding.JobId is { } jobId
                && (!jobsById.TryGetValue(jobId, out var job) || binding.EstablishmentId is null || job.EstablishmentId != binding.EstablishmentId))
                throw new ArgumentException("Regional evolution materialization Job ownership is invalid.", nameof(checkpoint));
            if ((binding.CompanyId is null) != (binding.EstablishmentId is null)
                || (binding.JobId is not null && binding.EstablishmentId is null))
                throw new ArgumentException("Regional evolution materialization Economy ownership is incomplete.", nameof(checkpoint));
        }

        foreach (var lifecycle in snapshot.Buildings)
        {
            if (baselineGeneratedIds.Contains(lifecycle.BuildingId) || lifecycle.Status == BuildingLifecycleStatus.Demolished) continue;
            if (!materializedGeneratedIds.Contains(lifecycle.BuildingId))
                throw new ArgumentException("Phase31 generated Building is missing its materialization binding.", nameof(checkpoint));
        }

        ulong previousEventId = 0;
        foreach (var item in snapshot.Events)
        {
            if (item.Id.Value == 0 || item.Id.Value <= previousEventId || item.Year > snapshot.CurrentYear
                || !settlementIds.Contains(item.SettlementId) || !Enum.IsDefined(item.Kind)
                || string.IsNullOrWhiteSpace(item.Reason) || item.Reason.Length > MaximumPersistentRegionalReasonLength)
                throw new ArgumentException("Regional evolution history is invalid.", nameof(checkpoint));
            if (item.BuildingId is { } eventBuildingId && !buildingIds.Contains(eventBuildingId))
                throw new ArgumentException("Regional evolution history references a missing Building.", nameof(checkpoint));
            previousEventId = item.Id.Value;
        }
    }
}
