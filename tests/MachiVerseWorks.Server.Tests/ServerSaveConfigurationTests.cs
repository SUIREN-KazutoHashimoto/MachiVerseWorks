using System.Net.WebSockets;
using MachiVerseWorks.Persistence;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerSaveConfigurationTests
{
    [TestMethod]
    public async Task SaveSimulationConfigControlsSchedulerHandshakeAndSubscriptionGrid()
    {
        var path = Path.Combine(Path.GetTempPath(), $"machiverse-save-{Guid.NewGuid():N}.json");
        try
        {
            var world = new SimulationWorld(new SimulationConfig(tickRate: 12, seed: 77, spatialCellSize: 128));
            world.CreateAgent(new WorldPoint(10, 10, 10), default);
            await using (var stream = File.Create(path)) WorldSaveSerializer.Save(stream, world);

            await using var host = await ServerTestHost.StartAsync(
                initialAgentCount: 0,
                tickRate: 60,
                snapshotRate: 30,
                additionalConfiguration: new Dictionary<string, string?>
                {
                    ["Simulation:SavePath"] = path,
                    ["Simulation:SpatialCellSize"] = "32",
                    ["Server:MaximumSubscriptionCellCount"] = "8",
                });
            var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
            Assert.AreEqual(12, simulation.TickRate);
            Assert.AreEqual(128d, simulation.SpatialCellSize);
            Assert.AreEqual(TimeSpan.FromSeconds(1d / 12d), simulation.TickInterval);

            using var socket = await host.ConnectWebSocketAsync();
            await ServerTestHost.SendAsync(socket, new HelloMessage(), ProtocolVersion.Current);
            var hello = Assert.IsInstanceOfType<HelloAckMessage>((await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message);
            Assert.AreEqual((ushort)12, hello.TickRate);

            await ServerTestHost.SendAsync(socket, new SubscribeVolumeMessage(0, 0, 0, 127, 127, 127), ProtocolVersion.Current);
            var spawn = await ReceiveUntilAsync(socket, static message => message is AgentSpawnMessage);
            Assert.IsInstanceOfType<AgentSpawnMessage>(spawn);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static async Task<IProtocolMessage> ReceiveUntilAsync(ClientWebSocket socket, Func<IProtocolMessage, bool> predicate)
    {
        for (var index = 0; index < 64; index++)
        {
            var message = (await ServerTestHost.ReceiveAsync(socket, TimeSpan.FromSeconds(3))).Message;
            if (predicate(message)) return message;
            if (message is ProtocolErrorMessage error)
                Assert.Fail($"Unexpected protocol error {error.Code}: {string.Join(", ", error.Parameters.Select(static item => $"{item.Key}={item.Value}"))}");
        }
        Assert.Fail("Expected protocol message was not received.");
        return null!;
    }
}
