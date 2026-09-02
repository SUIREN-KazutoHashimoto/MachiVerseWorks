namespace MachiVerseWorks.Protocol;

public sealed record ClearPersonInspectionMessage : IObservationRequestMessage
{
    public MessageType Type => MessageType.ClearPersonInspection;
}

public static class PersonInspectionProtocolCodec
{
    public static byte[] Serialize(ClearPersonInspectionMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsPersonInspectionClear)
            throw new ArgumentOutOfRangeException(nameof(version), version, "ClearPersonInspection requires Protocol 2.9 or newer.");
        var frame = new byte[ProtocolFrameHeader.Size];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.ClearPersonInspection, 0));
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsPersonInspectionClear || header.MessageType != MessageType.ClearPersonInspection || header.PayloadLength != 0)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        envelope = new ProtocolEnvelope(header.Version, new ClearPersonInspectionMessage());
        error = ProtocolDecodeError.None;
        return true;
    }
}
