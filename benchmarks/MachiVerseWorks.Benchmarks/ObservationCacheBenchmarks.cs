using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(ObservationCacheBenchmarkConfig))]
public class ObservationCacheBenchmarks
{
    private ObservationCache _cached = null!;
    private ObservationCache _disabled = null!;
    private SimulationPublishSnapshot _snapshot = null!;
    private SpatialObservationCacheKey _spatialKey;
    private EncodedObservationCacheKey _encodedKey;
    private AgentSpawnMessage _message = null!;
    private WorldVolume _volume;

    [Params(16, 64)]
    public int ViewerCount { get; set; }

    [GlobalSetup]
    public void Setup()
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

        _snapshot = new SimulationPublishSnapshot(
            100,
            64d,
            agents,
            [],
            new RoadNetworkReadModel(3, new RoadNetworkSnapshot([], [], [], [], [])),
            observationGeneration: 2,
            observationRevision: 80);
        _volume = new WorldVolume(120d, 120d, -10d, 520d, 520d, 10d);
        var revision = new ObservationRevision(2, 80);
        _spatialKey = new SpatialObservationCacheKey(SpatialObservationKind.Entities, _volume, revision);
        _encodedKey = new EncodedObservationCacheKey("benchmark-agent", ProtocolVersion.Current, revision, "1");
        _message = new AgentSpawnMessage(1, 1, 2, 3, 4, 5, 6, 100);
        _cached = new ObservationCache();
        _disabled = new ObservationCache(ObservationCacheOptions.Disabled);

        _ = _cached.GetOrCreateSpatial(_spatialKey, () => _snapshot.QueryEntities(_volume));
        _ = _cached.GetOrEncode(_encodedKey, () => ObservationProtocolAdapter.Serialize(_message, ProtocolVersion.Current));
    }

    [Benchmark]
    [BenchmarkCategory("Spatial")]
    public int SpatialWithoutCache()
    {
        var total = 0;
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            var snapshot = _disabled.GetOrCreateSpatial(_spatialKey, () => _snapshot.QueryEntities(_volume));
            total += snapshot.Agents.Length;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Spatial")]
    public int SpatialWithCache()
    {
        var total = 0;
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            var snapshot = _cached.GetOrCreateSpatial(_spatialKey, () => _snapshot.QueryEntities(_volume));
            total += snapshot.Agents.Length;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Encoding")]
    public int EncodingWithoutCache()
    {
        var bytes = 0;
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            bytes += _disabled.GetOrEncode(_encodedKey, () => ObservationProtocolAdapter.Serialize(_message, ProtocolVersion.Current)).Length;
        }
        return bytes;
    }

    [Benchmark]
    [BenchmarkCategory("Encoding")]
    public int EncodingWithCache()
    {
        var bytes = 0;
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            bytes += _cached.GetOrEncode(_encodedKey, () => ObservationProtocolAdapter.Serialize(_message, ProtocolVersion.Current)).Length;
        }
        return bytes;
    }

    public ObservationCacheMetrics CreateCacheMetrics() => _cached.CreateMetricsSnapshot();

    private sealed class ObservationCacheBenchmarkConfig : ManualConfig
    {
        public ObservationCacheBenchmarkConfig()
        {
            AddColumn(StatisticColumn.P95);
        }
    }
}
