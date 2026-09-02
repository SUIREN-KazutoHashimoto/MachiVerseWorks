using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationDeliveryPlannerTests
{
    private static readonly WorldVolume Volume = new(-100d, -100d, -100d, 100d, 100d, 100d);

    [TestMethod]
    public void GenerationChangeRemovesCommittedIdsBeforeRespawningReusedIds()
    {
        var subscription = new ClientSubscriptionState(
            Volume,
            4,
            new CommittedDeliveryRevision(3, 7, 20),
            new HashSet<ulong> { 1, 99 },
            [],
            [],
            null,
            null);
        var snapshot = new EntityPublishSnapshot(
            30,
            [new AgentSnapshot(new AgentId(1), new WorldPoint(1d, 2d, 3d), new WorldVector(4d, 5d, 6d), 30)],
            [],
            [],
            [],
            []);

        var plan = ObservationDeliveryPlanner.CreateDynamicPlan(snapshot, subscription, new ProtocolVersion(2, 0), 8);

        Assert.IsTrue(plan.RequiresGenerationResync);
        Assert.AreEqual(3, plan.Agents.Messages.Count);
        Assert.AreEqual(1UL, Assert.IsInstanceOfType<AgentRemoveMessage>(plan.Agents.Messages[0]).AgentId);
        Assert.AreEqual(99UL, Assert.IsInstanceOfType<AgentRemoveMessage>(plan.Agents.Messages[1]).AgentId);
        Assert.AreEqual(1UL, Assert.IsInstanceOfType<AgentSpawnMessage>(plan.Agents.Messages[2]).AgentId);
        Assert.IsFalse(plan.Agents.Messages.Any(static message => message is AgentUpdateMessage));
    }

    [TestMethod]
    public void SameGenerationKeepsDeltaDelivery()
    {
        var subscription = new ClientSubscriptionState(
            Volume,
            4,
            new CommittedDeliveryRevision(4, 7, 20),
            new HashSet<ulong> { 1 },
            [],
            [],
            null,
            null);
        var snapshot = new EntityPublishSnapshot(
            30,
            [new AgentSnapshot(new AgentId(1), new WorldPoint(1d, 2d, 3d), new WorldVector(4d, 5d, 6d), 30)],
            [],
            [],
            [],
            []);

        var plan = ObservationDeliveryPlanner.CreateDynamicPlan(snapshot, subscription, new ProtocolVersion(2, 0), 7);

        Assert.IsFalse(plan.RequiresGenerationResync);
        Assert.AreEqual(1, plan.Agents.Messages.Count);
        Assert.IsInstanceOfType<AgentUpdateMessage>(plan.Agents.Messages[0]);
    }

    [TestMethod]
    public void StaticPlanNeverSchedulesMessagesUnsupportedByNegotiatedMinor()
    {
        var subscription = new ClientSubscriptionState(Volume, 2, [], [], []);

        var v20 = ObservationDeliveryPlanner.CreateStaticPlan(subscription, new ProtocolVersion(2, 0), 1, 5, 6);
        var v21 = ObservationDeliveryPlanner.CreateStaticPlan(subscription, new ProtocolVersion(2, 1), 1, 5, 6);
        var v26 = ObservationDeliveryPlanner.CreateStaticPlan(subscription, new ProtocolVersion(2, 6), 1, 5, 6);

        Assert.IsFalse(v20.SendRoadSnapshot);
        Assert.IsFalse(v20.SendRailwaySnapshot);
        Assert.IsTrue(v21.SendRoadSnapshot);
        Assert.IsFalse(v21.SendRailwaySnapshot);
        Assert.IsTrue(v26.SendRoadSnapshot);
        Assert.IsTrue(v26.SendRailwaySnapshot);
    }

    [TestMethod]
    public void WorldGenerationChangeInvalidatesStaticMarkersEvenWhenTopologyRevisionMatches()
    {
        var subscription = new ClientSubscriptionState(
            Volume,
            9,
            new CommittedDeliveryRevision(9, 11, 40),
            [],
            [],
            [],
            new StaticDeliveryRevision(9, 11, 3),
            new StaticDeliveryRevision(9, 11, 4));

        var plan = ObservationDeliveryPlanner.CreateStaticPlan(subscription, new ProtocolVersion(2, 6), 12, 3, 4);

        Assert.IsTrue(plan.SendRoadSnapshot);
        Assert.IsTrue(plan.SendRailwaySnapshot);
    }

    [TestMethod]
    public void StaleInspectionRevisionIsNotDelivered()
    {
        var planned = new ClientInspectionState(100, 4);
        var unchanged = new ClientInspectionState(100, 4);
        var changedPerson = new ClientInspectionState(101, 5);
        var cleared = new ClientInspectionState(null, 5);

        Assert.IsTrue(ObservationDeliveryPlanner.ShouldDeliverInspection(planned, unchanged));
        Assert.IsFalse(ObservationDeliveryPlanner.ShouldDeliverInspection(planned, changedPerson));
        Assert.IsFalse(ObservationDeliveryPlanner.ShouldDeliverInspection(planned, cleared));
    }
}
