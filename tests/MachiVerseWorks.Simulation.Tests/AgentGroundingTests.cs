using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class AgentGroundingTests
{
    [TestMethod]
    public void CreateGroundedAgentsSnapsAuthoritativePositionsToTerrainSurface()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 450));
        var spawnVolume = new WorldVolume(-500d, -500d, -64d, 500d, 500d, 64d);

        var ids = world.CreateGroundedAgents(64, spawnVolume);

        Assert.AreEqual(64, ids.Length);
        foreach (var id in ids)
        {
            Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
            var terrain = world.QueryTerrainSurface(snapshot.Position.X, snapshot.Position.Y);
            Assert.AreEqual(terrain.Position.Z, snapshot.Position.Z, 1e-9, $"Agent {id.Value} was not grounded to the authoritative terrain surface.");
        }
    }

    [TestMethod]
    public void CreateAgentsRetainsNativeThreeDimensionalSpawnContract()
    {
        var world = new SimulationWorld(new SimulationConfig(seed: 451));
        const double configuredZ = 1_234d;
        var id = world.CreateAgents(1, new WorldVolume(0d, 0d, configuredZ, 0d, 0d, configuredZ))[0];

        Assert.IsTrue(world.TryGetAgentSnapshot(id, out var snapshot));
        Assert.AreEqual(configuredZ, snapshot.Position.Z, 0d);
        Assert.AreNotEqual(world.QueryTerrainSurface(0d, 0d).Position.Z, snapshot.Position.Z);
    }
}
