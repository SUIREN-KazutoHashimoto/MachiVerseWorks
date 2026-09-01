using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class OpticalMessageMapper
{
    private const int MaximumDebugEntries = 512;

    public static OpticalSnapshotMessage Create(OpticalSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var s = snapshot.Statistics;
        var statistics = new ProtocolOpticalStatistics(
            checked((uint)s.NodeCount), checked((uint)s.FiberCableCount), checked((uint)s.EquipmentCount), checked((uint)s.BackhaulCount), checked((uint)s.DemandCount),
            checked((uint)s.ConnectedDemandCount), checked((uint)s.CongestedDemandCount), checked((uint)s.DegradedDemandCount), checked((uint)s.UnavailableDemandCount),
            s.BackhaulCapacityGigabitsPerSecond, s.DemandGigabitsPerSecond, s.AllocatedGigabitsPerSecond, s.PeakFiberUtilization, s.TickCount);
        var nodes = snapshot.Nodes.Take(MaximumDebugEntries).Select(static item => new ProtocolOpticalNode(item.Id.Value, (ProtocolOpticalNodeKind)item.Kind, item.Position.X, item.Position.Y, item.Position.Z)).ToArray();
        var nodeIds = nodes.Select(static item => item.NodeId).ToHashSet();
        var cables = snapshot.FiberCables.Where(item => nodeIds.Contains(item.FromNodeId.Value) && nodeIds.Contains(item.ToNodeId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolFiberCable(item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value, item.CapacityGigabitsPerSecond, item.LoadGigabitsPerSecond, item.Utilization, item.IsInService, item.IsCongested)).ToArray();
        var cableUtilization = snapshot.FiberCables.ToDictionary(static item => item.Id, static item => item.Utilization);
        var equipment = snapshot.Equipment.Where(item => nodeIds.Contains(item.NodeId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolOpticalEquipment(item.Id.Value, item.NodeId.Value, (ProtocolOpticalEquipmentKind)item.Kind, item.BuildingId?.Value ?? 0, item.EstablishmentId?.Value ?? 0, item.CapacityGigabitsPerSecond, item.RequiresPower, item.IsInService, item.IsPowered, item.IsOperational)).ToArray();
        var backhauls = snapshot.Backhauls.Where(item => nodeIds.Contains(item.NodeId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolOpticalBackhaul(item.Id.Value, item.NodeId.Value, item.CapacityGigabitsPerSecond, item.AllocatedGigabitsPerSecond, item.Utilization, item.IsInService, item.IsOperational)).ToArray();
        var demands = snapshot.Demands.Where(item => nodeIds.Contains(item.NodeId.Value)).Take(MaximumDebugEntries)
            .Select(item => new ProtocolOpticalDemand(
                item.Id.Value, item.NodeId.Value, (ProtocolOpticalDemandKind)item.Kind, item.BuildingId?.Value ?? 0, item.EstablishmentId?.Value ?? 0,
                item.BaseDemandGigabitsPerSecond, item.DemandGigabitsPerSecond, item.AllocatedGigabitsPerSecond,
                (ProtocolOpticalQualityState)item.QualityState, item.BackhaulId?.Value ?? 0, EstimateLatencyMilliseconds(item, cableUtilization)))
            .ToArray();
        return new OpticalSnapshotMessage(statistics, Array.AsReadOnly(nodes), Array.AsReadOnly(cables), Array.AsReadOnly(equipment), Array.AsReadOnly(backhauls), Array.AsReadOnly(demands));
    }

    private static double EstimateLatencyMilliseconds(
        OpticalDemandSnapshot demand,
        IReadOnlyDictionary<FiberCableId, double> cableUtilization)
    {
        if (demand.AllocatedGigabitsPerSecond <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond) return 0d;
        var peakUtilization = 0d;
        foreach (var cableId in demand.RouteCableIds)
            peakUtilization = Math.Max(peakUtilization, cableUtilization.GetValueOrDefault(cableId));
        return 0.5d + (demand.RouteCableIds.Count * 0.25d) + (peakUtilization * peakUtilization * 5d);
    }
}
