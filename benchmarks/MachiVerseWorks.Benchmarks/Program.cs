using BenchmarkDotNet.Running;
using MachiVerseWorks.Benchmarks;

if (args.Contains("--read-model-latency", StringComparer.Ordinal))
{
    PublishedReadModelLatencyRunner.Run(Console.Out);
    return;
}

if (args.Contains("--road-traffic", StringComparer.Ordinal))
{
    var benchmarkArgs = args.Where(static argument => argument != "--road-traffic").ToArray();
    var options = BenchmarkOptions.Parse(benchmarkArgs);
    var results = RoadTrafficBenchmarkRunner.Run(options);

    Console.WriteLine("vehicles,lanes,iterations,tick_average_ms,tick_p95_ms,tick_p99_ms,tick_allocated_bytes,occupancy_average_ms,occupancy_p95_ms,occupancy_p99_ms,snapshot_average_ms,snapshot_p95_ms,snapshot_p99_ms,snapshot_allocated_bytes,managed_bytes");
    foreach (var result in results) Console.WriteLine(result.ToCsv());
    return;
}

if (args.Contains("--population", StringComparer.Ordinal))
{
    var benchmarkArgs = args.Where(static argument => argument != "--population").ToArray();
    var options = BenchmarkOptions.Parse(benchmarkArgs);
    var results = PopulationBenchmarkRunner.Run(options);

    Console.WriteLine("scenario,persons,households,ticks,average_ms,p50_ms,p95_ms,p99_ms,max_ms,allocated_bytes_per_tick,managed_bytes,max_active_pedestrians,max_active_vehicles");
    foreach (var result in results) Console.WriteLine(result.ToCsv());
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