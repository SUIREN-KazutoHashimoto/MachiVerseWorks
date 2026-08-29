using System.Text.Json;
using System.Text.Json.Serialization;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static class WorldSaveSerializer
{
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
        try
        {
            var document = JsonSerializer.Deserialize<SaveDataDocument>(utf8Json, JsonOptions)
                ?? throw new InvalidDataException("Save Data document is empty.");
            return RestoreDocument(document);
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
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException("Source stream must be readable.", nameof(source));
        }

        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return Deserialize(buffer.ToArray());
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

    private static SimulationWorld RestoreDocument(SaveDataDocument document)
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

    private static T Require<T>(T? value, string fieldName)
        where T : struct
    {
        return value ?? throw new InvalidDataException($"Save Data is missing required field '{fieldName}'.");
    }
}
