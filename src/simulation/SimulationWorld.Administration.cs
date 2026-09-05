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
            if (poi.BuildingId == id && !bounds.Contains(poi.Position))
                throw new InvalidOperationException($"Building {id.Value} bounds cannot exclude referenced POI {poi.Id.Value}.");
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

    public bool UpdateTrackNode(TrackNodeId id, WorldPoint position, TrackNodeKind kind)
    {
        ValidatePoint(position);
        ValidateEnum(kind, nameof(kind));
        EnsureRailwayInfrastructureMutable();
        return MutateRailwayCheckpoint(cp =>
        {
            var items = Replace(cp.TrackNodes ?? [], id, static x => x.Id, new SimulationTrackNodeCheckpoint(id, kind, position), out var changed);
            return (cp with { TrackNodes = items }, changed);
        });
    }

    public bool RemoveTrackNode(TrackNodeId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.TrackNodes ?? [], (cp, items) => cp with { TrackNodes = items });

    public bool UpdateTrackSegment(TrackSegmentId id, TrackNodeId startNodeId, TrackNodeId endNodeId, TrackDirection direction, double gaugeMeters, double speedLimitMetersPerSecond, TrackElectrification electrification, TrackUsage usage)
    {
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationTrackSegmentCheckpoint(id, startNodeId, endNodeId, direction, gaugeMeters, speedLimitMetersPerSecond, electrification, usage);
        return MutateRailwayCheckpoint(cp => (cp with { TrackSegments = Replace(cp.TrackSegments ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemoveTrackSegment(TrackSegmentId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.TrackSegments ?? [], (cp, items) => cp with { TrackSegments = items });

    public bool UpdateTrackConnection(TrackConnectionId id, TrackSegmentId fromSegmentId, TrackSegmentId toSegmentId, TrackNodeId viaNodeId)
    {
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationTrackConnectionCheckpoint(id, fromSegmentId, toSegmentId, viaNodeId);
        return MutateRailwayCheckpoint(cp => (cp with { TrackConnections = Replace(cp.TrackConnections ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemoveTrackConnection(TrackConnectionId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.TrackConnections ?? [], (cp, items) => cp with { TrackConnections = items });

    public bool UpdateBlockSection(BlockSectionId id, IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationBlockSectionCheckpoint(id, trackSegmentIds.ToArray());
        return MutateRailwayCheckpoint(cp => (cp with { BlockSections = Replace(cp.BlockSections ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemoveBlockSection(BlockSectionId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.BlockSections ?? [], (cp, items) => cp with { BlockSections = items });

    public bool UpdateStation(StationId id, WorldVolume bounds)
    {
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationStationCheckpoint(id, bounds);
        return MutateRailwayCheckpoint(cp => (cp with { Stations = Replace(cp.Stations ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemoveStation(StationId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.Stations ?? [], (cp, items) => cp with { Stations = items });

    public bool UpdatePlatform(PlatformId id, StationId stationId, TrackSegmentId trackSegmentId, double startSegmentOffset, double endSegmentOffset, WorldVolume bounds)
    {
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationPlatformCheckpoint(id, stationId, trackSegmentId, startSegmentOffset, endSegmentOffset, bounds);
        return MutateRailwayCheckpoint(cp => (cp with { Platforms = Replace(cp.Platforms ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemovePlatform(PlatformId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.Platforms ?? [], (cp, items) => cp with { Platforms = items });

    public bool UpdatePlatformAccessPoint(PlatformAccessPointId id, PlatformId platformId, RoadAccessPointId roadAccessPointId)
    {
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationPlatformAccessPointCheckpoint(id, platformId, roadAccessPointId);
        return MutateRailwayCheckpoint(cp => (cp with { PlatformAccessPoints = Replace(cp.PlatformAccessPoints ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemovePlatformAccessPoint(PlatformAccessPointId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.PlatformAccessPoints ?? [], (cp, items) => cp with { PlatformAccessPoints = items });

    public bool UpdateDepot(DepotId id, WorldVolume bounds, IReadOnlyList<TrackSegmentId> trackSegmentIds)
    {
        ArgumentNullException.ThrowIfNull(trackSegmentIds);
        EnsureRailwayInfrastructureMutable();
        var value = new SimulationDepotCheckpoint(id, bounds, trackSegmentIds.ToArray());
        return MutateRailwayCheckpoint(cp => (cp with { Depots = Replace(cp.Depots ?? [], id, static x => x.Id, value, out var changed) }, changed));
    }

    public bool RemoveDepot(DepotId id) => RemoveRailwayItem(id, static x => x.Id, cp => cp.Depots ?? [], (cp, items) => cp with { Depots = items });

    private bool MutateRailwayCheckpoint(Func<SimulationCheckpoint, (SimulationCheckpoint Checkpoint, bool Changed)> mutation)
    {
        EnsureRailwayInfrastructureMutable();
        var current = CreateRailwayAdministrationCheckpoint();
        var result = mutation(current);
        if (!result.Changed) return false;

        var candidate = result.Checkpoint with
        {
            RoadAccessPoints = CaptureRailwayReferencedRoadAccessPoints(result.Checkpoint.PlatformAccessPoints ?? []),
        };
        ValidateRailwayCheckpoint(candidate, Config.SpatialCellSize);
        _railway.Restore(candidate);
        return true;
    }

    private SimulationCheckpoint CreateRailwayAdministrationCheckpoint()
    {
        var platformAccessPoints = _railway.CreatePlatformAccessPointCheckpoint();
        return new SimulationCheckpoint(
            Config.TickRate,
            Config.Seed,
            Config.SpatialCellSize,
            Time.TickCount,
            Time.Elapsed.Ticks,
            _random.State,
            1,
            Array.Empty<SimulationAgentCheckpoint>(),
            1,
            Array.Empty<SimulationBuildingCheckpoint>(),
            1,
            Array.Empty<SimulationPoiCheckpoint>(),
            1,
            Array.Empty<SimulationRoadNodeCheckpoint>(),
            1,
            Array.Empty<SimulationRoadSegmentCheckpoint>(),
            1,
            Array.Empty<SimulationLaneCheckpoint>(),
            1,
            Array.Empty<SimulationLaneConnectionCheckpoint>(),
            1,
            CaptureRailwayReferencedRoadAccessPoints(platformAccessPoints),
            NextTrackNodeId: _railway.NextNodeId,
            TrackNodes: _railway.CreateNodeCheckpoint(),
            NextTrackSegmentId: _railway.NextSegmentId,
            TrackSegments: _railway.CreateSegmentCheckpoint(),
            NextTrackConnectionId: _railway.NextConnectionId,
            TrackConnections: _railway.CreateConnectionCheckpoint(),
            NextBlockSectionId: _railway.NextBlockId,
            BlockSections: _railway.CreateBlockCheckpoint(),
            NextStationId: _railway.NextStationId,
            Stations: _railway.CreateStationCheckpoint(),
            NextPlatformId: _railway.NextPlatformId,
            Platforms: _railway.CreatePlatformCheckpoint(),
            NextPlatformAccessPointId: _railway.NextPlatformAccessPointId,
            PlatformAccessPoints: platformAccessPoints,
            NextDepotId: _railway.NextDepotId,
            Depots: _railway.CreateDepotCheckpoint());
    }

    private SimulationRoadAccessPointCheckpoint[] CaptureRailwayReferencedRoadAccessPoints(IReadOnlyList<SimulationPlatformAccessPointCheckpoint> platformAccessPoints)
    {
        var result = new List<SimulationRoadAccessPointCheckpoint>();
        foreach (var id in platformAccessPoints.Select(static item => item.RoadAccessPointId).Distinct().OrderBy(static item => item.Value))
        {
            if (!_roads.TryGetAccessPoint(id, out var accessPoint)) continue;
            result.Add(new SimulationRoadAccessPointCheckpoint(
                accessPoint.Id,
                accessPoint.SegmentId,
                accessPoint.SegmentOffset,
                accessPoint.BuildingId,
                accessPoint.PoiId,
                accessPoint.Mode));
        }
        return result.ToArray();
    }

    private bool RemoveRailwayItem<T, TId>(TId id, Func<T, TId> idSelector, Func<SimulationCheckpoint, IReadOnlyList<T>> selector, Func<SimulationCheckpoint, IReadOnlyList<T>, SimulationCheckpoint> assign)
        where TId : struct, IEquatable<TId>
    {
        EnsureRailwayInfrastructureMutable();
        return MutateRailwayCheckpoint(cp =>
        {
            var source = selector(cp);
            var items = source.Where(x => !idSelector(x).Equals(id)).ToArray();
            return (assign(cp, items), items.Length != source.Count);
        });
    }

    private static T[] Replace<T, TId>(IReadOnlyList<T> source, TId id, Func<T, TId> idSelector, T value, out bool changed)
        where TId : struct, IEquatable<TId>
    {
        var items = source.ToArray();
        changed = false;
        for (var index = 0; index < items.Length; index++)
        {
            if (!idSelector(items[index]).Equals(id)) continue;
            items[index] = value;
            changed = true;
            break;
        }
        return items;
    }
}
