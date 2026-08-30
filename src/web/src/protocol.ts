export const PROTOCOL_MAGIC = 0x5057564d;
export const PROTOCOL_HEADER_SIZE = 16;
export const PROTOCOL_MAX_PAYLOAD_LENGTH = 1_048_576;
export const CURRENT_PROTOCOL_VERSION = Object.freeze({ major: 2, minor: 2 });

const ROAD_HEADER_LENGTH = 28;
const ROAD_NODE_LENGTH = 33;
const ROAD_SEGMENT_LENGTH = 25;
const LANE_LENGTH = 35;
const LANE_CONNECTION_LENGTH = 33;
const ROAD_ACCESS_POINT_LENGTH = 41;
const PEDESTRIAN_STATE_LENGTH = 81;

export enum MessageType {
  Hello = 1,
  HelloAck = 2,
  SubscribeVolume = 3,
  AgentSpawn = 100,
  AgentUpdate = 101,
  AgentRemove = 102,
  RoadNetworkSnapshot = 200,
  PedestrianSpawn = 300,
  PedestrianUpdate = 301,
  PedestrianRemove = 302,
  Error = 900,
}

export enum ProtocolErrorCode {
  UnsupportedProtocolVersion = 1,
  InvalidFrame = 2,
  UnknownMessageType = 3,
  InvalidPayload = 4,
  InvalidRequest = 5,
  InternalServerError = 1000,
}

export enum RoadNodeKind { Endpoint = 0, Intersection = 1 }
export enum RoadKind { Local = 0, Collector = 1, Arterial = 2, Highway = 3, Service = 4 }
export enum LaneDirection { Forward = 0, Reverse = 1 }
export enum TurnMovement { Unspecified = 0, Straight = 1, Left = 2, Right = 3, UTurn = 4 }
export enum RoadAccessMode { None = 0, Motor = 1, Foot = 2 }
export enum PedestrianMovementState { Walking = 0, WaitingForCrossing = 1, WaitingForOccupancy = 2, Arrived = 3 }

export interface ProtocolVersion { readonly major: number; readonly minor: number; }
export interface WorldVolume { readonly minX: number; readonly minY: number; readonly minZ: number; readonly maxX: number; readonly maxY: number; readonly maxZ: number; }
export interface ProtocolErrorParameter { readonly key: string; readonly value: string; }
export interface HelloMessage { readonly type: MessageType.Hello; }
export interface HelloAckMessage { readonly type: MessageType.HelloAck; readonly protocolVersion: ProtocolVersion; readonly tickRate: number; }
export interface SubscribeVolumeMessage extends WorldVolume { readonly type: MessageType.SubscribeVolume; }
export interface AgentStateMessage { readonly type: MessageType.AgentSpawn | MessageType.AgentUpdate; readonly agentId: bigint; readonly x: number; readonly y: number; readonly z: number; readonly velocityX: number; readonly velocityY: number; readonly velocityZ: number; readonly tickCount: bigint; }
export interface AgentRemoveMessage { readonly type: MessageType.AgentRemove; readonly agentId: bigint; readonly tickCount: bigint; }
export interface PedestrianStateMessage { readonly type: MessageType.PedestrianSpawn | MessageType.PedestrianUpdate; readonly pedestrianId: bigint; readonly tripRequestId: bigint; readonly x: number; readonly y: number; readonly z: number; readonly velocityX: number; readonly velocityY: number; readonly velocityZ: number; readonly walkingSpeedMetersPerSecond: number; readonly state: PedestrianMovementState; readonly tickCount: bigint; }
export interface PedestrianRemoveMessage { readonly type: MessageType.PedestrianRemove; readonly pedestrianId: bigint; readonly tickCount: bigint; }
export interface ProtocolErrorMessage { readonly type: MessageType.Error; readonly code: ProtocolErrorCode; readonly parameters: readonly ProtocolErrorParameter[]; }
export interface RoadNode { readonly id: bigint; readonly kind: RoadNodeKind; readonly x: number; readonly y: number; readonly z: number; }
export interface RoadSegment { readonly id: bigint; readonly kind: RoadKind; readonly startNodeId: bigint; readonly endNodeId: bigint; }
export interface Lane { readonly id: bigint; readonly segmentId: bigint; readonly direction: LaneDirection; readonly order: number; readonly widthMeters: number; readonly speedLimitMetersPerSecond: number; }
export interface LaneConnection { readonly id: bigint; readonly fromLaneId: bigint; readonly toLaneId: bigint; readonly viaNodeId: bigint; readonly movement: TurnMovement; }
export interface RoadAccessPoint { readonly id: bigint; readonly segmentId: bigint; readonly segmentOffset: number; readonly buildingId: bigint | null; readonly poiId: bigint | null; readonly mode: RoadAccessMode; }
export interface RoadNetworkSnapshotMessage { readonly type: MessageType.RoadNetworkSnapshot; readonly tickCount: bigint; readonly nodes: readonly RoadNode[]; readonly segments: readonly RoadSegment[]; readonly lanes: readonly Lane[]; readonly connections: readonly LaneConnection[]; readonly accessPoints: readonly RoadAccessPoint[]; }
export type ProtocolMessage = HelloMessage | HelloAckMessage | SubscribeVolumeMessage | AgentStateMessage | AgentRemoveMessage | PedestrianStateMessage | PedestrianRemoveMessage | RoadNetworkSnapshotMessage | ProtocolErrorMessage;
export interface ProtocolEnvelope { readonly version: ProtocolVersion; readonly message: ProtocolMessage; }

export class ProtocolDecodeFailure extends Error {
  public constructor(message: string) { super(message); this.name = 'ProtocolDecodeFailure'; }
}

const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

export function encodeHello(version: ProtocolVersion = CURRENT_PROTOCOL_VERSION): ArrayBuffer { return createFrame(MessageType.Hello, 0, version); }

export function encodeSubscribeVolume(volume: WorldVolume, version: ProtocolVersion = CURRENT_PROTOCOL_VERSION): ArrayBuffer {
  validateWorldVolume(volume);
  const frame = createFrame(MessageType.SubscribeVolume, 48, version);
  const view = new DataView(frame);
  view.setFloat64(PROTOCOL_HEADER_SIZE, volume.minX, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 8, volume.minY, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 16, volume.minZ, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 24, volume.maxX, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 32, volume.maxY, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 40, volume.maxZ, true);
  return frame;
}

export function decodeFrame(frame: ArrayBuffer): ProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Protocol frame is shorter than the 16-byte header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Protocol frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Protocol frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Protocol payload exceeds the supported limit.');
  if (PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Protocol frame length does not match its payload length.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  const messageType = view.getUint16(8, true) as MessageType;
  if (messageType === MessageType.RoadNetworkSnapshot && !supports(version, 1)) throw new ProtocolDecodeFailure('Road Network snapshots require Protocol 2.1 or newer.');
  if (isPedestrianMessage(messageType) && !supports(version, 2)) throw new ProtocolDecodeFailure('Pedestrian messages require Protocol 2.2 or newer.');
  return { version, message: decodeMessage(view, messageType, PROTOCOL_HEADER_SIZE, payloadLength) };
}

export function protocolVersionToString(version: ProtocolVersion): string { return `${String(version.major)}.${String(version.minor)}`; }

function supports(version: ProtocolVersion, minimumMinor: number): boolean { return version.major === 2 && version.minor >= minimumMinor; }
function isPedestrianMessage(type: MessageType): boolean { return type === MessageType.PedestrianSpawn || type === MessageType.PedestrianUpdate || type === MessageType.PedestrianRemove; }

function createFrame(messageType: MessageType, payloadLength: number, version: ProtocolVersion): ArrayBuffer {
  validateUInt16(version.major, 'Protocol major version');
  validateUInt16(version.minor, 'Protocol minor version');
  if (!Number.isInteger(payloadLength) || payloadLength < 0 || payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH) throw new RangeError('Protocol payload length is invalid.');
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, messageType, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  return frame;
}

function decodeMessage(view: DataView, messageType: MessageType, offset: number, payloadLength: number): ProtocolMessage {
  switch (messageType) {
    case MessageType.Hello:
      assertPayloadLength(payloadLength, 0, messageType);
      return { type: MessageType.Hello };
    case MessageType.HelloAck:
      assertPayloadLength(payloadLength, 6, messageType);
      return { type: MessageType.HelloAck, protocolVersion: { major: view.getUint16(offset, true), minor: view.getUint16(offset + 2, true) }, tickRate: view.getUint16(offset + 4, true) };
    case MessageType.SubscribeVolume: {
      assertPayloadLength(payloadLength, 48, messageType);
      const volume: WorldVolume = { minX: view.getFloat64(offset, true), minY: view.getFloat64(offset + 8, true), minZ: view.getFloat64(offset + 16, true), maxX: view.getFloat64(offset + 24, true), maxY: view.getFloat64(offset + 32, true), maxZ: view.getFloat64(offset + 40, true) };
      validateWorldVolume(volume);
      return { type: MessageType.SubscribeVolume, ...volume };
    }
    case MessageType.AgentSpawn:
    case MessageType.AgentUpdate:
      return decodeAgentState(view, messageType, offset, payloadLength);
    case MessageType.AgentRemove:
      assertPayloadLength(payloadLength, 16, messageType);
      return { type: MessageType.AgentRemove, agentId: view.getBigUint64(offset, true), tickCount: view.getBigUint64(offset + 8, true) };
    case MessageType.PedestrianSpawn:
    case MessageType.PedestrianUpdate:
      return decodePedestrianState(view, messageType, offset, payloadLength);
    case MessageType.PedestrianRemove: {
      assertPayloadLength(payloadLength, 16, messageType);
      const pedestrianId = view.getBigUint64(offset, true);
      assertStableId(pedestrianId, 'Pedestrian');
      return { type: MessageType.PedestrianRemove, pedestrianId, tickCount: view.getBigUint64(offset + 8, true) };
    }
    case MessageType.RoadNetworkSnapshot:
      return decodeRoadNetwork(view, offset, payloadLength);
    case MessageType.Error:
      return decodeProtocolError(view, offset, payloadLength);
    default:
      throw new ProtocolDecodeFailure(`Unknown protocol message type: ${String(messageType)}.`);
  }
}

function decodeAgentState(view: DataView, type: MessageType.AgentSpawn | MessageType.AgentUpdate, offset: number, payloadLength: number): AgentStateMessage {
  assertPayloadLength(payloadLength, 64, type);
  const message: AgentStateMessage = { type, agentId: view.getBigUint64(offset, true), x: view.getFloat64(offset + 8, true), y: view.getFloat64(offset + 16, true), z: view.getFloat64(offset + 24, true), velocityX: view.getFloat64(offset + 32, true), velocityY: view.getFloat64(offset + 40, true), velocityZ: view.getFloat64(offset + 48, true), tickCount: view.getBigUint64(offset + 56, true) };
  if (!Number.isFinite(message.x) || !Number.isFinite(message.y) || !Number.isFinite(message.z) || !Number.isFinite(message.velocityX) || !Number.isFinite(message.velocityY) || !Number.isFinite(message.velocityZ)) throw new ProtocolDecodeFailure('Agent state contains a non-finite value.');
  return message;
}

function decodePedestrianState(view: DataView, type: MessageType.PedestrianSpawn | MessageType.PedestrianUpdate, offset: number, payloadLength: number): PedestrianStateMessage {
  assertPayloadLength(payloadLength, PEDESTRIAN_STATE_LENGTH, type);
  const pedestrianId = view.getBigUint64(offset, true);
  const tripRequestId = view.getBigUint64(offset + 8, true);
  const message: PedestrianStateMessage = {
    type,
    pedestrianId,
    tripRequestId,
    x: view.getFloat64(offset + 16, true),
    y: view.getFloat64(offset + 24, true),
    z: view.getFloat64(offset + 32, true),
    velocityX: view.getFloat64(offset + 40, true),
    velocityY: view.getFloat64(offset + 48, true),
    velocityZ: view.getFloat64(offset + 56, true),
    walkingSpeedMetersPerSecond: view.getFloat64(offset + 64, true),
    state: view.getUint8(offset + 72) as PedestrianMovementState,
    tickCount: view.getBigUint64(offset + 73, true),
  };
  assertStableId(pedestrianId, 'Pedestrian');
  assertStableId(tripRequestId, 'TripRequest');
  if (!Number.isFinite(message.x) || !Number.isFinite(message.y) || !Number.isFinite(message.z) || !Number.isFinite(message.velocityX) || !Number.isFinite(message.velocityY) || !Number.isFinite(message.velocityZ) || !Number.isFinite(message.walkingSpeedMetersPerSecond) || message.walkingSpeedMetersPerSecond <= 0 || !isPedestrianMovementState(message.state)) throw new ProtocolDecodeFailure('Pedestrian state payload is invalid.');
  return message;
}

function decodeRoadNetwork(view: DataView, offset: number, payloadLength: number): RoadNetworkSnapshotMessage {
  if (payloadLength < ROAD_HEADER_LENGTH) throw new ProtocolDecodeFailure('Road Network payload is too short.');
  const tickCount = view.getBigUint64(offset, true);
  const nodeCount = view.getUint32(offset + 8, true);
  const segmentCount = view.getUint32(offset + 12, true);
  const laneCount = view.getUint32(offset + 16, true);
  const connectionCount = view.getUint32(offset + 20, true);
  const accessPointCount = view.getUint32(offset + 24, true);
  const expectedLength = ROAD_HEADER_LENGTH + nodeCount * ROAD_NODE_LENGTH + segmentCount * ROAD_SEGMENT_LENGTH + laneCount * LANE_LENGTH + connectionCount * LANE_CONNECTION_LENGTH + accessPointCount * ROAD_ACCESS_POINT_LENGTH;
  if (expectedLength !== payloadLength) throw new ProtocolDecodeFailure('Road Network payload counts do not match its length.');

  let cursor = offset + ROAD_HEADER_LENGTH;
  const nodes: RoadNode[] = [];
  for (let index = 0; index < nodeCount; index += 1) {
    const id = view.getBigUint64(cursor, true); const kind = view.getUint8(cursor + 8) as RoadNodeKind; const x = view.getFloat64(cursor + 9, true); const y = view.getFloat64(cursor + 17, true); const z = view.getFloat64(cursor + 25, true);
    assertStableId(id, 'RoadNode');
    if (!isRoadNodeKind(kind) || !Number.isFinite(x) || !Number.isFinite(y) || !Number.isFinite(z)) throw new ProtocolDecodeFailure('RoadNode payload is invalid.');
    nodes.push({ id, kind, x, y, z }); cursor += ROAD_NODE_LENGTH;
  }

  const segments: RoadSegment[] = [];
  for (let index = 0; index < segmentCount; index += 1) {
    const id = view.getBigUint64(cursor, true); const kind = view.getUint8(cursor + 8) as RoadKind; const startNodeId = view.getBigUint64(cursor + 9, true); const endNodeId = view.getBigUint64(cursor + 17, true);
    assertStableId(id, 'RoadSegment'); assertStableId(startNodeId, 'RoadSegment start node'); assertStableId(endNodeId, 'RoadSegment end node');
    if (!isRoadKind(kind) || startNodeId === endNodeId) throw new ProtocolDecodeFailure('RoadSegment payload is invalid.');
    segments.push({ id, kind, startNodeId, endNodeId }); cursor += ROAD_SEGMENT_LENGTH;
  }

  const lanes: Lane[] = [];
  for (let index = 0; index < laneCount; index += 1) {
    const id = view.getBigUint64(cursor, true); const segmentId = view.getBigUint64(cursor + 8, true); const direction = view.getUint8(cursor + 16) as LaneDirection; const order = view.getUint16(cursor + 17, true); const widthMeters = view.getFloat64(cursor + 19, true); const speedLimitMetersPerSecond = view.getFloat64(cursor + 27, true);
    assertStableId(id, 'Lane'); assertStableId(segmentId, 'Lane segment');
    if (!isLaneDirection(direction) || !Number.isFinite(widthMeters) || widthMeters <= 0 || !Number.isFinite(speedLimitMetersPerSecond) || speedLimitMetersPerSecond <= 0) throw new ProtocolDecodeFailure('Lane payload is invalid.');
    lanes.push({ id, segmentId, direction, order, widthMeters, speedLimitMetersPerSecond }); cursor += LANE_LENGTH;
  }

  const connections: LaneConnection[] = [];
  for (let index = 0; index < connectionCount; index += 1) {
    const id = view.getBigUint64(cursor, true); const fromLaneId = view.getBigUint64(cursor + 8, true); const toLaneId = view.getBigUint64(cursor + 16, true); const viaNodeId = view.getBigUint64(cursor + 24, true); const movement = view.getUint8(cursor + 32) as TurnMovement;
    assertStableId(id, 'LaneConnection'); assertStableId(fromLaneId, 'LaneConnection from lane'); assertStableId(toLaneId, 'LaneConnection to lane'); assertStableId(viaNodeId, 'LaneConnection via node');
    if (fromLaneId === toLaneId || !isTurnMovement(movement)) throw new ProtocolDecodeFailure('LaneConnection payload is invalid.');
    connections.push({ id, fromLaneId, toLaneId, viaNodeId, movement }); cursor += LANE_CONNECTION_LENGTH;
  }

  const accessPoints: RoadAccessPoint[] = [];
  for (let index = 0; index < accessPointCount; index += 1) {
    const id = view.getBigUint64(cursor, true); const segmentId = view.getBigUint64(cursor + 8, true); const segmentOffset = view.getFloat64(cursor + 16, true); const rawBuildingId = view.getBigUint64(cursor + 24, true); const rawPoiId = view.getBigUint64(cursor + 32, true); const mode = view.getUint8(cursor + 40) as RoadAccessMode;
    assertStableId(id, 'RoadAccessPoint'); assertStableId(segmentId, 'RoadAccessPoint segment');
    if (!Number.isFinite(segmentOffset) || segmentOffset < 0 || segmentOffset > 1 || (rawBuildingId === 0n && rawPoiId === 0n) || !isRoadAccessMode(mode)) throw new ProtocolDecodeFailure('RoadAccessPoint payload is invalid.');
    accessPoints.push({ id, segmentId, segmentOffset, buildingId: rawBuildingId === 0n ? null : rawBuildingId, poiId: rawPoiId === 0n ? null : rawPoiId, mode }); cursor += ROAD_ACCESS_POINT_LENGTH;
  }

  validateRoadNetworkReferences(nodes, segments, lanes, connections, accessPoints);
  return { type: MessageType.RoadNetworkSnapshot, tickCount, nodes, segments, lanes, connections, accessPoints };
}

function validateRoadNetworkReferences(nodes: readonly RoadNode[], segments: readonly RoadSegment[], lanes: readonly Lane[], connections: readonly LaneConnection[], accessPoints: readonly RoadAccessPoint[]): void {
  const nodeIds = uniqueIds(nodes, 'RoadNode'); const segmentIds = uniqueIds(segments, 'RoadSegment'); const laneIds = uniqueIds(lanes, 'Lane'); uniqueIds(connections, 'LaneConnection'); uniqueIds(accessPoints, 'RoadAccessPoint');
  for (const segment of segments) if (!nodeIds.has(segment.startNodeId) || !nodeIds.has(segment.endNodeId)) throw new ProtocolDecodeFailure('RoadSegment references a missing RoadNode.');
  for (const lane of lanes) if (!segmentIds.has(lane.segmentId)) throw new ProtocolDecodeFailure('Lane references a missing RoadSegment.');
  for (const connection of connections) if (!laneIds.has(connection.fromLaneId) || !laneIds.has(connection.toLaneId) || !nodeIds.has(connection.viaNodeId)) throw new ProtocolDecodeFailure('LaneConnection contains a dangling reference.');
  for (const accessPoint of accessPoints) if (!segmentIds.has(accessPoint.segmentId)) throw new ProtocolDecodeFailure('RoadAccessPoint references a missing RoadSegment.');
}

function uniqueIds<T extends { readonly id: bigint }>(items: readonly T[], label: string): Set<bigint> {
  const ids = new Set<bigint>();
  for (const item of items) { if (ids.has(item.id)) throw new ProtocolDecodeFailure(`${label} IDs are duplicated.`); ids.add(item.id); }
  return ids;
}

function decodeProtocolError(view: DataView, offset: number, payloadLength: number): ProtocolErrorMessage {
  if (payloadLength < 4) throw new ProtocolDecodeFailure('Protocol error payload is too short.');
  const end = offset + payloadLength; const code = view.getUint16(offset, true) as ProtocolErrorCode; const parameterCount = view.getUint16(offset + 2, true);
  if (parameterCount > 16) throw new ProtocolDecodeFailure('Protocol error contains too many parameters.');
  let cursor = offset + 4; const parameters: ProtocolErrorParameter[] = [];
  for (let index = 0; index < parameterCount; index += 1) { const key = readUtf8String(view, cursor, end, 64); cursor = key.nextOffset; const value = readUtf8String(view, cursor, end, 256); cursor = value.nextOffset; parameters.push({ key: key.value, value: value.value }); }
  if (cursor !== end) throw new ProtocolDecodeFailure('Protocol error payload contains trailing bytes.');
  return { type: MessageType.Error, code, parameters };
}

function readUtf8String(view: DataView, offset: number, end: number, maximumByteLength: number): { readonly value: string; readonly nextOffset: number } {
  if (offset + 2 > end) throw new ProtocolDecodeFailure('Protocol string length is truncated.');
  const byteLength = view.getUint16(offset, true); const valueOffset = offset + 2; const nextOffset = valueOffset + byteLength;
  if (byteLength > maximumByteLength || nextOffset > end) throw new ProtocolDecodeFailure('Protocol string exceeds its allowed bounds.');
  try { return { value: utf8Decoder.decode(new Uint8Array(view.buffer, view.byteOffset + valueOffset, byteLength)), nextOffset }; }
  catch { throw new ProtocolDecodeFailure('Protocol string is not valid UTF-8.'); }
}

function validateWorldVolume(volume: WorldVolume): void {
  if (!Number.isFinite(volume.minX) || !Number.isFinite(volume.minY) || !Number.isFinite(volume.minZ) || !Number.isFinite(volume.maxX) || !Number.isFinite(volume.maxY) || !Number.isFinite(volume.maxZ) || volume.maxX < volume.minX || volume.maxY < volume.minY || volume.maxZ < volume.minZ) throw new RangeError('World volume coordinates must be finite and ordered.');
}

function validateUInt16(value: number, label: string): void { if (!Number.isInteger(value) || value < 0 || value > 0xffff) throw new RangeError(`${label} must fit in an unsigned 16-bit integer.`); }
function assertPayloadLength(actual: number, expected: number, type: MessageType): void { if (actual !== expected) throw new ProtocolDecodeFailure(`Protocol message ${String(type)} has payload length ${String(actual)}; expected ${String(expected)}.`); }
function assertStableId(value: bigint, label: string): void { if (value === 0n) throw new ProtocolDecodeFailure(`${label} ID must be greater than zero.`); }
function isRoadNodeKind(value: number): value is RoadNodeKind { return value >= RoadNodeKind.Endpoint && value <= RoadNodeKind.Intersection; }
function isRoadKind(value: number): value is RoadKind { return value >= RoadKind.Local && value <= RoadKind.Service; }
function isLaneDirection(value: number): value is LaneDirection { return value === LaneDirection.Forward || value === LaneDirection.Reverse; }
function isTurnMovement(value: number): value is TurnMovement { return value >= TurnMovement.Unspecified && value <= TurnMovement.UTurn; }
function isRoadAccessMode(value: number): value is RoadAccessMode { return value >= RoadAccessMode.Motor && value <= (RoadAccessMode.Motor | RoadAccessMode.Foot); }
function isPedestrianMovementState(value: number): value is PedestrianMovementState { return value >= PedestrianMovementState.Walking && value <= PedestrianMovementState.Arrived; }
