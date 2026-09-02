namespace MachiVerseWorks.Server;

internal sealed class ObservationDeliveryCoordinator(
    ServerOptions options,
    ClientConnectionRegistry connections,
    SnapshotDeliveryScheduler deliveryScheduler)
{
    public bool TrySchedule(
        ClientConnection connection,
        Func<CancellationToken, Task> delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(delivery);
        return deliveryScheduler.TrySchedule(
            connection.Id,
            ObservationDeliveryLane.Default,
            () => DeliverAsync(connection, delivery, cancellationToken));
    }

    private async Task DeliverAsync(
        ClientConnection connection,
        Func<CancellationToken, Task> delivery,
        CancellationToken cancellationToken)
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
