namespace MachiVerseWorks.Simulation;

public readonly record struct CompanyId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct EstablishmentId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct JobId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum IndustrySector : byte
{
    Generic = 0,
    Retail = 1,
    Services = 2,
    Manufacturing = 3,
    Transport = 4,
    Public = 5,
}

public readonly record struct CompanySnapshot(
    CompanyId Id,
    IndustrySector Sector,
    long CashBalance,
    long Revenue,
    long Expense,
    double DailyProductionCapacity,
    double ProducedUnits,
    int EstablishmentCount,
    int EmployeeCount);

public readonly record struct EstablishmentSnapshot(
    EstablishmentId Id,
    CompanyId CompanyId,
    BuildingId? BuildingId,
    PoiId? PoiId,
    TripEndpoint Location);

public readonly record struct JobSnapshot(
    JobId Id,
    EstablishmentId EstablishmentId,
    int RequiredWorkerCount,
    long DailyWage,
    int FilledWorkerCount);

public readonly record struct EmploymentSnapshot(
    PersonId PersonId,
    JobId JobId,
    ulong StartedTick);

public readonly record struct HouseholdEconomySnapshot(
    HouseholdId HouseholdId,
    long CashBalance,
    long Income,
    long Spending);

public readonly record struct EconomyStatistics(
    int CompanyCount,
    int EstablishmentCount,
    int JobCount,
    int EmployedPersonCount,
    int VacantPositionCount,
    long HouseholdCashBalance,
    long HouseholdIncome,
    long HouseholdSpending,
    long CompanyCashBalance,
    long CompanyRevenue,
    long CompanyExpense,
    double ProducedUnits,
    ulong EconomicCycle,
    ulong TickCount);

public sealed record EconomySnapshot(
    EconomyStatistics Statistics,
    IReadOnlyList<CompanySnapshot> Companies,
    IReadOnlyList<EstablishmentSnapshot> Establishments,
    IReadOnlyList<JobSnapshot> Jobs,
    IReadOnlyList<EmploymentSnapshot> Employments,
    IReadOnlyList<HouseholdEconomySnapshot> Households);

public static class EconomyDefaults
{
    public const ulong TicksPerEconomicDay = 600;
    public const long DailyHouseholdConsumption = 100;
    public const int WorkStartMinuteOfDay = 9 * 60;
    public const int WorkEndMinuteOfDay = 17 * 60;
}

public sealed record EconomyCheckpoint(
    ulong NextCompanyId,
    ulong NextEstablishmentId,
    ulong NextJobId,
    ulong ProcessedEconomicCycle,
    IReadOnlyList<SimulationCompanyCheckpoint> Companies,
    IReadOnlyList<SimulationEstablishmentCheckpoint> Establishments,
    IReadOnlyList<SimulationJobCheckpoint> Jobs,
    IReadOnlyList<SimulationEmploymentCheckpoint> Employments,
    IReadOnlyList<SimulationHouseholdEconomyCheckpoint> Households,
    LogisticsCheckpoint? Logistics = null,
    PowerCheckpoint? Power = null,
    WaterSewerCheckpoint? WaterSewer = null);

public readonly record struct SimulationCompanyCheckpoint(
    CompanyId Id,
    IndustrySector Sector,
    long CashBalance,
    long Revenue,
    long Expense,
    double DailyProductionCapacity,
    double ProducedUnits);

public readonly record struct SimulationEstablishmentCheckpoint(
    EstablishmentId Id,
    CompanyId CompanyId,
    BuildingId? BuildingId,
    PoiId? PoiId);

public readonly record struct SimulationJobCheckpoint(
    JobId Id,
    EstablishmentId EstablishmentId,
    int RequiredWorkerCount,
    long DailyWage);

public readonly record struct SimulationEmploymentCheckpoint(
    PersonId PersonId,
    JobId JobId,
    ulong StartedTick);

public readonly record struct SimulationHouseholdEconomyCheckpoint(
    HouseholdId HouseholdId,
    long CashBalance,
    long Income,
    long Spending);
