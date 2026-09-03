using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class PersistentRegionalEvolutionPublishService(
    IObservationSource observationSource,
    SimulationRuntime simulation,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationDeliveryCoordinator deliveryCoordinator,
    ObservationCache cache,
    ILogger<PersistentRegionalEvolutionPublishService> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, byte, byte, Exception?> LogEncodingFailure =
        LoggerMessage.Define<int, byte, byte>(
            LogLevel.Error,
            new EventId(1, nameof(LogEncodingFailure)),
            "Persistent Regional Evolution year {CurrentYear} cannot be encoded for Protocol {Major}.{Minor}; delivery is suppressed until the source identity changes.");

    private readonly Dictionary<Guid, SourceIdentity> _delivered = [];
    private readonly HashSet<FailedEncoding> _failedEncodings = [];
    private PreparedSnapshot? _prepared;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var currentConnections = connections.CreateSnapshot();
                PruneDeliveryState(currentConnections);
                var targets = currentConnections.Where(static connection =>
                    connection.HandshakeCompleted
                    && connection.NegotiatedVersion.SupportsPersistentRegionalEvolution
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                if (!TryCaptureSourceIdentity(out var sourceIdentity))
                {
                    _prepared = null;
                    _failedEncodings.Clear();
                    continue;
                }

                _failedEncodings.RemoveWhere(item => item.Identity != sourceIdentity);
                if (targets.All(connection => _delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity))
                    continue;

                var prepared = _prepared;
                if (prepared is null || prepared.Identity != sourceIdentity)
                {
                    var captured = observationSource.CapturePersistentRegionalEvolutionSnapshot();
                    if (captured is null) continue;

                    // Do not publish a mixed/stale snapshot if an economy/logistics/evolution source changed
                    // while the detached payload was being captured.
                    if (!TryCaptureSourceIdentity(out var afterCapture) || afterCapture != sourceIdentity)
                        continue;

                    var message = PersistentRegionalEvolutionMessageMapper.ToProtocol(
                        captured.Value.Evolution,
                        captured.Value.Interactions);
                    var messages = PersistentRegionalEvolutionProtocolChunker.Split(message)
                        .Cast<IProtocolMessage>()
                        .ToArray();
                    prepared = new PreparedSnapshot(sourceIdentity, messages);
                    _prepared = prepared;
                }

                var revision = new ObservationRevision(
                    sourceIdentity.Generation,
                    unchecked((ulong)sourceIdentity.Observation.CurrentYear));
                var identityText = CreateIdentityText(sourceIdentity.Observation);

                foreach (var versionGroup in targets.GroupBy(static connection => connection.NegotiatedVersion))
                {
                    var version = versionGroup.Key;
                    var failedEncoding = new FailedEncoding(sourceIdentity, version);
                    if (_failedEncodings.Contains(failedEncoding)) continue;

                    var cacheKeys = new EncodedObservationCacheKey[prepared.Messages.Length];
                    var encodable = true;
                    try
                    {
                        for (var index = 0; index < prepared.Messages.Length; index++)
                        {
                            var key = new EncodedObservationCacheKey(
                                "persistent-regional-evolution",
                                version,
                                revision,
                                $"{identityText}:chunk:{index}",
                                IsStatic: true);
                            cacheKeys[index] = key;
                            var message = prepared.Messages[index];
                            // Populate the encoded cache before any per-client task is scheduled. Codec/domain
                            // failures therefore remain server-side and cannot abort an otherwise healthy client.
                            _ = cache.GetOrEncode(key, () => ObservationProtocolAdapter.Serialize(message, version));
                        }
                    }
                    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
                    {
                        encodable = false;
                        _failedEncodings.Add(failedEncoding);
                        LogEncodingFailure(logger, sourceIdentity.Observation.CurrentYear, version.Major, version.Minor, exception);
                    }
                    if (!encodable) continue;

                    foreach (var connection in versionGroup)
                    {
                        if (_delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity) continue;
                        if (deliveryCoordinator.TryScheduleCached(
                            connection,
                            ObservationDeliveryLane.PersistentRegionalEvolution,
                            prepared.Messages,
                            cacheKeys,
                            cache,
                            stoppingToken))
                        {
                            _delivered[connection.Id] = sourceIdentity;
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private bool TryCaptureSourceIdentity(out SourceIdentity identity)
    {
        while (true)
        {
            var generation = simulation.ObservationGeneration;
            var state = simulation.Read(static world =>
            {
                var hasSnapshot = world.TryGetPersistentRegionalObservationIdentity(out var observation);
                return (HasSnapshot: hasSnapshot, Observation: observation);
            });
            if (generation != simulation.ObservationGeneration) continue;
            if (!state.HasSnapshot)
            {
                identity = default;
                return false;
            }
            identity = new SourceIdentity(generation, state.Observation);
            return true;
        }
    }

    private void PruneDeliveryState(IReadOnlyList<ClientConnection> currentConnections)
    {
        if (_delivered.Count == 0) return;
        var active = currentConnections.Select(static connection => connection.Id).ToHashSet();
        foreach (var connectionId in _delivered.Keys.Where(id => !active.Contains(id)).ToArray())
            _delivered.Remove(connectionId);
    }

    private static string CreateIdentityText(PersistentRegionalObservationIdentity identity) =>
        FormattableString.Invariant($"y{identity.CurrentYear}:e{identity.EconomicCycle}:l{identity.LogisticsCycle}:emp{identity.EmploymentCount}:ship{identity.ShipmentCount}:del{identity.DeliveredShipmentCount}");

    private readonly record struct SourceIdentity(
        ulong Generation,
        PersistentRegionalObservationIdentity Observation);

    private readonly record struct FailedEncoding(SourceIdentity Identity, ProtocolVersion Version);

    private sealed record PreparedSnapshot(SourceIdentity Identity, IProtocolMessage[] Messages);
}
