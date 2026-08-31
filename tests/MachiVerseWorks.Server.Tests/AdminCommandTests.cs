using MachiVerseWorks.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class AdminCommandTests
{
    [TestMethod]
    public void ParserSupportsQuotedTokensAndOptions()
    {
        var parsed = AdminCommandParser.TryParse("world save \"saves/city one.json\" --force=true", out var command, out var error);
        Assert.IsTrue(parsed, error?.Message);
        Assert.IsNotNull(command);
        Assert.AreEqual("world", command.Name);
        CollectionAssert.AreEqual(new[] { "save", "saves/city one.json" }, command.Arguments.ToArray());
        Assert.AreEqual("true", command.Options["force"]);
    }

    [TestMethod]
    public void ParserRejectsUnterminatedQuotedToken()
    {
        var parsed = AdminCommandParser.TryParse("world save \"broken", out var command, out var error);
        Assert.IsFalse(parsed);
        Assert.IsNull(command);
        Assert.IsNotNull(error);
        Assert.AreEqual(AdminCommandResultCode.InvalidSyntax, error.Code);
    }

    [TestMethod]
    public void BoundedQueueReportsFullWithoutBlockingProducer()
    {
        var queue = new AdminCommandQueue();
        for (var index = 0; index < AdminCommandQueue.Capacity; index++)
            Assert.IsTrue(queue.TryWrite(Request(new AdminCommand("status", [], new Dictionary<string, string?>(), "status"))));
        Assert.IsFalse(queue.TryWrite(Request(new AdminCommand("status", [], new Dictionary<string, string?>(), "status"))));
    }

    [TestMethod]
    public async Task QueuePreservesFifoOrder()
    {
        var queue = new AdminCommandQueue();
        Assert.IsTrue(queue.TryWrite(Request(new AdminCommand("status", [], new Dictionary<string, string?>(), "first"))));
        Assert.IsTrue(queue.TryWrite(Request(new AdminCommand("version", [], new Dictionary<string, string?>(), "second"))));

        await using var enumerator = queue.ReadAllAsync(CancellationToken.None).GetAsyncEnumerator();
        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual("first", enumerator.Current.Command.RawText);
        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreEqual("second", enumerator.Current.Command.RawText);
    }

    [TestMethod]
    public void PauseStepResumeHasDeterministicTickOrdering()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Simulation:InitialAgentCount"] = "0",
            ["Simulation:TickRate"] = "30",
        }).Build();
        var runtime = new SimulationRuntime(ServerOptions.Load(configuration), configuration);

        runtime.Step();
        Assert.AreEqual(1UL, runtime.TickCount);
        Assert.IsTrue(runtime.Pause());
        runtime.Step();
        Assert.AreEqual(1UL, runtime.TickCount);
        Assert.AreEqual(3UL, runtime.StepPaused(2));
        Assert.IsTrue(runtime.Resume());
        runtime.Step();
        Assert.AreEqual(4UL, runtime.TickCount);
    }

    [TestMethod]
    public void RoadMutationIncrementsPublishedReadModelRevision()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Simulation:InitialAgentCount"] = "0",
        }).Build();
        var runtime = new SimulationRuntime(ServerOptions.Load(configuration), configuration);
        var before = runtime.CapturePublishSnapshot().RoadNetwork.Revision;

        runtime.Mutate(world => world.CreateRoadNode(new WorldPoint(1, 2, 3)), roadTopologyChanged: true);

        var after = runtime.CapturePublishSnapshot().RoadNetwork.Revision;
        Assert.IsTrue(after > before);
    }

    private static AdminCommandRequest Request(AdminCommand command) => new(command, new TaskCompletionSource<AdminCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously));
}
