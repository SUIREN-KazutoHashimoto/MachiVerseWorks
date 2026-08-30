using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class PedestrianSnapshotMessagePlannerTests
{
    [TestMethod]
    public void PlannerSpawnsUpdatesAndRemovesPedestriansBySubscriptionVisibility()
    {
        var first = new PedestrianSnapshot(
            new PedestrianId(1),
            new TripRequestId(10),
            new WorldPoint(1, 2, 3),
            new WorldVector(0.5, 0, 0),
            1.4,
            PedestrianMovementState.Walking,
            20);

        var spawnPlan = PedestrianSnapshotMessagePlanner.Create([first], new HashSet<ulong>(), 20);
        Assert.HasCount(1, spawnPlan.Messages);
        Assert.IsInstanceOfType<PedestrianSpawnMessage>(spawnPlan.Messages[0]);
        CollectionAssert.AreEquivalent(new ulong[] { 1 }, spawnPlan.CurrentPedestrianIds.ToArray());

        var updatePlan = PedestrianSnapshotMessagePlanner.Create([first with { Position = new WorldPoint(2, 2, 3), TickCount = 21 }], spawnPlan.CurrentPedestrianIds, 21);
        Assert.HasCount(1, updatePlan.Messages);
        Assert.IsInstanceOfType<PedestrianUpdateMessage>(updatePlan.Messages[0]);

        var removePlan = PedestrianSnapshotMessagePlanner.Create([], updatePlan.CurrentPedestrianIds, 22);
        Assert.HasCount(1, removePlan.Messages);
        var remove = (PedestrianRemoveMessage)removePlan.Messages[0];
        Assert.AreEqual(1UL, remove.PedestrianId);
        Assert.AreEqual(22UL, remove.TickCount);
    }
}
