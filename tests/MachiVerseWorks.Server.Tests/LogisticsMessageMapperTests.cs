using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class LogisticsMessageMapperTests
{
    [TestMethod]
    public void MapperPrioritizesActiveAndNewestShipmentsWhenHistoryExceedsDebugLimit()
    {
        var shipments = Enumerable.Range(1, 300)
            .Select(static value => CreateShipment((ulong)value, ShipmentState.Delivered))
            .Append(CreateShipment(301, ShipmentState.InTransit))
            .ToArray();
        var snapshot = new LogisticsSnapshot(
            new LogisticsStatistics(1, 0, 1, shipments.Length, 1, 0, 0d, 1d, 300, 1, 1),
            [new CommoditySnapshot(new CommodityId(1), CommodityKind.GeneralGoods)],
            [],
            [],
            shipments);

        var message = LogisticsMessageMapper.Create(snapshot);

        Assert.AreEqual(256, message.Shipments.Count);
        Assert.IsTrue(message.Shipments.Any(static item => item.ShipmentId == 301 && item.State == Protocol.ProtocolShipmentState.InTransit));
        Assert.IsTrue(message.Shipments.Any(static item => item.ShipmentId == 300));
        Assert.IsFalse(message.Shipments.Any(static item => item.ShipmentId == 1));
    }

    private static ShipmentSnapshot CreateShipment(ulong id, ShipmentState state) => new(
        new ShipmentId(id),
        new LogisticsOrderId(id),
        new EstablishmentId(1),
        new EstablishmentId(2),
        new CommodityId(1),
        1d,
        state,
        state == ShipmentState.InTransit ? new VehicleId(id) : null,
        new RoadAccessPointId(1),
        new RoadAccessPointId(2),
        1,
        state == ShipmentState.InTransit ? 1UL : null,
        state == ShipmentState.Delivered ? 2UL : null,
        2,
        0);
}
