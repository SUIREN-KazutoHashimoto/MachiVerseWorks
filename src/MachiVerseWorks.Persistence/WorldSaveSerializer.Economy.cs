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
    }
}
