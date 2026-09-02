using BenchmarkDotNet.Attributes;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
public class PersistentRegionalEvolutionBenchmarks
{
    private RegionalGenerationSnapshot _generation = null!;
    private PersistentRegionalEvolutionSnapshot _baseline = null!;

    [Params(8, 12)]
    public int SettlementCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var environment = new WorldEnvironmentConfig(
            31_021,
            new WorldVector(0.2d, 1d, 0d),
            latitudeDegrees: 43d,
            continentality: 0.54d,
            maritimeInfluence: 0.46d,
            meanAnnualTemperatureCelsius: 10.5d,
            seasonalityCelsius: 20d,
            annualPrecipitationMillimeters: 980d);
        var world = new SimulationWorld(new SimulationConfig(tickRate: 2, seed: 31_021, worldEnvironment: environment));
        _generation = world.GenerateRegionalGeneration(
            new WorldVolume(-1_000_000d, -1_000_000d, -12_000d, 1_000_000d, 1_000_000d, 12_000d),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.HighQuality, SettlementCount, iterationBudget: 2));
        _baseline = PersistentRegionalEvolutionEngine.Initialize(_generation);
    }

    [Benchmark]
    public PersistentRegionalEvolutionSnapshot AdvanceOneYear() =>
        PersistentRegionalEvolutionEngine.AdvanceYears(_baseline, _generation, 1);

    [Benchmark]
    public PersistentRegionalEvolutionSnapshot AdvanceTenYears() =>
        PersistentRegionalEvolutionEngine.AdvanceYears(_baseline, _generation, 10);
}
