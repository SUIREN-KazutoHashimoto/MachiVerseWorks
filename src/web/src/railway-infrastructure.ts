import * as THREE from 'three';

import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

const SNAPSHOT_HEADER_LENGTH = 41;
const NODE_LENGTH = 33;
const SEGMENT_LENGTH = 43;
const CONNECTION_LENGTH = 32;
const BLOCK_HEADER_LENGTH = 12;
const STATION_LENGTH = 56;
const PLATFORM_LENGTH = 88;
const ACCESS_POINT_LENGTH = 24;
const DEPOT_HEADER_LENGTH = 60;

export const WEB_RAILWAY_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 7 });

export enum RailwayMessageType {
  RailwayInfrastructureSnapshot = 700,
}

export enum TrackDirection { Bidirectional = 0, StartToEnd = 1, EndToStart = 2 }
export enum TrackElectrification { None = 0, Overhead = 1, ThirdRail = 2 }
export enum TrackUsage { Mainline = 0, Siding = 1, Depot = 2 }

export interface TrackNode {
  readonly id: bigint;
  readonly kind: number;
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

export interface TrackSegment {
  readonly id: bigint;
  readonly startNodeId: bigint;
  readonly endNodeId: bigint;
  readonly direction: TrackDirection;
  readonly gaugeMeters: number;
  readonly speedLimitMetersPerSecond: number;
  readonly electrification: TrackElectrification;
  readonly usage: TrackUsage;
}

export interface TrackConnection { readonly id: bigint; readonly fromSegmentId: bigint; readonly toSegmentId: bigint; readonly viaNodeId: bigint; }
export interface BlockSection { readonly id: bigint; readonly segmentIds: readonly bigint[]; }
export interface RailwayBounds { readonly minX: number; readonly minY: number; readonly minZ: number; readonly maxX: number; readonly maxY: number; readonly maxZ: number; }
export interface Station extends RailwayBounds { readonly id: bigint; }
export interface Platform extends RailwayBounds { readonly id: bigint; readonly stationId: bigint; readonly trackSegmentId: bigint; readonly startSegmentOffset: number; readonly endSegmentOffset: number; }
export interface PlatformAccessPoint { readonly id: bigint; readonly platformId: bigint; readonly roadAccessPointId: bigint; }
export interface Depot extends RailwayBounds { readonly id: bigint; readonly trackSegmentIds: readonly bigint[]; }

export interface RailwayInfrastructureSnapshotMessage {
  readonly type: RailwayMessageType.RailwayInfrastructureSnapshot;
  readonly revision: bigint;
  readonly isFullSnapshot: boolean;
  readonly nodes: readonly TrackNode[];
  readonly segments: readonly TrackSegment[];
  readonly connections: readonly TrackConnection[];
  readonly blocks: readonly BlockSection[];
  readonly stations: readonly Station[];
  readonly platforms: readonly Platform[];
  readonly platformAccessPoints: readonly PlatformAccessPoint[];
  readonly depots: readonly Depot[];
}

export type RailwayProtocolMessage = RailwayInfrastructureSnapshotMessage;
export interface RailwayProtocolEnvelope { readonly version: ProtocolVersion; readonly message: RailwayProtocolMessage; }

export function isRailwayFrame(frame: ArrayBuffer): boolean {
  return frame.byteLength >= PROTOCOL_HEADER_SIZE
    && new DataView(frame).getUint16(8, true) === RailwayMessageType.RailwayInfrastructureSnapshot;
}

export function decodeRailwayFrame(frame: ArrayBuffer): RailwayProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Railway frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Railway frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Railway frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Railway frame payload length is invalid.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 6) throw new ProtocolDecodeFailure('Railway infrastructure frames require Protocol 2.6 or newer.');
  const type = view.getUint16(8, true) as RailwayMessageType;
  if (type !== RailwayMessageType.RailwayInfrastructureSnapshot) throw new ProtocolDecodeFailure(`Unknown railway message type: ${String(type)}.`);
  return { version, message: decodeSnapshot(view, PROTOCOL_HEADER_SIZE, payloadLength) };
}

function decodeSnapshot(view: DataView, offset: number, payloadLength: number): RailwayInfrastructureSnapshotMessage {
  if (payloadLength < SNAPSHOT_HEADER_LENGTH) throw new ProtocolDecodeFailure('Railway infrastructure payload is too short.');
  const end = offset + payloadLength;
  let cursor = offset;
  const requireBytes = (count: number): void => {
    if (!Number.isSafeInteger(count) || count < 0 || cursor + count > end) throw new ProtocolDecodeFailure('Railway infrastructure payload is truncated.');
  };
  const readByte = (): number => { requireBytes(1); return view.getUint8(cursor++); };
  const readUint32 = (): number => { requireBytes(4); const value = view.getUint32(cursor, true); cursor += 4; return value; };
  const readUint64 = (): bigint => { requireBytes(8); const value = view.getBigUint64(cursor, true); cursor += 8; return value; };
  const readDouble = (): number => { requireBytes(8); const value = view.getFloat64(cursor, true); cursor += 8; return value; };

  const revision = readUint64();
  const full = readByte();
  if (full > 1) throw new ProtocolDecodeFailure('Railway snapshot full-snapshot flag is invalid.');
  const nodeCount = readUint32();
  const segmentCount = readUint32();
  const connectionCount = readUint32();
  const blockCount = readUint32();
  const stationCount = readUint32();
  const platformCount = readUint32();
  const accessCount = readUint32();
  const depotCount = readUint32();
  const fixedBytes = nodeCount * NODE_LENGTH + segmentCount * SEGMENT_LENGTH + connectionCount * CONNECTION_LENGTH + blockCount * BLOCK_HEADER_LENGTH + stationCount * STATION_LENGTH + platformCount * PLATFORM_LENGTH + accessCount * ACCESS_POINT_LENGTH + depotCount * DEPOT_HEADER_LENGTH;
  if (!Number.isSafeInteger(fixedBytes) || fixedBytes > end - cursor) throw new ProtocolDecodeFailure('Railway snapshot counts exceed its payload length.');

  const nodes: TrackNode[] = [];
  for (let index = 0; index < nodeCount; index += 1) {
    const item = { id: readUint64(), kind: readByte(), x: readDouble(), y: readDouble(), z: readDouble() };
    if (item.id === 0n || item.kind > 2 || !finite3(item.x, item.y, item.z)) throw new ProtocolDecodeFailure('TrackNode payload is invalid.');
    nodes.push(item);
  }

  const segments: TrackSegment[] = [];
  for (let index = 0; index < segmentCount; index += 1) {
    const item: TrackSegment = {
      id: readUint64(), startNodeId: readUint64(), endNodeId: readUint64(), direction: readByte() as TrackDirection,
      gaugeMeters: readDouble(), speedLimitMetersPerSecond: readDouble(), electrification: readByte() as TrackElectrification, usage: readByte() as TrackUsage,
    };
    if (item.id === 0n || item.startNodeId === 0n || item.endNodeId === 0n || item.startNodeId === item.endNodeId
      || !isTrackDirection(item.direction) || !Number.isFinite(item.gaugeMeters) || item.gaugeMeters <= 0
      || !Number.isFinite(item.speedLimitMetersPerSecond) || item.speedLimitMetersPerSecond <= 0
      || !isTrackElectrification(item.electrification) || !isTrackUsage(item.usage)) throw new ProtocolDecodeFailure('TrackSegment payload is invalid.');
    segments.push(item);
  }

  const connections: TrackConnection[] = [];
  for (let index = 0; index < connectionCount; index += 1) {
    const item = { id: readUint64(), fromSegmentId: readUint64(), toSegmentId: readUint64(), viaNodeId: readUint64() };
    if (item.id === 0n || item.fromSegmentId === 0n || item.toSegmentId === 0n || item.viaNodeId === 0n || item.fromSegmentId === item.toSegmentId) throw new ProtocolDecodeFailure('TrackConnection payload is invalid.');
    connections.push(item);
  }

  const blocks: BlockSection[] = [];
  for (let index = 0; index < blockCount; index += 1) {
    const id = readUint64(); const count = readUint32();
    if (id === 0n || count === 0 || count > Math.floor((end - cursor) / 8)) throw new ProtocolDecodeFailure('BlockSection payload is invalid.');
    const segmentIds: bigint[] = [];
    for (let itemIndex = 0; itemIndex < count; itemIndex += 1) { const segmentId = readUint64(); if (segmentId === 0n) throw new ProtocolDecodeFailure('BlockSection segment ID is invalid.'); segmentIds.push(segmentId); }
    blocks.push({ id, segmentIds });
  }

  const stations: Station[] = [];
  for (let index = 0; index < stationCount; index += 1) {
    const id = readUint64(); const bounds = readBounds(readDouble);
    if (id === 0n || !validBounds(bounds)) throw new ProtocolDecodeFailure('Station payload is invalid.');
    stations.push({ id, ...bounds });
  }

  const platforms: Platform[] = [];
  for (let index = 0; index < platformCount; index += 1) {
    const id = readUint64(); const stationId = readUint64(); const trackSegmentId = readUint64(); const startSegmentOffset = readDouble(); const endSegmentOffset = readDouble(); const bounds = readBounds(readDouble);
    if (id === 0n || stationId === 0n || trackSegmentId === 0n || !Number.isFinite(startSegmentOffset) || !Number.isFinite(endSegmentOffset)
      || startSegmentOffset < 0 || endSegmentOffset > 1 || endSegmentOffset <= startSegmentOffset || !validBounds(bounds)) throw new ProtocolDecodeFailure('Platform payload is invalid.');
    platforms.push({ id, stationId, trackSegmentId, startSegmentOffset, endSegmentOffset, ...bounds });
  }

  const platformAccessPoints: PlatformAccessPoint[] = [];
  for (let index = 0; index < accessCount; index += 1) {
    const item = { id: readUint64(), platformId: readUint64(), roadAccessPointId: readUint64() };
    if (item.id === 0n || item.platformId === 0n || item.roadAccessPointId === 0n) throw new ProtocolDecodeFailure('PlatformAccessPoint payload is invalid.');
    platformAccessPoints.push(item);
  }

  const depots: Depot[] = [];
  for (let index = 0; index < depotCount; index += 1) {
    const id = readUint64(); const bounds = readBounds(readDouble); const count = readUint32();
    if (id === 0n || count === 0 || !validBounds(bounds) || count > Math.floor((end - cursor) / 8)) throw new ProtocolDecodeFailure('Depot payload is invalid.');
    const trackSegmentIds: bigint[] = [];
    for (let itemIndex = 0; itemIndex < count; itemIndex += 1) { const segmentId = readUint64(); if (segmentId === 0n) throw new ProtocolDecodeFailure('Depot track segment ID is invalid.'); trackSegmentIds.push(segmentId); }
    depots.push({ id, ...bounds, trackSegmentIds });
  }
  if (cursor !== end) throw new ProtocolDecodeFailure('Railway infrastructure payload contains trailing bytes.');
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
}

export class RailwayInfrastructureLayer {
  private readonly trackMaterial = new THREE.LineBasicMaterial();
  private readonly stationMaterial = new THREE.LineBasicMaterial();
  private readonly platformMaterial = new THREE.LineBasicMaterial();
  private readonly tracks = new THREE.LineSegments(new THREE.BufferGeometry(), this.trackMaterial);
  private readonly stations = new THREE.LineSegments(new THREE.BufferGeometry(), this.stationMaterial);
  private readonly platforms = new THREE.LineSegments(new THREE.BufferGeometry(), this.platformMaterial);
  private readonly nodes = new Map<bigint, TrackNode>();
  private readonly segments = new Map<bigint, TrackSegment>();
  private readonly connections = new Map<bigint, TrackConnection>();
  private readonly blocks = new Map<bigint, BlockSection>();
  private readonly stationBounds = new Map<bigint, Station>();
  private readonly platformBounds = new Map<bigint, Platform>();
  private readonly platformAccessPoints = new Map<bigint, PlatformAccessPoint>();
  private readonly depots = new Map<bigint, Depot>();
  private revision: bigint | null = null;

  public constructor(private readonly scene: THREE.Scene) {
    this.tracks.name = 'railway-tracks'; this.stations.name = 'railway-stations'; this.platforms.name = 'railway-platforms';
    this.tracks.frustumCulled = false; this.stations.frustumCulled = false; this.platforms.frustumCulled = false;
    this.scene.add(this.tracks, this.stations, this.platforms);
  }

  public apply(snapshot: RailwayInfrastructureSnapshotMessage): void {
    if (snapshot.isFullSnapshot) {
      this.resetSnapshotState();
      this.revision = snapshot.revision;
    } else if (this.revision !== snapshot.revision) {
      return;
    }

    for (const item of snapshot.nodes) this.addUnique(this.nodes, item.id, item, 'TrackNode');
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
    for (const segment of this.segments.values()) {
      const start = this.nodes.get(segment.startNodeId); const end = this.nodes.get(segment.endNodeId);
      if (start === undefined || end === undefined) continue;
      appendPosition(trackPositions, start.x, start.y, start.z); appendPosition(trackPositions, end.x, end.y, end.z);
    }
    const stationPositions: number[] = []; for (const station of this.stationBounds.values()) appendBoxEdges(stationPositions, station);
    const platformPositions: number[] = []; for (const platform of this.platformBounds.values()) appendBoxEdges(platformPositions, platform);
    replacePositions(this.tracks.geometry, trackPositions); replacePositions(this.stations.geometry, stationPositions); replacePositions(this.platforms.geometry, platformPositions);
  }

  public clear(): void {
    this.resetSnapshotState();
    replacePositions(this.tracks.geometry, []); replacePositions(this.stations.geometry, []); replacePositions(this.platforms.geometry, []);
  }

  public dispose(): void {
    this.scene.remove(this.tracks, this.stations, this.platforms);
    this.tracks.geometry.dispose(); this.stations.geometry.dispose(); this.platforms.geometry.dispose();
    this.trackMaterial.dispose(); this.stationMaterial.dispose(); this.platformMaterial.dispose();
    this.resetSnapshotState();
  }

  private addUnique<T>(target: Map<bigint, T>, id: bigint, item: T, label: string): void {
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
}

function readBounds(readDouble: () => number): RailwayBounds {
  return { minX: readDouble(), minY: readDouble(), minZ: readDouble(), maxX: readDouble(), maxY: readDouble(), maxZ: readDouble() };
}
function assertUniqueIds(ids: readonly bigint[], label: string): void { const set = new Set(ids); if (set.size !== ids.length || set.has(0n)) throw new ProtocolDecodeFailure(`${label} IDs are duplicated or invalid.`); }
function isIncident(segment: TrackSegment, nodeId: bigint): boolean { return segment.startNodeId === nodeId || segment.endNodeId === nodeId; }
function finite3(x: number, y: number, z: number): boolean { return Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z); }
function validBounds(value: RailwayBounds): boolean { return finite3(value.minX, value.minY, value.minZ) && finite3(value.maxX, value.maxY, value.maxZ) && value.minX <= value.maxX && value.minY <= value.maxY && value.minZ <= value.maxZ; }
function isTrackDirection(value: TrackDirection): boolean { return value >= TrackDirection.Bidirectional && value <= TrackDirection.EndToStart; }
function isTrackElectrification(value: TrackElectrification): boolean { return value >= TrackElectrification.None && value <= TrackElectrification.ThirdRail; }
function isTrackUsage(value: TrackUsage): boolean { return value >= TrackUsage.Mainline && value <= TrackUsage.Depot; }
function appendPosition(target: number[], x: number, y: number, z: number): void { target.push(x, z, y); }
function appendBoxEdges(target: number[], bounds: RailwayBounds): void {
  const corners = [
    [bounds.minX, bounds.minY, bounds.minZ], [bounds.maxX, bounds.minY, bounds.minZ], [bounds.maxX, bounds.maxY, bounds.minZ], [bounds.minX, bounds.maxY, bounds.minZ],
    [bounds.minX, bounds.minY, bounds.maxZ], [bounds.maxX, bounds.minY, bounds.maxZ], [bounds.maxX, bounds.maxY, bounds.maxZ], [bounds.minX, bounds.maxY, bounds.maxZ],
  ] as const;
  const edges = [[0,1],[1,2],[2,3],[3,0],[4,5],[5,6],[6,7],[7,4],[0,4],[1,5],[2,6],[3,7]] as const;
  for (const [a, b] of edges) { const left = corners[a]; const right = corners[b]; appendPosition(target, left[0], left[1], left[2]); appendPosition(target, right[0], right[1], right[2]); }
}
function replacePositions(geometry: THREE.BufferGeometry, positions: readonly number[]): void {
  geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
  geometry.computeBoundingSphere();
}
