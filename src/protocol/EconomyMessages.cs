namespace MachiVerseWorks.Protocol;

public enum ProtocolIndustrySector : byte
{
    Generic = 0,
    Retail = 1,
    Services = 2,
    Manufacturing = 3,
    Transport = 4,
    Public = 5,
}

public readonly record struct ProtocolEconomyStatistics(
    uint CompanyCount,
    uint EstablishmentCount,
    uint JobCount,
    uint EmployedPersonCount,
    uint VacantPositionCount,
    long HouseholdCashBalance,
    long HouseholdIncome,
    long HouseholdSpending,
    long CompanyCashBalance,
    long CompanyRevenue,
    long CompanyExpense,
    double ProducedUnits,
    ulong EconomicCycle,
    ulong TickCount);

public readonly record struct ProtocolCompanyEconomy(
    ulong CompanyId,
    ProtocolIndustrySector Sector,
    long CashBalance,
    long Revenue,
    long Expense,
    double DailyProductionCapacity,
    double ProducedUnits,
    uint EstablishmentCount,
    uint EmployeeCount);

public readonly record struct ProtocolHouseholdEconomy(
    ulong HouseholdId,
    long CashBalance,
    long Income,
    long Spending);

public sealed record EconomySnapshotMessage(
    ProtocolEconomyStatistics Statistics,
    IReadOnlyList<ProtocolCompanyEconomy> Companies,
    IReadOnlyList<ProtocolHouseholdEconomy> Households) : IProtocolMessage
{
    public MessageType Type => MessageType.EconomySnapshot;
}
