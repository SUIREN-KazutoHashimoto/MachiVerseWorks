using System.Buffers.Binary;
using System.Text;

namespace MachiVerseWorks.Protocol;

public static class ProtocolCodec
{
    private const int HelloPayloadLength = 0;
    private const int HelloAckPayloadLength = 6;
    private const int SubscribeVolumePayloadLength = 48;
    private const int AgentStatePayloadLength = 64;
    private const int AgentRemovePayloadLength = 16;
    private const int MaximumErrorParameters = 16;
    private const int MaximumErrorParameterKeyBytes = 64;
    private const int MaximumErrorParameterValueBytes = 256;

    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Serialize(IProtocolMessage message, ProtocolVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        var payloadLength = GetPayloadLength(message);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength)
        {
            throw new ArgumentException("Message payload exceeds the protocol payload limit.", nameof(message));
        }

        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        var selectedVersion = version ?? ProtocolVersion.Current;
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(selectedVersion, message.Type, (uint)payloadLength));
        WritePayload(frame.AsSpan(ProtocolFrameHeader.Size), message);
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error))
        {
            return false;
        }

        if (!TryReadMessage(header.MessageType, frame[ProtocolFrameHeader.Size..], out var message, out error))
        {
            return false;
        }

        envelope = new ProtocolEnvelope(header.Version, message);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static int GetPayloadLength(IProtocolMessage message) => message switch
    {
        HelloMessage => HelloPayloadLength,
        HelloAckMessage => HelloAckPayloadLength,
        SubscribeVolumeMessage => SubscribeVolumePayloadLength,
        AgentSpawnMessage => AgentStatePayloadLength,
        AgentUpdateMessage => AgentStatePayloadLength,
        AgentRemoveMessage => AgentRemovePayloadLength,
        ProtocolErrorMessage errorMessage => GetErrorPayloadLength(errorMessage),
        _ => throw new ArgumentException($"Unsupported protocol message implementation: {message.GetType().FullName}.", nameof(message)),
    };

    private static int GetErrorPayloadLength(ProtocolErrorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Parameters);
        if (message.Parameters.Count > MaximumErrorParameters)
        {
            throw new ArgumentException($"Error messages support at most {MaximumErrorParameters} parameters.", nameof(message));
        }

        var length = sizeof(ushort) + sizeof(ushort);
        foreach (var parameter in message.Parameters)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            ValidateErrorParameter(parameter);
            length = checked(length + sizeof(ushort) + Utf8.GetByteCount(parameter.Key) + sizeof(ushort) + Utf8.GetByteCount(parameter.Value));
        }

        return length;
    }

    private static void ValidateErrorParameter(ProtocolErrorParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter.Key);
        ArgumentNullException.ThrowIfNull(parameter.Value);
        if (Utf8.GetByteCount(parameter.Key) > MaximumErrorParameterKeyBytes)
        {
            throw new ArgumentException($"Error parameter keys must be at most {MaximumErrorParameterKeyBytes} UTF-8 bytes.", nameof(parameter));
        }
        if (Utf8.GetByteCount(parameter.Value) > MaximumErrorParameterValueBytes)
        {
            throw new ArgumentException($"Error parameter values must be at most {MaximumErrorParameterValueBytes} UTF-8 bytes.", nameof(parameter));
        }
    }

    private static void WritePayload(Span<byte> payload, IProtocolMessage message)
    {
        switch (message)
        {
            case HelloMessage:
                return;
            case HelloAckMessage helloAck:
                WriteUInt16(payload, helloAck.ProtocolVersion.Major);
                WriteUInt16(payload[2..], helloAck.ProtocolVersion.Minor);
                WriteUInt16(payload[4..], helloAck.TickRate);
                return;
            case SubscribeVolumeMessage subscribeVolume:
                if (!IsFiniteVolume(subscribeVolume.MinX, subscribeVolume.MinY, subscribeVolume.MinZ, subscribeVolume.MaxX, subscribeVolume.MaxY, subscribeVolume.MaxZ))
                {
                    throw new ArgumentOutOfRangeException(nameof(message), "Subscribe volume coordinates must be finite and ordered.");
                }
                WriteDouble(payload, subscribeVolume.MinX);
                WriteDouble(payload[8..], subscribeVolume.MinY);
                WriteDouble(payload[16..], subscribeVolume.MinZ);
                WriteDouble(payload[24..], subscribeVolume.MaxX);
                WriteDouble(payload[32..], subscribeVolume.MaxY);
                WriteDouble(payload[40..], subscribeVolume.MaxZ);
                return;
            case AgentSpawnMessage spawn:
                WriteAgentStatePayload(payload, spawn.AgentId, spawn.X, spawn.Y, spawn.Z, spawn.VelocityX, spawn.VelocityY, spawn.VelocityZ, spawn.TickCount);
                return;
            case AgentUpdateMessage update:
                WriteAgentStatePayload(payload, update.AgentId, update.X, update.Y, update.Z, update.VelocityX, update.VelocityY, update.VelocityZ, update.TickCount);
                return;
            case AgentRemoveMessage remove:
                WriteUInt64(payload, remove.AgentId);
                WriteUInt64(payload[8..], remove.TickCount);
                return;
            case ProtocolErrorMessage protocolError:
                WriteErrorPayload(payload, protocolError);
                return;
            default:
                throw new ArgumentException($"Unsupported protocol message implementation: {message.GetType().FullName}.", nameof(message));
        }
    }

    private static void WriteAgentStatePayload(Span<byte> payload, ulong agentId, double x, double y, double z, double velocityX, double velocityY, double velocityZ, ulong tickCount)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        ValidateFinite(velocityX, nameof(velocityX));
        ValidateFinite(velocityY, nameof(velocityY));
        ValidateFinite(velocityZ, nameof(velocityZ));
        WriteUInt64(payload, agentId);
        WriteDouble(payload[8..], x);
        WriteDouble(payload[16..], y);
        WriteDouble(payload[24..], z);
        WriteDouble(payload[32..], velocityX);
        WriteDouble(payload[40..], velocityY);
        WriteDouble(payload[48..], velocityZ);
        WriteUInt64(payload[56..], tickCount);
    }

    private static void WriteErrorPayload(Span<byte> payload, ProtocolErrorMessage message)
    {
        WriteUInt16(payload, (ushort)message.Code);
        WriteUInt16(payload[2..], checked((ushort)message.Parameters.Count));
        var offset = 4;
        foreach (var parameter in message.Parameters)
        {
            offset += WriteUtf8String(payload[offset..], parameter.Key);
            offset += WriteUtf8String(payload[offset..], parameter.Value);
        }
    }

    private static int WriteUtf8String(Span<byte> destination, string value)
    {
        var byteCount = Utf8.GetByteCount(value);
        WriteUInt16(destination, checked((ushort)byteCount));
        Utf8.GetBytes(value, destination[2..]);
        return sizeof(ushort) + byteCount;
    }

    private static bool TryReadMessage(MessageType messageType, ReadOnlySpan<byte> payload, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        switch (messageType)
        {
            case MessageType.Hello:
                if (payload.Length != HelloPayloadLength) return InvalidPayload(out message, out error);
                message = new HelloMessage();
                error = ProtocolDecodeError.None;
                return true;
            case MessageType.HelloAck:
                if (payload.Length != HelloAckPayloadLength) return InvalidPayload(out message, out error);
                message = new HelloAckMessage(new ProtocolVersion(ReadUInt16(payload), ReadUInt16(payload[2..])), ReadUInt16(payload[4..]));
                error = ProtocolDecodeError.None;
                return true;
            case MessageType.SubscribeVolume:
                if (payload.Length != SubscribeVolumePayloadLength) return InvalidPayload(out message, out error);
                var minX = ReadDouble(payload);
                var minY = ReadDouble(payload[8..]);
                var minZ = ReadDouble(payload[16..]);
                var maxX = ReadDouble(payload[24..]);
                var maxY = ReadDouble(payload[32..]);
                var maxZ = ReadDouble(payload[40..]);
                if (!IsFiniteVolume(minX, minY, minZ, maxX, maxY, maxZ)) return InvalidPayload(out message, out error);
                message = new SubscribeVolumeMessage(minX, minY, minZ, maxX, maxY, maxZ);
                error = ProtocolDecodeError.None;
                return true;
            case MessageType.AgentSpawn:
                return TryReadAgentState(payload, static (agentId, x, y, z, velocityX, velocityY, velocityZ, tickCount) => new AgentSpawnMessage(agentId, x, y, z, velocityX, velocityY, velocityZ, tickCount), out message, out error);
            case MessageType.AgentUpdate:
                return TryReadAgentState(payload, static (agentId, x, y, z, velocityX, velocityY, velocityZ, tickCount) => new AgentUpdateMessage(agentId, x, y, z, velocityX, velocityY, velocityZ, tickCount), out message, out error);
            case MessageType.AgentRemove:
                if (payload.Length != AgentRemovePayloadLength) return InvalidPayload(out message, out error);
                message = new AgentRemoveMessage(ReadUInt64(payload), ReadUInt64(payload[8..]));
                error = ProtocolDecodeError.None;
                return true;
            case MessageType.Error:
                return TryReadError(payload, out message, out error);
            default:
                message = null!;
                error = ProtocolDecodeError.UnknownMessageType;
                return false;
        }
    }

    private static bool TryReadAgentState(ReadOnlySpan<byte> payload, Func<ulong, double, double, double, double, double, double, ulong, IProtocolMessage> factory, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (payload.Length != AgentStatePayloadLength) return InvalidPayload(out message, out error);
        var agentId = ReadUInt64(payload);
        var x = ReadDouble(payload[8..]);
        var y = ReadDouble(payload[16..]);
        var z = ReadDouble(payload[24..]);
        var velocityX = ReadDouble(payload[32..]);
        var velocityY = ReadDouble(payload[40..]);
        var velocityZ = ReadDouble(payload[48..]);
        var tickCount = ReadUInt64(payload[56..]);
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(velocityX) || !double.IsFinite(velocityY) || !double.IsFinite(velocityZ))
        {
            return InvalidPayload(out message, out error);
        }
        message = factory(agentId, x, y, z, velocityX, velocityY, velocityZ, tickCount);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static bool TryReadError(ReadOnlySpan<byte> payload, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (payload.Length < 4) return InvalidPayload(out message, out error);
        var code = (ProtocolErrorCode)ReadUInt16(payload);
        var parameterCount = ReadUInt16(payload[2..]);
        if (parameterCount > MaximumErrorParameters) return InvalidPayload(out message, out error);
        var parameters = new List<ProtocolErrorParameter>(parameterCount);
        var offset = 4;
        try
        {
            for (var index = 0; index < parameterCount; index++)
            {
                if (!TryReadUtf8String(payload, ref offset, MaximumErrorParameterKeyBytes, out var key) ||
                    !TryReadUtf8String(payload, ref offset, MaximumErrorParameterValueBytes, out var value))
                {
                    return InvalidPayload(out message, out error);
                }
                parameters.Add(new ProtocolErrorParameter(key, value));
            }
        }
        catch (DecoderFallbackException)
        {
            return InvalidPayload(out message, out error);
        }
        if (offset != payload.Length) return InvalidPayload(out message, out error);
        message = new ProtocolErrorMessage(code, parameters);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static bool TryReadUtf8String(ReadOnlySpan<byte> payload, ref int offset, int maximumBytes, out string value)
    {
        value = string.Empty;
        if (offset > payload.Length - sizeof(ushort)) return false;
        var byteLength = ReadUInt16(payload[offset..]);
        offset += sizeof(ushort);
        if (byteLength > maximumBytes || offset > payload.Length - byteLength) return false;
        value = Utf8.GetString(payload.Slice(offset, byteLength));
        offset += byteLength;
        return true;
    }

    private static bool InvalidPayload(out IProtocolMessage message, out ProtocolDecodeError error)
    {
        message = null!;
        error = ProtocolDecodeError.InvalidPayload;
        return false;
    }

    private static bool IsFiniteVolume(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    {
        return double.IsFinite(minX) && double.IsFinite(minY) && double.IsFinite(minZ) && double.IsFinite(maxX) && double.IsFinite(maxY) && double.IsFinite(maxZ) && maxX >= minX && maxY >= minY && maxZ >= minZ;
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Protocol coordinates and velocities must be finite.");
        }
    }

    private static void WriteUInt16(Span<byte> destination, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
    private static ushort ReadUInt16(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    private static void WriteUInt64(Span<byte> destination, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
    private static ulong ReadUInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt64LittleEndian(source);

    private static void WriteDouble(Span<byte> destination, double value)
    {
        ValidateFinite(value, nameof(value));
        BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value));
    }

    private static double ReadDouble(ReadOnlySpan<byte> source) => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source));
}
