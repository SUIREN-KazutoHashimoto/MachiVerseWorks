using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class GasMessageMapper
{
    private const int MaximumDebugEntries = 512;

    public static GasSnapshotMessage Create(GasSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var s = snapshot.Statistics;
        var statistics = new ProtocolGasStatistics(
            checked((uint)s.NodeCount), checked((uint)s.PipelineCount), checked((uint)s.SourceCount), checked((uint)s.ImportTerminalCount), checked((uint)s.StorageCount),
            checked((uint)s.ServicePointCount), checked((uint)s.PipedServicePointCount), checked((uint)s.DeliveredServicePointCount), checked((uint)s.UnavailableServicePointCount),
            s.SupplyCapacityCubicMetersPerDay, s.DemandCubicMetersPerDay, s.ServedCubicMetersPerDay, s.UnservedCubicMetersPerDay, s.StoredCubicMeters, s.TickCount);
        var nodes = snapshot.Nodes.Take(MaximumDebugEntries).Select(static item => new ProtocolGasNode(item.Id.Value, (ProtocolGasNodeKind)item.Kind, item.Position.X, item.Position.Y, item.Position.Z)).ToArray();
        var nodeIds = nodes.Select(static item => item.NodeId).ToHashSet();
        var pipelines = snapshot.Pipelines.Where(item => nodeIds.Contains(item.FromNodeId.Value) && nodeIds.Contains(item.ToNodeId.Value)).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolGasPipeline(item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value, item.CapacityCubicMetersPerDay, item.IsInService)).ToArray();
        var facilities = snapshot.Sources.Select(static item => new ProtocolGasFacility(ProtocolGasFacilityKind.Source, item.Id.Value, item.NodeId.Value, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, 0d, (ProtocolGasOperatingState)item.OperatingState))
            .Concat(snapshot.ImportTerminals.Select(static item => new ProtocolGasFacility(ProtocolGasFacilityKind.ImportTerminal, item.Id.Value, item.NodeId.Value, item.CapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, 0d, (ProtocolGasOperatingState)item.OperatingState)))
            .Concat(snapshot.Storages.Select(static item => new ProtocolGasFacility(ProtocolGasFacilityKind.Storage, item.Id.Value, item.NodeId.Value, item.ReleaseCapacityCubicMetersPerDay, item.OutputCubicMetersPerDay, item.StoredCubicMeters, (ProtocolGasOperatingState)item.OperatingState)))
            .Take(MaximumDebugEntries).ToArray();
        var servicePoints = snapshot.ServicePoints.OrderByDescending(static item => item.ServiceState).ThenBy(static item => item.Id.Value).Take(MaximumDebugEntries)
            .Select(static item => new ProtocolGasServicePoint(item.Id.Value, item.NodeId?.Value ?? 0, item.BuildingId?.Value ?? 0, item.EstablishmentId?.Value ?? 0, (ProtocolGasDeliveryMode)item.DeliveryMode, item.CommodityId?.Value ?? 0, item.BaseDemandCubicMetersPerDay, item.DemandCubicMetersPerDay, item.ServedCubicMetersPerDay, item.UnservedCubicMetersPerDay, (ProtocolGasServiceState)item.ServiceState)).ToArray();
        return new GasSnapshotMessage(statistics, Array.AsReadOnly(nodes), Array.AsReadOnly(pipelines), Array.AsReadOnly(facilities), Array.AsReadOnly(servicePoints));
    }
}
