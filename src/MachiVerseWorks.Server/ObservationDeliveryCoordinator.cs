namespace MachiVerseWorks.Server;

internal sealed class ObservationDeliveryCoordinator(
    ServerOptions options,
    ClientConnectionRegistry connections,
    SnapshotDeliveryScheduler deliveryScheduler)
{
    public bool TrySchedule(
        ClientConnection connection,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task> delivery)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(delivery);
        return deliveryScheduler.TrySchedule(
            connection.Id,
            ObservationDeliveryLane.Default,
            () => DeliverAsync(connection, cancellationToken, delivery));
    }

    private async Task DeliverAsync(
        ClientConnection connection,
        CancellationToken cancellationToken,
        Func<CancellationToken, Task> delivery)
    {
        try
        {
            await Task.Yield();
            using var sendCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            sendCancellation.CancelAfter(options.ObservationDeliveryTimeout);
            await delivery(sendCancellation.Token);
        }
        catch (Exception exception) when (SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(exception))
        {
            connection.Abort();
            connections.Remove(connection.Id);
        }
    }
}
