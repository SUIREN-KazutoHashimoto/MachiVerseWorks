using Microsoft.Extensions.Logging;

namespace MachiVerseWorks.Server;

internal static partial class ServerLog
{
    [LoggerMessage(1, LogLevel.Information, "Simulation tick loop started at {TickRate} Hz.")]
    public static partial void SimulationTickStarted(ILogger logger, int tickRate);

    [LoggerMessage(2, LogLevel.Information, "Simulation tick loop stopped at tick {TickCount}.")]
    public static partial void SimulationTickStopped(ILogger logger, ulong tickCount);

    [LoggerMessage(3, LogLevel.Information, "Snapshot publisher started at {SnapshotRate} Hz.")]
    public static partial void SnapshotPublisherStarted(ILogger logger, int snapshotRate);

    [LoggerMessage(4, LogLevel.Information, "Snapshot publisher stopped.")]
    public static partial void SnapshotPublisherStopped(ILogger logger);

    [LoggerMessage(5, LogLevel.Debug, "Snapshot delivery stopped for connection {ConnectionId}.")]
    public static partial void SnapshotDeliveryStopped(ILogger logger, Guid connectionId, Exception exception);

    [LoggerMessage(6, LogLevel.Information, "Client {ConnectionId} connected.")]
    public static partial void ClientConnected(ILogger logger, Guid connectionId);

    [LoggerMessage(7, LogLevel.Debug, "WebSocket connection {ConnectionId} ended.")]
    public static partial void WebSocketEnded(ILogger logger, Guid connectionId, Exception exception);

    [LoggerMessage(8, LogLevel.Information, "Client {ConnectionId} disconnected.")]
    public static partial void ClientDisconnected(ILogger logger, Guid connectionId);

    [LoggerMessage(9, LogLevel.Warning, "Ignoring unsupported client command type {CommandType}.")]
    public static partial void UnsupportedClientCommand(ILogger logger, string commandType);

    [LoggerMessage(
        10,
        LogLevel.Debug,
        "Snapshot delivered to {ConnectionId}: {AgentCount} agents, {PedestrianCount} pedestrians, {VehicleCount} vehicles, {TrainCount} trains, {EntityCount} entities, {MessageCount} messages, {Bytes} bytes, encode {EncodeTimeMs} ms, send {SendTimeMs} ms.")]
    public static partial void SnapshotDeliveryMetrics(
        ILogger logger,
        Guid connectionId,
        int agentCount,
        int pedestrianCount,
        int vehicleCount,
        int trainCount,
        int entityCount,
        int messageCount,
        long bytes,
        double encodeTimeMs,
        double sendTimeMs);

    [LoggerMessage(11, LogLevel.Critical, "Unexpected snapshot delivery failure for connection {ConnectionId}.")]
    public static partial void UnexpectedSnapshotDeliveryFailure(ILogger logger, Guid connectionId, Exception exception);
}
