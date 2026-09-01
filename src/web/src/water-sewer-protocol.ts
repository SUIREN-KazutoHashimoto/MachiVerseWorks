import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

export const WATER_SEWER_SNAPSHOT_MESSAGE_TYPE = 760;
const FIXED_PAYLOAD_LENGTH = 112;
const NODE_PAYLOAD_LENGTH = 34;
const PIPE_PAYLOAD_LENGTH = 34;
const FACILITY_PAYLOAD_LENGTH = 42;
const SERVICE_POINT_PAYLOAD_LENGTH = 106;

export enum UtilityNetworkKind { Water = 0, Sewer = 1 }
export enum UtilityNodeKind { Source = 0, Reservoir = 1, Pump = 2, Distribution = 3, Service = 4, Collection = 5, Treatment = 6 }
export enum UtilityFacilityKind { WaterSource = 0, Reservoir = 1, WaterPump = 2, SewerPump = 3, SewageTreatmentPlant = 4 }
export enum UtilityOperatingState { Online = 0, Offline = 1 }
export enum WaterServiceState { Supplied = 0, Constrained = 1, Unavailable = 2 }
export enum SewerServiceState { Available = 0, Constrained = 1, Unavailable = 2, Overflow = 3 }

export interface WaterSewerStatistics {
  readonly waterNodeCount: number;
  readonly waterPipeCount: number;
  readonly sewerNodeCount: number;
  readonly sewerPipeCount: number;
  readonly waterSourceCount: number;
  readonly reservoirCount: number;
  readonly pumpCount: number;
  readonly treatmentPlantCount: number;
  readonly servicePointCount: number;
  readonly waterUnavailableCount: number;
  readonly sewerUnavailableCount: number;
  readonly sewerOverflowCount: number;
  readonly waterSupplyCapacityCubicMetersPerDay: number;
  readonly waterDemandCubicMetersPerDay: number;
  readonly waterServedCubicMetersPerDay: number;
  readonly wastewaterGeneratedCubicMetersPerDay: number;
  readonly wastewaterProcessedCubicMetersPerDay: number;
  readonly wastewaterOverflowCubicMetersPerDay: number;
  readonly tickCount: bigint;
}

export interface UtilityNode {
  readonly networkKind: UtilityNetworkKind;
  readonly nodeId: bigint;
  readonly kind: UtilityNodeKind;
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

export interface UtilityPipe {
  readonly networkKind: UtilityNetworkKind;
  readonly pipeId: bigint;
  readonly fromNodeId: bigint;
  readonly toNodeId: bigint;
  readonly capacityCubicMetersPerDay: number;
  readonly isInService: boolean;
}

export interface UtilityFacility {
  readonly kind: UtilityFacilityKind;
  readonly facilityId: bigint;
  readonly nodeId: bigint;
  readonly powerLoadId: bigint;
  readonly capacityCubicMetersPerDay: number;
  readonly throughputCubicMetersPerDay: number;
  readonly operatingState: UtilityOperatingState;
}

export interface WaterSewerServicePoint {
  readonly servicePointId: bigint;
  readonly waterNodeId: bigint;
  readonly sewerNodeId: bigint;
  readonly buildingId: bigint;
  readonly establishmentId: bigint;
  readonly baseWaterDemandCubicMetersPerDay: number;
  readonly wastewaterReturnRatio: number;
  readonly waterDemandCubicMetersPerDay: number;
  readonly waterServedCubicMetersPerDay: number;
  readonly waterUnservedCubicMetersPerDay: number;
  readonly waterState: WaterServiceState;
  readonly wastewaterGeneratedCubicMetersPerDay: number;
  readonly wastewaterProcessedCubicMetersPerDay: number;
  readonly wastewaterOverflowCubicMetersPerDay: number;
  readonly sewerState: SewerServiceState;
}

export interface WaterSewerSnapshotMessage {
  readonly type: typeof WATER_SEWER_SNAPSHOT_MESSAGE_TYPE;
  readonly statistics: WaterSewerStatistics;
  readonly nodes: readonly UtilityNode[];
  readonly pipes: readonly UtilityPipe[];
  readonly facilities: readonly UtilityFacility[];
  readonly servicePoints: readonly WaterSewerServicePoint[];
}

export interface WaterSewerProtocolEnvelope {
  readonly version: ProtocolVersion;
  readonly message: WaterSewerSnapshotMessage;
}

export type WaterSewerProtocolMessage = WaterSewerSnapshotMessage;

export function isWaterSewerFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === WATER_SEWER_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeWaterSewerFrame(frame: ArrayBuffer): WaterSewerProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Water/Sewer frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Water/Sewer frame magic is invalid.');
  const version: ProtocolVersion = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 13) throw new ProtocolDecodeFailure('Water/Sewer snapshots require Protocol 2.13 or newer.');
  if (view.getUint16(8, true) !== WATER_SEWER_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Frame is not a Water/Sewer snapshot.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Water/Sewer frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Water/Sewer frame length is invalid.');
  if (payloadLength < FIXED_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Water/Sewer payload is too short.');

  const offset = PROTOCOL_HEADER_SIZE;
  const nodeCount = view.getUint16(offset + 104, true);
  const pipeCount = view.getUint16(offset + 106, true);
  const facilityCount = view.getUint16(offset + 108, true);
  const servicePointCount = view.getUint16(offset + 110, true);
  const expectedLength = FIXED_PAYLOAD_LENGTH
    + nodeCount * NODE_PAYLOAD_LENGTH
    + pipeCount * PIPE_PAYLOAD_LENGTH
    + facilityCount * FACILITY_PAYLOAD_LENGTH
    + servicePointCount * SERVICE_POINT_PAYLOAD_LENGTH;
  if (payloadLength !== expectedLength) throw new ProtocolDecodeFailure('Water/Sewer payload counts do not match its length.');

  const statistics: WaterSewerStatistics = {
    waterNodeCount: view.getUint32(offset, true),
    waterPipeCount: view.getUint32(offset + 4, true),
    sewerNodeCount: view.getUint32(offset + 8, true),
    sewerPipeCount: view.getUint32(offset + 12, true),
    waterSourceCount: view.getUint32(offset + 16, true),
    reservoirCount: view.getUint32(offset + 20, true),
    pumpCount: view.getUint32(offset + 24, true),
    treatmentPlantCount: view.getUint32(offset + 28, true),
    servicePointCount: view.getUint32(offset + 32, true),
    waterUnavailableCount: view.getUint32(offset + 36, true),
    sewerUnavailableCount: view.getUint32(offset + 40, true),
    sewerOverflowCount: view.getUint32(offset + 44, true),
    waterSupplyCapacityCubicMetersPerDay: view.getFloat64(offset + 48, true),
    waterDemandCubicMetersPerDay: view.getFloat64(offset + 56, true),
    waterServedCubicMetersPerDay: view.getFloat64(offset + 64, true),
    wastewaterGeneratedCubicMetersPerDay: view.getFloat64(offset + 72, true),
    wastewaterProcessedCubicMetersPerDay: view.getFloat64(offset + 80, true),
    wastewaterOverflowCubicMetersPerDay: view.getFloat64(offset + 88, true),
    tickCount: view.getBigUint64(offset + 96, true),
  };
  validateStatistics(statistics);

  let cursor = offset + FIXED_PAYLOAD_LENGTH;
  const nodes: UtilityNode[] = [];
  for (let index = 0; index < nodeCount; index += 1) {
    const node: UtilityNode = {
      networkKind: view.getUint8(cursor) as UtilityNetworkKind,
      nodeId: view.getBigUint64(cursor + 1, true),
      kind: view.getUint8(cursor + 9) as UtilityNodeKind,
      x: view.getFloat64(cursor + 10, true),
      y: view.getFloat64(cursor + 18, true),
      z: view.getFloat64(cursor + 26, true),
    };
    if (!validNode(node)) throw new ProtocolDecodeFailure('Water/Sewer node entry is invalid.');
    nodes.push(node);
    cursor += NODE_PAYLOAD_LENGTH;
  }

  const pipes: UtilityPipe[] = [];
  for (let index = 0; index < pipeCount; index += 1) {
    const inService = view.getUint8(cursor + 33);
    const pipe: UtilityPipe = {
      networkKind: view.getUint8(cursor) as UtilityNetworkKind,
      pipeId: view.getBigUint64(cursor + 1, true),
      fromNodeId: view.getBigUint64(cursor + 9, true),
      toNodeId: view.getBigUint64(cursor + 17, true),
      capacityCubicMetersPerDay: view.getFloat64(cursor + 25, true),
      isInService: inService !== 0,
    };
    if (inService > 1 || !validPipe(pipe)) throw new ProtocolDecodeFailure('Water/Sewer pipe entry is invalid.');
    pipes.push(pipe);
    cursor += PIPE_PAYLOAD_LENGTH;
  }

  const facilities: UtilityFacility[] = [];
  for (let index = 0; index < facilityCount; index += 1) {
    const facility: UtilityFacility = {
      kind: view.getUint8(cursor) as UtilityFacilityKind,
      facilityId: view.getBigUint64(cursor + 1, true),
      nodeId: view.getBigUint64(cursor + 9, true),
      powerLoadId: view.getBigUint64(cursor + 17, true),
      capacityCubicMetersPerDay: view.getFloat64(cursor + 25, true),
      throughputCubicMetersPerDay: view.getFloat64(cursor + 33, true),
      operatingState: view.getUint8(cursor + 41) as UtilityOperatingState,
    };
    if (!validFacility(facility)) throw new ProtocolDecodeFailure('Water/Sewer facility entry is invalid.');
    facilities.push(facility);
    cursor += FACILITY_PAYLOAD_LENGTH;
  }

  const servicePoints: WaterSewerServicePoint[] = [];
  for (let index = 0; index < servicePointCount; index += 1) {
    const servicePoint: WaterSewerServicePoint = {
      servicePointId: view.getBigUint64(cursor, true),
      waterNodeId: view.getBigUint64(cursor + 8, true),
      sewerNodeId: view.getBigUint64(cursor + 16, true),
      buildingId: view.getBigUint64(cursor + 24, true),
      establishmentId: view.getBigUint64(cursor + 32, true),
      baseWaterDemandCubicMetersPerDay: view.getFloat64(cursor + 40, true),
      wastewaterReturnRatio: view.getFloat64(cursor + 48, true),
      waterDemandCubicMetersPerDay: view.getFloat64(cursor + 56, true),
      waterServedCubicMetersPerDay: view.getFloat64(cursor + 64, true),
      waterUnservedCubicMetersPerDay: view.getFloat64(cursor + 72, true),
      waterState: view.getUint8(cursor + 80) as WaterServiceState,
      wastewaterGeneratedCubicMetersPerDay: view.getFloat64(cursor + 81, true),
      wastewaterProcessedCubicMetersPerDay: view.getFloat64(cursor + 89, true),
      wastewaterOverflowCubicMetersPerDay: view.getFloat64(cursor + 97, true),
      sewerState: view.getUint8(cursor + 105) as SewerServiceState,
    };
    if (!validServicePoint(servicePoint)) throw new ProtocolDecodeFailure('Water/Sewer service point entry is invalid.');
    servicePoints.push(servicePoint);
    cursor += SERVICE_POINT_PAYLOAD_LENGTH;
  }

  return {
    version,
    message: { type: WATER_SEWER_SNAPSHOT_MESSAGE_TYPE, statistics, nodes, pipes, facilities, servicePoints },
  };
}

function validateStatistics(value: WaterSewerStatistics): void {
  const values = [
    value.waterSupplyCapacityCubicMetersPerDay,
    value.waterDemandCubicMetersPerDay,
    value.waterServedCubicMetersPerDay,
    value.wastewaterGeneratedCubicMetersPerDay,
    value.wastewaterProcessedCubicMetersPerDay,
    value.wastewaterOverflowCubicMetersPerDay,
  ];
  if (values.some((item) => !Number.isFinite(item) || item < 0)
    || value.waterServedCubicMetersPerDay > value.waterDemandCubicMetersPerDay + 1e-9
    || value.wastewaterProcessedCubicMetersPerDay > value.wastewaterGeneratedCubicMetersPerDay + 1e-9) {
    throw new ProtocolDecodeFailure('Water/Sewer statistics are invalid.');
  }
}

function validNode(value: UtilityNode): boolean {
  const networkValid = value.networkKind === UtilityNetworkKind.Water || value.networkKind === UtilityNetworkKind.Sewer;
  const kindValid = value.kind >= UtilityNodeKind.Source && value.kind <= UtilityNodeKind.Treatment;
  const compatible = value.networkKind === UtilityNetworkKind.Water
    ? value.kind <= UtilityNodeKind.Service
    : value.kind === UtilityNodeKind.Service || value.kind === UtilityNodeKind.Collection || value.kind === UtilityNodeKind.Pump || value.kind === UtilityNodeKind.Treatment;
  return networkValid && value.nodeId !== 0n && kindValid && compatible && Number.isFinite(value.x) && Number.isFinite(value.y) && Number.isFinite(value.z);
}

function validPipe(value: UtilityPipe): boolean {
  return (value.networkKind === UtilityNetworkKind.Water || value.networkKind === UtilityNetworkKind.Sewer)
    && value.pipeId !== 0n && value.fromNodeId !== 0n && value.toNodeId !== 0n && value.fromNodeId !== value.toNodeId
    && Number.isFinite(value.capacityCubicMetersPerDay) && value.capacityCubicMetersPerDay > 0;
}

function validFacility(value: UtilityFacility): boolean {
  return value.kind >= UtilityFacilityKind.WaterSource && value.kind <= UtilityFacilityKind.SewageTreatmentPlant
    && value.facilityId !== 0n && value.nodeId !== 0n
    && Number.isFinite(value.capacityCubicMetersPerDay) && value.capacityCubicMetersPerDay > 0
    && Number.isFinite(value.throughputCubicMetersPerDay) && value.throughputCubicMetersPerDay >= 0
    && value.throughputCubicMetersPerDay <= value.capacityCubicMetersPerDay + 1e-9
    && (value.operatingState === UtilityOperatingState.Online || value.operatingState === UtilityOperatingState.Offline);
}

function validServicePoint(value: WaterSewerServicePoint): boolean {
  return value.servicePointId !== 0n && value.waterNodeId !== 0n && value.sewerNodeId !== 0n
    && (value.buildingId !== 0n || value.establishmentId !== 0n)
    && Number.isFinite(value.baseWaterDemandCubicMetersPerDay) && value.baseWaterDemandCubicMetersPerDay > 0
    && Number.isFinite(value.wastewaterReturnRatio) && value.wastewaterReturnRatio >= 0 && value.wastewaterReturnRatio <= 1
    && nonNegative(value.waterDemandCubicMetersPerDay) && nonNegative(value.waterServedCubicMetersPerDay) && nonNegative(value.waterUnservedCubicMetersPerDay)
    && value.waterServedCubicMetersPerDay <= value.waterDemandCubicMetersPerDay + 1e-9
    && value.waterState >= WaterServiceState.Supplied && value.waterState <= WaterServiceState.Unavailable
    && nonNegative(value.wastewaterGeneratedCubicMetersPerDay) && nonNegative(value.wastewaterProcessedCubicMetersPerDay) && nonNegative(value.wastewaterOverflowCubicMetersPerDay)
    && value.wastewaterProcessedCubicMetersPerDay <= value.wastewaterGeneratedCubicMetersPerDay + 1e-9
    && value.wastewaterOverflowCubicMetersPerDay <= value.wastewaterGeneratedCubicMetersPerDay + 1e-9
    && value.sewerState >= SewerServiceState.Available && value.sewerState <= SewerServiceState.Overflow;
}

function nonNegative(value: number): boolean { return Number.isFinite(value) && value >= 0; }
