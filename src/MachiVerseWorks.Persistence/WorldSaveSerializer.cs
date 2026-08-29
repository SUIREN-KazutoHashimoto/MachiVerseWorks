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

    public static byte[] Serialize(SimulationWorld world)
    {
        return Serialize(world, WorldSaveLimits.Default);
    }

    public static byte[] Serialize(SimulationWorld world, WorldSaveLimits limits)
    {
        using var buffer = SerializeToBuffer(world, limits);
        return buffer.ToArray();
    }

    public static void Save(Stream destination, SimulationWorld world)
    {
        Save(destination, world, WorldSaveLimits.Default);
    }

    public static void Save(Stream destination, SimulationWorld world, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(limits);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        using var buffer = SerializeToBuffer(world, limits);
        buffer.WriteTo(destination);
    }

    public static SimulationWorld Deserialize(ReadOnlySpan<byte> utf8Json)
    {
        return Deserialize(utf8Json, WorldSaveLimits.Default);
    }

    public static SimulationWorld Deserialize(ReadOnlySpan<byte> utf8Json, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (utf8Json.Length > limits.MaximumBytes)
        {
            throw new InvalidDataException(
                $"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
        }

        try
        {
            ValidateCollectionCountsBeforeMaterialization(utf8Json, limits);
            var document = JsonSerializer.Deserialize<SaveDataDocument>(utf8Json, JsonOptions)
                ?? throw new InvalidDataException("Save Data document is empty.");
            return RestoreDocument(document, limits);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Save Data is malformed or contains invalid values.", exception);
        }
    }

    public static SimulationWorld Load(Stream source)
    {
        return Load(source, WorldSaveLimits.Default);
    }

    public static SimulationWorld Load(Stream source, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);

        if (!source.CanRead)
        {
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        }

        if (source.CanSeek)
        {
            var remainingLength = source.Length - source.Position;
            if (remainingLength > limits.MaximumBytes)
            {
                throw new InvalidDataException(
                    $"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
            }
        }

        using var buffer = new MemoryStream();
        var readBuffer = new byte[Math.Min(StreamReadBufferSize, limits.MaximumBytes)];
        while (true)
        {
            var read = source.Read(readBuffer, 0, readBuffer.Length);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length > limits.MaximumBytes - read)
            {
                throw new InvalidDataException(
                    $"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
            }

            buffer.Write(readBuffer, 0, read);
        }

        return Deserialize(buffer.ToArray(), limits);
    }

    private static BoundedSaveBuffer SerializeToBuffer(
        SimulationWorld world,
        WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(limits);

        var checkpoint = world.CreateCheckpoint();
        ValidateCheckpointWithinLimits(checkpoint, limits);
        var document = CreateDocument(checkpoint);
        var buffer = new BoundedSaveBuffer(limits.MaximumBytes);
        try
        {
            JsonSerializer.Serialize(buffer, document, JsonOptions);
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static void ValidateCheckpointWithinLimits(SimulationCheckpoint checkpoint, WorldSaveLimits limits)
    {
        if (checkpoint.Agents.Count > limits.MaximumAgentCount)
        {
            throw new InvalidDataException(
                $"World contains {checkpoint.Agents.Count} Agents, exceeding the configured {limits.MaximumAgentCount}-Agent Save limit.");
        }

        if (checkpoint.Buildings.Count > limits.MaximumBuildingCount)
        {
            throw new InvalidDataException(
                $"World contains {checkpoint.Buildings.Count} Buildings, exceeding the configured {limits.MaximumBuildingCount}-Building Save limit.");
        }

        if (checkpoint.Pois.Count > limits.MaximumPoiCount)
        {
            throw new InvalidDataException(
                $"World contains {checkpoint.Pois.Count} POIs, exceeding the configured {limits.MaximumPoiCount}-POI Save limit.");
        }
    }

    private static SaveDataDocument CreateDocument(SimulationCheckpoint checkpoint)
    {
        var agents = new SaveAgentData[checkpoint.Agents.Count];
        for (var index = 0; index < agents.Length; index++)
        {
            var agent = checkpoint.Agents[index];
            agents[index] = new SaveAgentData
            {
                Id = agent.Id.Value,
                X = agent.Position.X,
                Y = agent.Position.Y,
                Z = agent.Position.Z,
                VelocityX = agent.Velocity.X,
                VelocityY = agent.Velocity.Y,
                VelocityZ = agent.Velocity.Z,
                IsActive = agent.IsActive,
            };
        }

        var buildings = new SaveBuildingData[checkpoint.Buildings.Count];
        for (var index = 0; index < buildings.Length; index++)
        {
            var building = checkpoint.Buildings[index];
            buildings[index] = new SaveBuildingData
            {
                Id = building.Id.Value,
                Kind = (byte)building.Kind,
                MinX = building.Bounds.MinX,
                MinY = building.Bounds.MinY,
                MinZ = building.Bounds.MinZ,
                MaxX = building.Bounds.MaxX,
                MaxY = building.Bounds.MaxY,
                MaxZ = building.Bounds.MaxZ,
            };
        }

        var pois = new SavePoiData[checkpoint.Pois.Count];
        for (var index = 0; index < pois.Length; index++)
        {
            var poi = checkpoint.Pois[index];
            pois[index] = new SavePoiData
            {
                Id = poi.Id.Value,
                Kind = (byte)poi.Kind,
                X = poi.Position.X,
                Y = poi.Position.Y,
                Z = poi.Position.Z,
                BuildingId = poi.BuildingId?.Value,
            };
        }

        return new SaveDataDocument
        {
            FormatVersion = SaveFormatVersion.Current,
            Simulation = new SaveSimulationData
            {
                TickRate = checkpoint.TickRate,
                Seed = checkpoint.Seed,
                SpatialCellSize = checkpoint.SpatialCellSize,
                TickCount = checkpoint.TickCount,
                ElapsedTicks = checkpoint.ElapsedTicks,
                RandomState = checkpoint.RandomState,
                NextAgentId = checkpoint.NextAgentId,
                Agents = agents,
                NextBuildingId = checkpoint.NextBuildingId,
                Buildings = buildings,
                NextPoiId = checkpoint.NextPoiId,
                Pois = pois,
            },
        };
    }

    private static SimulationWorld RestoreDocument(SaveDataDocument document, WorldSaveLimits limits)
    {
        var formatVersion = Require(document.FormatVersion, "formatVersion");
        if (formatVersion != SaveFormatVersion.Current)
        {
            throw new InvalidDataException(
                $"Unsupported Save format version {formatVersion}. Expected {SaveFormatVersion.Current}.");
        }

        var simulation = document.Simulation
            ?? throw new InvalidDataException("Save Data is missing simulation state.");
        var savedAgents = simulation.Agents
            ?? throw new InvalidDataException("Save Data is missing Agent state.");
        var savedBuildings = simulation.Buildings
            ?? throw new InvalidDataException("Save Data is missing Building state.");
        var savedPois = simulation.Pois
            ?? throw new InvalidDataException("Save Data is missing POI state.");

        ValidateMaterializedCounts(savedAgents.Length, savedBuildings.Length, savedPois.Length, limits);

        var agents = new SimulationAgentCheckpoint[savedAgents.Length];
        for (var index = 0; index < savedAgents.Length; index++)
        {
            var savedAgent = savedAgents[index]
                ?? throw new InvalidDataException($"Agent entry {index} is null.");
            agents[index] = new SimulationAgentCheckpoint(
                new AgentId(Require(savedAgent.Id, $"agents[{index}].id")),
                new WorldPoint(
                    Require(savedAgent.X, $"agents[{index}].x"),
                    Require(savedAgent.Y, $"agents[{index}].y"),
                    Require(savedAgent.Z, $"agents[{index}].z")),
                new WorldVector(
                    Require(savedAgent.VelocityX, $"agents[{index}].velocityX"),
                    Require(savedAgent.VelocityY, $"agents[{index}].velocityY"),
                    Require(savedAgent.VelocityZ, $"agents[{index}].velocityZ")),
                Require(savedAgent.IsActive, $"agents[{index}].isActive"));
        }

        var buildings = new SimulationBuildingCheckpoint[savedBuildings.Length];
        for (var index = 0; index < savedBuildings.Length; index++)
        {
            var savedBuilding = savedBuildings[index]
                ?? throw new InvalidDataException($"Building entry {index} is null.");
            buildings[index] = new SimulationBuildingCheckpoint(
                new BuildingId(Require(savedBuilding.Id, $"buildings[{index}].id")),
                (BuildingKind)Require(savedBuilding.Kind, $"buildings[{index}].kind"),
                new WorldVolume(
                    Require(savedBuilding.MinX, $"buildings[{index}].minX"),
                    Require(savedBuilding.MinY, $"buildings[{index}].minY"),
                    Require(savedBuilding.MinZ, $"buildings[{index}].minZ"),
                    Require(savedBuilding.MaxX, $"buildings[{index}].maxX"),
                    Require(savedBuilding.MaxY, $"buildings[{index}].maxY"),
                    Require(savedBuilding.MaxZ, $"buildings[{index}].maxZ")));
        }

        var pois = new SimulationPoiCheckpoint[savedPois.Length];
        for (var index = 0; index < savedPois.Length; index++)
        {
            var savedPoi = savedPois[index]
                ?? throw new InvalidDataException($"POI entry {index} is null.");
            var buildingId = savedPoi.BuildingId is { } savedBuildingId
                ? new BuildingId(savedBuildingId)
                : (BuildingId?)null;
            pois[index] = new SimulationPoiCheckpoint(
                new PoiId(Require(savedPoi.Id, $"pois[{index}].id")),
                (PoiKind)Require(savedPoi.Kind, $"pois[{index}].kind"),
                new WorldPoint(
                    Require(savedPoi.X, $"pois[{index}].x"),
                    Require(savedPoi.Y, $"pois[{index}].y"),
                    Require(savedPoi.Z, $"pois[{index}].z")),
                buildingId);
        }

        var checkpoint = new SimulationCheckpoint(
            Require(simulation.TickRate, "simulation.tickRate"),
            Require(simulation.Seed, "simulation.seed"),
            Require(simulation.SpatialCellSize, "simulation.spatialCellSize"),
            Require(simulation.TickCount, "simulation.tickCount"),
            Require(simulation.ElapsedTicks, "simulation.elapsedTicks"),
            Require(simulation.RandomState, "simulation.randomState"),
            Require(simulation.NextAgentId, "simulation.nextAgentId"),
            agents,
            Require(simulation.NextBuildingId, "simulation.nextBuildingId"),
            buildings,
            Require(simulation.NextPoiId, "simulation.nextPoiId"),
            pois);

        return SimulationWorld.RestoreCheckpoint(checkpoint);
    }

    private static void ValidateMaterializedCounts(
        int agentCount,
        int buildingCount,
        int poiCount,
        WorldSaveLimits limits)
    {
        if (agentCount > limits.MaximumAgentCount)
        {
            throw new InvalidDataException(
                $"Save Data contains {agentCount} Agents, exceeding the configured {limits.MaximumAgentCount}-Agent limit.");
        }

        if (buildingCount > limits.MaximumBuildingCount)
        {
            throw new InvalidDataException(
                $"Save Data contains {buildingCount} Buildings, exceeding the configured {limits.MaximumBuildingCount}-Building limit.");
        }

        if (poiCount > limits.MaximumPoiCount)
        {
            throw new InvalidDataException(
                $"Save Data contains {poiCount} POIs, exceeding the configured {limits.MaximumPoiCount}-POI limit.");
        }
    }

    private static void ValidateCollectionCountsBeforeMaterialization(
        ReadOnlySpan<byte> utf8Json,
        WorldSaveLimits limits)
    {
        var reader = new Utf8JsonReader(
            utf8Json,
            new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = JsonOptions.MaxDepth,
            });

        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            if (reader.ValueTextEquals("agents"))
            {
                ValidateNamedArrayElementCount(ref reader, limits.MaximumAgentCount, "Agent");
            }
            else if (reader.ValueTextEquals("buildings"))
            {
                ValidateNamedArrayElementCount(ref reader, limits.MaximumBuildingCount, "Building");
            }
            else if (reader.ValueTextEquals("pois"))
            {
                ValidateNamedArrayElementCount(ref reader, limits.MaximumPoiCount, "POI");
            }
        }
    }

    private static void ValidateNamedArrayElementCount(
        ref Utf8JsonReader reader,
        int maximumCount,
        string entityName)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
        {
            return;
        }

        var arrayDepth = reader.CurrentDepth;
        var elementCount = 0;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray && reader.CurrentDepth == arrayDepth)
            {
                return;
            }

            if (reader.CurrentDepth != arrayDepth + 1 ||
                reader.TokenType is not (
                    JsonTokenType.StartObject or
                    JsonTokenType.StartArray or
                    JsonTokenType.String or
                    JsonTokenType.Number or
                    JsonTokenType.True or
                    JsonTokenType.False or
                    JsonTokenType.Null))
            {
                continue;
            }

            elementCount++;
            if (elementCount > maximumCount)
            {
                throw new InvalidDataException(
                    $"Save Data {entityName} count exceeds the configured {maximumCount}-{entityName} limit before deserialization.");
            }
        }
    }

    private static T Require<T>(T? value, string fieldName)
        where T : struct
    {
        return value ?? throw new InvalidDataException($"Save Data is missing required field '{fieldName}'.");
    }

    private sealed class BoundedSaveBuffer : Stream
    {
        private readonly int maximumBytes;
        private readonly List<byte[]> segments = [];
        private byte[]? currentSegment;
        private int currentSegmentOffset;
        private int length;

        public BoundedSaveBuffer(int maximumBytes)
        {
            this.maximumBytes = maximumBytes;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => length;

        public override long Position
        {
            get => length;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > maximumBytes - length)
            {
                throw new InvalidDataException(
                    $"Save Data output exceeds the configured {maximumBytes}-byte limit.");
            }

            while (!buffer.IsEmpty)
            {
                if (currentSegment is null || currentSegmentOffset == currentSegment.Length)
                {
                    var segmentLength = Math.Min(StreamReadBufferSize, maximumBytes - length);
                    currentSegment = new byte[segmentLength];
                    currentSegmentOffset = 0;
                    segments.Add(currentSegment);
                }

                var copyLength = Math.Min(buffer.Length, currentSegment.Length - currentSegmentOffset);
                buffer[..copyLength].CopyTo(currentSegment.AsSpan(currentSegmentOffset, copyLength));
                currentSegmentOffset += copyLength;
                length += copyLength;
                buffer = buffer[copyLength..];
            }
        }

        public byte[] ToArray()
        {
            var result = new byte[length];
            CopyTo(result);
            return result;
        }

        public void WriteTo(Stream destination)
        {
            ArgumentNullException.ThrowIfNull(destination);
            var remaining = length;
            foreach (var segment in segments)
            {
                var count = Math.Min(segment.Length, remaining);
                destination.Write(segment.AsSpan(0, count));
                remaining -= count;
                if (remaining == 0)
                {
                    break;
                }
            }
        }

        private void CopyTo(Span<byte> destination)
        {
            var offset = 0;
            var remaining = length;
            foreach (var segment in segments)
            {
                var count = Math.Min(segment.Length, remaining);
                segment.AsSpan(0, count).CopyTo(destination[offset..]);
                offset += count;
                remaining -= count;
                if (remaining == 0)
                {
                    break;
                }
            }
        }
    }
}
