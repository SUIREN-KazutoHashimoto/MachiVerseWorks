using System.Globalization;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class SimulationTickService(SimulationRuntime simulation, ILogger<SimulationTickService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ServerLog.SimulationTickStarted(logger, simulation.TickRate);
        using var timer = new PeriodicTimer(simulation.TickInterval);
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

internal static class RoadSnapshotMessagePlanner
{
    public const string TooLargeDetailCode = "roadSnapshotTooLarge";

    public static IProtocolMessage Create(RoadNetworkSnapshot snapshot, ulong tickCount)
    {
        var message = RoadNetworkMessageMapper.Create(snapshot, tickCount);
        if (ProtocolCodec.FitsSingleFrame(message)) return message;

        return new ProtocolErrorMessage(
            ProtocolErrorCode.InvalidRequest,
            [
                new ProtocolErrorParameter(ProtocolErrorParameterKeys.Field, "volume"),
                new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, TooLargeDetailCode),
                new ProtocolErrorParameter("payloadBytes", ProtocolCodec.GetPayloadLength(message).ToString(CultureInfo.InvariantCulture)),
                new ProtocolErrorParameter("maximumPayloadBytes", ProtocolFrameHeader.MaxPayloadLength.ToString(CultureInfo.InvariantCulture)),
            ]);
    }
}

internal readonly record struct PendingSnapshotDelivery(ClientConnection Connection, ClientSubscriptionState Subscription);

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
                var pending = CapturePendingDeliveries();
                if (pending.Length == 0) continue;
                var publishSnapshot = simulation.CapturePublishSnapshot();
                SchedulePublish(publishSnapshot, pending, stoppingToken);
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

    private PendingSnapshotDelivery[] CapturePendingDeliveries()
    {
        var candidates = connections.CreateSnapshot();
        var pending = new List<PendingSnapshotDelivery>(candidates.Length);
        foreach (var connection in candidates)
        {
            if (!connection.HandshakeCompleted || connection.Socket.State != WebSocketState.Open || !connection.TryCaptureSubscription(out var subscription)) continue;
            pending.Add(new PendingSnapshotDelivery(connection, subscription));
        }
        return pending.ToArray();
    }

    private void SchedulePublish(SimulationPublishSnapshot publishSnapshot, PendingSnapshotDelivery[] pending, CancellationToken cancellationToken)
    {
        foreach (var delivery in pending)
        {
            _deliveryScheduler.TrySchedule(
                delivery.Connection.Id,
                () => PublishConnectionAsync(delivery.Connection, delivery.Subscription, publishSnapshot, cancellationToken));
        }
    }

    private async Task PublishConnectionAsync(ClientConnection connection, ClientSubscriptionState subscription, SimulationPublishSnapshot publishSnapshot, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            var snapshot = publishSnapshot.Query(subscription.Volume);
            var agentPlan = SnapshotMessagePlanner.Create(snapshot.Agents, subscription.KnownAgentIds, snapshot.TickCount);
            var pedestrianPlan = connection.NegotiatedVersion.SupportsPedestrians
                ? PedestrianSnapshotMessagePlanner.Create(snapshot.Pedestrians, subscription.KnownPedestrianIds, snapshot.TickCount)
                : new PedestrianSnapshotMessagePlan([], []);

            IProtocolMessage? roadMessage = null;
            var roadStateHandled = false;
            if (connection.NegotiatedVersion.SupportsRoadNetwork
                && connection.NeedsRoadSnapshot(subscription.Revision, publishSnapshot.RoadNetwork.Revision))
            {
                roadMessage = RoadSnapshotMessagePlanner.Create(snapshot.RoadNetwork, snapshot.TickCount);
                roadStateHandled = true;
            }

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
            if (roadStateHandled) connection.TryMarkRoadSnapshotDelivered(subscription.Revision, publishSnapshot.RoadNetwork.Revision);
            metrics.RecordSnapshotDelivery(snapshot.Agents.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);
            ServerLog.SnapshotDeliveryMetrics(logger, connection.Id, snapshot.Agents.Length, messageCount, bytes, encodeTimeMs, sendTimeMs);
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
