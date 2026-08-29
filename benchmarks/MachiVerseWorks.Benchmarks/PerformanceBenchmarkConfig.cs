using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;

namespace MachiVerseWorks.Benchmarks;

internal static class PerformanceBenchmarkConfig
{
    public static IConfig Create()
    {
        var config = ManualConfig.Create(DefaultConfig.Instance);
        config.AddDiagnoser(MemoryDiagnoser.Default);
        config.AddExporter(MarkdownExporter.GitHub);
        config.AddExporter(JsonExporter.Full);
        config.AddExporter(CsvMeasurementsExporter.Default);
        config.ArtifactsPath = Environment.GetEnvironmentVariable("MACHIVERSE_BENCHMARK_ARTIFACTS")
            ?? "BenchmarkDotNet.Artifacts";
        return config;
    }
}
