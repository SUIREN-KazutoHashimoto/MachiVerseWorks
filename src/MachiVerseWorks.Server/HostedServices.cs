using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationTickService(
    SimulationRuntime simulation,
    ServerOptions options,
    ILogger<SimulationTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SimulationTickStarted(logger, options.TickRate);
        using var timer = new PeriodicTimer(options.TickInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                simulation.Step();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        ServerLog.SimulationTickStopped(logger, simulation.TickCount);
    }
}

internal sealed record SnapshotMessagePlan(
    IReadOnlyList<IProtocolMessage> Messages,
    HashSet<ulong> CurrentAgentIds);

internal static class SnapshotMessagePlanner
{
    public static SnapshotMessagePlan Create(
        AgentSnapshot[] snapshots,
        IReadOnlySet<ulong> knownAgentIds,
        ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(knownAgentIds);

        Array.Sort(snapshots, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));

        var currentAgentIds = new HashSet<ulong>(snapshots.Length);
        var messages = new List<IProtocolMessage>(snapshots.Length + knownAgentIds.Count);

        foreach (var snapshot in snapshots)
        {
            var agentId = snapshot.Id.Value;
            currentAgentIds.Add(agentId);
            messages.Add(knownAgentIds.Contains(agentId)
                ? new AgentUpdateMessage(
                    agentId,
                    snapshot.Position.X,
                    snapshot.Position.Y,
                    snapshot.Velocity.X,
                    snapshot.Velocity.Y,
                    snapshot.TickCount)
                : new AgentSpawnMessage(
                    agentId,
                    snapshot.Position.X,
                    snapshot.Position.Y,
                    snapshot.Velocity.X,
                    snapshot.Velocity.Y,
                    snapshot.TickCount));
        }

        foreach (var knownAgentId in knownAgentIds)
        {
            if (!currentAgentIds.Contains(knownAgentId))
            {
                messages.Add(new AgentRemoveMessage(knownAgentId, tickCount));
            }
        }

        return new SnapshotMessagePlan(messages, currentAgentIds);
    }
}

internal sealed class SnapshotPublishService(
    SimulationRuntime simulation,
    ServerOptions options,
    ClientConnectionRegistry connections,
    E2eMetrics metrics,
    ILogger<SnapshotPublishService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SnapshotPublisherStarted(logger, options.SnapshotRate);
        using var timer = new PeriodicTimer(options.SnapshotInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await PublishAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        ServerLog.SnapshotPublisherStopped(logger);
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        foreach (var connection in connections.CreateSnapshot())
        {
            if (!connection.HandshakeCompleted ||
                connection.Socket.State != WebSocketState.Open ||
                !connection.TryCaptureSubscription(out var subscription))
            {
                continue;
            }

            var snapshots = simulation.CreateSnapshot(subscription.Area);
            var plan = SnapshotMessagePlanner.Create(
                snapshots,
                subscription.KnownAgentIds,
                simulation.TickCount);

            try
            {
                long bytes = 0;
                double encodeTimeMs = 0d;
                double sendTimeMs = 0d;
                foreach (var message in plan.Messages)
                {
                    var sendMetrics = await connection.SendAsync(
                        message,
                        connection.NegotiatedVersion,
                        cancellationToken);
                    bytes = checked(bytes + sendMetrics.FrameBytes);
                    encodeTimeMs += sendMetrics.EncodeTimeMs;
                    sendTimeMs += sendMetrics.SendTimeMs;
                }

                connection.TryReplaceKnownAgentIds(subscription.Revision, plan.CurrentAgentIds);
                metrics.RecordSnapshotDelivery(
                    snapshots.Length,
                    plan.Messages.Count,
                    bytes,
                    encodeTimeMs,
                    sendTimeMs);
                ServerLog.SnapshotDeliveryMetrics(
                    logger,
                    connection.Id,
                    snapshots.Length,
                    plan.Messages.Count,
                    bytes,
                    encodeTimeMs,
                    sendTimeMs);
            }
            catch (Exception exception) when (
                exception is WebSocketException or OperationCanceledException or ObjectDisposedException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    ServerLog.SnapshotDeliveryStopped(logger, connection.Id, exception);
                }

                connection.Abort();
                connections.Remove(connection.Id);
            }
        }
    }
}
