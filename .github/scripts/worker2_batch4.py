from pathlib import Path


def replace_once(path_name: str, old: str, new: str) -> None:
    path = Path(path_name)
    text = path.read_text(encoding="utf-8")
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path_name}: expected exactly one patch target, found {count}; target={old[:240]!r}")
    path.write_text(text.replace(old, new, 1), encoding="utf-8")


def append_before_final_brace(path_name: str, addition: str) -> None:
    path = Path(path_name)
    text = path.read_text(encoding="utf-8")
    marker = "\n}"
    index = text.rfind(marker)
    if index < 0:
        raise SystemExit(f"{path_name}: final brace not found")
    path.write_text(text[:index] + addition + text[index:], encoding="utf-8")


# #311: Intersection movement identity is the LaneConnection stable ID and must be unique.
replace_once(
    "src/MachiVerseWorks.Protocol/IntersectionControlProtocolCodec.cs",
    """        var movements = new ProtocolIntersectionMovementState[checked((int)movementCount)];
        var offset = HeaderLength;
        for (var index = 0; index < movements.Length; index++)
""",
    """        var movements = new ProtocolIntersectionMovementState[checked((int)movementCount)];
        var movementIds = new HashSet<ulong>();
        var connectionIds = new HashSet<ulong>();
        var offset = HeaderLength;
        for (var index = 0; index < movements.Length; index++)
""")
replace_once(
    "src/MachiVerseWorks.Protocol/IntersectionControlProtocolCodec.cs",
    """            if (movementId == 0
                || connectionId == 0
                || fromLaneId == 0
""",
    """            if (movementId == 0
                || connectionId == 0
                || movementId != connectionId
                || !movementIds.Add(movementId)
                || !connectionIds.Add(connectionId)
                || fromLaneId == 0
""")
replace_once(
    "src/MachiVerseWorks.Protocol/IntersectionControlProtocolCodec.cs",
    """        ArgumentNullException.ThrowIfNull(message.Movements);
        foreach (var movement in message.Movements)
        {
            if (movement.MovementId == 0 || movement.ConnectionId == 0 || movement.FromLaneId == 0 || movement.ToLaneId == 0)
""",
    """        ArgumentNullException.ThrowIfNull(message.Movements);
        var movementIds = new HashSet<ulong>();
        var connectionIds = new HashSet<ulong>();
        foreach (var movement in message.Movements)
        {
            if (movement.MovementId == 0 || movement.ConnectionId == 0 || movement.MovementId != movement.ConnectionId
                || !movementIds.Add(movement.MovementId) || !connectionIds.Add(movement.ConnectionId)
                || movement.FromLaneId == 0 || movement.ToLaneId == 0)
""")
replace_once(
    "src/web/src/traffic-protocol.ts",
    """  const movements: IntersectionMovementState[] = [];
  let cursor = offset + INTERSECTION_HEADER_LENGTH;
""",
    """  const movements: IntersectionMovementState[] = [];
  const movementIds = new Set<bigint>();
  const connectionIds = new Set<bigint>();
  let cursor = offset + INTERSECTION_HEADER_LENGTH;
""")
replace_once(
    "src/web/src/traffic-protocol.ts",
    """    assertStableId(movement.toLaneId, 'To Lane');
    if (!isTurnMovement(movement.turnMovement)
""",
    """    assertStableId(movement.toLaneId, 'To Lane');
    if (movement.movementId !== movement.connectionId || movementIds.has(movement.movementId) || connectionIds.has(movement.connectionId))
      throw new ProtocolDecodeFailure('Intersection movement identity is invalid or duplicated.');
    movementIds.add(movement.movementId);
    connectionIds.add(movement.connectionId);
    if (!isTurnMovement(movement.turnMovement)
""")

append_before_final_brace(
    "tests/MachiVerseWorks.Protocol.Tests/IntersectionControlProtocolTests.cs",
    """

    [TestMethod]
    public void IntersectionControlSnapshotRejectsMismatchedOrDuplicatedMovementIdentity()
    {
        var mismatched = new IntersectionControlSnapshotMessage(
            1, 5, ProtocolIntersectionControlMode.Unsignalized, 0, 0,
            [new ProtocolIntersectionMovementState(10, 11, 21, 22, ProtocolTurnMovement.Straight, 0, 0, 0, ProtocolSignalIndication.Green, 0, false)]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IntersectionControlProtocolCodec.Serialize(mismatched, ProtocolVersion.Current));

        var duplicated = new IntersectionControlSnapshotMessage(
            1, 5, ProtocolIntersectionControlMode.Unsignalized, 0, 0,
            [
                new ProtocolIntersectionMovementState(10, 10, 21, 22, ProtocolTurnMovement.Straight, 0, 0, 0, ProtocolSignalIndication.Green, 0, false),
                new ProtocolIntersectionMovementState(10, 10, 23, 24, ProtocolTurnMovement.Right, 0, 0, 0, ProtocolSignalIndication.Green, 0, false),
            ]);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => IntersectionControlProtocolCodec.Serialize(duplicated, ProtocolVersion.Current));
    }
""")

traffic_test = Path("src/web/tests/traffic-intersection-validation.test.mjs")
if traffic_test.exists():
    raise SystemExit("traffic-intersection-validation.test.mjs already exists")
traffic_test.write_text("""import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import { TrafficMessageType, decodeTrafficFrame } from '../src/traffic-protocol.ts';

test('traffic decoder rejects Intersection movement/connection identity mismatch', () => {
  const payloadLength = 31 + 63;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 4, true);
  view.setUint16(8, TrafficMessageType.IntersectionControlSnapshot, true); view.setUint16(10, 0, true); view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  view.setBigUint64(o, 1n, true); view.setBigUint64(o + 8, 5n, true); view.setUint8(o + 16, 0); view.setUint16(o + 17, 0, true); view.setBigUint64(o + 19, 0n, true); view.setUint32(o + 27, 1, true);
  const m = o + 31;
  view.setBigUint64(m, 10n, true); view.setBigUint64(m + 8, 11n, true); view.setBigUint64(m + 16, 21n, true); view.setBigUint64(m + 24, 22n, true);
  view.setUint8(m + 32, 1); view.setFloat64(m + 33, 0, true); view.setFloat64(m + 41, 0, true); view.setFloat64(m + 49, 0, true); view.setUint8(m + 57, 2); view.setUint32(m + 58, 0, true); view.setUint8(m + 62, 0);
  assert.throws(() => decodeTrafficFrame(frame), /identity/);
});
""", encoding="utf-8")


# #320: validate Railway Infrastructure identity per frame and topology across complete snapshots/chunks.
validator_path = Path("src/MachiVerseWorks.Protocol/RailwayInfrastructureProtocolValidator.cs")
if validator_path.exists():
    raise SystemExit("RailwayInfrastructureProtocolValidator.cs already exists")
validator_path.write_text("""namespace MachiVerseWorks.Protocol;

internal static class RailwayInfrastructureProtocolValidator
{
    public static void ValidateIdentity(RailwayInfrastructureSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Nodes);
        ArgumentNullException.ThrowIfNull(message.Segments);
        ArgumentNullException.ThrowIfNull(message.Connections);
        ArgumentNullException.ThrowIfNull(message.Blocks);
        ArgumentNullException.ThrowIfNull(message.Stations);
        ArgumentNullException.ThrowIfNull(message.Platforms);
        ArgumentNullException.ThrowIfNull(message.PlatformAccessPoints);
        ArgumentNullException.ThrowIfNull(message.Depots);

        ValidateUniqueIds(message.Nodes.Select(static item => item.Id), "TrackNode");
        ValidateUniqueIds(message.Segments.Select(static item => item.Id), "TrackSegment");
        ValidateUniqueIds(message.Connections.Select(static item => item.Id), "TrackConnection");
        ValidateUniqueIds(message.Blocks.Select(static item => item.Id), "BlockSection");
        ValidateUniqueIds(message.Stations.Select(static item => item.Id), "Station");
        ValidateUniqueIds(message.Platforms.Select(static item => item.Id), "Platform");
        ValidateUniqueIds(message.PlatformAccessPoints.Select(static item => item.Id), "PlatformAccessPoint");
        ValidateUniqueIds(message.Depots.Select(static item => item.Id), "Depot");
        foreach (var block in message.Blocks)
        {
            ArgumentNullException.ThrowIfNull(block);
            ArgumentNullException.ThrowIfNull(block.SegmentIds);
            ValidateUniqueIds(block.SegmentIds, $"BlockSection {block.Id} segment");
        }
        foreach (var depot in message.Depots)
        {
            ArgumentNullException.ThrowIfNull(depot);
            ArgumentNullException.ThrowIfNull(depot.TrackSegmentIds);
            ValidateUniqueIds(depot.TrackSegmentIds, $"Depot {depot.Id} track segment");
        }
    }

    public static void ValidateAggregate(RailwayInfrastructureSnapshotMessage message)
    {
        ValidateIdentity(message);
        var nodeIds = message.Nodes.Select(static item => item.Id).ToHashSet();
        var segments = message.Segments.ToDictionary(static item => item.Id);
        var stationIds = message.Stations.Select(static item => item.Id).ToHashSet();
        var platformIds = message.Platforms.Select(static item => item.Id).ToHashSet();

        foreach (var segment in message.Segments)
        {
            if (!nodeIds.Contains(segment.StartNodeId) || !nodeIds.Contains(segment.EndNodeId))
                throw new ArgumentOutOfRangeException(nameof(message), $"TrackSegment {segment.Id} references a missing TrackNode.");
        }
        foreach (var connection in message.Connections)
        {
            if (!segments.TryGetValue(connection.FromSegmentId, out var from)
                || !segments.TryGetValue(connection.ToSegmentId, out var to)
                || !nodeIds.Contains(connection.ViaNodeId)
                || !IsIncident(from, connection.ViaNodeId)
                || !IsIncident(to, connection.ViaNodeId))
                throw new ArgumentOutOfRangeException(nameof(message), $"TrackConnection {connection.Id} contains dangling or non-incident topology.");
        }
        foreach (var block in message.Blocks)
            if (block.SegmentIds.Any(id => !segments.ContainsKey(id)))
                throw new ArgumentOutOfRangeException(nameof(message), $"BlockSection {block.Id} references a missing TrackSegment.");
        foreach (var platform in message.Platforms)
            if (!stationIds.Contains(platform.StationId) || !segments.ContainsKey(platform.TrackSegmentId))
                throw new ArgumentOutOfRangeException(nameof(message), $"Platform {platform.Id} references a missing Station or TrackSegment.");
        foreach (var accessPoint in message.PlatformAccessPoints)
            if (!platformIds.Contains(accessPoint.PlatformId))
                throw new ArgumentOutOfRangeException(nameof(message), $"PlatformAccessPoint {accessPoint.Id} references a missing Platform.");
        foreach (var depot in message.Depots)
            if (depot.TrackSegmentIds.Any(id => !segments.ContainsKey(id)))
                throw new ArgumentOutOfRangeException(nameof(message), $"Depot {depot.Id} references a missing TrackSegment.");
    }

    private static void ValidateUniqueIds(IEnumerable<ulong> ids, string label)
    {
        var seen = new HashSet<ulong>();
        foreach (var id in ids)
            if (id == 0 || !seen.Add(id))
                throw new ArgumentOutOfRangeException("message", $"{label} IDs must be unique and greater than zero.");
    }

    private static bool IsIncident(ProtocolTrackSegment segment, ulong nodeId) => segment.StartNodeId == nodeId || segment.EndNodeId == nodeId;
}
""", encoding="utf-8")

replace_once(
    "src/MachiVerseWorks.Protocol/RailwayInfrastructureProtocolCodec.cs",
    """        ValidateMessage(message);

        var payloadLength = checked(
""",
    """        ValidateMessage(message);
        RailwayInfrastructureProtocolValidator.ValidateIdentity(message);

        var payloadLength = checked(
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RailwayInfrastructureProtocolCodec.cs",
    """            message = new RailwayInfrastructureSnapshotMessage(revision, full != 0, nodes, segments, connections, blocks, stations, platforms, accessPoints, depots);
            error = ProtocolDecodeError.None;
""",
    """            message = new RailwayInfrastructureSnapshotMessage(revision, full != 0, nodes, segments, connections, blocks, stations, platforms, accessPoints, depots);
            RailwayInfrastructureProtocolValidator.ValidateIdentity(message);
            error = ProtocolDecodeError.None;
""")
replace_once(
    "src/MachiVerseWorks.Protocol/RailwayInfrastructureProtocolCodec.cs",
    "exception is InvalidDataException or OverflowException or ArgumentOutOfRangeException",
    "exception is InvalidDataException or OverflowException or ArgumentException")
replace_once(
    "src/MachiVerseWorks.Protocol/RailwayInfrastructureProtocolChunker.cs",
    """        ArgumentNullException.ThrowIfNull(message.Depots);

        var chunks = new List<RailwayInfrastructureSnapshotMessage>();
""",
    """        ArgumentNullException.ThrowIfNull(message.Depots);
        if (message.IsFullSnapshot) RailwayInfrastructureProtocolValidator.ValidateAggregate(message);
        else RailwayInfrastructureProtocolValidator.ValidateIdentity(message);

        var chunks = new List<RailwayInfrastructureSnapshotMessage>();
""")

replace_once(
    "src/web/src/railway-infrastructure.ts",
    """  if (cursor !== end) throw new ProtocolDecodeFailure('Railway infrastructure payload contains trailing bytes.');

  return { type: RailwayMessageType.RailwayInfrastructureSnapshot, revision, isFullSnapshot: full === 1, nodes, segments, connections, blocks, stations, platforms, platformAccessPoints, depots };
""",
    """  if (cursor !== end) throw new ProtocolDecodeFailure('Railway infrastructure payload contains trailing bytes.');
  assertUniqueIds(nodes.map((item) => item.id), 'TrackNode');
  assertUniqueIds(segments.map((item) => item.id), 'TrackSegment');
  assertUniqueIds(connections.map((item) => item.id), 'TrackConnection');
  assertUniqueIds(blocks.map((item) => item.id), 'BlockSection');
  assertUniqueIds(stations.map((item) => item.id), 'Station');
  assertUniqueIds(platforms.map((item) => item.id), 'Platform');
  assertUniqueIds(platformAccessPoints.map((item) => item.id), 'PlatformAccessPoint');
  assertUniqueIds(depots.map((item) => item.id), 'Depot');
  for (const block of blocks) assertUniqueIds(block.segmentIds, `BlockSection ${block.id.toString()} segment`);
  for (const depot of depots) assertUniqueIds(depot.trackSegmentIds, `Depot ${depot.id.toString()} track segment`);

  return { type: RailwayMessageType.RailwayInfrastructureSnapshot, revision, isFullSnapshot: full === 1, nodes, segments, connections, blocks, stations, platforms, platformAccessPoints, depots };
""")
replace_once(
    "src/web/src/railway-infrastructure.ts",
    """  private readonly nodes = new Map<bigint, TrackNode>();
  private readonly segments = new Map<bigint, TrackSegment>();
  private readonly stationBounds = new Map<bigint, Station>();
  private readonly platformBounds = new Map<bigint, Platform>();
""",
    """  private readonly nodes = new Map<bigint, TrackNode>();
  private readonly segments = new Map<bigint, TrackSegment>();
  private readonly connections = new Map<bigint, TrackConnection>();
  private readonly blocks = new Map<bigint, BlockSection>();
  private readonly stationBounds = new Map<bigint, Station>();
  private readonly platformBounds = new Map<bigint, Platform>();
  private readonly platformAccessPoints = new Map<bigint, PlatformAccessPoint>();
  private readonly depots = new Map<bigint, Depot>();
""")
replace_once(
    "src/web/src/railway-infrastructure.ts",
    """    for (const item of snapshot.nodes) this.nodes.set(item.id, item);
    for (const item of snapshot.segments) this.segments.set(item.id, item);
    for (const item of snapshot.stations) this.stationBounds.set(item.id, item);
    for (const item of snapshot.platforms) this.platformBounds.set(item.id, item);

    const trackPositions: number[] = [];
""",
    """    for (const item of snapshot.nodes) this.addUnique(this.nodes, item.id, item, 'TrackNode');
    for (const item of snapshot.segments) {
      if (!this.nodes.has(item.startNodeId) || !this.nodes.has(item.endNodeId)) throw new ProtocolDecodeFailure(`TrackSegment ${item.id.toString()} references a missing TrackNode.`);
      this.addUnique(this.segments, item.id, item, 'TrackSegment');
    }
    for (const item of snapshot.connections) {
      const from = this.segments.get(item.fromSegmentId); const to = this.segments.get(item.toSegmentId);
      if (from === undefined || to === undefined || !this.nodes.has(item.viaNodeId) || !isIncident(from, item.viaNodeId) || !isIncident(to, item.viaNodeId)) throw new ProtocolDecodeFailure(`TrackConnection ${item.id.toString()} contains dangling topology.`);
      this.addUnique(this.connections, item.id, item, 'TrackConnection');
    }
    for (const item of snapshot.blocks) {
      if (new Set(item.segmentIds).size !== item.segmentIds.length || item.segmentIds.some((id) => !this.segments.has(id))) throw new ProtocolDecodeFailure(`BlockSection ${item.id.toString()} contains invalid TrackSegment references.`);
      this.addUnique(this.blocks, item.id, item, 'BlockSection');
    }
    for (const item of snapshot.stations) this.addUnique(this.stationBounds, item.id, item, 'Station');
    for (const item of snapshot.platforms) {
      if (!this.stationBounds.has(item.stationId) || !this.segments.has(item.trackSegmentId)) throw new ProtocolDecodeFailure(`Platform ${item.id.toString()} references a missing Station or TrackSegment.`);
      this.addUnique(this.platformBounds, item.id, item, 'Platform');
    }
    for (const item of snapshot.platformAccessPoints) {
      if (!this.platformBounds.has(item.platformId)) throw new ProtocolDecodeFailure(`PlatformAccessPoint ${item.id.toString()} references a missing Platform.`);
      this.addUnique(this.platformAccessPoints, item.id, item, 'PlatformAccessPoint');
    }
    for (const item of snapshot.depots) {
      if (new Set(item.trackSegmentIds).size !== item.trackSegmentIds.length || item.trackSegmentIds.some((id) => !this.segments.has(id))) throw new ProtocolDecodeFailure(`Depot ${item.id.toString()} contains invalid TrackSegment references.`);
      this.addUnique(this.depots, item.id, item, 'Depot');
    }

    const trackPositions: number[] = [];
""")
replace_once(
    "src/web/src/railway-infrastructure.ts",
    """  private resetSnapshotState(): void {
    this.revision = null;
    this.nodes.clear();
    this.segments.clear();
    this.stationBounds.clear();
    this.platformBounds.clear();
  }
""",
    """  private addUnique<T>(target: Map<bigint, T>, id: bigint, item: T, label: string): void {
    if (target.has(id)) throw new ProtocolDecodeFailure(`${label} ID ${id.toString()} is duplicated across Railway Infrastructure chunks.`);
    target.set(id, item);
  }

  private resetSnapshotState(): void {
    this.revision = null;
    this.nodes.clear();
    this.segments.clear();
    this.connections.clear();
    this.blocks.clear();
    this.stationBounds.clear();
    this.platformBounds.clear();
    this.platformAccessPoints.clear();
    this.depots.clear();
  }
""")
replace_once(
    "src/web/src/railway-infrastructure.ts",
    """function finite3(x: number, y: number, z: number): boolean { return Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z); }
""",
    """function assertUniqueIds(ids: readonly bigint[], label: string): void { const set = new Set(ids); if (set.size !== ids.length || set.has(0n)) throw new ProtocolDecodeFailure(`${label} IDs are duplicated or invalid.`); }
function isIncident(segment: TrackSegment, nodeId: bigint): boolean { return segment.startNodeId === nodeId || segment.endNodeId === nodeId; }
function finite3(x: number, y: number, z: number): boolean { return Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z); }
""")

append_before_final_brace(
    "tests/MachiVerseWorks.Protocol.Tests/RailwayInfrastructureProtocolTests.cs",
    """

    [TestMethod]
    public void RailwayInfrastructureRejectsDuplicateStableIdsWithinAFrame()
    {
        var message = new RailwayInfrastructureSnapshotMessage(
            1, true,
            [new ProtocolTrackNode(1, 0, 0, 0, 0), new ProtocolTrackNode(1, 0, 1, 0, 0)],
            [], [], [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayInfrastructureProtocolCodec.Serialize(message, ProtocolVersion.Current));
    }

    [TestMethod]
    public void RailwayInfrastructureChunkerRejectsDanglingAggregateTopology()
    {
        var message = new RailwayInfrastructureSnapshotMessage(
            1, true,
            [new ProtocolTrackNode(1, 0, 0, 0, 0)],
            [new ProtocolTrackSegment(10, 1, 999, ProtocolTrackDirection.Bidirectional, 1.067, 20, ProtocolTrackElectrification.None, ProtocolTrackUsage.Mainline)],
            [], [], [], [], [], []);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => RailwayInfrastructureProtocolChunker.Split(message));
    }
""")

replace_once(
    "src/web/tests/railway-infrastructure.test.mjs",
    """test('railway decoder rejects snapshots negotiated below 2.6', () => {
""",
    """test('railway decoder rejects duplicate stable IDs inside one chunk', () => {
  const frame = createFixtureFrame();
  const secondNodeIdOffset = PROTOCOL_HEADER_SIZE + 41 + 33;
  new DataView(frame).setBigUint64(secondNodeIdOffset, 1n, true);
  assert.throws(() => decodeRailwayFrame(frame), /duplicated|invalid/);
});

test('railway layer rejects duplicate IDs and dangling references across continuation chunks', () => {
  const scene = new THREE.Scene();
  const layer = new RailwayInfrastructureLayer(scene);
  const snapshot = decodeRailwayFrame(createFixtureFrame()).message;
  layer.apply({ ...snapshot, segments: [], stations: [], platforms: [] });
  assert.throws(() => layer.apply({ ...snapshot, isFullSnapshot: false, nodes: [snapshot.nodes[0]], segments: [], stations: [], platforms: [] }), /duplicated/);
  assert.throws(() => layer.apply({ ...snapshot, isFullSnapshot: false, nodes: [], segments: [{ ...snapshot.segments[0], id: 99n, startNodeId: 999n }], stations: [], platforms: [] }), /missing TrackNode/);
  layer.dispose();
});

test('railway decoder rejects snapshots negotiated below 2.6', () => {
""")


# #324: bound Optical Demand routeCableIds before DTO materialization and serialization.
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveLimits.cs",
    """    public const int DefaultMaximumRailwayRouteSegmentCount = 100_000;
""",
    """    public const int DefaultMaximumRailwayRouteSegmentCount = 100_000;
    public const int DefaultMaximumOpticalRouteCableCount = 100_000;
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveLimits.cs",
    """        int maximumGeographicFeatureGeometryPointCount = DefaultMaximumGeographicFeatureGeometryPointCount,
        int maximumNaturalToponymCount = DefaultMaximumNaturalToponymCount)
""",
    """        int maximumGeographicFeatureGeometryPointCount = DefaultMaximumGeographicFeatureGeometryPointCount,
        int maximumNaturalToponymCount = DefaultMaximumNaturalToponymCount,
        int maximumOpticalRouteCableCount = DefaultMaximumOpticalRouteCableCount)
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveLimits.cs",
    """        MaximumNaturalToponymCount = RequirePositive(maximumNaturalToponymCount, nameof(maximumNaturalToponymCount), "Maximum NaturalToponym count");
""",
    """        MaximumNaturalToponymCount = RequirePositive(maximumNaturalToponymCount, nameof(maximumNaturalToponymCount), "Maximum NaturalToponym count");
        MaximumOpticalRouteCableCount = RequirePositive(maximumOpticalRouteCableCount, nameof(maximumOpticalRouteCableCount), "Maximum Optical Demand route cable count");
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveLimits.cs",
    """    public int MaximumNaturalToponymCount { get; }
""",
    """    public int MaximumNaturalToponymCount { get; }
    public int MaximumOpticalRouteCableCount { get; }
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.Economy.cs",
    """        ValidateCount(optical.Demands.Count, limits.MaximumBuildingCount, "OpticalDemands");
    }
""",
    """        ValidateCount(optical.Demands.Count, limits.MaximumBuildingCount, "OpticalDemands");
        foreach (var demand in optical.Demands)
        {
            ArgumentNullException.ThrowIfNull(demand.RouteCableIds);
            ValidateCount(demand.RouteCableIds.Count, limits.MaximumOpticalRouteCableCount, "OpticalDemandRouteCableIds");
        }
    }
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """        else if (context == NestedSaveContext.Optical)
        {
            if (reader.ValueTextEquals("nodes")) return NestedSaveProperty.OpticalNodes;
            if (reader.ValueTextEquals("fiberCables")) return NestedSaveProperty.FiberCables;
            if (reader.ValueTextEquals("equipment")) return NestedSaveProperty.OpticalEquipment;
            if (reader.ValueTextEquals("backhauls")) return NestedSaveProperty.OpticalBackhauls;
            if (reader.ValueTextEquals("demands")) return NestedSaveProperty.OpticalDemands;
        }
        else if (context == NestedSaveContext.Radio)
""",
    """        else if (context == NestedSaveContext.Optical)
        {
            if (reader.ValueTextEquals("nodes")) return NestedSaveProperty.OpticalNodes;
            if (reader.ValueTextEquals("fiberCables")) return NestedSaveProperty.FiberCables;
            if (reader.ValueTextEquals("equipment")) return NestedSaveProperty.OpticalEquipment;
            if (reader.ValueTextEquals("backhauls")) return NestedSaveProperty.OpticalBackhauls;
            if (reader.ValueTextEquals("demands")) return NestedSaveProperty.OpticalDemands;
        }
        else if (context == NestedSaveContext.OpticalDemand && reader.ValueTextEquals("routeCableIds")) return NestedSaveProperty.OpticalRouteCableIds;
        else if (context == NestedSaveContext.Radio)
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            (NestedSaveContext.MultimodalTransit, NestedSaveProperty.TransitJourneys) => NestedSaveContext.TransitJourney,
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) => NestedSaveContext.GeographicFeature,
""",
    """            (NestedSaveContext.MultimodalTransit, NestedSaveProperty.TransitJourneys) => NestedSaveContext.TransitJourney,
            (NestedSaveContext.Optical, NestedSaveProperty.OpticalDemands) => NestedSaveContext.OpticalDemand,
            (NestedSaveContext.WorldEnvironment, NestedSaveProperty.Features) => NestedSaveContext.GeographicFeature,
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    """            (NestedSaveContext.Optical, NestedSaveProperty.OpticalDemands) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.demands", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioSites) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.sites", NestedArrayKind.None),
""",
    """            (NestedSaveContext.Optical, NestedSaveProperty.OpticalDemands) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.optical.demands", NestedArrayKind.None),
            (NestedSaveContext.OpticalDemand, NestedSaveProperty.OpticalRouteCableIds) => new(limits.MaximumOpticalRouteCableCount, "simulation.economy.optical.demands[].routeCableIds", NestedArrayKind.None),
            (NestedSaveContext.Radio, NestedSaveProperty.RadioSites) => new(limits.MaximumInfrastructureSiteCount, "simulation.economy.radio.sites", NestedArrayKind.None),
""")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    "Gas, Optical, Radio, WorldEnvironment, GeographicFeature,",
    "Gas, Optical, OpticalDemand, Radio, WorldEnvironment, GeographicFeature,")
replace_once(
    "src/MachiVerseWorks.Persistence/WorldSaveSerializer.NestedLimits.cs",
    "Optical, OpticalNodes, FiberCables, OpticalEquipment, OpticalBackhauls, OpticalDemands,",
    "Optical, OpticalNodes, FiberCables, OpticalEquipment, OpticalBackhauls, OpticalDemands, OpticalRouteCableIds,")
replace_once(
    "tests/MachiVerseWorks.Persistence.Tests/NestedSaveLimitTests.cs",
    """    [TestMethod]
    public void EconomyCoreCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
""",
    """    [TestMethod]
    public void OpticalDemandRouteCableIdsAreRejectedBeforeDtoMaterializationAboveLimit()
    {
        var limits = new WorldSaveLimits(maximumBytes: 100_000, maximumOpticalRouteCableCount: 1);
        AssertNestedBoundary(
            CreateSimulationJson("\\\"economy\\\":{\\\"optical\\\":{\\\"demands\\\":[{\\\"routeCableIds\\\":[1]}]}}"),
            CreateSimulationJson("\\\"economy\\\":{\\\"optical\\\":{\\\"demands\\\":[{\\\"routeCableIds\\\":[1,2]}]}}"),
            limits,
            "simulation.economy.optical.demands[].routeCableIds");
    }

    [TestMethod]
    public void EconomyCoreCollectionsAreRejectedBeforeDtoMaterializationAboveLimit()
""")


# #338: Web RegionalGeneration Int32-backed integers must stay inside Int32 range.
replace_once(
    "src/web/src/regional-generation-protocol.ts",
    """const MAXIMUM_TEXT_LENGTH = 256;
""",
    """const MAXIMUM_TEXT_LENGTH = 256;
const INT32_MAX = 2_147_483_647;
""")
regional_path = Path("src/web/src/regional-generation-protocol.ts")
regional_text = regional_path.read_text(encoding="utf-8")
count = regional_text.count("Number.MAX_SAFE_INTEGER")
if count != 7:
    raise SystemExit(f"regional-generation-protocol.ts: expected 7 Int32 MAX_SAFE_INTEGER uses, found {count}")
regional_path.write_text(regional_text.replace("Number.MAX_SAFE_INTEGER", "INT32_MAX"), encoding="utf-8")
replace_once(
    "src/web/tests/regional-generation-protocol.test.mjs",
    """function createFrame(json, version = { major: 2, minor: 18 }) {
""",
    """test('RegionalGeneration rejects Int32 overflow from wire JSON', () => {
  const overflow = createSnapshotJson().replace('\\\"population\\\":500', '\\\"population\\\":2147483648');
  assert.throws(() => decodeRegionalGenerationFrame(createFrame(overflow)), ProtocolDecodeFailure);
});

function createFrame(json, version = { major: 2, minor: 18 }) {
""")


# #340: Persistent Regional Evolution number-backed Int32 fields must be range checked.
replace_once(
    "src/web/src/persistent-regional-evolution-protocol.ts",
    """const MAXIMUM_REASON_LENGTH = 256;
""",
    """const MAXIMUM_REASON_LENGTH = 256;
const INT32_MIN = -2_147_483_648;
const INT32_MAX = 2_147_483_647;
""")
persistent_path = Path("src/web/src/persistent-regional-evolution-protocol.ts")
persistent_text = persistent_path.read_text(encoding="utf-8")
number_integer_count = persistent_text.count("Number.isInteger(")
if number_integer_count != 5:
    raise SystemExit(f"persistent-regional-evolution-protocol.ts: expected 5 Number.isInteger Int32 uses, found {number_integer_count}")
persistent_text = persistent_text.replace("Number.isInteger(", "int32(")
persistent_path.write_text(persistent_text, encoding="utf-8")
replace_once(
    "src/web/src/persistent-regional-evolution-protocol.ts",
    """function unit(value: unknown): value is number { return finite(value) && value >= 0 && value <= 1; }
function integerAtLeast(value: unknown, minimum: number): value is number { return typeof value === 'number' && int32(value) && value >= minimum; }
""",
    """function unit(value: unknown): value is number { return finite(value) && value >= 0 && value <= 1; }
function int32(value: unknown): value is number { return typeof value === 'number' && Number.isSafeInteger(value) && value >= INT32_MIN && value <= INT32_MAX; }
function integerAtLeast(value: unknown, minimum: number): value is number { return int32(value) && value >= minimum; }
""")
replace_once(
    "src/web/tests/persistent-regional-evolution-protocol.test.mjs",
    """test('PersistentRegionalEvolution decoder rejects Protocol versions older than 2.19', () => {
""",
    """test('PersistentRegionalEvolution decoder rejects Int32 overflow', () => {
  const payload = basePayload(100n, true);
  payload.currentYear = 2_147_483_648;
  assert.throws(() => decodePersistentRegionalEvolutionFrame(encodeSnapshot(payload, { major: 2, minor: 19 })), ProtocolDecodeFailure);
});

test('PersistentRegionalEvolution decoder rejects Protocol versions older than 2.19', () => {
""")

print("Batch 4 patches applied")
