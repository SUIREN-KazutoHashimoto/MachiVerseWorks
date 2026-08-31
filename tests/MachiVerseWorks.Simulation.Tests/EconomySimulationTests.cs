using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class EconomySimulationTests
{
    [TestMethod]
    public void EconomicCyclePaysWagesProducesGoodsAndConsumesDeterministically()
    {
        var world = CreateEconomicWorld(out var companyId, out var householdId, out _);

        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay * 3UL; tick++) world.Step();

        Assert.IsTrue(world.TryGetCompanySnapshot(companyId, out var company));
        Assert.IsTrue(world.TryGetHouseholdEconomySnapshot(householdId, out var household));
        Assert.AreEqual(8_800L, company.CashBalance);
        Assert.AreEqual(300L, company.Revenue);
        Assert.AreEqual(1_500L, company.Expense);
        Assert.AreEqual(30d, company.ProducedUnits, 1e-9);
        Assert.AreEqual(1_400L, household.CashBalance);
        Assert.AreEqual(1_500L, household.Income);
        Assert.AreEqual(300L, household.Spending);
        Assert.AreEqual(3UL, world.CreateEconomyStatistics().EconomicCycle);
    }

    [TestMethod]
    public void EconomyCheckpointPreservesStableIdsAndDeterministicContinuation()
    {
        var original = CreateEconomicWorld(out _, out _, out _);
        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay * 2UL + 127UL; tick++) original.Step();

        var restored = SimulationWorld.RestoreCheckpoint(original.CreateCheckpoint());
        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay * 2UL; tick++)
        {
            original.Step();
            restored.Step();
        }

        Assert.AreEqual(original.CreateEconomyStatistics(), restored.CreateEconomyStatistics());
        CollectionAssert.AreEqual(
            original.CreateEconomySnapshot().Companies.ToArray(),
            restored.CreateEconomySnapshot().Companies.ToArray());
        CollectionAssert.AreEqual(
            original.CreateEconomySnapshot().Households.ToArray(),
            restored.CreateEconomySnapshot().Households.ToArray());

        var originalNext = original.CreateCompany();
        var restoredNext = restored.CreateCompany();
        Assert.AreEqual(originalNext, restoredNext);
    }

    [TestMethod]
    public void EmploymentUsesEstablishmentAsWorkDestination()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1));
        var home = world.CreateBuilding(new WorldVolume(0, -2, 0, 2, 2, 3), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(18, -2, 0, 22, 2, 3), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0, 0, 0));
        var end = world.CreateRoadNode(new WorldPoint(25, 0, 0));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateRoadAccessPoint(segment, 0.04, home, mode: RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.80, work, mode: RoadAccessMode.Foot);
        var household = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(
            household,
            new PersonDemographics(30, IsEmployed: true),
            [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        var company = world.CreateCompany(IndustrySector.Services, 5_000, 1d);
        var establishment = world.CreateEstablishment(company, buildingId: work);
        var job = world.CreateJob(establishment, requiredWorkerCount: 1, dailyWage: 100);
        world.AssignEmployment(person, job);

        var checkpoint = world.CreateCheckpoint();
        var economicCycle = (9UL * 60UL * 60UL - 1UL) / EconomyDefaults.TicksPerEconomicDay;
        world = SimulationWorld.RestoreCheckpoint(checkpoint with
        {
            TickCount = 9UL * 60UL * 60UL - 1UL,
            ElapsedTicks = (long)(9UL * 60UL * 60UL - 1UL) * TimeSpan.TicksPerSecond,
            Economy = checkpoint.Economy! with { ProcessedEconomicCycle = economicCycle },
        });

        world.Step();

        Assert.IsTrue(world.TryGetPersonSnapshot(person, out var snapshot));
        Assert.AreEqual(PersonTravelState.Walking, snapshot.TravelState);
        Assert.AreEqual(TripEndpoint.ForBuilding(work), snapshot.Destination);
        Assert.AreEqual(ActivityKind.Work, snapshot.DestinationActivity);
    }

    private static SimulationWorld CreateEconomicWorld(
        out CompanyId companyId,
        out HouseholdId householdId,
        out PersonId personId)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 21));
        var home = world.CreateBuilding(new WorldVolume(0, 0, 0, 4, 4, 4), BuildingKind.Residential);
        var shop = world.CreateBuilding(new WorldVolume(20, 0, 0, 24, 4, 4), BuildingKind.Commercial);
        var retailPoi = world.CreatePoi(new WorldPoint(22, 2, 0), PoiKind.Retail, shop);
        householdId = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        personId = world.CreatePerson(
            householdId,
            new PersonDemographics(35, IsEmployed: true),
            [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        world.SetHouseholdCashBalance(householdId, 200);
        companyId = world.CreateCompany(IndustrySector.Retail, initialCashBalance: 10_000, dailyProductionCapacity: 10d);
        var establishment = world.CreateEstablishment(companyId, buildingId: shop, poiId: retailPoi);
        var job = world.CreateJob(establishment, requiredWorkerCount: 1, dailyWage: 500);
        world.AssignEmployment(personId, job);
        return world;
    }
}
