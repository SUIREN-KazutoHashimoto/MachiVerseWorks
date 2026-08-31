using System.Diagnostics;
using System.Globalization;
using MachiVerseWorks.Simulation;
using SimulationActivityKind = MachiVerseWorks.Simulation.ActivityKind;

namespace MachiVerseWorks.Benchmarks;

internal static class PopulationBenchmarkRunner
{
    private static readonly int[] IdlePersonCounts = [1_000, 10_000, 100_000];
    private static readonly int[] DispatchPersonCounts = [1_000, 10_000];

    public static IReadOnlyList<PopulationBenchmarkResult> Run(BenchmarkOptions options)
    {
        var results = new List<PopulationBenchmarkResult>(IdlePersonCounts.Length + DispatchPersonCounts.Length * 2);
        foreach (var personCount in IdlePersonCounts)
            results.Add(RunIdleScenario(personCount, options));
        foreach (var personCount in DispatchPersonCounts)
        {
            results.Add(RunDispatchScenario(personCount, TravelMode.Foot, options));
            results.Add(RunDispatchScenario(personCount, TravelMode.Motor, options));
        }
        return results;
    }

    private static PopulationBenchmarkResult RunIdleScenario(int personCount, BenchmarkOptions options)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 15015, spatialCellSize: 64d));
        var residence = world.CreateBuilding(new WorldVolume(-10d, -10d, 0d, 10d, 10d, 20d), BuildingKind.Residential);
        var endpoint = TripEndpoint.ForBuilding(residence);
        var schedule = new[] { new DailyActivityWindow(SimulationActivityKind.Home, 0, 1440) };
        CreatePopulation(world, personCount, endpoint, schedule, hasPrivateVehicle: false);

        for (var tick = 0; tick < options.WarmupTicks; tick++) world.Step();
        return Measure("idle", personCount, world, options);
    }

    private static PopulationBenchmarkResult RunDispatchScenario(int personCount, TravelMode mode, BenchmarkOptions options)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 15015, spatialCellSize: 64d));
        var home = world.CreateBuilding(new WorldVolume(0d, -3d, 0d, 4d, 3d, 4d), BuildingKind.Residential);
        var work = world.CreateBuilding(new WorldVolume(996d, -3d, 0d, 1000d, 3d, 4d), BuildingKind.Commercial);
        var start = world.CreateRoadNode(new WorldPoint(0d, 0d, 0d));
        var end = world.CreateRoadNode(new WorldPoint(1000d, 0d, 0d));
        var segment = world.CreateRoadSegment(start, end);
        world.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 20d);
        world.CreateRoadAccessPoint(segment, 0.01d, home, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);
        world.CreateRoadAccessPoint(segment, 0.99d, work, mode: RoadAccessMode.Motor | RoadAccessMode.Foot);

        // Warm infrastructure/JIT paths without consuming the first Population dispatch tick.
        for (var tick = 0; tick < options.WarmupTicks; tick++) world.Step();

        var schedule = new[]
        {
            new DailyActivityWindow(SimulationActivityKind.Work, 0, 1440, TripEndpoint.ForBuilding(work), ActivityPriority.High),
        };
        CreatePopulation(
            world,
            personCount,
            TripEndpoint.ForBuilding(home),
            schedule,
            hasPrivateVehicle: mode == TravelMode.Motor);

        return Measure(mode == TravelMode.Motor ? "motor-dispatch" : "foot-dispatch", personCount, world, options);
    }

    private static void CreatePopulation(
        SimulationWorld world,
        int personCount,
        TripEndpoint residence,
        IReadOnlyList<DailyActivityWindow> schedule,
        bool hasPrivateVehicle)
    {
        HouseholdId household = default;
        for (var index = 0; index < personCount; index++)
        {
            if (index % 4 == 0) household = world.CreateHousehold(residence);
            world.CreatePerson(
                household,
                new PersonDemographics(30, IsEmployed: true, HasPrivateVehicle: hasPrivateVehicle),
                schedule);
        }
    }

    private static PopulationBenchmarkResult Measure(string scenario, int personCount, SimulationWorld world, BenchmarkOptions options)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var managedBytes = GC.GetTotalMemory(forceFullCollection: false);
        var durations = new double[options.MeasurementTicks];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var maximumActivePedestrians = 0;
        var maximumActiveVehicles = 0;

        for (var tick = 0; tick < durations.Length; tick++)
        {
            var started = Stopwatch.GetTimestamp();
            world.Step();
            durations[tick] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            maximumActivePedestrians = Math.Max(maximumActivePedestrians, world.ActivePedestrianCount);
            maximumActiveVehicles = Math.Max(maximumActiveVehicles, world.ActiveVehicleCount);
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        Array.Sort(durations);
        var average = durations.Average();
        var statistics = world.CreatePopulationStatistics();
        if (statistics.PersonCount != personCount)
            throw new InvalidOperationException("Population benchmark setup did not preserve the requested Person count.");
        if (scenario == "foot-dispatch" && maximumActivePedestrians == 0)
            throw new InvalidOperationException("Foot dispatch benchmark did not create any active Pedestrian.");
        if (scenario == "motor-dispatch" && maximumActiveVehicles == 0)
            throw new InvalidOperationException("Motor dispatch benchmark did not create any active Vehicle.");

        return new PopulationBenchmarkResult(
            scenario,
            personCount,
            statistics.HouseholdCount,
            options.MeasurementTicks,
            average,
            Percentile(durations, 0.50d),
            Percentile(durations, 0.95d),
            Percentile(durations, 0.99d),
            durations[^1],
            (allocatedAfter - allocatedBefore) / (double)options.MeasurementTicks,
            managedBytes,
            maximumActivePedestrians,
            maximumActiveVehicles);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }
}

internal sealed record PopulationBenchmarkResult(
    string Scenario,
    int PersonCount,
    int HouseholdCount,
    int MeasurementTicks,
    double AverageMilliseconds,
    double P50Milliseconds,
    double P95Milliseconds,
    double P99Milliseconds,
    double MaxMilliseconds,
    double AllocatedBytesPerTick,
    long ManagedBytes,
    int MaximumActivePedestrians,
    int MaximumActiveVehicles)
{
    public string ToCsv() => string.Join(
        ',',
        Scenario,
        PersonCount.ToString(CultureInfo.InvariantCulture),
        HouseholdCount.ToString(CultureInfo.InvariantCulture),
        MeasurementTicks.ToString(CultureInfo.InvariantCulture),
        AverageMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        P50Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        P95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        P99Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        MaxMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        AllocatedBytesPerTick.ToString("F2", CultureInfo.InvariantCulture),
        ManagedBytes.ToString(CultureInfo.InvariantCulture),
        MaximumActivePedestrians.ToString(CultureInfo.InvariantCulture),
        MaximumActiveVehicles.ToString(CultureInfo.InvariantCulture));
}
