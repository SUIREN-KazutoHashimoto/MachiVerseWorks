using System.Text;
using System.Text.Json;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Persistence.Tests;

[TestClass]
public sealed class EconomySaveTests
{
    [TestMethod]
    public void SaveV11PreservesEconomyAndDeterministicContinuation()
    {
        var original = CreateWorld(out var companyId, out var householdId);
        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay + 50UL; tick++) original.Step();

        var bytes = WorldSaveSerializer.Serialize(original);
        StringAssert.Contains(Encoding.UTF8.GetString(bytes), "\"formatVersion\": 12");
        StringAssert.Contains(Encoding.UTF8.GetString(bytes), "\"economy\"");
        var restored = WorldSaveSerializer.Deserialize(bytes);

        Assert.IsTrue(original.TryGetCompanySnapshot(companyId, out var expectedCompany));
        Assert.IsTrue(restored.TryGetCompanySnapshot(companyId, out var actualCompany));
        Assert.AreEqual(expectedCompany, actualCompany);
        Assert.IsTrue(original.TryGetHouseholdEconomySnapshot(householdId, out var expectedHousehold));
        Assert.IsTrue(restored.TryGetHouseholdEconomySnapshot(householdId, out var actualHousehold));
        Assert.AreEqual(expectedHousehold, actualHousehold);

        for (ulong tick = 0; tick < EconomyDefaults.TicksPerEconomicDay; tick++) { original.Step(); restored.Step(); }
        Assert.AreEqual(original.CreateEconomyStatistics(), restored.CreateEconomyStatistics());
        Assert.AreEqual(original.CreateCompany(), restored.CreateCompany());
    }

    [TestMethod]
    public void SaveV10MigratesWithEmptyEconomy()
    {
        var world = new SimulationWorld();
        var currentJson = WorldSaveSerializer.Serialize(world);
        using var document = JsonDocument.Parse(currentJson);
        var simulation = document.RootElement.GetProperty("simulation");
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output))
        {
            writer.WriteStartObject();
            writer.WriteNumber("formatVersion", 10);
            writer.WritePropertyName("simulation");
            writer.WriteStartObject();
            foreach (var property in simulation.EnumerateObject())
            {
                if (property.NameEquals("economy")) continue;
                property.WriteTo(writer);
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        var restored = WorldSaveSerializer.Deserialize(output.ToArray());
        var economy = restored.CreateEconomySnapshot();
        Assert.AreEqual(0, economy.Companies.Count);
        Assert.AreEqual(0, economy.Jobs.Count);
        Assert.AreEqual(0, economy.Employments.Count);
        Assert.AreEqual(0, economy.Households.Count);
    }

    private static SimulationWorld CreateWorld(out CompanyId companyId, out HouseholdId householdId)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 21));
        var home = world.CreateBuilding(new WorldVolume(0, 0, 0, 4, 4, 4), BuildingKind.Residential);
        var shop = world.CreateBuilding(new WorldVolume(20, 0, 0, 24, 4, 4), BuildingKind.Commercial);
        var poi = world.CreatePoi(new WorldPoint(22, 2, 0), PoiKind.Retail, shop);
        householdId = world.CreateHousehold(TripEndpoint.ForBuilding(home));
        var person = world.CreatePerson(householdId, new PersonDemographics(35, IsEmployed: true), [new DailyActivityWindow(ActivityKind.Home, 0, 1440)]);
        world.SetHouseholdCashBalance(householdId, 200);
        companyId = world.CreateCompany(IndustrySector.Retail, 10_000, 10d);
        var establishment = world.CreateEstablishment(companyId, shop, poi);
        var job = world.CreateJob(establishment, 1, 500);
        world.AssignEmployment(person, job);
        return world;
    }
}
