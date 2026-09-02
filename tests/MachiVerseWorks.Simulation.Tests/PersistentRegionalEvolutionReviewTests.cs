using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class PersistentRegionalEvolutionReviewTests
{
    [TestMethod]
    public void CheckpointRestoresCustomTicksPerYear()
    {
        var world = CreateWorld(31901);
        world.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 3));
        world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(
            RegionalGenerationQualityPreset.Draft,
            settlementCount: 2,
            iterationBudget: 1));
        world.Step();
        world.Step();
        world.Step();

        var checkpoint = world.CreateCheckpoint();
        Assert.IsNotNull(checkpoint.Economy?.RegionalEvolution);
        Assert.AreEqual(3UL, checkpoint.Economy.RegionalEvolution.TicksPerYear);
        Assert.AreEqual(1, world.CreatePersistentRegionalEvolutionSnapshot().CurrentYear);

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        restored.Step();
        restored.Step();
        Assert.AreEqual(1, restored.CreatePersistentRegionalEvolutionSnapshot().CurrentYear);
        restored.Step();
        Assert.AreEqual(2, restored.CreatePersistentRegionalEvolutionSnapshot().CurrentYear);
    }

    [TestMethod]
    public void CheckpointRejectsBuildingThatReferencesMissingParcel()
    {
        var world = CreateWorld(31902);
        world.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(
            RegionalGenerationQualityPreset.Draft,
            settlementCount: 2,
            iterationBudget: 1));
        _ = world.CreatePersistentRegionalEvolutionSnapshot();
        var checkpoint = world.CreateCheckpoint();
        var evolution = checkpoint.Economy!.RegionalEvolution!;
        Assert.IsTrue(evolution.Snapshot.Buildings.Count > 0);
        var buildings = evolution.Snapshot.Buildings.ToArray();
        buildings[0] = buildings[0] with { ParcelId = new ParcelId(ulong.MaxValue) };
        var invalid = checkpoint with
        {
            Economy = checkpoint.Economy with
            {
                RegionalEvolution = evolution with
                {
                    Snapshot = evolution.Snapshot with { Buildings = buildings },
                },
            },
        };

        Assert.ThrowsExactly<ArgumentException>(() => SimulationWorld.RestoreCheckpoint(invalid));
    }

    [TestMethod]
    public void ReactivationDoesNotCreateBuildingUseChangeEvent()
    {
        var world = CreateWorld(31903);
        var generated = world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(
            RegionalGenerationQualityPreset.Draft,
            settlementCount: 2,
            iterationBudget: 1));
        var initial = PersistentRegionalEvolutionEngine.Initialize(generated);
        Assert.IsTrue(initial.Buildings.Count > 0);
        var building = initial.Buildings[0];
        var buildings = initial.Buildings.ToArray();
        buildings[0] = building with
        {
            Status = BuildingLifecycleStatus.Vacant,
            Occupancy = 1d,
            Condition = 1d,
        };
        var source = initial with { Buildings = buildings };

        var advanced = PersistentRegionalEvolutionEngine.AdvanceYears(
            source,
            generated,
            1,
            static _ => new RegionalEvolutionDrivers(1d, 1d, 1d, 1d, 1d, 1d));

        var actual = advanced.Buildings.First(item => item.BuildingId == building.BuildingId);
        Assert.AreEqual(BuildingLifecycleStatus.Active, actual.Status);
        Assert.IsFalse(advanced.Events.Any(item =>
            item.BuildingId == building.BuildingId
            && item.Kind == RegionalEvolutionEventKind.BuildingUseChanged));
    }

    [TestMethod]
    public void DerivedCollectionsRefreshFromAdjustedSettlementState()
    {
        var world = CreateWorld(31904);
        var generated = world.GenerateRegionalGeneration(CreateVolume(), new RegionalGenerationOptions(
            RegionalGenerationQualityPreset.Draft,
            settlementCount: 2,
            iterationBudget: 1));
        var initial = PersistentRegionalEvolutionEngine.Initialize(generated);
        var settlements = initial.Settlements.ToArray();
        var original = settlements[0];
        settlements[0] = original with
        {
            Accessibility = 0.05d,
            InfluenceRadiusMeters = original.InfluenceRadiusMeters * 2d,
        };

        var refreshed = PersistentRegionalEvolutionEngine.RefreshDerivedCollections(initial with { Settlements = settlements });

        var oldCatchment = initial.ServiceCatchments.First(item => item.SettlementId == original.SettlementId && item.Kind == RegionalServiceKind.Commerce);
        var newCatchment = refreshed.ServiceCatchments.First(item => item.SettlementId == original.SettlementId && item.Kind == RegionalServiceKind.Commerce);
        var oldRoadDemand = initial.InfrastructureDemands.First(item => item.SettlementId == original.SettlementId && item.Kind == InfrastructureDemandKind.Road);
        var newRoadDemand = refreshed.InfrastructureDemands.First(item => item.SettlementId == original.SettlementId && item.Kind == InfrastructureDemandKind.Road);
        Assert.IsGreaterThan(oldCatchment.RadiusMeters, newCatchment.RadiusMeters);
        Assert.AreNotEqual(oldRoadDemand.Demand, newRoadDemand.Demand);
    }

    [TestMethod]
    public void RelationAllocatorSurvivesCheckpointRestore()
    {
        var world = CreateWorld(31905);
        world.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        world.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 4, iterationBudget: 1),
            out _);
        world.AdvancePersistentRegionalEvolutionYears(2);
        var checkpoint = world.CreateCheckpoint();
        var evolution = checkpoint.Economy!.RegionalEvolution!;
        var maximumRelationId = evolution.Snapshot.Relations.Count == 0
            ? 0UL
            : evolution.Snapshot.Relations.Max(static item => item.Id.Value);
        Assert.IsGreaterThan(maximumRelationId, evolution.NextRelationId);

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        restored.AdvancePersistentRegionalEvolutionYears(1);
        var after = restored.CreateCheckpoint().Economy!.RegionalEvolution!;
        Assert.IsGreaterThanOrEqualTo(evolution.NextRelationId, after.NextRelationId);
    }

    [TestMethod]
    public void InitialRegionalBuildingDemolitionRemovesActualWorldObjectAndReferences()
    {
        var world = CreateWorld(31906);
        world.ConfigurePersistentRegionalEvolution(new PersistentRegionalEvolutionOptions(ticksPerYear: 1));
        world.InitializeRegionalWorld(
            CreateVolume(),
            new RegionalGenerationOptions(RegionalGenerationQualityPreset.Draft, settlementCount: 2, iterationBudget: 1),
            out _);
        _ = world.CreatePersistentRegionalEvolutionSnapshot();
        var beforeBuildingCount = world.BuildingCount;
        var checkpoint = world.CreateCheckpoint();
        var evolution = checkpoint.Economy!.RegionalEvolution!;
        var target = evolution.Snapshot.Buildings[0];
        var buildings = evolution.Snapshot.Buildings.ToArray();
        buildings[0] = target with
        {
            BuiltYear = -200,
            LastChangedYear = 0,
            Condition = 0.01d,
            Occupancy = 0.01d,
            Status = BuildingLifecycleStatus.Active,
        };
        var parcels = evolution.Snapshot.Parcels.ToArray();
        var parcelIndex = Array.FindIndex(parcels, item => item.ParcelId == target.ParcelId);
        Assert.IsGreaterThanOrEqualTo(0, parcelIndex);
        var targetParcel = parcels[parcelIndex];
        parcels[parcelIndex] = targetParcel with
        {
            DevelopmentDemand = 0d,
            LandValue = 0d,
            DevelopmentState = ParcelDevelopmentState.Occupied,
        };
        var settlements = evolution.Snapshot.Settlements
            .Select(item => item.SettlementId == targetParcel.SettlementId
                ? item with
                {
                    Population = 0,
                    Jobs = 0,
                    ServiceIndex = 0d,
                    Density = 0d,
                    Accessibility = 0d,
                    Trend = SettlementTrend.Declining,
                }
                : item)
            .ToArray();
        var prepared = checkpoint with
        {
            Economy = checkpoint.Economy with
            {
                RegionalEvolution = evolution with
                {
                    Snapshot = evolution.Snapshot with
                    {
                        Settlements = settlements,
                        Parcels = parcels,
                        Buildings = buildings,
                    },
                },
            },
        };

        var restored = SimulationWorld.RestoreCheckpoint(prepared);
        restored.AdvancePersistentRegionalEvolutionYears(1);
        var after = restored.CreatePersistentRegionalEvolutionSnapshot();
        var afterParcel = after.Parcels.First(item => item.ParcelId == target.ParcelId);

        Assert.AreEqual(beforeBuildingCount - 1, restored.BuildingCount);
        Assert.IsNull(afterParcel.BuildingId);
        Assert.AreEqual(ParcelDevelopmentState.Vacant, afterParcel.DevelopmentState);
    }

    private static SimulationWorld CreateWorld(ulong seed) =>
        new(new SimulationConfig(tickRate: 2, seed: seed, worldEnvironment: CreateConfig(seed + 10_000)));

    private static WorldEnvironmentConfig CreateConfig(ulong worldSeed) => new(
        worldSeed,
        new WorldVector(0.2d, 1d, 0d),
        latitudeDegrees: 43d,
        continentality: 0.54d,
        maritimeInfluence: 0.46d,
        meanAnnualTemperatureCelsius: 10.5d,
        seasonalityCelsius: 20d,
        annualPrecipitationMillimeters: 980d);

    private static WorldVolume CreateVolume() =>
        new(-1_000_000d, -1_000_000d, -12_000d, 1_000_000d, 1_000_000d, 12_000d);
}
