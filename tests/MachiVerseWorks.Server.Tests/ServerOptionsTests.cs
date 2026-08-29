using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerOptionsTests
{
    [TestMethod]
    public void ConfigurationOverridesNetworkAndSimulationDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:ListenAddress"] = "127.0.0.2",
                ["Server:Port"] = "9000",
                ["Server:SnapshotRate"] = "20",
                ["Simulation:TickRate"] = "60",
                ["Simulation:Seed"] = "42",
                ["Simulation:SpatialCellSize"] = "32",
                ["Simulation:InitialAgentCount"] = "123",
            })
            .Build();

        var options = ServerOptions.Load(configuration);

        Assert.AreEqual("127.0.0.2", options.ListenAddress.ToString());
        Assert.AreEqual(9000, options.Port);
        Assert.AreEqual(20, options.SnapshotRate);
        Assert.AreEqual(60, options.TickRate);
        Assert.AreEqual(42UL, options.Seed);
        Assert.AreEqual(32d, options.SpatialCellSize);
        Assert.AreEqual(123, options.InitialAgentCount);
    }

    [TestMethod]
    public void InvalidPortIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:Port"] = "70000",
            })
            .Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => ServerOptions.Load(configuration));
    }
}
