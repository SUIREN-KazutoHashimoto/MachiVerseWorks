using System.Diagnostics;
using System.Globalization;
using MachiVerseWorks.Simulation;
using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Benchmarks;

internal static class RoadTrafficBenchmarkRunner
{
    private static readonly int[] VehicleCounts = [1_000, 10_000, 100_000];
    private const int VehiclesPerLane = 100;
    private const double VehicleSpacingMeters = 8d;
    private const double SegmentLengthMeters = 1_200d;

    public static IReadOnlyList<RoadTrafficBenchmarkResult> Run(BenchmarkOptions options)
    {
        var results = new List<RoadTrafficBenchmarkResult>(VehicleCounts.Length);
        foreach (var vehicleCount in VehicleCounts)
        {
            results.Add(RunScenario(vehicleCount, options));
        }
        return results;
    }

    private static RoadTrafficBenchmarkResult RunScenario(int vehicleCount, BenchmarkOptions options)
    {
        var world = BuildWorld(vehicleCount, out var laneCount);
        var occupancy = BuildOccupancy(vehicleCount, laneCount);

        for (var tick = 0; tick < options.WarmupTicks; tick++) world.Step();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var tickMeasurement = Measure(options.MeasurementTicks, () =>
        {
            world.Step();
            return world.Time.TickCount;
        });

        var occupancyMeasurement = Measure(options.MeasurementTicks, occupancy.QueryAllLeaders);

        var snapshotMeasurement = Measure(options.MeasurementTicks, () =>
        {
            var snapshots = world.CreateAllVehicleSnapshots();
            return checked((ulong)snapshots.Length);
        });

        return new RoadTrafficBenchmarkResult(
            vehicleCount,
            laneCount,
            options.MeasurementTicks,
            tickMeasurement.AverageMilliseconds,
            tickMeasurement.P95Milliseconds,
            tickMeasurement.P99Milliseconds,
            tickMeasurement.AllocatedBytesPerOperation,
            occupancyMeasurement.AverageMilliseconds,
            occupancyMeasurement.P95Milliseconds,
            occupancyMeasurement.P99Milliseconds,
            snapshotMeasurement.AverageMilliseconds,
            snapshotMeasurement.P95Milliseconds,
            snapshotMeasurement.P99Milliseconds,
            snapshotMeasurement.AllocatedBytesPerOperation,
            GC.GetTotalMemory(forceFullCollection: false));
    }

    private static SimulationWorld BuildWorld(int vehicleCount, out int laneCount)
    {
        laneCount = (vehicleCount + VehiclesPerLane - 1) / VehiclesPerLane;
        var topologyWorld = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 13013, spatialCellSize: 64d));
        var routes = new RouteLaneStep[laneCount][];

        for (var laneIndex = 0; laneIndex < laneCount; laneIndex++)
        {
            var y = laneIndex * 4d;
            var start = topologyWorld.CreateRoadNode(new WorldPoint(0d, y, 0d));
            var end = topologyWorld.CreateRoadNode(new WorldPoint(SegmentLengthMeters, y, 0d));
            var segment = topologyWorld.CreateRoadSegment(start, end, RoadKind.Local);
            var lane = topologyWorld.CreateLane(segment, LaneDirection.Forward, 0, speedLimitMetersPerSecond: 20d);
            routes[laneIndex] =
            [
                new RouteLaneStep(lane, segment, 0d, 1d, SegmentLengthMeters, SegmentLengthMeters / 20d, null),
            ];
        }

        var checkpoint = topologyWorld.CreateCheckpoint();
        var vehicles = new SimulationVehicleCheckpoint[vehicleCount];
        for (var index = 0; index < vehicleCount; index++)
        {
            var laneIndex = index / VehiclesPerLane;
            var indexInLane = index % VehiclesPerLane;
            var progress = 10d + indexInLane * VehicleSpacingMeters;
            vehicles[index] = new SimulationVehicleCheckpoint(
                new VehicleId(checked((ulong)index + 1UL)),
                VehicleDimensions.PassengerCar,
                VehiclePerformance.PassengerCar,
                routes[laneIndex],
                0,
                progress,
                8d,
                VehicleMovementState.Driving);
        }

        var world = SimulationWorld.RestoreCheckpoint(checkpoint with
        {
            NextVehicleId = checked((ulong)vehicleCount + 1UL),
            Vehicles = vehicles,
        });

        if (world.VehicleCount != vehicleCount) throw new InvalidOperationException("Road Traffic benchmark setup did not preserve the requested Vehicle count.");
        return world;
    }

    private static OccupancyScenario BuildOccupancy(int vehicleCount, int laneCount)
    {
        var index = new LaneOccupancyIndex();
        var laneIds = new LaneId[vehicleCount];
        var progressMeters = new double[vehicleCount];

        for (var vehicleIndex = 0; vehicleIndex < vehicleCount; vehicleIndex++)
        {
            var laneIndex = vehicleIndex / VehiclesPerLane;
            if (laneIndex >= laneCount) throw new InvalidOperationException("Road Traffic occupancy setup exceeded Lane count.");
            var laneId = new LaneId(checked((ulong)laneIndex + 1UL));
            var progress = 10d + (vehicleIndex % VehiclesPerLane) * VehicleSpacingMeters;
            laneIds[vehicleIndex] = laneId;
            progressMeters[vehicleIndex] = progress;
            index.Add(new VehicleId(checked((ulong)vehicleIndex + 1UL)), laneId, progress, VehicleDimensions.PassengerCar.LengthMeters, 8d);
        }

        return new OccupancyScenario(index, laneIds, progressMeters);
    }

    private static MeasurementResult Measure(int iterations, Func<ulong> operation)
    {
        var durations = new double[iterations];
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        ulong checksum = 0;

        for (var iteration = 0; iteration < iterations; iteration++)
        {
            var started = Stopwatch.GetTimestamp();
            checksum ^= operation();
            durations[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(checksum);
        Array.Sort(durations);
        return new MeasurementResult(
            durations.Average(),
            Percentile(durations, 0.95d),
            Percentile(durations, 0.99d),
            (allocatedAfter - allocatedBefore) / (double)iterations);
    }

    private static double Percentile(double[] sortedValues, double percentile)
    {
        var rank = (int)Math.Ceiling(percentile * sortedValues.Length) - 1;
        return sortedValues[Math.Clamp(rank, 0, sortedValues.Length - 1)];
    }

    private sealed class OccupancyScenario(
        LaneOccupancyIndex index,
        LaneId[] laneIds,
        double[] progressMeters)
    {
        public ulong QueryAllLeaders()
        {
            ulong checksum = 0;
            for (var vehicleIndex = 0; vehicleIndex < laneIds.Length; vehicleIndex++)
            {
                if (index.TryGetLeader(laneIds[vehicleIndex], progressMeters[vehicleIndex] + 0.001d, out var leader))
                    checksum ^= leader.Id.Value;
            }
            return checksum;
        }
    }

    private readonly record struct MeasurementResult(
        double AverageMilliseconds,
        double P95Milliseconds,
        double P99Milliseconds,
        double AllocatedBytesPerOperation);
}

internal sealed record RoadTrafficBenchmarkResult(
    int VehicleCount,
    int LaneCount,
    int MeasurementIterations,
    double TickAverageMilliseconds,
    double TickP95Milliseconds,
    double TickP99Milliseconds,
    double TickAllocatedBytes,
    double OccupancyAverageMilliseconds,
    double OccupancyP95Milliseconds,
    double OccupancyP99Milliseconds,
    double SnapshotAverageMilliseconds,
    double SnapshotP95Milliseconds,
    double SnapshotP99Milliseconds,
    double SnapshotAllocatedBytes,
    long ManagedBytes)
{
    public string ToCsv() => string.Join(
        ',',
        VehicleCount.ToString(CultureInfo.InvariantCulture),
        LaneCount.ToString(CultureInfo.InvariantCulture),
        MeasurementIterations.ToString(CultureInfo.InvariantCulture),
        TickAverageMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        TickP95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        TickP99Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        TickAllocatedBytes.ToString("F2", CultureInfo.InvariantCulture),
        OccupancyAverageMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        OccupancyP95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        OccupancyP99Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        SnapshotAverageMilliseconds.ToString("F4", CultureInfo.InvariantCulture),
        SnapshotP95Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        SnapshotP99Milliseconds.ToString("F4", CultureInfo.InvariantCulture),
        SnapshotAllocatedBytes.ToString("F2", CultureInfo.InvariantCulture),
        ManagedBytes.ToString(CultureInfo.InvariantCulture));
}
