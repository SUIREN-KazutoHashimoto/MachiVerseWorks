using MachiVerseWorks.Protocol;

namespace MachiVerseWorks.Server;

internal sealed class ObservationDeliveryCoordinator(
    ServerOptions options,
    ClientConnectionRegistry connections,
    SnapshotDeliveryScheduler deliveryScheduler)
{
    public bool TrySchedule(
        ClientConnection connection,
        ObservationDeliveryLane lane,
        IProtocolMessage message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(message);
        if (!deliveryScheduler.TryReserve(connection.Id, lane)) return false;

        return deliveryScheduler.StartReserved(
            connection.Id,
            () => DeliverAsync(connection, message, cancellationToken));
    }

    public bool TrySchedule(
        ClientConnection connection,
        ObservationDeliveryLane lane,
        IProtocolMessage firstMessage,
        IProtocolMessage secondMessage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(firstMessage);
        ArgumentNullException.ThrowIfNull(secondMessage);
        if (!deliveryScheduler.TryReserve(connection.Id, lane)) return false;

        return deliveryScheduler.StartReserved(
            connection.Id,
            () => DeliverAsync(connection, firstMessage, secondMessage, cancellationToken));
    }

    public bool TryScheduleCached(
        ClientConnection connection,
        ObservationDeliveryLane lane,
        IProtocolMessage message,
        EncodedObservationCacheKey cacheKey,
        ObservationCache cache,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(cache);
        if (!deliveryScheduler.TryReserve(connection.Id, lane)) return false;

        return deliveryScheduler.StartReserved(
            connection.Id,
            () => DeliverCachedAsync(connection, message, cacheKey, cache, cancellationToken));
    }

    private async Task DeliverAsync(
        ClientConnection connection,
        IProtocolMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
            _ = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            connection.Abort();
            connections.Remove(connection.Id);
        }
    }

    private async Task DeliverAsync(
        ClientConnection connection,
        IProtocolMessage firstMessage,
        IProtocolMessage secondMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
            _ = await connection.SendAsync(firstMessage, connection.NegotiatedVersion, sendCancellation.Token);
            _ = await connection.SendAsync(secondMessage, connection.NegotiatedVersion, sendCancellation.Token);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            connection.Abort();
            connections.Remove(connection.Id);
        }
    }

    private async Task DeliverCachedAsync(
        ClientConnection connection,
        IProtocolMessage message,
        EncodedObservationCacheKey cacheKey,
        ObservationCache cache,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
            _ = await connection.SendCachedAsync(
                message,
                connection.NegotiatedVersion,
                cacheKey,
                cache,
                sendCancellation.Token);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            connection.Abort();
            connections.Remove(connection.Id);
        }
    }
}
