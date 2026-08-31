namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    public bool UpdateAgent(AgentId id, WorldPoint position, WorldVector velocity)
    {
        ValidatePoint(position);
        ValidateVector(velocity);
        return _agents.Update(id, position, velocity, _spatialIndex);
    }

    public bool UpdateBuilding(BuildingId id, WorldVolume bounds, BuildingKind kind)
    {
        ValidateBuildingKind(kind);
        _spatialIndex.ValidatePosition(new WorldPoint(bounds.MinX, bounds.MinY, bounds.MinZ));
        _spatialIndex.ValidatePosition(new WorldPoint(bounds.MaxX, bounds.MaxY, bounds.MaxZ));
        if (!_buildings.Contains(id)) return false;
        foreach (var poi in _pois.CreateSnapshot())
        {
            if (poi.BuildingId == id && !bounds.Contains(poi.Position))
                throw new InvalidOperationException($"Building {id.Value} bounds cannot exclude referenced POI {poi.Id.Value}.");
        }
        return _buildings.Update(id, kind, bounds);
    }

    public bool UpdatePoi(PoiId id, WorldPoint position, PoiKind kind, BuildingId? buildingId = null)
    {
        ValidatePoint(position);
        ValidatePoiKind(kind);
        _spatialIndex.ValidatePosition(position);
        if (!_pois.TryGetSnapshot(id, out _)) return false;
        if (buildingId is { } linkedBuildingId)
        {
            if (!_buildings.TryGetSnapshot(linkedBuildingId, out var building))
                throw new ArgumentException($"Building {linkedBuildingId.Value} does not exist.", nameof(buildingId));
            if (!building.Bounds.Contains(position))
                throw new ArgumentOutOfRangeException(nameof(position), position, $"POI position must be inside Building {linkedBuildingId.Value} bounds.");
        }
        return _pois.Update(id, kind, position, buildingId);
    }
}
