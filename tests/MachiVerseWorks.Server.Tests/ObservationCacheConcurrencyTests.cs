using MachiVerseWorks.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationCacheConcurrencyTests
{
    [TestMethod]
    public async Task EvictedInFlightEncodingDoesNotLeavePhantomBytes()
    {
        var cache = new ObservationCache(new ObservationCacheOptions(RetainedDynamicRevisions: 0));
        var staleRevision = new ObservationRevision(1, 1);
        var staleKey = new EncodedObservationCacheKey("race", ProtocolVersion.Current, staleRevision, "stale");
        using var encoderStarted = new ManualResetEventSlim();
        using var releaseEncoder = new ManualResetEventSlim();

        var encoding = Task.Run(() => cache.GetOrEncode(staleKey, () =>
        {
            encoderStarted.Set();
            releaseEncoder.Wait();
            return new byte[16];
        }));

        Assert.IsTrue(encoderStarted.Wait(TimeSpan.FromSeconds(5)));
        cache.ObserveRevision(new ObservationRevision(1, 2));
        releaseEncoder.Set();

        var staleFrame = await encoding;
        Assert.AreEqual(16, staleFrame.Length);
        var afterEviction = cache.CreateMetricsSnapshot();
        Assert.AreEqual(0L, afterEviction.EncodedBytes);
        Assert.AreEqual(0, afterEviction.EncodedEntries);

        var currentRevision = new ObservationRevision(1, 2);
        var currentKey = new EncodedObservationCacheKey("race", ProtocolVersion.Current, currentRevision, "current");
        _ = cache.GetOrEncode(currentKey, () => new byte[16]);

        var afterCurrent = cache.CreateMetricsSnapshot();
        Assert.AreEqual(16L, afterCurrent.EncodedBytes);
        Assert.AreEqual(1, afterCurrent.EncodedEntries);
    }
}
