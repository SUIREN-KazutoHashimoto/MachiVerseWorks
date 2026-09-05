using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class EconomyMessageMapper
{
    private const int MaximumDebugEntries = 256;

    public static EconomySnapshotMessage Create(EconomySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var statistics = snapshot.Statistics;
        var protocolStatistics = new ProtocolEconomyStatistics(
            checked((uint)statistics.CompanyCount),
            checked((uint)statistics.EstablishmentCount),
            checked((uint)statistics.JobCount),
            checked((uint)statistics.EmployedPersonCount),
            checked((uint)statistics.VacantPositionCount),
            statistics.HouseholdCashBalance,
            statistics.HouseholdIncome,
            statistics.HouseholdSpending,
            statistics.CompanyCashBalance,
            statistics.CompanyRevenue,
            statistics.CompanyExpense,
            statistics.ProducedUnits,
            statistics.EconomicCycle,
            statistics.TickCount);

        var companies = snapshot.Companies.Take(MaximumDebugEntries).Select(static company => new ProtocolCompanyEconomy(
            company.Id.Value,
            (ProtocolIndustrySector)company.Sector,
            company.CashBalance,
            company.Revenue,
            company.Expense,
            company.DailyProductionCapacity,
            company.ProducedUnits,
            checked((uint)company.EstablishmentCount),
            checked((uint)company.EmployeeCount))).ToArray();
        var households = snapshot.Households.Take(MaximumDebugEntries).Select(static household => new ProtocolHouseholdEconomy(
            household.HouseholdId.Value,
            household.CashBalance,
            household.Income,
            household.Spending)).ToArray();
        return new EconomySnapshotMessage(protocolStatistics, Array.AsReadOnly(companies), Array.AsReadOnly(households));
    }
}
