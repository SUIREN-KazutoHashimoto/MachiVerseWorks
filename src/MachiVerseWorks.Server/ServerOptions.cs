using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace MachiVerseWorks.Server;

internal sealed class ServerOptions
{
    public const int DefaultMaximumSubscriptionCellCount = 1_048_576;
    public const int MaximumSupportedSubscriptionCellCount = 4_194_304;
    public const int MaximumSupportedSnapshotRate = 60;
    public const int MaximumSupportedTickRate = 240;
    public const int MaximumSupportedInitialAgentCount = 1_000_000;
    public const int DefaultMaximumWebSocketConnections = 256;
    private const string DefaultAllowedWebSocketOrigins = "http://127.0.0.1:5173;http://localhost:5173";

    private ServerOptions(
        IPAddress listenAddress,
        int port,
        int snapshotRate,
        int maximumSubscriptionCellCount,
        IReadOnlyList<string> allowedWebSocketOrigins,
        bool allowInsecureRemoteAccess,
        bool enablePersonInspection,
        bool enableRemoteDiagnostics,
        bool consoleEnabled,
        int maximumWebSocketConnections,
        TimeSpan helloTimeout,
        TimeSpan frameReceiveTimeout,
        TimeSpan closeTimeout,
        TimeSpan observationDeliveryTimeout,
        int requestRateLimitPerSecond,
        int requestRateLimitBurst,
        int invalidRequestStrikeLimit,
        TimeSpan invalidRequestStrikeWindow,
        int tickRate,
        ulong seed,
        double spatialCellSize,
        int initialAgentCount,
        double spawnMinX,
        double spawnMinY,
        double spawnMinZ,
        double spawnMaxX,
        double spawnMaxY,
        double spawnMaxZ)
    {
        ListenAddress = listenAddress;
        Port = port;
        SnapshotRate = snapshotRate;
        MaximumSubscriptionCellCount = maximumSubscriptionCellCount;
        AllowedWebSocketOrigins = allowedWebSocketOrigins;
        AllowInsecureRemoteAccess = allowInsecureRemoteAccess;
        EnablePersonInspection = enablePersonInspection;
        EnableRemoteDiagnostics = enableRemoteDiagnostics;
        ConsoleEnabled = consoleEnabled;
        MaximumWebSocketConnections = maximumWebSocketConnections;
        HelloTimeout = helloTimeout;
        FrameReceiveTimeout = frameReceiveTimeout;
        CloseTimeout = closeTimeout;
        ObservationDeliveryTimeout = observationDeliveryTimeout;
        RequestRateLimitPerSecond = requestRateLimitPerSecond;
        RequestRateLimitBurst = requestRateLimitBurst;
        InvalidRequestStrikeLimit = invalidRequestStrikeLimit;
        InvalidRequestStrikeWindow = invalidRequestStrikeWindow;
        TickRate = tickRate;
        Seed = seed;
        SpatialCellSize = spatialCellSize;
        InitialAgentCount = initialAgentCount;
        SpawnMinX = spawnMinX;
        SpawnMinY = spawnMinY;
        SpawnMinZ = spawnMinZ;
        SpawnMaxX = spawnMaxX;
        SpawnMaxY = spawnMaxY;
        SpawnMaxZ = spawnMaxZ;
    }

    public IPAddress ListenAddress { get; }
    public int Port { get; }
    public int SnapshotRate { get; }
    public int MaximumSubscriptionCellCount { get; }
    public IReadOnlyList<string> AllowedWebSocketOrigins { get; }
    public bool AllowInsecureRemoteAccess { get; }
    public bool EnablePersonInspection { get; }
    public bool EnableRemoteDiagnostics { get; }
    public bool ConsoleEnabled { get; }
    public int MaximumWebSocketConnections { get; }
    public TimeSpan HelloTimeout { get; }
    public TimeSpan FrameReceiveTimeout { get; }
    public TimeSpan CloseTimeout { get; }
    public TimeSpan ObservationDeliveryTimeout { get; }
    public int RequestRateLimitPerSecond { get; }
    public int RequestRateLimitBurst { get; }
    public int InvalidRequestStrikeLimit { get; }
    public TimeSpan InvalidRequestStrikeWindow { get; }
    public int TickRate { get; }
    public ulong Seed { get; }
    public double SpatialCellSize { get; }
    public int InitialAgentCount { get; }
    public double SpawnMinX { get; }
    public double SpawnMinY { get; }
    public double SpawnMinZ { get; }
    public double SpawnMaxX { get; }
    public double SpawnMaxY { get; }
    public double SpawnMaxZ { get; }
    public TimeSpan TickInterval => TimeSpan.FromSeconds(1d / TickRate);
    public TimeSpan SnapshotInterval => TimeSpan.FromSeconds(1d / SnapshotRate);
    public bool IsLoopbackOnly => IPAddress.IsLoopback(ListenAddress);
    public bool DetailedDiagnosticsAvailable => IsLoopbackOnly || EnableRemoteDiagnostics;

    public static ServerOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var listenAddressText = configuration["Server:ListenAddress"] ?? "127.0.0.1";
        if (!IPAddress.TryParse(listenAddressText, out var listenAddress)) throw new InvalidOperationException($"Server:ListenAddress must be an IP address, but was '{listenAddressText}'.");
        var allowInsecureRemoteAccess = ReadBoolean(configuration, "Server:AllowInsecureRemoteAccess", false);
        if (!IPAddress.IsLoopback(listenAddress) && !allowInsecureRemoteAccess)
            throw new InvalidOperationException("Non-loopback Server:ListenAddress requires Server:AllowInsecureRemoteAccess=true. Origin validation is not authentication; use a trusted reverse proxy/authentication boundary for remote exposure.");

        var port = ReadInt32(configuration, "Server:Port", 5080);
        if (port is < 0 or > 65535) throw new InvalidOperationException("Server:Port must be between 0 and 65535.");
        var snapshotRate = ReadInt32(configuration, "Server:SnapshotRate", 10);
        if (snapshotRate is <= 0 or > MaximumSupportedSnapshotRate) throw new InvalidOperationException($"Server:SnapshotRate must be between 1 and {MaximumSupportedSnapshotRate}.");
        var maximumSubscriptionCellCount = ReadInt32(configuration, "Server:MaximumSubscriptionCellCount", DefaultMaximumSubscriptionCellCount);
        if (maximumSubscriptionCellCount is <= 0 or > MaximumSupportedSubscriptionCellCount) throw new InvalidOperationException($"Server:MaximumSubscriptionCellCount must be between 1 and {MaximumSupportedSubscriptionCellCount}.");
        var maximumWebSocketConnections = ReadInt32(configuration, "Server:MaximumWebSocketConnections", DefaultMaximumWebSocketConnections);
        if (maximumWebSocketConnections is <= 0 or > 10_000) throw new InvalidOperationException("Server:MaximumWebSocketConnections must be between 1 and 10000.");
        var helloTimeout = ReadDurationMilliseconds(configuration, "Server:HelloTimeoutMilliseconds", 5_000, 100, 60_000);
        var frameReceiveTimeout = ReadDurationMilliseconds(configuration, "Server:FrameReceiveTimeoutMilliseconds", 10_000, 100, 120_000);
        var closeTimeout = ReadDurationMilliseconds(configuration, "Server:CloseTimeoutMilliseconds", 2_000, 100, 30_000);
        var observationDeliveryTimeout = ReadDurationMilliseconds(configuration, "Server:ObservationDeliveryTimeoutMilliseconds", 5_000, 100, 60_000);
        var requestRateLimitPerSecond = ReadInt32(configuration, "Server:RequestRateLimitPerSecond", 30);
        if (requestRateLimitPerSecond is <= 0 or > 1_000) throw new InvalidOperationException("Server:RequestRateLimitPerSecond must be between 1 and 1000.");
        var requestRateLimitBurst = ReadInt32(configuration, "Server:RequestRateLimitBurst", 60);
        if (requestRateLimitBurst < requestRateLimitPerSecond || requestRateLimitBurst > 10_000) throw new InvalidOperationException("Server:RequestRateLimitBurst must be at least RequestRateLimitPerSecond and at most 10000.");
        var invalidRequestStrikeLimit = ReadInt32(configuration, "Server:InvalidRequestStrikeLimit", 8);
        if (invalidRequestStrikeLimit is <= 0 or > 100) throw new InvalidOperationException("Server:InvalidRequestStrikeLimit must be between 1 and 100.");
        var invalidRequestStrikeWindow = ReadDurationMilliseconds(configuration, "Server:InvalidRequestStrikeWindowMilliseconds", 60_000, 1_000, 600_000);
        var enableRemoteDiagnostics = ReadBoolean(configuration, "Server:EnableRemoteDiagnostics", false);
        var enablePersonInspection = ReadBoolean(configuration, "Server:EnablePersonInspection", IPAddress.IsLoopback(listenAddress));
        var consoleEnabled = ReadBoolean(configuration, "Server:Console:Enabled", true);
        var allowedWebSocketOrigins = ReadAllowedWebSocketOrigins(configuration);

        var tickRate = ReadInt32(configuration, "Simulation:TickRate", 30);
        if (tickRate is <= 0 or > MaximumSupportedTickRate) throw new InvalidOperationException($"Simulation:TickRate must be between 1 and {MaximumSupportedTickRate}.");
        var seed = ReadUInt64(configuration, "Simulation:Seed", 1UL);
        var spatialCellSize = ReadDouble(configuration, "Simulation:SpatialCellSize", 64d);
        if (!double.IsFinite(spatialCellSize) || spatialCellSize <= 0d) throw new InvalidOperationException("Simulation:SpatialCellSize must be finite and greater than zero.");
        var initialAgentCount = ReadInt32(configuration, "Simulation:InitialAgentCount", 1000);
        if (initialAgentCount is < 0 or > MaximumSupportedInitialAgentCount) throw new InvalidOperationException($"Simulation:InitialAgentCount must be between 0 and {MaximumSupportedInitialAgentCount}.");
        var spawnMinX = ReadDouble(configuration, "Simulation:SpawnVolume:MinX", -500d);
        var spawnMinY = ReadDouble(configuration, "Simulation:SpawnVolume:MinY", -500d);
        var spawnMinZ = ReadDouble(configuration, "Simulation:SpawnVolume:MinZ", -64d);
        var spawnMaxX = ReadDouble(configuration, "Simulation:SpawnVolume:MaxX", 500d);
        var spawnMaxY = ReadDouble(configuration, "Simulation:SpawnVolume:MaxY", 500d);
        var spawnMaxZ = ReadDouble(configuration, "Simulation:SpawnVolume:MaxZ", 64d);
        if (!double.IsFinite(spawnMinX) || !double.IsFinite(spawnMinY) || !double.IsFinite(spawnMinZ) || !double.IsFinite(spawnMaxX) || !double.IsFinite(spawnMaxY) || !double.IsFinite(spawnMaxZ) || spawnMaxX < spawnMinX || spawnMaxY < spawnMinY || spawnMaxZ < spawnMinZ)
            throw new InvalidOperationException("Simulation:SpawnVolume must contain finite 3D coordinates with max >= min.");

        return new ServerOptions(
            listenAddress, port, snapshotRate, maximumSubscriptionCellCount, allowedWebSocketOrigins,
            allowInsecureRemoteAccess, enablePersonInspection, enableRemoteDiagnostics, consoleEnabled, maximumWebSocketConnections,
            helloTimeout, frameReceiveTimeout, closeTimeout, observationDeliveryTimeout, requestRateLimitPerSecond, requestRateLimitBurst,
            invalidRequestStrikeLimit, invalidRequestStrikeWindow,
            tickRate, seed, spatialCellSize, initialAgentCount,
            spawnMinX, spawnMinY, spawnMinZ, spawnMaxX, spawnMaxY, spawnMaxZ);
    }

    private static string[] ReadAllowedWebSocketOrigins(IConfiguration configuration)
    {
        var value = configuration["Server:AllowedWebSocketOrigins"] ?? DefaultAllowedWebSocketOrigins;
        if (string.IsNullOrWhiteSpace(value)) return [];
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(WebSocketOriginPolicy.NormalizeOrigin).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int ReadInt32(IConfiguration configuration, string key, int defaultValue)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw new InvalidOperationException($"{key} must be a 32-bit integer.");
        return value;
    }

    private static ulong ReadUInt64(IConfiguration configuration, string key, ulong defaultValue)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)) throw new InvalidOperationException($"{key} must be an unsigned 64-bit integer.");
        return value;
    }

    private static double ReadDouble(IConfiguration configuration, string key, double defaultValue)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) throw new InvalidOperationException($"{key} must be a floating-point number.");
        return value;
    }

    private static bool ReadBoolean(IConfiguration configuration, string key, bool defaultValue)
    {
        var text = configuration[key];
        if (text is null) return defaultValue;
        if (!bool.TryParse(text, out var value)) throw new InvalidOperationException($"{key} must be true or false.");
        return value;
    }

    private static TimeSpan ReadDurationMilliseconds(IConfiguration configuration, string key, int defaultMilliseconds, int minimumMilliseconds, int maximumMilliseconds)
    {
        var milliseconds = ReadInt32(configuration, key, defaultMilliseconds);
        if (milliseconds < minimumMilliseconds || milliseconds > maximumMilliseconds)
            throw new InvalidOperationException($"{key} must be between {minimumMilliseconds} and {maximumMilliseconds} milliseconds.");
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
