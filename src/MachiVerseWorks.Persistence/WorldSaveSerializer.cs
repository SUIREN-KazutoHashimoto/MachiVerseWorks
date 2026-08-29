using System.Text.Json;
using System.Text.Json.Serialization;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static class WorldSaveSerializer
{
    private const int StreamReadBufferSize = 81_920;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
        MaxDepth = 16,
    };

    public static byte[] Serialize(SimulationWorld world) => Serialize(world, WorldSaveLimits.Default);
    public static byte[] Serialize(SimulationWorld world, WorldSaveLimits limits)
    {
        using var buffer = SerializeToBuffer(world, limits); return buffer.ToArray();
    }
    public static void Save(Stream destination, SimulationWorld world) => Save(destination, world, WorldSaveLimits.Default);
    public static void Save(Stream destination, SimulationWorld world, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(destination); ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(limits);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        using var buffer = SerializeToBuffer(world, limits); buffer.WriteTo(destination);
    }
    public static SimulationWorld Deserialize(ReadOnlySpan<byte> utf8Json) => Deserialize(utf8Json, WorldSaveLimits.Default);
    public static SimulationWorld Deserialize(ReadOnlySpan<byte> utf8Json, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (utf8Json.Length > limits.MaximumBytes) throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
        try
        {
            ValidateCollectionCountsBeforeMaterialization(utf8Json, limits);
            var document = JsonSerializer.Deserialize<SaveDataDocument>(utf8Json, JsonOptions) ?? throw new InvalidDataException("Save Data document is empty.");
            return RestoreDocument(document, limits);
        }
        catch (InvalidDataException) { throw; }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Save Data is malformed or contains invalid values.", exception);
        }
    }
    public static SimulationWorld Load(Stream source) => Load(source, WorldSaveLimits.Default);
    public static SimulationWorld Load(Stream source, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(limits);
        if (!source.CanRead) throw new ArgumentException("Source stream must be readable.", nameof(source));
        if (source.CanSeek && source.Length - source.Position > limits.MaximumBytes) throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
        using var buffer = new MemoryStream();
        var readBuffer = new byte[Math.Min(StreamReadBufferSize, limits.MaximumBytes)];
        while (true)
        {
            var read = source.Read(readBuffer, 0, readBuffer.Length); if (read == 0) break;
            if (buffer.Length > limits.MaximumBytes - read) throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
            buffer.Write(readBuffer, 0, read);
        }
        return Deserialize(buffer.ToArray(), limits);
    }

    private static BoundedSaveBuffer SerializeToBuffer(SimulationWorld world, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(world); ArgumentNullException.ThrowIfNull(limits);
        var checkpoint = world.CreateCheckpoint(); ValidateCheckpointWithinLimits(checkpoint, limits);
        var buffer = new BoundedSaveBuffer(limits.MaximumBytes);
        try { JsonSerializer.Serialize(buffer, CreateDocument(checkpoint), JsonOptions); return buffer; }
        catch { buffer.Dispose(); throw; }
    }

    private static void ValidateCheckpointWithinLimits(SimulationCheckpoint c, WorldSaveLimits l)
    {
        ValidateCount(c.Agents.Count, l.MaximumAgentCount, "Agents");
        ValidateCount(c.Buildings.Count, l.MaximumBuildingCount, "Buildings");
        ValidateCount(c.Pois.Count, l.MaximumPoiCount, "POIs");
        ValidateCount(c.RoadNodes.Count, l.MaximumRoadNodeCount, "RoadNodes");
        ValidateCount(c.RoadSegments.Count, l.MaximumRoadSegmentCount, "RoadSegments");
        ValidateCount(c.Lanes.Count, l.MaximumLaneCount, "Lanes");
        ValidateCount(c.LaneConnections.Count, l.MaximumLaneConnectionCount, "LaneConnections");
        ValidateCount(c.RoadAccessPoints.Count, l.MaximumRoadAccessPointCount, "RoadAccessPoints");
    }

    private static SaveDataDocument CreateDocument(SimulationCheckpoint c)
    {
        var agents = c.Agents.Select(static a => new SaveAgentData { Id = a.Id.Value, X = a.Position.X, Y = a.Position.Y, Z = a.Position.Z, VelocityX = a.Velocity.X, VelocityY = a.Velocity.Y, VelocityZ = a.Velocity.Z, IsActive = a.IsActive }).ToArray();
        var buildings = c.Buildings.Select(static b => new SaveBuildingData { Id = b.Id.Value, Kind = (byte)b.Kind, MinX = b.Bounds.MinX, MinY = b.Bounds.MinY, MinZ = b.Bounds.MinZ, MaxX = b.Bounds.MaxX, MaxY = b.Bounds.MaxY, MaxZ = b.Bounds.MaxZ }).ToArray();
        var pois = c.Pois.Select(static p => new SavePoiData { Id = p.Id.Value, Kind = (byte)p.Kind, X = p.Position.X, Y = p.Position.Y, Z = p.Position.Z, BuildingId = p.BuildingId?.Value }).ToArray();
        var roadNodes = c.RoadNodes.Select(static n => new SaveRoadNodeData { Id = n.Id.Value, Kind = (byte)n.Kind, X = n.Position.X, Y = n.Position.Y, Z = n.Position.Z }).ToArray();
        var roadSegments = c.RoadSegments.Select(static s => new SaveRoadSegmentData { Id = s.Id.Value, Kind = (byte)s.Kind, StartNodeId = s.StartNodeId.Value, EndNodeId = s.EndNodeId.Value }).ToArray();
        var lanes = c.Lanes.Select(static lane => new SaveLaneData { Id = lane.Id.Value, SegmentId = lane.SegmentId.Value, Direction = (byte)lane.Direction, Order = lane.Order, WidthMeters = lane.WidthMeters, SpeedLimitMetersPerSecond = lane.SpeedLimitMetersPerSecond }).ToArray();
        var connections = c.LaneConnections.Select(static x => new SaveLaneConnectionData { Id = x.Id.Value, FromLaneId = x.FromLaneId.Value, ToLaneId = x.ToLaneId.Value, ViaNodeId = x.ViaNodeId.Value, Movement = (byte)x.Movement }).ToArray();
        var access = c.RoadAccessPoints.Select(static a => new SaveRoadAccessPointData { Id = a.Id.Value, SegmentId = a.SegmentId.Value, SegmentOffset = a.SegmentOffset, BuildingId = a.BuildingId?.Value, PoiId = a.PoiId?.Value, Mode = (byte)a.Mode }).ToArray();
        return new SaveDataDocument
        {
            FormatVersion = SaveFormatVersion.Current,
            Simulation = new SaveSimulationData
            {
                TickRate = c.TickRate, Seed = c.Seed, SpatialCellSize = c.SpatialCellSize, TickCount = c.TickCount, ElapsedTicks = c.ElapsedTicks, RandomState = c.RandomState,
                NextAgentId = c.NextAgentId, Agents = agents, NextBuildingId = c.NextBuildingId, Buildings = buildings, NextPoiId = c.NextPoiId, Pois = pois,
                NextRoadNodeId = c.NextRoadNodeId, RoadNodes = roadNodes, NextRoadSegmentId = c.NextRoadSegmentId, RoadSegments = roadSegments,
                NextLaneId = c.NextLaneId, Lanes = lanes, NextLaneConnectionId = c.NextLaneConnectionId, LaneConnections = connections,
                NextRoadAccessPointId = c.NextRoadAccessPointId, RoadAccessPoints = access,
            },
        };
    }

    private static SimulationWorld RestoreDocument(SaveDataDocument document, WorldSaveLimits limits)
    {
        var format = Require(document.FormatVersion, "formatVersion");
        if (format is not (SaveFormatVersion.BuildingPoi or SaveFormatVersion.RoadNetwork)) throw new InvalidDataException($"Unsupported Save format version {format}. Expected {SaveFormatVersion.Current} or migratable version {SaveFormatVersion.BuildingPoi}.");
        var s = document.Simulation ?? throw new InvalidDataException("Save Data is missing simulation state.");
        var savedAgents = s.Agents ?? throw new InvalidDataException("Save Data is missing Agent state.");
        var savedBuildings = s.Buildings ?? throw new InvalidDataException("Save Data is missing Building state.");
        var savedPois = s.Pois ?? throw new InvalidDataException("Save Data is missing POI state.");
        var roadNodesData = format == SaveFormatVersion.RoadNetwork ? s.RoadNodes ?? throw new InvalidDataException("Save Data is missing RoadNode state.") : [];
        var roadSegmentsData = format == SaveFormatVersion.RoadNetwork ? s.RoadSegments ?? throw new InvalidDataException("Save Data is missing RoadSegment state.") : [];
        var lanesData = format == SaveFormatVersion.RoadNetwork ? s.Lanes ?? throw new InvalidDataException("Save Data is missing Lane state.") : [];
        var connectionsData = format == SaveFormatVersion.RoadNetwork ? s.LaneConnections ?? throw new InvalidDataException("Save Data is missing LaneConnection state.") : [];
        var accessData = format == SaveFormatVersion.RoadNetwork ? s.RoadAccessPoints ?? throw new InvalidDataException("Save Data is missing RoadAccessPoint state.") : [];
        ValidateMaterializedCounts(savedAgents.Length, savedBuildings.Length, savedPois.Length, roadNodesData.Length, roadSegmentsData.Length, lanesData.Length, connectionsData.Length, accessData.Length, limits);

        var agents = new SimulationAgentCheckpoint[savedAgents.Length];
        for (var i = 0; i < agents.Length; i++)
        {
            var a = savedAgents[i] ?? throw new InvalidDataException($"Agent entry {i} is null.");
            agents[i] = new SimulationAgentCheckpoint(new AgentId(Require(a.Id, $"agents[{i}].id")), new WorldPoint(Require(a.X, $"agents[{i}].x"), Require(a.Y, $"agents[{i}].y"), Require(a.Z, $"agents[{i}].z")), new WorldVector(Require(a.VelocityX, $"agents[{i}].velocityX"), Require(a.VelocityY, $"agents[{i}].velocityY"), Require(a.VelocityZ, $"agents[{i}].velocityZ")), Require(a.IsActive, $"agents[{i}].isActive"));
        }
        var buildings = new SimulationBuildingCheckpoint[savedBuildings.Length];
        for (var i = 0; i < buildings.Length; i++)
        {
            var b = savedBuildings[i] ?? throw new InvalidDataException($"Building entry {i} is null.");
            buildings[i] = new SimulationBuildingCheckpoint(new BuildingId(Require(b.Id, $"buildings[{i}].id")), (BuildingKind)Require(b.Kind, $"buildings[{i}].kind"), new WorldVolume(Require(b.MinX, $"buildings[{i}].minX"), Require(b.MinY, $"buildings[{i}].minY"), Require(b.MinZ, $"buildings[{i}].minZ"), Require(b.MaxX, $"buildings[{i}].maxX"), Require(b.MaxY, $"buildings[{i}].maxY"), Require(b.MaxZ, $"buildings[{i}].maxZ")));
        }
        var pois = new SimulationPoiCheckpoint[savedPois.Length];
        for (var i = 0; i < pois.Length; i++)
        {
            var p = savedPois[i] ?? throw new InvalidDataException($"POI entry {i} is null.");
            pois[i] = new SimulationPoiCheckpoint(new PoiId(Require(p.Id, $"pois[{i}].id")), (PoiKind)Require(p.Kind, $"pois[{i}].kind"), new WorldPoint(Require(p.X, $"pois[{i}].x"), Require(p.Y, $"pois[{i}].y"), Require(p.Z, $"pois[{i}].z")), p.BuildingId is { } buildingId ? new BuildingId(buildingId) : null);
        }
        var roadNodes = new SimulationRoadNodeCheckpoint[roadNodesData.Length];
        for (var i = 0; i < roadNodes.Length; i++) { var n = roadNodesData[i] ?? throw new InvalidDataException($"RoadNode entry {i} is null."); roadNodes[i] = new SimulationRoadNodeCheckpoint(new RoadNodeId(Require(n.Id, $"roadNodes[{i}].id")), (RoadNodeKind)Require(n.Kind, $"roadNodes[{i}].kind"), new WorldPoint(Require(n.X, $"roadNodes[{i}].x"), Require(n.Y, $"roadNodes[{i}].y"), Require(n.Z, $"roadNodes[{i}].z"))); }
        var roadSegments = new SimulationRoadSegmentCheckpoint[roadSegmentsData.Length];
        for (var i = 0; i < roadSegments.Length; i++) { var x = roadSegmentsData[i] ?? throw new InvalidDataException($"RoadSegment entry {i} is null."); roadSegments[i] = new SimulationRoadSegmentCheckpoint(new RoadSegmentId(Require(x.Id, $"roadSegments[{i}].id")), (RoadKind)Require(x.Kind, $"roadSegments[{i}].kind"), new RoadNodeId(Require(x.StartNodeId, $"roadSegments[{i}].startNodeId")), new RoadNodeId(Require(x.EndNodeId, $"roadSegments[{i}].endNodeId"))); }
        var lanes = new SimulationLaneCheckpoint[lanesData.Length];
        for (var i = 0; i < lanes.Length; i++) { var x = lanesData[i] ?? throw new InvalidDataException($"Lane entry {i} is null."); lanes[i] = new SimulationLaneCheckpoint(new LaneId(Require(x.Id, $"lanes[{i}].id")), new RoadSegmentId(Require(x.SegmentId, $"lanes[{i}].segmentId")), (LaneDirection)Require(x.Direction, $"lanes[{i}].direction"), Require(x.Order, $"lanes[{i}].order"), Require(x.WidthMeters, $"lanes[{i}].widthMeters"), Require(x.SpeedLimitMetersPerSecond, $"lanes[{i}].speedLimitMetersPerSecond")); }
        var connections = new SimulationLaneConnectionCheckpoint[connectionsData.Length];
        for (var i = 0; i < connections.Length; i++) { var x = connectionsData[i] ?? throw new InvalidDataException($"LaneConnection entry {i} is null."); connections[i] = new SimulationLaneConnectionCheckpoint(new LaneConnectionId(Require(x.Id, $"laneConnections[{i}].id")), new LaneId(Require(x.FromLaneId, $"laneConnections[{i}].fromLaneId")), new LaneId(Require(x.ToLaneId, $"laneConnections[{i}].toLaneId")), new RoadNodeId(Require(x.ViaNodeId, $"laneConnections[{i}].viaNodeId")), (TurnMovement)Require(x.Movement, $"laneConnections[{i}].movement")); }
        var access = new SimulationRoadAccessPointCheckpoint[accessData.Length];
        for (var i = 0; i < access.Length; i++) { var x = accessData[i] ?? throw new InvalidDataException($"RoadAccessPoint entry {i} is null."); access[i] = new SimulationRoadAccessPointCheckpoint(new RoadAccessPointId(Require(x.Id, $"roadAccessPoints[{i}].id")), new RoadSegmentId(Require(x.SegmentId, $"roadAccessPoints[{i}].segmentId")), Require(x.SegmentOffset, $"roadAccessPoints[{i}].segmentOffset"), x.BuildingId is { } buildingId ? new BuildingId(buildingId) : null, x.PoiId is { } poiId ? new PoiId(poiId) : null, (RoadAccessMode)Require(x.Mode, $"roadAccessPoints[{i}].mode")); }

        var checkpoint = new SimulationCheckpoint(
            Require(s.TickRate, "simulation.tickRate"), Require(s.Seed, "simulation.seed"), Require(s.SpatialCellSize, "simulation.spatialCellSize"), Require(s.TickCount, "simulation.tickCount"), Require(s.ElapsedTicks, "simulation.elapsedTicks"), Require(s.RandomState, "simulation.randomState"),
            Require(s.NextAgentId, "simulation.nextAgentId"), agents, Require(s.NextBuildingId, "simulation.nextBuildingId"), buildings, Require(s.NextPoiId, "simulation.nextPoiId"), pois,
            format == SaveFormatVersion.RoadNetwork ? Require(s.NextRoadNodeId, "simulation.nextRoadNodeId") : 1UL, roadNodes,
            format == SaveFormatVersion.RoadNetwork ? Require(s.NextRoadSegmentId, "simulation.nextRoadSegmentId") : 1UL, roadSegments,
            format == SaveFormatVersion.RoadNetwork ? Require(s.NextLaneId, "simulation.nextLaneId") : 1UL, lanes,
            format == SaveFormatVersion.RoadNetwork ? Require(s.NextLaneConnectionId, "simulation.nextLaneConnectionId") : 1UL, connections,
            format == SaveFormatVersion.RoadNetwork ? Require(s.NextRoadAccessPointId, "simulation.nextRoadAccessPointId") : 1UL, access);
        return SimulationWorld.RestoreCheckpoint(checkpoint);
    }

    private static void ValidateMaterializedCounts(int agents, int buildings, int pois, int nodes, int segments, int lanes, int connections, int access, WorldSaveLimits l)
    {
        ValidateCount(agents, l.MaximumAgentCount, "Agents"); ValidateCount(buildings, l.MaximumBuildingCount, "Buildings"); ValidateCount(pois, l.MaximumPoiCount, "POIs");
        ValidateCount(nodes, l.MaximumRoadNodeCount, "RoadNodes"); ValidateCount(segments, l.MaximumRoadSegmentCount, "RoadSegments"); ValidateCount(lanes, l.MaximumLaneCount, "Lanes");
        ValidateCount(connections, l.MaximumLaneConnectionCount, "LaneConnections"); ValidateCount(access, l.MaximumRoadAccessPointCount, "RoadAccessPoints");
    }

    private static void ValidateCount(int count, int maximum, string name)
    {
        if (count > maximum) throw new InvalidDataException($"Save Data contains {count} {name}, exceeding the configured {maximum}-{name} limit.");
    }

    private static void ValidateCollectionCountsBeforeMaterialization(ReadOnlySpan<byte> json, WorldSaveLimits l)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = JsonOptions.MaxDepth });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            if (reader.ValueTextEquals("agents")) ValidateNamedArrayElementCount(ref reader, l.MaximumAgentCount, "Agent");
            else if (reader.ValueTextEquals("buildings")) ValidateNamedArrayElementCount(ref reader, l.MaximumBuildingCount, "Building");
            else if (reader.ValueTextEquals("pois")) ValidateNamedArrayElementCount(ref reader, l.MaximumPoiCount, "POI");
            else if (reader.ValueTextEquals("roadNodes")) ValidateNamedArrayElementCount(ref reader, l.MaximumRoadNodeCount, "RoadNode");
            else if (reader.ValueTextEquals("roadSegments")) ValidateNamedArrayElementCount(ref reader, l.MaximumRoadSegmentCount, "RoadSegment");
            else if (reader.ValueTextEquals("lanes")) ValidateNamedArrayElementCount(ref reader, l.MaximumLaneCount, "Lane");
            else if (reader.ValueTextEquals("laneConnections")) ValidateNamedArrayElementCount(ref reader, l.MaximumLaneConnectionCount, "LaneConnection");
            else if (reader.ValueTextEquals("roadAccessPoints")) ValidateNamedArrayElementCount(ref reader, l.MaximumRoadAccessPointCount, "RoadAccessPoint");
        }
    }

    private static void ValidateNamedArrayElementCount(ref Utf8JsonReader reader, int maximumCount, string entityName)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) return;
        var arrayDepth = reader.CurrentDepth; var count = 0;
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == arrayDepth) return;
            if (reader.CurrentDepth != arrayDepth + 1 || reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray or JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null)) continue;
            if (++count > maximumCount) throw new InvalidDataException($"Save Data {entityName} count exceeds the configured {maximumCount}-{entityName} limit before deserialization.");
        }
    }

    private static T Require<T>(T? value, string fieldName) where T : struct => value ?? throw new InvalidDataException($"Save Data is missing required field '{fieldName}'.");

    private sealed class BoundedSaveBuffer : Stream
    {
        private readonly int maximumBytes;
        private readonly List<byte[]> segments = [];
        private byte[]? currentSegment;
        private int currentSegmentOffset;
        private int length;
        public BoundedSaveBuffer(int maximumBytes) => this.maximumBytes = maximumBytes;
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true; public override long Length => length;
        public override long Position { get => length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) { ArgumentNullException.ThrowIfNull(buffer); Write(buffer.AsSpan(offset, count)); }
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > maximumBytes - length) throw new InvalidDataException($"Save Data output exceeds the configured {maximumBytes}-byte limit.");
            while (!buffer.IsEmpty)
            {
                if (currentSegment is null || currentSegmentOffset == currentSegment.Length)
                {
                    var segmentLength = Math.Min(StreamReadBufferSize, maximumBytes - length); currentSegment = new byte[segmentLength]; currentSegmentOffset = 0; segments.Add(currentSegment);
                }
                var copyLength = Math.Min(buffer.Length, currentSegment.Length - currentSegmentOffset); buffer[..copyLength].CopyTo(currentSegment.AsSpan(currentSegmentOffset, copyLength)); currentSegmentOffset += copyLength; length += copyLength; buffer = buffer[copyLength..];
            }
        }
        public byte[] ToArray() { var result = new byte[length]; CopyTo(result); return result; }
        public void WriteTo(Stream destination)
        {
            ArgumentNullException.ThrowIfNull(destination); var remaining = length;
            foreach (var segment in segments) { var count = Math.Min(segment.Length, remaining); destination.Write(segment.AsSpan(0, count)); remaining -= count; if (remaining == 0) break; }
        }
        private void CopyTo(Span<byte> destination)
        {
            var offset = 0; var remaining = length;
            foreach (var segment in segments) { var count = Math.Min(segment.Length, remaining); segment.AsSpan(0, count).CopyTo(destination[offset..]); offset += count; remaining -= count; if (remaining == 0) break; }
        }
    }
}
