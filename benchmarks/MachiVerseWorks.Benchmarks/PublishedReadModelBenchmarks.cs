using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(PublishedReadModelBenchmarkConfig))]
public class PublishedReadModelBenchmarks
{
    private SimulationPublishSnapshot _snapshot = null!;
    private WorldVolume[] _volumes = null!;

    [Params(10, 100)]
    public int ClientCount { get; set; }

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
                agents[index] = new AgentSnapshot(new AgentId((ulong)index + 1), new WorldPoint(x * 10d, y * 10d, 0), default, 1);
                index++;
            }
        }

        _snapshot = new SimulationPublishSnapshot(1, 64, agents, [], new RoadNetworkReadModel(1, new RoadNetworkSnapshot([], [], [], [], [])));
        _volumes = new WorldVolume[ClientCount];
        for (var client = 0; client < ClientCount; client++)
        {
            var origin = (client % 10) * 80d;
            _volumes[client] = new WorldVolume(origin, origin, -10, origin + 160, origin + 160, 10);
        }
    }

    [Benchmark]
    public int QueryPublishedStateForConcurrentClients()
    {
        var counts = new int[ClientCount];
        Parallel.For(0, ClientCount, index => counts[index] = _snapshot.QueryEntities(_volumes[index]).Agents.Length);
        var total = 0;
        for (var index = 0; index < counts.Length; index++) total += counts[index];
        return total;
    }

    private sealed class PublishedReadModelBenchmarkConfig : ManualConfig
    {
        public PublishedReadModelBenchmarkConfig()
        {
            AddColumn(StatisticColumn.P95);
        }
    }
}
