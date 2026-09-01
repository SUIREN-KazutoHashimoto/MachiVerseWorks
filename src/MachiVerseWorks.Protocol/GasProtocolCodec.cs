using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class GasProtocolCodec
{
    private const int StatisticsPayloadLength = 84;
    private const int FixedPayloadLength = StatisticsPayloadLength + 8;
    private const int NodePayloadLength = 33;
    private const int PipelinePayloadLength = 33;
    private const int FacilityPayloadLength = 42;
    private const int ServicePointPayloadLength = 74;

    public static byte[] Serialize(GasSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Pipelines);
        ArgumentNullException.ThrowIfNull(message.Facilities);
        ArgumentNullException.ThrowIfNull(message.ServicePoints);
        if (!version.SupportsGas)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Gas messages require Protocol 2.14 or newer.");
        if (message.Nodes.Count > ushort.MaxValue || message.Pipelines.Count > ushort.MaxValue
            || message.Facilities.Count > ushort.MaxValue || message.ServicePoints.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(message), "Gas debug entry counts must fit in UInt16.");
        if (!IsValidStatistics(message.Statistics)) throw new ArgumentOutOfRangeException(nameof(message), "Gas statistics contain invalid values.");

        var payloadLength = checked(FixedPayloadLength
            + (message.Nodes.Count * NodePayloadLength)
            + (message.Pipelines.Count * PipelinePayloadLength)
            + (message.Facilities.Count * FacilityPayloadLength)
            + (message.ServicePoints.Count * ServicePointPayloadLength));
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.GasSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteStatistics(payload, message.Statistics);
        WriteUInt16(payload[84..], checked((ushort)message.Nodes.Count));
        WriteUInt16(payload[86..], checked((ushort)message.Pipelines.Count));
        WriteUInt16(payload[88..], checked((ushort)message.Facilities.Count));
        WriteUInt16(payload[90..], checked((ushort)message.ServicePoints.Count));

        var offset = FixedPayloadLength;
        foreach (var item in message.Nodes) { ValidateNode(item); WriteNode(payload.Slice(offset, NodePayloadLength), item); offset += NodePayloadLength; }
        foreach (var item in message.Pipelines) { ValidatePipeline(item); WritePipeline(payload.Slice(offset, PipelinePayloadLength), item); offset += PipelinePayloadLength; }
        foreach (var item in message.Facilities) { ValidateFacility(item); WriteFacility(payload.Slice(offset, FacilityPayloadLength), item); offset += FacilityPayloadLength; }
        foreach (var item in message.ServicePoints) { ValidateServicePoint(item); WriteServicePoint(payload.Slice(offset, ServicePointPayloadLength), item); offset += ServicePointPayloadLength; }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsGas || header.MessageType != MessageType.GasSnapshot)
        {
            error = header.MessageType == MessageType.GasSnapshot ? ProtocolDecodeError.InvalidPayload : ProtocolDecodeError.UnknownMessageType;
            return false;
        }
        var payload = frame[ProtocolFrameHeader.Size..];
        if (payload.Length < FixedPayloadLength) { error = ProtocolDecodeError.InvalidPayload; return false; }
        var nodeCount = ReadUInt16(payload[84..]);
        var pipelineCount = ReadUInt16(payload[86..]);
        var facilityCount = ReadUInt16(payload[88..]);
        var servicePointCount = ReadUInt16(payload[90..]);
        int expected;
        try { expected = checked(FixedPayloadLength + nodeCount * NodePayloadLength + pipelineCount * PipelinePayloadLength + facilityCount * FacilityPayloadLength + servicePointCount * ServicePointPayloadLength); }
        catch (OverflowException) { error = ProtocolDecodeError.InvalidPayload; return false; }
        if (payload.Length != expected) { error = ProtocolDecodeError.InvalidPayload; return false; }
        var statistics = ReadStatistics(payload);
        if (!IsValidStatistics(statistics)) { error = ProtocolDecodeError.InvalidPayload; return false; }

        var offset = FixedPayloadLength;
        var nodes = new ProtocolGasNode[nodeCount];
        for (var i = 0; i < nodes.Length; i++) { var item = ReadNode(payload.Slice(offset, NodePayloadLength)); if (!IsValidNode(item)) { error = ProtocolDecodeError.InvalidPayload; return false; } nodes[i] = item; offset += NodePayloadLength; }
        var pipelines = new ProtocolGasPipeline[pipelineCount];
        for (var i = 0; i < pipelines.Length; i++) { var raw = payload.Slice(offset, PipelinePayloadLength); if (raw[32] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; } var item = ReadPipeline(raw); if (!IsValidPipeline(item)) { error = ProtocolDecodeError.InvalidPayload; return false; } pipelines[i] = item; offset += PipelinePayloadLength; }
        var facilities = new ProtocolGasFacility[facilityCount];
        for (var i = 0; i < facilities.Length; i++) { var item = ReadFacility(payload.Slice(offset, FacilityPayloadLength)); if (!IsValidFacility(item)) { error = ProtocolDecodeError.InvalidPayload; return false; } facilities[i] = item; offset += FacilityPayloadLength; }
        var servicePoints = new ProtocolGasServicePoint[servicePointCount];
        for (var i = 0; i < servicePoints.Length; i++) { var item = ReadServicePoint(payload.Slice(offset, ServicePointPayloadLength)); if (!IsValidServicePoint(item)) { error = ProtocolDecodeError.InvalidPayload; return false; } servicePoints[i] = item; offset += ServicePointPayloadLength; }

        envelope = new ProtocolEnvelope(header.Version, new GasSnapshotMessage(statistics, Array.AsReadOnly(nodes), Array.AsReadOnly(pipelines), Array.AsReadOnly(facilities), Array.AsReadOnly(servicePoints)));
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void WriteStatistics(Span<byte> p, ProtocolGasStatistics v)
    {
        WriteUInt32(p, v.NodeCount); WriteUInt32(p[4..], v.PipelineCount); WriteUInt32(p[8..], v.SourceCount); WriteUInt32(p[12..], v.ImportTerminalCount);
        WriteUInt32(p[16..], v.StorageCount); WriteUInt32(p[20..], v.ServicePointCount); WriteUInt32(p[24..], v.PipedServicePointCount); WriteUInt32(p[28..], v.DeliveredServicePointCount);
        WriteUInt32(p[32..], v.UnavailableServicePointCount); WriteDouble(p[36..], v.SupplyCapacityCubicMetersPerDay); WriteDouble(p[44..], v.DemandCubicMetersPerDay);
        WriteDouble(p[52..], v.ServedCubicMetersPerDay); WriteDouble(p[60..], v.UnservedCubicMetersPerDay); WriteDouble(p[68..], v.StoredCubicMeters); WriteUInt64(p[76..], v.TickCount);
    }

    private static ProtocolGasStatistics ReadStatistics(ReadOnlySpan<byte> p) => new(
        ReadUInt32(p), ReadUInt32(p[4..]), ReadUInt32(p[8..]), ReadUInt32(p[12..]), ReadUInt32(p[16..]), ReadUInt32(p[20..]), ReadUInt32(p[24..]), ReadUInt32(p[28..]), ReadUInt32(p[32..]),
        ReadDouble(p[36..]), ReadDouble(p[44..]), ReadDouble(p[52..]), ReadDouble(p[60..]), ReadDouble(p[68..]), ReadUInt64(p[76..]));

    private static void WriteNode(Span<byte> p, ProtocolGasNode v) { WriteUInt64(p, v.NodeId); p[8] = (byte)v.Kind; WriteDouble(p[9..], v.X); WriteDouble(p[17..], v.Y); WriteDouble(p[25..], v.Z); }
    private static ProtocolGasNode ReadNode(ReadOnlySpan<byte> p) => new(ReadUInt64(p), (ProtocolGasNodeKind)p[8], ReadDouble(p[9..]), ReadDouble(p[17..]), ReadDouble(p[25..]));
    private static void WritePipeline(Span<byte> p, ProtocolGasPipeline v) { WriteUInt64(p, v.PipelineId); WriteUInt64(p[8..], v.FromNodeId); WriteUInt64(p[16..], v.ToNodeId); WriteDouble(p[24..], v.CapacityCubicMetersPerDay); p[32] = v.IsInService ? (byte)1 : (byte)0; }
    private static ProtocolGasPipeline ReadPipeline(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadUInt64(p[16..]), ReadDouble(p[24..]), p[32] != 0);
    private static void WriteFacility(Span<byte> p, ProtocolGasFacility v) { p[0] = (byte)v.Kind; WriteUInt64(p[1..], v.FacilityId); WriteUInt64(p[9..], v.NodeId); WriteDouble(p[17..], v.CapacityCubicMetersPerDay); WriteDouble(p[25..], v.OutputCubicMetersPerDay); WriteDouble(p[33..], v.StoredCubicMeters); p[41] = (byte)v.OperatingState; }
    private static ProtocolGasFacility ReadFacility(ReadOnlySpan<byte> p) => new((ProtocolGasFacilityKind)p[0], ReadUInt64(p[1..]), ReadUInt64(p[9..]), ReadDouble(p[17..]), ReadDouble(p[25..]), ReadDouble(p[33..]), (ProtocolGasOperatingState)p[41]);
    private static void WriteServicePoint(Span<byte> p, ProtocolGasServicePoint v) { WriteUInt64(p, v.ServicePointId); WriteUInt64(p[8..], v.NodeId); WriteUInt64(p[16..], v.BuildingId); WriteUInt64(p[24..], v.EstablishmentId); p[32] = (byte)v.DeliveryMode; WriteUInt64(p[33..], v.CommodityId); WriteDouble(p[41..], v.BaseDemandCubicMetersPerDay); WriteDouble(p[49..], v.DemandCubicMetersPerDay); WriteDouble(p[57..], v.ServedCubicMetersPerDay); WriteDouble(p[65..], v.UnservedCubicMetersPerDay); p[73] = (byte)v.ServiceState; }
    private static ProtocolGasServicePoint ReadServicePoint(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadUInt64(p[16..]), ReadUInt64(p[24..]), (ProtocolGasDeliveryMode)p[32], ReadUInt64(p[33..]), ReadDouble(p[41..]), ReadDouble(p[49..]), ReadDouble(p[57..]), ReadDouble(p[65..]), (ProtocolGasServiceState)p[73]);

    private static void ValidateNode(ProtocolGasNode v) { if (!IsValidNode(v)) throw new ArgumentOutOfRangeException(nameof(v)); }
    private static bool IsValidNode(ProtocolGasNode v) => v.NodeId != 0 && Enum.IsDefined(v.Kind) && double.IsFinite(v.X) && double.IsFinite(v.Y) && double.IsFinite(v.Z);
    private static void ValidatePipeline(ProtocolGasPipeline v) { if (!IsValidPipeline(v)) throw new ArgumentOutOfRangeException(nameof(v)); }
    private static bool IsValidPipeline(ProtocolGasPipeline v) => v.PipelineId != 0 && v.FromNodeId != 0 && v.ToNodeId != 0 && v.FromNodeId != v.ToNodeId && IsPositiveFinite(v.CapacityCubicMetersPerDay);
    private static void ValidateFacility(ProtocolGasFacility v) { if (!IsValidFacility(v)) throw new ArgumentOutOfRangeException(nameof(v)); }
    private static bool IsValidFacility(ProtocolGasFacility v) => Enum.IsDefined(v.Kind) && v.FacilityId != 0 && v.NodeId != 0 && IsPositiveFinite(v.CapacityCubicMetersPerDay) && IsNonNegativeFinite(v.OutputCubicMetersPerDay) && IsNonNegativeFinite(v.StoredCubicMeters) && v.OutputCubicMetersPerDay <= v.CapacityCubicMetersPerDay + 1e-9 && Enum.IsDefined(v.OperatingState);
    private static void ValidateServicePoint(ProtocolGasServicePoint v) { if (!IsValidServicePoint(v)) throw new ArgumentOutOfRangeException(nameof(v)); }
    private static bool IsValidServicePoint(ProtocolGasServicePoint v)
    {
        if (v.ServicePointId == 0 || (v.BuildingId == 0 && v.EstablishmentId == 0) || !Enum.IsDefined(v.DeliveryMode) || !Enum.IsDefined(v.ServiceState)
            || !IsPositiveFinite(v.BaseDemandCubicMetersPerDay) || !IsNonNegativeFinite(v.DemandCubicMetersPerDay) || !IsNonNegativeFinite(v.ServedCubicMetersPerDay) || !IsNonNegativeFinite(v.UnservedCubicMetersPerDay)
            || v.ServedCubicMetersPerDay > v.DemandCubicMetersPerDay + 1e-9) return false;
        return v.DeliveryMode == ProtocolGasDeliveryMode.Piped ? v.NodeId != 0 && v.CommodityId == 0 : v.NodeId == 0 && v.EstablishmentId != 0 && v.CommodityId != 0;
    }
    private static bool IsValidStatistics(ProtocolGasStatistics v) => IsNonNegativeFinite(v.SupplyCapacityCubicMetersPerDay) && IsNonNegativeFinite(v.DemandCubicMetersPerDay) && IsNonNegativeFinite(v.ServedCubicMetersPerDay) && IsNonNegativeFinite(v.UnservedCubicMetersPerDay) && IsNonNegativeFinite(v.StoredCubicMeters) && v.ServedCubicMetersPerDay <= v.DemandCubicMetersPerDay + 1e-9;
    private static bool IsPositiveFinite(double value) => double.IsFinite(value) && value > 0d;
    private static bool IsNonNegativeFinite(double value) => double.IsFinite(value) && value >= 0d;
    private static void WriteUInt16(Span<byte> p, ushort v) => BinaryPrimitives.WriteUInt16LittleEndian(p, v);
    private static ushort ReadUInt16(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt16LittleEndian(p);
    private static void WriteUInt32(Span<byte> p, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(p, v);
    private static uint ReadUInt32(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt32LittleEndian(p);
    private static void WriteUInt64(Span<byte> p, ulong v) => BinaryPrimitives.WriteUInt64LittleEndian(p, v);
    private static ulong ReadUInt64(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt64LittleEndian(p);
    private static void WriteDouble(Span<byte> p, double v) => BinaryPrimitives.WriteInt64LittleEndian(p, BitConverter.DoubleToInt64Bits(v));
    private static double ReadDouble(ReadOnlySpan<byte> p) => BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(p));
}
