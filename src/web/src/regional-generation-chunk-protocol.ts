import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';
import {
  REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
  type RegionalGenerationSnapshotMessage,
} from './regional-generation-protocol.ts';

export const REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE = 811;
const REGIONAL_GENERATION_CHUNK_PROTOCOL_MINOR = 22;
const CHUNK_METADATA_LENGTH = 20;
const MAXIMUM_AGGREGATE_PAYLOAD_BYTES = 64 * 1024 * 1024;
const MAXIMUM_SETTLEMENTS = 64;
const MAXIMUM_GROWTH_EVENTS = 1_024;
const MAXIMUM_CORRIDORS = 512;
const MAXIMUM_CORRIDOR_GEOMETRY_POINTS = 256;
const MAXIMUM_DISTRICTS = 512;
const MAXIMUM_PARCELS = 4_096;
const MAXIMUM_BUILDINGS = 4_096;
const MAXIMUM_POIS = 1_024;
const MAXIMUM_TOPONYMS = 4_096;
const MAXIMUM_ROAD_SIGNS = 4_096;
const UINT64_MAX = 18_446_744_073_709_551_615n;
const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

const UINT64_PROPERTY_NAMES = new Set([
  'tickCount', 'worldSeed', 'settlementId', 'eventId', 'corridorId', 'fromSettlementId', 'toSettlementId',
  'districtId', 'parcelId', 'buildingId', 'poiId', 'toponymId', 'roadSignId', 'nameId',
  'sourceNaturalToponymId', 'sourceFeatureId', 'parentHumanToponymId', 'destinationSettlementId', 'featureId',
]);

export interface RegionalGenerationSnapshotChunkMessage {
  readonly type: typeof REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE;
  readonly snapshotId: bigint;
  readonly chunkIndex: number;
  readonly chunkCount: number;
  readonly totalPayloadBytes: number;
  readonly data: Uint8Array;
}

export interface RegionalGenerationChunkEnvelope {
  readonly version: ProtocolVersion;
  readonly message: RegionalGenerationSnapshotChunkMessage;
}

interface PendingSnapshot {
  readonly snapshotId: bigint;
  readonly version: ProtocolVersion;
  readonly chunkCount: number;
  readonly totalPayloadBytes: number;
  readonly chunks: (Uint8Array | null)[];
  receivedCount: number;
  receivedBytes: number;
}

export function isRegionalGenerationChunkFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC
    && view.getUint16(8, true) === REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE;
}

export function decodeRegionalGenerationChunkFrame(frame: ArrayBuffer): RegionalGenerationChunkEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE + CHUNK_METADATA_LENGTH) {
    throw new ProtocolDecodeFailure('RegionalGeneration chunk frame is too short.');
  }
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC || view.getUint16(10, true) !== 0) {
    throw new ProtocolDecodeFailure('Invalid RegionalGeneration chunk frame header.');
  }
  if (view.getUint16(8, true) !== REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE) {
    throw new ProtocolDecodeFailure('Unknown RegionalGeneration chunk message type.');
  }
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < REGIONAL_GENERATION_CHUNK_PROTOCOL_MINOR) {
    throw new ProtocolDecodeFailure('RegionalGeneration chunk frames require Protocol 2.22 or newer.');
  }
  const payloadLength = view.getUint32(12, true);
  if (payloadLength < CHUNK_METADATA_LENGTH || payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH
    || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) {
    throw new ProtocolDecodeFailure('RegionalGeneration chunk frame length is invalid.');
  }

  const payloadOffset = PROTOCOL_HEADER_SIZE;
  const snapshotId = view.getBigUint64(payloadOffset, true);
  const chunkIndex = view.getInt32(payloadOffset + 8, true);
  const chunkCount = view.getInt32(payloadOffset + 12, true);
  const totalPayloadBytes = view.getInt32(payloadOffset + 16, true);
  const dataLength = payloadLength - CHUNK_METADATA_LENGTH;
  if (snapshotId === 0n || chunkCount <= 0 || chunkIndex < 0 || chunkIndex >= chunkCount
    || totalPayloadBytes <= 0 || totalPayloadBytes > MAXIMUM_AGGREGATE_PAYLOAD_BYTES
    || dataLength < 0 || dataLength > totalPayloadBytes) {
    throw new ProtocolDecodeFailure('RegionalGeneration chunk metadata is invalid.');
  }

  return {
    version,
    message: Object.freeze({
      type: REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE,
      snapshotId,
      chunkIndex,
      chunkCount,
      totalPayloadBytes,
      data: new Uint8Array(frame.slice(payloadOffset + CHUNK_METADATA_LENGTH)),
    }),
  };
}

export class RegionalGenerationChunkAssembler {
  private pending: PendingSnapshot | null = null;

  public reset(): void { this.pending = null; }

  public apply(envelope: RegionalGenerationChunkEnvelope): RegionalGenerationSnapshotMessage | null {
    const { version, message } = envelope;
    let pending = this.pending;
    if (pending === null || pending.snapshotId !== message.snapshotId) {
      pending = {
        snapshotId: message.snapshotId,
        version,
        chunkCount: message.chunkCount,
        totalPayloadBytes: message.totalPayloadBytes,
        chunks: Array.from({ length: message.chunkCount }, () => null),
        receivedCount: 0,
        receivedBytes: 0,
      };
      this.pending = pending;
    } else if (!versionsEqual(pending.version, version)
      || pending.chunkCount !== message.chunkCount
      || pending.totalPayloadBytes !== message.totalPayloadBytes) {
      this.pending = null;
      throw new ProtocolDecodeFailure('RegionalGeneration chunks contain inconsistent snapshot metadata.');
    }

    const existing = pending.chunks[message.chunkIndex];
    if (existing !== null) {
      if (!bytesEqual(existing, message.data)) {
        this.pending = null;
        throw new ProtocolDecodeFailure('RegionalGeneration chunk index was reused with different data.');
      }
      return null;
    }

    pending.chunks[message.chunkIndex] = message.data;
    pending.receivedCount += 1;
    pending.receivedBytes += message.data.byteLength;
    if (pending.receivedBytes > pending.totalPayloadBytes) {
      this.pending = null;
      throw new ProtocolDecodeFailure('RegionalGeneration chunk bytes exceed declared aggregate length.');
    }
    if (pending.receivedCount !== pending.chunkCount) return null;
    if (pending.receivedBytes !== pending.totalPayloadBytes) {
      this.pending = null;
      throw new ProtocolDecodeFailure('RegionalGeneration aggregate byte length is incomplete.');
    }

    const payload = new Uint8Array(pending.totalPayloadBytes);
    let offset = 0;
    for (const chunk of pending.chunks) {
      if (chunk === null) {
        this.pending = null;
        throw new ProtocolDecodeFailure('RegionalGeneration chunk set is incomplete.');
      }
      payload.set(chunk, offset);
      offset += chunk.byteLength;
    }
    this.pending = null;
    return decodeAggregate(payload);
  }
}

function decodeAggregate(payload: Uint8Array): RegionalGenerationSnapshotMessage {
  let raw: unknown;
  try {
    const json = utf8Decoder.decode(payload);
    raw = JSON.parse(quoteLosslessUInt64Properties(json), reviveUInt64);
  } catch (error) {
    if (error instanceof ProtocolDecodeFailure) throw error;
    throw new ProtocolDecodeFailure('RegionalGeneration aggregate is not valid UTF-8 JSON.');
  }
  return validateAggregate(raw);
}

function validateAggregate(value: unknown): RegionalGenerationSnapshotMessage {
  if (!isRecord(value)) throw new ProtocolDecodeFailure('RegionalGeneration aggregate root is invalid.');
  const arrays = ['settlements', 'growthEvents', 'corridors', 'districts', 'parcels', 'buildings', 'pois', 'toponyms', 'roadSigns'] as const;
  for (const key of arrays) if (!Array.isArray(value[key])) throw new ProtocolDecodeFailure(`RegionalGeneration ${key} collection is invalid.`);
  if (!isRecord(value.quality)) throw new ProtocolDecodeFailure('RegionalGeneration quality is invalid.');
  assertMaximum((value.settlements as unknown[]).length, MAXIMUM_SETTLEMENTS, 'Settlement');
  assertMaximum((value.growthEvents as unknown[]).length, MAXIMUM_GROWTH_EVENTS, 'GrowthEvent');
  assertMaximum((value.corridors as unknown[]).length, MAXIMUM_CORRIDORS, 'Corridor');
  assertMaximum((value.districts as unknown[]).length, MAXIMUM_DISTRICTS, 'District');
  assertMaximum((value.parcels as unknown[]).length, MAXIMUM_PARCELS, 'Parcel');
  assertMaximum((value.buildings as unknown[]).length, MAXIMUM_BUILDINGS, 'Building');
  assertMaximum((value.pois as unknown[]).length, MAXIMUM_POIS, 'POI');
  assertMaximum((value.toponyms as unknown[]).length, MAXIMUM_TOPONYMS, 'Toponym');
  assertMaximum((value.roadSigns as unknown[]).length, MAXIMUM_ROAD_SIGNS, 'RoadSign');

  assertUInt64(value.tickCount, false, 'tickCount');
  assertUInt64(value.worldSeed, true, 'worldSeed');
  assertInteger(value.preset, 0, 2, 'preset');
  assertInteger(value.iterations, 0, 32, 'iterations');
  assertVolume(value);
  assertQuality(value.quality);

  const toponyms = value.toponyms as Record<string, unknown>[];
  const toponymIds = collectIds(toponyms, 'toponymId', 'Toponym');
  for (const item of toponyms) {
    assertInteger(item.kind, 0, 5, 'Toponym kind');
    assertText(item.name, 160, 'Toponym name');
    assertText(item.generatorKey, 128, 'Toponym generatorKey');
    assertUInt64(item.sourceNaturalToponymId, false, 'Toponym sourceNaturalToponymId');
    assertUInt64(item.sourceFeatureId, false, 'Toponym sourceFeatureId');
    assertUInt64(item.parentHumanToponymId, false, 'Toponym parentHumanToponymId');
    const parentId = item.parentHumanToponymId as bigint;
    if (parentId !== 0n && !toponymIds.has(parentId)) throw new ProtocolDecodeFailure('RegionalGeneration Toponym parent reference is invalid.');
    if (typeof item.sourceNaturalName !== 'string' || item.sourceNaturalName.length > 160) throw new ProtocolDecodeFailure('RegionalGeneration Toponym natural source name is invalid.');
    if ((item.sourceNaturalToponymId as bigint) === 0n && item.sourceNaturalName.length !== 0) throw new ProtocolDecodeFailure('RegionalGeneration Toponym provenance is invalid.');
  }

  const settlements = value.settlements as Record<string, unknown>[];
  const settlementIds = collectIds(settlements, 'settlementId', 'Settlement');
  for (const item of settlements) {
    assertPoint(item, 'Settlement');
    assertInteger(item.environment, 0, 7, 'Settlement environment');
    assertInteger(item.origin, 0, 9, 'Settlement origin');
    assertInteger(item.role, 0, 7, 'Settlement role');
    assertInteger(item.initialEconomy, 0, 7, 'Settlement initialEconomy');
    assertInteger(item.population, 0, 2_147_483_647, 'Settlement population');
    assertInteger(item.jobs, 0, 2_147_483_647, 'Settlement jobs');
    assertPositive(item.influenceRadiusMeters, 'Settlement influenceRadiusMeters');
    assertUInt64(item.nameId, true, 'Settlement nameId');
    if (!toponymIds.has(item.nameId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration Settlement name reference is invalid.');
    assertUnitRecord(item.suitability, ['flatness', 'waterAccess', 'transportPotential', 'buildability', 'resourceAccess', 'floodRisk', 'steepSlopeRisk', 'isolation', 'constructionCost', 'totalScore'], 'Settlement suitability');
  }

  const growthEvents = value.growthEvents as Record<string, unknown>[];
  collectIds(growthEvents, 'eventId', 'GrowthEvent');
  for (const item of growthEvents) {
    assertUInt64(item.settlementId, true, 'GrowthEvent settlementId');
    if (!settlementIds.has(item.settlementId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration GrowthEvent settlement reference is invalid.');
    assertInteger(item.stage, 0, 5, 'GrowthEvent stage');
    assertInteger(item.sequence, 0, 2_147_483_647, 'GrowthEvent sequence');
    assertInteger(item.populationDelta, 0, 2_147_483_647, 'GrowthEvent populationDelta');
    assertInteger(item.jobDelta, 0, 2_147_483_647, 'GrowthEvent jobDelta');
    assertPoint(item, 'GrowthEvent');
    assertText(item.reason, 256, 'GrowthEvent reason');
  }

  const corridors = value.corridors as Record<string, unknown>[];
  const corridorIds = collectIds(corridors, 'corridorId', 'Corridor');
  for (const item of corridors) {
    assertInteger(item.kind, 0, 3, 'Corridor kind');
    assertUInt64(item.fromSettlementId, true, 'Corridor fromSettlementId');
    assertUInt64(item.toSettlementId, true, 'Corridor toSettlementId');
    if (!settlementIds.has(item.fromSettlementId as bigint) || !settlementIds.has(item.toSettlementId as bigint)
      || item.fromSettlementId === item.toSettlementId) throw new ProtocolDecodeFailure('RegionalGeneration Corridor settlement reference is invalid.');
    if (!Array.isArray(item.geometry) || item.geometry.length < 2 || item.geometry.length > MAXIMUM_CORRIDOR_GEOMETRY_POINTS) throw new ProtocolDecodeFailure('RegionalGeneration Corridor geometry count is invalid.');
    for (const point of item.geometry) assertPoint(point, 'Corridor geometry');
    assertUnit(item.terrainAdaptation, 'Corridor terrainAdaptation');
    assertNonNegative(item.constructionCost, 'Corridor constructionCost');
    assertUInt64(item.nameId, false, 'Corridor nameId');
    if ((item.nameId as bigint) !== 0n && !toponymIds.has(item.nameId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration Corridor name reference is invalid.');
  }

  const districts = value.districts as Record<string, unknown>[];
  const districtIds = collectIds(districts, 'districtId', 'District');
  const districtSettlement = new Map<bigint, bigint>();
  for (const item of districts) {
    assertUInt64(item.settlementId, true, 'District settlementId');
    if (!settlementIds.has(item.settlementId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration District settlement reference is invalid.');
    assertInteger(item.kind, 0, 5, 'District kind');
    assertVolume(item);
    assertUInt64(item.nameId, true, 'District nameId');
    if (!toponymIds.has(item.nameId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration District name reference is invalid.');
    assertUnit(item.accessibility, 'District accessibility');
    districtSettlement.set(item.districtId as bigint, item.settlementId as bigint);
  }

  const parcels = value.parcels as Record<string, unknown>[];
  const parcelIds = collectIds(parcels, 'parcelId', 'Parcel');
  const parcelById = new Map<bigint, Record<string, unknown>>();
  for (const item of parcels) {
    assertUInt64(item.settlementId, true, 'Parcel settlementId');
    assertUInt64(item.districtId, true, 'Parcel districtId');
    if (!settlementIds.has(item.settlementId as bigint) || !districtIds.has(item.districtId as bigint)
      || districtSettlement.get(item.districtId as bigint) !== item.settlementId) throw new ProtocolDecodeFailure('RegionalGeneration Parcel hierarchy is invalid.');
    assertVolume(item);
    assertInteger(item.zone, 0, 6, 'Parcel zone');
    assertInteger(item.developmentState, 0, 3, 'Parcel developmentState');
    assertUnit(item.developmentSuitability, 'Parcel developmentSuitability');
    assertUnit(item.landValue, 'Parcel landValue');
    assertUInt64(item.buildingId, false, 'Parcel buildingId');
    parcelById.set(item.parcelId as bigint, item);
  }

  const buildings = value.buildings as Record<string, unknown>[];
  const buildingIds = collectIds(buildings, 'buildingId', 'Building');
  const buildingParcel = new Map<bigint, bigint>();
  const occupiedParcels = new Set<bigint>();
  for (const item of buildings) {
    assertUInt64(item.parcelId, true, 'Building parcelId');
    if (!parcelIds.has(item.parcelId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration Building parcel reference is invalid.');
    assertInteger(item.use, 0, 6, 'Building use');
    assertVolume(item);
    assertInteger(item.floors, 1, 256, 'Building floors');
    assertInteger(item.capacity, 0, 2_147_483_647, 'Building capacity');
    assertInteger(item.historicalStage, 0, 2_147_483_647, 'Building historicalStage');
    const parcelId = item.parcelId as bigint;
    const parcel = parcelById.get(parcelId);
    if (parcel === undefined || parcel.buildingId !== item.buildingId || occupiedParcels.has(parcelId) || !containsHorizontal(parcel, item)) throw new ProtocolDecodeFailure('RegionalGeneration Building ownership is invalid.');
    occupiedParcels.add(parcelId);
    buildingParcel.set(item.buildingId as bigint, parcelId);
  }
  for (const parcel of parcels) {
    const buildingId = parcel.buildingId as bigint;
    if (buildingId !== 0n && (!buildingIds.has(buildingId) || buildingParcel.get(buildingId) !== parcel.parcelId)) throw new ProtocolDecodeFailure('RegionalGeneration Parcel/Building reciprocal reference is invalid.');
  }

  const pois = value.pois as Record<string, unknown>[];
  collectIds(pois, 'poiId', 'POI');
  for (const item of pois) {
    assertUInt64(item.settlementId, true, 'POI settlementId');
    assertInteger(item.kind, 0, 5, 'POI kind');
    assertPoint(item, 'POI');
    assertUInt64(item.buildingId, false, 'POI buildingId');
    assertUInt64(item.nameId, false, 'POI nameId');
    if (!settlementIds.has(item.settlementId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration POI settlement reference is invalid.');
    const buildingId = item.buildingId as bigint;
    if (buildingId !== 0n) {
      const parcel = parcelById.get(buildingParcel.get(buildingId) ?? 0n);
      if (parcel === undefined || parcel.settlementId !== item.settlementId) throw new ProtocolDecodeFailure('RegionalGeneration POI building hierarchy is invalid.');
    }
    if ((item.nameId as bigint) !== 0n && !toponymIds.has(item.nameId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration POI name reference is invalid.');
  }

  const roadSigns = value.roadSigns as Record<string, unknown>[];
  collectIds(roadSigns, 'roadSignId', 'RoadSign');
  for (const item of roadSigns) {
    assertInteger(item.kind, 0, 9, 'RoadSign kind');
    assertPoint(item, 'RoadSign');
    assertUInt64(item.corridorId, true, 'RoadSign corridorId');
    assertUInt64(item.destinationSettlementId, false, 'RoadSign destinationSettlementId');
    assertUInt64(item.featureId, false, 'RoadSign featureId');
    assertText(item.text, 256, 'RoadSign text');
    if (!corridorIds.has(item.corridorId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration RoadSign corridor reference is invalid.');
    if ((item.destinationSettlementId as bigint) !== 0n && !settlementIds.has(item.destinationSettlementId as bigint)) throw new ProtocolDecodeFailure('RegionalGeneration RoadSign destination reference is invalid.');
  }

  return Object.freeze({
    ...value,
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    settlements: Object.freeze(settlements.map(Object.freeze)),
    growthEvents: Object.freeze(growthEvents.map(Object.freeze)),
    corridors: Object.freeze(corridors.map((item) => Object.freeze({ ...item, geometry: Object.freeze((item.geometry as Record<string, unknown>[]).map(Object.freeze)) }))),
    districts: Object.freeze(districts.map(Object.freeze)),
    parcels: Object.freeze(parcels.map(Object.freeze)),
    buildings: Object.freeze(buildings.map(Object.freeze)),
    pois: Object.freeze(pois.map(Object.freeze)),
    toponyms: Object.freeze(toponyms.map(Object.freeze)),
    roadSigns: Object.freeze(roadSigns.map(Object.freeze)),
    quality: Object.freeze(value.quality),
  }) as unknown as RegionalGenerationSnapshotMessage;
}

function quoteLosslessUInt64Properties(json: string): string {
  return json.replace(/("(?:tickCount|worldSeed|settlementId|eventId|corridorId|fromSettlementId|toSettlementId|districtId|parcelId|buildingId|poiId|toponymId|roadSignId|nameId|sourceNaturalToponymId|sourceFeatureId|parentHumanToponymId|destinationSettlementId|featureId)"\s*:\s*)(\d+)/g, '$1"$2"');
}

function reviveUInt64(key: string, value: unknown): unknown {
  if (!UINT64_PROPERTY_NAMES.has(key)) return value;
  if (typeof value !== 'string' || !/^\d+$/.test(value)) throw new ProtocolDecodeFailure(`RegionalGeneration ${key} is invalid.`);
  const parsed = BigInt(value);
  if (parsed < 0n || parsed > UINT64_MAX) throw new ProtocolDecodeFailure(`RegionalGeneration ${key} is outside UInt64 range.`);
  return parsed;
}

function collectIds(items: readonly Record<string, unknown>[], key: string, label: string): Set<bigint> {
  const result = new Set<bigint>();
  for (const item of items) {
    if (!isRecord(item)) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} entry is invalid.`);
    assertUInt64(item[key], true, `${label} ${key}`);
    const id = item[key] as bigint;
    if (result.has(id)) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} IDs are duplicated.`);
    result.add(id);
  }
  return result;
}

function assertMaximum(actual: number, maximum: number, label: string): void {
  if (actual > maximum) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} count exceeds the supported maximum.`);
}
function assertUInt64(value: unknown, positive: boolean, label: string): void {
  if (typeof value !== 'bigint' || value < 0n || value > UINT64_MAX || (positive && value === 0n)) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`);
}
function assertInteger(value: unknown, minimum: number, maximum: number, label: string): void {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum || value > maximum) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`);
}
function assertPoint(value: unknown, label: string): void {
  if (!isRecord(value) || !finite(value.x) || !finite(value.y) || !finite(value.z)) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} point is invalid.`);
}
function assertVolume(value: unknown): void {
  if (!isRecord(value) || !finite(value.minX) || !finite(value.minY) || !finite(value.minZ) || !finite(value.maxX) || !finite(value.maxY) || !finite(value.maxZ)
    || value.maxX <= value.minX || value.maxY <= value.minY || value.maxZ < value.minZ) throw new ProtocolDecodeFailure('RegionalGeneration volume is invalid.');
}
function assertUnit(value: unknown, label: string): void { if (!finite(value) || value < 0 || value > 1) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`); }
function assertPositive(value: unknown, label: string): void { if (!finite(value) || value <= 0) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`); }
function assertNonNegative(value: unknown, label: string): void { if (!finite(value) || value < 0) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`); }
function assertText(value: unknown, maximumLength: number, label: string): void { if (typeof value !== 'string' || value.trim().length === 0 || value.length > maximumLength) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`); }
function assertUnitRecord(value: unknown, keys: readonly string[], label: string): void { if (!isRecord(value)) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} is invalid.`); for (const key of keys) assertUnit(value[key], `${label}.${key}`); }
function assertQuality(value: unknown): void { assertUnitRecord(value, ['terrainAdaptation', 'roadConnectivity', 'averageSlopeCost', 'accessibility', 'congestionRisk', 'landUseConsistency', 'floodExposure', 'urbanCompactness', 'polycentricBalance', 'overallScore'], 'quality'); }
function containsHorizontal(outer: Record<string, unknown>, inner: Record<string, unknown>): boolean { return (inner.minX as number) >= (outer.minX as number) && (inner.maxX as number) <= (outer.maxX as number) && (inner.minY as number) >= (outer.minY as number) && (inner.maxY as number) <= (outer.maxY as number); }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }
function finite(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value); }
function versionsEqual(left: ProtocolVersion, right: ProtocolVersion): boolean { return left.major === right.major && left.minor === right.minor; }
function bytesEqual(left: Uint8Array, right: Uint8Array): boolean { if (left.byteLength !== right.byteLength) return false; for (let index = 0; index < left.byteLength; index += 1) if (left[index] !== right[index]) return false; return true; }
