using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ThreeDimensionalServerTests
{
    [TestMethod]
    public void SubscriptionPolicyCountsCellsAcrossAltitude()
    {
        var volume = new WorldVolume(0d, 0d, 0d, 19d, 19d, 19d);
        Assert.IsTrue(SubscriptionVolumePolicy.TryValidate(volume, 10d, 8, out var acceptedDetail));
        Assert.IsNull(acceptedDetail);
        Assert.IsFalse(SubscriptionVolumePolicy.TryValidate(volume, 10d, 7, out var rejectedDetail));
        Assert.AreEqual(SubscriptionVolumePolicy.TooManyCellsDetailCode, rejectedDetail);
    }

    [TestMethod]
    public void SnapshotPlannerPreservesAltitudeAndVerticalVelocity()
    {
        AgentSnapshot[] snapshots =
        [
            new AgentSnapshot(new AgentId(7), new WorldPoint(1d, 2d, 30d), new WorldVector(4d, 5d, 6d), 12UL),
        ];
        var plan = SnapshotMessagePlanner.Create(snapshots, new HashSet<ulong>(), 12UL);
        var message = Assert.IsInstanceOfType<AgentSpawnMessage>(plan.Messages[0]);
        Assert.AreEqual(30d, message.Z);
        Assert.AreEqual(6d, message.VelocityZ);
    }
}
