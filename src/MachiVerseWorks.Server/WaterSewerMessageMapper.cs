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

        var servicePointCandidates = snapshot.ServicePoints
            .OrderByDescending(static item => item.WaterState)
            .ThenByDescending(static item => item.SewerState)
            .ThenBy(static item => item.Id.Value)
            .Take(MaximumDebugEntries)
            .ToArray();
        var requiredWaterNodeIds = servicePointCandidates.Select(static item => item.WaterNodeId.Value).ToHashSet();
        var requiredSewerNodeIds = servicePointCandidates.Select(static item => item.SewerNodeId.Value).ToHashSet();
        var (waterNodeBudget, sewerNodeBudget) = SplitBudget(snapshot.WaterNodes.Count, snapshot.SewerNodes.Count);
        var selectedWaterNodes = snapshot.WaterNodes
            .OrderBy(item => requiredWaterNodeIds.Contains(item.Id.Value) ? 0 : 1)
            .ThenBy(static item => item.Id.Value)
            .Take(waterNodeBudget)
            .ToArray();
        var selectedSewerNodes = snapshot.SewerNodes
            .OrderBy(item => requiredSewerNodeIds.Contains(item.Id.Value) ? 0 : 1)
            .ThenBy(static item => item.Id.Value)
            .Take(sewerNodeBudget)
            .ToArray();
        var nodes = selectedWaterNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, MapWaterNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z))
            .Concat(selectedSewerNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, MapSewerNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z)))
            .ToArray();

        var waterNodeIds = selectedWaterNodes.Select(static item => item.Id.Value).ToHashSet();
        var sewerNodeIds = selectedSewerNodes.Select(static item => item.Id.Value).ToHashSet();
        var waterPipeCandidates = snapshot.WaterPipes
            .Where(item => waterNodeIds.Contains(item.FromNodeId.Value) && waterNodeIds.Contains(item.ToNodeId.Value))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        var sewerPipeCandidates = snapshot.SewerPipes
            .Where(item => sewerNodeIds.Contains(item.FromNodeId.Value) && sewerNodeIds.Contains(item.ToNodeId.Value))
            .OrderBy(static item => item.Id.Value)
            .ToArray();
        var (waterPipeBudget, sewerPipeBudget) = SplitBudget(waterPipeCandidates.Length, sewerPipeCandidates.Length);
        var pipes = waterPipeCandidates.Take(waterPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService))
            .Concat(sewerPipeCandidates.Take(sewerPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService)))
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

        var servicePoints = servicePointCandidates
            .Where(item => waterNodeIds.Contains(item.WaterNodeId.Value) && sewerNodeIds.Contains(item.SewerNodeId.Value))
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

    private static (int First, int Second) SplitBudget(int firstCount, int secondCount)
    {
        var first = Math.Min(firstCount, MaximumDebugEntries / 2);
        var second = Math.Min(secondCount, MaximumDebugEntries / 2);
        var remaining = MaximumDebugEntries - first - second;
        var firstExtra = Math.Min(Math.Max(0, firstCount - first), remaining);
        first += firstExtra;
        remaining -= firstExtra;
        second += Math.Min(Math.Max(0, secondCount - second), remaining);
        return (first, second);
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
