from __future__ import annotations

import json
from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    target = Path(path)
    text = target.read_text(encoding='utf-8')
    if old not in text:
        raise SystemExit(f'pattern not found in {path}: {old[:100]!r}')
    target.write_text(text.replace(old, new, 1), encoding='utf-8')


# #313: concurrency-rejected MCP requests must not consume the per-minute quota.
replace_once(
    'src/MachiVerseWorks.Server/RemoteMcp.cs',
    '''    public bool TryAcquire(string credential, out IDisposable? lease, out int statusCode)\n    {\n        lease = null;\n        statusCode = StatusCodes.Status200OK;\n        lock (_rateLock)\n        {\n            var now = DateTimeOffset.UtcNow;\n            if (!_rates.TryGetValue(credential, out var rate) || now - rate.WindowStart >= TimeSpan.FromMinutes(1)) rate = (now, 0);\n            if (rate.Count >= options.RequestsPerMinute)\n            {\n                _rates[credential] = rate;\n                statusCode = StatusCodes.Status429TooManyRequests;\n                return false;\n            }\n            _rates[credential] = (rate.WindowStart, rate.Count + 1);\n        }\n        if (!_concurrency.Wait(0))\n        {\n            statusCode = StatusCodes.Status503ServiceUnavailable;\n            return false;\n        }\n        lease = new Lease(_concurrency);\n        return true;\n    }''',
    '''    public bool TryAcquire(string credential, out IDisposable? lease, out int statusCode)\n    {\n        lease = null;\n        statusCode = StatusCodes.Status200OK;\n        if (!_concurrency.Wait(0))\n        {\n            statusCode = StatusCodes.Status503ServiceUnavailable;\n            return false;\n        }\n\n        try\n        {\n            lock (_rateLock)\n            {\n                var now = DateTimeOffset.UtcNow;\n                if (!_rates.TryGetValue(credential, out var rate) || now - rate.WindowStart >= TimeSpan.FromMinutes(1)) rate = (now, 0);\n                if (rate.Count >= options.RequestsPerMinute)\n                {\n                    _rates[credential] = rate;\n                    statusCode = StatusCodes.Status429TooManyRequests;\n                    return false;\n                }\n                _rates[credential] = (rate.WindowStart, rate.Count + 1);\n            }\n\n            lease = new Lease(_concurrency);\n            return true;\n        }\n        finally\n        {\n            if (lease is null) _concurrency.Release();\n        }\n    }'''
)

Path('tests/MachiVerseWorks.Server.Tests/RemoteMcpRequestGateTests.cs').write_text(r'''using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RemoteMcpRequestGateTests
{
    [TestMethod]
    public void ConcurrencyRejectionsDoNotConsumeMinuteQuota()
    {
        using var gate = CreateGate(maxConcurrent: 1, requestsPerMinute: 2);
        Assert.IsTrue(gate.TryAcquire("credential", out var first, out var firstStatus));
        Assert.AreEqual(200, firstStatus);
        Assert.IsNotNull(first);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            Assert.IsFalse(gate.TryAcquire("credential", out var rejected, out var status));
            Assert.IsNull(rejected);
            Assert.AreEqual(503, status);
        }

        first.Dispose();
        Assert.IsTrue(gate.TryAcquire("credential", out var second, out var secondStatus));
        Assert.AreEqual(200, secondStatus);
        Assert.IsNotNull(second);
        second.Dispose();

        Assert.IsFalse(gate.TryAcquire("credential", out var limited, out var limitedStatus));
        Assert.IsNull(limited);
        Assert.AreEqual(429, limitedStatus);
    }

    [TestMethod]
    public async Task ConcurrentBurstAdmitsOnlyAvailableSlotsWithoutPoisoningQuota()
    {
        using var gate = CreateGate(maxConcurrent: 1, requestsPerMinute: 64);
        Assert.IsTrue(gate.TryAcquire("credential", out var held, out _));
        Assert.IsNotNull(held);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            var accepted = gate.TryAcquire("credential", out var lease, out var status);
            lease?.Dispose();
            return (accepted, status);
        })));
        Assert.IsTrue(attempts.All(result => !result.accepted && result.status == 503));

        held.Dispose();
        Assert.IsTrue(gate.TryAcquire("credential", out var retry, out var retryStatus));
        Assert.AreEqual(200, retryStatus);
        retry?.Dispose();
    }

    private static RemoteMcpRequestGate CreateGate(int maxConcurrent, int requestsPerMinute)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:Mcp:Enabled"] = "true",
            ["Server:Mcp:ReadToken"] = "read-token-0123456789-0123456789-abcdef",
            ["Server:Mcp:MaxConcurrentRequests"] = maxConcurrent.ToString(),
            ["Server:Mcp:RequestsPerMinute"] = requestsPerMinute.ToString(),
        }).Build();
        return new RemoteMcpRequestGate(RemoteMcpOptions.Load(configuration));
    }
}
''', encoding='utf-8')

# #317: rate limiting is overload control, not an invalid-request strike.
replace_once(
    'src/MachiVerseWorks.Server/WebSocketSessionHandler.cs',
    '''        if (!connection.TryConsumeRequest(options.RequestRateLimitPerSecond, options.RequestRateLimitBurst))\n        {\n            return await RejectRecoverableAsync(connection, [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "rateLimited")], cancellationToken);\n        }''',
    '''        if (!connection.TryConsumeRequest(options.RequestRateLimitPerSecond, options.RequestRateLimitBurst))\n        {\n            await SendErrorAsync(connection, ProtocolErrorCode.InvalidRequest,\n                [new ProtocolErrorParameter(ProtocolErrorParameterKeys.DetailCode, "rateLimited")],\n                connection.NegotiatedVersion, cancellationToken);\n            return true;\n        }'''
)

Path('tests/MachiVerseWorks.Server.Tests/WebSocketRateLimitPolicyTests.cs').write_text(r'''using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class WebSocketRateLimitPolicyTests
{
    [TestMethod]
    public void RateLimitedHandlerPathDoesNotRegisterInvalidRequestStrike()
    {
        var sourcePath = Path.Combine(FindRepositoryRoot(), "src", "MachiVerseWorks.Server", "WebSocketSessionHandler.cs");
        var source = File.ReadAllText(sourcePath);
        var rateStart = source.IndexOf("if (!connection.TryConsumeRequest", StringComparison.Ordinal);
        Assert.IsTrue(rateStart >= 0);
        var observationCheck = source.IndexOf("if (envelope.Message is not IObservationRequestMessage)", rateStart, StringComparison.Ordinal);
        Assert.IsTrue(observationCheck > rateStart);
        var rateBlock = source[rateStart..observationCheck];
        StringAssert.Contains(rateBlock, "detailCode, \"rateLimited\"");
        StringAssert.Contains(rateBlock, "return true;");
        Assert.IsFalse(rateBlock.Contains("RejectRecoverableAsync", StringComparison.Ordinal));
        Assert.IsFalse(rateBlock.Contains("RegisterInvalidRequest", StringComparison.Ordinal));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MachiVerseWorks.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
''', encoding='utf-8')

# #319: exercise the actual encode/send/backpressure path with slow and fast clients.
Path('benchmarks/MachiVerseWorks.Benchmarks/GatewayDeliveryBenchmarks.cs').write_text(r'''using System.Net.WebSockets;
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
    private AgentUpdateMessage _deliveryMessage = null!;

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
                agents[index] = new AgentSnapshot(new AgentId(id), new WorldPoint(x * 10d, y * 10d, 0d), new WorldVector(1d, 0d, 0d), 100);
                knownIds.Add(id);
                index++;
            }
        }

        _snapshot = new EntityPublishSnapshot(100, agents, [], [], [], []);
        _deliveryMessage = new AgentUpdateMessage(1, 100, 100, 0, 1, 0, 0, 101);
        var volume = new WorldVolume(-10d, -10d, -10d, 1_000d, 1_000d, 10d);
        _subscriptions = new ClientSubscriptionState[ViewerCount];
        _connectionIds = new Guid[ViewerCount];
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            _subscriptions[viewer] = new ClientSubscriptionState(volume, 10, new CommittedDeliveryRevision(10, 2, 99), new HashSet<ulong>(knownIds), [], [], null, null);
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
            var plan = ObservationDeliveryPlanner.CreateDynamicPlan(_snapshot, _subscriptions[viewer], new ProtocolVersion(2, 0), observationGeneration: 2);
            messages = checked(messages + plan.Agents.Messages.Count);
        }
        return messages;
    }

    [Benchmark]
    [BenchmarkCategory("SlowClient", "ActualDelivery")]
    public async Task<double> SlowClientBackpressureDelivery()
    {
        using var slowSocket = new BenchmarkWebSocket(TimeSpan.FromMilliseconds(2));
        using var slow = new ClientConnection(Guid.NewGuid(), slowSocket);
        var fastSockets = Enumerable.Range(0, Math.Max(1, ViewerCount - 1)).Select(_ => new BenchmarkWebSocket(TimeSpan.Zero)).ToArray();
        var fastConnections = fastSockets.Select(socket => new ClientConnection(Guid.NewGuid(), socket)).ToArray();
        try
        {
            var fastTasks = fastConnections.Select(connection => connection.SendAsync(_deliveryMessage, ProtocolVersion.Current, CancellationToken.None)).ToArray();
            var slowTask = slow.SendAsync(_deliveryMessage, ProtocolVersion.Current, CancellationToken.None);
            var fast = await Task.WhenAll(fastTasks);
            var slowMetrics = await slowTask;
            return slowMetrics.SendTimeMs + fast.Sum(metric => metric.SendTimeMs) + slowMetrics.EncodeTimeMs + fast.Sum(metric => metric.EncodeTimeMs);
        }
        finally
        {
            foreach (var connection in fastConnections) connection.Dispose();
            foreach (var socket in fastSockets) socket.Dispose();
        }
    }

    [Benchmark]
    [BenchmarkCategory("ReconnectStorm", "ActualDelivery")]
    public async Task<int> GenerationResyncReconnectDelivery()
    {
        var deliveredBytes = 0;
        for (var viewer = 0; viewer < ViewerCount; viewer++)
        {
            using var socket = new BenchmarkWebSocket(TimeSpan.Zero);
            using var connection = new ClientConnection(Guid.NewGuid(), socket);
            connection.SetSubscription(_subscriptions[viewer].Volume);
            var hello = await connection.SendAsync(new HelloAckMessage(ProtocolVersion.Current, 20), ProtocolVersion.Current, CancellationToken.None);
            var spawn = await connection.SendAsync(new AgentSpawnMessage((ulong)viewer + 1, viewer, viewer, 0, 0, 0, 0, 100), ProtocolVersion.Current, CancellationToken.None);
            deliveredBytes = checked(deliveredBytes + hello.FrameBytes + spawn.FrameBytes);
        }
        return deliveredBytes;
    }

    [Benchmark]
    [BenchmarkCategory("FairnessBudget", "ActualDelivery")]
    public async Task<int> SnapshotPopulationLaneFairness()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var deliveries = 0;
        foreach (var connectionId in _connectionIds)
        {
            using var socket = new BenchmarkWebSocket(TimeSpan.FromMilliseconds(1));
            using var connection = new ClientConnection(connectionId, socket);
            var snapshotStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseSnapshot = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!scheduler.TryReserve(connectionId, ObservationDeliveryLane.Snapshot)) continue;
            scheduler.StartReserved(connectionId, async () =>
            {
                snapshotStarted.SetResult();
                await connection.SendAsync(_deliveryMessage, ProtocolVersion.Current, CancellationToken.None);
                await releaseSnapshot.Task;
            });
            await snapshotStarted.Task;
            if (!scheduler.TryReserve(connectionId, ObservationDeliveryLane.Population)) deliveries++;
            releaseSnapshot.SetResult();
            while (scheduler.InFlightCount != 0) await Task.Yield();
            while (!scheduler.TryReserve(connectionId, ObservationDeliveryLane.Population)) await Task.Yield();
            try
            {
                _ = await connection.SendAsync(_deliveryMessage, ProtocolVersion.Current, CancellationToken.None);
                deliveries++;
            }
            finally
            {
                scheduler.ReleaseReservation(connectionId);
            }
        }
        return deliveries;
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
            scheduler.StartReserved(connectionId, async () => { await Task.Yield(); await releaseFirst.Task; });
            for (var laneIndex = 1; laneIndex < ContendedLanes.Length; laneIndex++) if (!scheduler.TryReserve(connectionId, ContendedLanes[laneIndex])) handoffs++;
            releaseFirst.SetResult();
            while (scheduler.InFlightCount != 0) await Task.Yield();
            for (var laneIndex = 1; laneIndex < ContendedLanes.Length; laneIndex++)
            {
                while (!scheduler.TryReserve(connectionId, ContendedLanes[laneIndex])) await Task.Yield();
                handoffs++;
                scheduler.ReleaseReservation(connectionId);
            }
            if (scheduler.TryReserve(connectionId, ContendedLanes[0])) { handoffs++; scheduler.ReleaseReservation(connectionId); }
        }
        return handoffs;
    }

    private sealed class GatewayDeliveryBenchmarkConfig : ManualConfig
    {
        public GatewayDeliveryBenchmarkConfig() => AddColumn(StatisticColumn.P95);
    }

    private sealed class BenchmarkWebSocket(TimeSpan sendDelay) : WebSocket
    {
        private WebSocketState _state = WebSocketState.Open;
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() => _state = WebSocketState.Aborted;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) { _state = WebSocketState.Closed; return Task.CompletedTask; }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) { _state = WebSocketState.CloseSent; return Task.CompletedTask; }
        public override void Dispose() => _state = WebSocketState.Closed;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            if (sendDelay > TimeSpan.Zero) await Task.Delay(sendDelay, cancellationToken);
        }
    }
}
''', encoding='utf-8')

# #327: localize all remaining infrastructure debug overlays.
replace_once('src/web/src/application.ts', 'this.logisticsDebug = new LogisticsDebugOverlay(host);', 'this.logisticsDebug = new LogisticsDebugOverlay(host, this.localizer);')
replace_once('src/web/src/application.ts', 'this.powerDebug = new PowerDebugOverlay(host);', 'this.powerDebug = new PowerDebugOverlay(host, this.localizer);')
replace_once('src/web/src/application.ts', 'this.opticalDebug = new OpticalDebugOverlay(host);', 'this.opticalDebug = new OpticalDebugOverlay(host, this.localizer);')
replace_once('src/web/src/application.ts', 'this.radioDebug = new RadioDebugOverlay(host);', 'this.radioDebug = new RadioDebugOverlay(host, this.localizer);')

replace_once('src/web/src/logistics-debug.ts', "import { ShipmentState, type LogisticsSnapshotMessage } from './logistics-protocol.ts';", "import { initializeLocalization, type Localizer } from './localization.ts';\nimport { type LogisticsSnapshotMessage } from './logistics-protocol.ts';")
replace_once('src/web/src/logistics-debug.ts', 'public constructor(host: HTMLElement) {', 'public constructor(host: HTMLElement, private readonly localizer: Localizer = initializeLocalization()) {')
replace_once('src/web/src/logistics-debug.ts', '''      `Logistics tick=${statistics.tickCount.toString()} cycle=${statistics.logisticsCycle.toString()}`,\n      `inventory=${statistics.inventoryUnits.toFixed(1)} orders=${String(statistics.openOrderCount)} shipments=${String(statistics.shipmentCount)} delayed=${String(statistics.delayedShipmentCount)}`,''', '''      this.localizer.t('logisticsDebug.summary', { tick: this.localizer.formatNumber(statistics.tickCount), cycle: this.localizer.formatNumber(statistics.logisticsCycle) }),\n      this.localizer.t('logisticsDebug.inventory', { inventory: this.localizer.formatNumber(statistics.inventoryUnits), orders: this.localizer.formatNumber(statistics.openOrderCount), shipments: this.localizer.formatNumber(statistics.shipmentCount), delayed: this.localizer.formatNumber(statistics.delayedShipmentCount) }),''')
replace_once('src/web/src/logistics-debug.ts', "lines.push(`INV est=${inventory.establishmentId.toString()} commodity=${inventory.commodityId.toString()} ${inventory.quantity.toFixed(1)}/${inventory.capacity.toFixed(1)}`);", "lines.push(this.localizer.t('logisticsDebug.inventoryDetail', { establishment: this.localizer.formatNumber(inventory.establishmentId), commodity: this.localizer.formatNumber(inventory.commodityId), quantity: this.localizer.formatNumber(inventory.quantity), capacity: this.localizer.formatNumber(inventory.capacity) }));")
replace_once('src/web/src/logistics-debug.ts', "lines.push(`SHP ${shipment.shipmentId.toString()} ${ShipmentState[shipment.state]} vehicle=${shipment.vehicleId === 0n ? '-' : shipment.vehicleId.toString()} qty=${shipment.quantity.toFixed(1)} delay=${shipment.delayTicks.toString()}`);", "lines.push(this.localizer.t('logisticsDebug.shipmentDetail', { shipment: this.localizer.formatNumber(shipment.shipmentId), state: this.localizer.t(`logisticsDebug.shipmentState.${String(shipment.state)}`), vehicle: shipment.vehicleId === 0n ? '-' : this.localizer.formatNumber(shipment.vehicleId), quantity: this.localizer.formatNumber(shipment.quantity), delay: this.localizer.formatNumber(shipment.delayTicks) }));")
replace_once('src/web/src/logistics-debug.ts', "public clear(): void { this.element.textContent = 'Logistics: waiting for snapshot'; }", "public clear(): void { this.element.textContent = this.localizer.t('logisticsDebug.waiting'); }")

replace_once('src/web/src/power-debug.ts', "import {\n  GeneratorOperatingState,", "import { initializeLocalization, type Localizer } from './localization.ts';\nimport {\n  GeneratorOperatingState,")
replace_once('src/web/src/power-debug.ts', 'public constructor(host: HTMLElement) {', 'public constructor(host: HTMLElement, private readonly localizer: Localizer = initializeLocalization()) {')
replace_once('src/web/src/power-debug.ts', "this.svg.setAttribute('aria-label', 'Power network debug view');", "this.svg.setAttribute('aria-label', this.localizer.t('powerDebug.ariaLabel'));")
replace_once('src/web/src/power-debug.ts', '''      `Power tick=${statistics.tickCount.toString()} outages=${String(statistics.outageLoadCount)}`,\n      `generation=${statistics.generationOutputMegawatts.toFixed(2)}/${statistics.generationCapacityMegawatts.toFixed(2)} MW`,\n      `demand=${statistics.demandMegawatts.toFixed(2)} served=${statistics.servedMegawatts.toFixed(2)} unserved=${statistics.unservedMegawatts.toFixed(2)} MW`,''', '''      this.localizer.t('powerDebug.summary', { tick: this.localizer.formatNumber(statistics.tickCount), outages: this.localizer.formatNumber(statistics.outageLoadCount) }),\n      this.localizer.t('powerDebug.generation', { output: this.localizer.formatNumber(statistics.generationOutputMegawatts), capacity: this.localizer.formatNumber(statistics.generationCapacityMegawatts) }),\n      this.localizer.t('powerDebug.demand', { demand: this.localizer.formatNumber(statistics.demandMegawatts), served: this.localizer.formatNumber(statistics.servedMegawatts), unserved: this.localizer.formatNumber(statistics.unservedMegawatts) }),''')
replace_once('src/web/src/power-debug.ts', "this.summary.textContent = 'Power: waiting for snapshot';", "this.summary.textContent = this.localizer.t('powerDebug.waiting');")

replace_once('src/web/src/optical-debug.ts', "import { OpticalQualityState, type OpticalSnapshotMessage } from './optical-protocol.ts';", "import { initializeLocalization, type Localizer } from './localization.ts';\nimport { OpticalQualityState, type OpticalSnapshotMessage } from './optical-protocol.ts';")
replace_once('src/web/src/optical-debug.ts', 'public constructor(host: HTMLElement) {', 'public constructor(host: HTMLElement, private readonly localizer: Localizer = initializeLocalization()) {')
replace_once('src/web/src/optical-debug.ts', "this.svg.setAttribute('aria-label', 'Optical communication network debug view');", "this.svg.setAttribute('aria-label', this.localizer.t('opticalDebug.ariaLabel'));")
replace_once('src/web/src/optical-debug.ts', '''      `Optical tick ${s.tickCount} | connected ${s.connectedDemandCount}/${s.demandCount} | unavailable ${s.unavailableDemandCount}`,\n      `Traffic ${s.allocatedGigabitsPerSecond.toFixed(2)}/${s.demandGigabitsPerSecond.toFixed(2)} Gbps | backhaul ${s.backhaulCapacityGigabitsPerSecond.toFixed(2)} Gbps`,\n      `Congested ${s.congestedDemandCount} | degraded ${s.degradedDemandCount} | peak fiber ${(s.peakFiberUtilization * 100).toFixed(1)}%`,''', '''      this.localizer.t('opticalDebug.summary', { tick: this.localizer.formatNumber(s.tickCount), connected: this.localizer.formatNumber(s.connectedDemandCount), demand: this.localizer.formatNumber(s.demandCount), unavailable: this.localizer.formatNumber(s.unavailableDemandCount) }),\n      this.localizer.t('opticalDebug.traffic', { allocated: this.localizer.formatNumber(s.allocatedGigabitsPerSecond), demand: this.localizer.formatNumber(s.demandGigabitsPerSecond), backhaul: this.localizer.formatNumber(s.backhaulCapacityGigabitsPerSecond) }),\n      this.localizer.t('opticalDebug.quality', { congested: this.localizer.formatNumber(s.congestedDemandCount), degraded: this.localizer.formatNumber(s.degradedDemandCount), peak: this.localizer.formatNumber(s.peakFiberUtilization * 100) }),''')
replace_once('src/web/src/optical-debug.ts', "public clear(): void { this.summary.textContent = 'Optical: waiting for snapshot'; this.svg.replaceChildren(); }", "public clear(): void { this.summary.textContent = this.localizer.t('opticalDebug.waiting'); this.svg.replaceChildren(); }")

replace_once('src/web/src/radio-debug.ts', "import { RadioAntennaPatternKind, RadioLinkState, type RadioSnapshotMessage, type SpectrumSnapshotMessage } from './radio-protocol.ts';", "import { initializeLocalization, type Localizer } from './localization.ts';\nimport { RadioAntennaPatternKind, RadioLinkState, type RadioSnapshotMessage, type SpectrumSnapshotMessage } from './radio-protocol.ts';")
replace_once('src/web/src/radio-debug.ts', 'public constructor(host:HTMLElement){', 'public constructor(host:HTMLElement,private readonly localizer:Localizer=initializeLocalization()){')
replace_once('src/web/src/radio-debug.ts', "this.svg.setAttribute('aria-label','Radio spectrum debug view');", "this.svg.setAttribute('aria-label',this.localizer.t('radioDebug.ariaLabel'));")
replace_once('src/web/src/radio-debug.ts', '''      `Radio tick ${s.tickCount} | sites ${s.siteCount} | tx ${message.transmitters.length} | rx ${message.receivers.length} | emissions ${message.emissions.length}`,\n      `links H/I/U ${s.healthyLinkCount}/${s.interferedLinkCount}/${s.unreachableLinkCount} | peak ${(s.peakSpectrumUtilization*100).toFixed(1)}% | conflicts ${s.conflictCount}`,\n      `channels ${channels.slice(0,4).join(', ')||'-'}${channels.length>4?' ...':''}`,\n      `spectrum ${spectrum===null?'waiting':`${spectrum.bands.length} bands / ${spectrum.frequencyBlocks.length} blocks / ${spectrum.conflicts.length} conflicts`}`,''', '''      this.localizer.t('radioDebug.summary',{tick:this.localizer.formatNumber(s.tickCount),sites:this.localizer.formatNumber(s.siteCount),transmitters:this.localizer.formatNumber(message.transmitters.length),receivers:this.localizer.formatNumber(message.receivers.length),emissions:this.localizer.formatNumber(message.emissions.length)}),\n      this.localizer.t('radioDebug.links',{healthy:this.localizer.formatNumber(s.healthyLinkCount),interfered:this.localizer.formatNumber(s.interferedLinkCount),unreachable:this.localizer.formatNumber(s.unreachableLinkCount),peak:this.localizer.formatNumber(s.peakSpectrumUtilization*100),conflicts:this.localizer.formatNumber(s.conflictCount)}),\n      this.localizer.t('radioDebug.channels',{channels:channels.slice(0,4).join(', ')||'-',more:channels.length>4?' ...':''}),\n      spectrum===null?this.localizer.t('radioDebug.spectrumWaiting'):this.localizer.t('radioDebug.spectrum',{bands:this.localizer.formatNumber(spectrum.bands.length),blocks:this.localizer.formatNumber(spectrum.frequencyBlocks.length),conflicts:this.localizer.formatNumber(spectrum.conflicts.length)}),''')
replace_once('src/web/src/radio-debug.ts', "public clear():void{this.spectrum=null;this.summary.textContent='Radio/Spectrum: waiting for snapshot';this.svg.replaceChildren();}", "public clear():void{this.spectrum=null;this.summary.textContent=this.localizer.t('radioDebug.waiting');this.svg.replaceChildren();}")

ja_path = Path('src/web/locales/ja-JP.json')
ja = json.loads(ja_path.read_text(encoding='utf-8'))
ja.update({
    'logisticsDebug.waiting': '物流: スナップショット待機中',
    'logisticsDebug.summary': '物流 Tick {tick} / Cycle {cycle}',
    'logisticsDebug.inventory': '在庫 {inventory} / 注文 {orders} / Shipment {shipments} / 遅延 {delayed}',
    'logisticsDebug.inventoryDetail': '在庫 事業所={establishment} 商品={commodity} {quantity}/{capacity}',
    'logisticsDebug.shipmentDetail': 'Shipment {shipment} {state} 車両={vehicle} 数量={quantity} 遅延={delay}',
    'logisticsDebug.shipmentState.0': '待機', 'logisticsDebug.shipmentState.1': '輸送中', 'logisticsDebug.shipmentState.2': '完了', 'logisticsDebug.shipmentState.3': '遅延',
    'powerDebug.ariaLabel': '電力ネットワークのデバッグ表示', 'powerDebug.waiting': '電力: スナップショット待機中',
    'powerDebug.summary': '電力 Tick {tick} / 停電負荷 {outages}', 'powerDebug.generation': '発電 {output}/{capacity} MW', 'powerDebug.demand': '需要 {demand} / 供給済み {served} / 未供給 {unserved} MW',
    'opticalDebug.ariaLabel': '光通信ネットワークのデバッグ表示', 'opticalDebug.waiting': '光通信: スナップショット待機中',
    'opticalDebug.summary': '光通信 Tick {tick} / 接続 {connected}/{demand} / 利用不可 {unavailable}', 'opticalDebug.traffic': '通信量 {allocated}/{demand} Gbps / バックホール {backhaul} Gbps', 'opticalDebug.quality': '輻輳 {congested} / 劣化 {degraded} / 最大Fiber利用率 {peak}%',
    'radioDebug.ariaLabel': '無線スペクトラムのデバッグ表示', 'radioDebug.waiting': '無線/スペクトラム: スナップショット待機中',
    'radioDebug.summary': '無線 Tick {tick} / Site {sites} / 送信 {transmitters} / 受信 {receivers} / Emission {emissions}', 'radioDebug.links': 'Link 正常 {healthy} / 干渉 {interfered} / 到達不能 {unreachable} / 最大利用率 {peak}% / 競合 {conflicts}', 'radioDebug.channels': 'Channel {channels}{more}', 'radioDebug.spectrumWaiting': 'Spectrum スナップショット待機中', 'radioDebug.spectrum': 'Spectrum Band {bands} / Block {blocks} / 競合 {conflicts}',
})
ja_path.write_text(json.dumps(ja, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

Path('src/web/tests/debug-localization-contract.test.mjs').write_text(r'''import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const overlays = ['logistics-debug.ts', 'power-debug.ts', 'optical-debug.ts', 'radio-debug.ts'];

test('infrastructure debug overlays receive Localizer and avoid hard-coded waiting text', async () => {
  for (const file of overlays) {
    const source = await readFile(new URL(`../src/${file}`, import.meta.url), 'utf8');
    assert.match(source, /Localizer/);
    assert.match(source, /localizer\.t\('/);
    assert.match(source, /localizer\.formatNumber\(/);
    assert.doesNotMatch(source, /waiting for snapshot/);
  }
  const application = await readFile(new URL('../src/application.ts', import.meta.url), 'utf8');
  for (const name of ['LogisticsDebugOverlay', 'PowerDebugOverlay', 'OpticalDebugOverlay', 'RadioDebugOverlay']) {
    assert.match(application, new RegExp(`new ${name}\\(host, this\\.localizer\\)`));
  }
});

test('Japanese locale defines every infrastructure debug key', async () => {
  const resource = JSON.parse(await readFile(new URL('../locales/ja-JP.json', import.meta.url), 'utf8'));
  for (const prefix of ['logisticsDebug.', 'powerDebug.', 'opticalDebug.', 'radioDebug.']) {
    assert.ok(Object.keys(resource).some((key) => key.startsWith(prefix)), `missing ${prefix}`);
  }
});
''', encoding='utf-8')

print('worker3 batch3 patch applied')
