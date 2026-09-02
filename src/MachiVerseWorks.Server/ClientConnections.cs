using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed record ClientHandshakeState(ProtocolVersion Version);

internal sealed class ConnectionLimitExceededException : InvalidOperationException
{
    public ConnectionLimitExceededException(int maximum)
        : base($"The WebSocket connection limit of {maximum} has been reached.") { }
}

internal readonly record struct CommittedDeliveryRevision(
    long SubscriptionRevision,
    ulong ObservationGeneration,
    ulong ObservationRevision);

internal readonly record struct StaticDeliveryRevision(
    long SubscriptionRevision,
    ulong ObservationGeneration,
    ulong SourceRevision);

internal readonly record struct ClientInspectionState(ulong? PersonId, long Revision);

internal sealed class ClientConnection : IDisposable
{
    private readonly object _stateGate = new();
    private readonly object _lifetimeGate = new();
    private readonly object _requestGate = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private HashSet<ulong> _knownAgentIds = [];
    private HashSet<ulong> _knownPedestrianIds = [];
    private HashSet<ulong> _knownVehicleIds = [];
    private WorldVolume? _subscription;
    private ulong? _inspectedPersonId;
    private ClientHandshakeState? _handshakeState;
    private long _subscriptionRevision;
    private long _inspectionRevision;
    private CommittedDeliveryRevision? _committedDelivery;
    private StaticDeliveryRevision? _roadDelivery;
    private StaticDeliveryRevision? _railwayDelivery;
    private int _activeSendCount;
    private bool _disposeRequested;
    private bool _sendGateDisposed;
    private double _requestTokens = -1d;
    private long _requestTokenTimestamp = Stopwatch.GetTimestamp();
    private int _invalidRequestStrikeCount;
    private long _invalidRequestWindowTimestamp = Stopwatch.GetTimestamp();

    public ClientConnection(Guid id, WebSocket socket)
    {
        Id = id;
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    public Guid Id { get; }
    public WebSocket Socket { get; }
    public bool HandshakeCompleted => Volatile.Read(ref _handshakeState) is not null;
    public ProtocolVersion NegotiatedVersion => Volatile.Read(ref _handshakeState)?.Version ?? default;

    public void CompleteHandshake(ProtocolVersion negotiatedVersion)
    {
        var state = new ClientHandshakeState(negotiatedVersion);
        if (Interlocked.CompareExchange(ref _handshakeState, state, null) is not null)
            throw new InvalidOperationException("The client handshake has already completed.");
    }

    public bool TryConsumeRequest(int requestsPerSecond, int burst)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestsPerSecond);
        ArgumentOutOfRangeException.ThrowIfLessThan(burst, requestsPerSecond);
        lock (_requestGate)
        {
            var now = Stopwatch.GetTimestamp();
            if (_requestTokens < 0d) _requestTokens = burst;
            var elapsedSeconds = Stopwatch.GetElapsedTime(_requestTokenTimestamp, now).TotalSeconds;
            _requestTokenTimestamp = now;
            _requestTokens = Math.Min(burst, _requestTokens + (elapsedSeconds * requestsPerSecond));
            if (_requestTokens < 1d) return false;
            _requestTokens -= 1d;
            return true;
        }
    }

    public bool RegisterInvalidRequest(int strikeLimit, TimeSpan strikeWindow)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(strikeLimit);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(strikeWindow, TimeSpan.Zero);
        lock (_requestGate)
        {
            var now = Stopwatch.GetTimestamp();
            if (Stopwatch.GetElapsedTime(_invalidRequestWindowTimestamp, now) >= strikeWindow)
            {
                _invalidRequestWindowTimestamp = now;
                _invalidRequestStrikeCount = 0;
            }
            _invalidRequestStrikeCount = checked(_invalidRequestStrikeCount + 1);
            return _invalidRequestStrikeCount < strikeLimit;
        }
    }

    public void SetSubscription(WorldVolume volume)
    {
        lock (_stateGate)
        {
            _subscription = volume;
            _subscriptionRevision = checked(_subscriptionRevision + 1);
        }
    }

    public void SetInspectedPerson(ulong personId)
    {
        if (personId == 0) throw new ArgumentOutOfRangeException(nameof(personId));
        lock (_stateGate)
        {
            _inspectedPersonId = personId;
            _inspectionRevision = checked(_inspectionRevision + 1);
        }
    }

    public void ClearPersonInspection()
    {
        lock (_stateGate)
        {
            _inspectedPersonId = null;
            _inspectionRevision = checked(_inspectionRevision + 1);
        }
    }

    public bool TryGetInspectedPersonId(out ulong personId)
    {
        lock (_stateGate)
        {
            if (_inspectedPersonId is not { } value)
            {
                personId = 0;
                return false;
            }
            personId = value;
            return true;
        }
    }

    public ClientInspectionState CaptureInspectionState()
    {
        lock (_stateGate) return new ClientInspectionState(_inspectedPersonId, _inspectionRevision);
    }

    public bool IsInspectionCurrent(ClientInspectionState state)
    {
        lock (_stateGate) return _inspectionRevision == state.Revision && _inspectedPersonId == state.PersonId;
    }

    public bool TryCaptureSubscription(out ClientSubscriptionState state)
    {
        lock (_stateGate)
        {
            if (_subscription is not WorldVolume volume)
            {
                state = default;
                return false;
            }
            state = new ClientSubscriptionState(
                volume,
                _subscriptionRevision,
                _committedDelivery,
                new HashSet<ulong>(_knownAgentIds),
                new HashSet<ulong>(_knownPedestrianIds),
                new HashSet<ulong>(_knownVehicleIds),
                _roadDelivery,
                _railwayDelivery);
            return true;
        }
    }

    public bool NeedsRoadSnapshot(long subscriptionRevision, ulong roadRevision) => NeedsRoadSnapshot(subscriptionRevision, 0, roadRevision);

    public bool NeedsRoadSnapshot(long subscriptionRevision, ulong observationGeneration, ulong roadRevision)
    {
        lock (_stateGate)
        {
            return _roadDelivery is not { } delivered
                || delivered.SubscriptionRevision != subscriptionRevision
                || delivered.ObservationGeneration != observationGeneration
                || delivered.SourceRevision != roadRevision;
        }
    }

    public bool TryMarkRoadSnapshotDelivered(long subscriptionRevision, ulong roadRevision) => TryMarkRoadSnapshotDelivered(subscriptionRevision, 0, roadRevision);

    public bool TryMarkRoadSnapshotDelivered(long subscriptionRevision, ulong observationGeneration, ulong roadRevision)
    {
        lock (_stateGate)
        {
            if (_subscriptionRevision != subscriptionRevision) return false;
            _roadDelivery = new StaticDeliveryRevision(subscriptionRevision, observationGeneration, roadRevision);
            return true;
        }
    }

    public bool NeedsRailwaySnapshot(long subscriptionRevision, ulong railwayRevision) => NeedsRailwaySnapshot(subscriptionRevision, 0, railwayRevision);

    public bool NeedsRailwaySnapshot(long subscriptionRevision, ulong observationGeneration, ulong railwayRevision)
    {
        lock (_stateGate)
        {
            return _railwayDelivery is not { } delivered
                || delivered.SubscriptionRevision != subscriptionRevision
                || delivered.ObservationGeneration != observationGeneration
                || delivered.SourceRevision != railwayRevision;
        }
    }

    public bool TryMarkRailwaySnapshotDelivered(long subscriptionRevision, ulong railwayRevision) => TryMarkRailwaySnapshotDelivered(subscriptionRevision, 0, railwayRevision);

    public bool TryMarkRailwaySnapshotDelivered(long subscriptionRevision, ulong observationGeneration, ulong railwayRevision)
    {
        lock (_stateGate)
        {
            if (_subscriptionRevision != subscriptionRevision) return false;
            _railwayDelivery = new StaticDeliveryRevision(subscriptionRevision, observationGeneration, railwayRevision);
            return true;
        }
    }

    public bool TryReplaceKnownAgentIds(long revision, HashSet<ulong> agentIds)
        => TryReplaceKnownEntityIds(revision, 0, 0, agentIds, new HashSet<ulong>(_knownPedestrianIds), new HashSet<ulong>(_knownVehicleIds));

    public bool TryReplaceKnownEntityIds(long revision, HashSet<ulong> agentIds, HashSet<ulong> pedestrianIds)
        => TryReplaceKnownEntityIds(revision, 0, 0, agentIds, pedestrianIds, new HashSet<ulong>(_knownVehicleIds));

    public bool TryReplaceKnownEntityIds(long revision, HashSet<ulong> agentIds, HashSet<ulong> pedestrianIds, HashSet<ulong> vehicleIds)
        => TryReplaceKnownEntityIds(revision, 0, 0, agentIds, pedestrianIds, vehicleIds);

    public bool TryReplaceKnownEntityIds(
        long subscriptionRevision,
        ulong observationGeneration,
        ulong observationRevision,
        HashSet<ulong> agentIds,
        HashSet<ulong> pedestrianIds,
        HashSet<ulong> vehicleIds)
    {
        ArgumentNullException.ThrowIfNull(agentIds);
        ArgumentNullException.ThrowIfNull(pedestrianIds);
        ArgumentNullException.ThrowIfNull(vehicleIds);
        lock (_stateGate)
        {
            if (_committedDelivery is { } committed && committed.SubscriptionRevision > subscriptionRevision)
                return false;

            _knownAgentIds = agentIds;
            _knownPedestrianIds = pedestrianIds;
            _knownVehicleIds = vehicleIds;
            _committedDelivery = new CommittedDeliveryRevision(subscriptionRevision, observationGeneration, observationRevision);
            return _subscriptionRevision == subscriptionRevision;
        }
    }

    public async Task<ProtocolSendMetrics> SendAsync(IProtocolMessage message, ProtocolVersion version, CancellationToken cancellationToken)
    {
        BeginSend();
        try
        {
            var encodeStarted = Stopwatch.GetTimestamp();
            var frame = ObservationProtocolAdapter.Serialize(message, version);
            var encodeTimeMs = Stopwatch.GetElapsedTime(encodeStarted).TotalMilliseconds;
            return await SendFrameAsync(frame, encodeTimeMs, cancellationToken);
        }
        finally { EndSend(); }
    }

    public async Task<ProtocolSendMetrics> SendCachedAsync(
        IProtocolMessage message,
        ProtocolVersion version,
        EncodedObservationCacheKey cacheKey,
        ObservationCache cache,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        BeginSend();
        try
        {
            var encodeStarted = Stopwatch.GetTimestamp();
            var frame = cache.GetOrEncode(cacheKey, () => ObservationProtocolAdapter.Serialize(message, version));
            var encodeTimeMs = Stopwatch.GetElapsedTime(encodeStarted).TotalMilliseconds;
            return await SendFrameAsync(frame, encodeTimeMs, cancellationToken);
        }
        finally { EndSend(); }
    }

    public async Task<ProtocolSendMetrics?> SendCachedIfInspectionCurrentAsync(
        IProtocolMessage message,
        ProtocolVersion version,
        EncodedObservationCacheKey cacheKey,
        ObservationCache cache,
        ClientInspectionState inspection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        BeginSend();
        try
        {
            var encodeStarted = Stopwatch.GetTimestamp();
            var frame = cache.GetOrEncode(cacheKey, () => ObservationProtocolAdapter.Serialize(message, version));
            var encodeTimeMs = Stopwatch.GetElapsedTime(encodeStarted).TotalMilliseconds;
            return await SendFrameIfInspectionCurrentAsync(frame, encodeTimeMs, inspection, cancellationToken);
        }
        finally { EndSend(); }
    }

    public async Task<ProtocolSendMetrics?> SendIfEntityInspectionCurrentAsync(
        IProtocolMessage message,
        ProtocolVersion version,
        EntityInspectionRegistry inspections,
        EntityInspectionSelection inspection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inspections);
        BeginSend();
        try
        {
            var encodeStarted = Stopwatch.GetTimestamp();
            var frame = ObservationProtocolAdapter.Serialize(message, version);
            var encodeTimeMs = Stopwatch.GetElapsedTime(encodeStarted).TotalMilliseconds;
            return await SendFrameIfEntityInspectionCurrentAsync(frame, encodeTimeMs, inspections, inspection, cancellationToken);
        }
        finally { EndSend(); }
    }

    public void Abort() => Socket.Abort();

    public void Dispose()
    {
        var disposeSendGate = false;
        lock (_lifetimeGate)
        {
            if (_disposeRequested) return;
            _disposeRequested = true;
            if (_activeSendCount == 0 && !_sendGateDisposed) { _sendGateDisposed = true; disposeSendGate = true; }
        }
        if (disposeSendGate) _sendGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<ProtocolSendMetrics> SendFrameAsync(byte[] frame, double encodeTimeMs, CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            if (Socket.State != WebSocketState.Open) throw new WebSocketException(WebSocketError.InvalidState);
            var sendStarted = Stopwatch.GetTimestamp();
            await Socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
            return new ProtocolSendMetrics(frame.Length, encodeTimeMs, Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds);
        }
        finally { _sendGate.Release(); }
    }

    private async Task<ProtocolSendMetrics?> SendFrameIfInspectionCurrentAsync(
        byte[] frame,
        double encodeTimeMs,
        ClientInspectionState inspection,
        CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            Task sendTask;
            long sendStarted;
            lock (_stateGate)
            {
                if (_inspectionRevision != inspection.Revision || _inspectedPersonId != inspection.PersonId)
                    return null;
                if (Socket.State != WebSocketState.Open) throw new WebSocketException(WebSocketError.InvalidState);
                sendStarted = Stopwatch.GetTimestamp();
                sendTask = Socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
            }

            await sendTask;
            return new ProtocolSendMetrics(frame.Length, encodeTimeMs, Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds);
        }
        finally { _sendGate.Release(); }
    }

    private async Task<ProtocolSendMetrics?> SendFrameIfEntityInspectionCurrentAsync(
        byte[] frame,
        double encodeTimeMs,
        EntityInspectionRegistry inspections,
        EntityInspectionSelection inspection,
        CancellationToken cancellationToken)
    {
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            long sendStarted = 0;
            if (!inspections.TryStartCurrentSend(
                    Id,
                    inspection,
                    () =>
                    {
                        if (Socket.State != WebSocketState.Open) throw new WebSocketException(WebSocketError.InvalidState);
                        sendStarted = Stopwatch.GetTimestamp();
                        return Socket.SendAsync(new ArraySegment<byte>(frame), WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
                    },
                    out var sendTask)
                || sendTask is null)
            {
                return null;
            }

            await sendTask;
            return new ProtocolSendMetrics(frame.Length, encodeTimeMs, Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds);
        }
        finally { _sendGate.Release(); }
    }

    private void BeginSend()
    {
        lock (_lifetimeGate) { ObjectDisposedException.ThrowIf(_disposeRequested, this); _activeSendCount = checked(_activeSendCount + 1); }
    }

    private void EndSend()
    {
        var disposeSendGate = false;
        lock (_lifetimeGate)
        {
            _activeSendCount--;
            if (_disposeRequested && _activeSendCount == 0 && !_sendGateDisposed) { _sendGateDisposed = true; disposeSendGate = true; }
        }
        if (disposeSendGate) _sendGate.Dispose();
    }
}

internal readonly record struct ClientSubscriptionState(
    WorldVolume Volume,
    long Revision,
    CommittedDeliveryRevision? CommittedDelivery,
    HashSet<ulong> KnownAgentIds,
    HashSet<ulong> KnownPedestrianIds,
    HashSet<ulong> KnownVehicleIds,
    StaticDeliveryRevision? RoadDelivery,
    StaticDeliveryRevision? RailwayDelivery)
{
    public ClientSubscriptionState(WorldVolume volume, long revision, HashSet<ulong> knownAgentIds)
        : this(volume, revision, null, knownAgentIds, [], [], null, null) { }

    public ClientSubscriptionState(WorldVolume volume, long revision, HashSet<ulong> knownAgentIds, HashSet<ulong> knownPedestrianIds)
        : this(volume, revision, null, knownAgentIds, knownPedestrianIds, [], null, null) { }

    public ClientSubscriptionState(WorldVolume volume, long revision, HashSet<ulong> knownAgentIds, HashSet<ulong> knownPedestrianIds, HashSet<ulong> knownVehicleIds)
        : this(volume, revision, null, knownAgentIds, knownPedestrianIds, knownVehicleIds, null, null) { }
}

internal sealed class ClientConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new();
    private readonly SnapshotDeliveryScheduler? _deliveryScheduler;
    private int _connectionCount;

    public ClientConnectionRegistry(SnapshotDeliveryScheduler? deliveryScheduler = null)
    {
        _deliveryScheduler = deliveryScheduler;
    }

    public int Count => Volatile.Read(ref _connectionCount);
    public ClientConnection Register(WebSocket socket) => Register(socket, int.MaxValue);

    public ClientConnection Register(WebSocket socket, int maximumConnections)
    {
        ArgumentNullException.ThrowIfNull(socket); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumConnections);
        var count = Interlocked.Increment(ref _connectionCount);
        if (count > maximumConnections) { Interlocked.Decrement(ref _connectionCount); throw new ConnectionLimitExceededException(maximumConnections); }
        var connection = new ClientConnection(Guid.NewGuid(), socket);
        if (_connections.TryAdd(connection.Id, connection)) return connection;
        Interlocked.Decrement(ref _connectionCount); connection.Dispose(); throw new InvalidOperationException("Failed to register a unique client connection.");
    }

    public bool TryGet(Guid id, out ClientConnection? connection) => _connections.TryGetValue(id, out connection);

    public bool Remove(Guid id)
    {
        if (!_connections.TryRemove(id, out _)) return false;
        _deliveryScheduler?.Discard(id);
        Interlocked.Decrement(ref _connectionCount);
        return true;
    }

    public ClientConnection[] CreateSnapshot() => _connections.Values.ToArray();
}
