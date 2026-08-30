namespace MachiVerseWorks.Simulation.Internal;

internal sealed class PedestrianNetworkStore
{
    private const ulong AccessNodeFlag = 1UL << 63;
    private const ulong StableIdMask = AccessNodeFlag - 1;
    private readonly Dictionary<PedestrianNodeId, PedestrianNodeSnapshot> nodes = [];
    private readonly Dictionary<PedestrianEdgeId, PedestrianEdgeSnapshot> edges = [];
    private readonly Dictionary<PedestrianNodeId, List<PedestrianEdgeId>> adjacency = [];
    private readonly Dictionary<BuildingId, List<PedestrianNodeId>> buildingNodes = [];
    private readonly Dictionary<PoiId, List<PedestrianNodeId>> poiNodes = [];
    private readonly Dictionary<(PedestrianEdgeId First, PedestrianEdgeId Second), PedestrianCrossingId> crossingByEdges = [];
    private readonly Dictionary<PedestrianCrossingId, PedestrianCrossingSnapshot> crossings = [];
    private Dictionary<PedestrianCrossingId, bool> crossingPermissions = [];

    public int NodeCount => nodes.Count;
    public int EdgeCount => edges.Count;
    public int CrossingCount => crossings.Count;

    public void Rebuild(RoadNetworkSnapshot roadNetwork)
    {
        ArgumentNullException.ThrowIfNull(roadNetwork);
        var previousPermissions = crossingPermissions;
        nodes.Clear(); edges.Clear(); adjacency.Clear(); buildingNodes.Clear(); poiNodes.Clear(); crossingByEdges.Clear(); crossings.Clear();
        crossingPermissions = [];

        var roadNodes = new Dictionary<RoadNodeId, RoadNodeSnapshot>(roadNetwork.Nodes.Count);
        foreach (var node in roadNetwork.Nodes) roadNodes.Add(node.Id, node);

        var accessBySegment = new Dictionary<RoadSegmentId, List<RoadAccessPointSnapshot>>();
        foreach (var access in roadNetwork.AccessPoints)
        {
            if ((access.Mode & RoadAccessMode.Foot) == 0) continue;
            if (!accessBySegment.TryGetValue(access.SegmentId, out var list))
            {
                list = [];
                accessBySegment.Add(access.SegmentId, list);
            }
            list.Add(access);
        }
        foreach (var list in accessBySegment.Values)
        {
            list.Sort(static (left, right) =>
            {
                var offset = left.SegmentOffset.CompareTo(right.SegmentOffset);
                return offset != 0 ? offset : left.Id.Value.CompareTo(right.Id.Value);
            });
        }

        var segments = roadNetwork.Segments.ToArray();
        Array.Sort(segments, static (left, right) => left.Id.Value.CompareTo(right.Id.Value));
        foreach (var segment in segments)
        {
            if (segment.Kind == RoadKind.Highway) continue;
            if (!roadNodes.TryGetValue(segment.StartNodeId, out var start) || !roadNodes.TryGetValue(segment.EndNodeId, out var end))
                throw new InvalidOperationException($"Road segment {segment.Id.Value} references a missing road node.");

            var startNode = EnsureRoadNode(start);
            var endNode = EnsureRoadNode(end);
            var chain = new List<(double Offset, PedestrianNodeId NodeId)> { (0d, startNode) };
            if (accessBySegment.TryGetValue(segment.Id, out var accessPoints))
            {
                foreach (var access in accessPoints)
                {
                    var position = Interpolate(start.Position, end.Position, access.SegmentOffset);
                    var pedestrianNode = EnsureAccessNode(access, position);
                    chain.Add((access.SegmentOffset, pedestrianNode));
                    if (access.BuildingId is { } buildingId) AddEndpoint(buildingNodes, buildingId, pedestrianNode);
                    if (access.PoiId is { } poiId) AddEndpoint(poiNodes, poiId, pedestrianNode);
                }
            }
            chain.Add((1d, endNode));

            for (var index = 0; index < chain.Count - 1; index++)
            {
                var first = chain[index].NodeId;
                var second = chain[index + 1].NodeId;
                AddEdge(segment.Id, first, second);
            }
        }

        foreach (var entry in adjacency) entry.Value.Sort(static (left, right) => left.Value.CompareTo(right.Value));
        foreach (var list in buildingNodes.Values) list.Sort(static (left, right) => left.Value.CompareTo(right.Value));
        foreach (var list in poiNodes.Values) list.Sort(static (left, right) => left.Value.CompareTo(right.Value));

        foreach (var node in nodes.Values)
        {
            if (node.Kind != PedestrianNodeKind.RoadJunction || node.RoadNodeId is not { } roadNodeId) continue;
            if (!roadNodes.TryGetValue(roadNodeId, out var roadNode) || roadNode.Kind != RoadNodeKind.Intersection) continue;
            if (!adjacency.TryGetValue(node.Id, out var incident)) continue;
            for (var firstIndex = 0; firstIndex < incident.Count; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < incident.Count; secondIndex++)
                {
                    var firstEdge = incident[firstIndex];
                    var secondEdge = incident[secondIndex];
                    if (edges[firstEdge].RoadSegmentId == edges[secondEdge].RoadSegmentId) continue;
                    AddCrossing(roadNodeId, firstEdge, secondEdge, previousPermissions);
                }
            }
        }
    }

    public PedestrianNetworkSnapshot CreateSnapshot()
    {
        var nodeArray = nodes.Values.OrderBy(static item => item.Id.Value).ToArray();
        var edgeArray = edges.Values.OrderBy(static item => item.Id.Value).ToArray();
        var crossingArray = crossings.Values.OrderBy(static item => item.Id.Value).Select(item => item with { IsOpen = IsCrossingOpen(item.Id) }).ToArray();
        return new PedestrianNetworkSnapshot(nodeArray, edgeArray, crossingArray);
    }

    public SimulationPedestrianCrossingCheckpoint[] CreateCrossingCheckpoint() => crossings.Keys
        .OrderBy(static id => id.Value)
        .Select(id => new SimulationPedestrianCrossingCheckpoint(id, IsCrossingOpen(id)))
        .ToArray();

    public void RestoreCrossingPermissions(IReadOnlyList<SimulationPedestrianCrossingCheckpoint> checkpoints)
    {
        ArgumentNullException.ThrowIfNull(checkpoints);
        foreach (var checkpoint in checkpoints)
        {
            if (!crossings.ContainsKey(checkpoint.Id))
                throw new ArgumentException($"Pedestrian crossing {checkpoint.Id.Value} does not exist in the restored Road Network.", nameof(checkpoints));
            crossingPermissions[checkpoint.Id] = checkpoint.IsOpen;
        }
    }

    public bool TrySetCrossingOpen(PedestrianCrossingId id, bool isOpen)
    {
        if (!crossings.ContainsKey(id)) return false;
        crossingPermissions[id] = isOpen;
        return true;
    }

    public bool IsCrossingOpen(PedestrianCrossingId id) => !crossingPermissions.TryGetValue(id, out var open) || open;

    public bool TryGetCrossing(PedestrianEdgeId first, PedestrianEdgeId second, out PedestrianCrossingId id)
    {
        var key = NormalizeEdgePair(first, second);
        return crossingByEdges.TryGetValue(key, out id);
    }

    public PedestrianRoute FindRoute(TripEndpoint origin, TripEndpoint destination)
    {
        var starts = ResolveEndpointCandidates(origin, nameof(origin));
        var ends = ResolveEndpointCandidates(destination, nameof(destination));
        var startSet = new HashSet<PedestrianNodeId>(starts);
        var endSet = new HashSet<PedestrianNodeId>(ends);

        foreach (var start in starts)
        {
            if (endSet.Contains(start)) return new PedestrianRoute(start, start, 0d, []);
        }

        var distances = new Dictionary<PedestrianNodeId, double>(starts.Count);
        var previous = new Dictionary<PedestrianNodeId, (PedestrianNodeId Node, PedestrianEdgeId Edge)>();
        var queue = new PriorityQueue<PedestrianNodeId, (double Distance, ulong NodeId)>();
        foreach (var start in starts)
        {
            distances[start] = 0d;
            queue.Enqueue(start, (0d, start.Value));
        }

        PedestrianNodeId? selectedEnd = null;
        while (queue.TryDequeue(out var current, out var priority))
        {
            if (!distances.TryGetValue(current, out var known) || priority.Distance > known) continue;
            if (endSet.Contains(current))
            {
                selectedEnd = current;
                break;
            }
            if (!adjacency.TryGetValue(current, out var incident)) continue;
            foreach (var edgeId in incident)
            {
                var edge = edges[edgeId];
                var next = edge.FirstNodeId == current ? edge.SecondNodeId : edge.FirstNodeId;
                if (startSet.Contains(next)) continue;
                var candidate = known + edge.LengthMeters;
                if (!distances.TryGetValue(next, out var old) || candidate < old || (candidate == old && IsPreferredPredecessor(current, edgeId, previous, next)))
                {
                    distances[next] = candidate;
                    previous[next] = (current, edgeId);
                    queue.Enqueue(next, (candidate, next.Value));
                }
            }
        }

        if (selectedEnd is not { } end || !distances.TryGetValue(end, out var total))
            throw new InvalidOperationException("No walkable pedestrian route exists between the requested endpoints.");

        var reversed = new List<PedestrianRouteLeg>();
        var cursor = end;
        while (previous.TryGetValue(cursor, out var step))
        {
            var edge = edges[step.Edge];
            reversed.Add(new PedestrianRouteLeg(step.Edge, step.Node, cursor, edge.LengthMeters));
            cursor = step.Node;
        }
        if (!startSet.Contains(cursor)) throw new InvalidOperationException("Pedestrian route predecessor chain does not terminate at an origin access point.");
        reversed.Reverse();
        return new PedestrianRoute(cursor, end, total, reversed.ToArray());
    }

    public WorldPoint GetNodePosition(PedestrianNodeId id)
    {
        if (!nodes.TryGetValue(id, out var node)) throw new ArgumentException($"Pedestrian node {id.Value} does not exist.", nameof(id));
        return node.Position;
    }

    public WorldPoint GetRoutePosition(PedestrianRouteLeg leg, double progressMeters)
    {
        var from = GetNodePosition(leg.FromNodeId);
        var to = GetNodePosition(leg.ToNodeId);
        if (leg.LengthMeters <= 0d) return to;
        var alpha = Math.Clamp(progressMeters / leg.LengthMeters, 0d, 1d);
        return Interpolate(from, to, alpha);
    }

    public WorldVector GetRouteVelocity(PedestrianRouteLeg leg, double speedMetersPerSecond)
    {
        var from = GetNodePosition(leg.FromNodeId);
        var to = GetNodePosition(leg.ToNodeId);
        var length = leg.LengthMeters;
        if (length <= 0d) return default;
        var scale = speedMetersPerSecond / length;
        return new WorldVector((to.X - from.X) * scale, (to.Y - from.Y) * scale, (to.Z - from.Z) * scale);
    }

    private PedestrianNodeId EnsureRoadNode(RoadNodeSnapshot roadNode)
    {
        var id = new PedestrianNodeId(MapRoadNodeId(roadNode.Id));
        if (!nodes.ContainsKey(id))
        {
            nodes.Add(id, new PedestrianNodeSnapshot(id, PedestrianNodeKind.RoadJunction, roadNode.Position, roadNode.Id, null));
            adjacency.Add(id, []);
        }
        return id;
    }

    private PedestrianNodeId EnsureAccessNode(RoadAccessPointSnapshot access, WorldPoint position)
    {
        var id = new PedestrianNodeId(MapAccessPointId(access.Id));
        if (!nodes.ContainsKey(id))
        {
            nodes.Add(id, new PedestrianNodeSnapshot(id, PedestrianNodeKind.AccessPoint, position, null, access.Id));
            adjacency.Add(id, []);
        }
        return id;
    }

    private void AddEdge(RoadSegmentId segmentId, PedestrianNodeId first, PedestrianNodeId second)
    {
        var firstPosition = nodes[first].Position;
        var secondPosition = nodes[second].Position;
        var length = Distance(firstPosition, secondPosition);
        var normalizedFirst = first.Value <= second.Value ? first : second;
        var normalizedSecond = first.Value <= second.Value ? second : first;
        var id = new PedestrianEdgeId(HashStableId(0x45, segmentId.Value, normalizedFirst.Value, normalizedSecond.Value));
        var snapshot = new PedestrianEdgeSnapshot(id, first, second, segmentId, length);
        if (edges.TryGetValue(id, out var existing) && existing != snapshot) throw new InvalidOperationException($"Pedestrian edge stable ID collision detected for {id.Value}.");
        if (edges.ContainsKey(id)) return;
        edges.Add(id, snapshot);
        adjacency[first].Add(id);
        adjacency[second].Add(id);
    }

    private void AddCrossing(RoadNodeId roadNodeId, PedestrianEdgeId first, PedestrianEdgeId second, Dictionary<PedestrianCrossingId, bool> previousPermissions)
    {
        var pair = NormalizeEdgePair(first, second);
        var id = new PedestrianCrossingId(HashStableId(0x43, roadNodeId.Value, pair.First.Value, pair.Second.Value));
        var open = !previousPermissions.TryGetValue(id, out var previousOpen) || previousOpen;
        var snapshot = new PedestrianCrossingSnapshot(id, roadNodeId, pair.First, pair.Second, open);
        if (crossings.TryGetValue(id, out var existing) && (existing with { IsOpen = open }) != snapshot) throw new InvalidOperationException($"Pedestrian crossing stable ID collision detected for {id.Value}.");
        crossings[id] = snapshot;
        crossingByEdges[pair] = id;
        crossingPermissions[id] = open;
    }

    private List<PedestrianNodeId> ResolveEndpointCandidates(TripEndpoint endpoint, string parameterName)
    {
        if ((endpoint.BuildingId is null) == (endpoint.PoiId is null)) throw new ArgumentException("Trip endpoint must reference exactly one Building or POI.", parameterName);
        if (endpoint.PoiId is { } poiId && poiNodes.TryGetValue(poiId, out var poiCandidates) && poiCandidates.Count > 0) return poiCandidates;
        if (endpoint.BuildingId is { } buildingId && buildingNodes.TryGetValue(buildingId, out var buildingCandidates) && buildingCandidates.Count > 0) return buildingCandidates;
        throw new InvalidOperationException("Trip endpoint does not have a Foot road access point on the pedestrian network.");
    }

    private static bool IsPreferredPredecessor(PedestrianNodeId current, PedestrianEdgeId edge, Dictionary<PedestrianNodeId, (PedestrianNodeId Node, PedestrianEdgeId Edge)> previous, PedestrianNodeId next)
    {
        if (!previous.TryGetValue(next, out var old)) return true;
        var edgeComparison = edge.Value.CompareTo(old.Edge.Value);
        return edgeComparison < 0 || (edgeComparison == 0 && current.Value < old.Node.Value);
    }

    private static void AddEndpoint<T>(Dictionary<T, List<PedestrianNodeId>> map, T key, PedestrianNodeId nodeId) where T : notnull
    {
        if (!map.TryGetValue(key, out var list)) { list = []; map.Add(key, list); }
        if (!list.Contains(nodeId)) list.Add(nodeId);
    }

    private static (PedestrianEdgeId First, PedestrianEdgeId Second) NormalizeEdgePair(PedestrianEdgeId first, PedestrianEdgeId second) => first.Value <= second.Value ? (first, second) : (second, first);

    private static ulong MapRoadNodeId(RoadNodeId id)
    {
        if (id.Value == 0 || id.Value > StableIdMask) throw new InvalidOperationException("Road node ID exceeds the pedestrian stable-ID mapping range.");
        return id.Value;
    }

    private static ulong MapAccessPointId(RoadAccessPointId id)
    {
        if (id.Value == 0 || id.Value > StableIdMask) throw new InvalidOperationException("Road access point ID exceeds the pedestrian stable-ID mapping range.");
        return AccessNodeFlag | id.Value;
    }

    private static WorldPoint Interpolate(WorldPoint first, WorldPoint second, double alpha) => new(
        first.X + (second.X - first.X) * alpha,
        first.Y + (second.Y - first.Y) * alpha,
        first.Z + (second.Z - first.Z) * alpha);

    private static double Distance(WorldPoint first, WorldPoint second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        var dz = second.Z - first.Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private static ulong HashStableId(byte domain, params ulong[] values)
    {
        var hash = 14695981039346656037UL;
        hash = (hash ^ domain) * 1099511628211UL;
        foreach (var value in values)
        {
            var remaining = value;
            for (var index = 0; index < sizeof(ulong); index++)
            {
                hash = (hash ^ (byte)remaining) * 1099511628211UL;
                remaining >>= 8;
            }
        }
        return hash == 0 ? 1UL : hash;
    }
}