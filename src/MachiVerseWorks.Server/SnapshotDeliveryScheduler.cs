using System.Runtime.ExceptionServices;

namespace MachiVerseWorks.Server;

internal sealed class SnapshotDeliveryScheduler
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Task?> _slots = [];
    private Exception? _unexpectedFailure;

    public int InFlightCount
    {
        get
        {
            lock (_gate)
            {
                var count = 0;
                foreach (var task in _slots.Values)
                {
                    if (task is not null) count++;
                }
                return count;
            }
        }
    }

    public bool TryReserve(Guid connectionId)
    {
        lock (_gate)
        {
            if (_slots.ContainsKey(connectionId)) return false;
            _slots.Add(connectionId, null);
            return true;
        }
    }

    public void StartReserved(Guid connectionId, Func<Task> deliveryFactory)
    {
        ArgumentNullException.ThrowIfNull(deliveryFactory);

        Task delivery;
        lock (_gate)
        {
            if (!_slots.TryGetValue(connectionId, out var current) || current is not null)
                throw new InvalidOperationException($"Connection {connectionId} does not have a pending snapshot delivery reservation.");

            try
            {
                delivery = deliveryFactory();
            }
            catch
            {
                _slots.Remove(connectionId);
                throw;
            }

            _slots[connectionId] = delivery;
        }

        _ = ObserveCompletionAsync(connectionId, delivery);
    }

    public void ReleaseReservation(Guid connectionId)
    {
        lock (_gate)
        {
            if (_slots.TryGetValue(connectionId, out var current) && current is null)
                _slots.Remove(connectionId);
        }
    }

    public bool TrySchedule(Guid connectionId, Func<Task> deliveryFactory)
    {
        ArgumentNullException.ThrowIfNull(deliveryFactory);
        if (!TryReserve(connectionId)) return false;
        StartReserved(connectionId, deliveryFactory);
        return true;
    }

    public Task[] CreateInFlightSnapshot()
    {
        lock (_gate)
        {
            var result = new List<Task>(_slots.Count);
            foreach (var task in _slots.Values)
            {
                if (task is not null) result.Add(task);
            }
            return result.ToArray();
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
                if (_slots.TryGetValue(connectionId, out var current) && ReferenceEquals(current, delivery))
                    _slots.Remove(connectionId);
            }
        }
    }
}
