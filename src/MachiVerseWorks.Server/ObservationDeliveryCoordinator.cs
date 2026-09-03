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

    public bool TrySchedule(
        ClientConnection connection,
        ObservationDeliveryLane lane,
        IReadOnlyList<IProtocolMessage> messages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(messages);
        ValidateMessages(messages);
        if (!deliveryScheduler.TryReserve(connection.Id, lane)) return false;

        return deliveryScheduler.StartReserved(
            connection.Id,
            () => DeliverAsync(connection, messages, cancellationToken));
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

    public bool TryScheduleCached(
        ClientConnection connection,
        ObservationDeliveryLane lane,
        IReadOnlyList<IProtocolMessage> messages,
        IReadOnlyList<EncodedObservationCacheKey> cacheKeys,
        ObservationCache cache,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(cacheKeys);
        ArgumentNullException.ThrowIfNull(cache);
        ValidateMessages(messages);
        if (messages.Count != cacheKeys.Count)
            throw new ArgumentException("A cache key is required for every message.", nameof(cacheKeys));
        if (!deliveryScheduler.TryReserve(connection.Id, lane)) return false;

        return deliveryScheduler.StartReserved(
            connection.Id,
            () => DeliverCachedAsync(connection, messages, cacheKeys, cache, cancellationToken));
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
            RemoveFailedClient(connection);
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
            RemoveFailedClient(connection);
        }
    }

    private async Task DeliverAsync(
        ClientConnection connection,
        IReadOnlyList<IProtocolMessage> messages,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            foreach (var message in messages)
            {
                using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
                _ = await connection.SendAsync(message, connection.NegotiatedVersion, sendCancellation.Token);
            }
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            RemoveFailedClient(connection);
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
            RemoveFailedClient(connection);
        }
    }

    private async Task DeliverCachedAsync(
        ClientConnection connection,
        IReadOnlyList<IProtocolMessage> messages,
        IReadOnlyList<EncodedObservationCacheKey> cacheKeys,
        ObservationCache cache,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Yield();
            for (var index = 0; index < messages.Count; index++)
            {
                using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
                _ = await connection.SendCachedAsync(
                    messages[index],
                    connection.NegotiatedVersion,
                    cacheKeys[index],
                    cache,
                    sendCancellation.Token);
            }
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            RemoveFailedClient(connection);
        }
    }

    private static void ValidateMessages(IReadOnlyList<IProtocolMessage> messages)
    {
        if (messages.Count == 0) throw new ArgumentException("Messages cannot be empty.", nameof(messages));
        if (messages.Any(static message => message is null))
            throw new ArgumentException("Messages cannot contain null entries.", nameof(messages));
    }

    private void RemoveFailedClient(ClientConnection connection)
    {
        connection.Abort();
        connections.Remove(connection.Id);
    }
}
