using System.Text.Json;
using System.Text.Json.Serialization;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Persistence;

public static partial class WorldSaveSerializer
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
        using var buffer = SerializeToBuffer(world, limits);
        return buffer.ToArray();
    }

    public static void Save(Stream destination, SimulationWorld world) => Save(destination, world, WorldSaveLimits.Default);

    public static void Save(Stream destination, SimulationWorld world, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(limits);
        if (!destination.CanWrite) throw new ArgumentException("Destination stream must be writable.", nameof(destination));
        using var buffer = SerializeToBuffer(world, limits);
        buffer.WriteTo(destination);
    }

    public static SimulationWorld Deserialize(ReadOnlySpan<byte> utf8Json) => Deserialize(utf8Json, WorldSaveLimits.Default);

    public static SimulationWorld Deserialize(ReadOnlySpan<byte> utf8Json, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (utf8Json.Length > limits.MaximumBytes) throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
        try
        {
            ValidateCollectionCountsBeforeMaterialization(utf8Json, limits);
            ValidateNestedCollectionCountsBeforeMaterialization(utf8Json, limits);
            var document = JsonSerializer.Deserialize<SaveDataDocument>(utf8Json, JsonOptions) ?? throw new InvalidDataException("Save Data document is empty.");
            return RestoreDocument(document, limits);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Save Data is malformed or contains invalid values.", exception);
        }
    }

    public static SimulationWorld Load(Stream source) => Load(source, WorldSaveLimits.Default);

    public static SimulationWorld Load(Stream source, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(limits);
        if (!source.CanRead) throw new ArgumentException("Source stream must be readable.", nameof(source));
        if (source.CanSeek && source.Length - source.Position > limits.MaximumBytes) throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
        using var buffer = new MemoryStream();
        var readBuffer = new byte[Math.Min(StreamReadBufferSize, limits.MaximumBytes)];
        while (true)
        {
            var read = source.Read(readBuffer, 0, readBuffer.Length);
            if (read == 0) break;
            if (buffer.Length > limits.MaximumBytes - read) throw new InvalidDataException($"Save Data exceeds the configured {limits.MaximumBytes}-byte input limit.");
            buffer.Write(readBuffer, 0, read);
        }
        return Deserialize(buffer.ToArray(), limits);
    }

    private static BoundedSaveBuffer SerializeToBuffer(SimulationWorld world, WorldSaveLimits limits)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(limits);
        var checkpoint = world.CreateCheckpoint();
        ValidateCheckpointWithinLimits(checkpoint, limits);
        var buffer = new BoundedSaveBuffer(limits.MaximumBytes);
        try
        {
            JsonSerializer.Serialize(buffer, CreateDocument(checkpoint), JsonOptions);
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
        ValidateCount(checkpoint.Agents.Count, limits.MaximumAgentCount, "Agents");
        ValidateCount(checkpoint.Buildings.Count, limits.MaximumBuildingCount, "Buildings");
        ValidateCount(checkpoint.Pois.Count, limits.MaximumPoiCount, "POIs");
        ValidateCount(checkpoint.RoadNodes.Count, limits.MaximumRoadNodeCount, "RoadNodes");
        ValidateCount(checkpoint.RoadSegments.Count, limits.MaximumRoadSegmentCount, "RoadSegments");
        ValidateCount(checkpoint.Lanes.Count, limits.MaximumLaneCount, "Lanes");
        ValidateCount(checkpoint.LaneConnections.Count, limits.MaximumLaneConnectionCount, "LaneConnections");
        ValidateCount(checkpoint.RoadAccessPoints.Count, limits.MaximumRoadAccessPointCount, "RoadAccessPoints");
        ValidateCount(checkpoint.Pedestrians?.Count ?? 0, limits.MaximumPedestrianCount, "Pedestrians");
        ValidateCount(checkpoint.PedestrianCrossings?.Count ?? 0, limits.MaximumPedestrianCrossingCount, "PedestrianCrossings");
        ValidateCount(checkpoint.Vehicles?.Count ?? 0, limits.MaximumVehicleCount, "Vehicles");
        ValidateCount(checkpoint.Households?.Count ?? 0, limits.MaximumHouseholdCount, "Households");
        ValidateCount(checkpoint.Persons?.Count ?? 0, limits.MaximumPersonCount, "Persons");
        ValidateCount(checkpoint.TrackNodes?.Count ?? 0, limits.MaximumRoadNodeCount, "TrackNodes");
        ValidateCount(checkpoint.TrackSegments?.Count ?? 0, limits.MaximumRoadSegmentCount, "TrackSegments");
        ValidateCount(checkpoint.TrackConnections?.Count ?? 0, limits.MaximumLaneConnectionCount, "TrackConnections");
        ValidateCount(checkpoint.BlockSections?.Count ?? 0, limits.MaximumRoadSegmentCount, "BlockSections");
        ValidateCount(checkpoint.Stations?.Count ?? 0, limits.MaximumBuildingCount, "Stations");
        ValidateCount(checkpoint.Platforms?.Count ?? 0, limits.MaximumRoadAccessPointCount, "Platforms");
        ValidateCount(checkpoint.PlatformAccessPoints?.Count ?? 0, limits.MaximumRoadAccessPointCount, "PlatformAccessPoints");
        ValidateCount(checkpoint.Depots?.Count ?? 0, limits.MaximumBuildingCount, "Depots");
        ValidateNestedCheckpointWithinLimits(checkpoint, limits);
        ValidateRailwayOperationsCheckpointWithinLimits(checkpoint, limits);
        ValidateMultimodalTransitCheckpointWithinLimits(checkpoint.MultimodalTransit, limits);
        ValidateEconomyCheckpointWithinLimits(checkpoint.Economy, limits);
    }

    private static SaveDataDocument CreateDocument(SimulationCheckpoint checkpoint)
    {
        var agents = checkpoint.Agents.Select(static item => new SaveAgentData
        {
            Id = item.Id.Value,
            X = item.Position.X,
            Y = item.Position.Y,
            Z = item.Position.Z,
            VelocityX = item.Velocity.X,
            VelocityY = item.Velocity.Y,
            VelocityZ = item.Velocity.Z,
            IsActive = item.IsActive,
        }).ToArray();
        var buildings = checkpoint.Buildings.Select(static item => new SaveBuildingData
        {
            Id = item.Id.Value,
            Kind = (byte)item.Kind,
            MinX = item.Bounds.MinX,
            MinY = item.Bounds.MinY,
            MinZ = item.Bounds.MinZ,
            MaxX = item.Bounds.MaxX,
            MaxY = item.Bounds.MaxY,
            MaxZ = item.Bounds.MaxZ,
        }).ToArray();
        var pois = checkpoint.Pois.Select(static item => new SavePoiData
        {
            Id = item.Id.Value,
            Kind = (byte)item.Kind,
            X = item.Position.X,
            Y = item.Position.Y,
            Z = item.Position.Z,
            BuildingId = item.BuildingId?.Value,
        }).ToArray();
        var roadNodes = checkpoint.RoadNodes.Select(static item => new SaveRoadNodeData
        {
            Id = item.Id.Value,
            Kind = (byte)item.Kind,
            X = item.Position.X,
            Y = item.Position.Y,
            Z = item.Position.Z,
        }).ToArray();
        var roadSegments = checkpoint.RoadSegments.Select(static item => new SaveRoadSegmentData
        {
            Id = item.Id.Value,
            Kind = (byte)item.Kind,
            StartNodeId = item.StartNodeId.Value,
            EndNodeId = item.EndNodeId.Value,
        }).ToArray();
        var lanes = checkpoint.Lanes.Select(static item => new SaveLaneData
        {
            Id = item.Id.Value,
            SegmentId = item.SegmentId.Value,
            Direction = (byte)item.Direction,
            Order = item.Order,
            WidthMeters = item.WidthMeters,
            SpeedLimitMetersPerSecond = item.SpeedLimitMetersPerSecond,
        }).ToArray();
        var connections = checkpoint.LaneConnections.Select(static item => new SaveLaneConnectionData
        {
            Id = item.Id.Value,
            FromLaneId = item.FromLaneId.Value,
            ToLaneId = item.ToLaneId.Value,
            ViaNodeId = item.ViaNodeId.Value,
            Movement = (byte)item.Movement,
        }).ToArray();
        var accessPoints = checkpoint.RoadAccessPoints.Select(static item => new SaveRoadAccessPointData
        {
            Id = item.Id.Value,
            SegmentId = item.SegmentId.Value,
            SegmentOffset = item.SegmentOffset,
            BuildingId = item.BuildingId?.Value,
            PoiId = item.PoiId?.Value,
            Mode = (byte)item.Mode,
        }).ToArray();
        var pedestrians = (checkpoint.Pedestrians ?? []).Select(static item => new SavePedestrianData
        {
            Id = item.Id.Value,
            TripRequestId = item.TripRequestId.Value,
            OriginBuildingId = item.Origin.BuildingId?.Value,
            OriginPoiId = item.Origin.PoiId?.Value,
            DestinationBuildingId = item.Destination.BuildingId?.Value,
            DestinationPoiId = item.Destination.PoiId?.Value,
            Mode = (byte)item.Mode,
            WalkingSpeedMetersPerSecond = item.WalkingSpeedMetersPerSecond,
            LegIndex = item.LegIndex,
            ProgressMeters = item.ProgressMeters,
            State = (byte)item.State,
        }).ToArray();
        var pedestrianCrossings = (checkpoint.PedestrianCrossings ?? []).Select(static item => new SavePedestrianCrossingData
        {
            Id = item.Id.Value,
            IsOpen = item.IsOpen,
        }).ToArray();
        var vehicles = (checkpoint.Vehicles ?? []).Select(static item => new SaveVehicleData
        {
            Id = item.Id.Value,
            LengthMeters = item.Dimensions.LengthMeters,
            WidthMeters = item.Dimensions.WidthMeters,
            HeightMeters = item.Dimensions.HeightMeters,
            MaximumSpeedMetersPerSecond = item.Performance.MaximumSpeedMetersPerSecond,
            MaximumAccelerationMetersPerSecondSquared = item.Performance.MaximumAccelerationMetersPerSecondSquared,
            ComfortableDecelerationMetersPerSecondSquared = item.Performance.ComfortableDecelerationMetersPerSecondSquared,
            MinimumGapMeters = item.Performance.MinimumGapMeters,
            TimeHeadwaySeconds = item.Performance.TimeHeadwaySeconds,
            RouteSteps = item.RouteSteps.Select(static step => new SaveVehicleRouteStepData
            {
                LaneId = step.LaneId.Value,
                SegmentId = step.SegmentId.Value,
                StartSegmentOffset = step.StartSegmentOffset,
                EndSegmentOffset = step.EndSegmentOffset,
                DistanceMeters = step.DistanceMeters,
                EstimatedTravelTimeSeconds = step.EstimatedTravelTimeSeconds,
                ExitConnectionId = step.ExitConnectionId?.Value,
            }).ToArray(),
            RouteStepIndex = item.RouteStepIndex,
            RouteProgressMeters = item.RouteProgressMeters,
            SpeedMetersPerSecond = item.SpeedMetersPerSecond,
            State = (byte)item.State,
        }).ToArray();
        var households = (checkpoint.Households ?? []).Select(static item => new SaveHouseholdData
        {
            Id = item.Id.Value,
            ResidenceBuildingId = item.Residence.BuildingId?.Value,
            ResidencePoiId = item.Residence.PoiId?.Value,
        }).ToArray();
        var persons = (checkpoint.Persons ?? []).Select(static item => new SavePersonData
        {
            Id = item.Id.Value,
            HouseholdId = item.HouseholdId.Value,
            AgeYears = item.Demographics.AgeYears,
            IsEmployed = item.Demographics.IsEmployed,
            IsStudent = item.Demographics.IsStudent,
            HasPrivateVehicle = item.Demographics.HasPrivateVehicle,
            ResidenceBuildingId = item.Residence.BuildingId?.Value,
            ResidencePoiId = item.Residence.PoiId?.Value,
            CurrentBuildingId = item.CurrentLocation.BuildingId?.Value,
            CurrentPoiId = item.CurrentLocation.PoiId?.Value,
            CurrentActivity = (byte)item.CurrentActivity,
            TravelState = (byte)item.TravelState,
            DestinationBuildingId = item.Destination?.BuildingId?.Value,
            DestinationPoiId = item.Destination?.PoiId?.Value,
            DestinationActivity = item.DestinationActivity is { } destinationActivity ? (byte)destinationActivity : null,
            ActiveTripRequestId = item.ActiveTripRequestId?.Value,
            ActiveTravelMode = item.ActiveTravelMode is { } activeMode ? (byte)activeMode : null,
            PedestrianId = item.PedestrianId?.Value,
            VehicleId = item.VehicleId?.Value,
            Schedule = item.Schedule.Select(static window => new SaveDailyActivityWindowData
            {
                Activity = (byte)window.Activity,
                StartMinuteOfDay = window.StartMinuteOfDay,
                EndMinuteOfDay = window.EndMinuteOfDay,
                DestinationBuildingId = window.Destination?.BuildingId?.Value,
                DestinationPoiId = window.Destination?.PoiId?.Value,
                Priority = (byte)window.Priority,
            }).ToArray(),
            Needs = item.Needs.Select(static need => new SavePersonNeedData
            {
                Kind = (byte)need.Kind,
                Satisfaction = need.Satisfaction,
                DecayPerHour = need.DecayPerHour,
            }).ToArray(),
        }).ToArray();
        var trackNodes = (checkpoint.TrackNodes ?? []).Select(static item => new SaveTrackNodeData
        {
            Id = item.Id.Value,
            Kind = (byte)item.Kind,
            X = item.Position.X,
            Y = item.Position.Y,
            Z = item.Position.Z,
        }).ToArray();
        var trackSegments = (checkpoint.TrackSegments ?? []).Select(static item => new SaveTrackSegmentData
        {
            Id = item.Id.Value,
            StartNodeId = item.StartNodeId.Value,
            EndNodeId = item.EndNodeId.Value,
            Direction = (byte)item.Direction,
            GaugeMeters = item.GaugeMeters,
            SpeedLimitMetersPerSecond = item.SpeedLimitMetersPerSecond,
            Electrification = (byte)item.Electrification,
            Usage = (byte)item.Usage,
        }).ToArray();
        var trackConnections = (checkpoint.TrackConnections ?? []).Select(static item => new SaveTrackConnectionData
        {
            Id = item.Id.Value,
            FromSegmentId = item.FromSegmentId.Value,
            ToSegmentId = item.ToSegmentId.Value,
            ViaNodeId = item.ViaNodeId.Value,
        }).ToArray();
        var blockSections = (checkpoint.BlockSections ?? []).Select(static item => new SaveBlockSectionData
        {
            Id = item.Id.Value,
            SegmentIds = item.SegmentIds.Select(static id => (ulong?)id.Value).ToArray(),
        }).ToArray();
        var stations = (checkpoint.Stations ?? []).Select(static item => new SaveStationData
        {
            Id = item.Id.Value,
            MinX = item.Bounds.MinX, MinY = item.Bounds.MinY, MinZ = item.Bounds.MinZ,
            MaxX = item.Bounds.MaxX, MaxY = item.Bounds.MaxY, MaxZ = item.Bounds.MaxZ,
        }).ToArray();
        var platforms = (checkpoint.Platforms ?? []).Select(static item => new SavePlatformData
        {
            Id = item.Id.Value,
            StationId = item.StationId.Value,
            TrackSegmentId = item.TrackSegmentId.Value,
            StartSegmentOffset = item.StartSegmentOffset,
            EndSegmentOffset = item.EndSegmentOffset,
            MinX = item.Bounds.MinX, MinY = item.Bounds.MinY, MinZ = item.Bounds.MinZ,
            MaxX = item.Bounds.MaxX, MaxY = item.Bounds.MaxY, MaxZ = item.Bounds.MaxZ,
        }).ToArray();
        var platformAccessPoints = (checkpoint.PlatformAccessPoints ?? []).Select(static item => new SavePlatformAccessPointData
        {
            Id = item.Id.Value,
            PlatformId = item.PlatformId.Value,
            RoadAccessPointId = item.RoadAccessPointId.Value,
        }).ToArray();
        var depots = (checkpoint.Depots ?? []).Select(static item => new SaveDepotData
        {
            Id = item.Id.Value,
            MinX = item.Bounds.MinX, MinY = item.Bounds.MinY, MinZ = item.Bounds.MinZ,
            MaxX = item.Bounds.MaxX, MaxY = item.Bounds.MaxY, MaxZ = item.Bounds.MaxZ,
            TrackSegmentIds = item.TrackSegmentIds.Select(static id => (ulong?)id.Value).ToArray(),
        }).ToArray();

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
                NextRoadNodeId = checkpoint.NextRoadNodeId,
                RoadNodes = roadNodes,
                NextRoadSegmentId = checkpoint.NextRoadSegmentId,
                RoadSegments = roadSegments,
                NextLaneId = checkpoint.NextLaneId,
                Lanes = lanes,
                NextLaneConnectionId = checkpoint.NextLaneConnectionId,
                LaneConnections = connections,
                NextRoadAccessPointId = checkpoint.NextRoadAccessPointId,
                RoadAccessPoints = accessPoints,
                NextPedestrianId = checkpoint.NextPedestrianId,
                Pedestrians = pedestrians,
                PedestrianCrossings = pedestrianCrossings,
                NextVehicleId = checkpoint.NextVehicleId,
                Vehicles = vehicles,
                NextHouseholdId = checkpoint.NextHouseholdId,
                Households = households,
                NextPersonId = checkpoint.NextPersonId,
                Persons = persons,
                NextTripRequestId = checkpoint.NextTripRequestId,
                NextTrackNodeId = checkpoint.NextTrackNodeId,
                TrackNodes = trackNodes,
                NextTrackSegmentId = checkpoint.NextTrackSegmentId,
                TrackSegments = trackSegments,
                NextTrackConnectionId = checkpoint.NextTrackConnectionId,
                TrackConnections = trackConnections,
                NextBlockSectionId = checkpoint.NextBlockSectionId,
                BlockSections = blockSections,
                NextStationId = checkpoint.NextStationId,
                Stations = stations,
                NextPlatformId = checkpoint.NextPlatformId,
                Platforms = platforms,
                NextPlatformAccessPointId = checkpoint.NextPlatformAccessPointId,
                PlatformAccessPoints = platformAccessPoints,
                NextDepotId = checkpoint.NextDepotId,
                Depots = depots,
                RailwayOperations = CreateRailwayOperationsData(checkpoint),
                MultimodalTransit = checkpoint.MultimodalTransit,
                Economy = checkpoint.Economy,
            },
        };
    }

    private static SimulationWorld RestoreDocument(SaveDataDocument document, WorldSaveLimits limits)
    {
        var format = Require(document.FormatVersion, "formatVersion");
        if (format is not (SaveFormatVersion.BuildingPoi or SaveFormatVersion.RoadNetwork or SaveFormatVersion.Pedestrian or SaveFormatVersion.Vehicle or SaveFormatVersion.Population or SaveFormatVersion.RailwayInfrastructure or SaveFormatVersion.RailwayOperations or SaveFormatVersion.MultimodalTransit or SaveFormatVersion.Economy))
        {
            throw new InvalidDataException($"Unsupported Save format version {format}. Expected {SaveFormatVersion.Current} or a supported migratable version.");
        }

        var simulation = document.Simulation ?? throw new InvalidDataException("Save Data is missing simulation state.");
        var savedAgents = simulation.Agents ?? throw new InvalidDataException("Save Data is missing Agent state.");
        var savedBuildings = simulation.Buildings ?? throw new InvalidDataException("Save Data is missing Building state.");
        var savedPois = simulation.Pois ?? throw new InvalidDataException("Save Data is missing POI state.");
        var hasRoadNetwork = format >= SaveFormatVersion.RoadNetwork;
        var hasPedestrians = format >= SaveFormatVersion.Pedestrian;
        var hasVehicles = format >= SaveFormatVersion.Vehicle;
        var hasPopulation = format >= SaveFormatVersion.Population;
        var hasRailway = format >= SaveFormatVersion.RailwayInfrastructure;
        var hasRailwayOperations = format >= SaveFormatVersion.RailwayOperations;
        var hasMultimodalTransit = format >= SaveFormatVersion.MultimodalTransit;
        var hasEconomy = format >= SaveFormatVersion.Economy;
        var roadNodesData = hasRoadNetwork ? simulation.RoadNodes ?? throw new InvalidDataException("Save Data is missing RoadNode state.") : [];
        var roadSegmentsData = hasRoadNetwork ? simulation.RoadSegments ?? throw new InvalidDataException("Save Data is missing RoadSegment state.") : [];
        var lanesData = hasRoadNetwork ? simulation.Lanes ?? throw new InvalidDataException("Save Data is missing Lane state.") : [];
        var connectionsData = hasRoadNetwork ? simulation.LaneConnections ?? throw new InvalidDataException("Save Data is missing LaneConnection state.") : [];
        var accessData = hasRoadNetwork ? simulation.RoadAccessPoints ?? throw new InvalidDataException("Save Data is missing RoadAccessPoint state.") : [];
        var pedestrianData = hasPedestrians ? simulation.Pedestrians ?? throw new InvalidDataException("Save Data is missing Pedestrian state.") : [];
        var pedestrianCrossingData = hasPedestrians ? simulation.PedestrianCrossings ?? [] : [];
        var vehicleData = hasVehicles ? simulation.Vehicles ?? throw new InvalidDataException("Save Data is missing Vehicle state.") : [];
        var householdData = hasPopulation ? simulation.Households ?? throw new InvalidDataException("Save Data is missing Household state.") : [];
        var personData = hasPopulation ? simulation.Persons ?? throw new InvalidDataException("Save Data is missing Person state.") : [];
        var trackNodeData = hasRailway ? simulation.TrackNodes ?? throw new InvalidDataException("Save Data is missing TrackNode state.") : [];
        var trackSegmentData = hasRailway ? simulation.TrackSegments ?? throw new InvalidDataException("Save Data is missing TrackSegment state.") : [];
        var trackConnectionData = hasRailway ? simulation.TrackConnections ?? throw new InvalidDataException("Save Data is missing TrackConnection state.") : [];
        var blockSectionData = hasRailway ? simulation.BlockSections ?? throw new InvalidDataException("Save Data is missing BlockSection state.") : [];
        var stationData = hasRailway ? simulation.Stations ?? throw new InvalidDataException("Save Data is missing Station state.") : [];
        var platformData = hasRailway ? simulation.Platforms ?? throw new InvalidDataException("Save Data is missing Platform state.") : [];
        var platformAccessData = hasRailway ? simulation.PlatformAccessPoints ?? throw new InvalidDataException("Save Data is missing PlatformAccessPoint state.") : [];
        var depotData = hasRailway ? simulation.Depots ?? throw new InvalidDataException("Save Data is missing Depot state.") : [];
        var railwayOperationsData = hasRailwayOperations ? simulation.RailwayOperations ?? throw new InvalidDataException("Save Data is missing RailwayOperations state.") : null;
        var multimodalTransitData = hasMultimodalTransit ? simulation.MultimodalTransit ?? throw new InvalidDataException("Save Data is missing Multimodal Transit state.") : null;
        var economyData = hasEconomy ? simulation.Economy ?? throw new InvalidDataException("Save Data is missing Economy state.") : null;
        ValidateMaterializedCounts(
            savedAgents.Length, savedBuildings.Length, savedPois.Length,
            roadNodesData.Length, roadSegmentsData.Length, lanesData.Length, connectionsData.Length, accessData.Length,
            pedestrianData.Length, pedestrianCrossingData.Length, vehicleData.Length,
            householdData.Length, personData.Length, limits);
        ValidateCount(trackNodeData.Length, limits.MaximumRoadNodeCount, "TrackNodes");
        ValidateCount(trackSegmentData.Length, limits.MaximumRoadSegmentCount, "TrackSegments");
        ValidateCount(trackConnectionData.Length, limits.MaximumLaneConnectionCount, "TrackConnections");
        ValidateCount(blockSectionData.Length, limits.MaximumRoadSegmentCount, "BlockSections");
        ValidateCount(stationData.Length, limits.MaximumBuildingCount, "Stations");
        ValidateCount(platformData.Length, limits.MaximumRoadAccessPointCount, "Platforms");
        ValidateCount(platformAccessData.Length, limits.MaximumRoadAccessPointCount, "PlatformAccessPoints");
        ValidateCount(depotData.Length, limits.MaximumBuildingCount, "Depots");
        ValidateRailwayOperationsDataCounts(railwayOperationsData, hasRailwayOperations, limits);
        ValidateMultimodalTransitCheckpointWithinLimits(multimodalTransitData, limits);
        ValidateEconomyCheckpointWithinLimits(economyData, limits);

        var agents = new SimulationAgentCheckpoint[savedAgents.Length];
        for (var index = 0; index < agents.Length; index++)
        {
            var item = savedAgents[index] ?? throw new InvalidDataException($"Agent entry {index} is null.");
            agents[index] = new SimulationAgentCheckpoint(
                new AgentId(Require(item.Id, $"agents[{index}].id")),
                new WorldPoint(Require(item.X, $"agents[{index}].x"), Require(item.Y, $"agents[{index}].y"), Require(item.Z, $"agents[{index}].z")),
                new WorldVector(Require(item.VelocityX, $"agents[{index}].velocityX"), Require(item.VelocityY, $"agents[{index}].velocityY"), Require(item.VelocityZ, $"agents[{index}].velocityZ")),
                Require(item.IsActive, $"agents[{index}].isActive"));
        }

        var buildings = new SimulationBuildingCheckpoint[savedBuildings.Length];
        for (var index = 0; index < buildings.Length; index++)
        {
            var item = savedBuildings[index] ?? throw new InvalidDataException($"Building entry {index} is null.");
            buildings[index] = new SimulationBuildingCheckpoint(
                new BuildingId(Require(item.Id, $"buildings[{index}].id")),
                (BuildingKind)Require(item.Kind, $"buildings[{index}].kind"),
                new WorldVolume(
                    Require(item.MinX, $"buildings[{index}].minX"), Require(item.MinY, $"buildings[{index}].minY"), Require(item.MinZ, $"buildings[{index}].minZ"),
                    Require(item.MaxX, $"buildings[{index}].maxX"), Require(item.MaxY, $"buildings[{index}].maxY"), Require(item.MaxZ, $"buildings[{index}].maxZ")));
        }

        var pois = new SimulationPoiCheckpoint[savedPois.Length];
        for (var index = 0; index < pois.Length; index++)
        {
            var item = savedPois[index] ?? throw new InvalidDataException($"POI entry {index} is null.");
            pois[index] = new SimulationPoiCheckpoint(
                new PoiId(Require(item.Id, $"pois[{index}].id")),
                (PoiKind)Require(item.Kind, $"pois[{index}].kind"),
                new WorldPoint(Require(item.X, $"pois[{index}].x"), Require(item.Y, $"pois[{index}].y"), Require(item.Z, $"pois[{index}].z")),
                item.BuildingId is { } buildingId ? new BuildingId(buildingId) : null);
        }

        var roadNodes = new SimulationRoadNodeCheckpoint[roadNodesData.Length];
        for (var index = 0; index < roadNodes.Length; index++)
        {
            var item = roadNodesData[index] ?? throw new InvalidDataException($"RoadNode entry {index} is null.");
            roadNodes[index] = new SimulationRoadNodeCheckpoint(
                new RoadNodeId(Require(item.Id, $"roadNodes[{index}].id")),
                (RoadNodeKind)Require(item.Kind, $"roadNodes[{index}].kind"),
                new WorldPoint(Require(item.X, $"roadNodes[{index}].x"), Require(item.Y, $"roadNodes[{index}].y"), Require(item.Z, $"roadNodes[{index}].z")));
        }

        var roadSegments = new SimulationRoadSegmentCheckpoint[roadSegmentsData.Length];
        for (var index = 0; index < roadSegments.Length; index++)
        {
            var item = roadSegmentsData[index] ?? throw new InvalidDataException($"RoadSegment entry {index} is null.");
            roadSegments[index] = new SimulationRoadSegmentCheckpoint(
                new RoadSegmentId(Require(item.Id, $"roadSegments[{index}].id")),
                (RoadKind)Require(item.Kind, $"roadSegments[{index}].kind"),
                new RoadNodeId(Require(item.StartNodeId, $"roadSegments[{index}].startNodeId")),
                new RoadNodeId(Require(item.EndNodeId, $"roadSegments[{index}].endNodeId")));
        }

        var lanes = new SimulationLaneCheckpoint[lanesData.Length];
        for (var index = 0; index < lanes.Length; index++)
        {
            var item = lanesData[index] ?? throw new InvalidDataException($"Lane entry {index} is null.");
            lanes[index] = new SimulationLaneCheckpoint(
                new LaneId(Require(item.Id, $"lanes[{index}].id")),
                new RoadSegmentId(Require(item.SegmentId, $"lanes[{index}].segmentId")),
                (LaneDirection)Require(item.Direction, $"lanes[{index}].direction"),
                Require(item.Order, $"lanes[{index}].order"),
                Require(item.WidthMeters, $"lanes[{index}].widthMeters"),
                Require(item.SpeedLimitMetersPerSecond, $"lanes[{index}].speedLimitMetersPerSecond"));
        }

        var connections = new SimulationLaneConnectionCheckpoint[connectionsData.Length];
        for (var index = 0; index < connections.Length; index++)
        {
            var item = connectionsData[index] ?? throw new InvalidDataException($"LaneConnection entry {index} is null.");
            connections[index] = new SimulationLaneConnectionCheckpoint(
                new LaneConnectionId(Require(item.Id, $"laneConnections[{index}].id")),
                new LaneId(Require(item.FromLaneId, $"laneConnections[{index}].fromLaneId")),
                new LaneId(Require(item.ToLaneId, $"laneConnections[{index}].toLaneId")),
                new RoadNodeId(Require(item.ViaNodeId, $"laneConnections[{index}].viaNodeId")),
                (TurnMovement)Require(item.Movement, $"laneConnections[{index}].movement"));
        }

        var accessPoints = new SimulationRoadAccessPointCheckpoint[accessData.Length];
        for (var index = 0; index < accessPoints.Length; index++)
        {
            var item = accessData[index] ?? throw new InvalidDataException($"RoadAccessPoint entry {index} is null.");
            accessPoints[index] = new SimulationRoadAccessPointCheckpoint(
                new RoadAccessPointId(Require(item.Id, $"roadAccessPoints[{index}].id")),
                new RoadSegmentId(Require(item.SegmentId, $"roadAccessPoints[{index}].segmentId")),
                Require(item.SegmentOffset, $"roadAccessPoints[{index}].segmentOffset"),
                item.BuildingId is { } buildingId ? new BuildingId(buildingId) : null,
                item.PoiId is { } poiId ? new PoiId(poiId) : null,
                (RoadAccessMode)Require(item.Mode, $"roadAccessPoints[{index}].mode"));
        }

        var pedestrians = new SimulationPedestrianCheckpoint[pedestrianData.Length];
        for (var index = 0; index < pedestrians.Length; index++)
        {
            var item = pedestrianData[index] ?? throw new InvalidDataException($"Pedestrian entry {index} is null.");
            var origin = RestoreEndpoint(item.OriginBuildingId, item.OriginPoiId, $"pedestrians[{index}].origin");
            var destination = RestoreEndpoint(item.DestinationBuildingId, item.DestinationPoiId, $"pedestrians[{index}].destination");
            pedestrians[index] = new SimulationPedestrianCheckpoint(
                new PedestrianId(Require(item.Id, $"pedestrians[{index}].id")),
                new TripRequestId(Require(item.TripRequestId, $"pedestrians[{index}].tripRequestId")),
                origin, destination,
                (TravelMode)Require(item.Mode, $"pedestrians[{index}].mode"),
                Require(item.WalkingSpeedMetersPerSecond, $"pedestrians[{index}].walkingSpeedMetersPerSecond"),
                Require(item.LegIndex, $"pedestrians[{index}].legIndex"),
                Require(item.ProgressMeters, $"pedestrians[{index}].progressMeters"),
                (PedestrianMovementState)Require(item.State, $"pedestrians[{index}].state"));
        }

        var pedestrianCrossings = new SimulationPedestrianCrossingCheckpoint[pedestrianCrossingData.Length];
        for (var index = 0; index < pedestrianCrossings.Length; index++)
        {
            var item = pedestrianCrossingData[index] ?? throw new InvalidDataException($"PedestrianCrossing entry {index} is null.");
            pedestrianCrossings[index] = new SimulationPedestrianCrossingCheckpoint(
                new PedestrianCrossingId(Require(item.Id, $"pedestrianCrossings[{index}].id")),
                Require(item.IsOpen, $"pedestrianCrossings[{index}].isOpen"));
        }

        var vehicles = new SimulationVehicleCheckpoint[vehicleData.Length];
        for (var index = 0; index < vehicles.Length; index++)
        {
            var item = vehicleData[index] ?? throw new InvalidDataException($"Vehicle entry {index} is null.");
            var routeData = item.RouteSteps ?? throw new InvalidDataException($"Save Data is missing Vehicle Route state at vehicles[{index}].routeSteps.");
            if (routeData.Length == 0) throw new InvalidDataException($"Vehicle entry {index} has an empty Route.");
            var route = new RouteLaneStep[routeData.Length];
            for (var stepIndex = 0; stepIndex < route.Length; stepIndex++)
            {
                var step = routeData[stepIndex] ?? throw new InvalidDataException($"Vehicle Route entry {index}:{stepIndex} is null.");
                route[stepIndex] = new RouteLaneStep(
                    new LaneId(Require(step.LaneId, $"vehicles[{index}].routeSteps[{stepIndex}].laneId")),
                    new RoadSegmentId(Require(step.SegmentId, $"vehicles[{index}].routeSteps[{stepIndex}].segmentId")),
                    Require(step.StartSegmentOffset, $"vehicles[{index}].routeSteps[{stepIndex}].startSegmentOffset"),
                    Require(step.EndSegmentOffset, $"vehicles[{index}].routeSteps[{stepIndex}].endSegmentOffset"),
                    Require(step.DistanceMeters, $"vehicles[{index}].routeSteps[{stepIndex}].distanceMeters"),
                    Require(step.EstimatedTravelTimeSeconds, $"vehicles[{index}].routeSteps[{stepIndex}].estimatedTravelTimeSeconds"),
                    step.ExitConnectionId is { } connectionId ? new LaneConnectionId(connectionId) : null);
            }
            vehicles[index] = new SimulationVehicleCheckpoint(
                new VehicleId(Require(item.Id, $"vehicles[{index}].id")),
                new VehicleDimensions(
                    Require(item.LengthMeters, $"vehicles[{index}].lengthMeters"),
                    Require(item.WidthMeters, $"vehicles[{index}].widthMeters"),
                    Require(item.HeightMeters, $"vehicles[{index}].heightMeters")),
                new VehiclePerformance(
                    Require(item.MaximumSpeedMetersPerSecond, $"vehicles[{index}].maximumSpeedMetersPerSecond"),
                    Require(item.MaximumAccelerationMetersPerSecondSquared, $"vehicles[{index}].maximumAccelerationMetersPerSecondSquared"),
                    Require(item.ComfortableDecelerationMetersPerSecondSquared, $"vehicles[{index}].comfortableDecelerationMetersPerSecondSquared"),
                    Require(item.MinimumGapMeters, $"vehicles[{index}].minimumGapMeters"),
                    Require(item.TimeHeadwaySeconds, $"vehicles[{index}].timeHeadwaySeconds")),
                route,
                Require(item.RouteStepIndex, $"vehicles[{index}].routeStepIndex"),
                Require(item.RouteProgressMeters, $"vehicles[{index}].routeProgressMeters"),
                Require(item.SpeedMetersPerSecond, $"vehicles[{index}].speedMetersPerSecond"),
                (VehicleMovementState)Require(item.State, $"vehicles[{index}].state"));
        }

        var households = new SimulationHouseholdCheckpoint[householdData.Length];
        for (var index = 0; index < households.Length; index++)
        {
            var item = householdData[index] ?? throw new InvalidDataException($"Household entry {index} is null.");
            households[index] = new SimulationHouseholdCheckpoint(
                new HouseholdId(Require(item.Id, $"households[{index}].id")),
                RestoreEndpoint(item.ResidenceBuildingId, item.ResidencePoiId, $"households[{index}].residence"));
        }

        var persons = new SimulationPersonCheckpoint[personData.Length];
        for (var index = 0; index < persons.Length; index++)
        {
            var item = personData[index] ?? throw new InvalidDataException($"Person entry {index} is null.");
            var scheduleData = item.Schedule ?? throw new InvalidDataException($"Save Data is missing Person schedule at persons[{index}].schedule.");
            var schedule = new DailyActivityWindow[scheduleData.Length];
            for (var scheduleIndex = 0; scheduleIndex < schedule.Length; scheduleIndex++)
            {
                var window = scheduleData[scheduleIndex] ?? throw new InvalidDataException($"Person schedule entry {index}:{scheduleIndex} is null.");
                schedule[scheduleIndex] = new DailyActivityWindow(
                    (ActivityKind)Require(window.Activity, $"persons[{index}].schedule[{scheduleIndex}].activity"),
                    Require(window.StartMinuteOfDay, $"persons[{index}].schedule[{scheduleIndex}].startMinuteOfDay"),
                    Require(window.EndMinuteOfDay, $"persons[{index}].schedule[{scheduleIndex}].endMinuteOfDay"),
                    RestoreOptionalEndpoint(window.DestinationBuildingId, window.DestinationPoiId, $"persons[{index}].schedule[{scheduleIndex}].destination"),
                    (ActivityPriority)Require(window.Priority, $"persons[{index}].schedule[{scheduleIndex}].priority"));
            }
            var needData = item.Needs ?? throw new InvalidDataException($"Save Data is missing Person needs at persons[{index}].needs.");
            var needs = new PersonNeed[needData.Length];
            for (var needIndex = 0; needIndex < needs.Length; needIndex++)
            {
                var need = needData[needIndex] ?? throw new InvalidDataException($"Person need entry {index}:{needIndex} is null.");
                needs[needIndex] = new PersonNeed(
                    (NeedKind)Require(need.Kind, $"persons[{index}].needs[{needIndex}].kind"),
                    Require(need.Satisfaction, $"persons[{index}].needs[{needIndex}].satisfaction"),
                    Require(need.DecayPerHour, $"persons[{index}].needs[{needIndex}].decayPerHour"));
            }
            persons[index] = new SimulationPersonCheckpoint(
                new PersonId(Require(item.Id, $"persons[{index}].id")),
                new HouseholdId(Require(item.HouseholdId, $"persons[{index}].householdId")),
                new PersonDemographics(
                    Require(item.AgeYears, $"persons[{index}].ageYears"),
                    Require(item.IsEmployed, $"persons[{index}].isEmployed"),
                    Require(item.IsStudent, $"persons[{index}].isStudent"),
                    Require(item.HasPrivateVehicle, $"persons[{index}].hasPrivateVehicle")),
                RestoreEndpoint(item.ResidenceBuildingId, item.ResidencePoiId, $"persons[{index}].residence"),
                RestoreEndpoint(item.CurrentBuildingId, item.CurrentPoiId, $"persons[{index}].currentLocation"),
                (ActivityKind)Require(item.CurrentActivity, $"persons[{index}].currentActivity"),
                (PersonTravelState)Require(item.TravelState, $"persons[{index}].travelState"),
                RestoreOptionalEndpoint(item.DestinationBuildingId, item.DestinationPoiId, $"persons[{index}].destination"),
                item.DestinationActivity is { } destinationActivity ? (ActivityKind)destinationActivity : null,
                item.ActiveTripRequestId is { } tripId ? new TripRequestId(tripId) : null,
                item.ActiveTravelMode is { } activeMode ? (TravelMode)activeMode : null,
                item.PedestrianId is { } pedestrianId ? new PedestrianId(pedestrianId) : null,
                item.VehicleId is { } vehicleId ? new VehicleId(vehicleId) : null,
                schedule,
                needs);
        }

        var trackNodes = new SimulationTrackNodeCheckpoint[trackNodeData.Length];
        for (var index = 0; index < trackNodes.Length; index++)
        {
            var item = trackNodeData[index] ?? throw new InvalidDataException($"TrackNode entry {index} is null.");
            trackNodes[index] = new SimulationTrackNodeCheckpoint(
                new TrackNodeId(Require(item.Id, $"trackNodes[{index}].id")),
                (TrackNodeKind)Require(item.Kind, $"trackNodes[{index}].kind"),
                new WorldPoint(Require(item.X, $"trackNodes[{index}].x"), Require(item.Y, $"trackNodes[{index}].y"), Require(item.Z, $"trackNodes[{index}].z")));
        }
        var trackSegments = new SimulationTrackSegmentCheckpoint[trackSegmentData.Length];
        for (var index = 0; index < trackSegments.Length; index++)
        {
            var item = trackSegmentData[index] ?? throw new InvalidDataException($"TrackSegment entry {index} is null.");
            trackSegments[index] = new SimulationTrackSegmentCheckpoint(
                new TrackSegmentId(Require(item.Id, $"trackSegments[{index}].id")),
                new TrackNodeId(Require(item.StartNodeId, $"trackSegments[{index}].startNodeId")),
                new TrackNodeId(Require(item.EndNodeId, $"trackSegments[{index}].endNodeId")),
                (TrackDirection)Require(item.Direction, $"trackSegments[{index}].direction"),
                Require(item.GaugeMeters, $"trackSegments[{index}].gaugeMeters"),
                Require(item.SpeedLimitMetersPerSecond, $"trackSegments[{index}].speedLimitMetersPerSecond"),
                (TrackElectrification)Require(item.Electrification, $"trackSegments[{index}].electrification"),
                (TrackUsage)Require(item.Usage, $"trackSegments[{index}].usage"));
        }
        var trackConnections = new SimulationTrackConnectionCheckpoint[trackConnectionData.Length];
        for (var index = 0; index < trackConnections.Length; index++)
        {
            var item = trackConnectionData[index] ?? throw new InvalidDataException($"TrackConnection entry {index} is null.");
            trackConnections[index] = new SimulationTrackConnectionCheckpoint(
                new TrackConnectionId(Require(item.Id, $"trackConnections[{index}].id")),
                new TrackSegmentId(Require(item.FromSegmentId, $"trackConnections[{index}].fromSegmentId")),
                new TrackSegmentId(Require(item.ToSegmentId, $"trackConnections[{index}].toSegmentId")),
                new TrackNodeId(Require(item.ViaNodeId, $"trackConnections[{index}].viaNodeId")));
        }
        var blockSections = new SimulationBlockSectionCheckpoint[blockSectionData.Length];
        for (var index = 0; index < blockSections.Length; index++)
        {
            var item = blockSectionData[index] ?? throw new InvalidDataException($"BlockSection entry {index} is null.");
            var segmentIdsData = item.SegmentIds ?? throw new InvalidDataException($"Save Data is missing BlockSection segment IDs at blockSections[{index}].segmentIds.");
            var segmentIds = new TrackSegmentId[segmentIdsData.Length];
            for (var segmentIndex = 0; segmentIndex < segmentIds.Length; segmentIndex++) segmentIds[segmentIndex] = new TrackSegmentId(Require(segmentIdsData[segmentIndex], $"blockSections[{index}].segmentIds[{segmentIndex}]"));
            blockSections[index] = new SimulationBlockSectionCheckpoint(new BlockSectionId(Require(item.Id, $"blockSections[{index}].id")), segmentIds);
        }
        var stations = new SimulationStationCheckpoint[stationData.Length];
        for (var index = 0; index < stations.Length; index++)
        {
            var item = stationData[index] ?? throw new InvalidDataException($"Station entry {index} is null.");
            stations[index] = new SimulationStationCheckpoint(new StationId(Require(item.Id, $"stations[{index}].id")), RestoreBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ, $"stations[{index}]"));
        }
        var platforms = new SimulationPlatformCheckpoint[platformData.Length];
        for (var index = 0; index < platforms.Length; index++)
        {
            var item = platformData[index] ?? throw new InvalidDataException($"Platform entry {index} is null.");
            platforms[index] = new SimulationPlatformCheckpoint(
                new PlatformId(Require(item.Id, $"platforms[{index}].id")),
                new StationId(Require(item.StationId, $"platforms[{index}].stationId")),
                new TrackSegmentId(Require(item.TrackSegmentId, $"platforms[{index}].trackSegmentId")),
                Require(item.StartSegmentOffset, $"platforms[{index}].startSegmentOffset"),
                Require(item.EndSegmentOffset, $"platforms[{index}].endSegmentOffset"),
                RestoreBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ, $"platforms[{index}]"));
        }
        var platformAccessPoints = new SimulationPlatformAccessPointCheckpoint[platformAccessData.Length];
        for (var index = 0; index < platformAccessPoints.Length; index++)
        {
            var item = platformAccessData[index] ?? throw new InvalidDataException($"PlatformAccessPoint entry {index} is null.");
            platformAccessPoints[index] = new SimulationPlatformAccessPointCheckpoint(
                new PlatformAccessPointId(Require(item.Id, $"platformAccessPoints[{index}].id")),
                new PlatformId(Require(item.PlatformId, $"platformAccessPoints[{index}].platformId")),
                new RoadAccessPointId(Require(item.RoadAccessPointId, $"platformAccessPoints[{index}].roadAccessPointId")));
        }
        var depots = new SimulationDepotCheckpoint[depotData.Length];
        for (var index = 0; index < depots.Length; index++)
        {
            var item = depotData[index] ?? throw new InvalidDataException($"Depot entry {index} is null.");
            var segmentIdsData = item.TrackSegmentIds ?? throw new InvalidDataException($"Save Data is missing Depot track segment IDs at depots[{index}].trackSegmentIds.");
            var segmentIds = new TrackSegmentId[segmentIdsData.Length];
            for (var segmentIndex = 0; segmentIndex < segmentIds.Length; segmentIndex++) segmentIds[segmentIndex] = new TrackSegmentId(Require(segmentIdsData[segmentIndex], $"depots[{index}].trackSegmentIds[{segmentIndex}]"));
            depots[index] = new SimulationDepotCheckpoint(
                new DepotId(Require(item.Id, $"depots[{index}].id")),
                RestoreBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ, $"depots[{index}]"),
                segmentIds);
        }

        var railwayOperations = RestoreRailwayOperations(railwayOperationsData, hasRailwayOperations);

        var checkpoint = new SimulationCheckpoint(
            Require(simulation.TickRate, "simulation.tickRate"),
            Require(simulation.Seed, "simulation.seed"),
            Require(simulation.SpatialCellSize, "simulation.spatialCellSize"),
            Require(simulation.TickCount, "simulation.tickCount"),
            Require(simulation.ElapsedTicks, "simulation.elapsedTicks"),
            Require(simulation.RandomState, "simulation.randomState"),
            Require(simulation.NextAgentId, "simulation.nextAgentId"), agents,
            Require(simulation.NextBuildingId, "simulation.nextBuildingId"), buildings,
            Require(simulation.NextPoiId, "simulation.nextPoiId"), pois,
            hasRoadNetwork ? Require(simulation.NextRoadNodeId, "simulation.nextRoadNodeId") : 1UL, roadNodes,
            hasRoadNetwork ? Require(simulation.NextRoadSegmentId, "simulation.nextRoadSegmentId") : 1UL, roadSegments,
            hasRoadNetwork ? Require(simulation.NextLaneId, "simulation.nextLaneId") : 1UL, lanes,
            hasRoadNetwork ? Require(simulation.NextLaneConnectionId, "simulation.nextLaneConnectionId") : 1UL, connections,
            hasRoadNetwork ? Require(simulation.NextRoadAccessPointId, "simulation.nextRoadAccessPointId") : 1UL, accessPoints,
            hasPedestrians ? Require(simulation.NextPedestrianId, "simulation.nextPedestrianId") : 1UL, pedestrians, pedestrianCrossings,
            hasVehicles ? Require(simulation.NextVehicleId, "simulation.nextVehicleId") : 1UL, vehicles,
            hasPopulation ? Require(simulation.NextHouseholdId, "simulation.nextHouseholdId") : 1UL, households,
            hasPopulation ? Require(simulation.NextPersonId, "simulation.nextPersonId") : 1UL, persons,
            hasPopulation ? Require(simulation.NextTripRequestId, "simulation.nextTripRequestId") : 1UL,
            hasRailway ? Require(simulation.NextTrackNodeId, "simulation.nextTrackNodeId") : 1UL, trackNodes,
            hasRailway ? Require(simulation.NextTrackSegmentId, "simulation.nextTrackSegmentId") : 1UL, trackSegments,
            hasRailway ? Require(simulation.NextTrackConnectionId, "simulation.nextTrackConnectionId") : 1UL, trackConnections,
            hasRailway ? Require(simulation.NextBlockSectionId, "simulation.nextBlockSectionId") : 1UL, blockSections,
            hasRailway ? Require(simulation.NextStationId, "simulation.nextStationId") : 1UL, stations,
            hasRailway ? Require(simulation.NextPlatformId, "simulation.nextPlatformId") : 1UL, platforms,
            hasRailway ? Require(simulation.NextPlatformAccessPointId, "simulation.nextPlatformAccessPointId") : 1UL, platformAccessPoints,
            hasRailway ? Require(simulation.NextDepotId, "simulation.nextDepotId") : 1UL, depots,
            railwayOperations.NextFormationId, railwayOperations.Formations,
            railwayOperations.NextRouteId, railwayOperations.Routes,
            railwayOperations.NextTimetableId, railwayOperations.Timetables,
            railwayOperations.NextServiceId, railwayOperations.Services,
            railwayOperations.NextTrainId, railwayOperations.Trains,
            multimodalTransitData,
            economyData);
        return SimulationWorld.RestoreCheckpoint(checkpoint);
    }

    private static WorldVolume RestoreBounds(double? minX, double? minY, double? minZ, double? maxX, double? maxY, double? maxZ, string fieldName) =>
        new(Require(minX, $"{fieldName}.minX"), Require(minY, $"{fieldName}.minY"), Require(minZ, $"{fieldName}.minZ"), Require(maxX, $"{fieldName}.maxX"), Require(maxY, $"{fieldName}.maxY"), Require(maxZ, $"{fieldName}.maxZ"));

    private static TripEndpoint RestoreEndpoint(ulong? buildingId, ulong? poiId, string fieldName)
    {
        if ((buildingId is null) == (poiId is null)) throw new InvalidDataException($"Save Data field '{fieldName}' must reference exactly one Building or POI.");
        return buildingId is { } building ? TripEndpoint.ForBuilding(new BuildingId(building)) : TripEndpoint.ForPoi(new PoiId(poiId!.Value));
    }

    private static TripEndpoint? RestoreOptionalEndpoint(ulong? buildingId, ulong? poiId, string fieldName)
    {
        if (buildingId is null && poiId is null) return null;
        return RestoreEndpoint(buildingId, poiId, fieldName);
    }

    private static void ValidateMaterializedCounts(
        int agents, int buildings, int pois, int nodes, int segments, int lanes, int connections, int accessPoints,
        int pedestrians, int pedestrianCrossings, int vehicles, int households, int persons, WorldSaveLimits limits)
    {
        ValidateCount(agents, limits.MaximumAgentCount, "Agents");
        ValidateCount(buildings, limits.MaximumBuildingCount, "Buildings");
        ValidateCount(pois, limits.MaximumPoiCount, "POIs");
        ValidateCount(nodes, limits.MaximumRoadNodeCount, "RoadNodes");
        ValidateCount(segments, limits.MaximumRoadSegmentCount, "RoadSegments");
        ValidateCount(lanes, limits.MaximumLaneCount, "Lanes");
        ValidateCount(connections, limits.MaximumLaneConnectionCount, "LaneConnections");
        ValidateCount(accessPoints, limits.MaximumRoadAccessPointCount, "RoadAccessPoints");
        ValidateCount(pedestrians, limits.MaximumPedestrianCount, "Pedestrians");
        ValidateCount(pedestrianCrossings, limits.MaximumPedestrianCrossingCount, "PedestrianCrossings");
        ValidateCount(vehicles, limits.MaximumVehicleCount, "Vehicles");
        ValidateCount(households, limits.MaximumHouseholdCount, "Households");
        ValidateCount(persons, limits.MaximumPersonCount, "Persons");
    }

    private static void ValidateCount(int count, int maximum, string name)
    {
        if (count > maximum) throw new InvalidDataException($"Save Data contains {count} {name}, exceeding the configured {maximum}-{name} limit.");
    }

    private static void ValidateCollectionCountsBeforeMaterialization(ReadOnlySpan<byte> json, WorldSaveLimits limits)
    {
        var reader = new Utf8JsonReader(json, new JsonReaderOptions { CommentHandling = JsonCommentHandling.Disallow, MaxDepth = JsonOptions.MaxDepth });
        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName) continue;
            if (reader.ValueTextEquals("agents")) ValidateNamedArrayElementCount(ref reader, limits.MaximumAgentCount, "Agent");
            else if (reader.ValueTextEquals("buildings")) ValidateNamedArrayElementCount(ref reader, limits.MaximumBuildingCount, "Building");
            else if (reader.ValueTextEquals("pois")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPoiCount, "POI");
            else if (reader.ValueTextEquals("roadNodes")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadNodeCount, "RoadNode");
            else if (reader.ValueTextEquals("roadSegments")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadSegmentCount, "RoadSegment");
            else if (reader.ValueTextEquals("lanes")) ValidateNamedArrayElementCount(ref reader, limits.MaximumLaneCount, "Lane");
            else if (reader.ValueTextEquals("laneConnections")) ValidateNamedArrayElementCount(ref reader, limits.MaximumLaneConnectionCount, "LaneConnection");
            else if (reader.ValueTextEquals("roadAccessPoints")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadAccessPointCount, "RoadAccessPoint");
            else if (reader.ValueTextEquals("pedestrians")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPedestrianCount, "Pedestrian");
            else if (reader.ValueTextEquals("pedestrianCrossings")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPedestrianCrossingCount, "PedestrianCrossing");
            else if (reader.ValueTextEquals("vehicles")) ValidateNamedArrayElementCount(ref reader, limits.MaximumVehicleCount, "Vehicle");
            else if (reader.ValueTextEquals("households")) ValidateNamedArrayElementCount(ref reader, limits.MaximumHouseholdCount, "Household");
            else if (reader.ValueTextEquals("persons")) ValidateNamedArrayElementCount(ref reader, limits.MaximumPersonCount, "Person");
            else if (reader.ValueTextEquals("trackNodes")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadNodeCount, "TrackNode");
            else if (reader.ValueTextEquals("trackSegments")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadSegmentCount, "TrackSegment");
            else if (reader.ValueTextEquals("trackConnections")) ValidateNamedArrayElementCount(ref reader, limits.MaximumLaneConnectionCount, "TrackConnection");
            else if (reader.ValueTextEquals("blockSections")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadSegmentCount, "BlockSection");
            else if (reader.ValueTextEquals("stations")) ValidateNamedArrayElementCount(ref reader, limits.MaximumBuildingCount, "Station");
            else if (reader.ValueTextEquals("platforms")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadAccessPointCount, "Platform");
            else if (reader.ValueTextEquals("platformAccessPoints")) ValidateNamedArrayElementCount(ref reader, limits.MaximumRoadAccessPointCount, "PlatformAccessPoint");
            else if (reader.ValueTextEquals("depots")) ValidateNamedArrayElementCount(ref reader, limits.MaximumBuildingCount, "Depot");
            else if (reader.ValueTextEquals("railwayOperations")) ValidateRailwayOperationsArrayCounts(ref reader, limits);
            else if (reader.ValueTextEquals("multimodalTransit")) ValidateMultimodalTransitArrayCounts(ref reader, limits);
        }
    }

    private static void ValidateNamedArrayElementCount(ref Utf8JsonReader reader, int maximumCount, string entityName)
    {
        if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray) return;
        var arrayDepth = reader.CurrentDepth;
        var count = 0;
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

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => length;
        public override long Position { get => length; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length > maximumBytes - length) throw new InvalidDataException($"Save Data output exceeds the configured {maximumBytes}-byte limit.");
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
                if (remaining == 0) break;
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
                if (remaining == 0) break;
            }
        }
    }
}
