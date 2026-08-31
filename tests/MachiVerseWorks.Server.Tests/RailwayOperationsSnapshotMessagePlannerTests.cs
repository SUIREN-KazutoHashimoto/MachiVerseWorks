using System.Globalization;
using MachiVerseWorks.Protocol;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RailwayOperationsSnapshotMessagePlannerTests
{
    [TestMethod]
    public void OversizedSnapshotBecomesStructuredSubscriptionError()
    {
        var message = CreateMessage(26_214);

        var planned = RailwayOperationsSnapshotMessagePlanner.Create(message);

        var error = planned as ProtocolErrorMessage;
        Assert.IsNotNull(error);
        Assert.AreEqual(ProtocolErrorCode.InvalidRequest, error.Code);
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == ProtocolErrorParameterKeys.Field && parameter.Value == "volume"));
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == ProtocolErrorParameterKeys.DetailCode && parameter.Value == RailwayOperationsSnapshotMessagePlanner.TooLargeDetailCode));
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == "payloadBytes" && parameter.Value == RailwayOperationsProtocolCodec.GetPayloadLength(message).ToString(CultureInfo.InvariantCulture)));
        Assert.IsTrue(error.Parameters.Any(parameter => parameter.Key == "maximumPayloadBytes" && parameter.Value == ProtocolFrameHeader.MaxPayloadLength.ToString(CultureInfo.InvariantCulture)));
    }

    [TestMethod]
    public void FittingSnapshotRemainsRailwayOperationsMessage()
    {
        var message = CreateMessage(1);

        var planned = RailwayOperationsSnapshotMessagePlanner.Create(message);

        Assert.AreSame(message, planned);
    }

    private static RailwayOperationsSnapshotMessage CreateMessage(int stopCount)
    {
        var stops = Enumerable.Range(0, stopCount)
            .Select(static index => new ProtocolTimetableStop(1, (ulong)index, (ulong)index, 0, 0))
            .ToArray();
        return new RailwayOperationsSnapshotMessage(1, [], [], [new ProtocolTimetable(1, stops)]);
    }
}