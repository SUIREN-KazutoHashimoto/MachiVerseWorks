namespace MachiVerseWorks.Simulation;

public sealed class CapacityPowerDispatchSolver : IPowerDispatchSolver
{
    public PowerDispatchResult Solve(PowerDispatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Nodes);
        ArgumentNullException.ThrowIfNull(request.Lines);
        ArgumentNullException.ThrowIfNull(request.Generators);
        ArgumentNullException.ThrowIfNull(request.Loads);

        var orderedNodes = request.Nodes.OrderBy(static item => item.Id.Value).ToArray();
        var nodeIndexes = new Dictionary<PowerNodeId, int>(orderedNodes.Length);
        for (var index = 0; index < orderedNodes.Length; index++)
        {
            if (orderedNodes[index].Id.Value == 0 || !nodeIndexes.TryAdd(orderedNodes[index].Id, index))
                throw new ArgumentException("Power dispatch request contains an invalid or duplicate node ID.", nameof(request));
        }

        var graph = new FlowGraph(orderedNodes.Length + 2);
        var source = orderedNodes.Length;
        var sink = source + 1;
        var lineIds = new HashSet<PowerLineId>();
        foreach (var line in request.Lines.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(line.CapacityMegawatts, nameof(request));
            if (line.Id.Value == 0 || !lineIds.Add(line.Id))
                throw new ArgumentException("Power dispatch request contains an invalid or duplicate line ID.", nameof(request));
            if (!nodeIndexes.TryGetValue(line.FromNodeId, out var from) || !nodeIndexes.TryGetValue(line.ToNodeId, out var to) || from == to)
                throw new ArgumentException("Power dispatch request contains a line with an invalid node reference.", nameof(request));
            if (!line.IsInService || line.CapacityMegawatts <= PowerDefaults.SupplyEpsilonMegawatts) continue;
            graph.AddEdge(from, to, line.CapacityMegawatts);
            graph.AddEdge(to, from, line.CapacityMegawatts);
        }

        var generatorIds = new HashSet<GeneratorId>();
        var generatorEdges = new List<(GeneratorId Id, int EdgeIndex, double Capacity)>();
        foreach (var generator in request.Generators.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(generator.AvailableCapacityMegawatts, nameof(request));
            if (generator.Id.Value == 0 || !generatorIds.Add(generator.Id))
                throw new ArgumentException("Power dispatch request contains an invalid or duplicate Generator ID.", nameof(request));
            if (!nodeIndexes.TryGetValue(generator.NodeId, out var node))
                throw new ArgumentException("Power dispatch request contains a Generator with an invalid node reference.", nameof(request));
            var edgeIndex = graph.AddEdge(source, node, generator.AvailableCapacityMegawatts);
            generatorEdges.Add((generator.Id, edgeIndex, generator.AvailableCapacityMegawatts));
        }

        var loadIds = new HashSet<PowerLoadId>();
        var loadEdges = new List<(PowerLoadId Id, int NodeIndex, int EdgeIndex, double Demand)>();
        foreach (var load in request.Loads.OrderBy(static item => item.Id.Value))
        {
            ValidateCapacity(load.DemandMegawatts, nameof(request));
            if (load.Id.Value == 0 || !loadIds.Add(load.Id))
                throw new ArgumentException("Power dispatch request contains an invalid or duplicate Load ID.", nameof(request));
            if (!nodeIndexes.TryGetValue(load.NodeId, out var node))
                throw new ArgumentException("Power dispatch request contains a Load with an invalid node reference.", nameof(request));
            var edgeIndex = graph.AddEdge(node, sink, load.DemandMegawatts);
            loadEdges.Add((load.Id, node, edgeIndex, load.DemandMegawatts));
        }

        graph.MaxFlow(source, sink);

        var generators = generatorEdges.Select(item => new PowerGeneratorDispatch(
            item.Id,
            Math.Max(0d, item.Capacity - graph.GetRemainingCapacity(source, item.EdgeIndex)))).ToArray();
        var loads = loadEdges.Select(item => new PowerLoadDispatch(
            item.Id,
            Math.Max(0d, item.Demand - graph.GetRemainingCapacity(item.NodeIndex, item.EdgeIndex)))).ToArray();
        return new PowerDispatchResult(Array.AsReadOnly(generators), Array.AsReadOnly(loads));
    }

    private static void ValidateCapacity(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d)
            throw new ArgumentOutOfRangeException(parameterName, "Power capacities and demand must be finite and non-negative.");
    }

    private sealed class FlowGraph(int vertexCount)
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
                    if (flow <= PowerDefaults.SupplyEpsilonMegawatts) break;
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
                    if (edge.Capacity <= PowerDefaults.SupplyEpsilonMegawatts || _levels[edge.To] >= 0) continue;
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
                if (edge.Capacity <= PowerDefaults.SupplyEpsilonMegawatts || _levels[edge.To] != _levels[current] + 1) continue;
                var sent = SendFlow(edge.To, sink, Math.Min(available, edge.Capacity));
                if (sent <= PowerDefaults.SupplyEpsilonMegawatts) continue;
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
}
