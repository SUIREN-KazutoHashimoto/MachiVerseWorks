using System.Globalization;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace MachiVerseWorks.Server;

internal sealed class ServerOptions
{
    private const int DefaultMaximumSubscriptionCellCount = 262_144;
    private const string DefaultAllowedWebSocketOrigins = "http://127.0.0.1:5173;http://localhost:5173";

    private ServerOptions(IPAddress listenAddress, int port, int snapshotRate, int maximumSubscriptionCellCount, IReadOnlyList<string> allowedWebSocketOrigins, int tickRate, ulong seed, double spatialCellSize, int initialAgentCount, double spawnMinX, double spawnMinY, double spawnMinZ, double spawnMaxX, double spawnMaxY, double spawnMaxZ)
    {
        ListenAddress = listenAddress;
        Port = port;
        SnapshotRate = snapshotRate;
        MaximumSubscriptionCellCount = maximumSubscriptionCellCount;
        AllowedWebSocketOrigins = allowedWebSocketOrigins;
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

    public static ServerOptions Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var listenAddressText = configuration["Server:ListenAddress"] ?? "127.0.0.1";
        if (!IPAddress.TryParse(listenAddressText, out var listenAddress)) throw new InvalidOperationException($"Server:ListenAddress must be an IP address, but was '{listenAddressText}'.");
        var port = ReadInt32(configuration, "Server:Port", 5080);
        if (port is < 0 or > 65535) throw new InvalidOperationException("Server:Port must be between 0 and 65535.");
        var snapshotRate = ReadInt32(configuration, "Server:SnapshotRate", 10);
        if (snapshotRate <= 0) throw new InvalidOperationException("Server:SnapshotRate must be greater than zero.");
        var maximumSubscriptionCellCount = ReadInt32(configuration, "Server:MaximumSubscriptionCellCount", DefaultMaximumSubscriptionCellCount);
        if (maximumSubscriptionCellCount <= 0) throw new InvalidOperationException("Server:MaximumSubscriptionCellCount must be greater than zero.");
        var allowedWebSocketOrigins = ReadAllowedWebSocketOrigins(configuration);
        var tickRate = ReadInt32(configuration, "Simulation:TickRate", 30);
        if (tickRate is <= 0 or > ushort.MaxValue) throw new InvalidOperationException($"Simulation:TickRate must be between 1 and {ushort.MaxValue}.");
        var seed = ReadUInt64(configuration, "Simulation:Seed", 1UL);
        var spatialCellSize = ReadDouble(configuration, "Simulation:SpatialCellSize", 64d);
        if (!double.IsFinite(spatialCellSize) || spatialCellSize <= 0d) throw new InvalidOperationException("Simulation:SpatialCellSize must be finite and greater than zero.");
        var initialAgentCount = ReadInt32(configuration, "Simulation:InitialAgentCount", 1000);
        if (initialAgentCount < 0) throw new InvalidOperationException("Simulation:InitialAgentCount cannot be negative.");
        var spawnMinX = ReadDouble(configuration, "Simulation:SpawnVolume:MinX", -500d);
        var spawnMinY = ReadDouble(configuration, "Simulation:SpawnVolume:MinY", -500d);
        var spawnMinZ = ReadDouble(configuration, "Simulation:SpawnVolume:MinZ", -64d);
        var spawnMaxX = ReadDouble(configuration, "Simulation:SpawnVolume:MaxX", 500d);
        var spawnMaxY = ReadDouble(configuration, "Simulation:SpawnVolume:MaxY", 500d);
        var spawnMaxZ = ReadDouble(configuration, "Simulation:SpawnVolume:MaxZ", 64d);
        if (!double.IsFinite(spawnMinX) || !double.IsFinite(spawnMinY) || !double.IsFinite(spawnMinZ) || !double.IsFinite(spawnMaxX) || !double.IsFinite(spawnMaxY) || !double.IsFinite(spawnMaxZ) || spawnMaxX < spawnMinX || spawnMaxY < spawnMinY || spawnMaxZ < spawnMinZ)
        {
            throw new InvalidOperationException("Simulation:SpawnVolume must contain finite 3D coordinates with max >= min.");
        }
        return new ServerOptions(listenAddress, port, snapshotRate, maximumSubscriptionCellCount, allowedWebSocketOrigins, tickRate, seed, spatialCellSize, initialAgentCount, spawnMinX, spawnMinY, spawnMinZ, spawnMaxX, spawnMaxY, spawnMaxZ);
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
}
