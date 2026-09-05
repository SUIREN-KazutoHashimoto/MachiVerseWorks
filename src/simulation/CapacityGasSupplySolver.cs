namespace MachiVerseWorks.Simulation;

public sealed class CapacityGasSupplySolver : IGasSupplySolver
{
    public GasSupplyResult Solve(GasSupplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nodes = request.Nodes.OrderBy(static item => item.Id.Value).ToArray();
        var indexes = new Dictionary<GasNodeId, int>(nodes.Length);
        foreach (var node in nodes)
        {
            if (node.Id.Value == 0 || !indexes.TryAdd(node.Id, indexes.Count))
                throw new ArgumentException("Gas supply request contains an invalid or duplicate node ID.", nameof(request));
        }

        var graph = new DirectedCapacityGraph(nodes.Length + 2);
        var sourceVertex = nodes.Length;
        var sinkVertex = sourceVertex + 1;

        var pipelineIds = new HashSet<GasPipelineId>();
        foreach (var pipeline in request.Pipelines.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(pipeline.CapacityCubicMetersPerDay, "Gas pipeline");
            if (pipeline.Id.Value == 0 || !pipelineIds.Add(pipeline.Id)
                || !indexes.TryGetValue(pipeline.FromNodeId, out var from)
                || !indexes.TryGetValue(pipeline.ToNodeId, out var to)
                || from == to)
            {
                throw new ArgumentException("Gas supply request contains an invalid Pipeline.", nameof(request));
            }
            if (pipeline.IsInService && pipeline.CapacityCubicMetersPerDay > GasDefaults.FlowEpsilonCubicMetersPerDay)
                graph.AddEdge(from, to, pipeline.CapacityCubicMetersPerDay);
        }

        var sourceEdges = AddSupplyEdges(request.Sources, static item => item.Id.Value, static item => item.NodeId, static item => item.AvailableCapacityCubicMetersPerDay, indexes, graph, sourceVertex, "Gas source");
        var terminalEdges = AddSupplyEdges(request.ImportTerminals, static item => item.Id.Value, static item => item.NodeId, static item => item.AvailableCapacityCubicMetersPerDay, indexes, graph, sourceVertex, "Gas import terminal");
        var storageEdges = AddSupplyEdges(request.Storages, static item => item.Id.Value, static item => item.NodeId, static item => item.AvailableCapacityCubicMetersPerDay, indexes, graph, sourceVertex, "Gas storage");

        var loadIds = new HashSet<GasServicePointId>();
        var loadEdges = new List<(GasServicePointId Id, int Vertex, int Edge, double Demand)>();
        foreach (var load in request.Loads.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(load.DemandCubicMetersPerDay, "Gas demand");
            if (load.Id.Value == 0 || !loadIds.Add(load.Id) || !indexes.TryGetValue(load.NodeId, out var node))
                throw new ArgumentException("Gas supply request contains an invalid Service Point.", nameof(request));
            var edge = graph.AddEdge(node, sinkVertex, load.DemandCubicMetersPerDay);
            loadEdges.Add((load.Id, node, edge, load.DemandCubicMetersPerDay));
        }

        graph.MaxFlow(sourceVertex, sinkVertex);
        return new GasSupplyResult(
            sourceEdges.Select(item => new GasSourceDispatch(new GasSourceId(item.Id), FlowUsed(graph, item.Vertex, item.Edge, item.Capacity))).ToArray(),
            terminalEdges.Select(item => new GasImportTerminalDispatch(new GasImportTerminalId(item.Id), FlowUsed(graph, item.Vertex, item.Edge, item.Capacity))).ToArray(),
            storageEdges.Select(item => new GasStorageDispatch(new GasStorageId(item.Id), FlowUsed(graph, item.Vertex, item.Edge, item.Capacity))).ToArray(),
            loadEdges.Select(item => new GasLoadDispatch(item.Id, FlowUsed(graph, item.Vertex, item.Edge, item.Demand))).ToArray());
    }

    private static List<(ulong Id, int Vertex, int Edge, double Capacity)> AddSupplyEdges<T>(
        IEnumerable<T> items,
        Func<T, ulong> getId,
        Func<T, GasNodeId> getNodeId,
        Func<T, double> getCapacity,
        Dictionary<GasNodeId, int> indexes,
        DirectedCapacityGraph graph,
        int sourceVertex,
        string name)
    {
        var result = new List<(ulong Id, int Vertex, int Edge, double Capacity)>();
        var ids = new HashSet<ulong>();
        foreach (var item in items.OrderBy(getId))
        {
            var id = getId(item);
            var capacity = getCapacity(item);
            ValidateCapacity(capacity, name);
            if (id == 0 || !ids.Add(id) || !indexes.TryGetValue(getNodeId(item), out var node))
                throw new ArgumentException($"Gas supply request contains an invalid {name}.");
            var edge = graph.AddEdge(sourceVertex, node, capacity);
            result.Add((id, sourceVertex, edge, capacity));
        }
        return result;
    }

    private static double FlowUsed(DirectedCapacityGraph graph, int vertex, int edge, double capacity) =>
        Math.Max(0d, capacity - graph.GetRemainingCapacity(vertex, edge));

    private static void ValidateCapacity(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d)
            throw new ArgumentOutOfRangeException(name, "Gas capacities must be finite and non-negative.");
    }
}
