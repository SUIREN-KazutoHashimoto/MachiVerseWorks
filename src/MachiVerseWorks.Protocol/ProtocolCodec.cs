using System.Buffers.Binary;
using System.Text;

namespace MachiVerseWorks.Protocol;

public static class ProtocolCodec
{
    private const int HelloPayloadLength = 0, HelloAckPayloadLength = 6, SubscribeVolumePayloadLength = 48, AgentStatePayloadLength = 64, AgentRemovePayloadLength = 16;
    private const int RoadHeaderLength = 28, RoadNodeLength = 33, RoadSegmentLength = 25, LaneLength = 35, LaneConnectionLength = 33, RoadAccessPointLength = 41;
    private const int MaximumErrorParameters = 16, MaximumErrorParameterKeyBytes = 64, MaximumErrorParameterValueBytes = 256;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Serialize(IProtocolMessage message, ProtocolVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(message); var selectedVersion = version ?? ProtocolVersion.Current;
        if (message is RoadNetworkSnapshotMessage && !selectedVersion.SupportsRoadNetwork) throw new ArgumentException($"Road Network messages require Protocol 2.1 or newer, but {selectedVersion} was selected.", nameof(version));
        var payloadLength = GetPayloadLength(message);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentException("Message payload exceeds the protocol payload limit.", nameof(message));
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(selectedVersion, message.Type, (uint)payloadLength));
        WritePayload(frame.AsSpan(ProtocolFrameHeader.Size), message); return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType == MessageType.RoadNetworkSnapshot && !header.Version.SupportsRoadNetwork) { error = ProtocolDecodeError.InvalidPayload; return false; }
        if (!TryReadMessage(header.MessageType, frame[ProtocolFrameHeader.Size..], out var message, out error)) return false;
        envelope = new ProtocolEnvelope(header.Version, message); error = ProtocolDecodeError.None; return true;
    }

    private static int GetPayloadLength(IProtocolMessage message) => message switch
    {
        HelloMessage => HelloPayloadLength, HelloAckMessage => HelloAckPayloadLength, SubscribeVolumeMessage => SubscribeVolumePayloadLength,
        AgentSpawnMessage => AgentStatePayloadLength, AgentUpdateMessage => AgentStatePayloadLength, AgentRemoveMessage => AgentRemovePayloadLength,
        RoadNetworkSnapshotMessage road => GetRoadPayloadLength(road), ProtocolErrorMessage error => GetErrorPayloadLength(error),
        _ => throw new ArgumentException($"Unsupported protocol message implementation: {message.GetType().FullName}.", nameof(message)),
    };

    private static int GetRoadPayloadLength(RoadNetworkSnapshotMessage m)
    {
        ArgumentNullException.ThrowIfNull(m.Nodes); ArgumentNullException.ThrowIfNull(m.Segments); ArgumentNullException.ThrowIfNull(m.Lanes); ArgumentNullException.ThrowIfNull(m.Connections); ArgumentNullException.ThrowIfNull(m.AccessPoints);
        return checked(RoadHeaderLength + m.Nodes.Count * RoadNodeLength + m.Segments.Count * RoadSegmentLength + m.Lanes.Count * LaneLength + m.Connections.Count * LaneConnectionLength + m.AccessPoints.Count * RoadAccessPointLength);
    }

    private static int GetErrorPayloadLength(ProtocolErrorMessage m)
    {
        ArgumentNullException.ThrowIfNull(m.Parameters); if (m.Parameters.Count > MaximumErrorParameters) throw new ArgumentException($"Error messages support at most {MaximumErrorParameters} parameters.", nameof(m));
        var length = 4; foreach (var p in m.Parameters) { ArgumentNullException.ThrowIfNull(p); ValidateErrorParameter(p); length = checked(length + 2 + Utf8.GetByteCount(p.Key) + 2 + Utf8.GetByteCount(p.Value)); } return length;
    }

    private static void ValidateErrorParameter(ProtocolErrorParameter p)
    {
        ArgumentNullException.ThrowIfNull(p.Key); ArgumentNullException.ThrowIfNull(p.Value);
        if (Utf8.GetByteCount(p.Key) > MaximumErrorParameterKeyBytes) throw new ArgumentException($"Error parameter keys must be at most {MaximumErrorParameterKeyBytes} UTF-8 bytes.", nameof(p));
        if (Utf8.GetByteCount(p.Value) > MaximumErrorParameterValueBytes) throw new ArgumentException($"Error parameter values must be at most {MaximumErrorParameterValueBytes} UTF-8 bytes.", nameof(p));
    }

    private static void WritePayload(Span<byte> payload, IProtocolMessage message)
    {
        switch (message)
        {
            case HelloMessage: return;
            case HelloAckMessage m: WriteUInt16(payload, m.ProtocolVersion.Major); WriteUInt16(payload[2..], m.ProtocolVersion.Minor); WriteUInt16(payload[4..], m.TickRate); return;
            case SubscribeVolumeMessage m:
                if (!IsFiniteVolume(m.MinX, m.MinY, m.MinZ, m.MaxX, m.MaxY, m.MaxZ)) throw new ArgumentOutOfRangeException(nameof(message), "Subscribe volume coordinates must be finite and ordered.");
                WriteDouble(payload, m.MinX); WriteDouble(payload[8..], m.MinY); WriteDouble(payload[16..], m.MinZ); WriteDouble(payload[24..], m.MaxX); WriteDouble(payload[32..], m.MaxY); WriteDouble(payload[40..], m.MaxZ); return;
            case AgentSpawnMessage m: WriteAgent(payload, m.AgentId, m.X, m.Y, m.Z, m.VelocityX, m.VelocityY, m.VelocityZ, m.TickCount); return;
            case AgentUpdateMessage m: WriteAgent(payload, m.AgentId, m.X, m.Y, m.Z, m.VelocityX, m.VelocityY, m.VelocityZ, m.TickCount); return;
            case AgentRemoveMessage m: WriteUInt64(payload, m.AgentId); WriteUInt64(payload[8..], m.TickCount); return;
            case RoadNetworkSnapshotMessage m: WriteRoad(payload, m); return;
            case ProtocolErrorMessage m: WriteError(payload, m); return;
            default: throw new ArgumentException($"Unsupported protocol message implementation: {message.GetType().FullName}.", nameof(message));
        }
    }

    private static void WriteAgent(Span<byte> p, ulong id, double x, double y, double z, double vx, double vy, double vz, ulong tick)
    {
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); ValidateFinite(vx, nameof(vx)); ValidateFinite(vy, nameof(vy)); ValidateFinite(vz, nameof(vz));
        WriteUInt64(p, id); WriteDouble(p[8..], x); WriteDouble(p[16..], y); WriteDouble(p[24..], z); WriteDouble(p[32..], vx); WriteDouble(p[40..], vy); WriteDouble(p[48..], vz); WriteUInt64(p[56..], tick);
    }

    private static void WriteRoad(Span<byte> p, RoadNetworkSnapshotMessage m)
    {
        WriteUInt64(p, m.TickCount); WriteUInt32(p[8..], checked((uint)m.Nodes.Count)); WriteUInt32(p[12..], checked((uint)m.Segments.Count)); WriteUInt32(p[16..], checked((uint)m.Lanes.Count)); WriteUInt32(p[20..], checked((uint)m.Connections.Count)); WriteUInt32(p[24..], checked((uint)m.AccessPoints.Count));
        var o = RoadHeaderLength;
        foreach (var n in m.Nodes) { ValidateId(n.Id, nameof(m)); ValidateFinite(n.X, nameof(m)); ValidateFinite(n.Y, nameof(m)); ValidateFinite(n.Z, nameof(m)); ValidateEnum(n.Kind, nameof(m)); WriteUInt64(p[o..], n.Id); p[o + 8] = (byte)n.Kind; WriteDouble(p[(o + 9)..], n.X); WriteDouble(p[(o + 17)..], n.Y); WriteDouble(p[(o + 25)..], n.Z); o += RoadNodeLength; }
        foreach (var s in m.Segments) { ValidateId(s.Id, nameof(m)); ValidateId(s.StartNodeId, nameof(m)); ValidateId(s.EndNodeId, nameof(m)); ValidateEnum(s.Kind, nameof(m)); WriteUInt64(p[o..], s.Id); p[o + 8] = (byte)s.Kind; WriteUInt64(p[(o + 9)..], s.StartNodeId); WriteUInt64(p[(o + 17)..], s.EndNodeId); o += RoadSegmentLength; }
        foreach (var lane in m.Lanes) { ValidateId(lane.Id, nameof(m)); ValidateId(lane.SegmentId, nameof(m)); ValidateEnum(lane.Direction, nameof(m)); if (!double.IsFinite(lane.WidthMeters) || lane.WidthMeters <= 0 || !double.IsFinite(lane.SpeedLimitMetersPerSecond) || lane.SpeedLimitMetersPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(m)); WriteUInt64(p[o..], lane.Id); WriteUInt64(p[(o + 8)..], lane.SegmentId); p[o + 16] = (byte)lane.Direction; WriteUInt16(p[(o + 17)..], lane.Order); WriteDouble(p[(o + 19)..], lane.WidthMeters); WriteDouble(p[(o + 27)..], lane.SpeedLimitMetersPerSecond); o += LaneLength; }
        foreach (var x in m.Connections) { ValidateId(x.Id, nameof(m)); ValidateId(x.FromLaneId, nameof(m)); ValidateId(x.ToLaneId, nameof(m)); ValidateId(x.ViaNodeId, nameof(m)); ValidateEnum(x.Movement, nameof(m)); WriteUInt64(p[o..], x.Id); WriteUInt64(p[(o + 8)..], x.FromLaneId); WriteUInt64(p[(o + 16)..], x.ToLaneId); WriteUInt64(p[(o + 24)..], x.ViaNodeId); p[o + 32] = (byte)x.Movement; o += LaneConnectionLength; }
        foreach (var a in m.AccessPoints) { ValidateId(a.Id, nameof(m)); ValidateId(a.SegmentId, nameof(m)); if (!double.IsFinite(a.SegmentOffset) || a.SegmentOffset < 0 || a.SegmentOffset > 1 || (a.BuildingId == 0 && a.PoiId == 0) || a.Mode == ProtocolRoadAccessMode.None || (a.Mode & ~(ProtocolRoadAccessMode.Motor | ProtocolRoadAccessMode.Foot)) != 0) throw new ArgumentOutOfRangeException(nameof(m)); WriteUInt64(p[o..], a.Id); WriteUInt64(p[(o + 8)..], a.SegmentId); WriteDouble(p[(o + 16)..], a.SegmentOffset); WriteUInt64(p[(o + 24)..], a.BuildingId); WriteUInt64(p[(o + 32)..], a.PoiId); p[o + 40] = (byte)a.Mode; o += RoadAccessPointLength; }
    }

    private static void WriteError(Span<byte> p, ProtocolErrorMessage m)
    {
        WriteUInt16(p, (ushort)m.Code); WriteUInt16(p[2..], checked((ushort)m.Parameters.Count)); var o = 4;
        foreach (var parameter in m.Parameters) { o += WriteUtf8String(p[o..], parameter.Key); o += WriteUtf8String(p[o..], parameter.Value); }
    }
    private static int WriteUtf8String(Span<byte> d, string value) { var count = Utf8.GetByteCount(value); WriteUInt16(d, checked((ushort)count)); Utf8.GetBytes(value, d[2..]); return count + 2; }

    private static bool TryReadMessage(MessageType type, ReadOnlySpan<byte> p, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        switch (type)
        {
            case MessageType.Hello: if (p.Length != 0) return InvalidPayload(out message, out error); message = new HelloMessage(); break;
            case MessageType.HelloAck: if (p.Length != 6) return InvalidPayload(out message, out error); message = new HelloAckMessage(new ProtocolVersion(ReadUInt16(p), ReadUInt16(p[2..])), ReadUInt16(p[4..])); break;
            case MessageType.SubscribeVolume:
                if (p.Length != 48) return InvalidPayload(out message, out error); var minX = ReadDouble(p); var minY = ReadDouble(p[8..]); var minZ = ReadDouble(p[16..]); var maxX = ReadDouble(p[24..]); var maxY = ReadDouble(p[32..]); var maxZ = ReadDouble(p[40..]); if (!IsFiniteVolume(minX, minY, minZ, maxX, maxY, maxZ)) return InvalidPayload(out message, out error); message = new SubscribeVolumeMessage(minX, minY, minZ, maxX, maxY, maxZ); break;
            case MessageType.AgentSpawn: return TryReadAgent(p, static (id, x, y, z, vx, vy, vz, tick) => new AgentSpawnMessage(id, x, y, z, vx, vy, vz, tick), out message, out error);
            case MessageType.AgentUpdate: return TryReadAgent(p, static (id, x, y, z, vx, vy, vz, tick) => new AgentUpdateMessage(id, x, y, z, vx, vy, vz, tick), out message, out error);
            case MessageType.AgentRemove: if (p.Length != 16) return InvalidPayload(out message, out error); message = new AgentRemoveMessage(ReadUInt64(p), ReadUInt64(p[8..])); break;
            case MessageType.RoadNetworkSnapshot: return TryReadRoad(p, out message, out error);
            case MessageType.Error: return TryReadError(p, out message, out error);
            default: message = null!; error = ProtocolDecodeError.UnknownMessageType; return false;
        }
        error = ProtocolDecodeError.None; return true;
    }

    private static bool TryReadAgent(ReadOnlySpan<byte> p, Func<ulong, double, double, double, double, double, double, ulong, IProtocolMessage> factory, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (p.Length != AgentStatePayloadLength) return InvalidPayload(out message, out error); var id = ReadUInt64(p); var x = ReadDouble(p[8..]); var y = ReadDouble(p[16..]); var z = ReadDouble(p[24..]); var vx = ReadDouble(p[32..]); var vy = ReadDouble(p[40..]); var vz = ReadDouble(p[48..]); var tick = ReadUInt64(p[56..]);
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(vx) || !double.IsFinite(vy) || !double.IsFinite(vz)) return InvalidPayload(out message, out error); message = factory(id, x, y, z, vx, vy, vz, tick); error = ProtocolDecodeError.None; return true;
    }

    private static bool TryReadRoad(ReadOnlySpan<byte> p, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (p.Length < RoadHeaderLength) return InvalidPayload(out message, out error);
        var tick = ReadUInt64(p); var nodeCount = ReadUInt32(p[8..]); var segmentCount = ReadUInt32(p[12..]); var laneCount = ReadUInt32(p[16..]); var connectionCount = ReadUInt32(p[20..]); var accessCount = ReadUInt32(p[24..]);
        long expected = RoadHeaderLength + (long)nodeCount * RoadNodeLength + (long)segmentCount * RoadSegmentLength + (long)laneCount * LaneLength + (long)connectionCount * LaneConnectionLength + (long)accessCount * RoadAccessPointLength;
        if (expected != p.Length || nodeCount > int.MaxValue || segmentCount > int.MaxValue || laneCount > int.MaxValue || connectionCount > int.MaxValue || accessCount > int.MaxValue) return InvalidPayload(out message, out error);
        var nodes = new ProtocolRoadNode[(int)nodeCount]; var segments = new ProtocolRoadSegment[(int)segmentCount]; var lanes = new ProtocolLane[(int)laneCount]; var connections = new ProtocolLaneConnection[(int)connectionCount]; var access = new ProtocolRoadAccessPoint[(int)accessCount]; var o = RoadHeaderLength;
        for (var i = 0; i < nodes.Length; i++) { var id = ReadUInt64(p[o..]); var kind = (ProtocolRoadNodeKind)p[o + 8]; var x = ReadDouble(p[(o + 9)..]); var y = ReadDouble(p[(o + 17)..]); var z = ReadDouble(p[(o + 25)..]); if (id == 0 || !Enum.IsDefined(kind) || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) return InvalidPayload(out message, out error); nodes[i] = new(id, kind, x, y, z); o += RoadNodeLength; }
        for (var i = 0; i < segments.Length; i++) { var id = ReadUInt64(p[o..]); var kind = (ProtocolRoadKind)p[o + 8]; var start = ReadUInt64(p[(o + 9)..]); var end = ReadUInt64(p[(o + 17)..]); if (id == 0 || start == 0 || end == 0 || start == end || !Enum.IsDefined(kind)) return InvalidPayload(out message, out error); segments[i] = new(id, kind, start, end); o += RoadSegmentLength; }
        for (var i = 0; i < lanes.Length; i++) { var id = ReadUInt64(p[o..]); var segment = ReadUInt64(p[(o + 8)..]); var direction = (ProtocolLaneDirection)p[o + 16]; var order = ReadUInt16(p[(o + 17)..]); var width = ReadDouble(p[(o + 19)..]); var speed = ReadDouble(p[(o + 27)..]); if (id == 0 || segment == 0 || !Enum.IsDefined(direction) || !double.IsFinite(width) || width <= 0 || !double.IsFinite(speed) || speed <= 0) return InvalidPayload(out message, out error); lanes[i] = new(id, segment, direction, order, width, speed); o += LaneLength; }
        for (var i = 0; i < connections.Length; i++) { var id = ReadUInt64(p[o..]); var from = ReadUInt64(p[(o + 8)..]); var to = ReadUInt64(p[(o + 16)..]); var via = ReadUInt64(p[(o + 24)..]); var movement = (ProtocolTurnMovement)p[o + 32]; if (id == 0 || from == 0 || to == 0 || via == 0 || from == to || !Enum.IsDefined(movement)) return InvalidPayload(out message, out error); connections[i] = new(id, from, to, via, movement); o += LaneConnectionLength; }
        for (var i = 0; i < access.Length; i++) { var id = ReadUInt64(p[o..]); var segment = ReadUInt64(p[(o + 8)..]); var offset = ReadDouble(p[(o + 16)..]); var building = ReadUInt64(p[(o + 24)..]); var poi = ReadUInt64(p[(o + 32)..]); var mode = (ProtocolRoadAccessMode)p[o + 40]; if (id == 0 || segment == 0 || !double.IsFinite(offset) || offset < 0 || offset > 1 || (building == 0 && poi == 0) || mode == ProtocolRoadAccessMode.None || (mode & ~(ProtocolRoadAccessMode.Motor | ProtocolRoadAccessMode.Foot)) != 0) return InvalidPayload(out message, out error); access[i] = new(id, segment, offset, building, poi, mode); o += RoadAccessPointLength; }
        message = new RoadNetworkSnapshotMessage(tick, nodes, segments, lanes, connections, access); error = ProtocolDecodeError.None; return true;
    }

    private static bool TryReadError(ReadOnlySpan<byte> p, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (p.Length < 4) return InvalidPayload(out message, out error); var code = (ProtocolErrorCode)ReadUInt16(p); var count = ReadUInt16(p[2..]); if (count > MaximumErrorParameters) return InvalidPayload(out message, out error); var parameters = new List<ProtocolErrorParameter>(count); var o = 4;
        try { for (var i = 0; i < count; i++) { if (!TryReadUtf8String(p, ref o, MaximumErrorParameterKeyBytes, out var key) || !TryReadUtf8String(p, ref o, MaximumErrorParameterValueBytes, out var value)) return InvalidPayload(out message, out error); parameters.Add(new(key, value)); } }
        catch (DecoderFallbackException) { return InvalidPayload(out message, out error); }
        if (o != p.Length) return InvalidPayload(out message, out error); message = new ProtocolErrorMessage(code, parameters); error = ProtocolDecodeError.None; return true;
    }
    private static bool TryReadUtf8String(ReadOnlySpan<byte> p, ref int o, int max, out string value) { value = string.Empty; if (o > p.Length - 2) return false; var len = ReadUInt16(p[o..]); o += 2; if (len > max || o > p.Length - len) return false; value = Utf8.GetString(p.Slice(o, len)); o += len; return true; }
    private static bool InvalidPayload(out IProtocolMessage message, out ProtocolDecodeError error) { message = null!; error = ProtocolDecodeError.InvalidPayload; return false; }
    private static bool IsFiniteVolume(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) => double.IsFinite(minX) && double.IsFinite(minY) && double.IsFinite(minZ) && double.IsFinite(maxX) && double.IsFinite(maxY) && double.IsFinite(maxZ) && maxX >= minX && maxY >= minY && maxZ >= minZ;
    private static void ValidateFinite(double value, string name) { if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Protocol coordinates and velocities must be finite."); }
    private static void ValidateId(ulong value, string name) { if (value == 0) throw new ArgumentOutOfRangeException(name, "Protocol Road IDs must be greater than zero."); }
    private static void ValidateEnum<T>(T value, string name) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(name, value, $"{typeof(T).Name} is invalid."); }
    private static void WriteUInt16(Span<byte> d, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(d, v); private static ushort ReadUInt16(ReadOnlySpan<byte> s) => BinaryPrimitives.ReadUInt16LittleEndian(s);
    private static void WriteUInt32(Span<byte> d, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(d, v); private static uint ReadUInt32(ReadOnlySpan<byte> s) => BinaryPrimitives.ReadUInt32LittleEndian(s);
    private static void WriteUInt64(Span<byte> d, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(d, v); private static ulong ReadUInt64(ReadOnlySpan<byte> s) => BinaryPrimitives.ReadUInt64LittleEndian(s);
    private static void WriteDouble(Span<byte> d, double v) { ValidateFinite(v, nameof(v)); BinaryPrimitives.WriteInt64LittleEndian(d, BitConverter.DoubleToInt64Bits(v)); }
    private static double ReadDouble(ReadOnlySpan<byte> s) => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(s));
}
