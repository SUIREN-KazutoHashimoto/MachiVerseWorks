using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationCacheIdentityTests
{
    [TestMethod]
    public void ChunkIdentityReusesPrecomputedVolumeIdentity()
    {
        var volume = new WorldVolume(-10, -20, -30, 40, 50, 60);
        var volumeIdentity = ObservationCacheIdentity.ForVolume(volume);

        var chunkIdentity = ObservationCacheIdentity.ForChunk(volumeIdentity, 7);

        Assert.AreEqual($"{volumeIdentity}:7", chunkIdentity);
    }
}
