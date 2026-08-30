import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

const SNAPSHOT_HEADER_LENGTH = 28;
const LINE_LENGTH = 9;
const STOP_LENGTH = 57;
const PATTERN_HEADER_LENGTH = 28;
const PATTERN_STOP_LENGTH = 24;
const VEHICLE_LENGTH = 70;
const ARRIVAL_LENGTH = 32;

export const WEB_MULTIMODAL_TRANSIT_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 8 });

export enum MultimodalTransitMessageType { MultimodalTransitSnapshot = 720 }
export enum TransitMode { Walk = 0, Bus = 1, Railway = 2, Taxi = 3, Motor = 4 }
export enum TransitStopKind { Bus = 0, Railway = 1 }
export enum TransitVehicleKind { Bus = 0, Taxi = 1 }
export enum TransitVehicleState { Idle = 0, AwaitingDeparture = 1, EnRouteToStop = 2, Dwelling = 3, EnRouteToPickup = 4, EnRouteToDropOff = 5, Completed = 6 }

export interface TransitLine { readonly id: bigint; readonly mode: TransitMode; }
export interface TransitStop { readonly id: bigint; readonly kind: TransitStopKind; readonly x: number; readonly y: number; readonly z: number; readonly laneId: bigint | null; readonly stationId: bigint | null; readonly platformId: bigint | null; }
export interface TransitPatternStop { readonly stopId: bigint; readonly travelTicksFromPrevious: bigint; readonly dwellTicks: bigint; }
export interface TransitPattern { readonly id: bigint; readonly lineId: bigint; readonly railwayServiceId: bigint | null; readonly stops: readonly TransitPatternStop[]; }
export interface TransitVehicle { readonly id: bigint; readonly kind: TransitVehicleKind; readonly tripId: bigint | null; readonly roadVehicleId: bigint | null; readonly stopIndex: number; readonly x: number; readonly y: number; readonly z: number; readonly state: TransitVehicleState; readonly estimatedArrivalTick: bigint; readonly dwellUntilTick: bigint; }
export interface TransitArrivalEstimate { readonly stopId: bigint; readonly lineId: bigint; readonly vehicleId: bigint; readonly estimatedArrivalTick: bigint; }
export interface MultimodalTransitSnapshotMessage { readonly type: MultimodalTransitMessageType.MultimodalTransitSnapshot; readonly tickCount: bigint; readonly lines: readonly TransitLine[]; readonly stops: readonly TransitStop[]; readonly patterns: readonly TransitPattern[]; readonly vehicles: readonly TransitVehicle[]; readonly arrivalEstimates: readonly TransitArrivalEstimate[]; }
export type MultimodalTransitProtocolMessage = MultimodalTransitSnapshotMessage;
export interface MultimodalTransitProtocolEnvelope { readonly version: ProtocolVersion; readonly message: MultimodalTransitProtocolMessage; }

export function isMultimodalTransitFrame(frame: ArrayBuffer): boolean {
  return frame.byteLength >= PROTOCOL_HEADER_SIZE && new DataView(frame).getUint16(8, true) === MultimodalTransitMessageType.MultimodalTransitSnapshot;
}

export function decodeMultimodalTransitFrame(frame: ArrayBuffer): MultimodalTransitProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Multimodal Transit frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Multimodal Transit frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Multimodal Transit frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Multimodal Transit frame payload length is invalid.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 8) throw new ProtocolDecodeFailure('Multimodal Transit frames require Protocol 2.8 or newer.');
  if (view.getUint16(8, true) !== MultimodalTransitMessageType.MultimodalTransitSnapshot) throw new ProtocolDecodeFailure('Unknown Multimodal Transit message type.');
  return { version, message: decodeSnapshot(view, PROTOCOL_HEADER_SIZE, payloadLength) };
}

function decodeSnapshot(view: DataView, offset: number, payloadLength: number): MultimodalTransitSnapshotMessage {
  if (payloadLength < SNAPSHOT_HEADER_LENGTH) throw new ProtocolDecodeFailure('Multimodal Transit payload is too short.');
  const end = offset + payloadLength;
  let cursor = offset;
  const requireBytes = (count: number): void => { if (!Number.isSafeInteger(count) || count < 0 || cursor + count > end) throw new ProtocolDecodeFailure('Multimodal Transit payload is truncated.'); };
  const readByte = (): number => { requireBytes(1); return view.getUint8(cursor++); };
  const readInt32 = (): number => { requireBytes(4); const value = view.getInt32(cursor, true); cursor += 4; return value; };
  const readUint32 = (): number => { requireBytes(4); const value = view.getUint32(cursor, true); cursor += 4; return value; };
  const readUint64 = (): bigint => { requireBytes(8); const value = view.getBigUint64(cursor, true); cursor += 8; return value; };
  const readDouble = (): number => { requireBytes(8); const value = view.getFloat64(cursor, true); cursor += 8; return value; };
  const readCount = (minimumBytes: number): number => { const count = readUint32(); if (count > Math.floor((end - cursor) / minimumBytes)) throw new ProtocolDecodeFailure('Multimodal Transit item count exceeds the payload length.'); return count; };

  const tickCount = readUint64();
  const lineCount = readCount(LINE_LENGTH);
  const stopCount = readCount(STOP_LENGTH);
  const patternCount = readCount(PATTERN_HEADER_LENGTH);
  const vehicleCount = readCount(VEHICLE_LENGTH);
  const arrivalCount = readCount(ARRIVAL_LENGTH);

  const lines: TransitLine[] = [];
  const lineIds = new Set<bigint>();
  for (let index = 0; index < lineCount; index += 1) {
    const id = readUint64(); const mode = readByte() as TransitMode;
    if (id === 0n || lineIds.has(id) || (mode !== TransitMode.Bus && mode !== TransitMode.Railway)) throw new ProtocolDecodeFailure('Transit Line payload is invalid.');
    lineIds.add(id); lines.push({ id, mode });
  }

  const stops: TransitStop[] = [];
  const stopIds = new Set<bigint>();
  for (let index = 0; index < stopCount; index += 1) {
    const id = readUint64(); const kind = readByte() as TransitStopKind; const x = readDouble(); const y = readDouble(); const z = readDouble(); const laneId = nullableId(readUint64()); const stationId = nullableId(readUint64()); const platformId = nullableId(readUint64());
    if (id === 0n || stopIds.has(id) || !finite3(x, y, z) || (kind !== TransitStopKind.Bus && kind !== TransitStopKind.Railway)) throw new ProtocolDecodeFailure('Transit Stop payload is invalid.');
    if ((kind === TransitStopKind.Bus && laneId === null) || (kind === TransitStopKind.Railway && stationId === null)) throw new ProtocolDecodeFailure('Transit Stop attachment is invalid.');
    stopIds.add(id); stops.push({ id, kind, x, y, z, laneId, stationId, platformId });
  }

  const patterns: TransitPattern[] = [];
  const patternIds = new Set<bigint>();
  for (let index = 0; index < patternCount; index += 1) {
    const id = readUint64(); const lineId = readUint64(); const railwayServiceId = nullableId(readUint64()); const count = readCount(PATTERN_STOP_LENGTH);
    if (id === 0n || patternIds.has(id) || !lineIds.has(lineId) || count < 2) throw new ProtocolDecodeFailure('Transit Pattern payload is invalid.');
    const patternStops: TransitPatternStop[] = [];
    for (let stopIndex = 0; stopIndex < count; stopIndex += 1) {
      const stopId = readUint64(); const travelTicksFromPrevious = readUint64(); const dwellTicks = readUint64();
      if (!stopIds.has(stopId) || (stopIndex === 0 ? travelTicksFromPrevious !== 0n : travelTicksFromPrevious === 0n)) throw new ProtocolDecodeFailure('Transit Pattern stop payload is invalid.');
      patternStops.push({ stopId, travelTicksFromPrevious, dwellTicks });
    }
    patternIds.add(id); patterns.push({ id, lineId, railwayServiceId, stops: patternStops });
  }

  const vehicles: TransitVehicle[] = [];
  const vehicleIds = new Set<bigint>();
  for (let index = 0; index < vehicleCount; index += 1) {
    const id = readUint64(); const kind = readByte() as TransitVehicleKind; const tripId = nullableId(readUint64()); const roadVehicleId = nullableId(readUint64()); const stopIndex = readInt32(); const x = readDouble(); const y = readDouble(); const z = readDouble(); const state = readByte() as TransitVehicleState; const estimatedArrivalTick = readUint64(); const dwellUntilTick = readUint64();
    if (id === 0n || vehicleIds.has(id) || stopIndex < 0 || !finite3(x, y, z) || (kind !== TransitVehicleKind.Bus && kind !== TransitVehicleKind.Taxi) || state < TransitVehicleState.Idle || state > TransitVehicleState.Completed) throw new ProtocolDecodeFailure('Transit Vehicle payload is invalid.');
    vehicleIds.add(id); vehicles.push({ id, kind, tripId, roadVehicleId, stopIndex, x, y, z, state, estimatedArrivalTick, dwellUntilTick });
  }

  const arrivalEstimates: TransitArrivalEstimate[] = [];
  for (let index = 0; index < arrivalCount; index += 1) {
    const stopId = readUint64(); const lineId = readUint64(); const vehicleId = readUint64(); const estimatedArrivalTick = readUint64();
    if (!stopIds.has(stopId) || !lineIds.has(lineId) || !vehicleIds.has(vehicleId)) throw new ProtocolDecodeFailure('Transit arrival estimate payload is invalid.');
    arrivalEstimates.push({ stopId, lineId, vehicleId, estimatedArrivalTick });
  }
  if (cursor !== end) throw new ProtocolDecodeFailure('Multimodal Transit payload contains trailing bytes.');
  return { type: MultimodalTransitMessageType.MultimodalTransitSnapshot, tickCount, lines, stops, patterns, vehicles, arrivalEstimates };
}

function nullableId(value: bigint): bigint | null { return value === 0n ? null : value; }
function finite3(x: number, y: number, z: number): boolean { return Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z); }
