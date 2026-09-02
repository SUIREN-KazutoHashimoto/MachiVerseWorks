using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class PersistentRegionalEvolutionProtocolCodecTests
{
    [TestMethod]
    public void Protocol219RoundTripsPersistentRegionalEvolution()
    {
        var message = CreateMessage();

        var frame = PersistentRegionalEvolutionProtocolCodec.Serialize(message, ProtocolVersion.Current);
        var decoded = PersistentRegionalEvolutionProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(decoded);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(MessageType.PersistentRegionalEvolutionSnapshot, envelope.Message.Type);
        var actual = (PersistentRegionalEvolutionSnapshotMessage)envelope.Message;
        Assert.AreEqual(message.CurrentYear, actual.CurrentYear);
        Assert.AreEqual(message.Settlements[0], actual.Settlements[0]);
        Assert.AreEqual(message.Events[0], actual.Events[0]);
        Assert.AreEqual(message.CommutingFlows[0], actual.CommutingFlows[0]);
    }

    [TestMethod]
    public void Protocol218CannotSerializePersistentRegionalEvolution()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            PersistentRegionalEvolutionProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 18)));
    }

    private static PersistentRegionalEvolutionSnapshotMessage CreateMessage() => new(
        12,
        7_200,
        [
            new ProtocolSettlementEvolution(1, 10, 20, 0, 10_000, 4_000, 0.7, 0.6, 0.8, 12_000, 2, 0, true, 0, null),
            new ProtocolSettlementEvolution(2, 30_000, 20, 0, 4_000, 1_500, 0.5, 0.4, 0.6, 7_000, 2, 1, true, 0, null),
        ],
        [new ProtocolParcelEvolution(1, 1, 0.8, 0.7, 2, 1)],
        [new ProtocolBuildingLifecycle(1, 1, 3, 4, 10, 0.8, 0.9, 120, 0)],
        [new ProtocolServiceCatchment(1, 0, 8_000, 0.7)],
        [new ProtocolInfrastructureDemand(1, 0, 0.55, "population/jobs/accessibility")],
        [new ProtocolRegionalRelation(1, 1, 2, 0, 0.65, true, 5)],
        [new ProtocolRegionalEvolutionEvent(1, 6, 0, 1, 0, "population +120")],
        [new ProtocolRegionalCommutingFlow(1, 2, 35)],
        [new ProtocolRegionalFreightFlow(1, 2, 1, 120, 3, 80)]);
}
