using MachiVerseWorks.Protocol;
using MachiVerseWorks.Server;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class GatewayPhase4VehicleInspectionTests
{
    [TestMethod]
    public void VehicleInspectionUsesAuthoritativeRoadTrafficSnapshot()
    {
        var vehicle = new VehicleSnapshot(
            new VehicleId(88),
            new LaneId(3),
            2,
            0.25,
            120,
            new WorldPoint(10, 2, 0),
            new WorldVector(8, 0, 0),
            new WorldVector(1, 0, 0),
            8,
            new VehicleDimensions(4.5, 1.8, 1.5),
            VehicleMovementState.Driving,
            900);

        var message = EntityInspectionMessageMapper.Create(
            new EntityInspectionTarget(ProtocolEntityType.Vehicle, 88),
            new PopulationPublishSnapshot(
                1,
                1,
                900,
                new PopulationStatistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 900),
                new Dictionary<ulong, PersonSnapshot>()),
            new Dictionary<ulong, VehicleSnapshot> { [88] = vehicle },
            new Dictionary<ulong, TrainSnapshot>(),
            null);

        Assert.IsTrue(message.Found);
        Assert.AreEqual(ProtocolEntityType.Vehicle, message.EntityType);
        Assert.IsTrue(message.CurrentState.Any(field => field.Name == "laneId" && field.Value == "3"));
        Assert.IsTrue(message.CurrentState.Any(field => field.Name == "state" && field.Value == "Driving"));
        Assert.IsTrue(message.CurrentState.Any(field => field.Name == "speedMetersPerSecond" && field.Value == "8"));
        Assert.AreEqual(0, message.RecentPast.Count);
        Assert.IsFalse(message.PlannedFutureAvailable);
    }
}
