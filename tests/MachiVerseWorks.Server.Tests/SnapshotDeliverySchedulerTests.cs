using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SnapshotDeliverySchedulerTests
{
    [TestMethod]
    public async Task SlowConnectionDoesNotBlockDifferentConnectionAndDoesNotQueueItself()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var slowConnectionId = Guid.NewGuid();
        var fastConnectionId = Guid.NewGuid();
        var slowStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSlow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fastCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.IsTrue(scheduler.TrySchedule(slowConnectionId, async () =>
        {
            slowStarted.SetResult();
            await releaseSlow.Task;
        }));
        await slowStarted.Task;

        Assert.IsFalse(scheduler.TrySchedule(slowConnectionId, () => Task.CompletedTask));
        Assert.IsTrue(scheduler.TrySchedule(fastConnectionId, () =>
        {
            fastCompleted.SetResult();
            return Task.CompletedTask;
        }));

        await fastCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(scheduler.InFlightCount >= 1);

        releaseSlow.SetResult();
        await WaitUntilAsync(() => scheduler.InFlightCount == 0, TimeSpan.FromSeconds(1));
        Assert.IsTrue(scheduler.TrySchedule(slowConnectionId, () => Task.CompletedTask));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline)
            {
                Assert.Fail("Condition was not satisfied before timeout.");
            }

            await Task.Delay(10);
        }
    }
}
