import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  TurnMovement,
  type ProtocolVersion,
} from './protocol.ts';

const VEHICLE_STATE_LENGTH = 105;
const INTERSECTION_HEADER_LENGTH = 31;
const INTERSECTION_MOVEMENT_LENGTH = 63;

export const WEB_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 4 });

export enum TrafficMessageType {
  VehicleSpawn = 400,
  VehicleUpdate = 401,
  VehicleRemove = 402,
  IntersectionControlSnapshot = 500,
}

export enum VehicleMovementState { Driving = 0, WaitingForTraffic = 1, ChangingLane = 2, Arrived = 3 }
export enum IntersectionControlMode { Unsignalized = 0, FixedSignal = 1 }
export enum SignalIndication { Red = 0, Yellow = 1, Green = 2 }

export interface VehicleStateMessage {
  readonly type: TrafficMessageType.VehicleSpawn | TrafficMessageType.VehicleUpdate;
  readonly vehicleId: bigint;
  readonly laneId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly forwardX: number;
  readonly forwardY: number;
  readonly forwardZ: number;
  readonly speedMetersPerSecond: number;
  readonly lengthMeters: number;
  readonly widthMeters: number;
  readonly heightMeters: number;
  readonly state: VehicleMovementState;
  readonly tickCount: bigint;
}

export interface VehicleRemoveMessage {
  readonly type: TrafficMessageType.VehicleRemove;
  readonly vehicleId: bigint;
  readonly tickCount: bigint;
}

export interface IntersectionMovementState {
  readonly movementId: bigint;
  readonly connectionId: bigint;
  readonly fromLaneId: bigint;
  readonly toLaneId: bigint;
  readonly turnMovement: TurnMovement;
  readonly stopLineX: number;
  readonly stopLineY: number;
  readonly stopLineZ: number;
  readonly indication: SignalIndication;
  readonly queueLength: number;
  readonly entryGrantedThisTick: boolean;
}

export interface IntersectionControlSnapshotMessage {
  readonly type: TrafficMessageType.IntersectionControlSnapshot;
  readonly tickCount: bigint;
  readonly intersectionNodeId: bigint;
  readonly mode: IntersectionControlMode;
  readonly phaseIndex: number;
  readonly phaseTick: bigint;
  readonly movements: readonly IntersectionMovementState[];
}

export type TrafficProtocolMessage = VehicleStateMessage | VehicleRemoveMessage | IntersectionControlSnapshotMessage;
export interface TrafficProtocolEnvelope { readonly version: ProtocolVersion; readonly message: TrafficProtocolMessage; }

export function isTrafficFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const type = new DataView(frame).getUint16(8, true);
  return type === TrafficMessageType.VehicleSpawn
    || type === TrafficMessageType.VehicleUpdate
    || type === TrafficMessageType.VehicleRemove
    || type === TrafficMessageType.IntersectionControlSnapshot;
}

export function decodeTrafficFrame(frame: ArrayBuffer): TrafficProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Traffic frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Traffic frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Traffic frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Traffic frame payload length is invalid.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  const type = view.getUint16(8, true) as TrafficMessageType;
  if (version.major !== 2 || (isVehicleType(type) ? version.minor < 3 : version.minor < 4)) throw new ProtocolDecodeFailure('Traffic frame requires a newer protocol version.');
  const offset = PROTOCOL_HEADER_SIZE;
  if (type === TrafficMessageType.VehicleSpawn || type === TrafficMessageType.VehicleUpdate) return { version, message: decodeVehicleState(view, type, offset, payloadLength) };
  if (type === TrafficMessageType.VehicleRemove) {
    assertPayloadLength(payloadLength, 16);
    const vehicleId = view.getBigUint64(offset, true);
    assertStableId(vehicleId, 'Vehicle');
    return { version, message: { type, vehicleId, tickCount: view.getBigUint64(offset + 8, true) } };
  }
  if (type === TrafficMessageType.IntersectionControlSnapshot) return { version, message: decodeIntersection(view, offset, payloadLength) };
  throw new ProtocolDecodeFailure(`Unknown traffic message type: ${String(type)}.`);
}

function decodeVehicleState(
  view: DataView,
  type: TrafficMessageType.VehicleSpawn | TrafficMessageType.VehicleUpdate,
  offset: number,
  payloadLength: number,
): VehicleStateMessage {
  assertPayloadLength(payloadLength, VEHICLE_STATE_LENGTH);
  const message: VehicleStateMessage = {
    type,
    vehicleId: view.getBigUint64(offset, true),
    laneId: view.getBigUint64(offset + 8, true),
    x: view.getFloat64(offset + 16, true),
    y: view.getFloat64(offset + 24, true),
    z: view.getFloat64(offset + 32, true),
    forwardX: view.getFloat64(offset + 40, true),
    forwardY: view.getFloat64(offset + 48, true),
    forwardZ: view.getFloat64(offset + 56, true),
    speedMetersPerSecond: view.getFloat64(offset + 64, true),
    lengthMeters: view.getFloat64(offset + 72, true),
    widthMeters: view.getFloat64(offset + 80, true),
    heightMeters: view.getFloat64(offset + 88, true),
    state: view.getUint8(offset + 96) as VehicleMovementState,
    tickCount: view.getBigUint64(offset + 97, true),
  };
  assertStableId(message.vehicleId, 'Vehicle');
  assertStableId(message.laneId, 'Lane');
  const forwardLengthSquared = message.forwardX ** 2 + message.forwardY ** 2 + message.forwardZ ** 2;
  if (![message.x, message.y, message.z, message.forwardX, message.forwardY, message.forwardZ, message.speedMetersPerSecond, message.lengthMeters, message.widthMeters, message.heightMeters].every(Number.isFinite)
    || !Number.isFinite(forwardLengthSquared)
    || forwardLengthSquared <= 1e-12
    || message.speedMetersPerSecond < 0
    || message.lengthMeters <= 0
    || message.widthMeters <= 0
    || message.heightMeters <= 0
    || !isVehicleState(message.state)) throw new ProtocolDecodeFailure('Vehicle state payload is invalid.');
  return message;
}

function decodeIntersection(view: DataView, offset: number, payloadLength: number): IntersectionControlSnapshotMessage {
  if (payloadLength < INTERSECTION_HEADER_LENGTH) throw new ProtocolDecodeFailure('Intersection control payload is too short.');
  const tickCount = view.getBigUint64(offset, true);
  const intersectionNodeId = view.getBigUint64(offset + 8, true);
  const mode = view.getUint8(offset + 16) as IntersectionControlMode;
  const phaseIndex = view.getUint16(offset + 17, true);
  const phaseTick = view.getBigUint64(offset + 19, true);
  const movementCount = view.getUint32(offset + 27, true);
  const expectedLength = INTERSECTION_HEADER_LENGTH + movementCount * INTERSECTION_MOVEMENT_LENGTH;
  if (expectedLength !== payloadLength || !isIntersectionMode(mode)) throw new ProtocolDecodeFailure('Intersection control header is invalid.');
  assertStableId(intersectionNodeId, 'Intersection node');

  const movements: IntersectionMovementState[] = [];
  const movementIds = new Set<bigint>();
  const connectionIds = new Set<bigint>();
  let cursor = offset + INTERSECTION_HEADER_LENGTH;
  for (let index = 0; index < movementCount; index += 1) {
    const movement: IntersectionMovementState = {
      movementId: view.getBigUint64(cursor, true),
      connectionId: view.getBigUint64(cursor + 8, true),
      fromLaneId: view.getBigUint64(cursor + 16, true),
      toLaneId: view.getBigUint64(cursor + 24, true),
      turnMovement: view.getUint8(cursor + 32) as TurnMovement,
      stopLineX: view.getFloat64(cursor + 33, true),
      stopLineY: view.getFloat64(cursor + 41, true),
      stopLineZ: view.getFloat64(cursor + 49, true),
      indication: view.getUint8(cursor + 57) as SignalIndication,
      queueLength: view.getUint32(cursor + 58, true),
      entryGrantedThisTick: view.getUint8(cursor + 62) !== 0,
    };
    assertStableId(movement.movementId, 'Movement');
    assertStableId(movement.connectionId, 'LaneConnection');
    assertStableId(movement.fromLaneId, 'From Lane');
    assertStableId(movement.toLaneId, 'To Lane');
    if (movement.movementId !== movement.connectionId || movementIds.has(movement.movementId) || connectionIds.has(movement.connectionId))
      throw new ProtocolDecodeFailure('Intersection movement identity is invalid or duplicated.');
    movementIds.add(movement.movementId);
    connectionIds.add(movement.connectionId);
    if (!isTurnMovement(movement.turnMovement)
      || !isSignalIndication(movement.indication)
      || !Number.isFinite(movement.stopLineX)
      || !Number.isFinite(movement.stopLineY)
      || !Number.isFinite(movement.stopLineZ)
      || view.getUint8(cursor + 62) > 1) throw new ProtocolDecodeFailure('Intersection movement payload is invalid.');
    movements.push(movement);
    cursor += INTERSECTION_MOVEMENT_LENGTH;
  }
  return { type: TrafficMessageType.IntersectionControlSnapshot, tickCount, intersectionNodeId, mode, phaseIndex, phaseTick, movements };
}

function isVehicleType(type: TrafficMessageType): boolean { return type === TrafficMessageType.VehicleSpawn || type === TrafficMessageType.VehicleUpdate || type === TrafficMessageType.VehicleRemove; }
function isVehicleState(value: VehicleMovementState): boolean { return value >= VehicleMovementState.Driving && value <= VehicleMovementState.Arrived; }
function isIntersectionMode(value: IntersectionControlMode): boolean { return value === IntersectionControlMode.Unsignalized || value === IntersectionControlMode.FixedSignal; }
function isSignalIndication(value: SignalIndication): boolean { return value === SignalIndication.Red || value === SignalIndication.Yellow || value === SignalIndication.Green; }
function isTurnMovement(value: TurnMovement): boolean { return value >= TurnMovement.Unspecified && value <= TurnMovement.UTurn; }
function assertStableId(value: bigint, label: string): void { if (value === 0n) throw new ProtocolDecodeFailure(`${label} ID must be greater than zero.`); }
function assertPayloadLength(actual: number, expected: number): void { if (actual !== expected) throw new ProtocolDecodeFailure(`Traffic payload length must be ${String(expected)} bytes.`); }
