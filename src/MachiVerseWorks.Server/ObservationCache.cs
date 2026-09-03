using System.Collections.Concurrent;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal sealed record ObservationCacheOptions(
    bool Enabled = true,
    int MaxEntityEntries = 32_768,
    int MaxSpatialEntries = 4_096,
    int MaxStaticEntries = 2_048,
    int MaxEncodedEntries = 8_192,
    long MaxEncodedBytes = 64L * 1024L * 1024L,
    ulong RetainedDynamicRevisions = 2)
{
    public static ObservationCacheOptions Default { get; } = new();
    public static ObservationCacheOptions Disabled { get; } = new(Enabled: false);
}

internal readonly record struct ObservationRevision(ulong Generation, ulong Revision);

internal enum EntityObservationKind : byte
{
    Person = 1,
}

internal enum SpatialObservationKind : byte
{
    Entities = 1,
    WorldEnvironment = 2,
}

internal enum StaticObservationKind : byte
{
    Road = 1,
    Railway = 2,
}

internal readonly record struct EntityObservationCacheKey(EntityObservationKind Kind, ulong EntityId, ObservationRevision Revision);
internal readonly record struct SpatialObservationCacheKey(SpatialObservationKind Kind, WorldVolume Volume, ObservationRevision Revision);
internal readonly record struct StaticObservationCacheKey(StaticObservationKind Kind, WorldVolume Volume, ObservationRevision Revision);
internal readonly record struct EncodedObservationCacheKey(
    string Kind,
    ProtocolVersion ProtocolVersion,
    ObservationRevision Revision,
    string Identity,
    bool IsStatic = false);

internal readonly record struct ObservationCacheMetrics(
    long Hits,
    long Misses,
    long Builds,
    long Encodings,
    long Evictions,
    long EncodedBytes,
    int EntityEntries,
    int SpatialEntries,
    int StaticEntries,
    int EncodedEntries)
{
    public double HitRate => Hits + Misses == 0 ? 0d : (double)Hits / (Hits + Misses);
}

/// <summary>
/// Gateway-owned cache for detached Observation data. Correctness is keyed only by authoritative
/// generation/revision markers; wall-clock age never decides whether an entry is valid.
/// </summary>
internal sealed class ObservationCache
{
    private readonly ObservationCacheOptions _options;
    private readonly CacheStore<EntityObservationCacheKey> _entities = new();
    private readonly CacheStore<SpatialObservationCacheKey> _spatial = new();
    private readonly CacheStore<StaticObservationCacheKey> _static = new();
    private readonly ConcurrentDictionary<EncodedObservationCacheKey, Lazy<byte[]>> _encoded = new();
    private readonly ConcurrentQueue<(EncodedObservationCacheKey Key, Lazy<byte[]> Entry)> _encodedOrder = new();
    private readonly HashSet<Lazy<byte[]>> _accountedEncodedEntries = [];
    private readonly object _revisionGate = new();
    private readonly object _encodedAccountingGate = new();
    private ulong _generation;
    private ulong _dynamicRevision;
    private long _hits;
    private long _misses;
    private long _builds;
    private long _encodings;
    private long _evictions;
    private long _encodedBytes;

    public ObservationCache()
        : this(ObservationCacheOptions.Default)
    {
    }

    internal ObservationCache(ObservationCacheOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxEntityEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxSpatialEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxStaticEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxEncodedEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.MaxEncodedBytes);
    }

    public T GetOrCreateEntity<T>(EntityObservationCacheKey key, Func<T> factory) where T : class =>
        GetOrCreate(_entities, key, key.Revision, _options.MaxEntityEntries, isStatic: false, factory);

    public T GetOrCreateSpatial<T>(SpatialObservationCacheKey key, Func<T> factory) where T : class =>
        GetOrCreate(_spatial, key, key.Revision, _options.MaxSpatialEntries, isStatic: false, factory);

    public T GetOrCreateStatic<T>(StaticObservationCacheKey key, Func<T> factory) where T : class =>
        GetOrCreate(_static, key, key.Revision, _options.MaxStaticEntries, isStatic: true, factory);

    public byte[] GetOrEncode(EncodedObservationCacheKey key, Func<byte[]> encoder)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        if (!_options.Enabled || !PrepareRevision(key.Revision, key.IsStatic))
        {
            Interlocked.Increment(ref _misses);
            Interlocked.Increment(ref _builds);
            Interlocked.Increment(ref _encodings);
            return encoder();
        }

        var candidate = new Lazy<byte[]>(() =>
        {
            Interlocked.Increment(ref _builds);
            Interlocked.Increment(ref _encodings);
            return encoder();
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        var actual = _encoded.GetOrAdd(key, candidate);
        var added = ReferenceEquals(actual, candidate);
        if (added) Interlocked.Increment(ref _misses);
        else Interlocked.Increment(ref _hits);

        try
        {
            var frame = actual.Value;
            if (added && TryAccountEncodedEntry(key, actual, frame.LongLength))
            {
                _encodedOrder.Enqueue((key, actual));
                TrimEncoded();
            }
            return frame;
        }
        catch
        {
            if (added) RemoveEncodedExact(key, actual);
            throw;
        }
    }

    public void ObserveRevision(ObservationRevision revision) => _ = PrepareRevision(revision, isStatic: false);
    public void ObserveGeneration(ulong generation) => _ = PrepareRevision(new ObservationRevision(generation, 0), isStatic: true);

    public ObservationCacheMetrics CreateMetricsSnapshot() => new(
        Interlocked.Read(ref _hits),
        Interlocked.Read(ref _misses),
        Interlocked.Read(ref _builds),
        Interlocked.Read(ref _encodings),
        Interlocked.Read(ref _evictions),
        Interlocked.Read(ref _encodedBytes),
        _entities.Count,
        _spatial.Count,
        _static.Count,
        _encoded.Count);

    private T GetOrCreate<TKey, T>(
        CacheStore<TKey> store,
        TKey key,
        ObservationRevision revision,
        int maximumEntries,
        bool isStatic,
        Func<T> factory)
        where TKey : notnull
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (!_options.Enabled || !PrepareRevision(revision, isStatic))
        {
            Interlocked.Increment(ref _misses);
            Interlocked.Increment(ref _builds);
            return factory();
        }

        var candidate = new Lazy<object>(() =>
        {
            Interlocked.Increment(ref _builds);
            return factory();
        }, LazyThreadSafetyMode.ExecutionAndPublication);
        var actual = store.GetOrAdd(key, candidate, out var added);
        if (added) Interlocked.Increment(ref _misses);
        else Interlocked.Increment(ref _hits);

        try
        {
            var value = (T)actual.Value;
            if (added) Trim(store, maximumEntries);
            return value;
        }
        catch
        {
            if (added) store.TryRemove(key, actual);
            throw;
        }
    }

    private bool PrepareRevision(ObservationRevision revision, bool isStatic)
    {
        lock (_revisionGate)
        {
            if (_generation == 0 || revision.Generation > _generation)
            {
                _generation = revision.Generation;
                _dynamicRevision = isStatic ? 0 : revision.Revision;
                ClearAll();
                return true;
            }

            if (revision.Generation < _generation) return false;
            if (isStatic) return true;

            if (revision.Revision > _dynamicRevision)
            {
                _dynamicRevision = revision.Revision;
                var minimum = revision.Revision > _options.RetainedDynamicRevisions
                    ? revision.Revision - _options.RetainedDynamicRevisions
                    : 0UL;
                EvictOlderDynamicEntries(revision.Generation, minimum);
                return true;
            }

            var oldestRetained = _dynamicRevision > _options.RetainedDynamicRevisions
                ? _dynamicRevision - _options.RetainedDynamicRevisions
                : 0UL;
            return revision.Revision >= oldestRetained;
        }
    }

    private void EvictOlderDynamicEntries(ulong generation, ulong minimumRevision)
    {
        AddEvictions(_entities.RemoveWhere(key => key.Revision.Generation != generation || key.Revision.Revision < minimumRevision));
        AddEvictions(_spatial.RemoveWhere(key => key.Revision.Generation != generation || key.Revision.Revision < minimumRevision));
        foreach (var entry in _encoded)
        {
            var key = entry.Key;
            if (key.IsStatic || (key.Revision.Generation == generation && key.Revision.Revision >= minimumRevision)) continue;
            if (!RemoveEncodedExact(key, entry.Value)) continue;
            Interlocked.Increment(ref _evictions);
        }
    }

    private void ClearAll()
    {
        AddEvictions(_entities.Clear());
        AddEvictions(_spatial.Clear());
        AddEvictions(_static.Clear());
        foreach (var entry in _encoded)
        {
            if (!RemoveEncodedExact(entry.Key, entry.Value)) continue;
            Interlocked.Increment(ref _evictions);
        }
        while (_encodedOrder.TryDequeue(out _)) { }
    }

    private void Trim<TKey>(CacheStore<TKey> store, int maximumEntries) where TKey : notnull
    {
        while (store.Count > maximumEntries && store.TryRemoveOldest()) Interlocked.Increment(ref _evictions);
    }

    private void TrimEncoded()
    {
        while ((_encoded.Count > _options.MaxEncodedEntries || Interlocked.Read(ref _encodedBytes) > _options.MaxEncodedBytes)
            && _encodedOrder.TryDequeue(out var oldest))
        {
            if (!RemoveEncodedExact(oldest.Key, oldest.Entry)) continue;
            Interlocked.Increment(ref _evictions);
        }
    }

    private bool TryAccountEncodedEntry(EncodedObservationCacheKey key, Lazy<byte[]> entry, long frameBytes)
    {
        lock (_encodedAccountingGate)
        {
            if (!_encoded.TryGetValue(key, out var current) || !ReferenceEquals(current, entry)) return false;
            if (_accountedEncodedEntries.Add(entry)) Interlocked.Add(ref _encodedBytes, frameBytes);
            return true;
        }
    }

    private bool RemoveEncodedExact(EncodedObservationCacheKey key, Lazy<byte[]> expected)
    {
        lock (_encodedAccountingGate)
        {
            if (!RemoveExact(_encoded, key, expected)) return false;
            ReleaseEncodedAccounting(expected);
            return true;
        }
    }

    private void ReleaseEncodedAccounting(Lazy<byte[]> entry)
    {
        if (_accountedEncodedEntries.Remove(entry)) Interlocked.Add(ref _encodedBytes, -entry.Value.LongLength);
    }

    private void AddEvictions(int count)
    {
        if (count > 0) Interlocked.Add(ref _evictions, count);
    }

    private static bool RemoveExact<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dictionary, TKey key, TValue value)
        where TKey : notnull
        where TValue : class =>
        ((ICollection<KeyValuePair<TKey, TValue>>)dictionary).Remove(new KeyValuePair<TKey, TValue>(key, value));

    private sealed class CacheStore<TKey> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, Lazy<object>> _entries = new();
        private readonly ConcurrentQueue<(TKey Key, Lazy<object> Entry)> _order = new();

        public int Count => _entries.Count;

        public Lazy<object> GetOrAdd(TKey key, Lazy<object> candidate, out bool added)
        {
            var actual = _entries.GetOrAdd(key, candidate);
            added = ReferenceEquals(actual, candidate);
            if (added) _order.Enqueue((key, actual));
            return actual;
        }

        public bool TryRemove(TKey key, Lazy<object> expected) => RemoveExact(_entries, key, expected);

        public bool TryRemoveOldest()
        {
            while (_order.TryDequeue(out var oldest))
                if (RemoveExact(_entries, oldest.Key, oldest.Entry)) return true;
            return false;
        }

        public int RemoveWhere(Func<TKey, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var count = 0;
            foreach (var entry in _entries)
                if (predicate(entry.Key) && RemoveExact(_entries, entry.Key, entry.Value)) count++;
            return count;
        }

        public int Clear()
        {
            var count = 0;
            foreach (var entry in _entries)
                if (RemoveExact(_entries, entry.Key, entry.Value)) count++;
            while (_order.TryDequeue(out _)) { }
            return count;
        }
    }
}
