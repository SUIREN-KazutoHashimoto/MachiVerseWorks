using System.Collections.Concurrent;

namespace MachiVerseWorks.Server;

internal sealed class MultimodalTransitOversizeDeliveryGate
{
    private readonly ConcurrentDictionary<Guid, long> _lastNotifiedSubscriptionRevision = [];

    public bool ShouldSend(Guid connectionId, long subscriptionRevision, bool isOversize)
    {
        if (!isOversize)
        {
            _lastNotifiedSubscriptionRevision.TryRemove(connectionId, out _);
            return true;
        }

        while (true)
        {
            if (!_lastNotifiedSubscriptionRevision.TryGetValue(connectionId, out var previousRevision))
            {
                if (_lastNotifiedSubscriptionRevision.TryAdd(connectionId, subscriptionRevision)) return true;
                continue;
            }

            if (previousRevision == subscriptionRevision) return false;
            if (_lastNotifiedSubscriptionRevision.TryUpdate(connectionId, subscriptionRevision, previousRevision)) return true;
        }
    }

    public void Remove(Guid connectionId) => _lastNotifiedSubscriptionRevision.TryRemove(connectionId, out _);
}
