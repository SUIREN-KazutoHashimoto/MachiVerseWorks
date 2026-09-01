using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Server;

namespace MachiVerseWorks.Server;

internal static class RemoteMcpPolicies
{
    public const string Read = "McpRead";
    public const string Write = "McpWrite";
    public const string Destructive = "McpDestructive";
    public const string ScopeClaim = "mcp_scope";
}

internal sealed class RemoteMcpOptions
{
    private const int MinimumTokenLength = 32;
    private readonly byte[]? _readTokenHash;
    private readonly byte[]? _writeTokenHash;
    private readonly byte[]? _destructiveTokenHash;

    private RemoteMcpOptions(
        bool enabled,
        byte[]? readTokenHash,
        byte[]? writeTokenHash,
        byte[]? destructiveTokenHash,
        IReadOnlySet<string> allowedOrigins,
        int maxRequestBytes,
        int maxConcurrentRequests,
        int requestsPerMinute,
        TimeSpan requestTimeout,
        int maxResultBytes,
        int maxLogEntries,
        int maxQueryItems,
        string saveDirectory)
    {
        Enabled = enabled;
        _readTokenHash = readTokenHash;
        _writeTokenHash = writeTokenHash;
        _destructiveTokenHash = destructiveTokenHash;
        AllowedOrigins = allowedOrigins;
        MaxRequestBytes = maxRequestBytes;
        MaxConcurrentRequests = maxConcurrentRequests;
        RequestsPerMinute = requestsPerMinute;
        RequestTimeout = requestTimeout;
        MaxResultBytes = maxResultBytes;
        MaxLogEntries = maxLogEntries;
        MaxQueryItems = maxQueryItems;
        SaveDirectory = saveDirectory;
    }

    public bool Enabled { get; }
    public IReadOnlySet<string> AllowedOrigins { get; }
    public int MaxRequestBytes { get; }
    public int MaxConcurrentRequests { get; }
    public int RequestsPerMinute { get; }
    public TimeSpan RequestTimeout { get; }
    public int MaxResultBytes { get; }
    public int MaxLogEntries { get; }
    public int MaxQueryItems { get; }
    public string SaveDirectory { get; }

    public static RemoteMcpOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var enabled = ReadBoolean(configuration, "Server:Mcp:Enabled", false);
        var readToken = EmptyToNull(configuration["Server:Mcp:ReadToken"]);
        var writeToken = EmptyToNull(configuration["Server:Mcp:WriteToken"]);
        var destructiveToken = EmptyToNull(configuration["Server:Mcp:DestructiveToken"]);

        if (enabled)
        {
            if (readToken is null && writeToken is null && destructiveToken is null)
                throw new InvalidOperationException("Server:Mcp:Enabled=true requires at least one bearer token.");
            ValidateToken(readToken, "Server:Mcp:ReadToken");
            ValidateToken(writeToken, "Server:Mcp:WriteToken");
            ValidateToken(destructiveToken, "Server:Mcp:DestructiveToken");
            var tokens = new[] { readToken, writeToken, destructiveToken }.Where(static token => token is not null).ToArray();
            if (tokens.Distinct(StringComparer.Ordinal).Count() != tokens.Length)
                throw new InvalidOperationException("Server:Mcp bearer tokens must be distinct so scopes cannot be confused.");
        }

        var allowedOrigins = (configuration["Server:Mcp:AllowedOrigins"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeOrigin)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var maxRequestBytes = ReadInt32(configuration, "Server:Mcp:MaxRequestBytes", 262_144, 4_096, 4_194_304);
        var maxConcurrentRequests = ReadInt32(configuration, "Server:Mcp:MaxConcurrentRequests", 8, 1, 128);
        var requestsPerMinute = ReadInt32(configuration, "Server:Mcp:RequestsPerMinute", 120, 1, 10_000);
        var requestTimeoutMs = ReadInt32(configuration, "Server:Mcp:RequestTimeoutMilliseconds", 30_000, 1_000, 120_000);
        var maxResultBytes = ReadInt32(configuration, "Server:Mcp:MaxResultBytes", 65_536, 1_024, 1_048_576);
        var maxLogEntries = ReadInt32(configuration, "Server:Mcp:MaxLogEntries", 512, 16, 10_000);
        var maxQueryItems = ReadInt32(configuration, "Server:Mcp:MaxQueryItems", 200, 1, 5_000);
        var saveDirectory = Path.GetFullPath(configuration["Server:Mcp:SaveDirectory"] ?? Path.Combine("data", "mcp-saves"));

        return new RemoteMcpOptions(
            enabled,
            Hash(readToken),
            Hash(writeToken),
            Hash(destructiveToken),
            allowedOrigins,
            maxRequestBytes,
            maxConcurrentRequests,
            requestsPerMinute,
            TimeSpan.FromMilliseconds(requestTimeoutMs),
            maxResultBytes,
            maxLogEntries,
            maxQueryItems,
            saveDirectory);
    }

    public bool TryAuthenticate(string? authorization, out string credential, out string[] scopes)
    {
        credential = string.Empty;
        scopes = [];
        if (string.IsNullOrWhiteSpace(authorization) || !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var token = authorization[7..].Trim();
        if (token.Length == 0) return false;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        if (Matches(_destructiveTokenHash, hash))
        {
            credential = "destructive";
            scopes = ["read", "write", "destructive"];
            return true;
        }
        if (Matches(_writeTokenHash, hash))
        {
            credential = "write";
            scopes = ["read", "write"];
            return true;
        }
        if (Matches(_readTokenHash, hash))
        {
            credential = "read";
            scopes = ["read"];
            return true;
        }
        return false;
    }

    public bool IsOriginAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return true;
        try { return AllowedOrigins.Contains(NormalizeOrigin(origin)); }
        catch (InvalidOperationException) { return false; }
    }

    private static bool Matches(byte[]? expected, byte[] actual) => expected is not null && CryptographicOperations.FixedTimeEquals(expected, actual);
    private static byte[]? Hash(string? token) => token is null ? null : SHA256.HashData(Encoding.UTF8.GetBytes(token));
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateToken(string? token, string key)
    {
        if (token is not null && token.Length < MinimumTokenLength)
            throw new InvalidOperationException($"{key} must contain at least {MinimumTokenLength} characters when configured.");
    }

    private static string NormalizeOrigin(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException($"MCP origin '{value}' must be an absolute HTTP(S) origin without path, query, fragment, or user information.");
        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static int ReadInt32(IConfiguration configuration, string key, int defaultValue, int minimum, int maximum)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < minimum || value > maximum)
            throw new InvalidOperationException($"{key} must be between {minimum} and {maximum}.");
        return value;
    }

    private static bool ReadBoolean(IConfiguration configuration, string key, bool defaultValue)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!bool.TryParse(text, out var value)) throw new InvalidOperationException($"{key} must be true or false.");
        return value;
    }
}

internal sealed class RemoteMcpRequestGate(RemoteMcpOptions options)
{
    private readonly SemaphoreSlim _concurrency = new(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
    private readonly object _rateLock = new();
    private readonly Dictionary<string, (DateTimeOffset WindowStart, int Count)> _rates = new(StringComparer.Ordinal);

    public bool TryAcquire(string credential, out IDisposable? lease, out int statusCode)
    {
        lease = null;
        statusCode = StatusCodes.Status200OK;
        lock (_rateLock)
        {
            var now = DateTimeOffset.UtcNow;
            if (!_rates.TryGetValue(credential, out var rate) || now - rate.WindowStart >= TimeSpan.FromMinutes(1)) rate = (now, 0);
            if (rate.Count >= options.RequestsPerMinute)
            {
                _rates[credential] = rate;
                statusCode = StatusCodes.Status429TooManyRequests;
                return false;
            }
            _rates[credential] = (rate.WindowStart, rate.Count + 1);
        }
        if (!_concurrency.Wait(0))
        {
            statusCode = StatusCodes.Status503ServiceUnavailable;
            return false;
        }
        lease = new Lease(_concurrency);
        return true;
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) semaphore.Release();
        }
    }
}

internal sealed class RemoteMcpSecurityMiddleware(RequestDelegate next, RemoteMcpOptions options, RemoteMcpRequestGate gate)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/mcp", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var maxBodyFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBodyFeature is { IsReadOnly: false }) maxBodyFeature.MaxRequestBodySize = options.MaxRequestBytes;
        if (context.Request.ContentLength > options.MaxRequestBytes)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        if (!options.IsOriginAllowed(context.Request.Headers.Origin.ToString()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!options.TryAuthenticate(context.Request.Headers.Authorization.ToString(), out var credential, out var scopes))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            return;
        }
        if (!gate.TryAcquire(credential, out var lease, out var rejectedStatus))
        {
            context.Response.StatusCode = rejectedStatus;
            return;
        }

        using (lease)
        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
        {
            var originalCancellation = context.RequestAborted;
            timeout.CancelAfter(options.RequestTimeout);
            context.RequestAborted = timeout.Token;
            var claims = scopes.Select(scope => new Claim(RemoteMcpPolicies.ScopeClaim, scope)).Append(new Claim(ClaimTypes.Name, $"mcp:{credential}"));
            context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "McpBearer"));
            try
            {
                await next(context);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !originalCancellation.IsCancellationRequested)
            {
                if (!context.Response.HasStarted) context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            }
            finally
            {
                context.RequestAborted = originalCancellation;
            }
        }
    }
}

internal sealed record RemoteMcpLogEntry(DateTimeOffset Timestamp, string Level, string Category, int EventId, string Message);

internal sealed class RemoteMcpLogBuffer(int capacity) : ILoggerProvider
{
    private readonly ConcurrentQueue<RemoteMcpLogEntry> _entries = new();
    private int _count;

    public ILogger CreateLogger(string categoryName) => new BufferLogger(this, categoryName);
    public void Dispose() { }

    public IReadOnlyList<RemoteMcpLogEntry> Query(int limit, string? contains)
    {
        var normalized = string.IsNullOrWhiteSpace(contains) ? null : contains.Trim();
        return _entries.Reverse()
            .Where(entry => normalized is null || entry.Category.Contains(normalized, StringComparison.OrdinalIgnoreCase) || entry.Message.Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .Reverse()
            .ToArray();
    }

    private void Add(RemoteMcpLogEntry entry)
    {
        _entries.Enqueue(entry);
        var count = Interlocked.Increment(ref _count);
        while (count > capacity && _entries.TryDequeue(out _)) count = Interlocked.Decrement(ref _count);
    }

    private sealed class BufferLogger(RemoteMcpLogBuffer owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            var message = formatter(state, exception);
            if (exception is not null) message = $"{message} | {exception.GetType().Name}: {exception.Message}";
            owner.Add(new RemoteMcpLogEntry(DateTimeOffset.UtcNow, logLevel.ToString(), category, eventId.Id, message));
        }
    }
}

internal sealed record RemoteMcpResult(bool Success, string Code, string Message)
{
    public static RemoteMcpResult Rejected(string code, string message) => new(false, code, message);
}

internal sealed class RemoteMcpAdminGateway(AdminCommandQueue queue, RemoteMcpOptions options)
{
    public async Task<RemoteMcpResult> ExecuteAsync(string commandText, CancellationToken cancellationToken, int? lineLimit = null)
    {
        if (!AdminCommandParser.TryParse(commandText, out var command, out var parseError) || command is null)
            return FromAdmin(parseError ?? new AdminCommandResult(AdminCommandResultCode.InvalidSyntax, "Command could not be parsed."), lineLimit);
        var completion = new TaskCompletionSource<AdminCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!queue.TryWrite(new AdminCommandRequest(command, completion)))
            return new RemoteMcpResult(false, "queue_full", "Administration command queue is full.");
        var result = await completion.Task.WaitAsync(cancellationToken);
        return FromAdmin(result, lineLimit);
    }

    private RemoteMcpResult FromAdmin(AdminCommandResult result, int? lineLimit)
    {
        var message = result.Message;
        if (lineLimit is > 0)
        {
            var lines = message.Split('\n');
            if (lines.Length > lineLimit) message = string.Join('\n', lines.Take(lineLimit.Value)) + $"\n... truncated ({lines.Length - lineLimit.Value} more line(s))";
        }
        message = TruncateUtf8(message, options.MaxResultBytes);
        return new RemoteMcpResult(result.Success, ToCode(result.Code), message);
    }

    private static string ToCode(AdminCommandResultCode code) => code switch
    {
        AdminCommandResultCode.Ok => "ok",
        AdminCommandResultCode.InvalidSyntax => "invalid_syntax",
        AdminCommandResultCode.UnknownCommand => "unknown_command",
        AdminCommandResultCode.InvalidArgument => "invalid_argument",
        AdminCommandResultCode.NotFound => "not_found",
        AdminCommandResultCode.Conflict => "conflict",
        AdminCommandResultCode.InvalidState => "invalid_state",
        AdminCommandResultCode.QueueFull => "queue_full",
        AdminCommandResultCode.IoError => "io_error",
        _ => "internal_error",
    };

    private static string TruncateUtf8(string value, int maxBytes)
    {
        if (Encoding.UTF8.GetByteCount(value) <= maxBytes) return value;
        var suffix = "\n... truncated";
        var budget = Math.Max(0, maxBytes - Encoding.UTF8.GetByteCount(suffix));
        var builder = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (Encoding.UTF8.GetByteCount(builder.ToString()) + rune.Utf8SequenceLength > budget) break;
            builder.Append(rune.ToString());
        }
        return builder + suffix;
    }
}

[McpServerToolType]
[Authorize(Policy = RemoteMcpPolicies.Read)]
internal sealed class RemoteMcpTools
{
    private static readonly IReadOnlyDictionary<string, string> QueryEntities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["agent"] = "agent", ["building"] = "building", ["poi"] = "poi", ["vehicle"] = "vehicle",
        ["road.node"] = "road node", ["road.segment"] = "road segment", ["road.lane"] = "road lane", ["road.connection"] = "road connection", ["road.access"] = "road access",
        ["railway.node"] = "railway node", ["railway.segment"] = "railway segment", ["railway.connection"] = "railway connection", ["railway.block"] = "railway block", ["railway.station"] = "railway station", ["railway.platform"] = "railway platform", ["railway.access"] = "railway access", ["railway.depot"] = "railway depot",
        ["formation"] = "formation", ["railroute"] = "railroute", ["timetable"] = "timetable", ["service"] = "service", ["train"] = "train",
    };

    private static readonly IReadOnlySet<string> MutableEntities = new HashSet<string>(QueryEntities.Keys.Where(static key => key is not "vehicle"), StringComparer.OrdinalIgnoreCase);

    [McpServerTool(Name = "server_status", ReadOnly = true, Destructive = false, UseStructuredContent = true), Description("Read server and authoritative simulation status through the administration command boundary.")]
    public static Task<RemoteMcpResult> ServerStatus(RemoteMcpAdminGateway admin, CancellationToken cancellationToken) => admin.ExecuteAsync("status", cancellationToken);

    [McpServerTool(Name = "server_version", ReadOnly = true, Destructive = false, UseStructuredContent = true), Description("Read the running MachiVerseWorks server version through the administration command boundary.")]
    public static Task<RemoteMcpResult> ServerVersion(RemoteMcpAdminGateway admin, CancellationToken cancellationToken) => admin.ExecuteAsync("version", cancellationToken);

    [McpServerTool(Name = "simulation_status", ReadOnly = true, Destructive = false, UseStructuredContent = true), Description("Read simulation tick, pause state, and tick rate through the administration command boundary.")]
    public static Task<RemoteMcpResult> SimulationStatus(RemoteMcpAdminGateway admin, CancellationToken cancellationToken) => admin.ExecuteAsync("simulation status", cancellationToken);

    [McpServerTool(Name = "diagnostics_metrics", ReadOnly = true, Destructive = false, UseStructuredContent = true), Description("Read bounded end-to-end server metrics.")]
    public static RemoteMcpResult DiagnosticsMetrics(E2eMetrics metrics, RemoteMcpOptions options)
    {
        var json = JsonSerializer.Serialize(metrics.Capture());
        return new RemoteMcpResult(true, "ok", json.Length <= options.MaxResultBytes ? json : json[..options.MaxResultBytes]);
    }

    [McpServerTool(Name = "logs_query", ReadOnly = true, Destructive = false, UseStructuredContent = true), Description("Query the bounded in-memory server log tail. Secrets are never included by the MCP security boundary.")]
    public static RemoteMcpResult LogsQuery(RemoteMcpLogBuffer logs, RemoteMcpOptions options, [Description("Maximum entries to return.")] int limit = 50, [Description("Optional case-insensitive text filter for category or message.")] string? contains = null)
    {
        if (limit < 1 || limit > options.MaxQueryItems) return RemoteMcpResult.Rejected("invalid_argument", $"limit must be between 1 and {options.MaxQueryItems}.");
        var json = JsonSerializer.Serialize(logs.Query(limit, contains));
        return new RemoteMcpResult(true, "ok", json.Length <= options.MaxResultBytes ? json : json[..options.MaxResultBytes]);
    }

    [McpServerTool(Name = "entity_query", ReadOnly = true, Destructive = false, UseStructuredContent = true), Description("List or inspect an allowlisted entity type through the administration command boundary.")]
    public static Task<RemoteMcpResult> EntityQuery(RemoteMcpAdminGateway admin, RemoteMcpOptions options, [Description("Entity type such as agent, building, road.segment, railway.station, or train.")] string entity, [Description("Optional entity ID. Omit to list entities.")] string? id = null, [Description("Maximum list lines returned.")] int limit = 50, CancellationToken cancellationToken = default)
    {
        if (!QueryEntities.TryGetValue(entity, out var prefix)) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", "Entity type is not allowlisted."));
        if (limit < 1 || limit > options.MaxQueryItems) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", $"limit must be between 1 and {options.MaxQueryItems}."));
        var command = id is null ? $"{prefix} list" : $"{prefix} show {Quote(id)}";
        return admin.ExecuteAsync(command, cancellationToken, id is null ? limit : null);
    }

    [McpServerTool(Name = "simulation_pause", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true), Authorize(Policy = RemoteMcpPolicies.Write), Description("Pause simulation execution through the administration command queue.")]
    public static Task<RemoteMcpResult> SimulationPause(RemoteMcpAdminGateway admin, CancellationToken cancellationToken) => admin.ExecuteAsync("simulation pause", cancellationToken);

    [McpServerTool(Name = "simulation_step", ReadOnly = false, Destructive = false, UseStructuredContent = true), Authorize(Policy = RemoteMcpPolicies.Write), Description("Advance a paused simulation by a bounded number of ticks through the administration command queue.")]
    public static Task<RemoteMcpResult> SimulationStep(RemoteMcpAdminGateway admin, [Description("Number of ticks to advance, from 1 to 10000.")] int count, CancellationToken cancellationToken)
    {
        if (count is < 1 or > 10_000) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", "count must be between 1 and 10000."));
        return admin.ExecuteAsync($"simulation step {count.ToString(CultureInfo.InvariantCulture)}", cancellationToken);
    }

    [McpServerTool(Name = "simulation_resume", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true), Authorize(Policy = RemoteMcpPolicies.Write), Description("Resume simulation execution through the administration command queue.")]
    public static Task<RemoteMcpResult> SimulationResume(RemoteMcpAdminGateway admin, CancellationToken cancellationToken) => admin.ExecuteAsync("simulation resume", cancellationToken);

    [McpServerTool(Name = "simulation_save", ReadOnly = false, Destructive = false, UseStructuredContent = true), Authorize(Policy = RemoteMcpPolicies.Write), Description("Save the authoritative world into the configured MCP save directory. Arbitrary file paths are not accepted.")]
    public static Task<RemoteMcpResult> SimulationSave(RemoteMcpAdminGateway admin, RemoteMcpOptions options, [Description("Save slot name using letters, digits, dot, underscore, or hyphen.")] string slot, CancellationToken cancellationToken)
    {
        if (!IsSafeSlot(slot)) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", "slot must be 1-64 characters and contain only letters, digits, dot, underscore, or hyphen."));
        Directory.CreateDirectory(options.SaveDirectory);
        var path = Path.Combine(options.SaveDirectory, slot + ".mvw");
        return admin.ExecuteAsync($"world save {Quote(path)}", cancellationToken);
    }

    [McpServerTool(Name = "entity_write", ReadOnly = false, Destructive = false, UseStructuredContent = true), Authorize(Policy = RemoteMcpPolicies.Write), Description("Create or update an allowlisted entity by mapping structured MCP input to the existing administration command boundary.")]
    public static Task<RemoteMcpResult> EntityWrite(RemoteMcpAdminGateway admin, [Description("Allowlisted entity type.")] string entity, [Description("Operation: add or update.")] string operation, [Description("Administration arguments for the selected entity operation. Each item becomes exactly one quoted command token.")] string[] arguments, CancellationToken cancellationToken)
    {
        if (!MutableEntities.Contains(entity) || !QueryEntities.TryGetValue(entity, out var prefix)) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", "Entity type is not writable through MCP."));
        if (!operation.Equals("add", StringComparison.OrdinalIgnoreCase) && !operation.Equals("update", StringComparison.OrdinalIgnoreCase)) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", "operation must be add or update."));
        if (!ValidateArguments(arguments, out var error)) return Task.FromResult(error!);
        return admin.ExecuteAsync(BuildCommand(prefix, operation.ToLowerInvariant(), arguments), cancellationToken);
    }

    [McpServerTool(Name = "entity_remove", ReadOnly = false, Destructive = true, UseStructuredContent = true), Authorize(Policy = RemoteMcpPolicies.Destructive), Description("Remove an allowlisted entity. Requires destructive scope and explicit confirmation.")]
    public static Task<RemoteMcpResult> EntityRemove(RemoteMcpAdminGateway admin, [Description("Allowlisted entity type.")] string entity, [Description("Administration arguments identifying the entity to remove.")] string[] arguments, [Description("Must be true to confirm the destructive operation.")] bool confirm, CancellationToken cancellationToken)
    {
        if (!confirm) return Task.FromResult(RemoteMcpResult.Rejected("confirmation_required", "Set confirm=true to execute an entity removal."));
        if (!MutableEntities.Contains(entity) || !QueryEntities.TryGetValue(entity, out var prefix)) return Task.FromResult(RemoteMcpResult.Rejected("invalid_argument", "Entity type is not removable through MCP."));
        if (!ValidateArguments(arguments, out var error)) return Task.FromResult(error!);
        return admin.ExecuteAsync(BuildCommand(prefix, "remove", arguments), cancellationToken);
    }

    private static string BuildCommand(string prefix, string operation, IEnumerable<string> arguments) => $"{prefix} {operation} {string.Join(' ', arguments.Select(Quote))}".TrimEnd();

    private static bool ValidateArguments(string[]? arguments, out RemoteMcpResult? error)
    {
        error = null;
        if (arguments is null || arguments.Length > 32)
        {
            error = RemoteMcpResult.Rejected("invalid_argument", "arguments must contain at most 32 items.");
            return false;
        }
        if (arguments.Any(static argument => argument is null || argument.Length > 256 || argument.IndexOfAny(['\r', '\n', '\0']) >= 0))
        {
            error = RemoteMcpResult.Rejected("invalid_argument", "Each argument must be at most 256 characters and cannot contain control line breaks or NUL.");
            return false;
        }
        return true;
    }

    private static bool IsSafeSlot(string slot) => slot.Length is >= 1 and <= 64 && slot.All(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-') && slot is not "." and not "..";
    private static string Quote(string value) => '"' + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + '"';
}
