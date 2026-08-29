using System.Runtime.ExceptionServices;

namespace MachiVerseWorks.Server;

internal sealed class SnapshotDeliveryScheduler
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Task> _inFlight = [];
    private Exception? _unexpectedFailure;

    public int InFlightCount
    {
        get
        {
            lock (_gate)
            {
                return _inFlight.Count;
            }
        }
    }

    public bool TrySchedule(Guid connectionId, Func<Task> deliveryFactory)
    {
        ArgumentNullException.ThrowIfNull(deliveryFactory);

        lock (_gate)
        {
            if (_inFlight.ContainsKey(connectionId))
            {
                return false;
            }

            var delivery = deliveryFactory();
            _inFlight.Add(connectionId, delivery);
            _ = ObserveCompletionAsync(connectionId, delivery);
            return true;
        }
    }

    public Task[] CreateInFlightSnapshot()
    {
        lock (_gate)
        {
            return _inFlight.Values.ToArray();
        }
    }

    public void ThrowIfFaulted()
    {
        Exception? failure;
        lock (_gate)
        {
            failure = _unexpectedFailure;
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private async Task ObserveCompletionAsync(Guid connectionId, Task delivery)
    {
        try
        {
            await delivery.ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            lock (_gate)
            {
                _unexpectedFailure ??= exception;
            }
        }
        finally
        {
            lock (_gate)
            {
                if (_inFlight.TryGetValue(connectionId, out var current) && ReferenceEquals(current, delivery))
                {
                    _inFlight.Remove(connectionId);
                }
            }
        }
    }
}
