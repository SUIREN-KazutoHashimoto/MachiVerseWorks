using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class RailwayInfrastructureProtocolCodec
{
    private const int SnapshotHeaderLength = 41;
    private const int NodeLength = 33;
    private const int SegmentLength = 43;
    private const int ConnectionLength = 32;
    private const int StationLength = 56;
    private const int PlatformLength = 88;
    private const int AccessPointLength = 24;
    private const int BlockHeaderLength = 12;
    private const int DepotHeaderLength = 60;

    public static byte[] Serialize(RailwayInfrastructureSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRailwayInfrastructure)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Railway infrastructure snapshots require Protocol 2.6 or newer.");
        ValidateMessage(message);
        RailwayInfrastructureProtocolValidator.ValidateIdentity(message);

        var payloadLength = checked(
            SnapshotHeaderLength
            + checked(message.Nodes.Count * NodeLength)
            + checked(message.Segments.Count * SegmentLength)
            + checked(message.Connections.Count * ConnectionLength)
            + message.Blocks.Sum(static item => checked(BlockHeaderLength + checked(item.SegmentIds.Count * sizeof(ulong))))
            + checked(message.Stations.Count * StationLength)
            + checked(message.Platforms.Count * PlatformLength)
            + checked(message.PlatformAccessPoints.Count * AccessPointLength)
            + message.Depots.Sum(static item => checked(DepotHeaderLength + checked(item.TrackSegmentIds.Count * sizeof(ulong)))));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(message), "Railway infrastructure snapshot exceeds the maximum protocol payload size.");

        var frame = new byte[checked(ProtocolFrameHeader.Size + payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.RailwayInfrastructureSnapshot, checked((uint)payloadLength)));
        var writer = new SpanWriter(frame.AsSpan(ProtocolFrameHeader.Size));
        writer.WriteUInt64(message.Revision);
        writer.WriteByte(message.IsFullSnapshot ? (byte)1 : (byte)0);
        writer.WriteUInt32(checked((uint)message.Nodes.Count));
        writer.WriteUInt32(checked((uint)message.Segments.Count));
        writer.WriteUInt32(checked((uint)message.Connections.Count));
        writer.WriteUInt32(checked((uint)message.Blocks.Count));
        writer.WriteUInt32(checked((uint)message.Stations.Count));
        writer.WriteUInt32(checked((uint)message.Platforms.Count));
        writer.WriteUInt32(checked((uint)message.PlatformAccessPoints.Count));
        writer.WriteUInt32(checked((uint)message.Depots.Count));

        foreach (var item in message.Nodes)
        {
            writer.WriteUInt64(item.Id); writer.WriteByte(item.Kind); writer.WriteDouble(item.X); writer.WriteDouble(item.Y); writer.WriteDouble(item.Z);
        }
        foreach (var item in message.Segments)
        {
            writer.WriteUInt64(item.Id); writer.WriteUInt64(item.StartNodeId); writer.WriteUInt64(item.EndNodeId); writer.WriteByte((byte)item.Direction);
            writer.WriteDouble(item.GaugeMeters); writer.WriteDouble(item.SpeedLimitMetersPerSecond); writer.WriteByte((byte)item.Electrification); writer.WriteByte((byte)item.Usage);
        }
        foreach (var item in message.Connections)
        {
            writer.WriteUInt64(item.Id); writer.WriteUInt64(item.FromSegmentId); writer.WriteUInt64(item.ToSegmentId); writer.WriteUInt64(item.ViaNodeId);
        }
        foreach (var item in message.Blocks)
        {
            writer.WriteUInt64(item.Id); writer.WriteUInt32(checked((uint)item.SegmentIds.Count));
            foreach (var segmentId in item.SegmentIds) writer.WriteUInt64(segmentId);
        }
        foreach (var item in message.Stations)
        {
            writer.WriteUInt64(item.Id); WriteBounds(ref writer, item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ);
        }
        foreach (var item in message.Platforms)
        {
            writer.WriteUInt64(item.Id); writer.WriteUInt64(item.StationId); writer.WriteUInt64(item.TrackSegmentId); writer.WriteDouble(item.StartSegmentOffset); writer.WriteDouble(item.EndSegmentOffset);
            WriteBounds(ref writer, item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ);
        }
        foreach (var item in message.PlatformAccessPoints)
        {
            writer.WriteUInt64(item.Id); writer.WriteUInt64(item.PlatformId); writer.WriteUInt64(item.RoadAccessPointId);
        }
        foreach (var item in message.Depots)
        {
            writer.WriteUInt64(item.Id); WriteBounds(ref writer, item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ);
            writer.WriteUInt32(checked((uint)item.TrackSegmentIds.Count));
            foreach (var segmentId in item.TrackSegmentIds) writer.WriteUInt64(segmentId);
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out RailwayInfrastructureSnapshotMessage message, out ProtocolDecodeError error)
    {
        message = null!;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.RailwayInfrastructureSnapshot) { error = ProtocolDecodeError.UnknownMessageType; return false; }
        if (!header.Version.SupportsRailwayInfrastructure || header.PayloadLength < SnapshotHeaderLength) { error = ProtocolDecodeError.InvalidPayload; return false; }
        try
        {
            var reader = new SpanReader(frame[ProtocolFrameHeader.Size..]);
            var revision = reader.ReadUInt64();
            var full = reader.ReadByte();
            if (full > 1) throw new InvalidDataException();
            var nodeCount = reader.ReadCount(NodeLength);
            var segmentCount = reader.ReadCount(SegmentLength);
            var connectionCount = reader.ReadCount(ConnectionLength);
            var blockCount = reader.ReadCount(BlockHeaderLength);
            var stationCount = reader.ReadCount(StationLength);
            var platformCount = reader.ReadCount(PlatformLength);
            var accessCount = reader.ReadCount(AccessPointLength);
            var depotCount = reader.ReadCount(DepotHeaderLength);

            var nodes = new ProtocolTrackNode[nodeCount];
            for (var i = 0; i < nodes.Length; i++)
            {
                var item = new ProtocolTrackNode(reader.ReadUInt64(), reader.ReadByte(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                if (item.Id == 0 || item.Kind > 2 || !Finite(item.X, item.Y, item.Z)) throw new InvalidDataException();
                nodes[i] = item;
            }
            var segments = new ProtocolTrackSegment[segmentCount];
            for (var i = 0; i < segments.Length; i++)
            {
                var item = new ProtocolTrackSegment(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), (ProtocolTrackDirection)reader.ReadByte(), reader.ReadDouble(), reader.ReadDouble(), (ProtocolTrackElectrification)reader.ReadByte(), (ProtocolTrackUsage)reader.ReadByte());
                if (item.Id == 0 || item.StartNodeId == 0 || item.EndNodeId == 0 || item.StartNodeId == item.EndNodeId || !Enum.IsDefined(item.Direction) || !double.IsFinite(item.GaugeMeters) || item.GaugeMeters <= 0 || !double.IsFinite(item.SpeedLimitMetersPerSecond) || item.SpeedLimitMetersPerSecond <= 0 || !Enum.IsDefined(item.Electrification) || !Enum.IsDefined(item.Usage)) throw new InvalidDataException();
                segments[i] = item;
            }
            var connections = new ProtocolTrackConnection[connectionCount];
            for (var i = 0; i < connections.Length; i++)
            {
                var item = new ProtocolTrackConnection(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                if (item.Id == 0 || item.FromSegmentId == 0 || item.ToSegmentId == 0 || item.ViaNodeId == 0 || item.FromSegmentId == item.ToSegmentId) throw new InvalidDataException();
                connections[i] = item;
            }
            var blocks = new ProtocolBlockSection[blockCount];
            for (var i = 0; i < blocks.Length; i++)
            {
                var id = reader.ReadUInt64(); var count = reader.ReadCount(sizeof(ulong));
                if (id == 0 || count == 0) throw new InvalidDataException();
                var ids = new ulong[count];
                for (var j = 0; j < ids.Length; j++) { ids[j] = reader.ReadUInt64(); if (ids[j] == 0) throw new InvalidDataException(); }
                blocks[i] = new ProtocolBlockSection(id, ids);
            }
            var stations = new ProtocolStation[stationCount];
            for (var i = 0; i < stations.Length; i++)
            {
                var item = new ProtocolStation(reader.ReadUInt64(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                if (item.Id == 0 || !ValidBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ)) throw new InvalidDataException();
                stations[i] = item;
            }
            var platforms = new ProtocolPlatform[platformCount];
            for (var i = 0; i < platforms.Length; i++)
            {
                var item = new ProtocolPlatform(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble());
                if (item.Id == 0 || item.StationId == 0 || item.TrackSegmentId == 0 || !double.IsFinite(item.StartSegmentOffset) || !double.IsFinite(item.EndSegmentOffset) || item.StartSegmentOffset < 0 || item.EndSegmentOffset > 1 || item.EndSegmentOffset <= item.StartSegmentOffset || !ValidBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ)) throw new InvalidDataException();
                platforms[i] = item;
            }
            var accessPoints = new ProtocolPlatformAccessPoint[accessCount];
            for (var i = 0; i < accessPoints.Length; i++)
            {
                var item = new ProtocolPlatformAccessPoint(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                if (item.Id == 0 || item.PlatformId == 0 || item.RoadAccessPointId == 0) throw new InvalidDataException();
                accessPoints[i] = item;
            }
            var depots = new ProtocolDepot[depotCount];
            for (var i = 0; i < depots.Length; i++)
            {
                var id = reader.ReadUInt64();
                var minX = reader.ReadDouble(); var minY = reader.ReadDouble(); var minZ = reader.ReadDouble(); var maxX = reader.ReadDouble(); var maxY = reader.ReadDouble(); var maxZ = reader.ReadDouble();
                var count = reader.ReadCount(sizeof(ulong));
                if (id == 0 || count == 0 || !ValidBounds(minX, minY, minZ, maxX, maxY, maxZ)) throw new InvalidDataException();
                var ids = new ulong[count];
                for (var j = 0; j < ids.Length; j++) { ids[j] = reader.ReadUInt64(); if (ids[j] == 0) throw new InvalidDataException(); }
                depots[i] = new ProtocolDepot(id, minX, minY, minZ, maxX, maxY, maxZ, ids);
            }
            if (!reader.IsComplete) throw new InvalidDataException();
            message = new RailwayInfrastructureSnapshotMessage(revision, full != 0, nodes, segments, connections, blocks, stations, platforms, accessPoints, depots);
            RailwayInfrastructureProtocolValidator.ValidateIdentity(message);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException or ArgumentException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static void ValidateMessage(RailwayInfrastructureSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Nodes); ArgumentNullException.ThrowIfNull(message.Segments); ArgumentNullException.ThrowIfNull(message.Connections); ArgumentNullException.ThrowIfNull(message.Blocks);
        ArgumentNullException.ThrowIfNull(message.Stations); ArgumentNullException.ThrowIfNull(message.Platforms); ArgumentNullException.ThrowIfNull(message.PlatformAccessPoints); ArgumentNullException.ThrowIfNull(message.Depots);
        foreach (var item in message.Nodes) if (item.Id == 0 || item.Kind > 2 || !Finite(item.X, item.Y, item.Z)) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.Segments) if (item.Id == 0 || item.StartNodeId == 0 || item.EndNodeId == 0 || item.StartNodeId == item.EndNodeId || !Enum.IsDefined(item.Direction) || !double.IsFinite(item.GaugeMeters) || item.GaugeMeters <= 0 || !double.IsFinite(item.SpeedLimitMetersPerSecond) || item.SpeedLimitMetersPerSecond <= 0 || !Enum.IsDefined(item.Electrification) || !Enum.IsDefined(item.Usage)) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.Connections) if (item.Id == 0 || item.FromSegmentId == 0 || item.ToSegmentId == 0 || item.ViaNodeId == 0 || item.FromSegmentId == item.ToSegmentId) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.Blocks) { ArgumentNullException.ThrowIfNull(item); ArgumentNullException.ThrowIfNull(item.SegmentIds); if (item.Id == 0 || item.SegmentIds.Count == 0 || item.SegmentIds.Any(static id => id == 0)) throw new ArgumentOutOfRangeException(nameof(message)); }
        foreach (var item in message.Stations) if (item.Id == 0 || !ValidBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ)) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.Platforms) if (item.Id == 0 || item.StationId == 0 || item.TrackSegmentId == 0 || !double.IsFinite(item.StartSegmentOffset) || !double.IsFinite(item.EndSegmentOffset) || item.StartSegmentOffset < 0 || item.EndSegmentOffset > 1 || item.EndSegmentOffset <= item.StartSegmentOffset || !ValidBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ)) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.PlatformAccessPoints) if (item.Id == 0 || item.PlatformId == 0 || item.RoadAccessPointId == 0) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.Depots) { ArgumentNullException.ThrowIfNull(item); ArgumentNullException.ThrowIfNull(item.TrackSegmentIds); if (item.Id == 0 || item.TrackSegmentIds.Count == 0 || item.TrackSegmentIds.Any(static id => id == 0) || !ValidBounds(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ)) throw new ArgumentOutOfRangeException(nameof(message)); }
    }

    private static void WriteBounds(ref SpanWriter writer, double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
    { writer.WriteDouble(minX); writer.WriteDouble(minY); writer.WriteDouble(minZ); writer.WriteDouble(maxX); writer.WriteDouble(maxY); writer.WriteDouble(maxZ); }
    private static bool Finite(double x, double y, double z) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);
    private static bool ValidBounds(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) => Finite(minX, minY, minZ) && Finite(maxX, maxY, maxZ) && minX <= maxX && minY <= maxY && minZ <= maxZ;

    private ref struct SpanWriter
    {
        private Span<byte> buffer; private int offset;
        public SpanWriter(Span<byte> buffer) { this.buffer = buffer; offset = 0; }
        public void WriteByte(byte value) => buffer[offset++] = value;
        public void WriteUInt32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..], value); offset += sizeof(uint); }
        public void WriteUInt64(ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(buffer[offset..], value); offset += sizeof(ulong); }
        public void WriteDouble(double value) { BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], BitConverter.DoubleToInt64Bits(value)); offset += sizeof(double); }
    }

    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> buffer; private int offset;
        public SpanReader(ReadOnlySpan<byte> buffer) { this.buffer = buffer; offset = 0; }
        public bool IsComplete => offset == buffer.Length;
        public byte ReadByte() { Ensure(1); return buffer[offset++]; }
        public uint ReadUInt32() { Ensure(sizeof(uint)); var value = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..]); offset += sizeof(uint); return value; }
        public ulong ReadUInt64() { Ensure(sizeof(ulong)); var value = BinaryPrimitives.ReadUInt64LittleEndian(buffer[offset..]); offset += sizeof(ulong); return value; }
        public double ReadDouble() { Ensure(sizeof(double)); var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..])); offset += sizeof(double); return value; }
        public int ReadCount(int minimumElementLength)
        {
            var count = ReadUInt32();
            if (count > int.MaxValue || (ulong)count * (ulong)minimumElementLength > (ulong)(buffer.Length - offset)) throw new InvalidDataException();
            return (int)count;
        }
        private void Ensure(int length) { if (length < 0 || offset > buffer.Length - length) throw new InvalidDataException(); }
    }
}
