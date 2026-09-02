using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RegionalBlockSubdivisionTests
{
    [TestMethod]
    public void SubdivisionUsesNearestRoadOrientationAndKeepsBlocksInsideDistrict()
    {
        var district = new District(
            new DistrictId(1),
            new SettlementId(1),
            DistrictKind.OldTown,
            new WorldVolume(-100d, -80d, 0d, 100d, 80d, 50d),
            new HumanToponymId(1),
            0.8d);
        var horizontalRoad = new RegionalCorridor(
            new RegionalCorridorId(10),
            RegionalCorridorKind.PrimaryRoad,
            new SettlementId(1),
            new SettlementId(2),
            [new WorldPoint(-150d, 0d, 0d), new WorldPoint(150d, 0d, 0d)],
            1d,
            1d,
            null);

        var first = RegionalBlockSubdivision.Subdivide(district, [horizontalRoad], roadReserveMeters: 20d);
        var second = RegionalBlockSubdivision.Subdivide(district, [horizontalRoad], roadReserveMeters: 20d);

        Assert.AreEqual(first.RoadBearingRadians, second.RoadBearingRadians);
        CollectionAssert.AreEqual(first.Blocks.ToArray(), second.Blocks.ToArray());
        Assert.AreEqual(4, first.Blocks.Count);
        Assert.IsTrue(first.Blocks.All(block => block.MinX >= district.Bounds.MinX && block.MaxX <= district.Bounds.MaxX));
        Assert.IsTrue(first.Blocks.All(block => block.MinY >= district.Bounds.MinY && block.MaxY <= district.Bounds.MaxY));
        Assert.IsTrue(first.Blocks.All(static block => block.Width > 0d && block.Depth > 0d));
        Assert.IsTrue(first.Blocks.All(block => block.MaxY <= -10d || block.MinY >= 10d));
    }

    [TestMethod]
    public void VerticalRoadLeavesDeterministicCentralRoadReserve()
    {
        var district = new District(
            new DistrictId(2),
            new SettlementId(1),
            DistrictKind.CentralBusiness,
            new WorldVolume(-90d, -100d, 0d, 90d, 100d, 60d),
            new HumanToponymId(2),
            0.9d);
        var verticalRoad = new RegionalCorridor(
            new RegionalCorridorId(20),
            RegionalCorridorKind.RegionalRoad,
            new SettlementId(1),
            new SettlementId(2),
            [new WorldPoint(0d, -150d, 0d), new WorldPoint(0d, 150d, 0d)],
            1d,
            1d,
            null);

        var result = RegionalBlockSubdivision.Subdivide(district, [verticalRoad], roadReserveMeters: 18d);

        Assert.AreEqual(4, result.Blocks.Count);
        Assert.IsTrue(result.Blocks.All(block => block.MaxX <= -9d || block.MinX >= 9d));
    }
}
