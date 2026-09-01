using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class RadioBenchmarks
{
    private const int PropagationBatchSize = 200_000;
    private SimulationWorld _world = null!;
    private RadioReceiverId _receiverId;
    private DeterministicRadioPropagationSolver _solver = null!;
    private RadioPropagationRequest _propagationRequest;

    [Params(50_000)]
    public int TransmitterCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new SimulationWorld(new SimulationConfig(tickRate: 10, seed: 28));
        var band = _world.CreateSpectrumBand("benchmark", 5_000d, 5_500d);
        var channel = _world.CreateRadioChannel(band, 5_200d, 20d);
        var receiverSite = _world.CreateRadioSite(new WorldPoint(25_000d, 25_000d, 0d), RadioSiteKind.Gateway);
        var receiverAntenna = _world.CreateRadioAntenna(receiverSite, new WorldVector(0d, 0d, 20d), new WorldVector(1d, 0d, 0d), 2d);
        _receiverId = _world.CreateRadioReceiver(receiverSite, receiverAntenna, 5_000d, 5_500d, -110d);

        for (var index = 0; index < TransmitterCount; index++)
        {
            var x = (index % 224) * 225d;
            var y = (index / 224) * 225d;
            var site = _world.CreateRadioSite(new WorldPoint(x, y, 0d), RadioSiteKind.Micro);
            var antenna = _world.CreateRadioAntenna(site, new WorldVector(0d, 0d, 15d), new WorldVector(1d, 0d, 0d), 5d);
            var transmitter = _world.CreateRadioTransmitter(site, antenna, 40d);
            _world.CreateRadioEmission(transmitter, channel, 37d, 0.25d);
        }

        _solver = new DeterministicRadioPropagationSolver();
        var tx = new RadioSiteSnapshot(new RadioSiteId(1), RadioSiteKind.PointToPoint, new WorldPoint(0d, 0d, 25d), 0d, 25d, true);
        var rx = new RadioSiteSnapshot(new RadioSiteId(2), RadioSiteKind.PointToPoint, new WorldPoint(1_000d, 0d, 25d), 0d, 25d, true);
        var block = new FrequencyBlock(new FrequencyBlockId(1), new SpectrumBandId(1), 5_200d, 20d);
        var budget = new RadioLinkBudget(new TransmitterPathBudget(new EffectiveRadiatedPower(55d), 0d, 0d), 10d, -110d, 6d);
        _propagationRequest = new RadioPropagationRequest(tx, rx, block, budget, -120d, RadioDefaults.ThermalNoiseFloorDbm);
    }

    [Benchmark]
    public int QueryCandidates50k() => _world.QueryRadioEmissionCandidates(_receiverId).Length;

    [Benchmark]
    public double PropagationBatch200k()
    {
        var sum = 0d;
        for (var index = 0; index < PropagationBatchSize; index++) sum += _solver.Solve(_propagationRequest).SinrDb;
        return sum;
    }
}
