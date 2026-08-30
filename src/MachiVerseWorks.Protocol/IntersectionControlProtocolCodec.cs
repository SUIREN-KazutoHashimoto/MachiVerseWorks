using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class IntersectionControlProtocolCodec
{
    private const int HeaderLength = 31;
    private const int MovementLength = 63;

    public static byte[] Serialize(IntersectionControlSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsIntersectionControl)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Intersection control snapshots require Protocol 2.4 or newer.");
        ValidateMessage(message);

        var payloadLength = checked(HeaderLength + checked(message.Movements.Count * MovementLength));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(message), "Intersection control snapshot exceeds the maximum protocol payload size.");

        var frame = new byte[checked(ProtocolFrameHeader.Size + payloadLength)];
        ProtocolFrameHeader.Write(
            frame,
            new ProtocolFrameHeader(version, MessageType.IntersectionControlSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        BinaryPrimitives.WriteUInt64LittleEndian(payload, message.TickCount);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[8..], message.IntersectionNodeId);
        payload[16] = (byte)message.Mode;
        BinaryPrimitives.WriteUInt16LittleEndian(payload[17..], message.PhaseIndex);
        BinaryPrimitives.WriteUInt64LittleEndian(payload[19..], message.PhaseTick);
        BinaryPrimitives.WriteUInt32LittleEndian(payload[27..], checked((uint)message.Movements.Count));

        var offset = HeaderLength;
        foreach (var movement in message.Movements)
        {
            BinaryPrimitives.WriteUInt64LittleEndian(payload[offset..], movement.MovementId);
            BinaryPrimitives.WriteUInt64LittleEndian(payload[(offset + 8)..], movement.ConnectionId);
            BinaryPrimitives.WriteUInt64LittleEndian(payload[(offset + 16)..], movement.FromLaneId);
            BinaryPrimitives.WriteUInt64LittleEndian(payload[(offset + 24)..], movement.ToLaneId);
            payload[offset + 32] = (byte)movement.TurnMovement;
            WriteDouble(payload[(offset + 33)..], movement.StopLineX);
            WriteDouble(payload[(offset + 41)..], movement.StopLineY);
            WriteDouble(payload[(offset + 49)..], movement.StopLineZ);
            payload[offset + 57] = (byte)movement.Indication;
            BinaryPrimitives.WriteUInt32LittleEndian(payload[(offset + 58)..], movement.QueueLength);
            payload[offset + 62] = movement.EntryGrantedThisTick ? (byte)1 : (byte)0;
            offset += MovementLength;
        }
        return frame;
    }

    public static bool TryDeserialize(
        ReadOnlySpan<byte> frame,
        out IntersectionControlSnapshotMessage message,
        out ProtocolDecodeError error)
    {
        message = null!;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.IntersectionControlSnapshot)
        {
            error = ProtocolDecodeError.UnknownMessageType;
            return false;
        }
        if (!header.Version.SupportsIntersectionControl || header.PayloadLength < HeaderLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var payload = frame[ProtocolFrameHeader.Size..];
        var tickCount = BinaryPrimitives.ReadUInt64LittleEndian(payload);
        var nodeId = BinaryPrimitives.ReadUInt64LittleEndian(payload[8..]);
        var mode = (ProtocolIntersectionControlMode)payload[16];
        var phaseIndex = BinaryPrimitives.ReadUInt16LittleEndian(payload[17..]);
        var phaseTick = BinaryPrimitives.ReadUInt64LittleEndian(payload[19..]);
        var movementCount = BinaryPrimitives.ReadUInt32LittleEndian(payload[27..]);
        var expectedLength = (ulong)HeaderLength + (ulong)movementCount * MovementLength;
        if (expectedLength != header.PayloadLength || nodeId == 0 || !Enum.IsDefined(mode))
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var movements = new ProtocolIntersectionMovementState[checked((int)movementCount)];
        var offset = HeaderLength;
        for (var index = 0; index < movements.Length; index++)
        {
            var movementId = BinaryPrimitives.ReadUInt64LittleEndian(payload[offset..]);
            var connectionId = BinaryPrimitives.ReadUInt64LittleEndian(payload[(offset + 8)..]);
            var fromLaneId = BinaryPrimitives.ReadUInt64LittleEndian(payload[(offset + 16)..]);
            var toLaneId = BinaryPrimitives.ReadUInt64LittleEndian(payload[(offset + 24)..]);
            var turn = (ProtocolTurnMovement)payload[offset + 32];
            var stopLineX = ReadDouble(payload[(offset + 33)..]);
            var stopLineY = ReadDouble(payload[(offset + 41)..]);
            var stopLineZ = ReadDouble(payload[(offset + 49)..]);
            var indication = (ProtocolSignalIndication)payload[offset + 57];
            var queueLength = BinaryPrimitives.ReadUInt32LittleEndian(payload[(offset + 58)..]);
            var granted = payload[offset + 62];
            if (movementId == 0
                || connectionId == 0
                || fromLaneId == 0
                || toLaneId == 0
                || !Enum.IsDefined(turn)
                || !double.IsFinite(stopLineX)
                || !double.IsFinite(stopLineY)
                || !double.IsFinite(stopLineZ)
                || !Enum.IsDefined(indication)
                || granted > 1)
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            movements[index] = new ProtocolIntersectionMovementState(
                movementId,
                connectionId,
                fromLaneId,
                toLaneId,
                turn,
                stopLineX,
                stopLineY,
                stopLineZ,
                indication,
                queueLength,
                granted != 0);
            offset += MovementLength;
        }

        message = new IntersectionControlSnapshotMessage(tickCount, nodeId, mode, phaseIndex, phaseTick, movements);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void ValidateMessage(IntersectionControlSnapshotMessage message)
    {
        if (message.IntersectionNodeId == 0) throw new ArgumentOutOfRangeException(nameof(message));
        if (!Enum.IsDefined(message.Mode)) throw new ArgumentOutOfRangeException(nameof(message));
        ArgumentNullException.ThrowIfNull(message.Movements);
        foreach (var movement in message.Movements)
        {
            if (movement.MovementId == 0 || movement.ConnectionId == 0 || movement.FromLaneId == 0 || movement.ToLaneId == 0)
                throw new ArgumentOutOfRangeException(nameof(message));
            if (!Enum.IsDefined(movement.TurnMovement)
                || !Enum.IsDefined(movement.Indication)
                || !double.IsFinite(movement.StopLineX)
                || !double.IsFinite(movement.StopLineY)
                || !double.IsFinite(movement.StopLineZ))
                throw new ArgumentOutOfRangeException(nameof(message));
        }
    }

    private static void WriteDouble(Span<byte> destination, double value) =>
        BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value));

    private static double ReadDouble(ReadOnlySpan<byte> source) =>
        BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source));
}
