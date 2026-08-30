using System.Diagnostics;
using System.Globalization;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

internal static class PublishedReadModelLatencyRunner
{
    private const int WarmupIterations = 50;
    private const int MeasurementIterations = 500;

    public static void Run(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteLine("clients,iterations,average_ms,p95_ms,p99_ms,max_ms");
        RunCase(10, writer);
        RunCase(100, writer);
    }

    private static void RunCase(int clientCount, TextWriter writer)
    {
        var snapshot = CreateSnapshot();
        var volumes = CreateVolumes(clientCount);
        for (var iteration = 0; iteration < WarmupIterations; iteration++)
        {
            QueryAll(snapshot, volumes);
        }

        var samples = new double[MeasurementIterations];
        for (var iteration = 0; iteration < samples.Length; iteration++)
        {
            var started = Stopwatch.GetTimestamp();
            QueryAll(snapshot, volumes);
            samples[iteration] = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        }

        Array.Sort(samples);
        var average = samples.Average();
        var p95 = Percentile(samples, 0.95d);
        var p99 = Percentile(samples, 0.99d);
        var maximum = samples[^1];
        writer.WriteLine(string.Join(',',
            clientCount.ToString(CultureInfo.InvariantCulture),
            samples.Length.ToString(CultureInfo.InvariantCulture),
            average.ToString("F6", CultureInfo.InvariantCulture),
            p95.ToString("F6", CultureInfo.InvariantCulture),
            p99.ToString("F6", CultureInfo.InvariantCulture),
            maximum.ToString("F6", CultureInfo.InvariantCulture)));
    }

    private static SimulationPublishSnapshot CreateSnapshot()
    {
        const int side = 100;
        var agents = new AgentSnapshot[side * side];
        var index = 0;
        for (var x = 0; x < side; x++)
        {
            for (var y = 0; y < side; y++)
            {
                agents[index] = new AgentSnapshot(
                    new AgentId((ulong)index + 1),
                    new WorldPoint(x * 10d, y * 10d, 0d),
                    default,
                    1);
                index++;
            }
        }

        return new SimulationPublishSnapshot(
            1,
            64d,
            agents,
            [],
            new RoadNetworkReadModel(1, new RoadNetworkSnapshot([], [], [], [], [])));
    }

    private static WorldVolume[] CreateVolumes(int clientCount)
    {
        var volumes = new WorldVolume[clientCount];
        for (var client = 0; client < clientCount; client++)
        {
            var origin = (client % 10) * 80d;
            volumes[client] = new WorldVolume(origin, origin, -10d, origin + 160d, origin + 160d, 10d);
        }
        return volumes;
    }

    private static void QueryAll(SimulationPublishSnapshot snapshot, WorldVolume[] volumes)
    {
        Parallel.For(0, volumes.Length, index => _ = snapshot.QueryEntities(volumes[index]));
    }

    private static double Percentile(double[] sortedSamples, double percentile)
    {
        var index = (int)Math.Ceiling(percentile * sortedSamples.Length) - 1;
        return sortedSamples[Math.Clamp(index, 0, sortedSamples.Length - 1)];
    }
}
