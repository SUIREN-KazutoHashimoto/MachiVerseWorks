using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class PowerProtocolCodec
{
    private const int FixedPayloadLength = 76;
    private const int NodePayloadLength = 33;
    private const int LinePayloadLength = 33;
    private const int GeneratorPayloadLength = 33;
    private const int LoadPayloadLength = 65;

    public static byte[] Serialize(PowerSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Lines);
        ArgumentNullException.ThrowIfNull(message.Generators);
        ArgumentNullException.ThrowIfNull(message.Loads);
        if (!version.SupportsPower) throw new ArgumentOutOfRangeException(nameof(version), version, "Power messages require Protocol 2.12 or newer.");
        if (message.Nodes.Count > ushort.MaxValue || message.Lines.Count > ushort.MaxValue || message.Generators.Count > ushort.MaxValue || message.Loads.Count > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(message), "Power debug entry counts must fit in UInt16.");
        ValidateStatistics(message.Statistics, nameof(message));

        var payloadLength = checked(FixedPayloadLength
            + (message.Nodes.Count * NodePayloadLength)
            + (message.Lines.Count * LinePayloadLength)
            + (message.Generators.Count * GeneratorPayloadLength)
            + (message.Loads.Count * LoadPayloadLength));
        var frame = new byte[ProtocolFrameHeader.GetFrameLength(payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.PowerSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteStatistics(payload, message.Statistics);
        WriteUInt16(payload[68..], checked((ushort)message.Nodes.Count));
        WriteUInt16(payload[70..], checked((ushort)message.Lines.Count));
        WriteUInt16(payload[72..], checked((ushort)message.Generators.Count));
        WriteUInt16(payload[74..], checked((ushort)message.Loads.Count));

        var offset = FixedPayloadLength;
        foreach (var node in message.Nodes)
        {
            ValidateNode(node, nameof(message));
            WriteNode(payload.Slice(offset, NodePayloadLength), node);
            offset += NodePayloadLength;
        }
        foreach (var line in message.Lines)
        {
            ValidateLine(line, nameof(message));
            WriteLine(payload.Slice(offset, LinePayloadLength), line);
            offset += LinePayloadLength;
        }
        foreach (var generator in message.Generators)
        {
            ValidateGenerator(generator, nameof(message));
            WriteGenerator(payload.Slice(offset, GeneratorPayloadLength), generator);
            offset += GeneratorPayloadLength;
        }
        foreach (var load in message.Loads)
        {
            ValidateLoad(load, nameof(message));
            WriteLoad(payload.Slice(offset, LoadPayloadLength), load);
            offset += LoadPayloadLength;
        }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsPower || header.MessageType != MessageType.PowerSnapshot)
        {
            error = header.MessageType == MessageType.PowerSnapshot ? ProtocolDecodeError.InvalidPayload : ProtocolDecodeError.UnknownMessageType;
            return false;
        }

        var payload = frame[ProtocolFrameHeader.Size..];
        if (payload.Length < FixedPayloadLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        var nodeCount = ReadUInt16(payload[68..]);
        var lineCount = ReadUInt16(payload[70..]);
        var generatorCount = ReadUInt16(payload[72..]);
        var loadCount = ReadUInt16(payload[74..]);
        int expectedLength;
        try
        {
            expectedLength = checked(FixedPayloadLength
                + (nodeCount * NodePayloadLength)
                + (lineCount * LinePayloadLength)
                + (generatorCount * GeneratorPayloadLength)
                + (loadCount * LoadPayloadLength));
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
        var nodes = new ProtocolPowerNode[nodeCount];
        for (var index = 0; index < nodes.Length; index++)
        {
            var value = ReadNode(payload.Slice(offset, NodePayloadLength));
            if (!IsValidNode(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            nodes[index] = value;
            offset += NodePayloadLength;
        }
        var lines = new ProtocolPowerLine[lineCount];
        for (var index = 0; index < lines.Length; index++)
        {
            var raw = payload.Slice(offset, LinePayloadLength);
            if (raw[32] > 1) { error = ProtocolDecodeError.InvalidPayload; return false; }
            var value = ReadLine(raw);
            if (!IsValidLine(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            lines[index] = value;
            offset += LinePayloadLength;
        }
        var generators = new ProtocolGenerator[generatorCount];
        for (var index = 0; index < generators.Length; index++)
        {
            var value = ReadGenerator(payload.Slice(offset, GeneratorPayloadLength));
            if (!IsValidGenerator(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            generators[index] = value;
            offset += GeneratorPayloadLength;
        }
        var loads = new ProtocolPowerLoad[loadCount];
        for (var index = 0; index < loads.Length; index++)
        {
            var value = ReadLoad(payload.Slice(offset, LoadPayloadLength));
            if (!IsValidLoad(value)) { error = ProtocolDecodeError.InvalidPayload; return false; }
            loads[index] = value;
            offset += LoadPayloadLength;
        }

        envelope = new ProtocolEnvelope(header.Version, new PowerSnapshotMessage(
            statistics,
            Array.AsReadOnly(nodes),
            Array.AsReadOnly(lines),
            Array.AsReadOnly(generators),
            Array.AsReadOnly(loads)));
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void WriteStatistics(Span<byte> payload, ProtocolPowerStatistics value)
    {
        WriteUInt32(payload, value.NodeCount);
        WriteUInt32(payload[4..], value.LineCount);
        WriteUInt32(payload[8..], value.GeneratorCount);
        WriteUInt32(payload[12..], value.LoadCount);
        WriteUInt32(payload[16..], value.OutageLoadCount);
        WriteDouble(payload[20..], value.GenerationCapacityMegawatts);
        WriteDouble(payload[28..], value.GenerationOutputMegawatts);
        WriteDouble(payload[36..], value.DemandMegawatts);
        WriteDouble(payload[44..], value.ServedMegawatts);
        WriteDouble(payload[52..], value.UnservedMegawatts);
        WriteUInt64(payload[60..], value.TickCount);
    }

    private static ProtocolPowerStatistics ReadStatistics(ReadOnlySpan<byte> payload) => new(
        ReadUInt32(payload), ReadUInt32(payload[4..]), ReadUInt32(payload[8..]), ReadUInt32(payload[12..]), ReadUInt32(payload[16..]),
        ReadDouble(payload[20..]), ReadDouble(payload[28..]), ReadDouble(payload[36..]), ReadDouble(payload[44..]), ReadDouble(payload[52..]), ReadUInt64(payload[60..]));

    private static void WriteNode(Span<byte> payload, ProtocolPowerNode value)
    {
        WriteUInt64(payload, value.NodeId);
        payload[8] = (byte)value.Kind;
        WriteDouble(payload[9..], value.X);
        WriteDouble(payload[17..], value.Y);
        WriteDouble(payload[25..], value.Z);
    }

    private static ProtocolPowerNode ReadNode(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload), (ProtocolPowerNodeKind)payload[8], ReadDouble(payload[9..]), ReadDouble(payload[17..]), ReadDouble(payload[25..]));

    private static void WriteLine(Span<byte> payload, ProtocolPowerLine value)
    {
        WriteUInt64(payload, value.LineId);
        WriteUInt64(payload[8..], value.FromNodeId);
        WriteUInt64(payload[16..], value.ToNodeId);
        WriteDouble(payload[24..], value.CapacityMegawatts);
        payload[32] = value.IsInService ? (byte)1 : (byte)0;
    }

    private static ProtocolPowerLine ReadLine(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload), ReadUInt64(payload[8..]), ReadUInt64(payload[16..]), ReadDouble(payload[24..]), payload[32] != 0);

    private static void WriteGenerator(Span<byte> payload, ProtocolGenerator value)
    {
        WriteUInt64(payload, value.GeneratorId);
        WriteUInt64(payload[8..], value.NodeId);
        WriteDouble(payload[16..], value.CapacityMegawatts);
        WriteDouble(payload[24..], value.OutputMegawatts);
        payload[32] = (byte)value.OperatingState;
    }

    private static ProtocolGenerator ReadGenerator(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload), ReadUInt64(payload[8..]), ReadDouble(payload[16..]), ReadDouble(payload[24..]), (ProtocolGeneratorOperatingState)payload[32]);

    private static void WriteLoad(Span<byte> payload, ProtocolPowerLoad value)
    {
        WriteUInt64(payload, value.LoadId);
        WriteUInt64(payload[8..], value.NodeId);
        WriteUInt64(payload[16..], value.BuildingId);
        WriteUInt64(payload[24..], value.EstablishmentId);
        WriteDouble(payload[32..], value.BaseDemandMegawatts);
        WriteDouble(payload[40..], value.DemandMegawatts);
        WriteDouble(payload[48..], value.ServedMegawatts);
        WriteDouble(payload[56..], value.UnservedMegawatts);
        payload[64] = (byte)value.SupplyState;
    }

    private static ProtocolPowerLoad ReadLoad(ReadOnlySpan<byte> payload) => new(
        ReadUInt64(payload), ReadUInt64(payload[8..]), ReadUInt64(payload[16..]), ReadUInt64(payload[24..]),
        ReadDouble(payload[32..]), ReadDouble(payload[40..]), ReadDouble(payload[48..]), ReadDouble(payload[56..]), (ProtocolPowerSupplyState)payload[64]);

    private static void ValidateStatistics(ProtocolPowerStatistics value, string parameterName)
    {
        if (!IsValidStatistics(value)) throw new ArgumentOutOfRangeException(parameterName, "Power statistics contain invalid values.");
    }

    private static bool IsValidStatistics(ProtocolPowerStatistics value) =>
        IsNonNegativeFinite(value.GenerationCapacityMegawatts)
        && IsNonNegativeFinite(value.GenerationOutputMegawatts)
        && IsNonNegativeFinite(value.DemandMegawatts)
        && IsNonNegativeFinite(value.ServedMegawatts)
        && IsNonNegativeFinite(value.UnservedMegawatts)
        && value.GenerationOutputMegawatts <= value.GenerationCapacityMegawatts + 1e-9
        && value.ServedMegawatts <= value.DemandMegawatts + 1e-9;

    private static void ValidateNode(ProtocolPowerNode value, string parameterName)
    {
        if (!IsValidNode(value)) throw new ArgumentOutOfRangeException(parameterName, "Power node entry contains invalid values.");
    }
    private static bool IsValidNode(ProtocolPowerNode value) => value.NodeId != 0 && Enum.IsDefined(value.Kind) && double.IsFinite(value.X) && double.IsFinite(value.Y) && double.IsFinite(value.Z);

    private static void ValidateLine(ProtocolPowerLine value, string parameterName)
    {
        if (!IsValidLine(value)) throw new ArgumentOutOfRangeException(parameterName, "Power line entry contains invalid values.");
    }
    private static bool IsValidLine(ProtocolPowerLine value) => value.LineId != 0 && value.FromNodeId != 0 && value.ToNodeId != 0 && value.FromNodeId != value.ToNodeId && double.IsFinite(value.CapacityMegawatts) && value.CapacityMegawatts > 0d;

    private static void ValidateGenerator(ProtocolGenerator value, string parameterName)
    {
        if (!IsValidGenerator(value)) throw new ArgumentOutOfRangeException(parameterName, "Generator entry contains invalid values.");
    }
    private static bool IsValidGenerator(ProtocolGenerator value) => value.GeneratorId != 0 && value.NodeId != 0 && double.IsFinite(value.CapacityMegawatts) && value.CapacityMegawatts > 0d && IsNonNegativeFinite(value.OutputMegawatts) && value.OutputMegawatts <= value.CapacityMegawatts + 1e-9 && Enum.IsDefined(value.OperatingState);

    private static void ValidateLoad(ProtocolPowerLoad value, string parameterName)
    {
        if (!IsValidLoad(value)) throw new ArgumentOutOfRangeException(parameterName, "Power load entry contains invalid values.");
    }
    private static bool IsValidLoad(ProtocolPowerLoad value) => value.LoadId != 0 && value.NodeId != 0 && (value.BuildingId != 0 || value.EstablishmentId != 0) && double.IsFinite(value.BaseDemandMegawatts) && value.BaseDemandMegawatts > 0d && IsNonNegativeFinite(value.DemandMegawatts) && IsNonNegativeFinite(value.ServedMegawatts) && IsNonNegativeFinite(value.UnservedMegawatts) && value.ServedMegawatts <= value.DemandMegawatts + 1e-9 && Enum.IsDefined(value.SupplyState);
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
