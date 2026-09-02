using System.Diagnostics;
using System.Globalization;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

internal static class ObservationCacheBenchmarkRunner
{
    private const long EncodedMemoryBudgetBytes = 64L * 1024L * 1024L;

    public static IReadOnlyList<ObservationCacheBenchmarkResult> Run()
    {
        return [RunScenario(16), RunScenario(64)];
    }

    private static ObservationCacheBenchmarkResult RunScenario(int viewerCount)
    {
        var snapshot = CreateSnapshot();
        var volume = new WorldVolume(120d, 120d, -10d, 520d, 520d, 10d);
        var revision = new ObservationRevision(2, 80);
        var spatialKey = new SpatialObservationCacheKey(SpatialObservationKind.Entities, volume, revision);
        var message = new AgentSpawnMessage(1, 1, 2, 3, 4, 5, 6, 100);
        var encodedKey = new EncodedObservationCacheKey("benchmark-agent", ProtocolVersion.Current, revision, "1");

        var uncachedSpatial = Measure(() =>
        {
            var cache = new ObservationCache(ObservationCacheOptions.Disabled);
            var count = 0;
            for (var viewer = 0; viewer < viewerCount; viewer++)
                count += cache.GetOrCreateSpatial(spatialKey, () => snapshot.QueryEntities(volume)).Agents.Length;
            GC.KeepAlive(count);
        });

        var spatialCache = new ObservationCache();
        var cachedSpatial = Measure(() =>
        {
            var count = 0;
            for (var viewer = 0; viewer < viewerCount; viewer++)
                count += spatialCache.GetOrCreateSpatial(spatialKey, () => snapshot.QueryEntities(volume)).Agents.Length;
            GC.KeepAlive(count);
        });
        var spatialMetrics = spatialCache.CreateMetricsSnapshot();

        var uncachedEncoding = Measure(() =>
        {
            var cache = new ObservationCache(ObservationCacheOptions.Disabled);
            var bytes = 0;
            for (var viewer = 0; viewer < viewerCount; viewer++)
                bytes += cache.GetOrEncode(encodedKey, () => ObservationProtocolAdapter.Serialize(message, ProtocolVersion.Current)).Length;
            GC.KeepAlive(bytes);
        });

        var encodingCache = new ObservationCache();
        var cachedEncoding = Measure(() =>
        {
            var bytes = 0;
            for (var viewer = 0; viewer < viewerCount; viewer++)
                bytes += encodingCache.GetOrEncode(encodedKey, () => ObservationProtocolAdapter.Serialize(message, ProtocolVersion.Current)).Length;
            GC.KeepAlive(bytes);
        });
        var encodingMetrics = encodingCache.CreateMetricsSnapshot();

        return new ObservationCacheBenchmarkResult(
            viewerCount,
            uncachedSpatial.ElapsedMilliseconds,
            cachedSpatial.ElapsedMilliseconds,
            uncachedSpatial.AllocatedBytes,
            cachedSpatial.AllocatedBytes,
            spatialMetrics.HitRate,
            spatialMetrics.Builds,
            uncachedEncoding.ElapsedMilliseconds,
            cachedEncoding.ElapsedMilliseconds,
            uncachedEncoding.AllocatedBytes,
            cachedEncoding.AllocatedBytes,
            encodingMetrics.HitRate,
            encodingMetrics.Encodings,
            encodingMetrics.EncodedBytes,
            EncodedMemoryBudgetBytes);
    }

    private static SimulationPublishSnapshot CreateSnapshot()
    {
        const int side = 100;
        var agents = new AgentSnapshot[side * side];
        var index = 0;
        for (var x = 0; x < side; x++)
        {
            for (var y = 0; y < side; y++)
            {
                agents[index] = new AgentSnapshot(new AgentId((ulong)index + 1), new WorldPoint(x * 10d, y * 10d, 0d), default, 100);
                index++;
            }
        }

        return new SimulationPublishSnapshot(
            100,
            64d,
            agents,
            [],
            [],
            new IntersectionControlSnapshot([], 100),
            new RoadNetworkReadModel(3, new RoadNetworkSnapshot([], [], [], [], [])),
            new RailwayInfrastructureReadModel(4, new RailwayInfrastructureSnapshot([], [], [], [], [], [], [], [])),
            observationGeneration: 2,
            observationRevision: 80);
    }

    private static Measurement Measure(Action action)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        action();
        var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
        return new Measurement(elapsed, allocated);
    }

    private readonly record struct Measurement(double ElapsedMilliseconds, long AllocatedBytes);
}

internal readonly record struct ObservationCacheBenchmarkResult(
    int ViewerCount,
    double SpatialUncachedMilliseconds,
    double SpatialCachedMilliseconds,
    long SpatialUncachedAllocatedBytes,
    long SpatialCachedAllocatedBytes,
    double SpatialHitRate,
    long SpatialBuildCount,
    double EncodingUncachedMilliseconds,
    double EncodingCachedMilliseconds,
    long EncodingUncachedAllocatedBytes,
    long EncodingCachedAllocatedBytes,
    double EncodingHitRate,
    long EncodingCount,
    long EncodedBytes,
    long EncodedMemoryBudgetBytes)
{
    public string ToCsv() => string.Join(',',
        ViewerCount.ToString(CultureInfo.InvariantCulture),
        SpatialUncachedMilliseconds.ToString("F6", CultureInfo.InvariantCulture),
        SpatialCachedMilliseconds.ToString("F6", CultureInfo.InvariantCulture),
        SpatialUncachedAllocatedBytes.ToString(CultureInfo.InvariantCulture),
        SpatialCachedAllocatedBytes.ToString(CultureInfo.InvariantCulture),
        SpatialHitRate.ToString("F6", CultureInfo.InvariantCulture),
        SpatialBuildCount.ToString(CultureInfo.InvariantCulture),
        EncodingUncachedMilliseconds.ToString("F6", CultureInfo.InvariantCulture),
        EncodingCachedMilliseconds.ToString("F6", CultureInfo.InvariantCulture),
        EncodingUncachedAllocatedBytes.ToString(CultureInfo.InvariantCulture),
        EncodingCachedAllocatedBytes.ToString(CultureInfo.InvariantCulture),
        EncodingHitRate.ToString("F6", CultureInfo.InvariantCulture),
        EncodingCount.ToString(CultureInfo.InvariantCulture),
        EncodedBytes.ToString(CultureInfo.InvariantCulture),
        EncodedMemoryBudgetBytes.ToString(CultureInfo.InvariantCulture));
}
