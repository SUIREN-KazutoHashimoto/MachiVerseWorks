using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class WaterSewerMessageMapper
{
    private const int MaximumDebugEntries = 512;

    public static WaterSewerSnapshotMessage Create(WaterSewerSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var statistics = snapshot.Statistics;
        var protocolStatistics = new ProtocolWaterSewerStatistics(
            checked((uint)statistics.WaterNodeCount),
            checked((uint)statistics.WaterPipeCount),
            checked((uint)statistics.SewerNodeCount),
            checked((uint)statistics.SewerPipeCount),
            checked((uint)statistics.WaterSourceCount),
            checked((uint)statistics.ReservoirCount),
            checked((uint)statistics.PumpCount),
            checked((uint)statistics.TreatmentPlantCount),
            checked((uint)statistics.ServicePointCount),
            checked((uint)statistics.WaterUnavailableCount),
            checked((uint)statistics.SewerUnavailableCount),
            checked((uint)statistics.SewerOverflowCount),
            statistics.WaterSupplyCapacityCubicMetersPerDay,
            statistics.WaterDemandCubicMetersPerDay,
            statistics.WaterServedCubicMetersPerDay,
            statistics.WastewaterGeneratedCubicMetersPerDay,
            statistics.WastewaterProcessedCubicMetersPerDay,
            statistics.WastewaterOverflowCubicMetersPerDay,
            statistics.TickCount);

        var nodes = snapshot.WaterNodes
            .Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Water,
                item.Id.Value,
                MapWaterNodeKind(item.Kind),
                item.Position.X,
                item.Position.Y,
                item.Position.Z))
            .Concat(snapshot.SewerNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Sewer,
                item.Id.Value,
                MapSewerNodeKind(item.Kind),
                item.Position.X,
                item.Position.Y,
                item.Position.Z)))
            .Take(MaximumDebugEntries)
            .ToArray();

        var waterNodeIds = nodes.Where(static item => item.NetworkKind == ProtocolUtilityNetworkKind.Water).Select(static item => item.NodeId).ToHashSet();
        var sewerNodeIds = nodes.Where(static item => item.NetworkKind == ProtocolUtilityNetworkKind.Sewer).Select(static item => item.NodeId).ToHashSet();
        var pipes = snapshot.WaterPipes
            .Where(item => waterNodeIds.Contains(item.FromNodeId.Value) && waterNodeIds.Contains(item.ToNodeId.Value))
            .Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Water,
                item.Id.Value,
                item.FromNodeId.Value,
                item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay,
                item.IsInService))
            .Concat(snapshot.SewerPipes
                .Where(item => sewerNodeIds.Contains(item.FromNodeId.Value) && sewerNodeIds.Contains(item.ToNodeId.Value))
                .Select(static item => new ProtocolUtilityPipe(
                    ProtocolUtilityNetworkKind.Sewer,
                    item.Id.Value,
                    item.FromNodeId.Value,
                    item.ToNodeId.Value,
                    item.CapacityCubicMetersPerDay,
                    item.IsInService)))
            .Take(MaximumDebugEntries)
            .ToArray();

        var facilities = snapshot.WaterSources
            .Select(static item => new ProtocolUtilityFacility(
                ProtocolUtilityFacilityKind.WaterSource,
                item.Id.Value,
                item.NodeId.Value,
                0,
                item.CapacityCubicMetersPerDay,
                item.OutputCubicMetersPerDay,
                (ProtocolUtilityOperatingState)item.OperatingState))
            .Concat(snapshot.Reservoirs.Select(static item => new ProtocolUtilityFacility(
                ProtocolUtilityFacilityKind.Reservoir,
                item.Id.Value,
                item.NodeId.Value,
                0,
                item.ReleaseCapacityCubicMetersPerDay,
                item.OutputCubicMetersPerDay,
                (ProtocolUtilityOperatingState)item.OperatingState)))
            .Concat(snapshot.Pumps.Select(static item => new ProtocolUtilityFacility(
                item.NetworkKind == PumpNetworkKind.Water ? ProtocolUtilityFacilityKind.WaterPump : ProtocolUtilityFacilityKind.SewerPump,
                item.Id.Value,
                item.NetworkKind == PumpNetworkKind.Water ? item.WaterNodeId!.Value.Value : item.SewerNodeId!.Value.Value,
                item.PowerLoadId?.Value ?? 0,
                item.CapacityCubicMetersPerDay,
                item.ThroughputCubicMetersPerDay,
                (ProtocolUtilityOperatingState)item.OperatingState)))
            .Concat(snapshot.TreatmentPlants.Select(static item => new ProtocolUtilityFacility(
                ProtocolUtilityFacilityKind.SewageTreatmentPlant,
                item.Id.Value,
                item.NodeId.Value,
                item.PowerLoadId?.Value ?? 0,
                item.CapacityCubicMetersPerDay,
                item.ProcessedCubicMetersPerDay,
                (ProtocolUtilityOperatingState)item.OperatingState)))
            .Take(MaximumDebugEntries)
            .ToArray();

        var servicePoints = snapshot.ServicePoints
            .OrderByDescending(static item => item.WaterState)
            .ThenByDescending(static item => item.SewerState)
            .ThenBy(static item => item.Id.Value)
            .Take(MaximumDebugEntries)
            .Select(static item => new ProtocolWaterSewerServicePoint(
                item.Id.Value,
                item.WaterNodeId.Value,
                item.SewerNodeId.Value,
                item.BuildingId?.Value ?? 0,
                item.EstablishmentId?.Value ?? 0,
                item.BaseWaterDemandCubicMetersPerDay,
                item.WastewaterReturnRatio,
                item.WaterDemandCubicMetersPerDay,
                item.WaterServedCubicMetersPerDay,
                item.WaterUnservedCubicMetersPerDay,
                (ProtocolWaterServiceState)item.WaterState,
                item.WastewaterGeneratedCubicMetersPerDay,
                item.WastewaterProcessedCubicMetersPerDay,
                item.WastewaterOverflowCubicMetersPerDay,
                (ProtocolSewerServiceState)item.SewerState))
            .ToArray();

        return new WaterSewerSnapshotMessage(
            protocolStatistics,
            Array.AsReadOnly(nodes),
            Array.AsReadOnly(pipes),
            Array.AsReadOnly(facilities),
            Array.AsReadOnly(servicePoints));
    }

    private static ProtocolUtilityNodeKind MapWaterNodeKind(WaterNodeKind kind) => kind switch
    {
        WaterNodeKind.Source => ProtocolUtilityNodeKind.Source,
        WaterNodeKind.Reservoir => ProtocolUtilityNodeKind.Reservoir,
        WaterNodeKind.Pump => ProtocolUtilityNodeKind.Pump,
        WaterNodeKind.Distribution => ProtocolUtilityNodeKind.Distribution,
        WaterNodeKind.Service => ProtocolUtilityNodeKind.Service,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Water node kind."),
    };

    private static ProtocolUtilityNodeKind MapSewerNodeKind(SewerNodeKind kind) => kind switch
    {
        SewerNodeKind.Service => ProtocolUtilityNodeKind.Service,
        SewerNodeKind.Collection => ProtocolUtilityNodeKind.Collection,
        SewerNodeKind.Pump => ProtocolUtilityNodeKind.Pump,
        SewerNodeKind.Treatment => ProtocolUtilityNodeKind.Treatment,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown Sewer node kind."),
    };
}
