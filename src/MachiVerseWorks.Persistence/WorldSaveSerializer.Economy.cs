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
}
