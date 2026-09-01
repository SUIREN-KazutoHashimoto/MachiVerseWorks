namespace MachiVerseWorks.Simulation;

public sealed class CapacityOpticalRoutingSolver : IOpticalRoutingSolver
{
    public OpticalRoutingResult Solve(OpticalRoutingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Nodes);
        ArgumentNullException.ThrowIfNull(request.FiberCables);
        ArgumentNullException.ThrowIfNull(request.Endpoints);
        ArgumentNullException.ThrowIfNull(request.Backhauls);
        ArgumentNullException.ThrowIfNull(request.Demands);

        var nodes = request.Nodes.ToDictionary(static item => item.Id);
        var adjacency = request.Nodes.ToDictionary(static item => item.Id, static _ => new List<OpticalRoutingCable>());
        var residualCable = new Dictionary<FiberCableId, double>();
        foreach (var cable in request.FiberCables.OrderBy(static item => item.Id.Value))
        {
            if (!nodes.ContainsKey(cable.FromNodeId) || !nodes.ContainsKey(cable.ToNodeId) || cable.FromNodeId == cable.ToNodeId)
                throw new ArgumentException("Optical routing cable references invalid nodes.", nameof(request));
            ValidatePositiveFinite(cable.CapacityGigabitsPerSecond, nameof(request));
            if (!residualCable.TryAdd(cable.Id, cable.IsInService ? cable.CapacityGigabitsPerSecond : 0d))
                throw new ArgumentException($"Duplicate FiberCable ID {cable.Id.Value}.", nameof(request));
            adjacency[cable.FromNodeId].Add(cable);
            adjacency[cable.ToNodeId].Add(cable);
        }
        foreach (var list in adjacency.Values)
            list.Sort(static (left, right) => left.Id.Value.CompareTo(right.Id.Value));

        var endpointResidual = new Dictionary<OpticalNodeId, double>();
        foreach (var endpoint in request.Endpoints.OrderBy(static item => item.NodeId.Value))
        {
            if (!nodes.ContainsKey(endpoint.NodeId)) throw new ArgumentException("Optical endpoint references an invalid node.", nameof(request));
            ValidateNonNegativeFinite(endpoint.CapacityGigabitsPerSecond, nameof(request));
            endpointResidual[endpoint.NodeId] = endpoint.IsOperational ? endpoint.CapacityGigabitsPerSecond : 0d;
        }

        var backhauls = request.Backhauls.OrderBy(static item => item.Id.Value).ToArray();
        var backhaulResidual = new Dictionary<OpticalBackhaulId, double>(backhauls.Length);
        foreach (var backhaul in backhauls)
        {
            if (!nodes.ContainsKey(backhaul.NodeId)) throw new ArgumentException("Optical backhaul references an invalid node.", nameof(request));
            ValidatePositiveFinite(backhaul.CapacityGigabitsPerSecond, nameof(request));
            if (!backhaulResidual.TryAdd(backhaul.Id, backhaul.IsOperational ? backhaul.CapacityGigabitsPerSecond : 0d))
                throw new ArgumentException($"Duplicate OpticalBackhaul ID {backhaul.Id.Value}.", nameof(request));
        }

        var routes = new List<OpticalDemandRouteResult>(request.Demands.Count);
        var seenDemands = new HashSet<OpticalDemandId>();
        foreach (var demand in request.Demands
            .OrderByDescending(static item => item.Priority)
            .ThenBy(static item => item.Id.Value))
        {
            if (!seenDemands.Add(demand.Id)) throw new ArgumentException($"Duplicate OpticalDemand ID {demand.Id.Value}.", nameof(request));
            if (!nodes.TryGetValue(demand.NodeId, out var demandNode)) throw new ArgumentException("Optical demand references an invalid node.", nameof(request));
            ValidateNonNegativeFinite(demand.RequestedGigabitsPerSecond, nameof(request));
            if (demand.RequestedGigabitsPerSecond <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond
                || !demandNode.IsAvailable
                || endpointResidual.GetValueOrDefault(demand.NodeId) <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond)
            {
                routes.Add(new OpticalDemandRouteResult(demand.Id, null, 0d, Array.Empty<FiberCableId>()));
                continue;
            }

            CandidateRoute? selected = null;
            foreach (var backhaul in backhauls)
            {
                var sourceResidual = backhaulResidual.GetValueOrDefault(backhaul.Id);
                if (sourceResidual <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond
                    || !nodes[backhaul.NodeId].IsAvailable)
                    continue;

                var path = FindShortestPath(backhaul.NodeId, demand.NodeId, nodes, adjacency, residualCable);
                if (path is null) continue;
                var bottleneck = Math.Min(sourceResidual, endpointResidual[demand.NodeId]);
                foreach (var cableId in path)
                    bottleneck = Math.Min(bottleneck, residualCable[cableId]);
                var allocation = Math.Min(demand.RequestedGigabitsPerSecond, bottleneck);
                if (allocation <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond) continue;

                var candidate = new CandidateRoute(backhaul.Id, path, allocation);
                if (selected is null || CompareCandidate(candidate, selected.Value) < 0)
                    selected = candidate;
            }

            if (selected is not { } route)
            {
                routes.Add(new OpticalDemandRouteResult(demand.Id, null, 0d, Array.Empty<FiberCableId>()));
                continue;
            }

            endpointResidual[demand.NodeId] -= route.Allocation;
            backhaulResidual[route.BackhaulId] -= route.Allocation;
            foreach (var cableId in route.Path)
                residualCable[cableId] -= route.Allocation;
            routes.Add(new OpticalDemandRouteResult(demand.Id, route.BackhaulId, route.Allocation, Array.AsReadOnly(route.Path)));
        }

        routes.Sort(static (left, right) => left.DemandId.Value.CompareTo(right.DemandId.Value));
        var cableLoads = request.FiberCables
            .OrderBy(static item => item.Id.Value)
            .Select(item => new OpticalFiberLoadResult(
                item.Id,
                item.IsInService ? Math.Max(0d, item.CapacityGigabitsPerSecond - residualCable[item.Id]) : 0d))
            .ToArray();
        var backhaulLoads = backhauls
            .Select(item => new OpticalBackhaulLoadResult(
                item.Id,
                item.IsOperational ? Math.Max(0d, item.CapacityGigabitsPerSecond - backhaulResidual[item.Id]) : 0d))
            .ToArray();
        return new OpticalRoutingResult(Array.AsReadOnly(routes.ToArray()), Array.AsReadOnly(cableLoads), Array.AsReadOnly(backhaulLoads));
    }

    private static FiberCableId[]? FindShortestPath(
        OpticalNodeId source,
        OpticalNodeId target,
        Dictionary<OpticalNodeId, OpticalRoutingNode> nodes,
        Dictionary<OpticalNodeId, List<OpticalRoutingCable>> adjacency,
        Dictionary<FiberCableId, double> residualCable)
    {
        if (source == target) return [];
        var queue = new Queue<OpticalNodeId>();
        var visited = new HashSet<OpticalNodeId> { source };
        var previous = new Dictionary<OpticalNodeId, (OpticalNodeId NodeId, FiberCableId CableId)>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var cable in adjacency[current])
            {
                if (residualCable[cable.Id] <= OpticalDefaults.BandwidthEpsilonGigabitsPerSecond) continue;
                var next = cable.FromNodeId == current ? cable.ToNodeId : cable.FromNodeId;
                if (!nodes[next].IsAvailable || !visited.Add(next)) continue;
                previous[next] = (current, cable.Id);
                if (next == target)
                {
                    var path = new List<FiberCableId>();
                    var cursor = target;
                    while (cursor != source)
                    {
                        var step = previous[cursor];
                        path.Add(step.CableId);
                        cursor = step.NodeId;
                    }
                    path.Reverse();
                    return path.ToArray();
                }
                queue.Enqueue(next);
            }
        }
        return null;
    }

    private static int CompareCandidate(CandidateRoute left, CandidateRoute right)
    {
        var allocation = right.Allocation.CompareTo(left.Allocation);
        if (allocation != 0) return allocation;
        var hops = left.Path.Length.CompareTo(right.Path.Length);
        if (hops != 0) return hops;
        var backhaul = left.BackhaulId.Value.CompareTo(right.BackhaulId.Value);
        if (backhaul != 0) return backhaul;
        for (var index = 0; index < left.Path.Length; index++)
        {
            var cable = left.Path[index].Value.CompareTo(right.Path[index].Value);
            if (cable != 0) return cable;
        }
        return 0;
    }

    private static void ValidatePositiveFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value <= 0d) throw new ArgumentOutOfRangeException(paramName);
    }

    private static void ValidateNonNegativeFinite(double value, string paramName)
    {
        if (!double.IsFinite(value) || value < 0d) throw new ArgumentOutOfRangeException(paramName);
    }

    private readonly record struct CandidateRoute(
        OpticalBackhaulId BackhaulId,
        FiberCableId[] Path,
        double Allocation);
}
