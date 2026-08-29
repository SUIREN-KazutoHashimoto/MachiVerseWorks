using System.Net.WebSockets;
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
        scheduler.ThrowIfFaulted();
        Assert.IsTrue(scheduler.TrySchedule(slowConnectionId, () => Task.CompletedTask));
    }

    [TestMethod]
    public async Task UnexpectedDeliveryFailureIsSurfacedToTheOwner()
    {
        var scheduler = new SnapshotDeliveryScheduler();
        var expected = new InvalidOperationException("unexpected delivery failure");

        Assert.IsTrue(scheduler.TrySchedule(Guid.NewGuid(), () => Task.FromException(expected)));
        await WaitUntilAsync(() => scheduler.InFlightCount == 0, TimeSpan.FromSeconds(1));

        var actual = Assert.ThrowsExactly<InvalidOperationException>(() => scheduler.ThrowIfFaulted());
        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void FailurePolicySeparatesClientTransportFailuresFromServerFailures()
    {
        Assert.IsTrue(SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(new WebSocketException()));
        Assert.IsTrue(SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(new OperationCanceledException()));
        Assert.IsTrue(SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(new ObjectDisposedException("socket")));
        Assert.IsFalse(SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(new InvalidOperationException()));
        Assert.IsFalse(SnapshotDeliveryFailurePolicy.IsExpectedClientFailure(new OverflowException()));
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
