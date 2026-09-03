using System.Runtime.ExceptionServices;

namespace MachiVerseWorks.Server;

internal enum ObservationDeliveryLane
{
    Snapshot = 0,
    Population = 1,
    Economy = 2,
    Logistics = 3,
    Power = 4,
    WaterSewer = 5,
    Gas = 6,
    Optical = 7,
    Radio = 8,
    WorldEnvironment = 9,
    PersistentRegionalEvolution = 10,
}

internal sealed class SnapshotDeliveryScheduler
{
    private sealed class DeliverySlot
    {
        public DeliverySlot(ObservationDeliveryLane lane)
        {
            Lane = lane;
            Reserved = true;
        }

        public ObservationDeliveryLane Lane { get; set; }
        public bool Reserved { get; set; }
        public bool DiscardRequested { get; set; }
        public Task? Delivery { get; set; }
        public ushort WaitingLaneMask { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<Guid, DeliverySlot> _slots = [];
    private Exception? _unexpectedFailure;

    public int InFlightCount
    {
        get
        {
            lock (_gate)
            {
                var count = 0;
                foreach (var slot in _slots.Values)
                {
                    if (slot.Delivery is not null) count++;
                }
                return count;
            }
        }
    }

    internal int TrackedConnectionCount
    {
        get
        {
            lock (_gate) return _slots.Count;
        }
    }

    public bool TryReserve(Guid connectionId) => TryReserve(connectionId, ObservationDeliveryLane.Snapshot);

    public bool TryReserve(Guid connectionId, ObservationDeliveryLane lane)
    {
        lock (_gate)
        {
            if (!_slots.TryGetValue(connectionId, out var slot))
            {
                _slots.Add(connectionId, new DeliverySlot(lane));
                return true;
            }

            if (slot.DiscardRequested) return false;

            var waitingBit = GetWaitingBit(lane);
            if (slot.Reserved || slot.Delivery is not null)
            {
                if (slot.Lane != lane)
                    slot.WaitingLaneMask |= waitingBit;
                return false;
            }

            if (slot.WaitingLaneMask != 0)
            {
                if ((slot.WaitingLaneMask & waitingBit) == 0)
                    return false;

                slot.WaitingLaneMask = (ushort)(slot.WaitingLaneMask & ~waitingBit);
            }

            slot.Lane = lane;
            slot.Reserved = true;
            return true;
        }
    }

    public bool StartReserved(Guid connectionId, Func<Task> deliveryFactory)
    {
        ArgumentNullException.ThrowIfNull(deliveryFactory);

        Task delivery;
        lock (_gate)
        {
            if (!_slots.TryGetValue(connectionId, out var slot)
                || !slot.Reserved
                || slot.Delivery is not null)
            {
                throw new InvalidOperationException($"Connection {connectionId} does not have a pending snapshot delivery reservation.");
            }

            if (slot.DiscardRequested)
            {
                _slots.Remove(connectionId);
                return false;
            }

            try
            {
                delivery = deliveryFactory();
            }
            catch
            {
                ReleaseSlotAfterReservationFailure(connectionId, slot);
                throw;
            }

            slot.Reserved = false;
            slot.Delivery = delivery;
        }

        _ = ObserveCompletionAsync(connectionId, delivery);
        return true;
    }

    public void ReleaseReservation(Guid connectionId)
    {
        lock (_gate)
        {
            if (!_slots.TryGetValue(connectionId, out var slot)
                || !slot.Reserved
                || slot.Delivery is not null)
            {
                return;
            }

            slot.Reserved = false;
            if (slot.DiscardRequested || slot.WaitingLaneMask == 0) _slots.Remove(connectionId);
        }
    }

    public void Discard(Guid connectionId)
    {
        lock (_gate)
        {
            if (!_slots.TryGetValue(connectionId, out var slot)) return;

            if (slot.Reserved && slot.Delivery is null)
            {
                slot.DiscardRequested = true;
                slot.WaitingLaneMask = 0;
                return;
            }

            _slots.Remove(connectionId);
        }
    }

    public bool TrySchedule(Guid connectionId, Func<Task> deliveryFactory)
        => TrySchedule(connectionId, ObservationDeliveryLane.Snapshot, deliveryFactory);

    public bool TrySchedule(Guid connectionId, ObservationDeliveryLane lane, Func<Task> deliveryFactory)
    {
        ArgumentNullException.ThrowIfNull(deliveryFactory);
        if (!TryReserve(connectionId, lane)) return false;
        return StartReserved(connectionId, deliveryFactory);
    }

    public Task[] CreateInFlightSnapshot()
    {
        lock (_gate)
        {
            var result = new List<Task>(_slots.Count);
            foreach (var slot in _slots.Values)
            {
                if (slot.Delivery is not null) result.Add(slot.Delivery);
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

    private static ushort GetWaitingBit(ObservationDeliveryLane lane)
    {
        var laneIndex = (int)lane;
        if ((uint)laneIndex >= 16)
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "Observation delivery lanes must fit in the scheduler waiting mask.");
        return (ushort)(1 << laneIndex);
    }

    private void ReleaseSlotAfterReservationFailure(Guid connectionId, DeliverySlot slot)
    {
        slot.Reserved = false;
        if (slot.DiscardRequested || slot.WaitingLaneMask == 0) _slots.Remove(connectionId);
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
                if (_slots.TryGetValue(connectionId, out var slot)
                    && ReferenceEquals(slot.Delivery, delivery))
                {
                    slot.Delivery = null;
                    if (slot.DiscardRequested || slot.WaitingLaneMask == 0) _slots.Remove(connectionId);
                }
            }
        }
    }
}
