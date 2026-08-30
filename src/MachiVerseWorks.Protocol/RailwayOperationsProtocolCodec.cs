using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class RailwayOperationsProtocolCodec
{
    private const int SnapshotHeaderLength = 20;
    private const int TrainLength = 129;
    private const int ServiceLength = 77;
    private const int TimetableHeaderLength = 12;
    private const int TimetableStopLength = 40;

    public static byte[] Serialize(RailwayOperationsSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRailwayOperations) throw new ArgumentOutOfRangeException(nameof(version), version, "Railway operations snapshots require Protocol 2.7 or newer.");
        Validate(message);
        var payloadLength = checked(
            SnapshotHeaderLength
            + checked(message.Trains.Count * TrainLength)
            + checked(message.Services.Count * ServiceLength)
            + message.Timetables.Sum(static item => checked(TimetableHeaderLength + checked(item.Stops.Count * TimetableStopLength))));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Railway operations snapshot exceeds the maximum protocol payload size.");
        var frame = new byte[checked(ProtocolFrameHeader.Size + payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.RailwayOperationsSnapshot, checked((uint)payloadLength)));
        var writer = new SpanWriter(frame.AsSpan(ProtocolFrameHeader.Size));
        writer.WriteUInt64(message.TickCount);
        writer.WriteUInt32(checked((uint)message.Trains.Count));
        writer.WriteUInt32(checked((uint)message.Services.Count));
        writer.WriteUInt32(checked((uint)message.Timetables.Count));
        foreach (var train in message.Trains)
        {
            writer.WriteUInt64(train.Id); writer.WriteUInt64(train.FormationId); writer.WriteUInt64(train.ServiceId); writer.WriteUInt64(train.RouteId);
            writer.WriteDouble(train.X); writer.WriteDouble(train.Y); writer.WriteDouble(train.Z);
            writer.WriteDouble(train.ForwardX); writer.WriteDouble(train.ForwardY); writer.WriteDouble(train.ForwardZ);
            writer.WriteDouble(train.SpeedMetersPerSecond); writer.WriteByte(train.State);
            writer.WriteUInt64(train.CurrentBlockId); writer.WriteUInt64(train.CurrentPlatformId); writer.WriteUInt64(train.AssignedPlatformId); writer.WriteUInt64(train.CurrentDepotId); writer.WriteUInt64(train.DwellDepartureTick);
        }
        foreach (var service in message.Services)
        {
            writer.WriteUInt64(service.Id); writer.WriteUInt64(service.FormationId); writer.WriteUInt64(service.RouteId); writer.WriteUInt64(service.TimetableId);
            writer.WriteUInt64(service.OriginDepotId); writer.WriteUInt64(service.DestinationDepotId); writer.WriteUInt64(service.PlannedStartTick); writer.WriteByte(service.State);
            writer.WriteUInt64(service.DelayTicks); writer.WriteInt32(service.NextStopIndex); writer.WriteUInt64(service.TrainId);
        }
        foreach (var timetable in message.Timetables)
        {
            writer.WriteUInt64(timetable.Id); writer.WriteUInt32(checked((uint)timetable.Stops.Count));
            foreach (var stop in timetable.Stops)
            {
                writer.WriteUInt64(stop.StationId); writer.WriteUInt64(stop.PlannedArrivalTick); writer.WriteUInt64(stop.PlannedDepartureTick); writer.WriteUInt64(stop.MinimumDwellTicks); writer.WriteUInt64(stop.PreferredPlatformId);
            }
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out RailwayOperationsSnapshotMessage message, out ProtocolDecodeError error)
    {
        message = null!;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.RailwayOperationsSnapshot) { error = ProtocolDecodeError.UnknownMessageType; return false; }
        if (!header.Version.SupportsRailwayOperations || header.PayloadLength < SnapshotHeaderLength) { error = ProtocolDecodeError.InvalidPayload; return false; }
        try
        {
            var reader = new SpanReader(frame[ProtocolFrameHeader.Size..]);
            var tickCount = reader.ReadUInt64();
            var trainCount = reader.ReadCount(TrainLength);
            var serviceCount = reader.ReadCount(ServiceLength);
            var timetableCount = reader.ReadCount(TimetableHeaderLength);
            var trains = new ProtocolTrainState[trainCount];
            for (var index = 0; index < trains.Length; index++)
            {
                var item = new ProtocolTrainState(
                    reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(),
                    reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadByte(),
                    reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                ValidateTrain(item);
                trains[index] = item;
            }
            var services = new ProtocolRailwayServiceState[serviceCount];
            for (var index = 0; index < services.Length; index++)
            {
                var item = new ProtocolRailwayServiceState(
                    reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadByte(), reader.ReadUInt64(), reader.ReadInt32(), reader.ReadUInt64());
                ValidateService(item);
                services[index] = item;
            }
            var timetables = new ProtocolTimetable[timetableCount];
            for (var index = 0; index < timetables.Length; index++)
            {
                var id = reader.ReadUInt64();
                var stopCount = reader.ReadCount(TimetableStopLength);
                if (id == 0 || stopCount == 0) throw new InvalidDataException();
                var stops = new ProtocolTimetableStop[stopCount];
                ulong previousDeparture = 0;
                for (var stopIndex = 0; stopIndex < stops.Length; stopIndex++)
                {
                    var stop = new ProtocolTimetableStop(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                    if (stop.StationId == 0 || stop.PlannedDepartureTick < stop.PlannedArrivalTick || (stopIndex > 0 && stop.PlannedArrivalTick < previousDeparture)) throw new InvalidDataException();
                    previousDeparture = stop.PlannedDepartureTick;
                    stops[stopIndex] = stop;
                }
                timetables[index] = new ProtocolTimetable(id, stops);
            }
            if (!reader.IsComplete) throw new InvalidDataException();
            message = new RailwayOperationsSnapshotMessage(tickCount, trains, services, timetables);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException or ArgumentOutOfRangeException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static void Validate(RailwayOperationsSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Trains); ArgumentNullException.ThrowIfNull(message.Services); ArgumentNullException.ThrowIfNull(message.Timetables);
        foreach (var train in message.Trains) ValidateTrain(train);
        foreach (var service in message.Services) ValidateService(service);
        foreach (var timetable in message.Timetables)
        {
            ArgumentNullException.ThrowIfNull(timetable); ArgumentNullException.ThrowIfNull(timetable.Stops);
            if (timetable.Id == 0 || timetable.Stops.Count == 0) throw new ArgumentOutOfRangeException(nameof(message));
            ulong previousDeparture = 0;
            for (var index = 0; index < timetable.Stops.Count; index++)
            {
                var stop = timetable.Stops[index];
                if (stop.StationId == 0 || stop.PlannedDepartureTick < stop.PlannedArrivalTick || (index > 0 && stop.PlannedArrivalTick < previousDeparture)) throw new ArgumentOutOfRangeException(nameof(message));
                previousDeparture = stop.PlannedDepartureTick;
            }
        }
    }

    private static void ValidateTrain(ProtocolTrainState item)
    {
        if (item.Id == 0 || item.FormationId == 0 || item.ServiceId == 0 || item.RouteId == 0 || item.State > 5 || !Finite(item.X, item.Y, item.Z) || !Finite(item.ForwardX, item.ForwardY, item.ForwardZ) || !double.IsFinite(item.SpeedMetersPerSecond) || item.SpeedMetersPerSecond < 0d) throw new ArgumentOutOfRangeException(nameof(item));
    }

    private static void ValidateService(ProtocolRailwayServiceState item)
    {
        if (item.Id == 0 || item.FormationId == 0 || item.RouteId == 0 || item.TimetableId == 0 || item.OriginDepotId == 0 || item.DestinationDepotId == 0 || item.State > 2 || item.NextStopIndex < 0) throw new ArgumentOutOfRangeException(nameof(item));
    }

    private static bool Finite(double x, double y, double z) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);

    private ref struct SpanWriter
    {
        private Span<byte> buffer; private int offset;
        public SpanWriter(Span<byte> buffer) { this.buffer = buffer; offset = 0; }
        public void WriteByte(byte value) => buffer[offset++] = value;
        public void WriteUInt32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..], value); offset += sizeof(uint); }
        public void WriteInt32(int value) { BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], value); offset += sizeof(int); }
        public void WriteUInt64(ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(buffer[offset..], value); offset += sizeof(ulong); }
        public void WriteDouble(double value) { BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], BitConverter.DoubleToInt64Bits(value)); offset += sizeof(double); }
    }

    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> buffer; private int offset;
        public SpanReader(ReadOnlySpan<byte> buffer) { this.buffer = buffer; offset = 0; }
        public bool IsComplete => offset == buffer.Length;
        private int Remaining => buffer.Length - offset;
        public byte ReadByte() { Ensure(1); return buffer[offset++]; }
        public uint ReadUInt32() { Ensure(sizeof(uint)); var value = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..]); offset += sizeof(uint); return value; }
        public int ReadInt32() { Ensure(sizeof(int)); var value = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]); offset += sizeof(int); return value; }
        public ulong ReadUInt64() { Ensure(sizeof(ulong)); var value = BinaryPrimitives.ReadUInt64LittleEndian(buffer[offset..]); offset += sizeof(ulong); return value; }
        public double ReadDouble() { Ensure(sizeof(double)); var value = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..])); offset += sizeof(double); return value; }
        public int ReadCount(int minimumBytesPerItem)
        {
            var count = ReadUInt32();
            if (count > int.MaxValue) throw new InvalidDataException();
            var value = (int)count;
            if (minimumBytesPerItem > 0 && value > Remaining / minimumBytesPerItem) throw new InvalidDataException();
            return value;
        }
        private void Ensure(int length) { if (length < 0 || Remaining < length) throw new InvalidDataException(); }
    }
}
