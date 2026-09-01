using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class PowerMessageMapper
{
    private const int MaximumDebugEntries = 512;

    public static PowerSnapshotMessage Create(PowerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var statistics = snapshot.Statistics;
        var protocolStatistics = new ProtocolPowerStatistics(
            checked((uint)statistics.NodeCount),
            checked((uint)statistics.LineCount),
            checked((uint)statistics.GeneratorCount),
            checked((uint)statistics.LoadCount),
            checked((uint)statistics.OutageLoadCount),
            statistics.GenerationCapacityMegawatts,
            statistics.GenerationOutputMegawatts,
            statistics.DemandMegawatts,
            statistics.ServedMegawatts,
            statistics.UnservedMegawatts,
            statistics.TickCount);

        var nodes = snapshot.Nodes.Take(MaximumDebugEntries).Select(static item => new ProtocolPowerNode(
            item.Id.Value, (ProtocolPowerNodeKind)item.Kind, item.Position.X, item.Position.Y, item.Position.Z)).ToArray();
        var visibleNodeIds = nodes.Select(static item => item.NodeId).ToHashSet();
        var lines = snapshot.Lines
            .Where(item => visibleNodeIds.Contains(item.FromNodeId.Value) && visibleNodeIds.Contains(item.ToNodeId.Value))
            .Take(MaximumDebugEntries)
            .Select(static item => new ProtocolPowerLine(item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value, item.CapacityMegawatts, item.IsInService)).ToArray();
        var generators = snapshot.Generators.Take(MaximumDebugEntries).Select(static item => new ProtocolGenerator(
            item.Id.Value, item.NodeId.Value, item.CapacityMegawatts, item.OutputMegawatts, (ProtocolGeneratorOperatingState)item.OperatingState)).ToArray();
        var loads = snapshot.Loads
            .OrderByDescending(static item => item.SupplyState)
            .ThenBy(static item => item.Id.Value)
            .Take(MaximumDebugEntries)
            .Select(static item => new ProtocolPowerLoad(
                item.Id.Value,
                item.NodeId.Value,
                item.BuildingId?.Value ?? 0UL,
                item.EstablishmentId?.Value ?? 0UL,
                item.BaseDemandMegawatts,
                item.DemandMegawatts,
                item.ServedMegawatts,
                item.UnservedMegawatts,
                (ProtocolPowerSupplyState)item.SupplyState)).ToArray();
        return new PowerSnapshotMessage(protocolStatistics, Array.AsReadOnly(nodes), Array.AsReadOnly(lines), Array.AsReadOnly(generators), Array.AsReadOnly(loads));
    }
}
