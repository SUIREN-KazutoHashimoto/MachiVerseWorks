using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Benchmarks;

[Config(typeof(PerformanceBenchmarkConfig))]
public sealed class ProtocolCodecBenchmarks
{
    private static readonly AgentUpdateMessage Message = new(
        42,
        125.25d,
        -480.5d,
        0.75d,
        -0.25d,
        12_345);

    private byte[] _frame = [];

    [GlobalSetup]
    public void Setup()
    {
        _frame = ProtocolCodec.Serialize(Message);
    }

    [Benchmark]
    public byte[] Encode()
    {
        return ProtocolCodec.Serialize(Message);
    }

    [Benchmark]
    public ProtocolEnvelope Decode()
    {
        if (!ProtocolCodec.TryDeserialize(_frame, out var envelope, out var error) || envelope is null)
        {
            throw new InvalidOperationException($"Protocol decode failed during benchmark: {error}.");
        }

        return envelope;
    }
}
