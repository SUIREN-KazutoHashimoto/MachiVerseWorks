using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class EconomyBenchmarks
{
    private SimulationWorld _world = null!;

    [Params(100, 1_000)]
    public int HouseholdCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 21));
        var home = _world.CreateBuilding(new WorldVolume(0, 0, 0, 10, 10, 10), BuildingKind.Residential);
        var shop = _world.CreateBuilding(new WorldVolume(20, 0, 0, 30, 10, 10), BuildingKind.Commercial);
        var poi = _world.CreatePoi(new WorldPoint(25, 5, 0), PoiKind.Retail, shop);
        var company = _world.CreateCompany(IndustrySector.Retail, initialCashBalance: 100_000_000, dailyProductionCapacity: HouseholdCount * 2d);
        var establishment = _world.CreateEstablishment(company, shop, poi);
        var job = _world.CreateJob(establishment, HouseholdCount, dailyWage: 500);
        for (var index = 0; index < HouseholdCount; index++)
        {
            var household = _world.CreateHousehold(TripEndpoint.ForBuilding(home));
            var person = _world.CreatePerson(household, new PersonDemographics(30, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
            _world.SetHouseholdCashBalance(household, 1_000);
            _world.AssignEmployment(person, job);
        }
    }

    [Benchmark]
    public void Tick() => _world.Step();

    [Benchmark]
    public EconomyStatistics Statistics() => _world.CreateEconomyStatistics();

    [Benchmark]
    public EconomySnapshot Snapshot() => _world.CreateEconomySnapshot();
}
