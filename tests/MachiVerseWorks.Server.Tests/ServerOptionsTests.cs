using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerOptionsTests
{
    [TestMethod]
    public void ConfigurationOverridesNetworkAndThreeDimensionalSimulationDefaults()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:ListenAddress"] = "127.0.0.2",
            ["Server:Port"] = "9000",
            ["Server:SnapshotRate"] = "20",
            ["Server:MaximumSubscriptionCellCount"] = "2048",
            ["Server:ObservationDeliveryTimeoutMilliseconds"] = "2500",
            ["Simulation:TickRate"] = "60",
            ["Simulation:Seed"] = "42",
            ["Simulation:SpatialCellSize"] = "32",
            ["Simulation:InitialAgentCount"] = "123",
            ["Simulation:SpawnVolume:MinX"] = "-10",
            ["Simulation:SpawnVolume:MinY"] = "-20",
            ["Simulation:SpawnVolume:MinZ"] = "-30",
            ["Simulation:SpawnVolume:MaxX"] = "40",
            ["Simulation:SpawnVolume:MaxY"] = "50",
            ["Simulation:SpawnVolume:MaxZ"] = "60",
        }).Build();

        var options = ServerOptions.Load(configuration);

        Assert.AreEqual("127.0.0.2", options.ListenAddress.ToString());
        Assert.AreEqual(9000, options.Port);
        Assert.AreEqual(20, options.SnapshotRate);
        Assert.AreEqual(2048, options.MaximumSubscriptionCellCount);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), options.ObservationDeliveryTimeout);
        Assert.AreEqual(60, options.TickRate);
        Assert.AreEqual(42UL, options.Seed);
        Assert.AreEqual(32d, options.SpatialCellSize);
        Assert.AreEqual(123, options.InitialAgentCount);
        Assert.AreEqual(-30d, options.SpawnMinZ);
        Assert.AreEqual(60d, options.SpawnMaxZ);
    }

    [TestMethod]
    public void DefaultSubscriptionBudgetSupportsUltrawideNativeThreeDimensionalClientFrustum()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();

        var options = ServerOptions.Load(configuration);

        Assert.AreEqual(1_048_576, options.MaximumSubscriptionCellCount);
        Assert.AreEqual(TimeSpan.FromSeconds(5), options.ObservationDeliveryTimeout);
        Assert.AreEqual(0, options.InitialAgentCount);
    }

    [TestMethod]
    public void InvalidPortIsRejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Server:Port"] = "70000" }).Build();
        Assert.ThrowsExactly<InvalidOperationException>(() => ServerOptions.Load(configuration));
    }

    [TestMethod]
    public void InvalidMaximumSubscriptionCellCountIsRejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Server:MaximumSubscriptionCellCount"] = "0" }).Build();
        Assert.ThrowsExactly<InvalidOperationException>(() => ServerOptions.Load(configuration));
    }

    [TestMethod]
    public void InvalidObservationDeliveryTimeoutIsRejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:ObservationDeliveryTimeoutMilliseconds"] = "99",
        }).Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => ServerOptions.Load(configuration));
    }
}
