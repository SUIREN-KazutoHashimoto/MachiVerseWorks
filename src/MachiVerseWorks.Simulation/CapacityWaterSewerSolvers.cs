namespace MachiVerseWorks.Simulation;

public sealed class CapacityWaterSupplySolver : IWaterSupplySolver
{
    public WaterSupplyResult Solve(WaterSupplyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nodes = request.Nodes.OrderBy(static item => item.Id.Value).ToArray();
        var indexes = BuildNodeIndexes(nodes.Select(static item => item.Id.Value), "Water");
        var totalCapacity = request.Pipes.Sum(static item => Math.Max(0d, item.CapacityCubicMetersPerDay))
            + request.Sources.Sum(static item => Math.Max(0d, item.AvailableCapacityCubicMetersPerDay))
            + request.Reservoirs.Sum(static item => Math.Max(0d, item.AvailableCapacityCubicMetersPerDay))
            + request.Loads.Sum(static item => Math.Max(0d, item.DemandCubicMetersPerDay)) + 1d;
        var graph = new DirectedCapacityGraph((nodes.Length * 2) + 2);
        var source = nodes.Length * 2;
        var sink = source + 1;

        var pumpCapacityByNode = new Dictionary<WaterNodeId, double>();
        var pumpIds = new HashSet<PumpId>();
        foreach (var pump in request.Pumps.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(pump.AvailableCapacityCubicMetersPerDay, "Water pump");
            if (pump.Id.Value == 0 || !pumpIds.Add(pump.Id) || !indexes.ContainsKey(pump.NodeId.Value))
                throw new ArgumentException("Water supply request contains an invalid Pump.", nameof(request));
            pumpCapacityByNode[pump.NodeId] = pumpCapacityByNode.GetValueOrDefault(pump.NodeId) + pump.AvailableCapacityCubicMetersPerDay;
        }

        var nodeEdges = new Dictionary<WaterNodeId, int>(nodes.Length);
        for (var index = 0; index < nodes.Length; index++)
        {
            var capacity = pumpCapacityByNode.GetValueOrDefault(nodes[index].Id, totalCapacity);
            nodeEdges[nodes[index].Id] = graph.AddEdge(index * 2, (index * 2) + 1, capacity);
        }

        var pipeIds = new HashSet<WaterPipeId>();
        foreach (var pipe in request.Pipes.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(pipe.CapacityCubicMetersPerDay, "Water pipe");
            if (pipe.Id.Value == 0 || !pipeIds.Add(pipe.Id) || !indexes.TryGetValue(pipe.FromNodeId.Value, out var from) || !indexes.TryGetValue(pipe.ToNodeId.Value, out var to) || from == to)
                throw new ArgumentException("Water supply request contains an invalid Pipe.", nameof(request));
            if (pipe.IsInService && pipe.CapacityCubicMetersPerDay > WaterSewerDefaults.FlowEpsilonCubicMetersPerDay)
                graph.AddEdge((from * 2) + 1, to * 2, pipe.CapacityCubicMetersPerDay);
        }

        var sourceIds = new HashSet<WaterSourceId>();
        var sourceEdges = new List<(WaterSourceId Id, int Vertex, int Edge, double Capacity)>();
        foreach (var item in request.Sources.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(item.AvailableCapacityCubicMetersPerDay, "Water source");
            if (item.Id.Value == 0 || !sourceIds.Add(item.Id) || !indexes.TryGetValue(item.NodeId.Value, out var node))
                throw new ArgumentException("Water supply request contains an invalid Source.", nameof(request));
            var edge = graph.AddEdge(source, node * 2, item.AvailableCapacityCubicMetersPerDay);
            sourceEdges.Add((item.Id, source, edge, item.AvailableCapacityCubicMetersPerDay));
        }

        var reservoirIds = new HashSet<ReservoirId>();
        var reservoirEdges = new List<(ReservoirId Id, int Vertex, int Edge, double Capacity)>();
        foreach (var item in request.Reservoirs.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(item.AvailableCapacityCubicMetersPerDay, "Reservoir");
            if (item.Id.Value == 0 || !reservoirIds.Add(item.Id) || !indexes.TryGetValue(item.NodeId.Value, out var node))
                throw new ArgumentException("Water supply request contains an invalid Reservoir.", nameof(request));
            var edge = graph.AddEdge(source, node * 2, item.AvailableCapacityCubicMetersPerDay);
            reservoirEdges.Add((item.Id, source, edge, item.AvailableCapacityCubicMetersPerDay));
        }

        var loadIds = new HashSet<WaterSewerServicePointId>();
        var loadEdges = new List<(WaterSewerServicePointId Id, int Vertex, int Edge, double Demand)>();
        foreach (var item in request.Loads.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(item.DemandCubicMetersPerDay, "Water demand");
            if (item.Id.Value == 0 || !loadIds.Add(item.Id) || !indexes.TryGetValue(item.NodeId.Value, out var node))
                throw new ArgumentException("Water supply request contains an invalid Service Point.", nameof(request));
            var vertex = (node * 2) + 1;
            var edge = graph.AddEdge(vertex, sink, item.DemandCubicMetersPerDay);
            loadEdges.Add((item.Id, vertex, edge, item.DemandCubicMetersPerDay));
        }

        graph.MaxFlow(source, sink);
        return new WaterSupplyResult(
            sourceEdges.Select(item => new WaterSourceDispatch(item.Id, FlowUsed(graph, item.Vertex, item.Edge, item.Capacity))).ToArray(),
            reservoirEdges.Select(item => new ReservoirDispatch(item.Id, FlowUsed(graph, item.Vertex, item.Edge, item.Capacity))).ToArray(),
            request.Pumps.OrderBy(static item => item.Id.Value).Select(item =>
            {
                var node = indexes[item.NodeId.Value];
                var capacity = pumpCapacityByNode[item.NodeId];
                var totalThroughput = FlowUsed(graph, node * 2, nodeEdges[item.NodeId], capacity);
                var share = capacity <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? 0d : item.AvailableCapacityCubicMetersPerDay / capacity;
                return new PumpDispatch(item.Id, totalThroughput * share);
            }).ToArray(),
            loadEdges.Select(item => new WaterLoadDispatch(item.Id, FlowUsed(graph, item.Vertex, item.Edge, item.Demand))).ToArray());
    }

    private static Dictionary<ulong, int> BuildNodeIndexes(IEnumerable<ulong> ids, string network)
    {
        var result = new Dictionary<ulong, int>();
        foreach (var id in ids)
        {
            if (id == 0 || !result.TryAdd(id, result.Count)) throw new ArgumentException($"{network} request contains an invalid or duplicate node ID.");
        }
        return result;
    }

    private static double FlowUsed(DirectedCapacityGraph graph, int vertex, int edge, double capacity) => Math.Max(0d, capacity - graph.GetRemainingCapacity(vertex, edge));
    private static void ValidateCapacity(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d) throw new ArgumentOutOfRangeException(name, "Utility capacities must be finite and non-negative.");
    }
}

public sealed class CapacitySewerSolver : ISewerSolver
{
    public SewerFlowResult Solve(SewerFlowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nodes = request.Nodes.OrderBy(static item => item.Id.Value).ToArray();
        var indexes = new Dictionary<ulong, int>(nodes.Length);
        foreach (var node in nodes)
        {
            if (node.Id.Value == 0 || !indexes.TryAdd(node.Id.Value, indexes.Count)) throw new ArgumentException("Sewer request contains an invalid or duplicate node ID.", nameof(request));
        }
        var totalCapacity = request.Pipes.Sum(static item => Math.Max(0d, item.CapacityCubicMetersPerDay))
            + request.Treatments.Sum(static item => Math.Max(0d, item.AvailableCapacityCubicMetersPerDay))
            + request.Loads.Sum(static item => Math.Max(0d, item.GeneratedCubicMetersPerDay)) + 1d;
        var graph = new DirectedCapacityGraph((nodes.Length * 2) + 2);
        var source = nodes.Length * 2;
        var sink = source + 1;

        var pumpCapacityByNode = new Dictionary<SewerNodeId, double>();
        var pumpIds = new HashSet<PumpId>();
        foreach (var pump in request.Pumps.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(pump.AvailableCapacityCubicMetersPerDay, "Sewer pump");
            if (pump.Id.Value == 0 || !pumpIds.Add(pump.Id) || !indexes.ContainsKey(pump.NodeId.Value)) throw new ArgumentException("Sewer request contains an invalid Pump.", nameof(request));
            pumpCapacityByNode[pump.NodeId] = pumpCapacityByNode.GetValueOrDefault(pump.NodeId) + pump.AvailableCapacityCubicMetersPerDay;
        }

        var nodeEdges = new Dictionary<SewerNodeId, int>(nodes.Length);
        for (var index = 0; index < nodes.Length; index++)
        {
            var capacity = pumpCapacityByNode.GetValueOrDefault(nodes[index].Id, totalCapacity);
            nodeEdges[nodes[index].Id] = graph.AddEdge(index * 2, (index * 2) + 1, capacity);
        }

        var pipeIds = new HashSet<SewerPipeId>();
        foreach (var pipe in request.Pipes.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(pipe.CapacityCubicMetersPerDay, "Sewer pipe");
            if (pipe.Id.Value == 0 || !pipeIds.Add(pipe.Id) || !indexes.TryGetValue(pipe.FromNodeId.Value, out var from) || !indexes.TryGetValue(pipe.ToNodeId.Value, out var to) || from == to)
                throw new ArgumentException("Sewer request contains an invalid Pipe.", nameof(request));
            if (pipe.IsInService && pipe.CapacityCubicMetersPerDay > WaterSewerDefaults.FlowEpsilonCubicMetersPerDay)
                graph.AddEdge((from * 2) + 1, to * 2, pipe.CapacityCubicMetersPerDay);
        }

        var loadIds = new HashSet<WaterSewerServicePointId>();
        var loadEdges = new List<(WaterSewerServicePointId Id, int Vertex, int Edge, double Generated)>();
        foreach (var load in request.Loads.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(load.GeneratedCubicMetersPerDay, "Wastewater generation");
            if (load.Id.Value == 0 || !loadIds.Add(load.Id) || !indexes.TryGetValue(load.NodeId.Value, out var node)) throw new ArgumentException("Sewer request contains an invalid Service Point.", nameof(request));
            var edge = graph.AddEdge(source, node * 2, load.GeneratedCubicMetersPerDay);
            loadEdges.Add((load.Id, source, edge, load.GeneratedCubicMetersPerDay));
        }

        var treatmentIds = new HashSet<SewageTreatmentPlantId>();
        var treatmentEdges = new List<(SewageTreatmentPlantId Id, int Vertex, int Edge, double Capacity)>();
        foreach (var treatment in request.Treatments.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(treatment.AvailableCapacityCubicMetersPerDay, "Treatment capacity");
            if (treatment.Id.Value == 0 || !treatmentIds.Add(treatment.Id) || !indexes.TryGetValue(treatment.NodeId.Value, out var node)) throw new ArgumentException("Sewer request contains an invalid Treatment Plant.", nameof(request));
            var vertex = (node * 2) + 1;
            var edge = graph.AddEdge(vertex, sink, treatment.AvailableCapacityCubicMetersPerDay);
            treatmentEdges.Add((treatment.Id, vertex, edge, treatment.AvailableCapacityCubicMetersPerDay));
        }

        graph.MaxFlow(source, sink);
        return new SewerFlowResult(
            request.Pumps.OrderBy(static item => item.Id.Value).Select(item =>
            {
                var node = indexes[item.NodeId.Value];
                var capacity = pumpCapacityByNode[item.NodeId];
                var totalThroughput = Math.Max(0d, capacity - graph.GetRemainingCapacity(node * 2, nodeEdges[item.NodeId]));
                var share = capacity <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay ? 0d : item.AvailableCapacityCubicMetersPerDay / capacity;
                return new PumpDispatch(item.Id, totalThroughput * share);
            }).ToArray(),
            treatmentEdges.Select(item => new SewerTreatmentDispatch(item.Id, Math.Max(0d, item.Capacity - graph.GetRemainingCapacity(item.Vertex, item.Edge)))).ToArray(),
            loadEdges.Select(item => new SewerLoadDispatch(item.Id, Math.Max(0d, item.Generated - graph.GetRemainingCapacity(item.Vertex, item.Edge)))).ToArray());
    }

    private static void ValidateCapacity(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0d) throw new ArgumentOutOfRangeException(name, "Utility capacities must be finite and non-negative.");
    }
}

internal sealed class DirectedCapacityGraph(int vertexCount)
{
    private readonly List<Edge>[] _edges = Enumerable.Range(0, vertexCount).Select(static _ => new List<Edge>()).ToArray();
    private readonly int[] _levels = new int[vertexCount];
    private readonly int[] _nextEdges = new int[vertexCount];

    public int AddEdge(int from, int to, double capacity)
    {
        var forwardIndex = _edges[from].Count;
        var reverseIndex = _edges[to].Count;
        _edges[from].Add(new Edge(to, reverseIndex, capacity));
        _edges[to].Add(new Edge(from, forwardIndex, 0d));
        return forwardIndex;
    }

    public double GetRemainingCapacity(int from, int edgeIndex) => _edges[from][edgeIndex].Capacity;

    public double MaxFlow(int source, int sink)
    {
        var total = 0d;
        while (BuildLevels(source, sink))
        {
            Array.Clear(_nextEdges);
            while (true)
            {
                var flow = SendFlow(source, sink, double.PositiveInfinity);
                if (flow <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay) break;
                total += flow;
            }
        }
        return total;
    }

    private bool BuildLevels(int source, int sink)
    {
        Array.Fill(_levels, -1);
        var queue = new Queue<int>();
        _levels[source] = 0;
        queue.Enqueue(source);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in _edges[current])
            {
                if (edge.Capacity <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay || _levels[edge.To] >= 0) continue;
                _levels[edge.To] = _levels[current] + 1;
                queue.Enqueue(edge.To);
            }
        }
        return _levels[sink] >= 0;
    }

    private double SendFlow(int current, int sink, double available)
    {
        if (current == sink) return available;
        for (; _nextEdges[current] < _edges[current].Count; _nextEdges[current]++)
        {
            var edge = _edges[current][_nextEdges[current]];
            if (edge.Capacity <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay || _levels[edge.To] != _levels[current] + 1) continue;
            var sent = SendFlow(edge.To, sink, Math.Min(available, edge.Capacity));
            if (sent <= WaterSewerDefaults.FlowEpsilonCubicMetersPerDay) continue;
            edge.Capacity -= sent;
            _edges[edge.To][edge.ReverseIndex].Capacity += sent;
            return sent;
        }
        return 0d;
    }

    private sealed class Edge(int to, int reverseIndex, double capacity)
    {
        public int To { get; } = to;
        public int ReverseIndex { get; } = reverseIndex;
        public double Capacity { get; set; } = capacity;
    }
}
