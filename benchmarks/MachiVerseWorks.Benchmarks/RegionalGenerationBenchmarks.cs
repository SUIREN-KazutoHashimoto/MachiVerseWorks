using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class RegionalGenerationBenchmarks
{
    private WorldEnvironmentConfig _environment = null!;
    private WorldVolume _volume;

    [Params(
        RegionalGenerationQualityPreset.Draft,
        RegionalGenerationQualityPreset.Standard,
        RegionalGenerationQualityPreset.HighQuality)]
    public RegionalGenerationQualityPreset Preset { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _environment = new WorldEnvironmentConfig(
            30_034UL,
            new WorldVector(0.15d, 1d, 0d),
            latitudeDegrees: 43d,
            continentality: 0.57d,
            maritimeInfluence: 0.43d,
            meanAnnualTemperatureCelsius: 10d,
            seasonalityCelsius: 21d,
            annualPrecipitationMillimeters: 920d);
        _volume = new WorldVolume(-600_000d, -600_000d, -12_000d, 600_000d, 600_000d, 12_000d);
    }

    [Benchmark]
    public double GenerateRegionalSnapshot()
    {
        var world = CreateWorld();
        var snapshot = world.GenerateRegionalGeneration(_volume, new RegionalGenerationOptions(Preset));
        return snapshot.Quality.OverallScore
            + snapshot.Settlements.Count
            + snapshot.Corridors.Count
            + snapshot.Parcels.Count
            + snapshot.Buildings.Count;
    }

    [Benchmark]
    public int GenerateAndMaterializeInitialWorld()
    {
        var world = CreateWorld();
        _ = world.InitializeRegionalWorld(
            _volume,
            new RegionalGenerationOptions(Preset),
            out var materialized);
        return materialized.RoadSegmentCount
            + materialized.LaneCount
            + materialized.BuildingCount
            + materialized.PersonCount
            + materialized.JobCount;
    }

    private SimulationWorld CreateWorld() => new(new SimulationConfig(
        tickRate: 30,
        seed: 30_034UL,
        worldEnvironment: _environment));
}
