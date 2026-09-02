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
    private readonly ConcurrentQueue<EncodedObservationCacheKey> _encodedOrder = new();
    private readonly object _revisionGate = new();
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
        GetOrCreate(_entities, key, _options.MaxEntityEntries, factory);

    public T GetOrCreateSpatial<T>(SpatialObservationCacheKey key, Func<T> factory) where T : class =>
        GetOrCreate(_spatial, key, _options.MaxSpatialEntries, factory);

    public T GetOrCreateStatic<T>(StaticObservationCacheKey key, Func<T> factory) where T : class =>
        GetOrCreate(_static, key, _options.MaxStaticEntries, factory);

    public byte[] GetOrEncode(EncodedObservationCacheKey key, Func<byte[]> encoder)
    {
        ArgumentNullException.ThrowIfNull(encoder);
        ObserveRevision(key.Revision);
        if (!_options.Enabled)
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
            if (added)
            {
                Interlocked.Add(ref _encodedBytes, frame.LongLength);
                _encodedOrder.Enqueue(key);
                TrimEncoded();
            }
            return frame;
        }
        catch
        {
            if (added) _encoded.TryRemove(new KeyValuePair<EncodedObservationCacheKey, Lazy<byte[]>>(key, actual));
            throw;
        }
    }

    public void ObserveRevision(ObservationRevision revision)
    {
        lock (_revisionGate)
        {
            if (_generation != revision.Generation)
            {
                _generation = revision.Generation;
                _dynamicRevision = revision.Revision;
                ClearAll();
                return;
            }

            if (revision.Revision <= _dynamicRevision) return;
            _dynamicRevision = revision.Revision;
            var minimum = revision.Revision > _options.RetainedDynamicRevisions
                ? revision.Revision - _options.RetainedDynamicRevisions
                : 0UL;
            EvictOlderDynamicEntries(revision.Generation, minimum);
        }
    }

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

    private T GetOrCreate<TKey, T>(CacheStore<TKey> store, TKey key, int maximumEntries, Func<T> factory)
        where TKey : notnull
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        var revision = key switch
        {
            EntityObservationCacheKey entity => entity.Revision,
            SpatialObservationCacheKey spatial => spatial.Revision,
            StaticObservationCacheKey @static => @static.Revision,
            _ => default,
        };
        ObserveRevision(revision);
        if (!_options.Enabled)
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

    private void EvictOlderDynamicEntries(ulong generation, ulong minimumRevision)
    {
        AddEvictions(_entities.RemoveWhere(key => key.Revision.Generation != generation || key.Revision.Revision < minimumRevision));
        AddEvictions(_spatial.RemoveWhere(key => key.Revision.Generation != generation || key.Revision.Revision < minimumRevision));
        foreach (var entry in _encoded)
        {
            var key = entry.Key;
            if (key.IsStatic || (key.Revision.Generation == generation && key.Revision.Revision >= minimumRevision)) continue;
            if (_encoded.TryRemove(entry))
            {
                Interlocked.Add(ref _encodedBytes, -entry.Value.Value.LongLength);
                Interlocked.Increment(ref _evictions);
            }
        }
    }

    private void ClearAll()
    {
        AddEvictions(_entities.Clear());
        AddEvictions(_spatial.Clear());
        AddEvictions(_static.Clear());
        foreach (var entry in _encoded)
        {
            if (!_encoded.TryRemove(entry)) continue;
            if (entry.Value.IsValueCreated) Interlocked.Add(ref _encodedBytes, -entry.Value.Value.LongLength);
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
            if (!_encoded.TryRemove(oldest, out var removed)) continue;
            if (removed.IsValueCreated) Interlocked.Add(ref _encodedBytes, -removed.Value.LongLength);
            Interlocked.Increment(ref _evictions);
        }
    }

    private void AddEvictions(int count)
    {
        if (count > 0) Interlocked.Add(ref _evictions, count);
    }

    private sealed class CacheStore<TKey> where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, Lazy<object>> _entries = new();
        private readonly ConcurrentQueue<TKey> _order = new();

        public int Count => _entries.Count;

        public Lazy<object> GetOrAdd(TKey key, Lazy<object> candidate, out bool added)
        {
            var actual = _entries.GetOrAdd(key, candidate);
            added = ReferenceEquals(actual, candidate);
            if (added) _order.Enqueue(key);
            return actual;
        }

        public bool TryRemove(TKey key, Lazy<object> expected) =>
            _entries.TryRemove(new KeyValuePair<TKey, Lazy<object>>(key, expected));

        public bool TryRemoveOldest()
        {
            while (_order.TryDequeue(out var key))
                if (_entries.TryRemove(key, out _)) return true;
            return false;
        }

        public int RemoveWhere(Func<TKey, bool> predicate)
        {
            ArgumentNullException.ThrowIfNull(predicate);
            var count = 0;
            foreach (var entry in _entries)
                if (predicate(entry.Key) && _entries.TryRemove(entry)) count++;
            return count;
        }

        public int Clear()
        {
            var count = 0;
            foreach (var entry in _entries)
                if (_entries.TryRemove(entry)) count++;
            while (_order.TryDequeue(out _)) { }
            return count;
        }
    }
}
