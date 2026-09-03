using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class WaterSewerProtocolCodec
{
    private const int StatisticsPayloadLength = 104;
    private const int FixedPayloadLength = StatisticsPayloadLength + 8;
    private const int NodePayloadLength = 34;
    private const int PipePayloadLength = 34;
    private const int FacilityPayloadLength = 42;
    private const int ServicePointPayloadLength = 106;

    public static byte[] Serialize(WaterSewerSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Pipes);
        ArgumentNullException.ThrowIfNull(message.Facilities);
        ArgumentNullException.ThrowIfNull(message.ServicePoints);
        if (!version.SupportsWaterSewer)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Water/Sewer messages require Protocol 2.13 or newer.");
        if (message.Nodes.Count > ushort.MaxValue
            || message.Pipes.Count > ushort.MaxValue
            || message.Facilities.Count > ushort.MaxValue
            || message.ServicePoints.Count > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(message), "Water/Sewer debug entry counts must fit in UInt16.");
        }
        ValidateStatistics(message.Statistics, nameof(message));

        var payloadLength = checked(
            FixedPayloadLength
            + (message.Nodes.Count * NodePayloadLength)
            + (message.Pipes.Count * PipePayloadLength)
            + (message.Facilities.Count * FacilityPayloadLength)
            + (message.ServicePoints.Count * ServicePointPayloadLength));
        var frame = new byte[ProtocolFrameHeader.GetFrameLength(payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.WaterSewerSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteStatistics(payload, message.Statistics);
        WriteUInt16(payload[104..], checked((ushort)message.Nodes.Count));
        WriteUInt16(payload[106..], checked((ushort)message.Pipes.Count));
        WriteUInt16(payload[108..], checked((ushort)message.Facilities.Count));
        WriteUInt16(payload[110..], checked((ushort)message.ServicePoints.Count));

        var offset = FixedPayloadLength;
        foreach (var node in message.Nodes)
        {
            ValidateNode(node, nameof(message));
            WriteNode(payload.Slice(offset, NodePayloadLength), node);
            offset += NodePayloadLength;
        }
        foreach (var pipe in message.Pipes)
        {
            ValidatePipe(pipe, nameof(message));
            WritePipe(payload.Slice(offset, PipePayloadLength), pipe);
            offset += PipePayloadLength;
        }
        foreach (var facility in message.Facilities)
        {
            ValidateFacility(facility, nameof(message));
            WriteFacility(payload.Slice(offset, FacilityPayloadLength), facility);
            offset += FacilityPayloadLength;
        }
        foreach (var servicePoint in message.ServicePoints)
        {
            ValidateServicePoint(servicePoint, nameof(message));
            WriteServicePoint(payload.Slice(offset, ServicePointPayloadLength), servicePoint);
            offset += ServicePointPayloadLength;
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsWaterSewer || header.MessageType != MessageType.WaterSewerSnapshot)
        {
            error = header.MessageType == MessageType.WaterSewerSnapshot
                ? ProtocolDecodeError.InvalidPayload
                : ProtocolDecodeError.UnknownMessageType;
            return false;
        }

        var payload = frame[ProtocolFrameHeader.Size..];
        if (payload.Length < FixedPayloadLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        var nodeCount = ReadUInt16(payload[104..]);
        var pipeCount = ReadUInt16(payload[106..]);
        var facilityCount = ReadUInt16(payload[108..]);
        var servicePointCount = ReadUInt16(payload[110..]);
        int expectedLength;
        try
        {
            expectedLength = checked(
                FixedPayloadLength
                + (nodeCount * NodePayloadLength)
                + (pipeCount * PipePayloadLength)
                + (facilityCount * FacilityPayloadLength)
                + (servicePointCount * ServicePointPayloadLength));
        }
        catch (OverflowException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        if (payload.Length != expectedLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var statistics = ReadStatistics(payload);
        if (!IsValidStatistics(statistics))
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        var offset = FixedPayloadLength;
        var nodes = new ProtocolUtilityNode[nodeCount];
        for (var index = 0; index < nodes.Length; index++)
        {
            var value = ReadNode(payload.Slice(offset, NodePayloadLength));
            if (!IsValidNode(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            nodes[index] = value;
            offset += NodePayloadLength;
        }
        var pipes = new ProtocolUtilityPipe[pipeCount];
        for (var index = 0; index < pipes.Length; index++)
        {
            var raw = payload.Slice(offset, PipePayloadLength);
            if (raw[33] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; }
            var value = ReadPipe(raw);
            if (!IsValidPipe(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            pipes[index] = value;
            offset += PipePayloadLength;
        }
        var facilities = new ProtocolUtilityFacility[facilityCount];
        for (var index = 0; index < facilities.Length; index++)
        {
            var value = ReadFacility(payload.Slice(offset, FacilityPayloadLength));
            if (!IsValidFacility(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            facilities[index] = value;
            offset += FacilityPayloadLength;
        }
        var servicePoints = new ProtocolWaterSewerServicePoint[servicePointCount];
        for (var index = 0; index < servicePoints.Length; index++)
        {
            var value = ReadServicePoint(payload.Slice(offset, ServicePointPayloadLength));
            if (!IsValidServicePoint(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            servicePoints[index] = value;
            offset += ServicePointPayloadLength;
        }

        envelope = new ProtocolEnvelope(
            header.Version,
            new WaterSewerSnapshotMessage(
                statistics,
                Array.AsReadOnly(nodes),
                Array.AsReadOnly(pipes),
                Array.AsReadOnly(facilities),
                Array.AsReadOnly(servicePoints)));
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void WriteStatistics(Span<byte> payload, ProtocolWaterSewerStatistics value)
    {
        WriteUInt32(payload, value.WaterNodeCount);
        WriteUInt32(payload[4..], value.WaterPipeCount);
        WriteUInt32(payload[8..], value.SewerNodeCount);
        WriteUInt32(payload[12..], value.SewerPipeCount);
        WriteUInt32(payload[16..], value.WaterSourceCount);
        WriteUInt32(payload[20..], value.ReservoirCount);
        WriteUInt32(payload[24..], value.PumpCount);
        WriteUInt32(payload[28..], value.TreatmentPlantCount);
        WriteUInt32(payload[32..], value.ServicePointCount);
        WriteUInt32(payload[36..], value.WaterUnavailableCount);
        WriteUInt32(payload[40..], value.SewerUnavailableCount);
        WriteUInt32(payload[44..], value.SewerOverflowCount);
        WriteDouble(payload[48..], value.WaterSupplyCapacityCubicMetersPerDay);
        WriteDouble(payload[56..], value.WaterDemandCubicMetersPerDay);
        WriteDouble(payload[64..], value.WaterServedCubicMetersPerDay);
        WriteDouble(payload[72..], value.WastewaterGeneratedCubicMetersPerDay);
        WriteDouble(payload[80..], value.WastewaterProcessedCubicMetersPerDay);
        WriteDouble(payload[88..], value.WastewaterOverflowCubicMetersPerDay);
        WriteUInt64(payload[96..], value.TickCount);
    }

    private static ProtocolWaterSewerStatistics ReadStatistics(ReadOnlySpan<byte> payload) => new(
        ReadUInt32(payload),
        ReadUInt32(payload[4..]),
        ReadUInt32(payload[8..]),
        ReadUInt32(payload[12..]),
        ReadUInt32(payload[16..]),
        ReadUInt32(payload[20..]),
        ReadUInt32(payload[24..]),
        ReadUInt32(payload[28..]),
        ReadUInt32(payload[32..]),
        ReadUInt32(payload[36..]),
        ReadUInt32(payload[40..]),
        ReadUInt32(payload[44..]),
        ReadDouble(payload[48..]),
        ReadDouble(payload[56..]),
        ReadDouble(payload[64..]),
        ReadDouble(payload[72..]),
        ReadDouble(payload[80..]),
        ReadDouble(payload[88..]),
        ReadUInt64(payload[96..]));

    private static void WriteNode(Span<byte> payload, ProtocolUtilityNode value)
    {
        payload[0] = (byte)value.NetworkKind;
        WriteUInt64(payload[1..], value.NodeId);
        payload[9] = (byte)value.Kind;
        WriteDouble(payload[10..], value.X);
        WriteDouble(payload[18..], value.Y);
        WriteDouble(payload[26..], value.Z);
    }

    private static ProtocolUtilityNode ReadNode(ReadOnlySpan<byte> payload) => new(
        (ProtocolUtilityNetworkKind)payload[0],
        ReadUInt64(payload[1..]),
        (ProtocolUtilityNodeKind)payload[9],
        ReadDouble(payload[10..]),
        ReadDouble(payload[18..]),
        ReadDouble(payload[26..]));

    private static void WritePipe(Span<byte> payload, ProtocolUtilityPipe value)
    {
        payload[0] = (byte)value.NetworkKind;
        WriteUInt64(payload[1..], value.PipeId);
        WriteUInt64(payload[9..], value.FromNodeId);
        WriteUInt64(payload[17..], value.ToNodeId);
        WriteDouble(payload[25..], value.CapacityCubicMetersPerDay);
        payload[33] = value.IsInService ? (byte)1 : (byte)0;
    }

    private static ProtocolUtilityPipe ReadPipe(ReadOnlySpan<byte> payload) => new(
        (ProtocolUtilityNetworkKind)payload[0],
        ReadUInt64(payload[1..]),
        ReadUInt64(payload[9..]),
        ReadUInt64(payload[17..]),
        ReadDouble(payload[25..]),
        payload[33] != 0);

    private static void WriteFacility(Span<byte> payload, ProtocolUtilityFacility value)
    {
        payload[0] = (byte)value.Kind;
        WriteUInt64(payload[1..], value.FacilityId);
        WriteUInt64(payload[9..], value.NodeId);
        WriteUInt64(payload[17..], value.PowerLoadId);
        WriteDouble(payload[25..], value.CapacityCubicMetersPerDay);
        WriteDouble(payload[33..], value.ThroughputCubicMetersPerDay);
        payload[41] = (byte)value.OperatingState;
    }

    private static ProtocolUtilityFacility ReadFacility(ReadOnlySpan<byte> payload) => new(
        (ProtocolUtilityFacilityKind)payload[0],
        ReadUInt64(payload[1..]),
        ReadUInt64(payload[9..]),
        ReadUInt64(payload[17..]),
        ReadDouble(payload[25..]),
        ReadDouble(payload[33..]),
        (ProtocolUtilityOperatingState)payload[41]);

    private static void WriteServicePoint(Span<byte> payload, ProtocolWaterSewerServicePoint value)
    {
        WriteUInt64(payload, value.ServicePointId);
        WriteUInt64(payload[8..], value.WaterNodeId);
        WriteUInt64(payload[16..], value.SewerNodeId);
        WriteUInt64(payload[24..], value.BuildingId);
        WriteUInt64(payload[32..], value.EstablishmentId);
        WriteDouble(payload[40..], value.BaseWaterDemandCubicMetersPerDay);
        WriteDouble(payload[48..], value.WastewaterReturnRatio);
        WriteDouble(payload[56..], value.WaterDemandCubicMetersPerDay);
        WriteDouble(payload[64..], value.WaterServedCubicMetersPerDay);
        WriteDouble(payload[72..], value.WaterUnservedCubicMetersPerDay);
        payload[80] = (byte)value.WaterState;
        WriteDouble(payload[81..], value.WastewaterGeneratedCubicMetersPerDay);
        WriteDouble(payload[89..], value.WastewaterProcessedCubicMetersPerDay);
        WriteDouble(payload[97..], value.WastewaterOverflowCubicMetersPerDay);
        payload[105] = (byte)value.SewerState;
    }

    private static ProtocolWaterSewerServicePoint ReadServicePoint(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload),
        ReadUInt64(payload[8..]),
        ReadUInt64(payload[16..]),
        ReadUInt64(payload[24..]),
        ReadUInt64(payload[32..]),
        ReadDouble(payload[40..]),
        ReadDouble(payload[48..]),
        ReadDouble(payload[56..]),
        ReadDouble(payload[64..]),
        ReadDouble(payload[72..]),
        (ProtocolWaterServiceState)payload[80],
        ReadDouble(payload[81..]),
        ReadDouble(payload[89..]),
        ReadDouble(payload[97..]),
        (ProtocolSewerServiceState)payload[105]);

    private static void ValidateStatistics(ProtocolWaterSewerStatistics value, string parameterName)
    {
        if (!IsValidStatistics(value))
            throw new ArgumentOutOfRangeException(parameterName, "Water/Sewer statistics contain invalid values.");
    }

    private static bool IsValidStatistics(ProtocolWaterSewerStatistics value) =>
        IsNonNegativeFinite(value.WaterSupplyCapacityCubicMetersPerDay)
        && IsNonNegativeFinite(value.WaterDemandCubicMetersPerDay)
        && IsNonNegativeFinite(value.WaterServedCubicMetersPerDay)
        && IsNonNegativeFinite(value.WastewaterGeneratedCubicMetersPerDay)
        && IsNonNegativeFinite(value.WastewaterProcessedCubicMetersPerDay)
        && IsNonNegativeFinite(value.WastewaterOverflowCubicMetersPerDay)
        && value.WaterServedCubicMetersPerDay <= value.WaterDemandCubicMetersPerDay + 1e-9
        && value.WastewaterProcessedCubicMetersPerDay <= value.WastewaterGeneratedCubicMetersPerDay + 1e-9;

    private static void ValidateNode(ProtocolUtilityNode value, string parameterName)
    {
        if (!IsValidNode(value)) throw new ArgumentOutOfRangeException(parameterName, "Water/Sewer node entry contains invalid values.");
    }

    private static bool IsValidNode(ProtocolUtilityNode value) =>
        Enum.IsDefined(value.NetworkKind)
        && value.NodeId != 0
        && Enum.IsDefined(value.Kind)
        && double.IsFinite(value.X)
        && double.IsFinite(value.Y)
        && double.IsFinite(value.Z)
        && IsNodeKindCompatible(value.NetworkKind, value.Kind);

    private static bool IsNodeKindCompatible(ProtocolUtilityNetworkKind network, ProtocolUtilityNodeKind kind) => network switch
    {
        ProtocolUtilityNetworkKind.Water => kind is ProtocolUtilityNodeKind.Source or ProtocolUtilityNodeKind.Reservoir or ProtocolUtilityNodeKind.Pump or ProtocolUtilityNodeKind.Distribution or ProtocolUtilityNodeKind.Service,
        ProtocolUtilityNetworkKind.Sewer => kind is ProtocolUtilityNodeKind.Service or ProtocolUtilityNodeKind.Collection or ProtocolUtilityNodeKind.Pump or ProtocolUtilityNodeKind.Treatment,
        _ => false,
    };

    private static void ValidatePipe(ProtocolUtilityPipe value, string parameterName)
    {
        if (!IsValidPipe(value)) throw new ArgumentOutOfRangeException(parameterName, "Water/Sewer pipe entry contains invalid values.");
    }

    private static bool IsValidPipe(ProtocolUtilityPipe value) =>
        Enum.IsDefined(value.NetworkKind)
        && value.PipeId != 0
        && value.FromNodeId != 0
        && value.ToNodeId != 0
        && value.FromNodeId != value.ToNodeId
        && double.IsFinite(value.CapacityCubicMetersPerDay)
        && value.CapacityCubicMetersPerDay > 0d;

    private static void ValidateFacility(ProtocolUtilityFacility value, string parameterName)
    {
        if (!IsValidFacility(value)) throw new ArgumentOutOfRangeException(parameterName, "Water/Sewer facility entry contains invalid values.");
    }

    private static bool IsValidFacility(ProtocolUtilityFacility value) =>
        Enum.IsDefined(value.Kind)
        && value.FacilityId != 0
        && value.NodeId != 0
        && double.IsFinite(value.CapacityCubicMetersPerDay)
        && value.CapacityCubicMetersPerDay > 0d
        && IsNonNegativeFinite(value.ThroughputCubicMetersPerDay)
        && value.ThroughputCubicMetersPerDay <= value.CapacityCubicMetersPerDay + 1e-9
        && Enum.IsDefined(value.OperatingState);

    private static void ValidateServicePoint(ProtocolWaterSewerServicePoint value, string parameterName)
    {
        if (!IsValidServicePoint(value)) throw new ArgumentOutOfRangeException(parameterName, "Water/Sewer service point entry contains invalid values.");
    }

    private static bool IsValidServicePoint(ProtocolWaterSewerServicePoint value) =>
        value.ServicePointId != 0
        && value.WaterNodeId != 0
        && value.SewerNodeId != 0
        && (value.BuildingId != 0 || value.EstablishmentId != 0)
        && double.IsFinite(value.BaseWaterDemandCubicMetersPerDay)
        && value.BaseWaterDemandCubicMetersPerDay > 0d
        && double.IsFinite(value.WastewaterReturnRatio)
        && value.WastewaterReturnRatio >= 0d
        && value.WastewaterReturnRatio <= 1d
        && IsNonNegativeFinite(value.WaterDemandCubicMetersPerDay)
        && IsNonNegativeFinite(value.WaterServedCubicMetersPerDay)
        && IsNonNegativeFinite(value.WaterUnservedCubicMetersPerDay)
        && value.WaterServedCubicMetersPerDay <= value.WaterDemandCubicMetersPerDay + 1e-9
        && Enum.IsDefined(value.WaterState)
        && IsNonNegativeFinite(value.WastewaterGeneratedCubicMetersPerDay)
        && IsNonNegativeFinite(value.WastewaterProcessedCubicMetersPerDay)
        && IsNonNegativeFinite(value.WastewaterOverflowCubicMetersPerDay)
        && value.WastewaterProcessedCubicMetersPerDay <= value.WastewaterGeneratedCubicMetersPerDay + 1e-9
        && value.WastewaterOverflowCubicMetersPerDay <= value.WastewaterGeneratedCubicMetersPerDay + 1e-9
        && Enum.IsDefined(value.SewerState);

    private static bool IsNonNegativeFinite(double value) => double.IsFinite(value) && value >= 0d;
    private static void WriteUInt16(Span<byte> destination, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(destination, value);
    private static ushort ReadUInt16(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt16LittleEndian(source);
    private static void WriteUInt32(Span<byte> destination, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(destination, value);
    private static uint ReadUInt32(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt32LittleEndian(source);
    private static void WriteUInt64(Span<byte> destination, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(destination, value);
    private static ulong ReadUInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadUInt64LittleEndian(source);
    private static void WriteInt64(Span<byte> destination, long value) => BinaryPrimitives.WriteInt64LittleEndian(destination, value);
    private static long ReadInt64(ReadOnlySpan<byte> source) => BinaryPrimitives.ReadInt64LittleEndian(source);
    private static void WriteDouble(Span<byte> destination, double value) => WriteInt64(destination, BitConverter.DoubleToInt64Bits(value));
    private static double ReadDouble(ReadOnlySpan<byte> source) => BitConverter.Int64BitsToDouble(ReadInt64(source));
}
