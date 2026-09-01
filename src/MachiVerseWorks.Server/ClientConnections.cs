using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Channels;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed record ClientHandshakeState(ProtocolVersion Version);

internal sealed class ConnectionLimitExceededException : InvalidOperationException
{
    public ConnectionLimitExceededException(int maximum)
        : base($"The WebSocket connection limit of {maximum} has been reached.") { }
}

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
    private long _lastRoadSubscriptionRevision = long.MinValue;
    private ulong _lastRoadRevision;
    private long _lastRailwaySubscriptionRevision = long.MinValue;
    private ulong _lastRailwayRevision;
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
        lock (_stateGate) _inspectedPersonId = personId;
    }

    public void ClearPersonInspection()
    {
        lock (_stateGate) _inspectedPersonId = null;
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

    public bool TryCaptureSubscription(out ClientSubscriptionState state)
    {
        lock (_stateGate)
        {
            if (_subscription is not WorldVolume volume)
            {
                state = default;
                return false;
            }
            state = new ClientSubscriptionState(volume, _subscriptionRevision, new HashSet<ulong>(_knownAgentIds), new HashSet<ulong>(_knownPedestrianIds), new HashSet<ulong>(_knownVehicleIds));
            return true;
        }
    }

    public bool NeedsRoadSnapshot(long subscriptionRevision, ulong roadRevision)
    {
        lock (_stateGate) return _lastRoadSubscriptionRevision != subscriptionRevision || _lastRoadRevision != roadRevision;
    }

    public bool TryMarkRoadSnapshotDelivered(long subscriptionRevision, ulong roadRevision)
    {
        lock (_stateGate)
        {
            if (_subscriptionRevision != subscriptionRevision) return false;
            _lastRoadSubscriptionRevision = subscriptionRevision;
            _lastRoadRevision = roadRevision;
            return true;
        }
    }

    public bool NeedsRailwaySnapshot(long subscriptionRevision, ulong railwayRevision)
    {
        lock (_stateGate) return _lastRailwaySubscriptionRevision != subscriptionRevision || _lastRailwayRevision != railwayRevision;
    }

    public bool TryMarkRailwaySnapshotDelivered(long subscriptionRevision, ulong railwayRevision)
    {
        lock (_stateGate)
        {
            if (_subscriptionRevision != subscriptionRevision) return false;
            _lastRailwaySubscriptionRevision = subscriptionRevision;
            _lastRailwayRevision = railwayRevision;
            return true;
        }
    }

    public bool TryReplaceKnownAgentIds(long revision, HashSet<ulong> agentIds) => TryReplaceKnownEntityIds(revision, agentIds, new HashSet<ulong>(_knownPedestrianIds), new HashSet<ulong>(_knownVehicleIds));
    public bool TryReplaceKnownEntityIds(long revision, HashSet<ulong> agentIds, HashSet<ulong> pedestrianIds) => TryReplaceKnownEntityIds(revision, agentIds, pedestrianIds, new HashSet<ulong>(_knownVehicleIds));

    public bool TryReplaceKnownEntityIds(long revision, HashSet<ulong> agentIds, HashSet<ulong> pedestrianIds, HashSet<ulong> vehicleIds)
    {
        ArgumentNullException.ThrowIfNull(agentIds); ArgumentNullException.ThrowIfNull(pedestrianIds); ArgumentNullException.ThrowIfNull(vehicleIds);
        lock (_stateGate)
        {
            _knownAgentIds = agentIds; _knownPedestrianIds = pedestrianIds; _knownVehicleIds = vehicleIds;
            return _subscriptionRevision == revision;
        }
    }

    public async Task<ProtocolSendMetrics> SendAsync(IProtocolMessage message, ProtocolVersion version, CancellationToken cancellationToken)
    {
        BeginSend();
        try
        {
            var encodeStarted = Stopwatch.GetTimestamp();
            var frame = message switch
            {
                IntersectionControlSnapshotMessage intersection => IntersectionControlProtocolCodec.Serialize(intersection, version),
                RailwayInfrastructureSnapshotMessage railway => RailwayInfrastructureProtocolCodec.Serialize(railway, version),
                RailwayOperationsSnapshotMessage railwayOperations => RailwayOperationsProtocolCodec.Serialize(railwayOperations, version),
                MultimodalTransitSnapshotMessage multimodalTransit => MultimodalTransitProtocolCodec.Serialize(multimodalTransit, version),
                EconomySnapshotMessage economy => EconomyProtocolCodec.Serialize(economy, version),
                LogisticsSnapshotMessage logistics => LogisticsProtocolCodec.Serialize(logistics, version),
                PowerSnapshotMessage power => PowerProtocolCodec.Serialize(power, version),
                WaterSewerSnapshotMessage waterSewer => WaterSewerProtocolCodec.Serialize(waterSewer, version),
                GasSnapshotMessage gas => GasProtocolCodec.Serialize(gas, version),
                OpticalSnapshotMessage optical => OpticalProtocolCodec.Serialize(optical, version),
                RadioSnapshotMessage radio => RadioProtocolCodec.Serialize(radio, version),
                SpectrumSnapshotMessage spectrum => RadioProtocolCodec.Serialize(spectrum, version),
                WorldEnvironmentSnapshotMessage worldEnvironment => WorldEnvironmentProtocolCodec.Serialize(worldEnvironment, version),
                InspectPersonMessage or PopulationStatisticsMessage or PersonDebugMessage => PopulationProtocolCodec.Serialize(message, version),
                _ => ProtocolCodec.Serialize(message, version),
            };
            var encodeTimeMs = Stopwatch.GetElapsedTime(encodeStarted).TotalMilliseconds;
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

internal readonly record struct ClientSubscriptionState(WorldVolume Volume, long Revision, HashSet<ulong> KnownAgentIds, HashSet<ulong> KnownPedestrianIds, HashSet<ulong> KnownVehicleIds)
{
    public ClientSubscriptionState(WorldVolume volume, long revision, HashSet<ulong> knownAgentIds) : this(volume, revision, knownAgentIds, [], []) { }
    public ClientSubscriptionState(WorldVolume volume, long revision, HashSet<ulong> knownAgentIds, HashSet<ulong> knownPedestrianIds) : this(volume, revision, knownAgentIds, knownPedestrianIds, []) { }
}

internal sealed class ClientConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new();
    private int _connectionCount;
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
    public bool Remove(Guid id) { if (!_connections.TryRemove(id, out _)) return false; Interlocked.Decrement(ref _connectionCount); return true; }
    public ClientConnection[] CreateSnapshot() => _connections.Values.ToArray();
}

internal abstract record ClientCommand(Guid ConnectionId);
internal sealed record SubscribeVolumeCommand(Guid ConnectionId, WorldVolume Volume) : ClientCommand(ConnectionId);
internal sealed record InspectPersonCommand(Guid ConnectionId, ulong PersonId) : ClientCommand(ConnectionId);
internal sealed record ClearPersonInspectionCommand(Guid ConnectionId) : ClientCommand(ConnectionId);

internal sealed class ClientCommandQueue
{
    private const int Capacity = 1024;
    private readonly Channel<ClientCommand> _channel = Channel.CreateBounded<ClientCommand>(new BoundedChannelOptions(Capacity) { SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });
    public ValueTask WriteAsync(ClientCommand command, CancellationToken cancellationToken) { ArgumentNullException.ThrowIfNull(command); return _channel.Writer.WriteAsync(command, cancellationToken); }
    public IAsyncEnumerable<ClientCommand> ReadAllAsync(CancellationToken cancellationToken) => _channel.Reader.ReadAllAsync(cancellationToken);
}

internal sealed class ClientCommandProcessor(ClientCommandQueue queue, ClientConnectionRegistry connections, ILogger<ClientCommandProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var command in queue.ReadAllAsync(stoppingToken))
            {
                if (!connections.TryGet(command.ConnectionId, out var connection) || connection is null) continue;
                switch (command)
                {
                    case SubscribeVolumeCommand subscribe: connection.SetSubscription(subscribe.Volume); break;
                    case InspectPersonCommand inspect: connection.SetInspectedPerson(inspect.PersonId); break;
                    case ClearPersonInspectionCommand: connection.ClearPersonInspection(); break;
                    default: ServerLog.UnsupportedClientCommand(logger, command.GetType().Name); break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
