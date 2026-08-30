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
        var current = new HashSet<ulong>(snapshots.Length);
        var messages = new List<IProtocolMessage>(snapshots.Length + knownAgentIds.Count);
        foreach (var snapshot in snapshots)
        {
            var id = snapshot.Id.Value;
            current.Add(id);
            messages.Add(knownAgentIds.Contains(id)
                ? new AgentUpdateMessage(id, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount)
                : new AgentSpawnMessage(id, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.TickCount));
        }
        foreach (var id in knownAgentIds)
        {
            if (!current.Contains(id)) messages.Add(new AgentRemoveMessage(id, tickCount));
        }
        return new SnapshotMessagePlan(messages, current);
    }
}

internal sealed record PedestrianSnapshotMessagePlan(IReadOnlyList<IProtocolMessage> Messages, HashSet<ulong> CurrentPedestrianIds);

internal static class PedestrianSnapshotMessagePlanner
{
    public static PedestrianSnapshotMessagePlan Create(PedestrianSnapshot[] snapshots, IReadOnlySet<ulong> knownPedestrianIds, ulong tickCount)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(knownPedestrianIds);
        Array.Sort(snapshots, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        var current = new HashSet<ulong>(snapshots.Length);
        var messages = new List<IProtocolMessage>(snapshots.Length + knownPedestrianIds.Count);
        foreach (var snapshot in snapshots)
        {
            var id = snapshot.Id.Value;
            current.Add(id);
            var state = (ProtocolPedestrianMovementState)snapshot.State;
            messages.Add(knownPedestrianIds.Contains(id)
                ? new PedestrianUpdateMessage(id, snapshot.TripRequestId.Value, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.WalkingSpeedMetersPerSecond, state, snapshot.TickCount)
                : new PedestrianSpawnMessage(id, snapshot.TripRequestId.Value, snapshot.Position.X, snapshot.Position.Y, snapshot.Position.Z, snapshot.Velocity.X, snapshot.Velocity.Y, snapshot.Velocity.Z, snapshot.WalkingSpeedMetersPerSecond, state, snapshot.TickCount));
        }
        foreach (var id in knownPedestrianIds)
        {
            if (!current.Contains(id)) messages.Add(new PedestrianRemoveMessage(id, tickCount));
        }
        return new PedestrianSnapshotMessagePlan(messages, current);
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
            if (connection.HandshakeCompleted && connection.Socket.State == WebSocketState.Open && connection.TryCaptureSubscription(out var subscription))
                _deliveryScheduler.TrySchedule(connection.Id, () => PublishConnectionAsync(connection, subscription, cancellationToken));
        }
    }

    private async Task PublishConnectionAsync(ClientConnection connection, ClientSubscriptionState subscription, CancellationToken cancellationToken)
    {
        try
        {
            var snapshots = simulation.CreateSnapshot(subscription.Volume);
            var tick = simulation.TickCount;
            var agentPlan = SnapshotMessagePlanner.Create(snapshots, subscription.KnownAgentIds, tick);
            var pedestrianPlan = connection.NegotiatedVersion.SupportsPedestrians
                ? PedestrianSnapshotMessagePlanner.Create(simulation.CreatePedestrianSnapshot(subscription.Volume), subscription.KnownPedestrianIds, tick)
                : new PedestrianSnapshotMessagePlan([], []);
            var roadMessage = connection.NegotiatedVersion.SupportsRoadNetwork
                ? RoadNetworkMessageMapper.Create(simulation.CreateRoadNetworkSnapshot(subscription.Volume), tick)
                : null;

            long bytes = 0;
            double encodeTimeMs = 0;
            double sendTimeMs = 0;
            var messageCount = agentPlan.Messages.Count + pedestrianPlan.Messages.Count + (roadMessage is null ? 0 : 1);
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            foreach (var message in agentPlan.Messages)
            {
                sendCancellation.CancelAfter(ClientSendTimeout);
                var sent = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            foreach (var message in pedestrianPlan.Messages)
            {
                sendCancellation.CancelAfter(ClientSendTimeout);
                var sent = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            if (roadMessage is not null)
            {
                sendCancellation.CancelAfter(ClientSendTimeout);
                var sent = await connection.SendAsync(roadMessage, connection.NegotiatedVersion, sendCancellation.Token);
                bytes = checked(bytes + sent.FrameBytes); encodeTimeMs += sent.EncodeTimeMs; sendTimeMs += sent.SendTimeMs;
            }
            connection.TryReplaceKnownEntityIds(subscription.Revision, agentPlan.CurrentAgentIds, pedestrianPlan.CurrentPedestrianIds);
            metrics.RecordSnapshotDelivery(snapshots.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);
            ServerLog.SnapshotDeliveryMetrics(logger, connection.Id, snapshots.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            if (!cancellationToken.IsCancellationRequested) ServerLog.SnapshotDeliveryStopped(logger, connection.Id, exception);
            connection.Abort(); connections.Remove(connection.Id);
        }
        catch (Exception exception)
        {
            ServerLog.UnexpectedSnapshotDeliveryFailure(logger, connection.Id, exception);
            throw;
        }
    }
}
