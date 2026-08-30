import * as THREE from 'three';

import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

const SNAPSHOT_HEADER_LENGTH = 20;
const TRAIN_LENGTH = 129;
const SERVICE_LENGTH = 77;
const TIMETABLE_HEADER_LENGTH = 12;
const TIMETABLE_STOP_LENGTH = 40;

export enum RailwayOperationsMessageType {
  RailwayOperationsSnapshot = 710,
}

export enum RailwayServiceState { Planned = 0, Active = 1, Completed = 2 }
export enum TrainMovementState { InDepot = 0, WaitingForBlock = 1, Running = 2, ApproachingStation = 3, Dwelling = 4, Completed = 5 }

export interface TrainState {
  readonly id: bigint;
  readonly formationId: bigint;
  readonly serviceId: bigint;
  readonly routeId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly forwardX: number;
  readonly forwardY: number;
  readonly forwardZ: number;
  readonly speedMetersPerSecond: number;
  readonly state: TrainMovementState;
  readonly currentBlockId: bigint | null;
  readonly currentPlatformId: bigint | null;
  readonly assignedPlatformId: bigint | null;
  readonly currentDepotId: bigint | null;
  readonly dwellDepartureTick: bigint;
}

export interface RailwayServiceStateMessage {
  readonly id: bigint;
  readonly formationId: bigint;
  readonly routeId: bigint;
  readonly timetableId: bigint;
  readonly originDepotId: bigint;
  readonly destinationDepotId: bigint;
  readonly plannedStartTick: bigint;
  readonly state: RailwayServiceState;
  readonly delayTicks: bigint;
  readonly nextStopIndex: number;
  readonly trainId: bigint | null;
}

export interface TimetableStop {
  readonly stationId: bigint;
  readonly plannedArrivalTick: bigint;
  readonly plannedDepartureTick: bigint;
  readonly minimumDwellTicks: bigint;
  readonly preferredPlatformId: bigint | null;
}

export interface RailwayTimetable { readonly id: bigint; readonly stops: readonly TimetableStop[]; }

export interface RailwayOperationsSnapshotMessage {
  readonly type: RailwayOperationsMessageType.RailwayOperationsSnapshot;
  readonly tickCount: bigint;
  readonly trains: readonly TrainState[];
  readonly services: readonly RailwayServiceStateMessage[];
  readonly timetables: readonly RailwayTimetable[];
}

export type RailwayOperationsProtocolMessage = RailwayOperationsSnapshotMessage;
export interface RailwayOperationsProtocolEnvelope { readonly version: ProtocolVersion; readonly message: RailwayOperationsProtocolMessage; }

export function isRailwayOperationsFrame(frame: ArrayBuffer): boolean {
  return frame.byteLength >= PROTOCOL_HEADER_SIZE
    && new DataView(frame).getUint16(8, true) === RailwayOperationsMessageType.RailwayOperationsSnapshot;
}

export function decodeRailwayOperationsFrame(frame: ArrayBuffer): RailwayOperationsProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Railway operations frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Railway operations frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Railway operations frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Railway operations frame payload length is invalid.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 7) throw new ProtocolDecodeFailure('Railway operations frames require Protocol 2.7 or newer.');
  if (view.getUint16(8, true) !== RailwayOperationsMessageType.RailwayOperationsSnapshot) throw new ProtocolDecodeFailure('Unknown railway operations message type.');
  return { version, message: decodeSnapshot(view, PROTOCOL_HEADER_SIZE, payloadLength) };
}

function decodeSnapshot(view: DataView, offset: number, payloadLength: number): RailwayOperationsSnapshotMessage {
  if (payloadLength < SNAPSHOT_HEADER_LENGTH) throw new ProtocolDecodeFailure('Railway operations payload is too short.');
  const end = offset + payloadLength;
  let cursor = offset;
  const requireBytes = (count: number): void => { if (!Number.isSafeInteger(count) || count < 0 || cursor + count > end) throw new ProtocolDecodeFailure('Railway operations payload is truncated.'); };
  const readByte = (): number => { requireBytes(1); return view.getUint8(cursor++); };
  const readInt32 = (): number => { requireBytes(4); const value = view.getInt32(cursor, true); cursor += 4; return value; };
  const readUint32 = (): number => { requireBytes(4); const value = view.getUint32(cursor, true); cursor += 4; return value; };
  const readUint64 = (): bigint => { requireBytes(8); const value = view.getBigUint64(cursor, true); cursor += 8; return value; };
  const readDouble = (): number => { requireBytes(8); const value = view.getFloat64(cursor, true); cursor += 8; return value; };
  const nullableId = (value: bigint): bigint | null => value === 0n ? null : value;

  const tickCount = readUint64();
  const trainCount = readUint32();
  const serviceCount = readUint32();
  const timetableCount = readUint32();
  const fixedBytes = trainCount * TRAIN_LENGTH + serviceCount * SERVICE_LENGTH + timetableCount * TIMETABLE_HEADER_LENGTH;
  if (!Number.isSafeInteger(fixedBytes) || fixedBytes > end - cursor) throw new ProtocolDecodeFailure('Railway operations counts exceed payload length.');

  const trains: TrainState[] = [];
  for (let index = 0; index < trainCount; index += 1) {
    const item: TrainState = {
      id: readUint64(), formationId: readUint64(), serviceId: readUint64(), routeId: readUint64(),
      x: readDouble(), y: readDouble(), z: readDouble(), forwardX: readDouble(), forwardY: readDouble(), forwardZ: readDouble(),
      speedMetersPerSecond: readDouble(), state: readByte() as TrainMovementState,
      currentBlockId: nullableId(readUint64()), currentPlatformId: nullableId(readUint64()), assignedPlatformId: nullableId(readUint64()), currentDepotId: nullableId(readUint64()), dwellDepartureTick: readUint64(),
    };
    if (item.id === 0n || item.formationId === 0n || item.serviceId === 0n || item.routeId === 0n || item.state < TrainMovementState.InDepot || item.state > TrainMovementState.Completed
      || !finite3(item.x, item.y, item.z) || !finite3(item.forwardX, item.forwardY, item.forwardZ) || !Number.isFinite(item.speedMetersPerSecond) || item.speedMetersPerSecond < 0) throw new ProtocolDecodeFailure('Train payload is invalid.');
    trains.push(item);
  }

  const services: RailwayServiceStateMessage[] = [];
  for (let index = 0; index < serviceCount; index += 1) {
    const item: RailwayServiceStateMessage = {
      id: readUint64(), formationId: readUint64(), routeId: readUint64(), timetableId: readUint64(), originDepotId: readUint64(), destinationDepotId: readUint64(), plannedStartTick: readUint64(),
      state: readByte() as RailwayServiceState, delayTicks: readUint64(), nextStopIndex: readInt32(), trainId: nullableId(readUint64()),
    };
    if (item.id === 0n || item.formationId === 0n || item.routeId === 0n || item.timetableId === 0n || item.originDepotId === 0n || item.destinationDepotId === 0n
      || item.state < RailwayServiceState.Planned || item.state > RailwayServiceState.Completed || item.nextStopIndex < 0) throw new ProtocolDecodeFailure('Railway service payload is invalid.');
    services.push(item);
  }

  const timetables: RailwayTimetable[] = [];
  for (let index = 0; index < timetableCount; index += 1) {
    const id = readUint64(); const stopCount = readUint32();
    if (id === 0n || stopCount === 0 || stopCount > Math.floor((end - cursor) / TIMETABLE_STOP_LENGTH)) throw new ProtocolDecodeFailure('Railway timetable payload is invalid.');
    const stops: TimetableStop[] = [];
    let previousDeparture = 0n;
    for (let stopIndex = 0; stopIndex < stopCount; stopIndex += 1) {
      const stationId = readUint64(); const plannedArrivalTick = readUint64(); const plannedDepartureTick = readUint64(); const minimumDwellTicks = readUint64(); const preferredPlatformId = nullableId(readUint64());
      if (stationId === 0n || plannedDepartureTick < plannedArrivalTick || (stopIndex > 0 && plannedArrivalTick < previousDeparture)) throw new ProtocolDecodeFailure('Railway timetable stop payload is invalid.');
      previousDeparture = plannedDepartureTick;
      stops.push({ stationId, plannedArrivalTick, plannedDepartureTick, minimumDwellTicks, preferredPlatformId });
    }
    timetables.push({ id, stops });
  }
  if (cursor !== end) throw new ProtocolDecodeFailure('Railway operations payload contains trailing bytes.');
  return { type: RailwayOperationsMessageType.RailwayOperationsSnapshot, tickCount, trains, services, timetables };
}

export class RailwayOperationsLayer {
  private readonly group = new THREE.Group();
  private readonly geometry = new THREE.BoxGeometry(18, 3, 3);
  private readonly material = new THREE.MeshBasicMaterial();
  private readonly meshes = new Map<bigint, THREE.Mesh>();
  private readonly direction = new THREE.Vector3();
  private readonly forwardAxis = new THREE.Vector3(1, 0, 0);

  public constructor(private readonly scene: THREE.Scene) {
    this.group.name = 'railway-trains';
    this.group.frustumCulled = false;
    this.scene.add(this.group);
  }

  public apply(snapshot: RailwayOperationsSnapshotMessage): void {
    const activeIds = new Set<bigint>();
    for (const train of snapshot.trains) {
      activeIds.add(train.id);
      let mesh = this.meshes.get(train.id);
      if (mesh === undefined) {
        mesh = new THREE.Mesh(this.geometry, this.material);
        mesh.name = `train-${train.id.toString()}`;
        mesh.frustumCulled = false;
        this.meshes.set(train.id, mesh);
        this.group.add(mesh);
      }
      mesh.position.set(train.x, train.z, train.y);
      this.direction.set(train.forwardX, train.forwardZ, train.forwardY);
      if (this.direction.lengthSq() > 1e-12) mesh.quaternion.setFromUnitVectors(this.forwardAxis, this.direction.normalize());
      mesh.userData.state = train.state;
      mesh.userData.serviceId = train.serviceId.toString();
      mesh.userData.platformId = (train.currentPlatformId ?? train.assignedPlatformId)?.toString() ?? '';
    }
    for (const [id, mesh] of this.meshes) {
      if (activeIds.has(id)) continue;
      this.group.remove(mesh);
      this.meshes.delete(id);
    }
    this.group.userData.tickCount = snapshot.tickCount.toString();
    this.group.userData.delayedServices = snapshot.services.filter((service) => service.delayTicks > 0n).length;
    this.group.userData.completedServices = snapshot.services.filter((service) => service.state === RailwayServiceState.Completed).length;
  }

  public clear(): void {
    for (const mesh of this.meshes.values()) this.group.remove(mesh);
    this.meshes.clear();
    this.group.userData.tickCount = '0';
    this.group.userData.delayedServices = 0;
    this.group.userData.completedServices = 0;
  }

  public dispose(): void {
    this.clear();
    this.scene.remove(this.group);
    this.geometry.dispose();
    this.material.dispose();
  }
}

function finite3(x: number, y: number, z: number): boolean { return Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z); }
