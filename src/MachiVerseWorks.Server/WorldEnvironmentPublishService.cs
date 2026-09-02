using System.Net.WebSockets;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed class WorldEnvironmentPublishService(
    IObservationSource observationSource,
    ServerOptions options,
    ClientConnectionRegistry connections,
    ObservationCache cache,
    ObservationDeliveryCoordinator deliveryCoordinator) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.SnapshotInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var targets = connections.CreateSnapshot()
                    .Where(static connection => connection.HandshakeCompleted
                        && connection.NegotiatedVersion.SupportsWorldEnvironment
                        && connection.Socket.State == WebSocketState.Open)
                    .Select(connection => connection.TryCaptureSubscription(out var subscription)
                        ? new EnvironmentPublishTarget(connection, subscription.Volume)
                        : null)
                    .Where(static target => target is not null)
                    .Select(static target => target!)
                    .ToArray();
                if (targets.Length == 0) continue;

                var messages = new Dictionary<WorldVolume, EnvironmentPublishMessage>();
                foreach (var target in targets)
                {
                    if (!messages.TryGetValue(target.Volume, out var cachedMessage))
                    {
                        var observed = observationSource.CaptureWorldEnvironmentSnapshot(target.Volume);
                        var revision = new ObservationRevision(observed.ObservationGeneration, observed.ObservationRevision);
                        var message = cache.GetOrCreateSpatial(
                            new SpatialObservationCacheKey(SpatialObservationKind.WorldEnvironment, target.Volume, revision),
                            () => WorldEnvironmentMessageMapper.ToProtocol(observed.Value));
                        cachedMessage = new EnvironmentPublishMessage(message, revision);
                        messages.Add(target.Volume, cachedMessage);
                    }

                    _ = deliveryCoordinator.TrySchedule(
                        target.Connection,
                        async sendCancellation =>
                        {
                            var key = new EncodedObservationCacheKey(
                                "world-environment",
                                target.Connection.NegotiatedVersion,
                                cachedMessage.Revision,
                                ObservationCacheIdentity.ForVolume(target.Volume));
                            _ = await target.Connection.SendCachedAsync(
                                cachedMessage.Message,
                                target.Connection.NegotiatedVersion,
                                key,
                                cache,
                                sendCancellation);
                        },
                        stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private sealed record EnvironmentPublishTarget(ClientConnection Connection, WorldVolume Volume);
    private sealed record EnvironmentPublishMessage(WorldEnvironmentSnapshotMessage Message, ObservationRevision Revision);
}
