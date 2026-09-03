using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

/// <summary>
/// Publishes the authoritative Regional Generation baseline. A lightweight source identity is checked
/// before the immutable baseline is copied, mapped, or encoded. Encoded frames are shared by protocol
/// version and source identity so a static baseline is serialized once rather than once per client.
/// Protocol 2.22+ receives a chunked aggregate, while older clients retain the legacy single-frame path.
/// </summary>
internal sealed class RegionalGenerationPublishService(
    IObservationSource observationSource,
    SimulationRuntime simulation,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationDeliveryCoordinator deliveryCoordinator,
    ObservationCache cache,
    ILogger<RegionalGenerationPublishService> logger) : BackgroundService
{
    private static readonly Action<ILogger, ulong, ulong, byte, byte, Exception?> LogEncodingFailure =
        LoggerMessage.Define<ulong, ulong, byte, byte>(
            LogLevel.Error,
            new EventId(1, nameof(LogEncodingFailure)),
            "Regional Generation observation generation {Generation} source tick {SourceTick} cannot be encoded for Protocol {Major}.{Minor}; delivery is suppressed until the source identity changes.");

    private readonly Dictionary<Guid, SourceIdentity> _delivered = [];
    private readonly HashSet<FailedEncoding> _failedEncodings = [];

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
                    && connection.NegotiatedVersion.SupportsRegionalGeneration
                    && connection.Socket.State == WebSocketState.Open).ToArray();
                if (targets.Length == 0) continue;

                var sourceIdentity = CaptureSourceIdentity();
                _failedEncodings.RemoveWhere(item => item.Identity != sourceIdentity);
                if (targets.All(connection => _delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity))
                    continue;

                // Only materialize the immutable baseline when at least one client actually needs this identity.
                var observation = observationSource.CaptureRegionalGenerationObservation();
                var capturedIdentity = new SourceIdentity(observation.Generation, observation.HasSnapshot, observation.SourceTick);
                if (capturedIdentity != sourceIdentity)
                {
                    // The simulation changed between the cheap identity read and full capture. Use the coherent
                    // captured identity and let the next interval observe any subsequent change.
                    sourceIdentity = capturedIdentity;
                    _failedEncodings.RemoveWhere(item => item.Identity != sourceIdentity);
                }

                var message = RegionalGenerationMessageMapper.ToProtocol(
                    observation.Snapshot ?? CreateEmptySnapshot(observation.TickCount));
                var revision = new ObservationRevision(sourceIdentity.Generation, sourceIdentity.SourceTick);
                IProtocolMessage[]? chunkMessages = null;

                foreach (var versionGroup in targets.GroupBy(static connection => connection.NegotiatedVersion))
                {
                    var version = versionGroup.Key;
                    var failure = new FailedEncoding(sourceIdentity, version);
                    if (_failedEncodings.Contains(failure)) continue;

                    if (version.SupportsRegionalGenerationChunking)
                    {
                        try
                        {
                            chunkMessages ??= RegionalGenerationSnapshotChunker
                                .Split(message, CreateSnapshotId(sourceIdentity))
                                .Cast<IProtocolMessage>()
                                .ToArray();
                            var cacheKeys = new EncodedObservationCacheKey[chunkMessages.Length];
                            for (var index = 0; index < chunkMessages.Length; index++)
                            {
                                var key = new EncodedObservationCacheKey(
                                    "regional-generation-chunk",
                                    version,
                                    revision,
                                    $"{(sourceIdentity.HasSnapshot ? "baseline" : "empty")}:chunk:{index}",
                                    IsStatic: true);
                                cacheKeys[index] = key;
                                var chunk = chunkMessages[index];
                                _ = cache.GetOrEncode(key, () => ObservationProtocolAdapter.Serialize(chunk, version));
                            }

                            foreach (var connection in versionGroup)
                            {
                                if (_delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity) continue;
                                if (deliveryCoordinator.TryScheduleCached(
                                    connection,
                                    ObservationDeliveryLane.RegionalGeneration,
                                    chunkMessages,
                                    cacheKeys,
                                    cache,
                                    stoppingToken))
                                {
                                    _delivered[connection.Id] = sourceIdentity;
                                }
                            }
                        }
                        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
                        {
                            _failedEncodings.Add(failure);
                            LogEncodingFailure(logger, sourceIdentity.Generation, sourceIdentity.SourceTick, version.Major, version.Minor, exception);
                        }
                        continue;
                    }

                    var cacheKey = new EncodedObservationCacheKey(
                        "regional-generation",
                        version,
                        revision,
                        sourceIdentity.HasSnapshot ? "baseline" : "empty",
                        IsStatic: true);
                    try
                    {
                        // Legacy Protocol 2.18-2.21 preserves the existing single-frame contract. Oversized
                        // snapshots are suppressed for those clients instead of disconnecting them.
                        _ = cache.GetOrEncode(cacheKey, () => ObservationProtocolAdapter.Serialize(message, version));
                    }
                    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or OverflowException)
                    {
                        _failedEncodings.Add(failure);
                        LogEncodingFailure(logger, sourceIdentity.Generation, sourceIdentity.SourceTick, version.Major, version.Minor, exception);
                        continue;
                    }

                    foreach (var connection in versionGroup)
                    {
                        if (_delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity) continue;
                        if (deliveryCoordinator.TryScheduleCached(
                            connection,
                            ObservationDeliveryLane.RegionalGeneration,
                            message,
                            cacheKey,
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

    private SourceIdentity CaptureSourceIdentity()
    {
        while (true)
        {
            var generation = simulation.ObservationGeneration;
            var state = simulation.Read(static world =>
            {
                var hasSnapshot = world.TryGetRegionalGenerationSourceTick(out var sourceTick);
                return (HasSnapshot: hasSnapshot, SourceTick: sourceTick);
            });
            if (generation == simulation.ObservationGeneration)
                return new SourceIdentity(generation, state.HasSnapshot, state.SourceTick);
        }
    }

    private void PruneDeliveryState(IReadOnlyList<ClientConnection> currentConnections)
    {
        if (_delivered.Count == 0) return;
        var active = currentConnections.Select(static connection => connection.Id).ToHashSet();
        foreach (var connectionId in _delivered.Keys.Where(id => !active.Contains(id)).ToArray())
            _delivered.Remove(connectionId);
    }

    private static ulong CreateSnapshotId(SourceIdentity identity)
    {
        var value = identity.SourceTick ^ unchecked(identity.Generation * 0x9E3779B97F4A7C15UL);
        return value == 0 ? 1UL : value;
    }

    private static RegionalGenerationSnapshot CreateEmptySnapshot(ulong tickCount) => new(
        new WorldVolume(-1d, -1d, 0d, 1d, 1d, 1d),
        RegionalGenerationQualityPreset.Draft,
        WorldSeed: 1,
        Iterations: 0,
        Settlements: [],
        GrowthEvents: [],
        Corridors: [],
        Districts: [],
        Parcels: [],
        Buildings: [],
        Pois: [],
        Toponyms: [],
        RoadSigns: [],
        Quality: new RegionalQualityReport(0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d),
        TickCount: tickCount);

    private readonly record struct SourceIdentity(ulong Generation, bool HasSnapshot, ulong SourceTick);
    private readonly record struct FailedEncoding(SourceIdentity Identity, ProtocolVersion Version);
}
