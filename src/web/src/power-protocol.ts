import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

export const POWER_SNAPSHOT_MESSAGE_TYPE = 750;
const FIXED_PAYLOAD_LENGTH = 76;
const NODE_PAYLOAD_LENGTH = 33;
const LINE_PAYLOAD_LENGTH = 33;
const GENERATOR_PAYLOAD_LENGTH = 33;
const LOAD_PAYLOAD_LENGTH = 65;

export enum PowerNodeKind {
  GeneratorBus = 0,
  Substation = 1,
  Distribution = 2,
  Load = 3,
}

export enum GeneratorOperatingState {
  Online = 0,
  Offline = 1,
}

export enum PowerSupplyState {
  Supplied = 0,
  Constrained = 1,
  Outage = 2,
}

export interface PowerStatistics {
  readonly nodeCount: number;
  readonly lineCount: number;
  readonly generatorCount: number;
  readonly loadCount: number;
  readonly outageLoadCount: number;
  readonly generationCapacityMegawatts: number;
  readonly generationOutputMegawatts: number;
  readonly demandMegawatts: number;
  readonly servedMegawatts: number;
  readonly unservedMegawatts: number;
  readonly tickCount: bigint;
}

export interface PowerNode {
  readonly nodeId: bigint;
  readonly kind: PowerNodeKind;
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

export interface PowerLine {
  readonly lineId: bigint;
  readonly fromNodeId: bigint;
  readonly toNodeId: bigint;
  readonly capacityMegawatts: number;
  readonly isInService: boolean;
}

export interface PowerGenerator {
  readonly generatorId: bigint;
  readonly nodeId: bigint;
  readonly capacityMegawatts: number;
  readonly outputMegawatts: number;
  readonly operatingState: GeneratorOperatingState;
}

export interface PowerLoad {
  readonly loadId: bigint;
  readonly nodeId: bigint;
  readonly buildingId: bigint;
  readonly establishmentId: bigint;
  readonly baseDemandMegawatts: number;
  readonly demandMegawatts: number;
  readonly servedMegawatts: number;
  readonly unservedMegawatts: number;
  readonly supplyState: PowerSupplyState;
}

export interface PowerSnapshotMessage {
  readonly type: typeof POWER_SNAPSHOT_MESSAGE_TYPE;
  readonly statistics: PowerStatistics;
  readonly nodes: readonly PowerNode[];
  readonly lines: readonly PowerLine[];
  readonly generators: readonly PowerGenerator[];
  readonly loads: readonly PowerLoad[];
}

export interface PowerProtocolEnvelope {
  readonly version: ProtocolVersion;
  readonly message: PowerSnapshotMessage;
}

export type PowerProtocolMessage = PowerSnapshotMessage;

export function isPowerFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === POWER_SNAPSHOT_MESSAGE_TYPE;
}

export function decodePowerFrame(frame: ArrayBuffer): PowerProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Power frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Power frame magic is invalid.');
  const version: ProtocolVersion = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 12) throw new ProtocolDecodeFailure('Power snapshots require Protocol 2.12 or newer.');
  if (view.getUint16(8, true) !== POWER_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Frame is not a Power snapshot.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Power frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Power frame length is invalid.');
  if (payloadLength < FIXED_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Power payload is too short.');

  const offset = PROTOCOL_HEADER_SIZE;
  const nodeCount = view.getUint16(offset + 68, true);
  const lineCount = view.getUint16(offset + 70, true);
  const generatorCount = view.getUint16(offset + 72, true);
  const loadCount = view.getUint16(offset + 74, true);
  const expectedLength = FIXED_PAYLOAD_LENGTH
    + nodeCount * NODE_PAYLOAD_LENGTH
    + lineCount * LINE_PAYLOAD_LENGTH
    + generatorCount * GENERATOR_PAYLOAD_LENGTH
    + loadCount * LOAD_PAYLOAD_LENGTH;
  if (payloadLength !== expectedLength) throw new ProtocolDecodeFailure('Power payload counts do not match its length.');

  const statistics: PowerStatistics = {
    nodeCount: view.getUint32(offset, true),
    lineCount: view.getUint32(offset + 4, true),
    generatorCount: view.getUint32(offset + 8, true),
    loadCount: view.getUint32(offset + 12, true),
    outageLoadCount: view.getUint32(offset + 16, true),
    generationCapacityMegawatts: view.getFloat64(offset + 20, true),
    generationOutputMegawatts: view.getFloat64(offset + 28, true),
    demandMegawatts: view.getFloat64(offset + 36, true),
    servedMegawatts: view.getFloat64(offset + 44, true),
    unservedMegawatts: view.getFloat64(offset + 52, true),
    tickCount: view.getBigUint64(offset + 60, true),
  };
  validateNonNegative(statistics.generationCapacityMegawatts, 'generation capacity');
  validateNonNegative(statistics.generationOutputMegawatts, 'generation output');
  validateNonNegative(statistics.demandMegawatts, 'demand');
  validateNonNegative(statistics.servedMegawatts, 'served demand');
  validateNonNegative(statistics.unservedMegawatts, 'unserved demand');

  let cursor = offset + FIXED_PAYLOAD_LENGTH;
  const nodes: PowerNode[] = [];
  for (let index = 0; index < nodeCount; index += 1) {
    const node: PowerNode = {
      nodeId: view.getBigUint64(cursor, true),
      kind: view.getUint8(cursor + 8) as PowerNodeKind,
      x: view.getFloat64(cursor + 9, true),
      y: view.getFloat64(cursor + 17, true),
      z: view.getFloat64(cursor + 25, true),
    };
    if (node.nodeId === 0n || node.kind < PowerNodeKind.GeneratorBus || node.kind > PowerNodeKind.Load || !Number.isFinite(node.x) || !Number.isFinite(node.y) || !Number.isFinite(node.z)) throw new ProtocolDecodeFailure('Power node entry is invalid.');
    nodes.push(node);
    cursor += NODE_PAYLOAD_LENGTH;
  }

  const lines: PowerLine[] = [];
  for (let index = 0; index < lineCount; index += 1) {
    const line: PowerLine = {
      lineId: view.getBigUint64(cursor, true),
      fromNodeId: view.getBigUint64(cursor + 8, true),
      toNodeId: view.getBigUint64(cursor + 16, true),
      capacityMegawatts: view.getFloat64(cursor + 24, true),
      isInService: view.getUint8(cursor + 32) !== 0,
    };
    if (line.lineId === 0n || line.fromNodeId === 0n || line.toNodeId === 0n || line.fromNodeId === line.toNodeId || !Number.isFinite(line.capacityMegawatts) || line.capacityMegawatts <= 0) throw new ProtocolDecodeFailure('Power line entry is invalid.');
    lines.push(line);
    cursor += LINE_PAYLOAD_LENGTH;
  }

  const generators: PowerGenerator[] = [];
  for (let index = 0; index < generatorCount; index += 1) {
    const generator: PowerGenerator = {
      generatorId: view.getBigUint64(cursor, true),
      nodeId: view.getBigUint64(cursor + 8, true),
      capacityMegawatts: view.getFloat64(cursor + 16, true),
      outputMegawatts: view.getFloat64(cursor + 24, true),
      operatingState: view.getUint8(cursor + 32) as GeneratorOperatingState,
    };
    if (generator.generatorId === 0n || generator.nodeId === 0n || !Number.isFinite(generator.capacityMegawatts) || generator.capacityMegawatts <= 0 || !Number.isFinite(generator.outputMegawatts) || generator.outputMegawatts < 0 || generator.operatingState < GeneratorOperatingState.Online || generator.operatingState > GeneratorOperatingState.Offline) throw new ProtocolDecodeFailure('Power Generator entry is invalid.');
    generators.push(generator);
    cursor += GENERATOR_PAYLOAD_LENGTH;
  }

  const loads: PowerLoad[] = [];
  for (let index = 0; index < loadCount; index += 1) {
    const load: PowerLoad = {
      loadId: view.getBigUint64(cursor, true),
      nodeId: view.getBigUint64(cursor + 8, true),
      buildingId: view.getBigUint64(cursor + 16, true),
      establishmentId: view.getBigUint64(cursor + 24, true),
      baseDemandMegawatts: view.getFloat64(cursor + 32, true),
      demandMegawatts: view.getFloat64(cursor + 40, true),
      servedMegawatts: view.getFloat64(cursor + 48, true),
      unservedMegawatts: view.getFloat64(cursor + 56, true),
      supplyState: view.getUint8(cursor + 64) as PowerSupplyState,
    };
    if (load.loadId === 0n || load.nodeId === 0n || (load.buildingId === 0n && load.establishmentId === 0n) || !Number.isFinite(load.baseDemandMegawatts) || load.baseDemandMegawatts <= 0 || load.supplyState < PowerSupplyState.Supplied || load.supplyState > PowerSupplyState.Outage) throw new ProtocolDecodeFailure('Power Load entry is invalid.');
    validateNonNegative(load.demandMegawatts, 'load demand');
    validateNonNegative(load.servedMegawatts, 'load served demand');
    validateNonNegative(load.unservedMegawatts, 'load unserved demand');
    loads.push(load);
    cursor += LOAD_PAYLOAD_LENGTH;
  }

  return { version, message: { type: POWER_SNAPSHOT_MESSAGE_TYPE, statistics, nodes, lines, generators, loads } };
}

function validateNonNegative(value: number, name: string): void {
  if (!Number.isFinite(value) || value < 0) throw new ProtocolDecodeFailure(`Power ${name} is invalid.`);
}
