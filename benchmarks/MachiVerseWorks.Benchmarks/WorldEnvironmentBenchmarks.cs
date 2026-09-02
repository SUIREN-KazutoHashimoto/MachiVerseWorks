using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class WorldEnvironmentBenchmarks
{
    private SimulationWorld _world = null!;
    private WorldPoint[] _globalQueryPoints = null!;
    private WorldPoint[] _terrainQueryPoints = null!;
    private WorldVolume _largeWorldVolume;
    private WorldVolume _detailVolume;

    [Params(10_000)]
    public int GlobalQueryCount { get; set; }

    [Params(1_000)]
    public int TerrainQueryCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var environment = new WorldEnvironmentConfig(
            29_028UL,
            new WorldVector(0.15d, 1d, 0d),
            latitudeDegrees: 43d,
            continentality: 0.57d,
            maritimeInfluence: 0.43d,
            meanAnnualTemperatureCelsius: 10d,
            seasonalityCelsius: 21d,
            annualPrecipitationMillimeters: 920d);
        _world = new SimulationWorld(new SimulationConfig(tickRate: 30, seed: 29028UL, worldEnvironment: environment));
        _largeWorldVolume = new WorldVolume(-2_000_000d, -2_000_000d, -12_000d, 2_000_000d, 2_000_000d, 12_000d);
        _detailVolume = new WorldVolume(-250_000d, -250_000d, -12_000d, 250_000d, 250_000d, 12_000d);
        _globalQueryPoints = CreatePoints(GlobalQueryCount, 4_000_000d, 97_531UL);
        _terrainQueryPoints = CreatePoints(TerrainQueryCount, 500_000d, 73_939UL);
    }

    [Benchmark]
    public double QueryGlobalField10k()
    {
        var checksum = 0d;
        foreach (var point in _globalQueryPoints)
        {
            var sample = _world.QueryEnvironment(point);
            checksum += sample.ElevationMeters + sample.SettlementScore;
        }
        return checksum;
    }

    [Benchmark]
    public double QueryDetailedTerrain1k()
    {
        var checksum = 0d;
        foreach (var point in _terrainQueryPoints)
        {
            var sample = _world.QueryTerrainSurface(point.X, point.Y);
            checksum += sample.Position.Z + sample.SlopeDegrees + sample.Normal.Z;
        }
        return checksum;
    }

    [Benchmark]
    public int GenerateLargeWorldReferenceSnapshot() =>
        _world.CreateWorldEnvironmentSnapshot(_largeWorldVolume, 16, 16, 128).Features.Count;

    [Benchmark]
    public int GenerateDetailedReferenceSnapshot() =>
        _world.CreateDetailedWorldEnvironmentSnapshot(_detailVolume, 8, 8, 128).TerrainSamples.Count;

    private static WorldPoint[] CreatePoints(int count, double span, ulong salt)
    {
        var points = new WorldPoint[count];
        for (var index = 0; index < count; index++)
        {
            var x = (ToUnit(Mix((ulong)index ^ salt)) - 0.5d) * span;
            var y = (ToUnit(Mix((ulong)index ^ (salt << 1))) - 0.5d) * span;
            points[index] = new WorldPoint(x, y, 0d);
        }
        return points;
    }

    private static ulong Mix(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static double ToUnit(ulong value) => (value >> 11) * (1d / 9_007_199_254_740_992d);
}
