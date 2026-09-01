using System.Buffers.Binary;

namespace MachiVerseWorks.Protocol;

public static class OpticalProtocolCodec
{
    private const int StatisticsLength = 76;
    private const int FixedLength = 86;
    private const int NodeLength = 33;
    private const int CableLength = 50;
    private const int EquipmentLength = 45;
    private const int BackhaulLength = 42;
    private const int DemandLength = 74;

    public static byte[] Serialize(OpticalSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsOptical) throw new ArgumentOutOfRangeException(nameof(version), version, "Optical messages require Protocol 2.15 or newer.");
        ValidateCollections(message);
        var payloadLength = checked(FixedLength + message.Nodes.Count * NodeLength + message.FiberCables.Count * CableLength + message.Equipment.Count * EquipmentLength + message.Backhauls.Count * BackhaulLength + message.Demands.Count * DemandLength);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Optical snapshot exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.OpticalSnapshot, checked((uint)payloadLength)));
        var p = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteStatistics(p, message.Statistics);
        WriteUInt16(p[76..], checked((ushort)message.Nodes.Count));
        WriteUInt16(p[78..], checked((ushort)message.FiberCables.Count));
        WriteUInt16(p[80..], checked((ushort)message.Equipment.Count));
        WriteUInt16(p[82..], checked((ushort)message.Backhauls.Count));
        WriteUInt16(p[84..], checked((ushort)message.Demands.Count));
        var offset = FixedLength;
        foreach (var v in message.Nodes) { WriteNode(p.Slice(offset, NodeLength), v); offset += NodeLength; }
        foreach (var v in message.FiberCables) { WriteCable(p.Slice(offset, CableLength), v); offset += CableLength; }
        foreach (var v in message.Equipment) { WriteEquipment(p.Slice(offset, EquipmentLength), v); offset += EquipmentLength; }
        foreach (var v in message.Backhauls) { WriteBackhaul(p.Slice(offset, BackhaulLength), v); offset += BackhaulLength; }
        foreach (var v in message.Demands) { WriteDemand(p.Slice(offset, DemandLength), v); offset += DemandLength; }
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.OpticalSnapshot) { error = ProtocolDecodeError.UnknownMessageType; return false; }
        if (!header.Version.SupportsOptical) { error = ProtocolDecodeError.InvalidPayload; return false; }
        var p = frame[ProtocolFrameHeader.Size..];
        if (p.Length < FixedLength) { error = ProtocolDecodeError.InvalidPayload; return false; }
        var nc = ReadUInt16(p[76..]); var cc = ReadUInt16(p[78..]); var ec = ReadUInt16(p[80..]); var bc = ReadUInt16(p[82..]); var dc = ReadUInt16(p[84..]);
        int expected;
        try { expected = checked(FixedLength + nc * NodeLength + cc * CableLength + ec * EquipmentLength + bc * BackhaulLength + dc * DemandLength); }
        catch (OverflowException) { error = ProtocolDecodeError.InvalidPayload; return false; }
        if (p.Length != expected) { error = ProtocolDecodeError.InvalidPayload; return false; }
        var offset = FixedLength;
        var nodes = new ProtocolOpticalNode[nc]; for (var i = 0; i < nodes.Length; i++) { nodes[i] = ReadNode(p.Slice(offset, NodeLength)); offset += NodeLength; }
        var cables = new ProtocolFiberCable[cc]; for (var i = 0; i < cables.Length; i++) { cables[i] = ReadCable(p.Slice(offset, CableLength)); offset += CableLength; }
        var equipment = new ProtocolOpticalEquipment[ec]; for (var i = 0; i < equipment.Length; i++) { equipment[i] = ReadEquipment(p.Slice(offset, EquipmentLength)); offset += EquipmentLength; }
        var backhauls = new ProtocolOpticalBackhaul[bc]; for (var i = 0; i < backhauls.Length; i++) { backhauls[i] = ReadBackhaul(p.Slice(offset, BackhaulLength)); offset += BackhaulLength; }
        var demands = new ProtocolOpticalDemand[dc]; for (var i = 0; i < demands.Length; i++) { demands[i] = ReadDemand(p.Slice(offset, DemandLength)); offset += DemandLength; }
        var message = new OpticalSnapshotMessage(ReadStatistics(p), Array.AsReadOnly(nodes), Array.AsReadOnly(cables), Array.AsReadOnly(equipment), Array.AsReadOnly(backhauls), Array.AsReadOnly(demands));
        if (!IsValid(message)) { error = ProtocolDecodeError.InvalidPayload; return false; }
        envelope = new ProtocolEnvelope(header.Version, message); error = ProtocolDecodeError.None; return true;
    }

    private static void ValidateCollections(OpticalSnapshotMessage m)
    {
        ArgumentNullException.ThrowIfNull(m.Nodes); ArgumentNullException.ThrowIfNull(m.FiberCables); ArgumentNullException.ThrowIfNull(m.Equipment); ArgumentNullException.ThrowIfNull(m.Backhauls); ArgumentNullException.ThrowIfNull(m.Demands);
        if (m.Nodes.Count > ushort.MaxValue || m.FiberCables.Count > ushort.MaxValue || m.Equipment.Count > ushort.MaxValue || m.Backhauls.Count > ushort.MaxValue || m.Demands.Count > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(m));
        if (!IsValid(m)) throw new ArgumentOutOfRangeException(nameof(m), "Optical snapshot contains invalid values.");
    }

    private static bool IsValid(OpticalSnapshotMessage m)
    {
        var s = m.Statistics;
        if (!NonNegative(s.BackhaulCapacityGigabitsPerSecond) || !NonNegative(s.DemandGigabitsPerSecond) || !NonNegative(s.AllocatedGigabitsPerSecond) || !NonNegative(s.PeakFiberUtilization)) return false;
        foreach (var v in m.Nodes) if (v.NodeId == 0 || !Enum.IsDefined(v.Kind) || !Finite(v.X) || !Finite(v.Y) || !Finite(v.Z)) return false;
        foreach (var v in m.FiberCables) if (v.CableId == 0 || v.FromNodeId == 0 || v.ToNodeId == 0 || v.FromNodeId == v.ToNodeId || !Positive(v.CapacityGigabitsPerSecond) || !NonNegative(v.LoadGigabitsPerSecond) || !NonNegative(v.Utilization) || v.Utilization > 1d + 1e-9) return false;
        foreach (var v in m.Equipment) if (v.EquipmentId == 0 || v.NodeId == 0 || !Enum.IsDefined(v.Kind) || !Positive(v.CapacityGigabitsPerSecond)) return false;
        foreach (var v in m.Backhauls) if (v.BackhaulId == 0 || v.NodeId == 0 || !Positive(v.CapacityGigabitsPerSecond) || !NonNegative(v.AllocatedGigabitsPerSecond) || !NonNegative(v.Utilization) || v.Utilization > 1d + 1e-9) return false;
        foreach (var v in m.Demands) if (v.DemandId == 0 || v.NodeId == 0 || !Enum.IsDefined(v.Kind) || !Enum.IsDefined(v.QualityState) || !Positive(v.BaseDemandGigabitsPerSecond) || !NonNegative(v.DemandGigabitsPerSecond) || !NonNegative(v.AllocatedGigabitsPerSecond)) return false;
        return true;
    }

    private static void WriteStatistics(Span<byte> p, ProtocolOpticalStatistics v) { WriteUInt32(p,v.NodeCount); WriteUInt32(p[4..],v.FiberCableCount); WriteUInt32(p[8..],v.EquipmentCount); WriteUInt32(p[12..],v.BackhaulCount); WriteUInt32(p[16..],v.DemandCount); WriteUInt32(p[20..],v.ConnectedDemandCount); WriteUInt32(p[24..],v.CongestedDemandCount); WriteUInt32(p[28..],v.DegradedDemandCount); WriteUInt32(p[32..],v.UnavailableDemandCount); WriteDouble(p[36..],v.BackhaulCapacityGigabitsPerSecond); WriteDouble(p[44..],v.DemandGigabitsPerSecond); WriteDouble(p[52..],v.AllocatedGigabitsPerSecond); WriteDouble(p[60..],v.PeakFiberUtilization); WriteUInt64(p[68..],v.TickCount); }
    private static ProtocolOpticalStatistics ReadStatistics(ReadOnlySpan<byte> p) => new(ReadUInt32(p),ReadUInt32(p[4..]),ReadUInt32(p[8..]),ReadUInt32(p[12..]),ReadUInt32(p[16..]),ReadUInt32(p[20..]),ReadUInt32(p[24..]),ReadUInt32(p[28..]),ReadUInt32(p[32..]),ReadDouble(p[36..]),ReadDouble(p[44..]),ReadDouble(p[52..]),ReadDouble(p[60..]),ReadUInt64(p[68..]));
    private static void WriteNode(Span<byte> p, ProtocolOpticalNode v) { WriteUInt64(p,v.NodeId); p[8]=(byte)v.Kind; WriteDouble(p[9..],v.X); WriteDouble(p[17..],v.Y); WriteDouble(p[25..],v.Z); }
    private static ProtocolOpticalNode ReadNode(ReadOnlySpan<byte> p)=>new(ReadUInt64(p),(ProtocolOpticalNodeKind)p[8],ReadDouble(p[9..]),ReadDouble(p[17..]),ReadDouble(p[25..]));
    private static void WriteCable(Span<byte> p, ProtocolFiberCable v) { WriteUInt64(p,v.CableId); WriteUInt64(p[8..],v.FromNodeId); WriteUInt64(p[16..],v.ToNodeId); WriteDouble(p[24..],v.CapacityGigabitsPerSecond); WriteDouble(p[32..],v.LoadGigabitsPerSecond); WriteDouble(p[40..],v.Utilization); p[48]=Bool(v.IsInService); p[49]=Bool(v.IsCongested); }
    private static ProtocolFiberCable ReadCable(ReadOnlySpan<byte> p)=>new(ReadUInt64(p),ReadUInt64(p[8..]),ReadUInt64(p[16..]),ReadDouble(p[24..]),ReadDouble(p[32..]),ReadDouble(p[40..]),p[48]!=0,p[49]!=0);
    private static void WriteEquipment(Span<byte> p, ProtocolOpticalEquipment v) { WriteUInt64(p,v.EquipmentId); WriteUInt64(p[8..],v.NodeId); p[16]=(byte)v.Kind; WriteUInt64(p[17..],v.BuildingId); WriteUInt64(p[25..],v.EstablishmentId); WriteDouble(p[33..],v.CapacityGigabitsPerSecond); p[41]=Bool(v.RequiresPower); p[42]=Bool(v.IsInService); p[43]=Bool(v.IsPowered); p[44]=Bool(v.IsOperational); }
    private static ProtocolOpticalEquipment ReadEquipment(ReadOnlySpan<byte> p)=>new(ReadUInt64(p),ReadUInt64(p[8..]),(ProtocolOpticalEquipmentKind)p[16],ReadUInt64(p[17..]),ReadUInt64(p[25..]),ReadDouble(p[33..]),p[41]!=0,p[42]!=0,p[43]!=0,p[44]!=0);
    private static void WriteBackhaul(Span<byte> p, ProtocolOpticalBackhaul v) { WriteUInt64(p,v.BackhaulId); WriteUInt64(p[8..],v.NodeId); WriteDouble(p[16..],v.CapacityGigabitsPerSecond); WriteDouble(p[24..],v.AllocatedGigabitsPerSecond); WriteDouble(p[32..],v.Utilization); p[40]=Bool(v.IsInService); p[41]=Bool(v.IsOperational); }
    private static ProtocolOpticalBackhaul ReadBackhaul(ReadOnlySpan<byte> p)=>new(ReadUInt64(p),ReadUInt64(p[8..]),ReadDouble(p[16..]),ReadDouble(p[24..]),ReadDouble(p[32..]),p[40]!=0,p[41]!=0);
    private static void WriteDemand(Span<byte> p, ProtocolOpticalDemand v) { WriteUInt64(p,v.DemandId); WriteUInt64(p[8..],v.NodeId); p[16]=(byte)v.Kind; WriteUInt64(p[17..],v.BuildingId); WriteUInt64(p[25..],v.EstablishmentId); WriteDouble(p[33..],v.BaseDemandGigabitsPerSecond); WriteDouble(p[41..],v.DemandGigabitsPerSecond); WriteDouble(p[49..],v.AllocatedGigabitsPerSecond); p[57]=(byte)v.QualityState; WriteUInt64(p[58..],v.BackhaulId); }
    private static ProtocolOpticalDemand ReadDemand(ReadOnlySpan<byte> p)=>new(ReadUInt64(p),ReadUInt64(p[8..]),(ProtocolOpticalDemandKind)p[16],ReadUInt64(p[17..]),ReadUInt64(p[25..]),ReadDouble(p[33..]),ReadDouble(p[41..]),ReadDouble(p[49..]),(ProtocolOpticalQualityState)p[57],ReadUInt64(p[58..]));
    private static byte Bool(bool value)=>value?(byte)1:(byte)0; private static bool Finite(double v)=>double.IsFinite(v); private static bool Positive(double v)=>double.IsFinite(v)&&v>0d; private static bool NonNegative(double v)=>double.IsFinite(v)&&v>=0d;
    private static void WriteUInt16(Span<byte> p, ushort v)=>BinaryPrimitives.WriteUInt16LittleEndian(p,v); private static ushort ReadUInt16(ReadOnlySpan<byte> p)=>BinaryPrimitives.ReadUInt16LittleEndian(p); private static void WriteUInt32(Span<byte> p,uint v)=>BinaryPrimitives.WriteUInt32LittleEndian(p,v); private static uint ReadUInt32(ReadOnlySpan<byte> p)=>BinaryPrimitives.ReadUInt32LittleEndian(p); private static void WriteUInt64(Span<byte> p,ulong v)=>BinaryPrimitives.WriteUInt64LittleEndian(p,v); private static ulong ReadUInt64(ReadOnlySpan<byte> p)=>BinaryPrimitives.ReadUInt64LittleEndian(p); private static void WriteDouble(Span<byte> p,double v)=>WriteUInt64(p,unchecked((ulong)BitConverter.DoubleToInt64Bits(v))); private static double ReadDouble(ReadOnlySpan<byte> p)=>BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64(p)));
}
