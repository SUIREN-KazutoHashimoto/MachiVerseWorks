using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class MultimodalTransitProtocolCodec
{
    private const int HeaderLength = 28;
    private const int LineLength = 9;
    private const int StopLength = 57;
    private const int PatternHeaderLength = 28;
    private const int PatternStopLength = 24;
    private const int VehicleLength = 70;
    private const int ArrivalLength = 32;

    public static int GetPayloadLength(MultimodalTransitSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Lines);
        ArgumentNullException.ThrowIfNull(message.Stops);
        ArgumentNullException.ThrowIfNull(message.Patterns);
        ArgumentNullException.ThrowIfNull(message.Vehicles);
        ArgumentNullException.ThrowIfNull(message.ArrivalEstimates);
        var patternBytes = 0;
        foreach (var pattern in message.Patterns)
        {
            ArgumentNullException.ThrowIfNull(pattern);
            ArgumentNullException.ThrowIfNull(pattern.Stops);
            patternBytes = checked(patternBytes + PatternHeaderLength + pattern.Stops.Count * PatternStopLength);
        }
        return checked(HeaderLength + message.Lines.Count * LineLength + message.Stops.Count * StopLength + patternBytes + message.Vehicles.Count * VehicleLength + message.ArrivalEstimates.Count * ArrivalLength);
    }

    public static byte[] Serialize(MultimodalTransitSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsMultimodalTransit) throw new ArgumentOutOfRangeException(nameof(version), version, "Multimodal Transit messages require Protocol 2.8 or newer.");
        Validate(message);
        var payloadLength = GetPayloadLength(message);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentException("Multimodal Transit payload exceeds the protocol payload limit.", nameof(message));
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.MultimodalTransitSnapshot, checked((uint)payloadLength)));
        var writer = new SpanWriter(frame.AsSpan(ProtocolFrameHeader.Size));
        writer.WriteUInt64(message.TickCount); writer.WriteUInt32(checked((uint)message.Lines.Count)); writer.WriteUInt32(checked((uint)message.Stops.Count)); writer.WriteUInt32(checked((uint)message.Patterns.Count)); writer.WriteUInt32(checked((uint)message.Vehicles.Count)); writer.WriteUInt32(checked((uint)message.ArrivalEstimates.Count));
        foreach (var line in message.Lines) { writer.WriteUInt64(line.Id); writer.WriteByte((byte)line.Mode); }
        foreach (var stop in message.Stops) { writer.WriteUInt64(stop.Id); writer.WriteByte((byte)stop.Kind); writer.WriteDouble(stop.X); writer.WriteDouble(stop.Y); writer.WriteDouble(stop.Z); writer.WriteUInt64(stop.LaneId); writer.WriteUInt64(stop.StationId); writer.WriteUInt64(stop.PlatformId); }
        foreach (var pattern in message.Patterns)
        {
            writer.WriteUInt64(pattern.Id); writer.WriteUInt64(pattern.LineId); writer.WriteUInt64(pattern.RailwayServiceId); writer.WriteUInt32(checked((uint)pattern.Stops.Count));
            foreach (var stop in pattern.Stops) { writer.WriteUInt64(stop.StopId); writer.WriteUInt64(stop.TravelTicksFromPrevious); writer.WriteUInt64(stop.DwellTicks); }
        }
        foreach (var vehicle in message.Vehicles)
        {
            writer.WriteUInt64(vehicle.Id); writer.WriteByte((byte)vehicle.Kind); writer.WriteUInt64(vehicle.TripId); writer.WriteUInt64(vehicle.RoadVehicleId); writer.WriteInt32(vehicle.StopIndex); writer.WriteDouble(vehicle.X); writer.WriteDouble(vehicle.Y); writer.WriteDouble(vehicle.Z); writer.WriteByte((byte)vehicle.State); writer.WriteUInt64(vehicle.EstimatedArrivalTick); writer.WriteUInt64(vehicle.DwellUntilTick);
        }
        foreach (var arrival in message.ArrivalEstimates) { writer.WriteUInt64(arrival.StopId); writer.WriteUInt64(arrival.LineId); writer.WriteUInt64(arrival.VehicleId); writer.WriteUInt64(arrival.EstimatedArrivalTick); }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out MultimodalTransitSnapshotMessage message, out ProtocolDecodeError error)
    {
        message = null!;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.MultimodalTransitSnapshot) { error = ProtocolDecodeError.UnknownMessageType; return false; }
        if (!header.Version.SupportsMultimodalTransit || header.PayloadLength < HeaderLength) { error = ProtocolDecodeError.InvalidPayload; return false; }
        try
        {
            var reader = new SpanReader(frame[ProtocolFrameHeader.Size..]);
            var tick = reader.ReadUInt64();
            var lineCount = reader.ReadCount(LineLength); var stopCount = reader.ReadCount(StopLength); var patternCount = reader.ReadCount(PatternHeaderLength); var vehicleCount = reader.ReadCount(VehicleLength); var arrivalCount = reader.ReadCount(ArrivalLength);
            var lines = new ProtocolTransitLine[lineCount];
            for (var i = 0; i < lines.Length; i++) lines[i] = new ProtocolTransitLine(reader.ReadUInt64(), (ProtocolTransitMode)reader.ReadByte());
            var stops = new ProtocolTransitStop[stopCount];
            for (var i = 0; i < stops.Length; i++) stops[i] = new ProtocolTransitStop(reader.ReadUInt64(), (ProtocolTransitStopKind)reader.ReadByte(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
            var patterns = new ProtocolTransitPattern[patternCount];
            for (var i = 0; i < patterns.Length; i++)
            {
                var id = reader.ReadUInt64(); var lineId = reader.ReadUInt64(); var railwayServiceId = reader.ReadUInt64(); var count = reader.ReadCount(PatternStopLength); var patternStops = new ProtocolTransitPatternStop[count];
                for (var j = 0; j < count; j++) patternStops[j] = new ProtocolTransitPatternStop(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
                patterns[i] = new ProtocolTransitPattern(id, lineId, railwayServiceId, patternStops);
            }
            var vehicles = new ProtocolTransitVehicle[vehicleCount];
            for (var i = 0; i < vehicles.Length; i++) vehicles[i] = new ProtocolTransitVehicle(reader.ReadUInt64(), (ProtocolTransitVehicleKind)reader.ReadByte(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadInt32(), reader.ReadDouble(), reader.ReadDouble(), reader.ReadDouble(), (ProtocolTransitVehicleState)reader.ReadByte(), reader.ReadUInt64(), reader.ReadUInt64());
            var arrivals = new ProtocolTransitArrivalEstimate[arrivalCount];
            for (var i = 0; i < arrivals.Length; i++) arrivals[i] = new ProtocolTransitArrivalEstimate(reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64(), reader.ReadUInt64());
            if (!reader.IsComplete) throw new InvalidDataException();
            message = new MultimodalTransitSnapshotMessage(tick, lines, stops, patterns, vehicles, arrivals); Validate(message); error = ProtocolDecodeError.None; return true;
        }
        catch (Exception exception) when (exception is InvalidDataException or ArgumentOutOfRangeException or OverflowException) { error = ProtocolDecodeError.InvalidPayload; return false; }
    }

    private static void Validate(MultimodalTransitSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Lines); ArgumentNullException.ThrowIfNull(message.Stops); ArgumentNullException.ThrowIfNull(message.Patterns); ArgumentNullException.ThrowIfNull(message.Vehicles); ArgumentNullException.ThrowIfNull(message.ArrivalEstimates);
        var lineById = new Dictionary<ulong, ProtocolTransitMode>();
        foreach (var item in message.Lines)
            if (item.Id == 0 || !lineById.TryAdd(item.Id, item.Mode) || !Enum.IsDefined(item.Mode) || item.Mode is not (ProtocolTransitMode.Bus or ProtocolTransitMode.Railway)) throw new ArgumentOutOfRangeException(nameof(message));
        var stopIds = new HashSet<ulong>();
        foreach (var item in message.Stops)
        {
            if (item.Id == 0 || !stopIds.Add(item.Id) || !Enum.IsDefined(item.Kind) || !Finite(item.X, item.Y, item.Z)) throw new ArgumentOutOfRangeException(nameof(message));
            if ((item.Kind == ProtocolTransitStopKind.Bus && item.LaneId == 0) || (item.Kind == ProtocolTransitStopKind.Railway && item.StationId == 0)) throw new ArgumentOutOfRangeException(nameof(message));
        }
        var patternIds = new HashSet<ulong>();
        foreach (var pattern in message.Patterns)
        {
            ArgumentNullException.ThrowIfNull(pattern); ArgumentNullException.ThrowIfNull(pattern.Stops);
            if (pattern.Id == 0 || !patternIds.Add(pattern.Id) || !lineById.TryGetValue(pattern.LineId, out var lineMode) || pattern.Stops.Count < 2) throw new ArgumentOutOfRangeException(nameof(message));
            if ((lineMode == ProtocolTransitMode.Railway && pattern.RailwayServiceId == 0) || (lineMode == ProtocolTransitMode.Bus && pattern.RailwayServiceId != 0)) throw new ArgumentOutOfRangeException(nameof(message));
            for (var index = 0; index < pattern.Stops.Count; index++) { var stop = pattern.Stops[index]; if (!stopIds.Contains(stop.StopId) || (index == 0 && stop.TravelTicksFromPrevious != 0) || (index > 0 && stop.TravelTicksFromPrevious == 0)) throw new ArgumentOutOfRangeException(nameof(message)); }
        }
        var vehicleIds = new HashSet<ulong>(); foreach (var item in message.Vehicles) if (item.Id == 0 || !vehicleIds.Add(item.Id) || !Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.State) || item.StopIndex < 0 || !Finite(item.X, item.Y, item.Z)) throw new ArgumentOutOfRangeException(nameof(message));
        foreach (var item in message.ArrivalEstimates) if (!stopIds.Contains(item.StopId) || !lineById.ContainsKey(item.LineId) || !vehicleIds.Contains(item.VehicleId)) throw new ArgumentOutOfRangeException(nameof(message));
    }
    private static bool Finite(double x, double y, double z) => double.IsFinite(x) && double.IsFinite(y) && double.IsFinite(z);

    private ref struct SpanWriter
    {
        private Span<byte> buffer; private int offset; public SpanWriter(Span<byte> buffer) { this.buffer = buffer; offset = 0; }
        public void WriteByte(byte value) => buffer[offset++] = value;
        public void WriteUInt32(uint value) { BinaryPrimitives.WriteUInt32LittleEndian(buffer[offset..], value); offset += 4; }
        public void WriteInt32(int value) { BinaryPrimitives.WriteInt32LittleEndian(buffer[offset..], value); offset += 4; }
        public void WriteUInt64(ulong value) { BinaryPrimitives.WriteUInt64LittleEndian(buffer[offset..], value); offset += 8; }
        public void WriteDouble(double value) { BinaryPrimitives.WriteInt64LittleEndian(buffer[offset..], BitConverter.DoubleToInt64Bits(value)); offset += 8; }
    }
    private ref struct SpanReader
    {
        private readonly ReadOnlySpan<byte> buffer; private int offset; public SpanReader(ReadOnlySpan<byte> buffer) { this.buffer = buffer; offset = 0; } public bool IsComplete => offset == buffer.Length; private int Remaining => buffer.Length - offset;
        public byte ReadByte() { Ensure(1); return buffer[offset++]; } public uint ReadUInt32() { Ensure(4); var v = BinaryPrimitives.ReadUInt32LittleEndian(buffer[offset..]); offset += 4; return v; } public int ReadInt32() { Ensure(4); var v = BinaryPrimitives.ReadInt32LittleEndian(buffer[offset..]); offset += 4; return v; } public ulong ReadUInt64() { Ensure(8); var v = BinaryPrimitives.ReadUInt64LittleEndian(buffer[offset..]); offset += 8; return v; } public double ReadDouble() { Ensure(8); var v = BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(buffer[offset..])); offset += 8; return v; }
        public int ReadCount(int minimum) { var raw = ReadUInt32(); if (raw > int.MaxValue) throw new InvalidDataException(); var v = (int)raw; if (minimum > 0 && v > Remaining / minimum) throw new InvalidDataException(); return v; }
        private void Ensure(int length) { if (Remaining < length) throw new InvalidDataException(); }
    }
}
