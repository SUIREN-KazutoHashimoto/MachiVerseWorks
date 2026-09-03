using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationCacheTests
{
    private static readonly WorldVolume TestVolume = new(-32d, -32d, -8d, 32d, 32d, 8d);

    [TestMethod]
    public void DisabledMissAndHitPathsProduceEquivalentValues()
    {
        var revision = new ObservationRevision(1, 7);
        var key = new SpatialObservationCacheKey(SpatialObservationKind.Entities, TestVolume, revision);
        var enabledBuilds = 0;
        var enabled = new ObservationCache();

        var miss = enabled.GetOrCreateSpatial(key, () => new CacheValue(++enabledBuilds));
        var hit = enabled.GetOrCreateSpatial(key, () => new CacheValue(++enabledBuilds));

        Assert.AreEqual(miss, hit);
        Assert.AreSame(miss, hit);
        Assert.AreEqual(1, enabledBuilds);

        var disabledBuilds = 0;
        var disabled = new ObservationCache(ObservationCacheOptions.Disabled);
        var disabledFirst = disabled.GetOrCreateSpatial(key, () => new CacheValue(++disabledBuilds));
        var disabledSecond = disabled.GetOrCreateSpatial(key, () => new CacheValue(disabledBuilds));

        Assert.AreEqual(miss.Value, disabledFirst.Value);
        Assert.AreEqual(miss.Value, disabledSecond.Value);
        Assert.AreEqual(1, disabledBuilds);
        Assert.AreNotSame(disabledFirst, disabledSecond);
    }

    [TestMethod]
    public async Task SameRevisionConcurrentRequestsBuildOnce()
    {
        var cache = new ObservationCache();
        var key = new SpatialObservationCacheKey(SpatialObservationKind.Entities, TestVolume, new ObservationRevision(1, 12));
        using var factoryStarted = new ManualResetEventSlim();
        using var releaseFactory = new ManualResetEventSlim();
        var buildCount = 0;

        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() => cache.GetOrCreateSpatial(key, () =>
        {
            Interlocked.Increment(ref buildCount);
            factoryStarted.Set();
            releaseFactory.Wait();
            return new CacheValue(42);
        }))).ToArray();

        Assert.IsTrue(factoryStarted.Wait(TimeSpan.FromSeconds(5)));
        releaseFactory.Set();
        var values = await Task.WhenAll(tasks);

        Assert.AreEqual(1, buildCount);
        foreach (var value in values) Assert.AreSame(values[0], value);
        var metrics = cache.CreateMetricsSnapshot();
        Assert.AreEqual(1L, metrics.Builds);
        Assert.AreEqual(1L, metrics.Misses);
        Assert.AreEqual(7L, metrics.Hits);
    }

    [TestMethod]
    public void RevisionAndGenerationChangesRebuildDynamicEntriesWithoutExpiringStaticEntriesByTick()
    {
        var cache = new ObservationCache(new ObservationCacheOptions(RetainedDynamicRevisions: 1));
        var dynamicOne = new SpatialObservationCacheKey(SpatialObservationKind.Entities, TestVolume, new ObservationRevision(1, 10));
        var dynamicTwo = new SpatialObservationCacheKey(SpatialObservationKind.Entities, TestVolume, new ObservationRevision(1, 11));
        var staticKey = new StaticObservationCacheKey(StaticObservationKind.Road, TestVolume, new ObservationRevision(1, 3));

        var firstDynamic = cache.GetOrCreateSpatial(dynamicOne, () => new CacheValue(10));
        var staticValue = cache.GetOrCreateStatic(staticKey, () => new CacheValue(3));
        var secondDynamic = cache.GetOrCreateSpatial(dynamicTwo, () => new CacheValue(11));
        var staticAfterTick = cache.GetOrCreateStatic(staticKey, () => new CacheValue(99));

        Assert.AreNotSame(firstDynamic, secondDynamic);
        Assert.AreSame(staticValue, staticAfterTick);

        cache.ObserveRevision(new ObservationRevision(2, 1));
        var replacementKey = new StaticObservationCacheKey(StaticObservationKind.Road, TestVolume, new ObservationRevision(2, 3));
        var replacement = cache.GetOrCreateStatic(replacementKey, () => new CacheValue(4));
        Assert.AreNotSame(staticValue, replacement);

        var staleBuildCount = 0;
        _ = cache.GetOrCreateStatic(staticKey, () => new CacheValue(++staleBuildCount));
        _ = cache.GetOrCreateStatic(staticKey, () => new CacheValue(++staleBuildCount));
        Assert.AreEqual(2, staleBuildCount, "Older generations must bypass the shared cache instead of rolling it backwards.");
    }

    [TestMethod]
    public void EncodedPayloadCachePreservesWireBytesAndSeparatesProtocolVersions()
    {
        var cache = new ObservationCache();
        var revision = new ObservationRevision(1, 20);
        var message = new AgentSpawnMessage(7, 1, 2, 3, 4, 5, 6, 20);
        var current = ProtocolVersion.Current;
        var previous = new ProtocolVersion(2, 16);
        var currentKey = new EncodedObservationCacheKey("agent-test", current, revision, "7");
        var previousKey = new EncodedObservationCacheKey("agent-test", previous, revision, "7");

        var currentFirst = cache.GetOrEncode(currentKey, () => ObservationProtocolAdapter.Serialize(message, current));
        var currentHit = cache.GetOrEncode(currentKey, () => throw new AssertFailedException("A cache hit must not re-encode the payload."));
        var previousFrame = cache.GetOrEncode(previousKey, () => ObservationProtocolAdapter.Serialize(message, previous));

        CollectionAssert.AreEqual(ObservationProtocolAdapter.Serialize(message, current), currentFirst);
        CollectionAssert.AreEqual(currentFirst, currentHit);
        CollectionAssert.AreEqual(ObservationProtocolAdapter.Serialize(message, previous), previousFrame);
        Assert.AreSame(currentFirst, currentHit);
        Assert.AreEqual(2L, cache.CreateMetricsSnapshot().Encodings);
    }

    [TestMethod]
    public void EncodedPayloadCacheEnforcesMemoryBudget()
    {
        var cache = new ObservationCache(new ObservationCacheOptions(
            MaxEncodedEntries: 4,
            MaxEncodedBytes: 24));
        var revision = new ObservationRevision(1, 1);

        _ = cache.GetOrEncode(new EncodedObservationCacheKey("memory", ProtocolVersion.Current, revision, "a"), () => new byte[16]);
        _ = cache.GetOrEncode(new EncodedObservationCacheKey("memory", ProtocolVersion.Current, revision, "b"), () => new byte[16]);

        var metrics = cache.CreateMetricsSnapshot();
        Assert.IsTrue(metrics.EncodedBytes <= 24);
        Assert.IsTrue(metrics.EncodedEntries <= 1);
        Assert.IsTrue(metrics.Evictions >= 1);
    }

    [TestMethod]
    public void SimulationRuntimeAdvancesObservationRevisionForMutationAndGenerationForWorldReplacement()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Simulation:InitialAgentCount"] = "0",
                ["Simulation:TickRate"] = "1",
            })
            .Build();
        var runtime = new SimulationRuntime(ServerOptions.Load(configuration), configuration);
        var initial = runtime.CapturePublishSnapshot();

        runtime.Pause();
        _ = runtime.Mutate(
            static world => world.CreateRoadNode(new WorldPoint(10d, 20d, 0d)),
            roadTopologyChanged: true);
        var mutated = runtime.CapturePublishSnapshot();

        Assert.AreEqual(initial.TickCount, mutated.TickCount);
        Assert.AreEqual(initial.ObservationGeneration, mutated.ObservationGeneration);
        Assert.IsTrue(mutated.ObservationRevision > initial.ObservationRevision);

        runtime.ReplaceWorld(new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 9, spatialCellSize: 64d)));
        var replaced = runtime.CapturePublishSnapshot();

        Assert.IsTrue(replaced.ObservationGeneration > mutated.ObservationGeneration);
        Assert.AreEqual(1UL, replaced.ObservationRevision);
    }

    [TestMethod]
    public void FailedTopologyMutationDoesNotAdvanceTopologyOrObservationRevision()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Simulation:InitialAgentCount"] = "0", ["Simulation:TickRate"] = "1" })
            .Build();
        var runtime = new SimulationRuntime(ServerOptions.Load(configuration), configuration);
        var roadRevision = runtime.RoadRevision;
        var observationRevision = runtime.ObservationRevision;

        var removed = runtime.Mutate(static world => world.RemoveRoadNode(new RoadNodeId(999999)), roadTopologyChanged: true);

        Assert.IsFalse(removed);
        Assert.AreEqual(roadRevision, runtime.RoadRevision);
        Assert.AreEqual(observationRevision, runtime.ObservationRevision);
    }

    private sealed record CacheValue(int Value);
}
