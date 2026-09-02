using System.Net.WebSockets;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Benchmarks;

[MemoryDiagnoser]
[Config(typeof(GatewayDeliveryBenchmarkConfig))]
public class GatewayDeliveryBenchmarks
{
    private static readonly ObservationDeliveryLane[] ContendedLanes =
    [
        ObservationDeliveryLane.Snapshot,
        ObservationDeliveryLane.Population,
        ObservationDeliveryLane.Economy,
        ObservationDeliveryLane.Logistics,
        ObservationDeliveryLane.Power,
        ObservationDeliveryLane.WaterSewer,
        ObservationDeliveryLane.Gas,
        ObservationDeliveryLane.Optical,
        ObservationDeliveryLane.Radio,
        ObservationDeliveryLane.WorldEnvironment,
    ];

    private EntityPublishSnapshot _snapshot = null!;
    private ClientSubscriptionState[] _subscriptions = null!;
    private Guid[] _connectionIds = null!;

    [Params(8, 32)]
    public int ViewerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const int side = 100;
        var agents = new AgentSnapshot[side * side];
        var knownIds = new HashSet<ulong>(agents.Length);
        var index = 0;
        for (var x = 0; x < side; x++)
        {
            for (var y = 0; y < side; y++)
            {
                var id = (ulong)index + 1;
                agents[index] = new AgentSnapshot(
                    new AgentId(id),
                    new WorldPoint(x * 10d, y * 10d, 0d),
                    new WorldVector(1d, 0d, 0d),
                    100);
                knownIds.Add(id);
                index++;
            }
        }

        _snapshot = new EntityPublishSnapshot(100, agents, [], [], [], []);
        var volume = new WorldVolume(-10d, -10d, -10d, 1_000d, 1_000d, 10d);
        _subscriptions = new ClientSubscriptionState[ViewerCount];
        _connectionIds = new Guid[ViewerCount];
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            _subscriptions[viewer] = new ClientSubscriptionState(
                volume,
                10,
                new CommittedDeliveryRevision(10, 2, 99),
                new HashSet<ulong>(knownIds),
                [],
                [],
                null,
                null);
            _connectionIds[viewer] = Guid.NewGuid();
        }
    }

    [Benchmark]
    [BenchmarkCategory("BroadSubscription")]
    public int BroadSubscriptionPlanning()
    {
        var messages = 0;
        for (var viewer = 0; viewer < _subscriptions.Length; viewer++)
        {
            var plan = ObservationDeliveryPlanner.CreateDynamicPlan(
                _snapshot,
                _subscriptions[viewer],
                new ProtocolVersion(2, 0),
                observationGeneration: 2);
            messages = checked(messages + plan.Agents.Messages.Count);
        }
        return messages;
    }

    [Benchmark]
    [BenchmarkCategory("ReconnectStorm")]
    public int ReconnectConnectionLocalState()
    {
        var captured = 0;
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            using var socket = new BenchmarkWebSocket();
            using var connection = new ClientConnection(Guid.NewGuid(), socket);
            connection.SetSubscription(_subscriptions[viewer].Volume);
            if (connection.TryCaptureSubscription(out var state) && state.KnownAgentIds.Count == 0)
                captured++;
        }
        return captured;
    }

    [Benchmark]
    [BenchmarkCategory("FairnessBudget")]
    public async Task<int> ContendedLaneHandoffs()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var handoffs = 0;
        foreach (var connectionId in _connectionIds)
        {
            var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!scheduler.TryReserve(connectionId, ContendedLanes[0])) continue;
            scheduler.StartReserved(connectionId, async () =>
            {
                await Task.Yield();
                await releaseFirst.Task;
            });

            for (var laneIndex = 1; laneIndex < ContendedLanes.Length; laneIndex++)
            {
                if (!scheduler.TryReserve(connectionId, ContendedLanes[laneIndex])) handoffs++;
            }

            releaseFirst.SetResult();
            while (scheduler.InFlightCount != 0) await Task.Yield();

            for (var laneIndex = 1; laneIndex < ContendedLanes.Length; laneIndex++)
            {
                while (!scheduler.TryReserve(connectionId, ContendedLanes[laneIndex])) await Task.Yield();
                handoffs++;
                scheduler.ReleaseReservation(connectionId);
            }

            if (scheduler.TryReserve(connectionId, ContendedLanes[0]))
            {
                handoffs++;
                scheduler.ReleaseReservation(connectionId);
            }
        }

        return handoffs;
    }

    private sealed class GatewayDeliveryBenchmarkConfig : ManualConfig
    {
        public GatewayDeliveryBenchmarkConfig()
        {
            AddColumn(StatisticColumn.P95);
        }
    }

    private sealed class BenchmarkWebSocket : WebSocket
    {
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
