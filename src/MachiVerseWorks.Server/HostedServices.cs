using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationTickService(SimulationRuntime simulation, ServerOptions options, ILogger<SimulationTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SimulationTickStarted(logger, options.TickRate);
        using var timer = new PeriodicTimer(options.TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken)) simulation.Step();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        ServerLog.SimulationTickStopped(logger, simulation.TickCount);
    }
}

internal sealed record SnapshotMessagePlan(IReadOnlyList<IProtocolMessage> Messages, HashSet<ulong> CurrentAgentIds);

internal static class SnapshotMessagePlanner
{
    public static SnapshotMessagePlan Create(AgentSnapshot[] snapshots, IReadOnlySet<ulong> knownAgentIds, ulong tickCount)
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
                ? new AgentUpdateMessage(agentId, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount)
                : new AgentSpawnMessage(agentId, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount));
        }
        foreach (var knownAgentId in knownAgentIds)
        {
            if (!currentAgentIds.Contains(knownAgentId)) messages.Add(new AgentRemoveMessage(knownAgentId, tickCount));
        }
        return new SnapshotMessagePlan(messages, currentAgentIds);
    }
}

internal sealed class SnapshotPublishService(SimulationRuntime simulation, ServerOptions options, ClientConnectionRegistry connections, E2eMetrics metrics, ILogger<SnapshotPublishService> logger) : BackgroundService
{
    private static readonly TimeSpan ClientSendTimeout = TimeSpan.FromSeconds(5);
    private readonly SnapshotDeliveryScheduler _deliveryScheduler = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SnapshotPublisherStarted(logger, options.SnapshotRate);
        using var timer = new PeriodicTimer(options.SnapshotInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                _deliveryScheduler.ThrowIfFaulted();
                SchedulePublish(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _deliveryScheduler.ThrowIfFaulted();
        var inFlight = _deliveryScheduler.CreateInFlightSnapshot();
        if (inFlight.Length > 0) await Task.WhenAll(inFlight);
        _deliveryScheduler.ThrowIfFaulted();
        ServerLog.SnapshotPublisherStopped(logger);
    }

    private void SchedulePublish(CancellationToken cancellationToken)
    {
        foreach (var connection in connections.CreateSnapshot())
        {
            if (!connection.HandshakeCompleted || connection.Socket.State != WebSocketState.Open || !connection.TryCaptureSubscription(out var subscription)) continue;
            _deliveryScheduler.TrySchedule(connection.Id, () => PublishConnectionAsync(connection, subscription, cancellationToken));
        }
    }

    private async Task PublishConnectionAsync(ClientConnection connection, ClientSubscriptionState subscription, CancellationToken cancellationToken)
    {
        try
        {
            var snapshots = simulation.CreateSnapshot(subscription.Volume);
            var plan = SnapshotMessagePlanner.Create(snapshots, subscription.KnownAgentIds, simulation.TickCount);
            long bytes = 0;
            double encodeTimeMs = 0d;
            double sendTimeMs = 0d;
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            foreach (var message in plan.Messages)
            {
                sendCancellation.CancelAfter(ClientSendTimeout);
                var sendMetrics = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token);
                bytes = checked(bytes + sendMetrics.FrameBytes);
                encodeTimeMs += sendMetrics.EncodeTimeMs;
                sendTimeMs += sendMetrics.SendTimeMs;
            }
            connection.TryReplaceKnownAgentIds(subscription.Revision, plan.CurrentAgentIds);
            metrics.RecordSnapshotDelivery(snapshots.Length, plan.Messages.Count, bytes, encodeTimeMs, sendTimeMs);
            ServerLog.SnapshotDeliveryMetrics(logger, connection.Id, snapshots.Length, plan.Messages.Count, bytes, encodeTimeMs, sendTimeMs);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            if (!cancellationToken.IsCancellationRequested) ServerLog.SnapshotDeliveryStopped(logger, connection.Id, exception);
            connection.Abort();
            connections.Remove(connection.Id);
        }
        catch (Exception exception)
        {
            ServerLog.UnexpectedSnapshotDeliveryFailure(logger, connection.Id, exception);
            throw;
        }
    }
}
