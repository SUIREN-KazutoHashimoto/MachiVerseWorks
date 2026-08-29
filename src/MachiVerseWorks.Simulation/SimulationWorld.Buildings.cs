using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly BuildingStore _buildings = new();
    private readonly PoiStore _pois = new();

    public int BuildingCount => _buildings.Count;

    public int PoiCount => _pois.Count;

    public BuildingId CreateBuilding(WorldVolume bounds, BuildingKind kind = BuildingKind.Generic)
    {
        ValidateBuildingKind(kind);
        _spatialIndex.ValidatePosition(new WorldPoint(bounds.MinX, bounds.MinY, bounds.MinZ));
        _spatialIndex.ValidatePosition(new WorldPoint(bounds.MaxX, bounds.MaxY, bounds.MaxZ));
        return _buildings.Add(kind, bounds);
    }

    public bool RemoveBuilding(BuildingId id)
    {
        if (!_buildings.Contains(id))
        {
            return false;
        }

        if (_pois.ContainsBuildingReference(id))
        {
            throw new InvalidOperationException(
                $"Building {id.Value} cannot be removed while one or more POIs reference it.");
        }

        return _buildings.Remove(id);
    }

    public bool TryGetBuildingSnapshot(BuildingId id, out BuildingSnapshot snapshot)
    {
        return _buildings.TryGetSnapshot(id, out snapshot);
    }

    public BuildingSnapshot[] CreateBuildingSnapshot()
    {
        return _buildings.CreateSnapshot();
    }

    public PoiId CreatePoi(
        WorldPoint position,
        PoiKind kind = PoiKind.Generic,
        BuildingId? buildingId = null)
    {
        ValidatePoint(position);
        ValidatePoiKind(kind);
        _spatialIndex.ValidatePosition(position);

        if (buildingId is { } linkedBuildingId)
        {
            if (!_buildings.TryGetSnapshot(linkedBuildingId, out var building))
            {
                throw new ArgumentException(
                    $"Building {linkedBuildingId.Value} does not exist.",
                    nameof(buildingId));
            }

            if (!building.Bounds.Contains(position))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position),
                    position,
                    $"POI position must be inside Building {linkedBuildingId.Value} bounds.");
            }
        }

        return _pois.Add(kind, position, buildingId);
    }

    public bool RemovePoi(PoiId id)
    {
        return _pois.Remove(id);
    }

    public bool TryGetPoiSnapshot(PoiId id, out PoiSnapshot snapshot)
    {
        return _pois.TryGetSnapshot(id, out snapshot);
    }

    public PoiSnapshot[] CreatePoiSnapshot()
    {
        return _pois.CreateSnapshot();
    }

    private static void ValidateBuildingKind(BuildingKind kind)
    {
        if (!Enum.IsDefined(typeof(BuildingKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Building kind is not defined.");
        }
    }

    private static void ValidatePoiKind(PoiKind kind)
    {
        if (!Enum.IsDefined(typeof(PoiKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "POI kind is not defined.");
        }
    }

    private static void ValidateUrbanObjectCheckpoint(
        SimulationCheckpoint checkpoint,
        double spatialCellSize)
    {
        if (checkpoint.NextBuildingId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.NextBuildingId,
                "Next Building ID must be greater than zero.");
        }

        if (checkpoint.NextPoiId == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.NextPoiId,
                "Next POI ID must be greater than zero.");
        }

        var buildingBounds = new Dictionary<ulong, WorldVolume>(checkpoint.Buildings.Count);
        var maximumBuildingId = 0UL;
        foreach (var building in checkpoint.Buildings)
        {
            if (building.Id.Value == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpoint),
                    building.Id.Value,
                    "Building IDs must be greater than zero.");
            }

            if (!buildingBounds.TryAdd(building.Id.Value, building.Bounds))
            {
                throw new ArgumentException(
                    $"Duplicate Building ID {building.Id.Value}.",
                    nameof(checkpoint));
            }

            ValidateBuildingKind(building.Kind);
            _ = SpatialGrid.ToCell(
                new WorldPoint(building.Bounds.MinX, building.Bounds.MinY, building.Bounds.MinZ),
                spatialCellSize);
            _ = SpatialGrid.ToCell(
                new WorldPoint(building.Bounds.MaxX, building.Bounds.MaxY, building.Bounds.MaxZ),
                spatialCellSize);
            maximumBuildingId = Math.Max(maximumBuildingId, building.Id.Value);
        }

        if (checkpoint.NextBuildingId <= maximumBuildingId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.NextBuildingId,
                "Next Building ID must be greater than every stored Building ID.");
        }

        var seenPoiIds = new HashSet<ulong>(checkpoint.Pois.Count);
        var maximumPoiId = 0UL;
        foreach (var poi in checkpoint.Pois)
        {
            if (poi.Id.Value == 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(checkpoint),
                    poi.Id.Value,
                    "POI IDs must be greater than zero.");
            }

            if (!seenPoiIds.Add(poi.Id.Value))
            {
                throw new ArgumentException($"Duplicate POI ID {poi.Id.Value}.", nameof(checkpoint));
            }

            ValidatePoiKind(poi.Kind);
            _ = SpatialGrid.ToCell(poi.Position, spatialCellSize);

            if (poi.BuildingId is { } buildingId)
            {
                if (!buildingBounds.TryGetValue(buildingId.Value, out var bounds))
                {
                    throw new ArgumentException(
                        $"POI {poi.Id.Value} references missing Building {buildingId.Value}.",
                        nameof(checkpoint));
                }

                if (!bounds.Contains(poi.Position))
                {
                    throw new ArgumentException(
                        $"POI {poi.Id.Value} lies outside referenced Building {buildingId.Value} bounds.",
                        nameof(checkpoint));
                }
            }

            maximumPoiId = Math.Max(maximumPoiId, poi.Id.Value);
        }

        if (checkpoint.NextPoiId <= maximumPoiId)
        {
            throw new ArgumentOutOfRangeException(
                nameof(checkpoint),
                checkpoint.NextPoiId,
                "Next POI ID must be greater than every stored POI ID.");
        }
    }

    private void RestoreUrbanObjects(SimulationCheckpoint checkpoint)
    {
        _buildings.Restore(checkpoint.Buildings, checkpoint.NextBuildingId);
        _pois.Restore(checkpoint.Pois, checkpoint.NextPoiId);
    }
}
