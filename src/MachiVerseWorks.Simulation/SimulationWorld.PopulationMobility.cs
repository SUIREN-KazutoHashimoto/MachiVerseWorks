namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public bool RelocateHousehold(HouseholdId householdId, TripEndpoint residence)
    {
        if (residence.BuildingId is { } buildingId)
        {
            if (!TryGetBuildingSnapshot(buildingId, out _))
                throw new ArgumentException($"Building {buildingId.Value} does not exist.", nameof(residence));
        }
        else if (residence.PoiId is { } poiId)
        {
            if (!TryGetPoiSnapshot(poiId, out _))
                throw new ArgumentException($"POI {poiId.Value} does not exist.", nameof(residence));
        }
        else
        {
            throw new ArgumentException("Household residence must reference a Building or POI.", nameof(residence));
        }

        return _population.RelocateHousehold(householdId, residence);
    }
}
