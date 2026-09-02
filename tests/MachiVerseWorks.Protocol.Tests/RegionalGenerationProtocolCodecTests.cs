using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class RegionalGenerationProtocolCodecTests
{
    [TestMethod]
    public void RegionalSnapshotRoundTripsOnProtocol218()
    {
        var message = CreateMessage();
        var version = new ProtocolVersion(2, 18);

        var frame = RegionalGenerationProtocolCodec.Serialize(message, version);
        var decoded = RegionalGenerationProtocolCodec.TryDeserialize(frame, out var envelope, out var error);

        Assert.IsTrue(decoded);
        Assert.AreEqual(ProtocolDecodeError.None, error);
        Assert.IsNotNull(envelope);
        Assert.AreEqual(version, envelope.Version);
        var actual = envelope.Message as RegionalGenerationSnapshotMessage;
        Assert.IsNotNull(actual);
        Assert.AreEqual(message.WorldSeed, actual.WorldSeed);
        Assert.AreEqual(message.Settlements[0], actual.Settlements[0]);
        Assert.AreEqual(message.Quality, actual.Quality);
    }

    [TestMethod]
    public void RegionalSnapshotRequiresProtocol218()
    {
        AssertThrows<ArgumentOutOfRangeException>(() =>
            RegionalGenerationProtocolCodec.Serialize(CreateMessage(), new ProtocolVersion(2, 17)));
    }

    [TestMethod]
    public void RegionalSnapshotRejectsBrokenReferences()
    {
        var message = CreateMessage() with
        {
            Corridors =
            [
                new ProtocolRegionalCorridor(
                    10,
                    2,
                    1,
                    999,
                    [new ProtocolWorldPoint(0d, 0d, 0d), new ProtocolWorldPoint(1d, 1d, 0d)],
                    0.8d,
                    100d,
                    100),
            ],
        };

        AssertThrows<ArgumentOutOfRangeException>(() =>
            RegionalGenerationProtocolCodec.Serialize(message, new ProtocolVersion(2, 18)));
    }

    [TestMethod]
    public void CurrentProtocolAdvertisesRegionalGenerationSupport()
    {
        Assert.AreEqual(new ProtocolVersion(2, 18), ProtocolVersion.Current);
        Assert.IsTrue(ProtocolVersion.Current.SupportsRegionalGeneration);
        Assert.IsFalse(new ProtocolVersion(2, 17).SupportsRegionalGeneration);
    }

    private static void AssertThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception exception)
        {
            Assert.Fail($"Expected {typeof(TException).Name}, but got {exception.GetType().Name}: {exception.Message}");
        }

        Assert.Fail($"Expected {typeof(TException).Name}, but no exception was thrown.");
    }

    private static RegionalGenerationSnapshotMessage CreateMessage() => new(
        TickCount: 12,
        WorldSeed: 30_001,
        Preset: 1,
        Iterations: 2,
        MinX: -100d,
        MinY: -100d,
        MinZ: -10d,
        MaxX: 100d,
        MaxY: 100d,
        MaxZ: 10d,
        Settlements:
        [
            new ProtocolSettlement(
                1,
                0d,
                0d,
                1d,
                1,
                1,
                2,
                2,
                new ProtocolSettlementSuitability(0.8d, 0.9d, 0.8d, 0.9d, 0.6d, 0.1d, 0.2d, 0.1d, 0.2d, 0.82d),
                1_000,
                400,
                5_000d,
                100),
            new ProtocolSettlement(
                2,
                50d,
                50d,
                1d,
                0,
                8,
                5,
                4,
                new ProtocolSettlementSuitability(0.7d, 0.8d, 0.75d, 0.8d, 0.7d, 0.2d, 0.25d, 0.1d, 0.25d, 0.75d),
                800,
                350,
                4_000d,
                101),
        ],
        GrowthEvents:
        [
            new ProtocolHistoricalGrowthEvent(20, 1, 0, 0, 0d, 0d, 1d, 100, 30, "origin"),
        ],
        Corridors:
        [
            new ProtocolRegionalCorridor(
                10,
                2,
                1,
                2,
                [new ProtocolWorldPoint(0d, 0d, 1d), new ProtocolWorldPoint(50d, 50d, 1d)],
                0.8d,
                100d,
                102),
        ],
        Districts:
        [
            new ProtocolDistrict(30, 1, 0, -10d, -10d, 0d, 10d, 10d, 20d, 103, 0.8d),
        ],
        Parcels:
        [
            new ProtocolParcel(40, 1, 30, -5d, -5d, 0d, 5d, 5d, 10d, 0, 2, 0.8d, 0.7d, 50),
        ],
        Buildings:
        [
            new ProtocolGeneratedBuilding(50, 40, 0, -4d, -4d, 0d, 4d, 4d, 9d, 3, 20, 2),
        ],
        Pois:
        [
            new ProtocolGeneratedPoi(60, 1, 0, 0d, 0d, 1d, 50, 0),
        ],
        Toponyms:
        [
            new ProtocolHumanToponym(100, 0, "Aru", 0, string.Empty, 0, 0, "phase30-regional-v1"),
            new ProtocolHumanToponym(101, 0, "Bela", 0, string.Empty, 0, 0, "phase30-regional-v1"),
            new ProtocolHumanToponym(102, 2, "Aru-Bela Road", 0, string.Empty, 0, 0, "phase30-regional-v1"),
            new ProtocolHumanToponym(103, 1, "Aru Old Town", 0, string.Empty, 0, 100, "phase30-regional-v1"),
        ],
        RoadSigns:
        [
            new ProtocolRoadSign(70, 0, 20d, 20d, 1d, 10, 2, 0, "Bela 1 km"),
        ],
        Quality: new ProtocolRegionalQualityReport(0.8d, 1d, 0.2d, 0.7d, 0.2d, 0.8d, 0.1d, 0.8d, 0.9d, 0.83d));
}
