using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading.Channels;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class ClientConnection : IDisposable
{
    private readonly object _stateGate = new();
    private readonly object _lifetimeGate = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private HashSet<ulong> _knownAgentIds = [];
    private WorldRect? _subscription;
    private long _subscriptionRevision;
    private int _activeSendCount;
    private bool _disposeRequested;
    private bool _sendGateDisposed;

    public ClientConnection(Guid id, WebSocket socket)
    {
        Id = id;
        Socket = socket ?? throw new ArgumentNullException(nameof(socket));
    }

    public Guid Id { get; }

    public WebSocket Socket { get; }

    public bool HandshakeCompleted { get; private set; }

    public ProtocolVersion NegotiatedVersion { get; private set; }

    public void CompleteHandshake(ProtocolVersion negotiatedVersion)
    {
        NegotiatedVersion = negotiatedVersion;
        HandshakeCompleted = true;
    }

    public void SetSubscription(WorldRect area)
    {
        lock (_stateGate)
        {
            _subscription = area;
            _subscriptionRevision = checked(_subscriptionRevision + 1);
        }
    }

    public bool TryCaptureSubscription(out ClientSubscriptionState state)
    {
        lock (_stateGate)
        {
            if (_subscription is not WorldRect area)
            {
                state = default;
                return false;
            }

            state = new ClientSubscriptionState(
                area,
                _subscriptionRevision,
                new HashSet<ulong>(_knownAgentIds));
            return true;
        }
    }

    public bool TryReplaceKnownAgentIds(long revision, HashSet<ulong> agentIds)
    {
        ArgumentNullException.ThrowIfNull(agentIds);

        lock (_stateGate)
        {
            var revisionMatches = _subscriptionRevision == revision;

            // This set represents what was actually delivered to the client after a complete
            // snapshot plan, independently of which subscription is current now. Committing the
            // exact delivered set also preserves stale-plan removes, so the next subscription can
            // choose Spawn/Update/Remove from the client's real state.
            _knownAgentIds = agentIds;
            return revisionMatches;
        }
    }

    public async Task<ProtocolSendMetrics> SendAsync(
        IProtocolMessage message,
        ProtocolVersion version,
        CancellationToken cancellationToken)
    {
        BeginSend();
        try
        {
            var encodeStarted = Stopwatch.GetTimestamp();
            var frame = ProtocolCodec.Serialize(message, version);
            var encodeTimeMs = Stopwatch.GetElapsedTime(encodeStarted).TotalMilliseconds;

            await _sendGate.WaitAsync(cancellationToken);
            try
            {
                if (Socket.State != WebSocketState.Open)
                {
                    throw new WebSocketException(WebSocketError.InvalidState);
                }

                var sendStarted = Stopwatch.GetTimestamp();
                await Socket.SendAsync(
                    new ArraySegment<byte>(frame),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    cancellationToken);
                var sendTimeMs = Stopwatch.GetElapsedTime(sendStarted).TotalMilliseconds;
                return new ProtocolSendMetrics(frame.Length, encodeTimeMs, sendTimeMs);
            }
            finally
            {
                _sendGate.Release();
            }
        }
        finally
        {
            EndSend();
        }
    }

    public void Abort()
    {
        Socket.Abort();
    }

    public void Dispose()
    {
        var disposeSendGate = false;
        lock (_lifetimeGate)
        {
            if (_disposeRequested)
            {
                return;
            }

            _disposeRequested = true;
            if (_activeSendCount == 0 && !_sendGateDisposed)
            {
                _sendGateDisposed = true;
                disposeSendGate = true;
            }
        }

        if (disposeSendGate)
        {
            _sendGate.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void BeginSend()
    {
        lock (_lifetimeGate)
        {
            ObjectDisposedException.ThrowIf(_disposeRequested, this);
            _activeSendCount = checked(_activeSendCount + 1);
        }
    }

    private void EndSend()
    {
        var disposeSendGate = false;
        lock (_lifetimeGate)
        {
            _activeSendCount--;
            if (_disposeRequested && _activeSendCount == 0 && !_sendGateDisposed)
            {
                _sendGateDisposed = true;
                disposeSendGate = true;
            }
        }

        if (disposeSendGate)
        {
            _sendGate.Dispose();
        }
    }
}

internal readonly record struct ClientSubscriptionState(
    WorldRect Area,
    long Revision,
    HashSet<ulong> KnownAgentIds);

internal sealed class ClientConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, ClientConnection> _connections = new();

    public int Count => _connections.Count;

    public ClientConnection Register(WebSocket socket)
    {
        var connection = new ClientConnection(Guid.NewGuid(), socket);
        if (!_connections.TryAdd(connection.Id, connection))
        {
            connection.Dispose();
            throw new InvalidOperationException("Failed to register a unique client connection.");
        }

        return connection;
    }

    public bool TryGet(Guid id, out ClientConnection? connection)
    {
        return _connections.TryGetValue(id, out connection);
    }

    public bool Remove(Guid id)
    {
        return _connections.TryRemove(id, out _);
    }

    public ClientConnection[] CreateSnapshot()
    {
        return _connections.Values.ToArray();
    }
}

internal abstract record ClientCommand(Guid ConnectionId);

internal sealed record SubscribeAreaCommand(
    Guid ConnectionId,
    WorldRect Area) : ClientCommand(ConnectionId);

internal sealed class ClientCommandQueue
{
    private const int Capacity = 1024;
    private readonly Channel<ClientCommand> _channel = Channel.CreateBounded<ClientCommand>(
        new BoundedChannelOptions(Capacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
        });

    public ValueTask WriteAsync(ClientCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return _channel.Writer.WriteAsync(command, cancellationToken);
    }

    public IAsyncEnumerable<ClientCommand> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

internal sealed class ClientCommandProcessor(
    ClientCommandQueue queue,
    ClientConnectionRegistry connections,
    ILogger<ClientCommandProcessor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var command in queue.ReadAllAsync(stoppingToken))
            {
                if (!connections.TryGet(command.ConnectionId, out var connection) || connection is null)
                {
                    continue;
                }

                switch (command)
                {
                    case SubscribeAreaCommand subscribe:
                        connection.SetSubscription(subscribe.Area);
                        break;
                    default:
                        ServerLog.UnsupportedClientCommand(logger, command.GetType().Name);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
