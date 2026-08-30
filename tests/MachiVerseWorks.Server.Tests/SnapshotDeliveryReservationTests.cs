using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SnapshotDeliveryReservationTests
{
    [TestMethod]
    public async Task ReservationBlocksDuplicateWorkBeforeDeliveryStarts()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var connectionId = Guid.NewGuid();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(scheduler.TryReserve(connectionId));
        Assert.IsFalse(scheduler.TryReserve(connectionId));
        Assert.AreEqual(0, scheduler.InFlightCount);

        scheduler.StartReserved(connectionId, async () =>
        {
            started.SetResult();
            await release.Task;
        });

        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(1, scheduler.InFlightCount);
        Assert.IsFalse(scheduler.TryReserve(connectionId));

        release.SetResult();
        await WaitUntilAsync(() => scheduler.InFlightCount == 0, TimeSpan.FromSeconds(1));
        Assert.IsTrue(scheduler.TryReserve(connectionId));
        scheduler.ReleaseReservation(connectionId);
    }

    [TestMethod]
    public void ReleasedReservationCanBeReusedWithoutStartingDelivery()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var connectionId = Guid.NewGuid();

        Assert.IsTrue(scheduler.TryReserve(connectionId));
        scheduler.ReleaseReservation(connectionId);
        Assert.IsTrue(scheduler.TryReserve(connectionId));
        scheduler.ReleaseReservation(connectionId);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Condition was not satisfied before timeout.");
            await Task.Delay(10);
        }
    }
}
