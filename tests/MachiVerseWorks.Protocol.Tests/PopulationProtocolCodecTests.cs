using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class PopulationProtocolCodecTests
{
    [TestMethod]
    public void PopulationStatisticsRoundTripsInProtocol25()
    {
        var expected = new PopulationStatisticsMessage(12, 34, 20, 9, 5, 10, 8, 3, 4, 2, 5, 2, 1234);

        var frame = PopulationProtocolCodec.Serialize(expected, ProtocolVersion.Current);

        Assert.IsTrue(PopulationProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(ProtocolVersion.Current, envelope.Version);
        Assert.AreEqual(expected, envelope.Message);
    }

    [TestMethod]
    public void PersonDebugRoundTripsOptionalTripState()
    {
        var expected = new PersonDebugMessage(
            10,
            4,
            2,
            0,
            2,
            0,
            ProtocolActivityKind.Home,
            ProtocolPersonTravelState.Walking,
            0,
            9,
            ProtocolActivityKind.Work,
            55,
            ProtocolTravelMode.Foot,
            7,
            0,
            999);

        var frame = PopulationProtocolCodec.Serialize(expected, ProtocolVersion.Current);

        Assert.IsTrue(PopulationProtocolCodec.TryDeserialize(frame, out var envelope, out var error));
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(expected, envelope.Message);
    }

    [TestMethod]
    public void InspectPersonRejectsZeroId()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PopulationProtocolCodec.Serialize(new InspectPersonMessage(0), ProtocolVersion.Current));
    }

    [TestMethod]
    public void PopulationMessagesRejectOlderProtocol()
    {
        var older = new ProtocolVersion(2, 4);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PopulationProtocolCodec.Serialize(
                new PopulationStatisticsMessage(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0),
                older));
    }
}
