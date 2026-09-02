namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public bool TryGetGeneratedBuildingId(BuildingId buildingId, out GeneratedBuildingId generatedBuildingId)
    {
        foreach (var binding in _persistentRegionalMaterializations.Values)
        {
            if (binding.BuildingId != buildingId) continue;
            generatedBuildingId = binding.GeneratedBuildingId;
            return true;
        }

        generatedBuildingId = default;
        return false;
    }
}
