using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SubscriptionAreaPolicyTests
{
    [TestMethod]
    public void AreaAtCellBudgetIsAccepted()
    {
        var area = new WorldRect(0d, 0d, 4032d, 4032d);

        var accepted = SubscriptionAreaPolicy.TryValidate(area, 64d, 4096, out var detailCode);

        Assert.IsTrue(accepted);
        Assert.IsNull(detailCode);
    }

    [TestMethod]
    public void AreaOutsideSpatialGridIsRejected()
    {
        var area = new WorldRect(-1e300, -1e300, 1e300, 1e300);

        var accepted = SubscriptionAreaPolicy.TryValidate(area, 64d, 4096, out var detailCode);

        Assert.IsFalse(accepted);
        Assert.AreEqual(SubscriptionAreaPolicy.OutsideSpatialGridDetailCode, detailCode);
    }

    [TestMethod]
    public void AreaExceedingCellBudgetIsRejected()
    {
        var area = new WorldRect(0d, 0d, 4096d, 4096d);

        var accepted = SubscriptionAreaPolicy.TryValidate(area, 64d, 4096, out var detailCode);

        Assert.IsFalse(accepted);
        Assert.AreEqual(SubscriptionAreaPolicy.TooManyCellsDetailCode, detailCode);
    }
}
