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

        var servicePointCandidates = SelectServicePointCandidates(snapshot.ServicePoints);
        var requiredWaterNodeIds = servicePointCandidates.Select(static item => item.WaterNodeId.Value).ToHashSet();
        var requiredSewerNodeIds = servicePointCandidates.Select(static item => item.SewerNodeId.Value).ToHashSet();

        var selectedWaterNodes = new List<WaterNodeSnapshot>(MaximumDebugEntries);
        foreach (var item in snapshot.WaterNodes)
            if (requiredWaterNodeIds.Contains(item.Id.Value)) selectedWaterNodes.Add(item);
        var selectedSewerNodes = new List<SewerNodeSnapshot>(MaximumDebugEntries);
        foreach (var item in snapshot.SewerNodes)
            if (requiredSewerNodeIds.Contains(item.Id.Value)) selectedSewerNodes.Add(item);

        var remainingNodeBudget = Math.Max(0, MaximumDebugEntries - selectedWaterNodes.Count - selectedSewerNodes.Count);
        var availableWaterNodes = Math.Max(0, snapshot.WaterNodes.Count - selectedWaterNodes.Count);
        var availableSewerNodes = Math.Max(0, snapshot.SewerNodes.Count - selectedSewerNodes.Count);
        var (extraWaterBudget, extraSewerBudget) = SplitBudget(availableWaterNodes, availableSewerNodes, remainingNodeBudget);
        foreach (var item in snapshot.WaterNodes)
        {
            if (extraWaterBudget == 0) break;
            if (requiredWaterNodeIds.Contains(item.Id.Value)) continue;
            selectedWaterNodes.Add(item);
            extraWaterBudget--;
        }
        foreach (var item in snapshot.SewerNodes)
        {
            if (extraSewerBudget == 0) break;
            if (requiredSewerNodeIds.Contains(item.Id.Value)) continue;
            selectedSewerNodes.Add(item);
            extraSewerBudget--;
        }

        var nodes = selectedWaterNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, MapWaterNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z))
            .Concat(selectedSewerNodes.Select(static item => new ProtocolUtilityNode(
                ProtocolUtilityNetworkKind.Sewer, item.Id.Value, MapSewerNodeKind(item.Kind),
                item.Position.X, item.Position.Y, item.Position.Z)))
            .ToArray();

        var waterNodeIds = selectedWaterNodes.Select(static item => item.Id.Value).ToHashSet();
        var sewerNodeIds = selectedSewerNodes.Select(static item => item.Id.Value).ToHashSet();
        bool WaterPipeSelected(WaterPipeSnapshot item) => waterNodeIds.Contains(item.FromNodeId.Value) && waterNodeIds.Contains(item.ToNodeId.Value);
        bool SewerPipeSelected(SewerPipeSnapshot item) => sewerNodeIds.Contains(item.FromNodeId.Value) && sewerNodeIds.Contains(item.ToNodeId.Value);
        var waterPipeCount = snapshot.WaterPipes.Count(WaterPipeSelected);
        var sewerPipeCount = snapshot.SewerPipes.Count(SewerPipeSelected);
        var (waterPipeBudget, sewerPipeBudget) = SplitBudget(waterPipeCount, sewerPipeCount);
        var pipes = snapshot.WaterPipes.Where(WaterPipeSelected).Take(waterPipeBudget).Select(static item => new ProtocolUtilityPipe(
                ProtocolUtilityNetworkKind.Water, item.Id.Value, item.FromNodeId.Value, item.ToNodeId.Value,
                item.CapacityCubicMetersPerDay, item.IsInService))
            .Concat(snapshot.SewerPipes.Where(SewerPipeSelected).Take(sewerPipeBudget).Select(static item => new ProtocolUtilityPipe(
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

    private static List<WaterSewerServicePointSnapshot> SelectServicePointCandidates(IReadOnlyList<WaterSewerServicePointSnapshot> servicePoints)
    {
        var selected = new List<WaterSewerServicePointSnapshot>(MaximumDebugEntries);
        var waterNodeIds = new HashSet<ulong>();
        var sewerNodeIds = new HashSet<ulong>();
        for (var priority = 6; priority >= 0 && selected.Count < MaximumDebugEntries; priority--)
        {
            foreach (var item in servicePoints)
            {
                if (GetServicePointPriority(item) != priority) continue;
                var addedWater = waterNodeIds.Add(item.WaterNodeId.Value);
                var addedSewer = sewerNodeIds.Add(item.SewerNodeId.Value);
                if (waterNodeIds.Count + sewerNodeIds.Count > MaximumDebugEntries)
                {
                    if (addedWater) waterNodeIds.Remove(item.WaterNodeId.Value);
                    if (addedSewer) sewerNodeIds.Remove(item.SewerNodeId.Value);
                    continue;
                }
                selected.Add(item);
                if (selected.Count == MaximumDebugEntries) break;
            }
        }
        return selected;
    }

    private static int GetServicePointPriority(WaterSewerServicePointSnapshot item) =>
        item.SewerState == SewerServiceState.Overflow ? 6
        : item.WaterState == WaterServiceState.Unavailable ? 5
        : item.SewerState == SewerServiceState.Unavailable ? 4
        : item.WaterState == WaterServiceState.Constrained ? 3
        : item.SewerState == SewerServiceState.Constrained ? 2
        : 0;

    private static (int First, int Second) SplitBudget(int firstCount, int secondCount, int totalBudget = MaximumDebugEntries)
    {
        var first = Math.Min(firstCount, totalBudget / 2);
        var second = Math.Min(secondCount, totalBudget / 2);
        var remaining = totalBudget - first - second;
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
