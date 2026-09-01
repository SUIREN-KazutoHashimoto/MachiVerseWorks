namespace MachiVerseWorks.Protocol;

public enum ProtocolOpticalNodeKind : byte { BackboneGateway = 0, CentralOffice = 1, Distribution = 2, Access = 3, Endpoint = 4, DataCenter = 5 }
public enum ProtocolOpticalEquipmentKind : byte { Olt = 0, Onu = 1, Splitter = 2, Switch = 3, Router = 4 }
public enum ProtocolOpticalDemandKind : byte { Building = 0, Office = 1, DataCenter = 2, RadioBackhaul = 3 }
public enum ProtocolOpticalQualityState : byte { Healthy = 0, Congested = 1, Degraded = 2, Unavailable = 3 }

public readonly record struct ProtocolOpticalStatistics(
    uint NodeCount, uint FiberCableCount, uint EquipmentCount, uint BackhaulCount, uint DemandCount,
    uint ConnectedDemandCount, uint CongestedDemandCount, uint DegradedDemandCount, uint UnavailableDemandCount,
    double BackhaulCapacityGigabitsPerSecond, double DemandGigabitsPerSecond, double AllocatedGigabitsPerSecond,
    double PeakFiberUtilization, ulong TickCount);

public readonly record struct ProtocolOpticalNode(ulong NodeId, ProtocolOpticalNodeKind Kind, double X, double Y, double Z);
public readonly record struct ProtocolFiberCable(ulong CableId, ulong FromNodeId, ulong ToNodeId, double CapacityGigabitsPerSecond, double LoadGigabitsPerSecond, double Utilization, bool IsInService, bool IsCongested);
public readonly record struct ProtocolOpticalEquipment(ulong EquipmentId, ulong NodeId, ProtocolOpticalEquipmentKind Kind, ulong BuildingId, ulong EstablishmentId, double CapacityGigabitsPerSecond, bool RequiresPower, bool IsInService, bool IsPowered, bool IsOperational);
public readonly record struct ProtocolOpticalBackhaul(ulong BackhaulId, ulong NodeId, double CapacityGigabitsPerSecond, double AllocatedGigabitsPerSecond, double Utilization, bool IsInService, bool IsOperational);
public readonly record struct ProtocolOpticalDemand(ulong DemandId, ulong NodeId, ProtocolOpticalDemandKind Kind, ulong BuildingId, ulong EstablishmentId, double BaseDemandGigabitsPerSecond, double DemandGigabitsPerSecond, double AllocatedGigabitsPerSecond, ProtocolOpticalQualityState QualityState, ulong BackhaulId);

public sealed record OpticalSnapshotMessage(
    ProtocolOpticalStatistics Statistics,
    IReadOnlyList<ProtocolOpticalNode> Nodes,
    IReadOnlyList<ProtocolFiberCable> FiberCables,
    IReadOnlyList<ProtocolOpticalEquipment> Equipment,
    IReadOnlyList<ProtocolOpticalBackhaul> Backhauls,
    IReadOnlyList<ProtocolOpticalDemand> Demands) : IProtocolMessage
{
    public MessageType Type => MessageType.OpticalSnapshot;
}
