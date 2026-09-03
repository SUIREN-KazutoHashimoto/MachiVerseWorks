namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly List<EconomyCompanyState> _economyCompanies = [];
    private readonly Dictionary<CompanyId, EconomyCompanyState> _economyCompanyIndex = [];
    private readonly List<EconomyEstablishmentState> _economyEstablishments = [];
    private readonly Dictionary<EstablishmentId, EconomyEstablishmentState> _economyEstablishmentIndex = [];
    private readonly List<EconomyJobState> _economyJobs = [];
    private readonly Dictionary<JobId, EconomyJobState> _economyJobIndex = [];
    private readonly Dictionary<PersonId, EconomyEmploymentState> _economyEmployments = [];
    private readonly Dictionary<HouseholdId, EconomyHouseholdState> _economyHouseholds = [];
    private ulong _nextCompanyId = 1;
    private ulong _nextEstablishmentId = 1;
    private ulong _nextJobId = 1;
    private ulong _processedEconomicCycle;

    public int CompanyCount => _economyCompanies.Count;
    public int EstablishmentCount => _economyEstablishments.Count;
    public int JobCount => _economyJobs.Count;
    public int EmploymentCount => _economyEmployments.Count;

    public CompanyId CreateCompany(
        IndustrySector sector = IndustrySector.Generic,
        long initialCashBalance = 0,
        double dailyProductionCapacity = 0d)
    {
        ValidateIndustrySector(sector);
        if (initialCashBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCashBalance), initialCashBalance, "Initial company cash balance cannot be negative.");
        if (!double.IsFinite(dailyProductionCapacity) || dailyProductionCapacity < 0d)
            throw new ArgumentOutOfRangeException(nameof(dailyProductionCapacity), dailyProductionCapacity, "Daily production capacity must be finite and non-negative.");
        EnsureEconomyIdCapacity(_nextCompanyId, "Company");
        var id = new CompanyId(_nextCompanyId++);
        var state = new EconomyCompanyState(id, sector, initialCashBalance, dailyProductionCapacity);
        _economyCompanyIndex.Add(id, state);
        _economyCompanies.Add(state);
        return id;
    }

    public EstablishmentId CreateEstablishment(
        CompanyId companyId,
        BuildingId? buildingId = null,
        PoiId? poiId = null)
    {
        if (!_economyCompanyIndex.ContainsKey(companyId))
            throw new ArgumentException($"Company {companyId.Value} does not exist.", nameof(companyId));
        if (buildingId is null && poiId is null)
            throw new ArgumentException("An Establishment must reference a Building, a POI, or both.", nameof(buildingId));

        if (buildingId is { } linkedBuildingId && !TryGetBuildingSnapshot(linkedBuildingId, out _))
            throw new ArgumentException($"Building {linkedBuildingId.Value} does not exist.", nameof(buildingId));

        if (poiId is { } linkedPoiId)
        {
            if (!TryGetPoiSnapshot(linkedPoiId, out var poi))
                throw new ArgumentException($"POI {linkedPoiId.Value} does not exist.", nameof(poiId));
            if (buildingId is { } requestedBuildingId && poi.BuildingId is { } poiBuildingId && requestedBuildingId != poiBuildingId)
                throw new ArgumentException($"POI {linkedPoiId.Value} belongs to Building {poiBuildingId.Value}, not Building {requestedBuildingId.Value}.", nameof(buildingId));
            buildingId ??= poi.BuildingId;
        }

        EnsureEconomyIdCapacity(_nextEstablishmentId, "Establishment");
        var id = new EstablishmentId(_nextEstablishmentId++);
        var state = new EconomyEstablishmentState(id, companyId, buildingId, poiId);
        _economyEstablishmentIndex.Add(id, state);
        _economyEstablishments.Add(state);
        return id;
    }

    public JobId CreateJob(EstablishmentId establishmentId, int requiredWorkerCount, long dailyWage)
    {
        if (!_economyEstablishmentIndex.ContainsKey(establishmentId))
            throw new ArgumentException($"Establishment {establishmentId.Value} does not exist.", nameof(establishmentId));
        if (requiredWorkerCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(requiredWorkerCount), requiredWorkerCount, "Required worker count must be greater than zero.");
        if (dailyWage < 0)
            throw new ArgumentOutOfRangeException(nameof(dailyWage), dailyWage, "Daily wage cannot be negative.");
        EnsureEconomyIdCapacity(_nextJobId, "Job");
        var id = new JobId(_nextJobId++);
        var state = new EconomyJobState(id, establishmentId, requiredWorkerCount, dailyWage);
        _economyJobIndex.Add(id, state);
        _economyJobs.Add(state);
        return id;
    }

    public void AssignEmployment(PersonId personId, JobId jobId)
    {
        if (!_population.TryGetPerson(personId, out _))
            throw new ArgumentException($"Person {personId.Value} does not exist.", nameof(personId));
        if (!_economyJobIndex.TryGetValue(jobId, out var job))
            throw new ArgumentException($"Job {jobId.Value} does not exist.", nameof(jobId));
        if (_economyEmployments.ContainsKey(personId))
            throw new InvalidOperationException($"Person {personId.Value} already has an Employment.");
        if (GetFilledWorkerCount(jobId) >= job.RequiredWorkerCount)
            throw new InvalidOperationException($"Job {jobId.Value} has no vacant positions.");
        _economyEmployments.Add(personId, new EconomyEmploymentState(personId, jobId, Time.TickCount));
    }

    public bool EndEmployment(PersonId personId) => _economyEmployments.Remove(personId);

    public void SetHouseholdCashBalance(HouseholdId householdId, long cashBalance)
    {
        if (!_population.TryGetHousehold(householdId, out _))
            throw new ArgumentException($"Household {householdId.Value} does not exist.", nameof(householdId));
        if (cashBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(cashBalance), cashBalance, "Household cash balance cannot be negative.");
        EnsureHouseholdEconomyState(householdId).CashBalance = cashBalance;
    }

    public bool TryGetCompanySnapshot(CompanyId id, out CompanySnapshot snapshot)
    {
        if (_economyCompanyIndex.TryGetValue(id, out var state))
        {
            snapshot = CreateCompanySnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetEstablishmentSnapshot(EstablishmentId id, out EstablishmentSnapshot snapshot)
    {
        if (_economyEstablishmentIndex.TryGetValue(id, out var state))
        {
            snapshot = CreateEstablishmentSnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetJobSnapshot(JobId id, out JobSnapshot snapshot)
    {
        if (_economyJobIndex.TryGetValue(id, out var state))
        {
            snapshot = CreateJobSnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetEmploymentSnapshot(PersonId personId, out EmploymentSnapshot snapshot)
    {
        if (_economyEmployments.TryGetValue(personId, out var state))
        {
            snapshot = new EmploymentSnapshot(state.PersonId, state.JobId, state.StartedTick);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetHouseholdEconomySnapshot(HouseholdId householdId, out HouseholdEconomySnapshot snapshot)
    {
        if (_economyHouseholds.TryGetValue(householdId, out var state))
        {
            snapshot = new HouseholdEconomySnapshot(state.HouseholdId, state.CashBalance, state.Income, state.Spending);
            return true;
        }
        snapshot = default;
        return false;
    }

    public EconomySnapshot CreateEconomySnapshot()
    {
        var companies = _economyCompanies.OrderBy(static item => item.Id.Value).Select(CreateCompanySnapshot).ToArray();
        var establishments = _economyEstablishments.OrderBy(static item => item.Id.Value).Select(CreateEstablishmentSnapshot).ToArray();
        var jobs = _economyJobs.OrderBy(static item => item.Id.Value).Select(CreateJobSnapshot).ToArray();
        var employments = _economyEmployments.Values.OrderBy(static item => item.PersonId.Value)
            .Select(static item => new EmploymentSnapshot(item.PersonId, item.JobId, item.StartedTick)).ToArray();
        var households = _economyHouseholds.Values.OrderBy(static item => item.HouseholdId.Value)
            .Select(static item => new HouseholdEconomySnapshot(item.HouseholdId, item.CashBalance, item.Income, item.Spending)).ToArray();
        return new EconomySnapshot(CreateEconomyStatistics(), companies, establishments, jobs, employments, households);
    }

    public EconomyStatistics CreateEconomyStatistics()
    {
        long vacantPositions = 0;
        for (var index = 0; index < _economyJobs.Count; index++)
        {
            var job = _economyJobs[index];
            vacantPositions += Math.Max(0, job.RequiredWorkerCount - GetFilledWorkerCount(job.Id));
        }

        return new EconomyStatistics(
            _economyCompanies.Count,
            _economyEstablishments.Count,
            _economyJobs.Count,
            _economyEmployments.Count,
            SimulationNumeric.SaturatingToInt32NonNegative(vacantPositions),
            SimulationNumeric.SaturatingLongSum(_economyHouseholds.Values, static item => item.CashBalance),
            SimulationNumeric.SaturatingLongSum(_economyHouseholds.Values, static item => item.Income),
            SimulationNumeric.SaturatingLongSum(_economyHouseholds.Values, static item => item.Spending),
            SimulationNumeric.SaturatingLongSum(_economyCompanies, static item => item.CashBalance),
            SimulationNumeric.SaturatingLongSum(_economyCompanies, static item => item.Revenue),
            SimulationNumeric.SaturatingLongSum(_economyCompanies, static item => item.Expense),
            SimulationNumeric.SaturatingDoubleSum(_economyCompanies, static item => item.ProducedUnits),
            _processedEconomicCycle,
            Time.TickCount);
    }

    private void StepEconomy(SimulationTime nextTime)
    {
        var targetCycle = nextTime.TickCount / EconomyDefaults.TicksPerEconomicDay;
        while (_processedEconomicCycle < targetCycle)
        {
            ProcessEconomicCycle();
            _processedEconomicCycle++;
        }
    }

    private void ProcessEconomicCycle()
    {
        var checkpoint = CreateEconomyCheckpoint();
        try
        {
            ProduceGoods();
            PayWages();
            ProcessHouseholdConsumption();
        }
        catch
        {
            RestoreEconomy(checkpoint);
            throw;
        }
    }

    private void ProduceGoods()
    {
        foreach (var company in _economyCompanies.OrderBy(static item => item.Id.Value))
        {
            long requiredWorkers = 0;
            long filledWorkers = 0;
            for (var index = 0; index < _economyJobs.Count; index++)
            {
                var job = _economyJobs[index];
                if (!_economyEstablishmentIndex.TryGetValue(job.EstablishmentId, out var establishment) || establishment.CompanyId != company.Id)
                    continue;
                requiredWorkers += job.RequiredWorkerCount;
                filledWorkers += GetFilledWorkerCount(job.Id);
            }
            var utilization = requiredWorkers == 0 ? 0d : Math.Min(1d, (double)filledWorkers / requiredWorkers);
            var producedUnits = company.ProducedUnits + (company.DailyProductionCapacity * utilization);
            if (!double.IsFinite(producedUnits) || producedUnits < 0d)
                throw new OverflowException($"Company {company.Id.Value} production would exceed the finite numeric range.");
            company.ProducedUnits = producedUnits;
        }
    }

    private void PayWages()
    {
        foreach (var employment in _economyEmployments.Values.OrderBy(static item => item.PersonId.Value))
        {
            if (!_economyJobIndex.TryGetValue(employment.JobId, out var job)
                || !_economyEstablishmentIndex.TryGetValue(job.EstablishmentId, out var establishment)
                || !_economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company)
                || !_population.TryGetPerson(employment.PersonId, out var person))
                continue;

            var payment = Math.Min(company.CashBalance, job.DailyWage);
            if (payment <= 0) continue;
            var household = EnsureHouseholdEconomyState(person.HouseholdId);
            company.CashBalance = checked(company.CashBalance - payment);
            company.Expense = checked(company.Expense + payment);
            household.CashBalance = checked(household.CashBalance + payment);
            household.Income = checked(household.Income + payment);
        }
    }

    private void ProcessHouseholdConsumption()
    {
        var establishment = _economyEstablishments
            .OrderBy(static item => item.Id.Value)
            .FirstOrDefault(IsCommercialEstablishment);
        if (establishment is null || !_economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company)) return;

        foreach (var household in _economyHouseholds.Values.OrderBy(static item => item.HouseholdId.Value))
        {
            var spending = Math.Min(household.CashBalance, EconomyDefaults.DailyHouseholdConsumption);
            if (spending <= 0) continue;
            household.CashBalance = checked(household.CashBalance - spending);
            household.Spending = checked(household.Spending + spending);
            company.CashBalance = checked(company.CashBalance + spending);
            company.Revenue = checked(company.Revenue + spending);
        }
    }

    private bool IsCommercialEstablishment(EconomyEstablishmentState establishment)
    {
        if (establishment.PoiId is { } poiId && TryGetPoiSnapshot(poiId, out var poi) && poi.Kind is PoiKind.Retail or PoiKind.Service)
            return true;
        if (establishment.BuildingId is { } buildingId && TryGetBuildingSnapshot(buildingId, out var building)
            && building.Kind is BuildingKind.Commercial or BuildingKind.MixedUse)
            return true;
        return _economyCompanyIndex.TryGetValue(establishment.CompanyId, out var company)
            && company.Sector is IndustrySector.Retail or IndustrySector.Services;
    }

    private bool TryGetEmploymentWorkplace(PersonId personId, out TripEndpoint workplace)
    {
        if (_economyEmployments.TryGetValue(personId, out var employment)
            && _economyJobIndex.TryGetValue(employment.JobId, out var job)
            && _economyEstablishmentIndex.TryGetValue(job.EstablishmentId, out var establishment))
        {
            workplace = CreateEstablishmentLocation(establishment);
            return true;
        }
        workplace = default;
        return false;
    }

    private EconomyHouseholdState EnsureHouseholdEconomyState(HouseholdId householdId)
    {
        if (_economyHouseholds.TryGetValue(householdId, out var existing)) return existing;
        var state = new EconomyHouseholdState(householdId);
        _economyHouseholds.Add(householdId, state);
        return state;
    }

    private CompanySnapshot CreateCompanySnapshot(EconomyCompanyState state)
    {
        var establishmentCount = _economyEstablishments.Count(item => item.CompanyId == state.Id);
        var employeeCount = _economyEmployments.Values.Count(item =>
            _economyJobIndex.TryGetValue(item.JobId, out var job)
            && _economyEstablishmentIndex.TryGetValue(job.EstablishmentId, out var establishment)
            && establishment.CompanyId == state.Id);
        return new CompanySnapshot(
            state.Id,
            state.Sector,
            state.CashBalance,
            state.Revenue,
            state.Expense,
            state.DailyProductionCapacity,
            state.ProducedUnits,
            establishmentCount,
            employeeCount);
    }

    private EstablishmentSnapshot CreateEstablishmentSnapshot(EconomyEstablishmentState state) => new(
        state.Id,
        state.CompanyId,
        state.BuildingId,
        state.PoiId,
        CreateEstablishmentLocation(state));

    private JobSnapshot CreateJobSnapshot(EconomyJobState state) => new(
        state.Id,
        state.EstablishmentId,
        state.RequiredWorkerCount,
        state.DailyWage,
        GetFilledWorkerCount(state.Id));

    private static TripEndpoint CreateEstablishmentLocation(EconomyEstablishmentState state) =>
        state.PoiId is { } poiId ? TripEndpoint.ForPoi(poiId) : TripEndpoint.ForBuilding(state.BuildingId!.Value);

    private int GetFilledWorkerCount(JobId jobId) => _economyEmployments.Values.Count(item => item.JobId == jobId);

    private bool ContainsEconomyBuildingReference(BuildingId id) => _economyEstablishments.Any(item => item.BuildingId == id);
    private bool ContainsEconomyPoiReference(PoiId id) => _economyEstablishments.Any(item => item.PoiId == id);

    private EconomyCheckpoint CreateEconomyCheckpoint() => new(
        _nextCompanyId,
        _nextEstablishmentId,
        _nextJobId,
        _processedEconomicCycle,
        _economyCompanies.OrderBy(static item => item.Id.Value).Select(static item => new SimulationCompanyCheckpoint(
            item.Id, item.Sector, item.CashBalance, item.Revenue, item.Expense, item.DailyProductionCapacity, item.ProducedUnits)).ToArray(),
        _economyEstablishments.OrderBy(static item => item.Id.Value).Select(static item => new SimulationEstablishmentCheckpoint(
            item.Id, item.CompanyId, item.BuildingId, item.PoiId)).ToArray(),
        _economyJobs.OrderBy(static item => item.Id.Value).Select(static item => new SimulationJobCheckpoint(
            item.Id, item.EstablishmentId, item.RequiredWorkerCount, item.DailyWage)).ToArray(),
        _economyEmployments.Values.OrderBy(static item => item.PersonId.Value).Select(static item => new SimulationEmploymentCheckpoint(
            item.PersonId, item.JobId, item.StartedTick)).ToArray(),
        _economyHouseholds.Values.OrderBy(static item => item.HouseholdId.Value).Select(static item => new SimulationHouseholdEconomyCheckpoint(
            item.HouseholdId, item.CashBalance, item.Income, item.Spending)).ToArray());

    private void RestoreEconomy(EconomyCheckpoint? checkpoint)
    {
        _economyCompanies.Clear();
        _economyCompanyIndex.Clear();
        _economyEstablishments.Clear();
        _economyEstablishmentIndex.Clear();
        _economyJobs.Clear();
        _economyJobIndex.Clear();
        _economyEmployments.Clear();
        _economyHouseholds.Clear();
        _nextCompanyId = 1;
        _nextEstablishmentId = 1;
        _nextJobId = 1;
        _processedEconomicCycle = 0;
        if (checkpoint is null)
        {
            for (var index = 0; index < _population.HouseholdCount; index++)
                EnsureHouseholdEconomyState(_population.GetHouseholdAt(index).Id);
            return;
        }

        foreach (var item in checkpoint.Companies)
        {
            var state = new EconomyCompanyState(item.Id, item.Sector, item.CashBalance, item.DailyProductionCapacity)
            {
                Revenue = item.Revenue,
                Expense = item.Expense,
                ProducedUnits = item.ProducedUnits,
            };
            _economyCompanies.Add(state);
            _economyCompanyIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Establishments)
        {
            var state = new EconomyEstablishmentState(item.Id, item.CompanyId, item.BuildingId, item.PoiId);
            _economyEstablishments.Add(state);
            _economyEstablishmentIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Jobs)
        {
            var state = new EconomyJobState(item.Id, item.EstablishmentId, item.RequiredWorkerCount, item.DailyWage);
            _economyJobs.Add(state);
            _economyJobIndex.Add(state.Id, state);
        }
        foreach (var item in checkpoint.Employments)
            _economyEmployments.Add(item.PersonId, new EconomyEmploymentState(item.PersonId, item.JobId, item.StartedTick));
        foreach (var item in checkpoint.Households)
            _economyHouseholds.Add(item.HouseholdId, new EconomyHouseholdState(item.HouseholdId)
            {
                CashBalance = item.CashBalance,
                Income = item.Income,
                Spending = item.Spending,
            });
        _nextCompanyId = checkpoint.NextCompanyId;
        _nextEstablishmentId = checkpoint.NextEstablishmentId;
        _nextJobId = checkpoint.NextJobId;
        _processedEconomicCycle = checkpoint.ProcessedEconomicCycle;
    }

    private static void ValidateEconomyCheckpoint(SimulationCheckpoint checkpoint)
    {
        var economy = checkpoint.Economy;
        if (economy is null) return;
        if (economy.NextCompanyId == 0 || economy.NextEstablishmentId == 0 || economy.NextJobId == 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Economy next IDs must be greater than zero.");
        if (economy.ProcessedEconomicCycle > checkpoint.TickCount / EconomyDefaults.TicksPerEconomicDay)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), "Processed economic cycle cannot be ahead of simulation time.");

        var companyIds = new HashSet<CompanyId>();
        var maxCompanyId = 0UL;
        foreach (var company in economy.Companies)
        {
            if (company.Id.Value == 0 || !companyIds.Add(company.Id)) throw new ArgumentException("Economy contains an invalid or duplicate Company ID.", nameof(checkpoint));
            ValidateIndustrySector(company.Sector);
            if (company.CashBalance < 0 || company.Revenue < 0 || company.Expense < 0
                || !double.IsFinite(company.DailyProductionCapacity) || company.DailyProductionCapacity < 0d
                || !double.IsFinite(company.ProducedUnits) || company.ProducedUnits < 0d)
                throw new ArgumentException($"Company {company.Id.Value} contains invalid economic values.", nameof(checkpoint));
            maxCompanyId = Math.Max(maxCompanyId, company.Id.Value);
        }
        if (economy.NextCompanyId <= maxCompanyId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Company ID must be greater than stored Company IDs.");

        var buildingIds = (checkpoint.Buildings ?? []).Select(static item => item.Id).ToHashSet();
        var poiIds = (checkpoint.Pois ?? []).Select(static item => item.Id).ToHashSet();
        var establishmentIds = new HashSet<EstablishmentId>();
        var maxEstablishmentId = 0UL;
        foreach (var establishment in economy.Establishments)
        {
            if (establishment.Id.Value == 0 || !establishmentIds.Add(establishment.Id)) throw new ArgumentException("Economy contains an invalid or duplicate Establishment ID.", nameof(checkpoint));
            if (!companyIds.Contains(establishment.CompanyId)) throw new ArgumentException($"Establishment {establishment.Id.Value} references a missing Company.", nameof(checkpoint));
            if (establishment.BuildingId is null && establishment.PoiId is null) throw new ArgumentException($"Establishment {establishment.Id.Value} has no placement.", nameof(checkpoint));
            if (establishment.BuildingId is { } buildingId && !buildingIds.Contains(buildingId)) throw new ArgumentException($"Establishment {establishment.Id.Value} references a missing Building.", nameof(checkpoint));
            if (establishment.PoiId is { } poiId && !poiIds.Contains(poiId)) throw new ArgumentException($"Establishment {establishment.Id.Value} references a missing POI.", nameof(checkpoint));
            maxEstablishmentId = Math.Max(maxEstablishmentId, establishment.Id.Value);
        }
        if (economy.NextEstablishmentId <= maxEstablishmentId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Establishment ID must be greater than stored Establishment IDs.");

        var jobIds = new HashSet<JobId>();
        var maxJobId = 0UL;
        foreach (var job in economy.Jobs)
        {
            if (job.Id.Value == 0 || !jobIds.Add(job.Id)) throw new ArgumentException("Economy contains an invalid or duplicate Job ID.", nameof(checkpoint));
            if (!establishmentIds.Contains(job.EstablishmentId) || job.RequiredWorkerCount <= 0 || job.DailyWage < 0)
                throw new ArgumentException($"Job {job.Id.Value} contains invalid state.", nameof(checkpoint));
            maxJobId = Math.Max(maxJobId, job.Id.Value);
        }
        if (economy.NextJobId <= maxJobId) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Job ID must be greater than stored Job IDs.");

        var personIds = (checkpoint.Persons ?? []).Select(static item => item.Id).ToHashSet();
        var employmentPersons = new HashSet<PersonId>();
        var filledCounts = new Dictionary<JobId, int>();
        foreach (var employment in economy.Employments)
        {
            if (!personIds.Contains(employment.PersonId) || !jobIds.Contains(employment.JobId) || !employmentPersons.Add(employment.PersonId))
                throw new ArgumentException("Economy contains an invalid Employment reference.", nameof(checkpoint));
            filledCounts[employment.JobId] = filledCounts.GetValueOrDefault(employment.JobId) + 1;
        }
        foreach (var job in economy.Jobs)
            if (filledCounts.GetValueOrDefault(job.Id) > job.RequiredWorkerCount)
                throw new ArgumentException($"Job {job.Id.Value} exceeds its required worker count.", nameof(checkpoint));

        var householdIds = (checkpoint.Households ?? []).Select(static item => item.Id).ToHashSet();
        var economyHouseholds = new HashSet<HouseholdId>();
        foreach (var household in economy.Households)
        {
            if (!householdIds.Contains(household.HouseholdId) || !economyHouseholds.Add(household.HouseholdId)
                || household.CashBalance < 0 || household.Income < 0 || household.Spending < 0)
                throw new ArgumentException("Economy contains an invalid Household economic state.", nameof(checkpoint));
        }
    }

    private static void ValidateIndustrySector(IndustrySector sector)
    {
        if (!Enum.IsDefined(sector)) throw new ArgumentOutOfRangeException(nameof(sector), sector, "Industry sector is not defined.");
    }

    private static void EnsureEconomyIdCapacity(ulong nextId, string name)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted.");
    }

    private sealed class EconomyCompanyState(CompanyId id, IndustrySector sector, long cashBalance, double dailyProductionCapacity)
    {
        public CompanyId Id { get; } = id;
        public IndustrySector Sector { get; } = sector;
        public long CashBalance { get; set; } = cashBalance;
        public long Revenue { get; set; }
        public long Expense { get; set; }
        public double DailyProductionCapacity { get; } = dailyProductionCapacity;
        public double ProducedUnits { get; set; }
    }

    private sealed class EconomyEstablishmentState(CompanyId companyId, BuildingId? buildingId, PoiId? poiId)
    {
        public EconomyEstablishmentState(EstablishmentId id, CompanyId companyId, BuildingId? buildingId, PoiId? poiId)
            : this(companyId, buildingId, poiId) => Id = id;
        public EstablishmentId Id { get; }
        public CompanyId CompanyId { get; } = companyId;
        public BuildingId? BuildingId { get; } = buildingId;
        public PoiId? PoiId { get; } = poiId;
    }

    private sealed class EconomyJobState(EstablishmentId establishmentId, int requiredWorkerCount, long dailyWage)
    {
        public EconomyJobState(JobId id, EstablishmentId establishmentId, int requiredWorkerCount, long dailyWage)
            : this(establishmentId, requiredWorkerCount, dailyWage) => Id = id;
        public JobId Id { get; }
        public EstablishmentId EstablishmentId { get; } = establishmentId;
        public int RequiredWorkerCount { get; } = requiredWorkerCount;
        public long DailyWage { get; } = dailyWage;
    }

    private sealed class EconomyEmploymentState(PersonId personId, JobId jobId, ulong startedTick)
    {
        public PersonId PersonId { get; } = personId;
        public JobId JobId { get; } = jobId;
        public ulong StartedTick { get; } = startedTick;
    }

    private sealed class EconomyHouseholdState(HouseholdId householdId)
    {
        public HouseholdId HouseholdId { get; } = householdId;
        public long CashBalance { get; set; }
        public long Income { get; set; }
        public long Spending { get; set; }
    }
}
