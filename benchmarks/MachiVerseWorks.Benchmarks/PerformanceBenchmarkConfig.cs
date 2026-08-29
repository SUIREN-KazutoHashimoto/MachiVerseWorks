using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;

namespace MachiVerseWorks.Benchmarks;

public sealed class PerformanceBenchmarkConfig : ManualConfig
{
    public PerformanceBenchmarkConfig()
    {
        Add(DefaultConfig.Instance);
        AddDiagnoser(MemoryDiagnoser.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.Full);
        AddExporter(CsvMeasurementsExporter.Default);

        ArtifactsPath = Environment.GetEnvironmentVariable("MACHIVERSE_BENCHMARK_ARTIFACTS")
            ?? "BenchmarkDotNet.Artifacts";
    }
}
