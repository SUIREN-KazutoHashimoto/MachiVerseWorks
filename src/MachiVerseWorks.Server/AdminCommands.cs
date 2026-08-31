using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading.Channels;
using MachiVerseWorks.Persistence;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal enum AdminCommandResultCode
{
    Ok,
    InvalidSyntax,
    UnknownCommand,
    InvalidArgument,
    NotFound,
    Conflict,
    InvalidState,
    QueueFull,
    IoError,
    InternalError,
}

internal sealed record AdminCommand(string Name, IReadOnlyList<string> Arguments, IReadOnlyDictionary<string, string?> Options, string RawText);
internal sealed record AdminCommandResult(AdminCommandResultCode Code, string Message)
{
    public bool Success => Code == AdminCommandResultCode.Ok;
    public override string ToString() => $"{Code.ToString().ToLowerInvariant()}: {Message}";
    public static AdminCommandResult Ok(string message) => new(AdminCommandResultCode.Ok, message);
}

internal static class AdminCommandParser
{
    public static bool TryParse(string? input, out AdminCommand? command, out AdminCommandResult? error)
    {
        command = null; error = null;
        if (string.IsNullOrWhiteSpace(input)) { error = new(AdminCommandResultCode.InvalidSyntax, "Command is empty."); return false; }
        if (!TryTokenize(input, out var tokens, out var tokenError)) { error = new(AdminCommandResultCode.InvalidSyntax, tokenError); return false; }
        if (tokens.Count == 0) { error = new(AdminCommandResultCode.InvalidSyntax, "Command is empty."); return false; }
        var arguments = new List<string>(); var options = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens.Skip(1))
        {
            if (!token.StartsWith("--", StringComparison.Ordinal)) { arguments.Add(token); continue; }
            var option = token[2..]; if (option.Length == 0) { error = new(AdminCommandResultCode.InvalidSyntax, "Option name is empty."); return false; }
            var separator = option.IndexOf('=');
            var name = separator < 0 ? option : option[..separator]; var value = separator < 0 ? null : option[(separator + 1)..];
            if (!options.TryAdd(name, value)) { error = new(AdminCommandResultCode.InvalidSyntax, $"Duplicate option '--{name}'."); return false; }
        }
        command = new AdminCommand(tokens[0].ToLowerInvariant(), arguments, options, input); return true;
    }

    private static bool TryTokenize(string input, out List<string> tokens, out string error)
    {
        tokens = []; error = string.Empty; var current = new StringBuilder(); var quoted = false; var escaping = false;
        foreach (var ch in input)
        {
            if (escaping) { current.Append(ch); escaping = false; continue; }
            if (ch == '\\' && quoted) { escaping = true; continue; }
            if (ch == '"') { quoted = !quoted; continue; }
            if (char.IsWhiteSpace(ch) && !quoted) { if (current.Length > 0) { tokens.Add(current.ToString()); current.Clear(); } continue; }
            current.Append(ch);
        }
        if (escaping) { error = "Quoted token ends with an incomplete escape sequence."; return false; }
        if (quoted) { error = "Quoted token is not terminated."; return false; }
        if (current.Length > 0) tokens.Add(current.ToString()); return true;
    }
}

internal sealed record AdminCommandRequest(AdminCommand Command, TaskCompletionSource<AdminCommandResult> Completion);

internal sealed class AdminCommandQueue
{
    public const int Capacity = 256;
    private readonly Channel<AdminCommandRequest> _channel = Channel.CreateBounded<AdminCommandRequest>(new BoundedChannelOptions(Capacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
    public bool TryWrite(AdminCommandRequest request) { ArgumentNullException.ThrowIfNull(request); return _channel.Writer.TryWrite(request); }
    public IAsyncEnumerable<AdminCommandRequest> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class AdminCommandExecutor(
    AdminCommandQueue queue,
    SimulationRuntime simulation,
    ClientConnectionRegistry connections,
    IHostApplicationLifetime lifetime,
    ILogger<AdminCommandExecutor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var request in queue.ReadAllAsync(stoppingToken))
            {
                try { request.Completion.TrySetResult(await ExecuteCoreAsync(request.Command, stoppingToken)); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { request.Completion.TrySetCanceled(stoppingToken); }
                catch (Exception exception) { logger.LogError(exception, "Admin command failed: {Command}", request.Command.RawText); request.Completion.TrySetResult(MapException(exception)); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task<AdminCommandResult> ExecuteCoreAsync(AdminCommand command, CancellationToken cancellationToken)
    {
        return command.Name switch
        {
            "help" => Help(command),
            "status" => Status(),
            "version" => Version(),
            "exit" or "stop" => StopServer(),
            "simulation" or "sim" => Simulation(command),
            "agent" => Agent(command),
            "building" => Building(command),
            "poi" => Poi(command),
            "road" => Road(command),
            "connection" or "connections" => Connection(command),
            "world" => await WorldAsync(command, cancellationToken),
            _ => new(AdminCommandResultCode.UnknownCommand, $"Unknown command '{command.Name}'. Run 'help' for supported commands."),
        };
    }

    private static AdminCommandResult Help(AdminCommand command)
    {
        var text = "Commands: help, status, version, exit, simulation status|pause|resume|step [count], agent list|show|add|remove, building list|show|add|remove, poi list|show|add|remove, road node|segment|lane|connection|access list|show|add|remove, connection list|show|disconnect, world save|load <path>. Numeric values use invariant culture; use quoted tokens for paths containing spaces.";
        return AdminCommandResult.Ok(text);
    }

    private AdminCommandResult Status() => AdminCommandResult.Ok(FormattableString.Invariant($"tick={simulation.TickCount} paused={simulation.IsPaused.ToString().ToLowerInvariant()} agents={simulation.ActiveAgentCount} vehicles={simulation.ActiveVehicleCount} roadSegments={simulation.RoadSegmentCount} trackSegments={simulation.TrackSegmentCount} connections={connections.Count}"));
    private static AdminCommandResult Version() => AdminCommandResult.Ok(Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown");
    private AdminCommandResult StopServer() { lifetime.StopApplication(); return AdminCommandResult.Ok("Server shutdown requested."); }

    private AdminCommandResult Simulation(AdminCommand command)
    {
        if (command.Arguments.Count == 0 || Eq(command.Arguments[0], "status")) return AdminCommandResult.Ok(FormattableString.Invariant($"tick={simulation.TickCount} paused={simulation.IsPaused.ToString().ToLowerInvariant()} tickRate={simulation.TickRate}"));
        if (Eq(command.Arguments[0], "pause")) return simulation.Pause() ? AdminCommandResult.Ok("Simulation paused.") : new(AdminCommandResultCode.InvalidState, "Simulation is already paused.");
        if (Eq(command.Arguments[0], "resume")) return simulation.Resume() ? AdminCommandResult.Ok("Simulation resumed.") : new(AdminCommandResultCode.InvalidState, "Simulation is not paused.");
        if (Eq(command.Arguments[0], "step"))
        {
            var count = command.Arguments.Count > 1 ? ParseInt(command.Arguments[1], "count") : 1;
            var tick = simulation.StepPaused(count); return AdminCommandResult.Ok(FormattableString.Invariant($"Advanced {count} tick(s); tick={tick}."));
        }
        return UnknownSubcommand("simulation", command);
    }

    private AdminCommandResult Agent(AdminCommand command)
    {
        var action = RequiredAction(command, "agent");
        if (action is null) return Syntax("agent list|show <id>|add <x> <y> <z> [--vx=n --vy=n --vz=n]|remove <id>");
        if (Eq(action, "list"))
        {
            var items = simulation.Read(static world => world.CreateAllAgentSnapshots());
            return AdminCommandResult.Ok(items.Length == 0 ? "No agents." : string.Join(Environment.NewLine, items.OrderBy(x => x.Id.Value).Select(FormatAgent)));
        }
        if (Eq(action, "show"))
        {
            var id = new AgentId(ParseId(Arg(command, 1, "id"), "id"));
            var item = simulation.Read(world => world.TryGetAgentSnapshot(id, out var snapshot) ? snapshot : (AgentSnapshot?)null);
            return item is { } value ? AdminCommandResult.Ok(FormatAgent(value)) : NotFound("Agent", id.Value);
        }
        if (Eq(action, "add"))
        {
            var position = Point(command, 1); var velocity = new WorldVector(OptionDouble(command, "vx", 0d), OptionDouble(command, "vy", 0d), OptionDouble(command, "vz", 0d));
            var id = simulation.Mutate(world => world.CreateAgent(position, velocity)); return AdminCommandResult.Ok($"Agent {id.Value} created.");
        }
        if (Eq(action, "remove"))
        {
            var id = new AgentId(ParseId(Arg(command, 1, "id"), "id")); return simulation.Mutate(world => world.RemoveAgent(id)) ? AdminCommandResult.Ok($"Agent {id.Value} removed.") : NotFound("Agent", id.Value);
        }
        return UnknownSubcommand("agent", command);
    }

    private AdminCommandResult Building(AdminCommand command)
    {
        var action = RequiredAction(command, "building"); if (action is null) return Syntax("building list|show <id>|add <minX> <minY> <minZ> <maxX> <maxY> <maxZ> [--kind=Generic]|remove <id>");
        if (Eq(action, "list")) { var items = simulation.Read(static world => world.CreateBuildingSnapshot()); return AdminCommandResult.Ok(items.Length == 0 ? "No buildings." : string.Join(Environment.NewLine, items.Select(FormatBuilding))); }
        if (Eq(action, "show")) { var id = new BuildingId(ParseId(Arg(command, 1, "id"), "id")); var item = simulation.Read(world => world.TryGetBuildingSnapshot(id, out var x) ? x : (BuildingSnapshot?)null); return item is { } value ? AdminCommandResult.Ok(FormatBuilding(value)) : NotFound("Building", id.Value); }
        if (Eq(action, "add")) { var bounds = Volume(command, 1); var kind = OptionEnum(command, "kind", BuildingKind.Generic); var id = simulation.Mutate(world => world.CreateBuilding(bounds, kind)); return AdminCommandResult.Ok($"Building {id.Value} created."); }
        if (Eq(action, "remove")) { var id = new BuildingId(ParseId(Arg(command, 1, "id"), "id")); return simulation.Mutate(world => world.RemoveBuilding(id)) ? AdminCommandResult.Ok($"Building {id.Value} removed.") : NotFound("Building", id.Value); }
        return UnknownSubcommand("building", command);
    }

    private AdminCommandResult Poi(AdminCommand command)
    {
        var action = RequiredAction(command, "poi"); if (action is null) return Syntax("poi list|show <id>|add <x> <y> <z> [--kind=Generic] [--building=id]|remove <id>");
        if (Eq(action, "list")) { var items = simulation.Read(static world => world.CreatePoiSnapshot()); return AdminCommandResult.Ok(items.Length == 0 ? "No POIs." : string.Join(Environment.NewLine, items.Select(FormatPoi))); }
        if (Eq(action, "show")) { var id = new PoiId(ParseId(Arg(command, 1, "id"), "id")); var item = simulation.Read(world => world.TryGetPoiSnapshot(id, out var x) ? x : (PoiSnapshot?)null); return item is { } value ? AdminCommandResult.Ok(FormatPoi(value)) : NotFound("POI", id.Value); }
        if (Eq(action, "add")) { var position = Point(command, 1); var kind = OptionEnum(command, "kind", PoiKind.Generic); BuildingId? building = command.Options.TryGetValue("building", out var value) && value is not null ? new BuildingId(ParseId(value, "building")) : null; var id = simulation.Mutate(world => world.CreatePoi(position, kind, building)); return AdminCommandResult.Ok($"POI {id.Value} created."); }
        if (Eq(action, "remove")) { var id = new PoiId(ParseId(Arg(command, 1, "id"), "id")); return simulation.Mutate(world => world.RemovePoi(id)) ? AdminCommandResult.Ok($"POI {id.Value} removed.") : NotFound("POI", id.Value); }
        return UnknownSubcommand("poi", command);
    }

    private AdminCommandResult Road(AdminCommand command)
    {
        if (command.Arguments.Count < 2) return Syntax("road node|segment|lane|connection|access list|show|add|remove ...");
        var entity = command.Arguments[0].ToLowerInvariant(); var action = command.Arguments[1].ToLowerInvariant();
        var snapshot = simulation.Read(static world => world.CreateRoadNetworkSnapshot());
        if (action == "list") return entity switch
        {
            "node" => AdminCommandResult.Ok(string.Join(Environment.NewLine, snapshot.Nodes.OrderBy(x => x.Id.Value).Select(x => FormattableString.Invariant($"{x.Id.Value} {x.Kind} {x.Position.X},{x.Position.Y},{x.Position.Z}")))),
            "segment" => AdminCommandResult.Ok(string.Join(Environment.NewLine, snapshot.Segments.OrderBy(x => x.Id.Value).Select(x => $"{x.Id.Value} {x.Kind} {x.StartNodeId.Value}->{x.EndNodeId.Value}"))),
            "lane" => AdminCommandResult.Ok(string.Join(Environment.NewLine, snapshot.Lanes.OrderBy(x => x.Id.Value).Select(x => FormattableString.Invariant($"{x.Id.Value} segment={x.SegmentId.Value} {x.Direction} order={x.Order} width={x.WidthMeters} speed={x.SpeedLimitMetersPerSecond}")))),
            "connection" => AdminCommandResult.Ok(string.Join(Environment.NewLine, snapshot.Connections.OrderBy(x => x.Id.Value).Select(x => $"{x.Id.Value} {x.FromLaneId.Value}->{x.ToLaneId.Value} via={x.ViaNodeId.Value} {x.Movement}"))),
            "access" => AdminCommandResult.Ok(string.Join(Environment.NewLine, snapshot.AccessPoints.OrderBy(x => x.Id.Value).Select(x => FormattableString.Invariant($"{x.Id.Value} segment={x.SegmentId.Value} offset={x.SegmentOffset} mode={x.Mode} building={x.BuildingId?.Value} poi={x.PoiId?.Value}")))),
            _ => new(AdminCommandResultCode.InvalidArgument, $"Unknown road entity '{entity}'."),
        };
        if (entity == "node" && action == "add") { var p = Point(command, 2); var kind = OptionEnum(command, "kind", RoadNodeKind.Endpoint); var id = simulation.Mutate(world => world.CreateRoadNode(p, kind), roadTopologyChanged: true); return AdminCommandResult.Ok($"Road node {id.Value} created."); }
        if (entity == "node" && action == "remove") { var id = new RoadNodeId(ParseId(Arg(command, 2, "id"), "id")); return simulation.Mutate(world => world.RemoveRoadNode(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road node {id.Value} removed.") : NotFound("Road node", id.Value); }
        if (entity == "segment" && action == "add") { var start = new RoadNodeId(ParseId(Arg(command, 2, "startNodeId"), "startNodeId")); var end = new RoadNodeId(ParseId(Arg(command, 3, "endNodeId"), "endNodeId")); var kind = OptionEnum(command, "kind", RoadKind.Local); var id = simulation.Mutate(world => world.CreateRoadSegment(start, end, kind), roadTopologyChanged: true); return AdminCommandResult.Ok($"Road segment {id.Value} created."); }
        if (entity == "segment" && action == "remove") { var id = new RoadSegmentId(ParseId(Arg(command, 2, "id"), "id")); return simulation.Mutate(world => world.RemoveRoadSegment(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Road segment {id.Value} removed.") : NotFound("Road segment", id.Value); }
        if (entity == "lane" && action == "add") { var segment = new RoadSegmentId(ParseId(Arg(command, 2, "segmentId"), "segmentId")); var direction = OptionEnum(command, "direction", LaneDirection.Forward); var order = checked((ushort)OptionInt(command, "order", 0)); var width = OptionDouble(command, "width", 3.5d); var speed = OptionDouble(command, "speed", 13.8888888889d); var id = simulation.Mutate(world => world.CreateLane(segment, direction, order, width, speed), roadTopologyChanged: true); return AdminCommandResult.Ok($"Lane {id.Value} created."); }
        if (entity == "lane" && action == "remove") { var id = new LaneId(ParseId(Arg(command, 2, "id"), "id")); return simulation.Mutate(world => world.RemoveLane(id), roadTopologyChanged: true) ? AdminCommandResult.Ok($"Lane {id.Value} removed.") : NotFound("Lane", id.Value); }
        return new(AdminCommandResultCode.InvalidArgument, $"Unsupported road operation '{entity} {action}'. Run 'help'.");
    }

    private AdminCommandResult Connection(AdminCommand command)
    {
        var action = RequiredAction(command, "connection"); if (action is null || Eq(action, "list")) return AdminCommandResult.Ok(string.Join(Environment.NewLine, connections.CreateSnapshot().OrderBy(x => x.Id).Select(x => $"{x.Id} state={x.Socket.State} protocol={x.NegotiatedVersion}")));
        if (Eq(action, "show")) { var id = ParseGuid(Arg(command, 1, "id")); return connections.TryGet(id, out var item) && item is not null ? AdminCommandResult.Ok($"{item.Id} state={item.Socket.State} handshake={item.HandshakeCompleted} protocol={item.NegotiatedVersion}") : new(AdminCommandResultCode.NotFound, $"Connection {id} was not found."); }
        if (Eq(action, "disconnect")) { var id = ParseGuid(Arg(command, 1, "id")); if (!connections.TryGet(id, out var item) || item is null) return new(AdminCommandResultCode.NotFound, $"Connection {id} was not found."); item.Abort(); connections.Remove(id); return AdminCommandResult.Ok($"Connection {id} disconnected."); }
        return UnknownSubcommand("connection", command);
    }

    private async Task<AdminCommandResult> WorldAsync(AdminCommand command, CancellationToken cancellationToken)
    {
        var action = RequiredAction(command, "world"); if (action is null) return Syntax("world save|load <path>");
        var path = Path.GetFullPath(Arg(command, 1, "path"));
        if (Eq(action, "save"))
        {
            var checkpoint = simulation.CaptureCheckpoint();
            var detachedWorld = SimulationWorld.RestoreCheckpoint(checkpoint);
            var data = WorldSaveSerializer.Serialize(detachedWorld);
            await File.WriteAllBytesAsync(path, data, cancellationToken);
            return AdminCommandResult.Ok($"World saved to '{path}'.");
        }
        if (Eq(action, "load"))
        {
            var data = await File.ReadAllBytesAsync(path, cancellationToken);
            var world = WorldSaveSerializer.Deserialize(data);
            simulation.ReplaceWorld(world);
            return AdminCommandResult.Ok($"World loaded from '{path}'.");
        }
        return UnknownSubcommand("world", command);
    }

    private static AdminCommandResult MapException(Exception exception) => exception switch
    {
        FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or IOException => new(AdminCommandResultCode.IoError, exception.Message),
        FormatException or ArgumentException or OverflowException => new(AdminCommandResultCode.InvalidArgument, exception.Message),
        InvalidOperationException => new(AdminCommandResultCode.Conflict, exception.Message),
        _ => new(AdminCommandResultCode.InternalError, "The command failed unexpectedly. See server logs for details."),
    };
    private static AdminCommandResult Syntax(string syntax) => new(AdminCommandResultCode.InvalidSyntax, $"Usage: {syntax}");
    private static AdminCommandResult NotFound(string name, ulong id) => new(AdminCommandResultCode.NotFound, $"{name} {id} was not found.");
    private static AdminCommandResult UnknownSubcommand(string name, AdminCommand command) => new(AdminCommandResultCode.InvalidArgument, $"Unknown {name} subcommand '{(command.Arguments.Count == 0 ? string.Empty : command.Arguments[0])}'.");
    private static string? RequiredAction(AdminCommand command, string _) => command.Arguments.Count == 0 ? null : command.Arguments[0];
    private static string Arg(AdminCommand command, int index, string name) => command.Arguments.Count > index ? command.Arguments[index] : throw new FormatException($"Missing argument '{name}'.");
    private static bool Eq(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static ulong ParseId(string text, string name) => ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : throw new FormatException($"'{name}' must be a positive unsigned 64-bit integer.");
    private static int ParseInt(string text, string name) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0 ? value : throw new FormatException($"'{name}' must be a positive integer.");
    private static Guid ParseGuid(string text) => Guid.TryParse(text, out var value) ? value : throw new FormatException("Connection id must be a GUID.");
    private static double ParseDouble(string text, string name) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : throw new FormatException($"'{name}' must be a finite invariant-culture number.");
    private static int OptionInt(AdminCommand command, string name, int defaultValue) => command.Options.TryGetValue(name, out var value) ? value is null ? throw new FormatException($"Option '--{name}' requires a value.") : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : throw new FormatException($"Option '--{name}' must be an integer.") : defaultValue;
    private static double OptionDouble(AdminCommand command, string name, double defaultValue) => command.Options.TryGetValue(name, out var value) ? value is null ? throw new FormatException($"Option '--{name}' requires a value.") : ParseDouble(value, name) : defaultValue;
    private static T OptionEnum<T>(AdminCommand command, string name, T defaultValue) where T : struct, Enum => command.Options.TryGetValue(name, out var value) ? value is null ? throw new FormatException($"Option '--{name}' requires a value.") : Enum.TryParse<T>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : throw new FormatException($"Option '--{name}' has an invalid {typeof(T).Name} value.") : defaultValue;
    private static WorldPoint Point(AdminCommand command, int start) => new(ParseDouble(Arg(command, start, "x"), "x"), ParseDouble(Arg(command, start + 1, "y"), "y"), ParseDouble(Arg(command, start + 2, "z"), "z"));
    private static WorldVolume Volume(AdminCommand command, int start) => new(ParseDouble(Arg(command, start, "minX"), "minX"), ParseDouble(Arg(command, start + 1, "minY"), "minY"), ParseDouble(Arg(command, start + 2, "minZ"), "minZ"), ParseDouble(Arg(command, start + 3, "maxX"), "maxX"), ParseDouble(Arg(command, start + 4, "maxY"), "maxY"), ParseDouble(Arg(command, start + 5, "maxZ"), "maxZ"));
    private static string FormatAgent(AgentSnapshot x) => FormattableString.Invariant($"{x.Id.Value} pos={x.Position.X},{x.Position.Y},{x.Position.Z} vel={x.Velocity.X},{x.Velocity.Y},{x.Velocity.Z} tick={x.TickCount}");
    private static string FormatBuilding(BuildingSnapshot x) => FormattableString.Invariant($"{x.Id.Value} {x.Kind} bounds={x.Bounds.MinX},{x.Bounds.MinY},{x.Bounds.MinZ}..{x.Bounds.MaxX},{x.Bounds.MaxY},{x.Bounds.MaxZ}");
    private static string FormatPoi(PoiSnapshot x) => FormattableString.Invariant($"{x.Id.Value} {x.Kind} pos={x.Position.X},{x.Position.Y},{x.Position.Z} building={x.BuildingId?.Value}");
}

internal sealed class ServerConsoleService(AdminCommandQueue queue, ServerOptions options, ILogger<ServerConsoleService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.ConsoleEnabled) return;
        logger.LogInformation("Server administration console enabled. Type 'help' for commands.");
        while (!stoppingToken.IsCancellationRequested)
        {
            string? line;
            try { line = await Console.In.ReadLineAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            if (line is null) { logger.LogInformation("Server administration console reached EOF."); break; }
            if (!AdminCommandParser.TryParse(line, out var command, out var error)) { await Console.Out.WriteLineAsync(error!.ToString()); continue; }
            var completion = new TaskCompletionSource<AdminCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!queue.TryWrite(new AdminCommandRequest(command!, completion))) { await Console.Out.WriteLineAsync(new AdminCommandResult(AdminCommandResultCode.QueueFull, "Admin command queue is full.").ToString()); continue; }
            try { await Console.Out.WriteLineAsync((await completion.Task.WaitAsync(stoppingToken)).ToString()); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
