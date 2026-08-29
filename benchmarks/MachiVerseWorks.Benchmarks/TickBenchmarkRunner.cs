using System.Diagnostics;
using System.Globalization;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

internal sealed record BenchmarkOptions(int WarmupTicks, int MeasurementTicks)
{
    public static BenchmarkOptions Parse(string[] args)
    {
        var warmupTicks = 60;
        var measurementTicks = 200;

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--warmup":
                    warmupTicks = ParsePositiveInt(args, ref index, "--warmup");
                    break;
                case "--ticks":
                    measurementTicks = ParsePositiveInt(args, ref index, "--ticks");
                    break;
                default:
                    throw new ArgumentException($"Unknown benchmark argument: {args[index]}", nameof(args));
            }
        }

        return new BenchmarkOptions(warmupTicks, measurementTicks);
    }

    private static int ParsePositiveInt(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length ||
            !int.TryParse(args[++index], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
            value <= 0)
        {
            throw new ArgumentException($"{option} requires a positive integer value.", nameof(args));
        }

        return value;
    }
}

internal static class TickBenchmarkRunner
{
    private static readonly int[] AgentCounts = [1_000, 10_000, 100_000];

    public static IReadOnlyList<TickBenchmarkResult> Run(BenchmarkOptions options)
    {
        var results = new List<TickBenchmarkResult>(AgentCounts.Length);

        foreach (var agentCount in AgentCounts)
        {
            results.Add(RunScenario(agentCount, options));
        }

        return results;
    }

    private static TickBenchmarkResult RunScenario(int agentCount, BenchmarkOptions options)
    {
        var world = new SimulationWorld(
            new SimulationConfig(tickRate: 30, seed: 1234, spatialCellSize: 64d));
        world.CreateAgents(agentCount, new WorldRect(-5_000d, -5_000d, 5_000d, 5_000d));

        for (var tick = 0; tick < options.WarmupTicks; tick++)
        {
            world.Step();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var durations = new double[options.MeasurementTicks];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var tick = 0; tick < durations.Length; tick++)
        {
            var started = Stopwatch.GetTimestamp();
            world.Step();
            durations[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        var allocatedPerTick = (allocatedAfter - allocatedBefore) / (double)options.MeasurementTicks;

        Array.Sort(durations);

        var sum = 0d;
        foreach (var duration in durations)
        {
            sum += duration;
        }

        var average = sum / durations.Length;
        return new TickBenchmarkResult(
            agentCount,
            options.MeasurementTicks,
            average,
            Percentile(durations, 0.50d),
            Percentile(durations, 0.95d),
            Percentile(durations, 0.99d),
            durations[^1],
            1000d / average,
            allocatedPerTick);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }
}

internal sealed record TickBenchmarkResult(
    int AgentCount,
    int MeasurementTicks,
    double AverageMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds,
    double TicksPerSecond,
    double AllocatedBytesPerTick)
{
    public string ToCsv()
    {
        return string.Join(
            ',',
            AgentCount.ToString(CultureInfo.InvariantCulture),
            MeasurementTicks.ToString(CultureInfo.InvariantCulture),
            AverageMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
            P50Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
            P95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
            P99Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
            MaxMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
            TicksPerSecond.ToString("F2", CultureInfo.InvariantCulture),
            AllocatedBytesPerTick.ToString("F2", CultureInfo.InvariantCulture));
    }
}
