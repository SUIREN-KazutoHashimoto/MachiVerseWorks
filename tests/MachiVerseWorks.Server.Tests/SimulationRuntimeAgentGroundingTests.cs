using MachiVerseWorks.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SimulationRuntimeAgentGroundingTests
{
    [TestMethod]
    public void DefaultRuntimeBootstrapGroundsInitialAgentsToAuthoritativeTerrain()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Simulation:Seed"] = "450",
            ["Simulation:InitialAgentCount"] = "32",
            ["Simulation:SpawnVolume:MinX"] = "-500",
            ["Simulation:SpawnVolume:MinY"] = "-500",
            ["Simulation:SpawnVolume:MinZ"] = "-64",
            ["Simulation:SpawnVolume:MaxX"] = "500",
            ["Simulation:SpawnVolume:MaxY"] = "500",
            ["Simulation:SpawnVolume:MaxZ"] = "64",
        }).Build();
        var runtime = new SimulationRuntime(ServerOptions.Load(configuration), configuration);

        var observations = runtime.Read(world => world.CreateAllAgentSnapshots()
            .Select(snapshot => (Snapshot: snapshot, Terrain: world.QueryTerrainSurface(snapshot.Position.X, snapshot.Position.Y)))
            .ToArray());

        Assert.AreEqual(32, observations.Length);
        foreach (var observation in observations)
            Assert.AreEqual(observation.Terrain.Position.Z, observation.Snapshot.Position.Z, 1e-9, $"Agent {observation.Snapshot.Id.Value} was not grounded during Server bootstrap.");
    }
}
