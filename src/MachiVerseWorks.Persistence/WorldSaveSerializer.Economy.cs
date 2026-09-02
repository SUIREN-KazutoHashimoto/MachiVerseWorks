using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static partial class WorldSaveSerializer
{
    private static void ValidateEconomyCheckpointWithinLimits(EconomyCheckpoint? economy, WorldSaveLimits limits)
    {
        if (economy is null) return;
        ValidateCount(economy.Companies.Count, limits.MaximumBuildingCount, "Companies");
        ValidateCount(economy.Establishments.Count, limits.MaximumBuildingCount, "Establishments");
        ValidateCount(economy.Jobs.Count, limits.MaximumPersonCount, "Jobs");
        ValidateCount(economy.Employments.Count, limits.MaximumPersonCount, "Employments");
        ValidateCount(economy.Households.Count, limits.MaximumHouseholdCount, "EconomyHouseholds");
        ValidateLogisticsCheckpointWithinLimits(economy.Logistics, limits);
        ValidatePowerCheckpointWithinLimits(economy.Power, limits);
        ValidateWaterSewerCheckpointWithinLimits(economy.WaterSewer, limits);
        ValidateGasCheckpointWithinLimits(economy.Gas, limits);
        ValidateOpticalCheckpointWithinLimits(economy.Optical, limits);
        ValidateWorldEnvironmentCheckpointWithinLimits(economy.WorldEnvironment, limits);
        ValidateRegionalGenerationCheckpointWithinLimits(economy.RegionalGeneration, limits);
    }

    private static void ValidateLogisticsCheckpointWithinLimits(LogisticsCheckpoint? logistics, WorldSaveLimits limits)
    {
        if (logistics is null) return;
        ValidateCount(logistics.Commodities.Count, limits.MaximumBuildingCount, "Commodities");
        ValidateCount(logistics.Inventories.Count, limits.MaximumBuildingCount, "Inventories");
        ValidateCount(logistics.Orders.Count, limits.MaximumPersonCount, "LogisticsOrders");
        ValidateCount(logistics.Shipments.Count, limits.MaximumVehicleCount, "Shipments");
    }

    private static void ValidatePowerCheckpointWithinLimits(PowerCheckpoint? power, WorldSaveLimits limits)
    {
        if (power is null) return;
        ValidateCount(power.Nodes.Count, limits.MaximumRoadNodeCount, "PowerNodes");
        ValidateCount(power.Lines.Count, limits.MaximumRoadSegmentCount, "PowerLines");
        ValidateCount(power.Generators.Count, limits.MaximumBuildingCount, "Generators");
        ValidateCount(power.Loads.Count, limits.MaximumBuildingCount, "PowerLoads");
    }

    private static void ValidateWaterSewerCheckpointWithinLimits(WaterSewerCheckpoint? waterSewer, WorldSaveLimits limits)
    {
        if (waterSewer is null) return;
        ValidateCount(waterSewer.WaterNodes.Count, limits.MaximumRoadNodeCount, "WaterNodes");
        ValidateCount(waterSewer.WaterPipes.Count, limits.MaximumRoadSegmentCount, "WaterPipes");
        ValidateCount(waterSewer.SewerNodes.Count, limits.MaximumRoadNodeCount, "SewerNodes");
        ValidateCount(waterSewer.SewerPipes.Count, limits.MaximumRoadSegmentCount, "SewerPipes");
        ValidateCount(waterSewer.WaterSources.Count, limits.MaximumBuildingCount, "WaterSources");
        ValidateCount(waterSewer.Reservoirs.Count, limits.MaximumBuildingCount, "Reservoirs");
        ValidateCount(waterSewer.Pumps.Count, limits.MaximumBuildingCount, "WaterSewerPumps");
        ValidateCount(waterSewer.TreatmentPlants.Count, limits.MaximumBuildingCount, "SewageTreatmentPlants");
        ValidateCount(waterSewer.ServicePoints.Count, limits.MaximumBuildingCount, "WaterSewerServicePoints");
    }

    private static void ValidateGasCheckpointWithinLimits(GasCheckpoint? gas, WorldSaveLimits limits)
    {
        if (gas is null) return;
        ValidateCount(gas.Nodes.Count, limits.MaximumRoadNodeCount, "GasNodes");
        ValidateCount(gas.Pipelines.Count, limits.MaximumRoadSegmentCount, "GasPipelines");
        ValidateCount(gas.Sources.Count, limits.MaximumBuildingCount, "GasSources");
        ValidateCount(gas.ImportTerminals.Count, limits.MaximumBuildingCount, "GasImportTerminals");
        ValidateCount(gas.Storages.Count, limits.MaximumBuildingCount, "GasStorages");
        ValidateCount(gas.ServicePoints.Count, limits.MaximumBuildingCount, "GasServicePoints");
    }

    private static void ValidateOpticalCheckpointWithinLimits(OpticalCheckpoint? optical, WorldSaveLimits limits)
    {
        if (optical is null) return;
        ValidateCount(optical.Nodes.Count, limits.MaximumRoadNodeCount, "OpticalNodes");
        ValidateCount(optical.FiberCables.Count, limits.MaximumRoadSegmentCount, "FiberCables");
        ValidateCount(optical.Equipment.Count, limits.MaximumBuildingCount, "OpticalEquipment");
        ValidateCount(optical.Backhauls.Count, limits.MaximumBuildingCount, "OpticalBackhauls");
        ValidateCount(optical.Demands.Count, limits.MaximumBuildingCount, "OpticalDemands");
    }

    private static void ValidateWorldEnvironmentCheckpointWithinLimits(WorldEnvironmentCheckpoint? worldEnvironment, WorldSaveLimits limits)
    {
        if (worldEnvironment is null) return;
        ValidateCount(worldEnvironment.Features.Count, limits.MaximumGeographicFeatureCount, "GeographicFeatures");
        ValidateCount(worldEnvironment.Toponyms.Count, limits.MaximumNaturalToponymCount, "NaturalToponyms");
        foreach (var feature in worldEnvironment.Features)
            ValidateCount(feature.Geometry.Count, limits.MaximumGeographicFeatureGeometryPointCount, "GeographicFeatureGeometryPoints");
    }

    private static void ValidateRegionalGenerationCheckpointWithinLimits(RegionalGenerationCheckpoint? regionalGeneration, WorldSaveLimits limits)
    {
        if (regionalGeneration is null) return;
        var snapshot = regionalGeneration.Snapshot;
        ValidateCount(snapshot.Settlements.Count, limits.MaximumBuildingCount, "RegionalSettlements");
        ValidateCount(snapshot.GrowthEvents.Count, limits.MaximumPersonCount, "RegionalGrowthEvents");
        ValidateCount(snapshot.Corridors.Count, limits.MaximumRoadSegmentCount, "RegionalCorridors");
        ValidateCount(snapshot.Districts.Count, limits.MaximumBuildingCount, "RegionalDistricts");
        ValidateCount(snapshot.Parcels.Count, limits.MaximumBuildingCount, "RegionalParcels");
        ValidateCount(snapshot.Buildings.Count, limits.MaximumBuildingCount, "RegionalGeneratedBuildings");
        ValidateCount(snapshot.Pois.Count, limits.MaximumPoiCount, "RegionalGeneratedPois");
        ValidateCount(snapshot.Toponyms.Count, limits.MaximumNaturalToponymCount, "RegionalHumanToponyms");
        ValidateCount(snapshot.RoadSigns.Count, limits.MaximumRoadAccessPointCount, "RegionalRoadSigns");
        foreach (var corridor in snapshot.Corridors)
            ValidateCount(corridor.Geometry.Count, limits.MaximumGeographicFeatureGeometryPointCount, "RegionalCorridorGeometryPoints");
    }
}
