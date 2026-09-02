using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

/// <summary>
/// Publishes the authoritative Regional Generation baseline as a world-global, read-only observation.
/// The snapshot is intentionally independent of the client's spatial subscription volume because the
/// Protocol 2.18 contract represents one coherent regional baseline with stable cross-entity relations.
/// </summary>
internal sealed class RegionalGenerationPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationDeliveryCoordinator deliveryCoordinator,
    ILogger<RegionalGenerationPublishService> logger) : BackgroundService
{
    private static readonly Action<ILogger, ulong, ulong, Exception?> LogOversizedSnapshot =
        LoggerMessage.Define<ulong, ulong>(
            LogLevel.Error,
            new EventId(1, nameof(LogOversizedSnapshot)),
            "Regional Generation observation generation {Generation} source tick {SourceTick} cannot fit the Protocol 2.18 single-frame contract; delivery was skipped without disconnecting Clients.");

    private readonly Dictionary<Guid, (ulong Generation, bool HasSnapshot, ulong SourceTick)> _delivered = [];

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

                var observation = observationSource.CaptureRegionalGenerationObservation();
                var sourceIdentity = (observation.Generation, observation.HasSnapshot, observation.SourceTick);
                if (targets.All(connection => _delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity))
                    continue;

                var message = RegionalGenerationMessageMapper.ToProtocol(
                    observation.Snapshot ?? CreateEmptySnapshot(observation.TickCount));
                try
                {
                    // The Regional Generation contract is a single Protocol 2.18 frame. Validate once before
                    // scheduling so an oversized authoritative payload never gets misclassified as a Client failure.
                    _ = RegionalGenerationProtocolCodec.Serialize(message, ProtocolVersion.Current);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    LogOversizedSnapshot(logger, observation.Generation, observation.SourceTick, exception);
                    continue;
                }

                foreach (var connection in targets)
                {
                    if (_delivered.TryGetValue(connection.Id, out var delivered) && delivered == sourceIdentity) continue;
                    if (deliveryCoordinator.TrySchedule(
                        connection,
                        ObservationDeliveryLane.Snapshot,
                        message,
                        stoppingToken))
                    {
                        _delivered[connection.Id] = sourceIdentity;
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private void PruneDeliveryState(IReadOnlyList<ClientConnection> currentConnections)
    {
        if (_delivered.Count == 0) return;
        var active = currentConnections.Select(static connection => connection.Id).ToHashSet();
        foreach (var connectionId in _delivered.Keys.Where(id => !active.Contains(id)).ToArray())
            _delivered.Remove(connectionId);
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
        Quality: new RegionalQualityReport(0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d, 0d),
        TickCount: tickCount);
}
