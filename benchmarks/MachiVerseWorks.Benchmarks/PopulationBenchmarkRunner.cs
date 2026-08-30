using System.Diagnostics;
using System.Globalization;
using MachiVerseWorks.Simulation;
using SimulationActivityKind = MachiVerseWorks.Simulation.ActivityKind;

namespace MachiVerseWorks.Benchmarks;

internal static class PopulationBenchmarkRunner
{
    private static readonly int[] PersonCounts = [1_000, 10_000, 100_000];

    public static IReadOnlyList<PopulationBenchmarkResult> Run(BenchmarkOptions options)
    {
        var results = new List<PopulationBenchmarkResult>(PersonCounts.Length);
        foreach (var personCount in PersonCounts)
        {
            results.Add(RunScenario(personCount, options));
        }
        return results;
    }

    private static PopulationBenchmarkResult RunScenario(int personCount, BenchmarkOptions options)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 15015, spatialCellSize: 64d));
        var residence = world.CreateBuilding(new WorldVolume(-10d, -10d, 0d, 10d, 10d, 20d), BuildingKind.Residential);
        var endpoint = TripEndpoint.ForBuilding(residence);
        var schedule = new[] { new DailyActivityWindow(SimulationActivityKind.Home, 0, 1440) };

        HouseholdId household = default;
        for (var index = 0; index < personCount; index++)
        {
            if (index % 4 == 0) household = world.CreateHousehold(endpoint);
            world.CreatePerson(household, new PersonDemographics(30), schedule);
        }

        for (var tick = 0; tick < options.WarmupTicks; tick++) world.Step();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var managedBytes = GC.GetTotalMemory(forceFullCollection: false);
        var durations = new double[options.MeasurementTicks];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        for (var tick = 0; tick < durations.Length; tick++)
        {
            var started = Stopwatch.GetTimestamp();
            world.Step();
            durations[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        Array.Sort(durations);
        var average = durations.Average();
        var statistics = world.CreatePopulationStatistics();
        if (statistics.PersonCount != personCount)
            throw new InvalidOperationException("Population benchmark setup did not preserve the requested Person count.");

        return new PopulationBenchmarkResult(
            personCount,
            statistics.HouseholdCount,
            options.MeasurementTicks,
            average,
            Percentile(durations, 0.50d),
            Percentile(durations, 0.95d),
            Percentile(durations, 0.99d),
            durations[^1],
            (allocatedAfter - allocatedBefore) / (double)options.MeasurementTicks,
            managedBytes);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }
}

internal sealed record PopulationBenchmarkResult(
    int PersonCount,
    int HouseholdCount,
    int MeasurementTicks,
    double AverageMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds,
    double AllocatedBytesPerTick,
    long ManagedBytes)
{
    public string ToCsv() => string.Join(
        ',',
        PersonCount.ToString(CultureInfo.InvariantCulture),
        HouseholdCount.ToString(CultureInfo.InvariantCulture),
        MeasurementTicks.ToString(CultureInfo.InvariantCulture),
        AverageMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        P50Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        P95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        P99Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        MaxMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        AllocatedBytesPerTick.ToString("F2", CultureInfo.InvariantCulture),
        ManagedBytes.ToString(CultureInfo.InvariantCulture));
}
