import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

export const GAS_SNAPSHOT_MESSAGE_TYPE = 770;
const FIXED_PAYLOAD_LENGTH = 92;
const NODE_PAYLOAD_LENGTH = 33;
const PIPELINE_PAYLOAD_LENGTH = 33;
const FACILITY_PAYLOAD_LENGTH = 42;
const SERVICE_POINT_PAYLOAD_LENGTH = 74;

export enum GasNodeKind { Source = 0, ImportTerminal = 1, Storage = 2, Distribution = 3, Service = 4, Regulator = 5 }
export enum GasFacilityKind { Source = 0, ImportTerminal = 1, Storage = 2 }
export enum GasOperatingState { Online = 0, Offline = 1 }
export enum GasDeliveryMode { Piped = 0, Delivered = 1 }
export enum GasServiceState { Supplied = 0, Constrained = 1, Unavailable = 2 }

export interface GasStatistics {
  readonly nodeCount: number;
  readonly pipelineCount: number;
  readonly sourceCount: number;
  readonly importTerminalCount: number;
  readonly storageCount: number;
  readonly servicePointCount: number;
  readonly pipedServicePointCount: number;
  readonly deliveredServicePointCount: number;
  readonly unavailableServicePointCount: number;
  readonly supplyCapacityCubicMetersPerDay: number;
  readonly demandCubicMetersPerDay: number;
  readonly servedCubicMetersPerDay: number;
  readonly unservedCubicMetersPerDay: number;
  readonly storedCubicMeters: number;
  readonly tickCount: bigint;
}

export interface GasNode { readonly nodeId: bigint; readonly kind: GasNodeKind; readonly x: number; readonly y: number; readonly z: number; }
export interface GasPipeline { readonly pipelineId: bigint; readonly fromNodeId: bigint; readonly toNodeId: bigint; readonly capacityCubicMetersPerDay: number; readonly isInService: boolean; }
export interface GasFacility { readonly kind: GasFacilityKind; readonly facilityId: bigint; readonly nodeId: bigint; readonly capacityCubicMetersPerDay: number; readonly outputCubicMetersPerDay: number; readonly storedCubicMeters: number; readonly operatingState: GasOperatingState; }
export interface GasServicePoint {
  readonly servicePointId: bigint;
  readonly nodeId: bigint;
  readonly buildingId: bigint;
  readonly establishmentId: bigint;
  readonly deliveryMode: GasDeliveryMode;
  readonly commodityId: bigint;
  readonly baseDemandCubicMetersPerDay: number;
  readonly demandCubicMetersPerDay: number;
  readonly servedCubicMetersPerDay: number;
  readonly unservedCubicMetersPerDay: number;
  readonly serviceState: GasServiceState;
}
export interface GasSnapshotMessage {
  readonly type: typeof GAS_SNAPSHOT_MESSAGE_TYPE;
  readonly statistics: GasStatistics;
  readonly nodes: readonly GasNode[];
  readonly pipelines: readonly GasPipeline[];
  readonly facilities: readonly GasFacility[];
  readonly servicePoints: readonly GasServicePoint[];
}
export interface GasProtocolEnvelope { readonly version: ProtocolVersion; readonly message: GasSnapshotMessage; }
export type GasProtocolMessage = GasSnapshotMessage;

export function isGasFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === GAS_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeGasFrame(frame: ArrayBuffer): GasProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Gas frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Gas frame magic is invalid.');
  const version: ProtocolVersion = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 14) throw new ProtocolDecodeFailure('Gas snapshots require Protocol 2.14 or newer.');
  if (view.getUint16(8, true) !== GAS_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Frame is not a Gas snapshot.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Gas frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Gas frame length is invalid.');
  if (payloadLength < FIXED_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Gas payload is too short.');

  const offset = PROTOCOL_HEADER_SIZE;
  const nodeCount = view.getUint16(offset + 84, true);
  const pipelineCount = view.getUint16(offset + 86, true);
  const facilityCount = view.getUint16(offset + 88, true);
  const servicePointCount = view.getUint16(offset + 90, true);
  const expectedLength = FIXED_PAYLOAD_LENGTH + nodeCount * NODE_PAYLOAD_LENGTH + pipelineCount * PIPELINE_PAYLOAD_LENGTH + facilityCount * FACILITY_PAYLOAD_LENGTH + servicePointCount * SERVICE_POINT_PAYLOAD_LENGTH;
  if (payloadLength !== expectedLength) throw new ProtocolDecodeFailure('Gas payload counts do not match its length.');

  const statistics: GasStatistics = {
    nodeCount: view.getUint32(offset, true), pipelineCount: view.getUint32(offset + 4, true), sourceCount: view.getUint32(offset + 8, true), importTerminalCount: view.getUint32(offset + 12, true),
    storageCount: view.getUint32(offset + 16, true), servicePointCount: view.getUint32(offset + 20, true), pipedServicePointCount: view.getUint32(offset + 24, true), deliveredServicePointCount: view.getUint32(offset + 28, true),
    unavailableServicePointCount: view.getUint32(offset + 32, true), supplyCapacityCubicMetersPerDay: view.getFloat64(offset + 36, true), demandCubicMetersPerDay: view.getFloat64(offset + 44, true),
    servedCubicMetersPerDay: view.getFloat64(offset + 52, true), unservedCubicMetersPerDay: view.getFloat64(offset + 60, true), storedCubicMeters: view.getFloat64(offset + 68, true), tickCount: view.getBigUint64(offset + 76, true),
  };
  validateStatistics(statistics);

  let cursor = offset + FIXED_PAYLOAD_LENGTH;
  const nodes: GasNode[] = [];
  for (let index = 0; index < nodeCount; index += 1) {
    const node: GasNode = { nodeId: view.getBigUint64(cursor, true), kind: view.getUint8(cursor + 8) as GasNodeKind, x: view.getFloat64(cursor + 9, true), y: view.getFloat64(cursor + 17, true), z: view.getFloat64(cursor + 25, true) };
    if (!validNode(node)) throw new ProtocolDecodeFailure('Gas node entry is invalid.');
    nodes.push(node); cursor += NODE_PAYLOAD_LENGTH;
  }
  const pipelines: GasPipeline[] = [];
  for (let index = 0; index < pipelineCount; index += 1) {
    const inService = view.getUint8(cursor + 32);
    const pipeline: GasPipeline = { pipelineId: view.getBigUint64(cursor, true), fromNodeId: view.getBigUint64(cursor + 8, true), toNodeId: view.getBigUint64(cursor + 16, true), capacityCubicMetersPerDay: view.getFloat64(cursor + 24, true), isInService: inService !== 0 };
    if (inService > 1 || !validPipeline(pipeline)) throw new ProtocolDecodeFailure('Gas pipeline entry is invalid.');
    pipelines.push(pipeline); cursor += PIPELINE_PAYLOAD_LENGTH;
  }
  const facilities: GasFacility[] = [];
  for (let index = 0; index < facilityCount; index += 1) {
    const facility: GasFacility = { kind: view.getUint8(cursor) as GasFacilityKind, facilityId: view.getBigUint64(cursor + 1, true), nodeId: view.getBigUint64(cursor + 9, true), capacityCubicMetersPerDay: view.getFloat64(cursor + 17, true), outputCubicMetersPerDay: view.getFloat64(cursor + 25, true), storedCubicMeters: view.getFloat64(cursor + 33, true), operatingState: view.getUint8(cursor + 41) as GasOperatingState };
    if (!validFacility(facility)) throw new ProtocolDecodeFailure('Gas facility entry is invalid.');
    facilities.push(facility); cursor += FACILITY_PAYLOAD_LENGTH;
  }
  const servicePoints: GasServicePoint[] = [];
  for (let index = 0; index < servicePointCount; index += 1) {
    const servicePoint: GasServicePoint = { servicePointId: view.getBigUint64(cursor, true), nodeId: view.getBigUint64(cursor + 8, true), buildingId: view.getBigUint64(cursor + 16, true), establishmentId: view.getBigUint64(cursor + 24, true), deliveryMode: view.getUint8(cursor + 32) as GasDeliveryMode, commodityId: view.getBigUint64(cursor + 33, true), baseDemandCubicMetersPerDay: view.getFloat64(cursor + 41, true), demandCubicMetersPerDay: view.getFloat64(cursor + 49, true), servedCubicMetersPerDay: view.getFloat64(cursor + 57, true), unservedCubicMetersPerDay: view.getFloat64(cursor + 65, true), serviceState: view.getUint8(cursor + 73) as GasServiceState };
    if (!validServicePoint(servicePoint)) throw new ProtocolDecodeFailure('Gas service point entry is invalid.');
    servicePoints.push(servicePoint); cursor += SERVICE_POINT_PAYLOAD_LENGTH;
  }
  return { version, message: { type: GAS_SNAPSHOT_MESSAGE_TYPE, statistics, nodes, pipelines, facilities, servicePoints } };
}

function validateStatistics(value: GasStatistics): void {
  if ([value.supplyCapacityCubicMetersPerDay, value.demandCubicMetersPerDay, value.servedCubicMetersPerDay, value.unservedCubicMetersPerDay, value.storedCubicMeters].some((item) => !nonNegative(item)) || value.servedCubicMetersPerDay > value.demandCubicMetersPerDay + 1e-9) throw new ProtocolDecodeFailure('Gas statistics are invalid.');
}
function validNode(value: GasNode): boolean { return value.nodeId !== 0n && value.kind >= GasNodeKind.Source && value.kind <= GasNodeKind.Regulator && Number.isFinite(value.x) && Number.isFinite(value.y) && Number.isFinite(value.z); }
function validPipeline(value: GasPipeline): boolean { return value.pipelineId !== 0n && value.fromNodeId !== 0n && value.toNodeId !== 0n && value.fromNodeId !== value.toNodeId && positive(value.capacityCubicMetersPerDay); }
function validFacility(value: GasFacility): boolean { return value.kind >= GasFacilityKind.Source && value.kind <= GasFacilityKind.Storage && value.facilityId !== 0n && value.nodeId !== 0n && positive(value.capacityCubicMetersPerDay) && nonNegative(value.outputCubicMetersPerDay) && nonNegative(value.storedCubicMeters) && value.outputCubicMetersPerDay <= value.capacityCubicMetersPerDay + 1e-9 && (value.operatingState === GasOperatingState.Online || value.operatingState === GasOperatingState.Offline); }
function validServicePoint(value: GasServicePoint): boolean {
  if (value.servicePointId === 0n || (value.buildingId === 0n && value.establishmentId === 0n) || !positive(value.baseDemandCubicMetersPerDay) || !nonNegative(value.demandCubicMetersPerDay) || !nonNegative(value.servedCubicMetersPerDay) || !nonNegative(value.unservedCubicMetersPerDay) || value.servedCubicMetersPerDay > value.demandCubicMetersPerDay + 1e-9 || value.serviceState < GasServiceState.Supplied || value.serviceState > GasServiceState.Unavailable) return false;
  return value.deliveryMode === GasDeliveryMode.Piped ? value.nodeId !== 0n && value.commodityId === 0n : value.deliveryMode === GasDeliveryMode.Delivered && value.nodeId === 0n && value.establishmentId !== 0n && value.commodityId !== 0n;
}
function positive(value: number): boolean { return Number.isFinite(value) && value > 0; }
function nonNegative(value: number): boolean { return Number.isFinite(value) && value >= 0; }
