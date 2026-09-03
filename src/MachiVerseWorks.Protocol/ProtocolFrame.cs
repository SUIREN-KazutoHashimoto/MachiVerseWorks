using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public enum ProtocolDecodeError
{
    None = 0,
    FrameTooShort,
    InvalidMagic,
    UnsupportedFlags,
    PayloadTooLarge,
    FrameLengthMismatch,
    UnknownMessageType,
    InvalidPayload,
}

public readonly record struct ProtocolFrameHeader(
    ProtocolVersion Version,
    MessageType MessageType,
    uint PayloadLength)
{
    public const int Size = 16;
    public const uint Magic = 0x5057564D;
    public const ushort SupportedFlags = 0;
    public const uint MaxPayloadLength = 1_048_576;

    internal static int GetFrameLength(int payloadLength)
    {
        if (payloadLength < 0 || (uint)payloadLength > MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(payloadLength), payloadLength, $"Protocol payload must be between 0 and {MaxPayloadLength} bytes.");
        return checked(Size + payloadLength);
    }

    internal static void Write(Span<byte> destination, ProtocolFrameHeader header)
    {
        if (destination.Length < Size)
        {
            throw new ArgumentException("Destination is too short for a protocol frame header.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt32LittleEndian(destination, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[4..], header.Version.Major);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[6..], header.Version.Minor);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[8..], (ushort)header.MessageType);
        BinaryPrimitives.WriteUInt16LittleEndian(destination[10..], SupportedFlags);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[12..], header.PayloadLength);
    }

    public static bool TryRead(
        ReadOnlySpan<byte> frame,
        out ProtocolFrameHeader header,
        out ProtocolDecodeError error)
    {
        header = default;

        if (frame.Length < Size)
        {
            error = ProtocolDecodeError.FrameTooShort;
            return false;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(frame) != Magic)
        {
            error = ProtocolDecodeError.InvalidMagic;
            return false;
        }

        if (BinaryPrimitives.ReadUInt16LittleEndian(frame[10..]) != SupportedFlags)
        {
            error = ProtocolDecodeError.UnsupportedFlags;
            return false;
        }

        var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(frame[12..]);
        if (payloadLength > MaxPayloadLength)
        {
            error = ProtocolDecodeError.PayloadTooLarge;
            return false;
        }

        if ((long)Size + payloadLength != frame.Length)
        {
            error = ProtocolDecodeError.FrameLengthMismatch;
            return false;
        }

        var version = new ProtocolVersion(
            BinaryPrimitives.ReadUInt16LittleEndian(frame[4..]),
            BinaryPrimitives.ReadUInt16LittleEndian(frame[6..]));
        var messageType = (MessageType)BinaryPrimitives.ReadUInt16LittleEndian(frame[8..]);

        header = new ProtocolFrameHeader(version, messageType, payloadLength);
        error = ProtocolDecodeError.None;
        return true;
    }
}

public sealed record ProtocolEnvelope(
    ProtocolVersion Version,
    IProtocolMessage Message);
