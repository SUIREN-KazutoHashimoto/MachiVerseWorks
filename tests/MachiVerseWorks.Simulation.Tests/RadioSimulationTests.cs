using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RadioSimulationTests
{
    [TestMethod]
    public void BuildingObstructionAddsDeterministicLoss()
    {
        var clear = CreatePointToPointWorld(addObstruction: false, out _, out _, out var clearLink);
        var blocked = CreatePointToPointWorld(addObstruction: true, out _, out _, out var blockedLink);
        clear.Step();
        blocked.Step();

        Assert.IsTrue(clear.TryGetRadioLinkSnapshot(clearLink, out var clearSnapshot));
        Assert.IsTrue(blocked.TryGetRadioLinkSnapshot(blockedLink, out var blockedSnapshot));
        Assert.IsTrue(blockedSnapshot.PathLossDb >= clearSnapshot.PathLossDb + 17.9d);
        Assert.IsTrue(blockedSnapshot.ReceivedPowerDbm < clearSnapshot.ReceivedPowerDbm);
    }

    [TestMethod]
    public void OverlappingEmissionInterferenceReducesSinrDeterministically()
    {
        var world = CreatePointToPointWorld(addObstruction: false, out var targetReceiver, out _, out var targetLink);
        var band = world.CreateSpectrumBand("interference", 5_000d, 5_500d);
        var channel = world.CreateRadioChannel(band, 5_205d, 20d);
        var interfererSite = world.CreateRadioSite(new WorldPoint(120d, 80d, 0d), RadioSiteKind.Macro);
        var antenna = world.CreateRadioAntenna(interfererSite, new WorldVector(0d, 0d, 25d), new WorldVector(1d, 0d, 0d), 15d, RadioAntennaPatternKind.Directional, 120d, 20d);
        var transmitter = world.CreateRadioTransmitter(interfererSite, antenna, 46d);
        var emission = world.CreateRadioEmission(transmitter, channel, 43d, 0.6d, isInService: false);

        world.Step();
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(targetLink, out var withoutInterference));
        Assert.AreEqual(1, world.QueryRadioEmissionCandidates(targetReceiver).Length);

        world.SetRadioEmissionInService(emission, true);
        world.Step();

        Assert.IsTrue(world.TryGetRadioLinkSnapshot(targetLink, out var withInterference));
        Assert.IsTrue(withInterference.InterferenceDbm > withoutInterference.InterferenceDbm);
        Assert.IsTrue(withInterference.SinrDb < withoutInterference.SinrDb);
        Assert.AreEqual(2, world.QueryRadioEmissionCandidates(targetReceiver).Length);
        Assert.IsTrue(world.CreateSpectrumConflicts().Count > 0);
    }

    [TestMethod]
    public void PowerAndOpticalBackhaulOutagesStopAndRecoverRadioLink()
    {
        var world = CreatePointToPointWorld(addObstruction: false, out _, out var sourceSite, out var link);
        var building = world.CreateBuilding(new WorldVolume(-15d, -15d, -1d, 15d, 15d, 12d), BuildingKind.Commercial);
        var powerSource = world.CreatePowerNode(new WorldPoint(-30d, 0d, 0d), PowerNodeKind.GeneratorBus);
        var powerLoad = world.CreatePowerNode(new WorldPoint(0d, 0d, 0d), PowerNodeKind.Load);
        var powerLine = world.CreatePowerLine(powerSource, powerLoad, 10d);
        world.CreateGenerator(powerSource, 5d);
        world.CreatePowerLoad(powerLoad, 1d, building);

        var opticalNode = world.CreateOpticalNode(new WorldPoint(0d, -5d, 2d), OpticalNodeKind.BackboneGateway);
        world.CreateOpticalEquipment(opticalNode, OpticalEquipmentKind.Router, 20d, requiresPower: false);
        var backhaul = world.CreateOpticalBackhaul(opticalNode, 20d);
        world.BindRadioSiteInfrastructure(sourceSite, building, backhaul, requiresPower: true);

        world.Step();
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var online));
        Assert.AreNotEqual(RadioLinkState.OutOfService, online.State);

        world.SetPowerLineInService(powerLine, false);
        world.Step();
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var powerOutage));
        Assert.AreEqual(RadioLinkState.OutOfService, powerOutage.State);

        world.SetPowerLineInService(powerLine, true);
        world.Step();
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var powerRecovered));
        Assert.AreNotEqual(RadioLinkState.OutOfService, powerRecovered.State);

        world.SetOpticalBackhaulInService(backhaul, false);
        world.Step();
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var backhaulOutage));
        Assert.AreEqual(RadioLinkState.OutOfService, backhaulOutage.State);

        world.SetOpticalBackhaulInService(backhaul, true);
        world.Step();
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var recovered));
        Assert.AreNotEqual(RadioLinkState.OutOfService, recovered.State);
    }

    [TestMethod]
    public void CheckpointRestoresExplicitRadioEntitiesAndStableIds()
    {
        var world = CreatePointToPointWorld(addObstruction: true, out _, out var sourceSite, out var link);
        world.Step();
        var checkpoint = world.CreateCheckpoint();

        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        var original = world.CreateRadioSnapshot();
        var actual = restored.CreateRadioSnapshot();

        Assert.AreEqual(original.Statistics, actual.Statistics);
        CollectionAssert.AreEqual(original.Antennas!.ToArray(), actual.Antennas!.ToArray());
        CollectionAssert.AreEqual(original.Transmitters!.ToArray(), actual.Transmitters!.ToArray());
        CollectionAssert.AreEqual(original.Receivers!.ToArray(), actual.Receivers!.ToArray());
        CollectionAssert.AreEqual(original.Emissions!.ToArray(), actual.Emissions!.ToArray());
        Assert.IsTrue(restored.TryGetRadioLinkSnapshot(link, out var restoredLink));
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var expectedLink));
        Assert.AreEqual(expectedLink, restoredLink);

        var nextAntenna = restored.CreateRadioAntenna(sourceSite, new WorldVector(0d, 0d, 10d), new WorldVector(1d, 0d, 0d), 0d);
        Assert.AreEqual(checkpoint.Economy!.Radio!.NextAntennaId, nextAntenna.Value);
    }

    [TestMethod]
    public void CandidateQueryFiltersByReceiverBandAndDistance()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2805));
        var lowBand = world.CreateSpectrumBand("low", 900d, 1_000d);
        var highBand = world.CreateSpectrumBand("high", 5_000d, 5_500d);
        var lowChannel = world.CreateRadioChannel(lowBand, 950d, 10d);
        var highChannel = world.CreateRadioChannel(highBand, 5_200d, 20d);
        var receiverSite = world.CreateRadioSite(new WorldPoint(0d, 0d, 0d));
        var receiverAntenna = world.CreateRadioAntenna(receiverSite, new WorldVector(0d, 0d, 10d), new WorldVector(1d, 0d, 0d), 0d);
        var receiver = world.CreateRadioReceiver(receiverSite, receiverAntenna, 900d, 1_000d, -110d);

        _ = CreateEmission(world, new WorldPoint(500d, 0d, 0d), lowChannel);
        _ = CreateEmission(world, new WorldPoint(500d, 100d, 0d), highChannel);
        _ = CreateEmission(world, new WorldPoint(25_000d, 0d, 0d), lowChannel);

        var candidates = world.QueryRadioEmissionCandidates(receiver);
        Assert.AreEqual(1, candidates.Length);
        Assert.AreEqual(950d, candidates[0].CenterFrequencyMegahertz, 1e-9);
    }

    private static SimulationWorld CreatePointToPointWorld(bool addObstruction, out RadioReceiverId receiverId, out RadioSiteId sourceSite, out RadioLinkId linkId)
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2801));
        if (addObstruction) world.CreateBuilding(new WorldVolume(450d, -60d, 0d, 550d, 60d, 80d), BuildingKind.Commercial);
        var band = world.CreateSpectrumBand("radio-test", 5_000d, 5_500d);
        var channel = world.CreateRadioChannel(band, 5_200d, 20d);
        sourceSite = world.CreateRadioSite(new WorldPoint(0d, 0d, 0d), RadioSiteKind.PointToPoint);
        var receiverSite = world.CreateRadioSite(new WorldPoint(1_000d, 0d, 0d), RadioSiteKind.PointToPoint);
        var sourceAntenna = world.CreateRadioAntenna(sourceSite, new WorldVector(0d, 0d, 25d), new WorldVector(1d, 0d, 0d), 15d, RadioAntennaPatternKind.Directional, 60d, 25d);
        var receiverAntenna = world.CreateRadioAntenna(receiverSite, new WorldVector(0d, 0d, 25d), new WorldVector(-1d, 0d, 0d), 10d, RadioAntennaPatternKind.Directional, 60d, 25d);
        var transmitter = world.CreateRadioTransmitter(sourceSite, sourceAntenna, 46d);
        receiverId = world.CreateRadioReceiver(receiverSite, receiverAntenna, 5_000d, 5_500d, -110d);
        var emission = world.CreateRadioEmission(transmitter, channel, 43d, 0.5d);
        linkId = world.CreateRadioLink(emission, receiverId, 6d);
        return world;
    }

    private static RadioEmissionId CreateEmission(SimulationWorld world, WorldPoint position, RadioChannelId channel)
    {
        var site = world.CreateRadioSite(position, RadioSiteKind.Macro);
        var antenna = world.CreateRadioAntenna(site, new WorldVector(0d, 0d, 15d), new WorldVector(1d, 0d, 0d), 5d);
        var transmitter = world.CreateRadioTransmitter(site, antenna, 40d);
        return world.CreateRadioEmission(transmitter, channel, 37d, 0.25d);
    }
}
