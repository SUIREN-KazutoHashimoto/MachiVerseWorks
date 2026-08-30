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
    private const int PedestrianStatePayloadLength = 81;
    private const int PedestrianRemovePayloadLength = 16;
    private const int RoadHeaderLength = 28;
    private const int RoadNodeLength = 33;
    private const int RoadSegmentLength = 25;
    private const int LaneLength = 35;
    private const int LaneConnectionLength = 33;
    private const int RoadAccessPointLength = 41;
    private const int MaximumErrorParameters = 16;
    private const int MaximumErrorParameterKeyBytes = 64;
    private const int MaximumErrorParameterValueBytes = 256;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Serialize(IProtocolMessage message, ProtocolVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        var selectedVersion = version ?? ProtocolVersion.Current;
        if (message is RoadNetworkSnapshotMessage && !selectedVersion.SupportsRoadNetwork)
            throw new ArgumentException($"Road Network messages require Protocol 2.1 or newer, but {selectedVersion} was selected.", nameof(version));
        if (IsPedestrianMessage(message.Type) && !selectedVersion.SupportsPedestrians)
            throw new ArgumentException($"Pedestrian messages require Protocol 2.2 or newer, but {selectedVersion} was selected.", nameof(version));
        var payloadLength = GetPayloadLength(message);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentException("Message payload exceeds the protocol payload limit.", nameof(message));
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(selectedVersion, message.Type, (uint)payloadLength));
        WritePayload(frame.AsSpan(ProtocolFrameHeader.Size), message);
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType == MessageType.RoadNetworkSnapshot && !header.Version.SupportsRoadNetwork) { error = ProtocolDecodeError.InvalidPayload; return false; }
        if (IsPedestrianMessage(header.MessageType) && !header.Version.SupportsPedestrians) { error = ProtocolDecodeError.InvalidPayload; return false; }
        if (!TryReadMessage(header.MessageType, frame[ProtocolFrameHeader.Size..], out var message, out error)) return false;
        envelope = new ProtocolEnvelope(header.Version, message);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static bool IsPedestrianMessage(MessageType type) => type is MessageType.PedestrianSpawn or MessageType.PedestrianUpdate or MessageType.PedestrianRemove;

    private static int GetPayloadLength(IProtocolMessage message) => message switch
    {
        HelloMessage => HelloPayloadLength,
        HelloAckMessage => HelloAckPayloadLength,
        SubscribeVolumeMessage => SubscribeVolumePayloadLength,
        AgentSpawnMessage => AgentStatePayloadLength,
        AgentUpdateMessage => AgentStatePayloadLength,
        AgentRemoveMessage => AgentRemovePayloadLength,
        PedestrianSpawnMessage => PedestrianStatePayloadLength,
        PedestrianUpdateMessage => PedestrianStatePayloadLength,
        PedestrianRemoveMessage => PedestrianRemovePayloadLength,
        RoadNetworkSnapshotMessage road => GetRoadPayloadLength(road),
        ProtocolErrorMessage error => GetErrorPayloadLength(error),
        _ => throw new ArgumentException($"Unsupported protocol message implementation: {message.GetType().FullName}.", nameof(message)),
    };

    private static int GetRoadPayloadLength(RoadNetworkSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Segments);
        ArgumentNullException.ThrowIfNull(message.Lanes);
        ArgumentNullException.ThrowIfNull(message.Connections);
        ArgumentNullException.ThrowIfNull(message.AccessPoints);
        return checked(RoadHeaderLength + message.Nodes.Count * RoadNodeLength + message.Segments.Count * RoadSegmentLength + message.Lanes.Count * LaneLength + message.Connections.Count * LaneConnectionLength + message.AccessPoints.Count * RoadAccessPointLength);
    }

    private static int GetErrorPayloadLength(ProtocolErrorMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Parameters);
        if (message.Parameters.Count > MaximumErrorParameters) throw new ArgumentException($"Error messages support at most {MaximumErrorParameters} parameters.", nameof(message));
        var length = 4;
        foreach (var parameter in message.Parameters)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            ValidateErrorParameter(parameter);
            length = checked(length + 2 + Utf8.GetByteCount(parameter.Key) + 2 + Utf8.GetByteCount(parameter.Value));
        }
        return length;
    }

    private static void ValidateErrorParameter(ProtocolErrorParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter.Key);
        ArgumentNullException.ThrowIfNull(parameter.Value);
        if (Utf8.GetByteCount(parameter.Key) > MaximumErrorParameterKeyBytes) throw new ArgumentException($"Error parameter keys must be at most {MaximumErrorParameterKeyBytes} UTF-8 bytes.", nameof(parameter));
        if (Utf8.GetByteCount(parameter.Value) > MaximumErrorParameterValueBytes) throw new ArgumentException($"Error parameter values must be at most {MaximumErrorParameterValueBytes} UTF-8 bytes.", nameof(parameter));
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
            case SubscribeVolumeMessage subscribe:
                if (!IsFiniteVolume(subscribe.MinX, subscribe.MinY, subscribe.MinZ, subscribe.MaxX, subscribe.MaxY, subscribe.MaxZ)) throw new ArgumentOutOfRangeException(nameof(message), "Subscribe volume coordinates must be finite and ordered.");
                WriteDouble(payload, subscribe.MinX); WriteDouble(payload[8..], subscribe.MinY); WriteDouble(payload[16..], subscribe.MinZ); WriteDouble(payload[24..], subscribe.MaxX); WriteDouble(payload[32..], subscribe.MaxY); WriteDouble(payload[40..], subscribe.MaxZ);
                return;
            case AgentSpawnMessage agentSpawn:
                WriteAgent(payload, agentSpawn.AgentId, agentSpawn.X, agentSpawn.Y, agentSpawn.Z, agentSpawn.VelocityX, agentSpawn.VelocityY, agentSpawn.VelocityZ, agentSpawn.TickCount);
                return;
            case AgentUpdateMessage agentUpdate:
                WriteAgent(payload, agentUpdate.AgentId, agentUpdate.X, agentUpdate.Y, agentUpdate.Z, agentUpdate.VelocityX, agentUpdate.VelocityY, agentUpdate.VelocityZ, agentUpdate.TickCount);
                return;
            case AgentRemoveMessage agentRemove:
                WriteUInt64(payload, agentRemove.AgentId); WriteUInt64(payload[8..], agentRemove.TickCount);
                return;
            case PedestrianSpawnMessage pedestrianSpawn:
                WritePedestrian(payload, pedestrianSpawn.PedestrianId, pedestrianSpawn.TripRequestId, pedestrianSpawn.X, pedestrianSpawn.Y, pedestrianSpawn.Z, pedestrianSpawn.VelocityX, pedestrianSpawn.VelocityY, pedestrianSpawn.VelocityZ, pedestrianSpawn.WalkingSpeedMetersPerSecond, pedestrianSpawn.State, pedestrianSpawn.TickCount);
                return;
            case PedestrianUpdateMessage pedestrianUpdate:
                WritePedestrian(payload, pedestrianUpdate.PedestrianId, pedestrianUpdate.TripRequestId, pedestrianUpdate.X, pedestrianUpdate.Y, pedestrianUpdate.Z, pedestrianUpdate.VelocityX, pedestrianUpdate.VelocityY, pedestrianUpdate.VelocityZ, pedestrianUpdate.WalkingSpeedMetersPerSecond, pedestrianUpdate.State, pedestrianUpdate.TickCount);
                return;
            case PedestrianRemoveMessage pedestrianRemove:
                ValidateStableId(pedestrianRemove.PedestrianId, nameof(message));
                WriteUInt64(payload, pedestrianRemove.PedestrianId); WriteUInt64(payload[8..], pedestrianRemove.TickCount);
                return;
            case RoadNetworkSnapshotMessage road:
                WriteRoad(payload, road);
                return;
            case ProtocolErrorMessage protocolError:
                WriteError(payload, protocolError);
                return;
            default:
                throw new ArgumentException($"Unsupported protocol message implementation: {message.GetType().FullName}.", nameof(message));
        }
    }

    private static void WriteAgent(Span<byte> payload, ulong id, double x, double y, double z, double velocityX, double velocityY, double velocityZ, ulong tick)
    {
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); ValidateFinite(velocityX, nameof(velocityX)); ValidateFinite(velocityY, nameof(velocityY)); ValidateFinite(velocityZ, nameof(velocityZ));
        WriteUInt64(payload, id); WriteDouble(payload[8..], x); WriteDouble(payload[16..], y); WriteDouble(payload[24..], z); WriteDouble(payload[32..], velocityX); WriteDouble(payload[40..], velocityY); WriteDouble(payload[48..], velocityZ); WriteUInt64(payload[56..], tick);
    }

    private static void WritePedestrian(Span<byte> payload, ulong id, ulong tripRequestId, double x, double y, double z, double velocityX, double velocityY, double velocityZ, double walkingSpeed, ProtocolPedestrianMovementState state, ulong tick)
    {
        ValidateStableId(id, nameof(id)); ValidateStableId(tripRequestId, nameof(tripRequestId));
        ValidateFinite(x, nameof(x)); ValidateFinite(y, nameof(y)); ValidateFinite(z, nameof(z)); ValidateFinite(velocityX, nameof(velocityX)); ValidateFinite(velocityY, nameof(velocityY)); ValidateFinite(velocityZ, nameof(velocityZ));
        if (!double.IsFinite(walkingSpeed) || walkingSpeed <= 0d) throw new ArgumentOutOfRangeException(nameof(walkingSpeed));
        ValidateEnum(state, nameof(state));
        WriteUInt64(payload, id); WriteUInt64(payload[8..], tripRequestId);
        WriteDouble(payload[16..], x); WriteDouble(payload[24..], y); WriteDouble(payload[32..], z);
        WriteDouble(payload[40..], velocityX); WriteDouble(payload[48..], velocityY); WriteDouble(payload[56..], velocityZ);
        WriteDouble(payload[64..], walkingSpeed); payload[72] = (byte)state; WriteUInt64(payload[73..], tick);
    }

    private static void WriteRoad(Span<byte> payload, RoadNetworkSnapshotMessage message)
    {
        WriteUInt64(payload, message.TickCount); WriteUInt32(payload[8..], checked((uint)message.Nodes.Count)); WriteUInt32(payload[12..], checked((uint)message.Segments.Count)); WriteUInt32(payload[16..], checked((uint)message.Lanes.Count)); WriteUInt32(payload[20..], checked((uint)message.Connections.Count)); WriteUInt32(payload[24..], checked((uint)message.AccessPoints.Count));
        var offset = RoadHeaderLength;
        foreach (var node in message.Nodes) { ValidateStableId(node.Id, nameof(message)); ValidateFinite(node.X, nameof(message)); ValidateFinite(node.Y, nameof(message)); ValidateFinite(node.Z, nameof(message)); ValidateEnum(node.Kind, nameof(message)); WriteUInt64(payload[offset..], node.Id); payload[offset + 8] = (byte)node.Kind; WriteDouble(payload[(offset + 9)..], node.X); WriteDouble(payload[(offset + 17)..], node.Y); WriteDouble(payload[(offset + 25)..], node.Z); offset += RoadNodeLength; }
        foreach (var segment in message.Segments) { ValidateStableId(segment.Id, nameof(message)); ValidateStableId(segment.StartNodeId, nameof(message)); ValidateStableId(segment.EndNodeId, nameof(message)); ValidateEnum(segment.Kind, nameof(message)); WriteUInt64(payload[offset..], segment.Id); payload[offset + 8] = (byte)segment.Kind; WriteUInt64(payload[(offset + 9)..], segment.StartNodeId); WriteUInt64(payload[(offset + 17)..], segment.EndNodeId); offset += RoadSegmentLength; }
        foreach (var lane in message.Lanes) { ValidateStableId(lane.Id, nameof(message)); ValidateStableId(lane.SegmentId, nameof(message)); ValidateEnum(lane.Direction, nameof(message)); if (!double.IsFinite(lane.WidthMeters) || lane.WidthMeters <= 0 || !double.IsFinite(lane.SpeedLimitMetersPerSecond) || lane.SpeedLimitMetersPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(message)); WriteUInt64(payload[offset..], lane.Id); WriteUInt64(payload[(offset + 8)..], lane.SegmentId); payload[offset + 16] = (byte)lane.Direction; WriteUInt16(payload[(offset + 17)..], lane.Order); WriteDouble(payload[(offset + 19)..], lane.WidthMeters); WriteDouble(payload[(offset + 27)..], lane.SpeedLimitMetersPerSecond); offset += LaneLength; }
        foreach (var connection in message.Connections) { ValidateStableId(connection.Id, nameof(message)); ValidateStableId(connection.FromLaneId, nameof(message)); ValidateStableId(connection.ToLaneId, nameof(message)); ValidateStableId(connection.ViaNodeId, nameof(message)); ValidateEnum(connection.Movement, nameof(message)); WriteUInt64(payload[offset..], connection.Id); WriteUInt64(payload[(offset + 8)..], connection.FromLaneId); WriteUInt64(payload[(offset + 16)..], connection.ToLaneId); WriteUInt64(payload[(offset + 24)..], connection.ViaNodeId); payload[offset + 32] = (byte)connection.Movement; offset += LaneConnectionLength; }
        foreach (var access in message.AccessPoints) { ValidateStableId(access.Id, nameof(message)); ValidateStableId(access.SegmentId, nameof(message)); if (!double.IsFinite(access.SegmentOffset) || access.SegmentOffset < 0 || access.SegmentOffset > 1 || (access.BuildingId == 0 && access.PoiId == 0) || access.Mode == ProtocolRoadAccessMode.None || (access.Mode & ~(ProtocolRoadAccessMode.Motor | ProtocolRoadAccessMode.Foot)) != 0) throw new ArgumentOutOfRangeException(nameof(message)); WriteUInt64(payload[offset..], access.Id); WriteUInt64(payload[(offset + 8)..], access.SegmentId); WriteDouble(payload[(offset + 16)..], access.SegmentOffset); WriteUInt64(payload[(offset + 24)..], access.BuildingId); WriteUInt64(payload[(offset + 32)..], access.PoiId); payload[offset + 40] = (byte)access.Mode; offset += RoadAccessPointLength; }
    }

    private static void WriteError(Span<byte> payload, ProtocolErrorMessage message)
    {
        WriteUInt16(payload, (ushort)message.Code); WriteUInt16(payload[2..], checked((ushort)message.Parameters.Count)); var offset = 4;
        foreach (var parameter in message.Parameters) { offset += WriteUtf8String(payload[offset..], parameter.Key); offset += WriteUtf8String(payload[offset..], parameter.Value); }
    }

    private static int WriteUtf8String(Span<byte> destination, string value)
    {
        var count = Utf8.GetByteCount(value); WriteUInt16(destination, checked((ushort)count)); Utf8.GetBytes(value, destination[2..]); return count + 2;
    }

    private static bool TryReadMessage(MessageType type, ReadOnlySpan<byte> payload, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        switch (type)
        {
            case MessageType.Hello:
                if (payload.Length != HelloPayloadLength) return InvalidPayload(out message, out error);
                message = new HelloMessage(); break;
            case MessageType.HelloAck:
                if (payload.Length != HelloAckPayloadLength) return InvalidPayload(out message, out error);
                message = new HelloAckMessage(new ProtocolVersion(ReadUInt16(payload), ReadUInt16(payload[2..])), ReadUInt16(payload[4..])); break;
            case MessageType.SubscribeVolume:
            {
                if (payload.Length != SubscribeVolumePayloadLength) return InvalidPayload(out message, out error);
                var minX = ReadDouble(payload); var minY = ReadDouble(payload[8..]); var minZ = ReadDouble(payload[16..]); var maxX = ReadDouble(payload[24..]); var maxY = ReadDouble(payload[32..]); var maxZ = ReadDouble(payload[40..]);
                if (!IsFiniteVolume(minX, minY, minZ, maxX, maxY, maxZ)) return InvalidPayload(out message, out error);
                message = new SubscribeVolumeMessage(minX, minY, minZ, maxX, maxY, maxZ); break;
            }
            case MessageType.AgentSpawn:
                return TryReadAgent(payload, static (id, x, y, z, vx, vy, vz, tick) => new AgentSpawnMessage(id, x, y, z, vx, vy, vz, tick), out message, out error);
            case MessageType.AgentUpdate:
                return TryReadAgent(payload, static (id, x, y, z, vx, vy, vz, tick) => new AgentUpdateMessage(id, x, y, z, vx, vy, vz, tick), out message, out error);
            case MessageType.AgentRemove:
                if (payload.Length != AgentRemovePayloadLength) return InvalidPayload(out message, out error);
                message = new AgentRemoveMessage(ReadUInt64(payload), ReadUInt64(payload[8..])); break;
            case MessageType.PedestrianSpawn:
                return TryReadPedestrian(payload, true, out message, out error);
            case MessageType.PedestrianUpdate:
                return TryReadPedestrian(payload, false, out message, out error);
            case MessageType.PedestrianRemove:
                if (payload.Length != PedestrianRemovePayloadLength || ReadUInt64(payload) == 0) return InvalidPayload(out message, out error);
                message = new PedestrianRemoveMessage(ReadUInt64(payload), ReadUInt64(payload[8..])); break;
            case MessageType.RoadNetworkSnapshot:
                return TryReadRoad(payload, out message, out error);
            case MessageType.Error:
                return TryReadError(payload, out message, out error);
            default:
                message = null!; error = ProtocolDecodeError.UnknownMessageType; return false;
        }
        error = ProtocolDecodeError.None;
        return true;
    }

    private static bool TryReadAgent(ReadOnlySpan<byte> payload, Func<ulong, double, double, double, double, double, double, ulong, IProtocolMessage> factory, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (payload.Length != AgentStatePayloadLength) return InvalidPayload(out message, out error);
        var id = ReadUInt64(payload); var x = ReadDouble(payload[8..]); var y = ReadDouble(payload[16..]); var z = ReadDouble(payload[24..]); var velocityX = ReadDouble(payload[32..]); var velocityY = ReadDouble(payload[40..]); var velocityZ = ReadDouble(payload[48..]); var tick = ReadUInt64(payload[56..]);
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(velocityX) || !double.IsFinite(velocityY) || !double.IsFinite(velocityZ)) return InvalidPayload(out message, out error);
        message = factory(id, x, y, z, velocityX, velocityY, velocityZ, tick); error = ProtocolDecodeError.None; return true;
    }

    private static bool TryReadPedestrian(ReadOnlySpan<byte> payload, bool spawn, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (payload.Length != PedestrianStatePayloadLength) return InvalidPayload(out message, out error);
        var id = ReadUInt64(payload); var tripRequestId = ReadUInt64(payload[8..]);
        var x = ReadDouble(payload[16..]); var y = ReadDouble(payload[24..]); var z = ReadDouble(payload[32..]);
        var velocityX = ReadDouble(payload[40..]); var velocityY = ReadDouble(payload[48..]); var velocityZ = ReadDouble(payload[56..]);
        var walkingSpeed = ReadDouble(payload[64..]); var state = (ProtocolPedestrianMovementState)payload[72]; var tick = ReadUInt64(payload[73..]);
        if (id == 0 || tripRequestId == 0 || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z) || !double.IsFinite(velocityX) || !double.IsFinite(velocityY) || !double.IsFinite(velocityZ) || !double.IsFinite(walkingSpeed) || walkingSpeed <= 0d || !Enum.IsDefined(state)) return InvalidPayload(out message, out error);
        message = spawn
            ? new PedestrianSpawnMessage(id, tripRequestId, x, y, z, velocityX, velocityY, velocityZ, walkingSpeed, state, tick)
            : new PedestrianUpdateMessage(id, tripRequestId, x, y, z, velocityX, velocityY, velocityZ, walkingSpeed, state, tick);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static bool TryReadRoad(ReadOnlySpan<byte> payload, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (payload.Length < RoadHeaderLength) return InvalidPayload(out message, out error);
        var tick = ReadUInt64(payload); var nodeCount = ReadUInt32(payload[8..]); var segmentCount = ReadUInt32(payload[12..]); var laneCount = ReadUInt32(payload[16..]); var connectionCount = ReadUInt32(payload[20..]); var accessCount = ReadUInt32(payload[24..]);
        long expected = RoadHeaderLength + (long)nodeCount * RoadNodeLength + (long)segmentCount * RoadSegmentLength + (long)laneCount * LaneLength + (long)connectionCount * LaneConnectionLength + (long)accessCount * RoadAccessPointLength;
        if (expected != payload.Length || nodeCount > int.MaxValue || segmentCount > int.MaxValue || laneCount > int.MaxValue || connectionCount > int.MaxValue || accessCount > int.MaxValue) return InvalidPayload(out message, out error);
        var nodes = new ProtocolRoadNode[(int)nodeCount]; var segments = new ProtocolRoadSegment[(int)segmentCount]; var lanes = new ProtocolLane[(int)laneCount]; var connections = new ProtocolLaneConnection[(int)connectionCount]; var accessPoints = new ProtocolRoadAccessPoint[(int)accessCount]; var offset = RoadHeaderLength;
        for (var index = 0; index < nodes.Length; index++) { var id = ReadUInt64(payload[offset..]); var kind = (ProtocolRoadNodeKind)payload[offset + 8]; var x = ReadDouble(payload[(offset + 9)..]); var y = ReadDouble(payload[(offset + 17)..]); var z = ReadDouble(payload[(offset + 25)..]); if (id == 0 || !Enum.IsDefined(kind) || !double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z)) return InvalidPayload(out message, out error); nodes[index] = new(id, kind, x, y, z); offset += RoadNodeLength; }
        for (var index = 0; index < segments.Length; index++) { var id = ReadUInt64(payload[offset..]); var kind = (ProtocolRoadKind)payload[offset + 8]; var start = ReadUInt64(payload[(offset + 9)..]); var end = ReadUInt64(payload[(offset + 17)..]); if (id == 0 || start == 0 || end == 0 || start == end || !Enum.IsDefined(kind)) return InvalidPayload(out message, out error); segments[index] = new(id, kind, start, end); offset += RoadSegmentLength; }
        for (var index = 0; index < lanes.Length; index++) { var id = ReadUInt64(payload[offset..]); var segment = ReadUInt64(payload[(offset + 8)..]); var direction = (ProtocolLaneDirection)payload[offset + 16]; var order = ReadUInt16(payload[(offset + 17)..]); var width = ReadDouble(payload[(offset + 19)..]); var speed = ReadDouble(payload[(offset + 27)..]); if (id == 0 || segment == 0 || !Enum.IsDefined(direction) || !double.IsFinite(width) || width <= 0 || !double.IsFinite(speed) || speed <= 0) return InvalidPayload(out message, out error); lanes[index] = new(id, segment, direction, order, width, speed); offset += LaneLength; }
        for (var index = 0; index < connections.Length; index++) { var id = ReadUInt64(payload[offset..]); var from = ReadUInt64(payload[(offset + 8)..]); var to = ReadUInt64(payload[(offset + 16)..]); var via = ReadUInt64(payload[(offset + 24)..]); var movement = (ProtocolTurnMovement)payload[offset + 32]; if (id == 0 || from == 0 || to == 0 || via == 0 || from == to || !Enum.IsDefined(movement)) return InvalidPayload(out message, out error); connections[index] = new(id, from, to, via, movement); offset += LaneConnectionLength; }
        for (var index = 0; index < accessPoints.Length; index++) { var id = ReadUInt64(payload[offset..]); var segment = ReadUInt64(payload[(offset + 8)..]); var segmentOffset = ReadDouble(payload[(offset + 16)..]); var building = ReadUInt64(payload[(offset + 24)..]); var poi = ReadUInt64(payload[(offset + 32)..]); var mode = (ProtocolRoadAccessMode)payload[offset + 40]; if (id == 0 || segment == 0 || !double.IsFinite(segmentOffset) || segmentOffset < 0 || segmentOffset > 1 || (building == 0 && poi == 0) || mode == ProtocolRoadAccessMode.None || (mode & ~(ProtocolRoadAccessMode.Motor | ProtocolRoadAccessMode.Foot)) != 0) return InvalidPayload(out message, out error); accessPoints[index] = new(id, segment, segmentOffset, building, poi, mode); offset += RoadAccessPointLength; }
        message = new RoadNetworkSnapshotMessage(tick, nodes, segments, lanes, connections, accessPoints); error = ProtocolDecodeError.None; return true;
    }

    private static bool TryReadError(ReadOnlySpan<byte> payload, out IProtocolMessage message, out ProtocolDecodeError error)
    {
        if (payload.Length < 4) return InvalidPayload(out message, out error);
        var code = (ProtocolErrorCode)ReadUInt16(payload); var count = ReadUInt16(payload[2..]);
        if (count > MaximumErrorParameters) return InvalidPayload(out message, out error);
        var parameters = new List<ProtocolErrorParameter>(count); var offset = 4;
        try
        {
            for (var index = 0; index < count; index++)
            {
                if (!TryReadUtf8String(payload, ref offset, MaximumErrorParameterKeyBytes, out var key) || !TryReadUtf8String(payload, ref offset, MaximumErrorParameterValueBytes, out var value)) return InvalidPayload(out message, out error);
                parameters.Add(new ProtocolErrorParameter(key, value));
            }
        }
        catch (DecoderFallbackException)
        {
            return InvalidPayload(out message, out error);
        }
        if (offset != payload.Length) return InvalidPayload(out message, out error);
        message = new ProtocolErrorMessage(code, parameters); error = ProtocolDecodeError.None; return true;
    }

    private static bool TryReadUtf8String(ReadOnlySpan<byte> payload, ref int offset, int maximum, out string value)
    {
        value = string.Empty;
        if (offset > payload.Length - 2) return false;
        var length = ReadUInt16(payload[offset..]); offset += 2;
        if (length > maximum || offset > payload.Length - length) return false;
        value = Utf8.GetString(payload.Slice(offset, length)); offset += length; return true;
    }

    private static bool InvalidPayload(out IProtocolMessage message, out ProtocolDecodeError error)
    {
        message = null!; error = ProtocolDecodeError.InvalidPayload; return false;
    }

    private static bool IsFiniteVolume(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) => double.IsFinite(minX) && double.IsFinite(minY) && double.IsFinite(minZ) && double.IsFinite(maxX) && double.IsFinite(maxY) && double.IsFinite(maxZ) && maxX >= minX && maxY >= minY && maxZ >= minZ;
    private static void ValidateFinite(double value, string name) { if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Protocol coordinates and velocities must be finite."); }
    private static void ValidateStableId(ulong value, string name) { if (value == 0) throw new ArgumentOutOfRangeException(name, "Protocol stable IDs must be greater than zero."); }
    private static void ValidateEnum<T>(T value, string name) where T : struct, Enum { if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(name, value, $"{typeof(T).Name} is invalid."); }
    private static void WriteUInt16(Span<byte> destination, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
    private static ushort ReadUInt16(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    private static void WriteUInt32(Span<byte> destination, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    private static uint ReadUInt32(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt32LittleEndian(source);
    private static void WriteUInt64(Span<byte> destination, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
    private static ulong ReadUInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt64LittleEndian(source);
    private static void WriteDouble(Span<byte> destination, double value) { ValidateFinite(value, nameof(value)); BinaryPrimitives.WriteInt64LittleEndian(destination, BitConverter.DoubleToInt64Bits(value)); }
    private static double ReadDouble(ReadOnlySpan<byte> source) => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(source));
}
