using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class SubscriptionVolumePolicyTests
{
    [TestMethod]
    public void VolumeAtThreeDimensionalCellBudgetIsAccepted()
    {
        var volume = new WorldVolume(0d, 0d, 0d, 1023d, 1023d, 1023d);
        var accepted = SubscriptionVolumePolicy.TryValidate(volume, 64d, 4096, out var detailCode);
        Assert.IsTrue(accepted);
        Assert.IsNull(detailCode);
    }

    [TestMethod]
    public void VolumeOutsideSpatialGridIsRejected()
    {
        var volume = new WorldVolume(-1e300, -1e300, -1e300, 1e300, 1e300, 1e300);
        var accepted = SubscriptionVolumePolicy.TryValidate(volume, 64d, 4096, out var detailCode);
        Assert.IsFalse(accepted);
        Assert.AreEqual(SubscriptionVolumePolicy.OutsideSpatialGridDetailCode, detailCode);
    }

    [TestMethod]
    public void VolumeExceedingThreeDimensionalCellBudgetIsRejected()
    {
        var volume = new WorldVolume(0d, 0d, 0d, 1023d, 1023d, 1087d);
        var accepted = SubscriptionVolumePolicy.TryValidate(volume, 64d, 4096, out var detailCode);
        Assert.IsFalse(accepted);
        Assert.AreEqual(SubscriptionVolumePolicy.TooManyCellsDetailCode, detailCode);
    }
}
