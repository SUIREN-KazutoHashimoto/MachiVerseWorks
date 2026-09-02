using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ObservationRequestContractTests
{
    [TestMethod]
    public void ClientObservationRequestsHaveExplicitMarker()
    {
        IProtocolMessage[] requests =
        [
            new SubscribeVolumeMessage(0, 0, 0, 1, 1, 1),
            new InspectPersonMessage(1),
            new ClearPersonInspectionMessage(),
            new InspectEntityMessage(ProtocolEntityType.Building, 1),
            new ClearEntityInspectionMessage(),
        ];

        foreach (var request in requests)
            Assert.IsTrue(request is IObservationRequestMessage, $"{request.GetType().Name} must be an observation request.");
    }

    [TestMethod]
    public void HandshakeAndServerMessagesAreNotObservationRequests()
    {
        IProtocolMessage[] messages =
        [
            new HelloMessage(),
            new HelloAckMessage(ProtocolVersion.Current, 20),
            new AgentRemoveMessage(1, 1),
        ];

        foreach (var message in messages)
            Assert.IsFalse(message is IObservationRequestMessage, $"{message.GetType().Name} must not be classified as an observation request.");
    }
}
