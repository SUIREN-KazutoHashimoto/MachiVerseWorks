using MachiVerseWorks.Benchmarks;

var options = BenchmarkOptions.Parse(args);
var results = TickBenchmarkRunner.Run(options);

Console.WriteLine("agents,ticks,average_ms,p50_ms,p95_ms,p99_ms,max_ms,ticks_per_second,allocated_bytes_per_tick");

foreach (var result in results)
{
    Console.WriteLine(result.ToCsv());
}
