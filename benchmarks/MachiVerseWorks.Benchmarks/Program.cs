using BenchmarkDotNet.Running;
using MachiVerseWorks.Benchmarks;

if (args.Contains("--read-model-latency", StringComparer.Ordinal))
{
    PublishedReadModelLatencyRunner.Run(Console.Out);
    return;
}

if (args.Any(static argument => argument is "--warmup" or "--ticks"))
{
    var options = BenchmarkOptions.Parse(args);
    var results = TickBenchmarkRunner.Run(options);

    Console.WriteLine("agents,ticks,average_ms,p50_ms,p95_ms,p99_ms,max_ms,ticks_per_second,allocated_bytes_per_tick");
    foreach (var result in results)
    {
        Console.WriteLine(result.ToCsv());
    }

    return;
}

BenchmarkSwitcher
    .FromAssembly(typeof(PublishedReadModelBenchmarks).Assembly)
    .Run(args, PerformanceBenchmarkConfig.Create());
