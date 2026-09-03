using System.Globalization;
using System.Reflection;
using MachiVerseWorks.Persistence;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class AdminCommandExecutorV2(
    AdminCommandQueue queue,
    SimulationRuntime simulation,
    ClientConnectionRegistry connections,
    IHostApplicationLifetime lifetime,
    ILogger<AdminCommandExecutorV2> logger) : BackgroundService
{
    private sealed record Descriptor(string Name, string Syntax, string Summary);

    private static readonly Descriptor[] Descriptors =
    [
        new("help", "help [command]", "Show command help."),
        new("status", "status", "Show server and simulation status."),
        new("version", "version", "Show application version."),
        new("exit", "exit", "Request graceful server shutdown."),
        new("simulation", "simulation status|pause|resume|step [count]", "Control deterministic simulation execution."),
        new("agent", "agent list|show|add|update|remove ...", "Inspect and mutate agents."),
        new("building", "building list|show|add|update|remove ...", "Inspect and mutate buildings."),
        new("poi", "poi list|show|add|update|remove ...", "Inspect and mutate POIs."),
        new("road", "road node|segment|lane|connection|access list|show|add|update|remove ...", "Inspect and mutate road infrastructure."),
        new("vehicle", "vehicle list|show|spawn|remove ...", "Inspect vehicles or spawn one from a routing result."),
        new("railway", "railway node|segment|connection|block|station|platform|access|depot list|show|add|update|remove ...", "Inspect and mutate railway infrastructure."),
        new("formation", "formation list|show|add ...", "Inspect or create train formations."),
        new("railroute", "railroute list|show|add <trackSegmentIdsCsv>", "Inspect or create railway routes."),
        new("timetable", "timetable list|show|add <station:arrival:departure:dwell[:platform],...>", "Inspect or create timetables."),
        new("service", "service list|show|add <formation> <route> <timetable> <originDepot> <destinationDepot> [startTick]", "Inspect or create railway services."),
        new("train", "train list|show|add <serviceId>", "Inspect or safely create trains."),
        new("connection", "connection list|show|disconnect <guid>", "Inspect or disconnect server client connections."),
        new("world", "world save|load <path>", "Persist or atomically replace the authoritative world."),
    ];

    private static readonly Action<ILogger, string, Exception?> CommandFailed = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(2010, nameof(CommandFailed)),
        "Administration command failed: {Command}");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, request.CancellationToken);
                    request.CancellationToken.ThrowIfCancellationRequested();
                    var result = await ExecuteCoreAsync(request.Command, executionCancellation.Token);
                    request.CancellationToken.ThrowIfCancellationRequested();
                    request.Completion.TrySetResult(result);
                }
                catch (OperationCanceledException) when (request.CancellationToken.IsCancellationRequested)
                {
                    request.Completion.TrySetCanceled(request.CancellationToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    request.Completion.TrySetCanceled(stoppingToken);
                }
                catch (Exception exception)
                {
                    CommandFailed(logger, request.Command.RawText, exception);
                    request.Completion.TrySetResult(MapException(exception));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<AdminCommandResult> ExecuteCoreAsync(AdminCommand command, CancellationToken cancellationToken) => command.Name switch
    {
        "help" => Help(command),
        "status" => Status(),
        "version" => Version(),
        "exit" or "stop" => Stop(),
        "simulation" or "sim" => Simulation(command),
        "agent" => Agent(command),
        "building" => Building(command),
        "poi" => Poi(command),
        "road" => Road(command),
        "vehicle" => Vehicle(command),
        "railway" => Railway(command),
        "formation" => Formation(command),
        "railroute" => RailRoute(command),
        "timetable" => Timetable(command),
        "service" => Service(command),
        "train" => Train(command),
        "connection" or "connections" => Connection(command),
        "world" => await WorldAsync(command, cancellationToken),
        _ => new(AdminCommandResultCode.UnknownCommand, $"Unknown command '{command.Name}'. Run 'help' for supported commands."),
    };

    private static AdminCommandResult Help(AdminCommand command)
    {
        if (command.Arguments.Count == 0)
            return AdminCommandResult.Ok(string.Join(Environment.NewLine, Descriptors.Select(x => $"{x.Syntax} - {x.Summary}")));
        var descriptor = Descriptors.FirstOrDefault(x => Eq(x.Name, command.Arguments[0]));
        return descriptor is null
            ? new(AdminCommandResultCode.UnknownCommand, $"Unknown command '{command.Arguments[0]}'.")
            : AdminCommandResult.Ok($"{descriptor.Syntax}{Environment.NewLine}{descriptor.Summary}");
    }

    private AdminCommandResult Status() => AdminCommandResult.Ok(FormattableString.Invariant(
        $"tick={simulation.TickCount} paused={simulation.IsPaused.ToString().ToLowerInvariant()} agents={simulation.ActiveAgentCount} vehicles={simulation.ActiveVehicleCount} roadSegments={simulation.RoadSegmentCount} trackSegments={simulation.TrackSegmentCount} connections={connections.Count}"));

    private static AdminCommandResult Version() => AdminCommandResult.Ok(
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "unknown");

    private AdminCommandResult Stop()
    {
        lifetime.StopApplication();
        return AdminCommandResult.Ok("Server shutdown requested.");
    }

    private AdminCommandResult Simulation(AdminCommand command)
    {
        var action = command.Arguments.Count == 0 ? "status" : command.Arguments[0];
        if (Eq(action, "status")) return AdminCommandResult.Ok(FormattableString.Invariant($"tick={simulation.TickCount} paused={simulation.IsPaused.ToString().ToLowerInvariant()} tickRate={simulation.TickRate}"));
        if (Eq(action, "pause")) return simulation.Pause() ? AdminCommandResult.Ok("Simulation paused.") : new(AdminCommandResultCode.InvalidState, "Simulation is already paused.");
        if (Eq(action, "resume")) return simulation.Resume() ? AdminCommandResult.Ok("Simulation resumed.") : new(AdminCommandResultCode.InvalidState, "Simulation is not paused.");
        if (Eq(action, "step"))
        {
            var count = command.Arguments.Count > 1 ? PositiveInt(command.Arguments[1], "count") : 1;
            return AdminCommandResult.Ok(FormattableString.Invariant($"tick={simulation.StepPaused(count)} advanced={count}"));
        }
        return InvalidAction("simulation", action);
    }

    private AdminCommandResult Agent(AdminCommand command)
    {
        var action = Action(command, "agent");
        if (Eq(action, "list")) return ListResult(simulation.Read(static w => w.CreateAllAgentSnapshots()).OrderBy(x => x.Id.Value).Select(FormatAgent), "No agents.");
        if (Eq(action, "show"))
        {
            var id = new AgentId(Id(Arg(command, 1, "id"), "id"));
            var item = simulation.Read(w => w.TryGetAgentSnapshot(id, out var x) ? x : (AgentSnapshot?)null);
            return item is { } value ? AdminCommandResult.Ok(FormatAgent(value)) : NotFound("Agent", id.Value);
        }
        if (Eq(action, "add"))
        {
            var id = simulation.Mutate(w => w.CreateAgent(Point(command, 1), Velocity(command)));
            return AdminCommandResult.Ok($"Agent {id.Value} created.");
        }
        if (Eq(action, "update"))
        {
            var id = new AgentId(Id(Arg(command, 1, "id"), "id"));
            return simulation.Mutate(w => w.UpdateAgent(id, Point(command, 2), Velocity(command))) ? AdminCommandResult.Ok($"Agent {id.Value} updated.") : NotFound("Agent", id.Value);
        }
        if (Eq(action, "remove"))
        {
            var id = new AgentId(Id(Arg(command, 1, "id"), "id"));
            return simulation.Mutate(w => w.RemoveAgent(id)) ? AdminCommandResult.Ok($"Agent {id.Value} removed.") : NotFound("Agent", id.Value);
        }
        return InvalidAction("agent", action);
    }

    private AdminCommandResult Building(AdminCommand command)
    {
        var action = Action(command, "building");
        if (Eq(action, "list")) return ListResult(simulation.Read(static w => w.CreateBuildingSnapshot()).Select(FormatBuilding), "No buildings.");
        if (Eq(action, "show"))
        {
            var id = new BuildingId(Id(Arg(command, 1, "id"), "id"));
            var item = simulation.Read(w => w.TryGetBuildingSnapshot(id, out var x) ? x : (BuildingSnapshot?)null);
            return item is { } value ? AdminCommandResult.Ok(FormatBuilding(value)) : NotFound("Building", id.Value);
        }
        if (Eq(action, "add"))
        {
            var id = simulation.Mutate(w => w.CreateBuilding(Volume(command, 1), OptionEnum(command, "kind", BuildingKind.Generic)));
            return AdminCommandResult.Ok($"Building {id.Value} created.");
        }
        if (Eq(action, "update"))
        {
            var id = new BuildingId(Id(Arg(command, 1, "id"), "id"));
            var kind = OptionEnum(command, "kind", BuildingKind.Generic);
            return simulation.Mutate(w => w.UpdateBuilding(id, Volume(command, 2), kind)) ? AdminCommandResult.Ok($"Building {id.Value} updated.") : NotFound("Building", id.Value);
        }
        if (Eq(action, "remove"))
        {
            var id = new BuildingId(Id(Arg(command, 1, "id"), "id"));
            return simulation.Mutate(w => w.RemoveBuilding(id)) ? AdminCommandResult.Ok($"Building {id.Value} removed.") : NotFound("Building", id.Value);
        }
        return InvalidAction("building", action);
    }

    private AdminCommandResult Poi(AdminCommand command)
    {
        var action = Action(command, "poi");
        if (Eq(action, "list")) return ListResult(simulation.Read(static w => w.CreatePoiSnapshot()).Select(FormatPoi), "No POIs.");
        if (Eq(action, "show"))
        {
            var id = new PoiId(Id(Arg(command, 1, "id"), "id"));
            var item = simulation.Read(w => w.TryGetPoiSnapshot(id, out var x) ? x : (PoiSnapshot?)null);
            return item is { } value ? AdminCommandResult.Ok(FormatPoi(value)) : NotFound("POI", id.Value);
        }
        if (Eq(action, "add") || Eq(action, "update"))
        {
            var start = Eq(action, "add") ? 1 : 2;
            var id = Eq(action, "add") ? default : new PoiId(Id(Arg(command, 1, "id"), "id"));
            var position = Point(command, start);
            var kind = OptionEnum(command, "kind", PoiKind.Generic);
            BuildingId? building = OptionId(command, "building") is { } buildingId ? new BuildingId(buildingId) : null;
            if (Eq(action, "add"))
            {
                var created = simulation.Mutate(w => w.CreatePoi(position, kind, building));
                return AdminCommandResult.Ok($"POI {created.Value} created.");
            }
            return simulation.Mutate(w => w.UpdatePoi(id, position, kind, building)) ? AdminCommandResult.Ok($"POI {id.Value} updated.") : NotFound("POI", id.Value);
        }
        if (Eq(action, "remove"))
        {
            var id = new PoiId(Id(Arg(command, 1, "id"), "id"));
            return simulation.Mutate(w => w.RemovePoi(id)) ? AdminCommandResult.Ok($"POI {id.Value} removed.") : NotFound("POI", id.Value);
        }
        return InvalidAction("poi", action);
    }

    private AdminCommandResult Road(AdminCommand command)
    {
        if (command.Arguments.Count < 2) return Syntax("road node|segment|lane|connection|access list|show|add|update|remove ...");
        var entity = command.Arguments[0].ToLowerInvariant();
        var action = command.Arguments[1].ToLowerInvariant();
        var snapshot = simulation.Read(static w => w.CreateRoadNetworkSnapshot());
        if (Eq(action, "list")) return RoadList(entity, snapshot);
        if (Eq(action, "show")) return RoadShow(entity, snapshot, Id(Arg(command, 2, "id"), "id"));

        if (entity == "node")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateRoadNode(Point(command, 2), OptionEnum(command, "kind", RoadNodeKind.Endpoint)), roadTopologyChanged: true); return AdminCommandResult.Ok($"Road node {created.Value} created."); }
            var id = new RoadNodeId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") return simulation.Mutate(w => w.UpdateRoadNode(id, Point(command, 3), OptionEnum(command, "kind", RoadNodeKind.Endpoint)), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road node {id.Value} updated.") : NotFound("Road node", id.Value);
            if (action == "remove") return simulation.Mutate(w => w.RemoveRoadNode(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road node {id.Value} removed.") : NotFound("Road node", id.Value);
        }
        if (entity == "segment")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateRoadSegment(new RoadNodeId(Id(Arg(command, 2, "startNode"), "startNode")), new RoadNodeId(Id(Arg(command, 3, "endNode"), "endNode")), OptionEnum(command, "kind", RoadKind.Local)), roadTopologyChanged: true); return AdminCommandResult.Ok($"Road segment {created.Value} created."); }
            var id = new RoadSegmentId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") return simulation.Mutate(w => w.UpdateRoadSegment(id, new RoadNodeId(Id(Arg(command, 3, "startNode"), "startNode")), new RoadNodeId(Id(Arg(command, 4, "endNode"), "endNode")), OptionEnum(command, "kind", RoadKind.Local)), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road segment {id.Value} updated.") : NotFound("Road segment", id.Value);
            if (action == "remove") return simulation.Mutate(w => w.RemoveRoadSegment(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road segment {id.Value} removed.") : NotFound("Road segment", id.Value);
        }
        if (entity == "lane")
        {
            if (action == "add") { var created = CreateLane(command, 2); return AdminCommandResult.Ok($"Lane {created.Value} created."); }
            var id = new LaneId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") return UpdateLane(command, id) ? AdminCommandResult.Ok($"Lane {id.Value} updated.") : NotFound("Lane", id.Value);
            if (action == "remove") return simulation.Mutate(w => w.RemoveLane(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Lane {id.Value} removed.") : NotFound("Lane", id.Value);
        }
        if (entity == "connection")
        {
            if (action == "add") { var created = CreateLaneConnection(command, 2); return AdminCommandResult.Ok($"Lane connection {created.Value} created."); }
            var id = new LaneConnectionId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") return UpdateLaneConnection(command, id) ? AdminCommandResult.Ok($"Lane connection {id.Value} updated.") : NotFound("Lane connection", id.Value);
            if (action == "remove") return simulation.Mutate(w => w.RemoveLaneConnection(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Lane connection {id.Value} removed.") : NotFound("Lane connection", id.Value);
        }
        if (entity == "access")
        {
            if (action == "add") { var created = CreateRoadAccess(command, 2); return AdminCommandResult.Ok($"Road access point {created.Value} created."); }
            var id = new RoadAccessPointId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") return UpdateRoadAccess(command, id) ? AdminCommandResult.Ok($"Road access point {id.Value} updated.") : NotFound("Road access point", id.Value);
            if (action == "remove") return simulation.Mutate(w => w.RemoveRoadAccessPoint(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road access point {id.Value} removed.") : NotFound("Road access point", id.Value);
        }
        return new(AdminCommandResultCode.InvalidArgument, $"Unsupported road operation '{entity} {action}'.");
    }

    private AdminCommandResult Vehicle(AdminCommand command)
    {
        var action = Action(command, "vehicle");
        if (Eq(action, "list")) return ListResult(simulation.Read(static w => w.CreateAllVehicleSnapshots()).OrderBy(x => x.Id.Value).Select(FormatVehicle), "No vehicles.");
        if (Eq(action, "show"))
        {
            var id = new VehicleId(Id(Arg(command, 1, "id"), "id"));
            var item = simulation.Read(w => w.TryGetVehicleSnapshot(id, out var x) ? x : (VehicleSnapshot?)null);
            return item is { } value ? AdminCommandResult.Ok(FormatVehicle(value)) : NotFound("Vehicle", id.Value);
        }
        if (Eq(action, "spawn"))
        {
            var origin = Point(command, 1); var destination = Point(command, 4);
            var cost = OptionEnum(command, "cost", RoutingCostMetric.Distance);
            var length = OptionDouble(command, "length", 4.5d); var speed = OptionDouble(command, "speed", 0d);
            var id = simulation.Mutate(w =>
            {
                var route = w.FindRoadRoute(new RouteRequest(origin, destination, cost));
                if (route.Steps.Count == 0) throw new InvalidOperationException("Routing produced an empty vehicle route.");
                return w.CreateVehicle(route, new VehicleDimensions(length, 1.8d, 1.5d), initialSpeedMetersPerSecond: speed);
            });
            return AdminCommandResult.Ok($"Vehicle {id.Value} created from a routing result.");
        }
        if (Eq(action, "remove"))
        {
            var id = new VehicleId(Id(Arg(command, 1, "id"), "id"));
            return simulation.Mutate(w => w.RemoveVehicle(id)) ? AdminCommandResult.Ok($"Vehicle {id.Value} removed.") : NotFound("Vehicle", id.Value);
        }
        return InvalidAction("vehicle", action);
    }

    private AdminCommandResult Railway(AdminCommand command)
    {
        if (command.Arguments.Count < 2) return Syntax("railway node|segment|connection|block|station|platform|access|depot list|show|add|update|remove ...");
        var entity = command.Arguments[0].ToLowerInvariant(); var action = command.Arguments[1].ToLowerInvariant();
        var snapshot = simulation.Read(static w => w.CreateRailwayInfrastructureSnapshot());
        if (action == "list") return RailwayList(entity, snapshot);
        if (action == "show") return RailwayShow(entity, snapshot, Id(Arg(command, 2, "id"), "id"));
        return RailwayMutate(command, entity, action);
    }

    private AdminCommandResult Formation(AdminCommand command)
    {
        var action = Action(command, "formation"); var snapshot = simulation.Read(static w => w.CreateRailwayOperationsSnapshot());
        if (action == "list") return ListResult(snapshot.Formations.Select(FormatFormation), "No formations.");
        if (action == "show") { var id = Id(Arg(command, 1, "id"), "id"); var item = snapshot.Formations.FirstOrDefault(x => x.Id.Value == id); return item is null ? NotFound("Formation", id) : AdminCommandResult.Ok(FormatFormation(item)); }
        if (action == "add")
        {
            var created = simulation.Mutate(w => w.CreateTrainFormation(Double(Arg(command, 1, "length"), "length"), Double(Arg(command, 2, "maxSpeed"), "maxSpeed"), Double(Arg(command, 3, "acceleration"), "acceleration"), Double(Arg(command, 4, "deceleration"), "deceleration"), PositiveInt(Arg(command, 5, "capacity"), "capacity")));
            return AdminCommandResult.Ok($"Formation {created.Value} created.");
        }
        return InvalidAction("formation", action);
    }

    private AdminCommandResult RailRoute(AdminCommand command)
    {
        var action = Action(command, "railroute"); var snapshot = simulation.Read(static w => w.CreateRailwayOperationsSnapshot());
        if (action == "list") return ListResult(snapshot.Routes.Select(FormatRailRoute), "No railway routes.");
        if (action == "show") { var id = Id(Arg(command, 1, "id"), "id"); var item = snapshot.Routes.FirstOrDefault(x => x.Id.Value == id); return item is null ? NotFound("Railway route", id) : AdminCommandResult.Ok(FormatRailRoute(item)); }
        if (action == "add") { var ids = CsvIds(Arg(command, 1, "trackSegmentIds")).Select(x => new TrackSegmentId(x)).ToArray(); var created = simulation.Mutate(w => w.CreateRailwayRoute(ids)); return AdminCommandResult.Ok($"Railway route {created.Value} created."); }
        return InvalidAction("railroute", action);
    }

    private AdminCommandResult Timetable(AdminCommand command)
    {
        var action = Action(command, "timetable"); var snapshot = simulation.Read(static w => w.CreateRailwayOperationsSnapshot());
        if (action == "list") return ListResult(snapshot.Timetables.Select(FormatTimetable), "No timetables.");
        if (action == "show") { var id = Id(Arg(command, 1, "id"), "id"); var item = snapshot.Timetables.FirstOrDefault(x => x.Id.Value == id); return item is null ? NotFound("Timetable", id) : AdminCommandResult.Ok(FormatTimetable(item)); }
        if (action == "add") { var stops = ParseStops(Arg(command, 1, "stops")); var created = simulation.Mutate(w => w.CreateTimetable(stops)); return AdminCommandResult.Ok($"Timetable {created.Value} created."); }
        return InvalidAction("timetable", action);
    }

    private AdminCommandResult Service(AdminCommand command)
    {
        var action = Action(command, "service"); var snapshot = simulation.Read(static w => w.CreateRailwayOperationsSnapshot());
        if (action == "list") return ListResult(snapshot.Services.Select(FormatService), "No railway services.");
        if (action == "show") { var id = Id(Arg(command, 1, "id"), "id"); var item = snapshot.Services.FirstOrDefault(x => x.Id.Value == id); return item is null ? NotFound("Railway service", id) : AdminCommandResult.Ok(FormatService(item)); }
        if (action == "add")
        {
            var start = command.Arguments.Count > 6 ? UInt64(Arg(command, 6, "startTick"), "startTick", allowZero: true) : 0UL;
            var created = simulation.Mutate(w => w.CreateRailwayService(new TrainFormationId(Id(Arg(command, 1, "formation"), "formation")), new RailwayRouteId(Id(Arg(command, 2, "route"), "route")), new TimetableId(Id(Arg(command, 3, "timetable"), "timetable")), new DepotId(Id(Arg(command, 4, "originDepot"), "originDepot")), new DepotId(Id(Arg(command, 5, "destinationDepot"), "destinationDepot")), start));
            return AdminCommandResult.Ok($"Railway service {created.Value} created.");
        }
        return InvalidAction("service", action);
    }

    private AdminCommandResult Train(AdminCommand command)
    {
        var action = Action(command, "train"); var snapshot = simulation.Read(static w => w.CreateRailwayOperationsSnapshot());
        if (action == "list") return ListResult(snapshot.Trains.Select(FormatTrain), "No trains.");
        if (action == "show") { var id = Id(Arg(command, 1, "id"), "id"); var item = snapshot.Trains.FirstOrDefault(x => x.Id.Value == id); return item is null ? NotFound("Train", id) : AdminCommandResult.Ok(FormatTrain(item)); }
        if (action == "add") { var created = simulation.Mutate(w => w.CreateTrain(new RailwayServiceId(Id(Arg(command, 1, "service"), "service")))); return AdminCommandResult.Ok($"Train {created.Value} created."); }
        return InvalidAction("train", action);
    }

    private AdminCommandResult Connection(AdminCommand command)
    {
        var action = command.Arguments.Count == 0 ? "list" : command.Arguments[0];
        if (Eq(action, "list")) return ListResult(connections.CreateSnapshot().OrderBy(x => x.Id).Select(x => $"{x.Id} state={x.Socket.State} protocol={x.NegotiatedVersion}"), "No client connections.");
        var id = GuidValue(Arg(command, 1, "id"));
        if (!connections.TryGet(id, out var item) || item is null) return new(AdminCommandResultCode.NotFound, $"Connection {id} was not found.");
        if (Eq(action, "show")) return AdminCommandResult.Ok($"{item.Id} state={item.Socket.State} handshake={item.HandshakeCompleted} protocol={item.NegotiatedVersion}");
        if (Eq(action, "disconnect")) { item.Abort(); connections.Remove(id); return AdminCommandResult.Ok($"Connection {id} disconnected."); }
        return InvalidAction("connection", action);
    }

    private async Task<AdminCommandResult> WorldAsync(AdminCommand command, CancellationToken cancellationToken)
    {
        var action = Action(command, "world"); var path = Path.GetFullPath(Arg(command, 1, "path"));
        if (Eq(action, "save") || Eq(action, "save-new"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detached = SimulationWorld.RestoreCheckpoint(simulation.CaptureCheckpoint());
            cancellationToken.ThrowIfCancellationRequested();
            var data = WorldSaveSerializer.Serialize(detached);
            cancellationToken.ThrowIfCancellationRequested();
            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporaryPath, data, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporaryPath, path, overwrite: Eq(action, "save"));
                }
                catch (IOException) when (Eq(action, "save-new") && File.Exists(path))
                {
                    return new AdminCommandResult(AdminCommandResultCode.Conflict, $"World save '{path}' already exists.");
                }
                return AdminCommandResult.Ok($"World saved to '{path}'.");
            }
            finally
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        if (Eq(action, "load"))
        {
            var data = await File.ReadAllBytesAsync(path, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var world = WorldSaveSerializer.Deserialize(data);
            cancellationToken.ThrowIfCancellationRequested();
            simulation.ReplaceWorld(world);
            foreach (var connection in connections.CreateSnapshot())
            {
                connection.Abort();
                connections.Remove(connection.Id);
            }
            return AdminCommandResult.Ok($"World loaded from '{path}'.");
        }
        return InvalidAction("world", action);
    }

    private AdminCommandResult RailwayMutate(AdminCommand command, string entity, string action)
    {
        bool updated;
        if (entity == "node")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateTrackNode(Point(command, 2), OptionEnum(command, "kind", TrackNodeKind.Endpoint)), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Track node {created.Value} created."); }
            var id = new TrackNodeId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdateTrackNode(id, Point(command, 3), OptionEnum(command, "kind", TrackNodeKind.Endpoint)), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemoveTrackNode(id), railwayTopologyChanged: true);
            else return InvalidAction("railway node", action);
            return updated ? AdminCommandResult.Ok($"Track node {id.Value} {action}d.") : NotFound("Track node", id.Value);
        }
        if (entity == "segment")
        {
            if (action == "add")
            {
                var created = simulation.Mutate(w => w.CreateTrackSegment(new TrackNodeId(Id(Arg(command, 2, "startNode"), "startNode")), new TrackNodeId(Id(Arg(command, 3, "endNode"), "endNode")), OptionEnum(command, "direction", TrackDirection.Bidirectional), OptionDouble(command, "gauge", 1.435d), OptionDouble(command, "speed", 22.2222222222d), OptionEnum(command, "electrification", TrackElectrification.None), OptionEnum(command, "usage", TrackUsage.Mainline)), railwayTopologyChanged: true);
                return AdminCommandResult.Ok($"Track segment {created.Value} created.");
            }
            var id = new TrackSegmentId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdateTrackSegment(id, new TrackNodeId(Id(Arg(command, 3, "startNode"), "startNode")), new TrackNodeId(Id(Arg(command, 4, "endNode"), "endNode")), OptionEnum(command, "direction", TrackDirection.Bidirectional), OptionDouble(command, "gauge", 1.435d), OptionDouble(command, "speed", 22.2222222222d), OptionEnum(command, "electrification", TrackElectrification.None), OptionEnum(command, "usage", TrackUsage.Mainline)), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemoveTrackSegment(id), railwayTopologyChanged: true);
            else return InvalidAction("railway segment", action);
            return updated ? AdminCommandResult.Ok($"Track segment {id.Value} {action}d.") : NotFound("Track segment", id.Value);
        }
        if (entity == "connection")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateTrackConnection(new TrackSegmentId(Id(Arg(command, 2, "from"), "from")), new TrackSegmentId(Id(Arg(command, 3, "to"), "to")), new TrackNodeId(Id(Arg(command, 4, "via"), "via"))), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Track connection {created.Value} created."); }
            var id = new TrackConnectionId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdateTrackConnection(id, new TrackSegmentId(Id(Arg(command, 3, "from"), "from")), new TrackSegmentId(Id(Arg(command, 4, "to"), "to")), new TrackNodeId(Id(Arg(command, 5, "via"), "via"))), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemoveTrackConnection(id), railwayTopologyChanged: true);
            else return InvalidAction("railway connection", action);
            return updated ? AdminCommandResult.Ok($"Track connection {id.Value} {action}d.") : NotFound("Track connection", id.Value);
        }
        if (entity == "block")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateBlockSection(CsvIds(Arg(command, 2, "segments")).Select(x => new TrackSegmentId(x)).ToArray()), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Block section {created.Value} created."); }
            var id = new BlockSectionId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdateBlockSection(id, CsvIds(Arg(command, 3, "segments")).Select(x => new TrackSegmentId(x)).ToArray()), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemoveBlockSection(id), railwayTopologyChanged: true);
            else return InvalidAction("railway block", action);
            return updated ? AdminCommandResult.Ok($"Block section {id.Value} {action}d.") : NotFound("Block section", id.Value);
        }
        if (entity == "station")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateStation(Volume(command, 2)), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Station {created.Value} created."); }
            var id = new StationId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdateStation(id, Volume(command, 3)), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemoveStation(id), railwayTopologyChanged: true);
            else return InvalidAction("railway station", action);
            return updated ? AdminCommandResult.Ok($"Station {id.Value} {action}d.") : NotFound("Station", id.Value);
        }
        if (entity == "platform")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreatePlatform(new StationId(Id(Arg(command, 2, "station"), "station")), new TrackSegmentId(Id(Arg(command, 3, "segment"), "segment")), Double(Arg(command, 4, "startOffset"), "startOffset"), Double(Arg(command, 5, "endOffset"), "endOffset"), Volume(command, 6)), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Platform {created.Value} created."); }
            var id = new PlatformId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdatePlatform(id, new StationId(Id(Arg(command, 3, "station"), "station")), new TrackSegmentId(Id(Arg(command, 4, "segment"), "segment")), Double(Arg(command, 5, "startOffset"), "startOffset"), Double(Arg(command, 6, "endOffset"), "endOffset"), Volume(command, 7)), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemovePlatform(id), railwayTopologyChanged: true);
            else return InvalidAction("railway platform", action);
            return updated ? AdminCommandResult.Ok($"Platform {id.Value} {action}d.") : NotFound("Platform", id.Value);
        }
        if (entity == "access")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreatePlatformAccessPoint(new PlatformId(Id(Arg(command, 2, "platform"), "platform")), new RoadAccessPointId(Id(Arg(command, 3, "roadAccess"), "roadAccess"))), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Platform access point {created.Value} created."); }
            var id = new PlatformAccessPointId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdatePlatformAccessPoint(id, new PlatformId(Id(Arg(command, 3, "platform"), "platform")), new RoadAccessPointId(Id(Arg(command, 4, "roadAccess"), "roadAccess"))), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemovePlatformAccessPoint(id), railwayTopologyChanged: true);
            else return InvalidAction("railway access", action);
            return updated ? AdminCommandResult.Ok($"Platform access point {id.Value} {action}d.") : NotFound("Platform access point", id.Value);
        }
        if (entity == "depot")
        {
            if (action == "add") { var created = simulation.Mutate(w => w.CreateDepot(Volume(command, 2), CsvIds(Arg(command, 8, "segments")).Select(x => new TrackSegmentId(x)).ToArray()), railwayTopologyChanged: true); return AdminCommandResult.Ok($"Depot {created.Value} created."); }
            var id = new DepotId(Id(Arg(command, 2, "id"), "id"));
            if (action == "update") updated = simulation.Mutate(w => w.UpdateDepot(id, Volume(command, 3), CsvIds(Arg(command, 9, "segments")).Select(x => new TrackSegmentId(x)).ToArray()), railwayTopologyChanged: true);
            else if (action == "remove") updated = simulation.Mutate(w => w.RemoveDepot(id), railwayTopologyChanged: true);
            else return InvalidAction("railway depot", action);
            return updated ? AdminCommandResult.Ok($"Depot {id.Value} {action}d.") : NotFound("Depot", id.Value);
        }
        return new(AdminCommandResultCode.InvalidArgument, $"Unknown railway entity '{entity}'.");
    }

    private LaneId CreateLane(AdminCommand c, int start) => simulation.Mutate(w => w.CreateLane(new RoadSegmentId(Id(Arg(c, start, "segment"), "segment")), OptionEnum(c, "direction", LaneDirection.Forward), checked((ushort)OptionInt(c, "order", 0)), OptionDouble(c, "width", 3.5d), OptionDouble(c, "speed", 13.8888888889d)), roadTopologyChanged: true);
    private bool UpdateLane(AdminCommand c, LaneId id) => simulation.Mutate(w => w.UpdateLane(id, new RoadSegmentId(Id(Arg(c, 3, "segment"), "segment")), OptionEnum(c, "direction", LaneDirection.Forward), checked((ushort)OptionInt(c, "order", 0)), OptionDouble(c, "width", 3.5d), OptionDouble(c, "speed", 13.8888888889d)), roadTopologyChanged: true);
    private LaneConnectionId CreateLaneConnection(AdminCommand c, int start) => simulation.Mutate(w => w.CreateLaneConnection(new LaneId(Id(Arg(c, start, "fromLane"), "fromLane")), new LaneId(Id(Arg(c, start + 1, "toLane"), "toLane")), new RoadNodeId(Id(Arg(c, start + 2, "viaNode"), "viaNode")), OptionEnum(c, "movement", TurnMovement.Unspecified)), roadTopologyChanged: true);
    private bool UpdateLaneConnection(AdminCommand c, LaneConnectionId id) => simulation.Mutate(w => w.UpdateLaneConnection(id, new LaneId(Id(Arg(c, 3, "fromLane"), "fromLane")), new LaneId(Id(Arg(c, 4, "toLane"), "toLane")), new RoadNodeId(Id(Arg(c, 5, "viaNode"), "viaNode")), OptionEnum(c, "movement", TurnMovement.Unspecified)), roadTopologyChanged: true);
    private RoadAccessPointId CreateRoadAccess(AdminCommand c, int start) => simulation.Mutate(w => w.CreateRoadAccessPoint(new RoadSegmentId(Id(Arg(c, start, "segment"), "segment")), Double(Arg(c, start + 1, "offset"), "offset"), OptionId(c, "building") is { } b ? new BuildingId(b) : null, OptionId(c, "poi") is { } p ? new PoiId(p) : null, OptionEnum(c, "mode", RoadAccessMode.Motor)), roadTopologyChanged: true);
    private bool UpdateRoadAccess(AdminCommand c, RoadAccessPointId id) => simulation.Mutate(w => w.UpdateRoadAccessPoint(id, new RoadSegmentId(Id(Arg(c, 3, "segment"), "segment")), Double(Arg(c, 4, "offset"), "offset"), OptionId(c, "building") is { } b ? new BuildingId(b) : null, OptionId(c, "poi") is { } p ? new PoiId(p) : null, OptionEnum(c, "mode", RoadAccessMode.Motor)), roadTopologyChanged: true);

    private static AdminCommandResult RoadList(string entity, RoadNetworkSnapshot s) => entity switch
    {
        "node" => ListResult(s.Nodes.Select(x => $"{x.Id.Value} {x.Kind} {P(x.Position)}"), "No road nodes."),
        "segment" => ListResult(s.Segments.Select(x => $"{x.Id.Value} {x.Kind} {x.StartNodeId.Value}->{x.EndNodeId.Value}"), "No road segments."),
        "lane" => ListResult(s.Lanes.Select(x => FormattableString.Invariant($"{x.Id.Value} segment={x.SegmentId.Value} {x.Direction} order={x.Order} width={x.WidthMeters} speed={x.SpeedLimitMetersPerSecond}")), "No lanes."),
        "connection" => ListResult(s.Connections.Select(x => $"{x.Id.Value} {x.FromLaneId.Value}->{x.ToLaneId.Value} via={x.ViaNodeId.Value} movement={x.Movement}"), "No lane connections."),
        "access" => ListResult(s.AccessPoints.Select(x => FormattableString.Invariant($"{x.Id.Value} segment={x.SegmentId.Value} offset={x.SegmentOffset} mode={x.Mode} building={x.BuildingId?.Value} poi={x.PoiId?.Value}")), "No road access points."),
        _ => new(AdminCommandResultCode.InvalidArgument, $"Unknown road entity '{entity}'."),
    };

    private static AdminCommandResult RoadShow(string entity, RoadNetworkSnapshot s, ulong id) => entity switch
    {
        "node" => One(s.Nodes.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} {x.Kind} {P(x.Position)}"), "Road node", id),
        "segment" => One(s.Segments.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} {x.Kind} {x.StartNodeId.Value}->{x.EndNodeId.Value}"), "Road segment", id),
        "lane" => One(s.Lanes.Where(x => x.Id.Value == id).Select(x => FormattableString.Invariant($"{x.Id.Value} segment={x.SegmentId.Value} {x.Direction} order={x.Order} width={x.WidthMeters} speed={x.SpeedLimitMetersPerSecond}")), "Lane", id),
        "connection" => One(s.Connections.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} {x.FromLaneId.Value}->{x.ToLaneId.Value} via={x.ViaNodeId.Value} movement={x.Movement}"), "Lane connection", id),
        "access" => One(s.AccessPoints.Where(x => x.Id.Value == id).Select(x => FormattableString.Invariant($"{x.Id.Value} segment={x.SegmentId.Value} offset={x.SegmentOffset} mode={x.Mode} building={x.BuildingId?.Value} poi={x.PoiId?.Value}")), "Road access point", id),
        _ => new(AdminCommandResultCode.InvalidArgument, $"Unknown road entity '{entity}'."),
    };

    private static AdminCommandResult RailwayList(string entity, RailwayInfrastructureSnapshot s) => entity switch
    {
        "node" => ListResult(s.Nodes.Select(x => $"{x.Id.Value} {x.Kind} {P(x.Position)}"), "No track nodes."),
        "segment" => ListResult(s.Segments.Select(FormatTrackSegment), "No track segments."),
        "connection" => ListResult(s.Connections.Select(x => $"{x.Id.Value} {x.FromSegmentId.Value}->{x.ToSegmentId.Value} via={x.ViaNodeId.Value}"), "No track connections."),
        "block" => ListResult(s.Blocks.Select(x => $"{x.Id.Value} segments={string.Join(',', x.SegmentIds.Select(y => y.Value))}"), "No block sections."),
        "station" => ListResult(s.Stations.Select(x => $"{x.Id.Value} bounds={V(x.Bounds)}"), "No stations."),
        "platform" => ListResult(s.Platforms.Select(FormatPlatform), "No platforms."),
        "access" => ListResult(s.PlatformAccessPoints.Select(x => $"{x.Id.Value} platform={x.PlatformId.Value} roadAccess={x.RoadAccessPointId.Value}"), "No platform access points."),
        "depot" => ListResult(s.Depots.Select(x => $"{x.Id.Value} bounds={V(x.Bounds)} segments={string.Join(',', x.TrackSegmentIds.Select(y => y.Value))}"), "No depots."),
        _ => new(AdminCommandResultCode.InvalidArgument, $"Unknown railway entity '{entity}'."),
    };

    private static AdminCommandResult RailwayShow(string entity, RailwayInfrastructureSnapshot s, ulong id) => entity switch
    {
        "node" => One(s.Nodes.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} {x.Kind} {P(x.Position)}"), "Track node", id),
        "segment" => One(s.Segments.Where(x => x.Id.Value == id).Select(FormatTrackSegment), "Track segment", id),
        "connection" => One(s.Connections.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} {x.FromSegmentId.Value}->{x.ToSegmentId.Value} via={x.ViaNodeId.Value}"), "Track connection", id),
        "block" => One(s.Blocks.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} segments={string.Join(',', x.SegmentIds.Select(y => y.Value))}"), "Block section", id),
        "station" => One(s.Stations.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} bounds={V(x.Bounds)}"), "Station", id),
        "platform" => One(s.Platforms.Where(x => x.Id.Value == id).Select(FormatPlatform), "Platform", id),
        "access" => One(s.PlatformAccessPoints.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} platform={x.PlatformId.Value} roadAccess={x.RoadAccessPointId.Value}"), "Platform access point", id),
        "depot" => One(s.Depots.Where(x => x.Id.Value == id).Select(x => $"{x.Id.Value} bounds={V(x.Bounds)} segments={string.Join(',', x.TrackSegmentIds.Select(y => y.Value))}"), "Depot", id),
        _ => new(AdminCommandResultCode.InvalidArgument, $"Unknown railway entity '{entity}'."),
    };

    private static TimetableStopSnapshot[] ParseStops(string text) => text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(value =>
    {
        var p = value.Split(':', StringSplitOptions.TrimEntries);
        if (p.Length is < 4 or > 5) throw new FormatException("Each timetable stop must be station:arrival:departure:dwell[:platform].");
        PlatformId? platform = p.Length == 5 ? new PlatformId(Id(p[4], "platform")) : null;
        return new TimetableStopSnapshot(new StationId(Id(p[0], "station")), UInt64(p[1], "arrival", true), UInt64(p[2], "departure", true), UInt64(p[3], "dwell", true), platform);
    }).ToArray();

    private static AdminCommandResult MapException(Exception e) => e switch
    {
        FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or IOException => new(AdminCommandResultCode.IoError, e.Message),
        FormatException or ArgumentException or OverflowException => new(AdminCommandResultCode.InvalidArgument, e.Message),
        InvalidOperationException => new(AdminCommandResultCode.Conflict, e.Message),
        _ => new(AdminCommandResultCode.InternalError, "The command failed unexpectedly. See server logs for details."),
    };

    private static AdminCommandResult Syntax(string s) => new(AdminCommandResultCode.InvalidSyntax, $"Usage: {s}");
    private static AdminCommandResult InvalidAction(string command, string action) => new(AdminCommandResultCode.InvalidArgument, $"Unknown {command} action '{action}'.");
    private static AdminCommandResult NotFound(string type, ulong id) => new(AdminCommandResultCode.NotFound, $"{type} {id} was not found.");
    private static AdminCommandResult ListResult(IEnumerable<string> items, string empty) { var a = items.ToArray(); return AdminCommandResult.Ok(a.Length == 0 ? empty : string.Join(Environment.NewLine, a)); }
    private static AdminCommandResult One(IEnumerable<string> items, string type, ulong id) { var value = items.FirstOrDefault(); return value is null ? NotFound(type, id) : AdminCommandResult.Ok(value); }
    private static string Action(AdminCommand c, string name) => c.Arguments.Count == 0 ? throw new FormatException($"Missing {name} action. Run 'help {name}'.") : c.Arguments[0];
    private static string Arg(AdminCommand c, int index, string name) => c.Arguments.Count > index ? c.Arguments[index] : throw new FormatException($"Missing argument '{name}'.");
    private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static ulong Id(string value, string name) => UInt64(value, name, false);
    private static ulong UInt64(string value, string name, bool allowZero) => ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var x) && (allowZero || x > 0) ? x : throw new FormatException($"'{name}' must be {(allowZero ? "an unsigned" : "a positive unsigned")} 64-bit integer.");
    private static int PositiveInt(string value, string name) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) && x > 0 ? x : throw new FormatException($"'{name}' must be a positive integer.");
    private static double Double(string value, string name) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) && double.IsFinite(x) ? x : throw new FormatException($"'{name}' must be a finite invariant-culture number.");
    private static Guid GuidValue(string value) => Guid.TryParse(value, out var x) ? x : throw new FormatException("Connection id must be a GUID.");
    private static ulong[] CsvIds(string value) { var result = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(x => Id(x, "id")).ToArray(); if (result.Length == 0) throw new FormatException("At least one ID is required."); return result; }
    private static int OptionInt(AdminCommand c, string name, int d) => c.Options.TryGetValue(name, out var v) ? v is null ? throw new FormatException($"Option '--{name}' requires a value.") : int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : throw new FormatException($"Option '--{name}' must be an integer.") : d;
    private static double OptionDouble(AdminCommand c, string name, double d) => c.Options.TryGetValue(name, out var v) ? v is null ? throw new FormatException($"Option '--{name}' requires a value.") : Double(v, name) : d;
    private static ulong? OptionId(AdminCommand c, string name) => c.Options.TryGetValue(name, out var v) ? v is null ? throw new FormatException($"Option '--{name}' requires a value.") : Id(v, name) : null;
    private static T OptionEnum<T>(AdminCommand c, string name, T d) where T : struct, Enum => c.Options.TryGetValue(name, out var v) ? v is null ? throw new FormatException($"Option '--{name}' requires a value.") : Enum.TryParse<T>(v, true, out var x) && Enum.IsDefined(x) ? x : throw new FormatException($"Option '--{name}' has an invalid {typeof(T).Name} value.") : d;
    private static WorldPoint Point(AdminCommand c, int s) => new(Double(Arg(c, s, "x"), "x"), Double(Arg(c, s + 1, "y"), "y"), Double(Arg(c, s + 2, "z"), "z"));
    private static WorldVector Velocity(AdminCommand c) => new(OptionDouble(c, "vx", 0d), OptionDouble(c, "vy", 0d), OptionDouble(c, "vz", 0d));
    private static WorldVolume Volume(AdminCommand c, int s) => new(Double(Arg(c, s, "minX"), "minX"), Double(Arg(c, s + 1, "minY"), "minY"), Double(Arg(c, s + 2, "minZ"), "minZ"), Double(Arg(c, s + 3, "maxX"), "maxX"), Double(Arg(c, s + 4, "maxY"), "maxY"), Double(Arg(c, s + 5, "maxZ"), "maxZ"));
    private static string P(WorldPoint p) => FormattableString.Invariant($"{p.X},{p.Y},{p.Z}");
    private static string V(WorldVolume v) => FormattableString.Invariant($"{v.MinX},{v.MinY},{v.MinZ}..{v.MaxX},{v.MaxY},{v.MaxZ}");
    private static string FormatAgent(AgentSnapshot x) => FormattableString.Invariant($"{x.Id.Value} pos={P(x.Position)} vel={x.Velocity.X},{x.Velocity.Y},{x.Velocity.Z} tick={x.TickCount}");
    private static string FormatBuilding(BuildingSnapshot x) => $"{x.Id.Value} {x.Kind} bounds={V(x.Bounds)}";
    private static string FormatPoi(PoiSnapshot x) => $"{x.Id.Value} {x.Kind} pos={P(x.Position)} building={x.BuildingId?.Value}";
    private static string FormatVehicle(VehicleSnapshot x) => FormattableString.Invariant($"{x.Id.Value} lane={x.LaneId.Value} pos={P(x.Position)} speed={x.SpeedMetersPerSecond} state={x.State} tick={x.TickCount}");
    private static string FormatTrackSegment(TrackSegmentSnapshot x) => FormattableString.Invariant($"{x.Id.Value} {x.StartNodeId.Value}->{x.EndNodeId.Value} direction={x.Direction} gauge={x.GaugeMeters} speed={x.SpeedLimitMetersPerSecond} electrification={x.Electrification} usage={x.Usage}");
    private static string FormatPlatform(PlatformSnapshot x) => FormattableString.Invariant($"{x.Id.Value} station={x.StationId.Value} segment={x.TrackSegmentId.Value} offsets={x.StartSegmentOffset}..{x.EndSegmentOffset} bounds={V(x.Bounds)}");
    private static string FormatFormation(TrainFormationSnapshot x) => FormattableString.Invariant($"{x.Id.Value} length={x.LengthMeters} maxSpeed={x.MaximumSpeedMetersPerSecond} capacity={x.Capacity}");
    private static string FormatRailRoute(RailwayRouteSnapshot x) => FormattableString.Invariant($"{x.Id.Value} length={x.LengthMeters} segments={string.Join(',', x.TrackSegmentIds.Select(y => y.Value))}");
    private static string FormatTimetable(TimetableSnapshot x) => $"{x.Id.Value} stops={x.Stops.Count}";
    private static string FormatService(RailwayServiceSnapshot x) => $"{x.Id.Value} formation={x.FormationId.Value} route={x.RouteId.Value} timetable={x.TimetableId.Value} state={x.State} train={x.TrainId?.Value}";
    private static string FormatTrain(TrainSnapshot x) => FormattableString.Invariant($"{x.Id.Value} service={x.ServiceId.Value} route={x.RouteId.Value} pos={P(x.Position)} speed={x.SpeedMetersPerSecond} state={x.State} tick={x.TickCount}");
}
