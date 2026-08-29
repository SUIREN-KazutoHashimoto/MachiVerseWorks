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
        ArgumentNullException.ThrowIfNull(world);
        var document = CreateDocument(world.CreateCheckpoint());
        return JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
    }

    public static void Save(Stream destination, SimulationWorld world)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(world);

        if (!destination.CanWrite)
        {
            throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        }

        var data = Serialize(world);
        destination.Write(data);
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
            ValidateAgentCountBeforeMaterialization(utf8Json, limits.MaximumAgentCount);
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
                VelocityX = agent.Velocity.X,
                VelocityY = agent.Velocity.Y,
                IsActive = agent.IsActive,
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
        if (savedAgents.Length > limits.MaximumAgentCount)
        {
            throw new InvalidDataException(
                $"Save Data contains {savedAgents.Length} Agents, exceeding the configured {limits.MaximumAgentCount}-Agent limit.");
        }

        var agents = new SimulationAgentCheckpoint[savedAgents.Length];

        for (var index = 0; index < savedAgents.Length; index++)
        {
            var savedAgent = savedAgents[index]
                ?? throw new InvalidDataException($"Agent entry {index} is null.");
            agents[index] = new SimulationAgentCheckpoint(
                new AgentId(Require(savedAgent.Id, $"agents[{index}].id")),
                new WorldPoint(
                    Require(savedAgent.X, $"agents[{index}].x"),
                    Require(savedAgent.Y, $"agents[{index}].y")),
                new WorldVector(
                    Require(savedAgent.VelocityX, $"agents[{index}].velocityX"),
                    Require(savedAgent.VelocityY, $"agents[{index}].velocityY")),
                Require(savedAgent.IsActive, $"agents[{index}].isActive"));
        }

        var checkpoint = new SimulationCheckpoint(
            Require(simulation.TickRate, "simulation.tickRate"),
            Require(simulation.Seed, "simulation.seed"),
            Require(simulation.SpatialCellSize, "simulation.spatialCellSize"),
            Require(simulation.TickCount, "simulation.tickCount"),
            Require(simulation.ElapsedTicks, "simulation.elapsedTicks"),
            Require(simulation.RandomState, "simulation.randomState"),
            Require(simulation.NextAgentId, "simulation.nextAgentId"),
            agents);

        return SimulationWorld.RestoreCheckpoint(checkpoint);
    }

    private static void ValidateAgentCountBeforeMaterialization(
        ReadOnlySpan<byte> utf8Json,
        int maximumAgentCount)
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
            if (reader.TokenType != JsonTokenType.PropertyName ||
                !reader.ValueTextEquals("agents"))
            {
                continue;
            }

            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
            {
                continue;
            }

            ValidateArrayElementCount(ref reader, maximumAgentCount);
        }
    }

    private static void ValidateArrayElementCount(
        ref Utf8JsonReader reader,
        int maximumAgentCount)
    {
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
            if (elementCount > maximumAgentCount)
            {
                throw new InvalidDataException(
                    $"Save Data Agent count exceeds the configured {maximumAgentCount}-Agent limit before deserialization.");
            }
        }
    }

    private static T Require<T>(T? value, string fieldName)
        where T : struct
    {
        return value ?? throw new InvalidDataException($"Save Data is missing required field '{fieldName}'.");
    }
}
