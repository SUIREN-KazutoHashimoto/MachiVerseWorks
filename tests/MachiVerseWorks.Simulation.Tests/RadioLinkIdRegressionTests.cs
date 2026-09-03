using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RadioLinkIdRegressionTests
{
    [TestMethod]
    public void CreatingExplicitRadioLinkAdvancesNextLinkIdAndRestores()
    {
        var world = new SimulationWorld(new SimulationConfig(tickRate: 1, seed: 2811));
        var band = world.CreateSpectrumBand("radio-link-id", 5_000d, 5_500d);
        var channel = world.CreateRadioChannel(band, 5_200d, 20d);
        var sourceSite = world.CreateRadioSite(new WorldPoint(0d, 0d, 0d), RadioSiteKind.PointToPoint);
        var receiverSite = world.CreateRadioSite(new WorldPoint(1_000d, 0d, 0d), RadioSiteKind.PointToPoint);
        var sourceAntenna = world.CreateRadioAntenna(sourceSite, new WorldVector(0d, 0d, 25d), new WorldVector(1d, 0d, 0d), 15d, RadioAntennaPatternKind.Directional, 60d, 25d);
        var receiverAntenna = world.CreateRadioAntenna(receiverSite, new WorldVector(0d, 0d, 25d), new WorldVector(-1d, 0d, 0d), 10d, RadioAntennaPatternKind.Directional, 60d, 25d);
        var transmitter = world.CreateRadioTransmitter(sourceSite, sourceAntenna, 46d);
        var receiver = world.CreateRadioReceiver(receiverSite, receiverAntenna, 5_000d, 5_500d, -110d);
        var emission = world.CreateRadioEmission(transmitter, channel, 43d, 0.5d);

        var link = world.CreateRadioLink(emission, receiver, 6d);
        var checkpoint = world.CreateCheckpoint();

        Assert.AreEqual(link.Value + 1UL, checkpoint.Economy!.Radio!.NextLinkId);
        var restored = SimulationWorld.RestoreCheckpoint(checkpoint);
        Assert.IsTrue(restored.TryGetRadioLinkSnapshot(link, out _));
    }
}
