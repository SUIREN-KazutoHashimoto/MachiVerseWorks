import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';
import { GeneratedBuildingUse, ParcelDevelopmentState } from './regional-generation-protocol.ts';

export const PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE = 820;
const PERSISTENT_REGIONAL_EVOLUTION_PROTOCOL_MINOR = 19;
const MAXIMUM_SETTLEMENTS = 256;
const MAXIMUM_PARCELS = 16_384;
const MAXIMUM_BUILDINGS = 16_384;
const MAXIMUM_DERIVED_ITEMS = 65_536;
const MAXIMUM_EVENTS = 262_144;
const MAXIMUM_REASON_LENGTH = 256;
const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

type WireUInt64 = string | number;

export enum SettlementScale { Hamlet = 0, Village = 1, Town = 2, City = 3, Metropolis = 4 }
export enum SettlementTrend { Growing = 0, Stable = 1, Declining = 2, Recovering = 3, Dormant = 4 }
export enum BuildingLifecycleStatus { Active = 0, Vacant = 1, Renovating = 2, Repurposing = 3, Abandoned = 4, Demolished = 5 }
export enum RegionalServiceKind { Commerce = 0, Education = 1, Medical = 2 }
export enum InfrastructureDemandKind { Road = 0, Transit = 1, Utility = 2 }
export enum RegionalRelationKind { Commuting = 0, Trade = 1, Service = 2, Metro = 3 }
export enum RegionalEvolutionEventKind {
  Growth = 0,
  Decline = 1,
  ClassificationChanged = 2,
  ParcelDevelopment = 3,
  BuildingConstructed = 4,
  BuildingRenovated = 5,
  BuildingUseChanged = 6,
  BuildingVacated = 7,
  BuildingAbandoned = 8,
  BuildingDemolished = 9,
  SettlementEmergence = 10,
  SettlementDormancy = 11,
  SettlementRecovery = 12,
  RegionalRelationFormed = 13,
  RegionalRelationEnded = 14,
}

export interface SettlementEvolutionObservation {
  readonly settlementId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly population: number;
  readonly jobs: number;
  readonly serviceIndex: number;
  readonly density: number;
  readonly accessibility: number;
  readonly influenceRadiusMeters: number;
  readonly scale: SettlementScale;
  readonly trend: SettlementTrend;
  readonly isActive: boolean;
  readonly establishedYear: number;
  readonly dormantSinceYear: number | null;
}

export interface ParcelEvolutionObservation {
  readonly parcelId: bigint;
  readonly settlementId: bigint;
  readonly developmentDemand: number;
  readonly landValue: number;
  readonly developmentState: ParcelDevelopmentState;
  readonly buildingId: bigint;
}

export interface BuildingLifecycleObservation {
  readonly buildingId: bigint;
  readonly parcelId: bigint;
  readonly use: GeneratedBuildingUse;
  readonly builtYear: number;
  readonly lastChangedYear: number;
  readonly condition: number;
  readonly occupancy: number;
  readonly capacity: number;
  readonly status: BuildingLifecycleStatus;
}

export interface ServiceCatchmentObservation { readonly settlementId: bigint; readonly kind: RegionalServiceKind; readonly radiusMeters: number; readonly coverage: number; }
export interface InfrastructureDemandObservation { readonly settlementId: bigint; readonly kind: InfrastructureDemandKind; readonly demand: number; readonly reason: string; }
export interface RegionalRelationObservation { readonly relationId: bigint; readonly fromSettlementId: bigint; readonly toSettlementId: bigint; readonly kind: RegionalRelationKind; readonly strength: number; readonly isActive: boolean; readonly sinceYear: number; }
export interface RegionalEvolutionEventObservation { readonly eventId: bigint; readonly year: number; readonly kind: RegionalEvolutionEventKind; readonly settlementId: bigint; readonly buildingId: bigint; readonly reason: string; }
export interface RegionalCommutingFlowObservation { readonly fromSettlementId: bigint; readonly toSettlementId: bigint; readonly workerCount: number; }
export interface RegionalFreightFlowObservation { readonly fromSettlementId: bigint; readonly toSettlementId: bigint; readonly commodityId: bigint; readonly quantity: number; readonly shipmentCount: number; readonly deliveredQuantity: number; }

export interface PersistentRegionalEvolutionSnapshotMessage {
  readonly type: typeof PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE;
  readonly currentYear: number;
  readonly tickCount: bigint;
  readonly settlements: readonly SettlementEvolutionObservation[];
  readonly parcels: readonly ParcelEvolutionObservation[];
  readonly buildings: readonly BuildingLifecycleObservation[];
  readonly serviceCatchments: readonly ServiceCatchmentObservation[];
  readonly infrastructureDemands: readonly InfrastructureDemandObservation[];
  readonly relations: readonly RegionalRelationObservation[];
  readonly events: readonly RegionalEvolutionEventObservation[];
  readonly commutingFlows: readonly RegionalCommutingFlowObservation[];
  readonly freightFlows: readonly RegionalFreightFlowObservation[];
  readonly isFullSnapshot: boolean;
}

export interface PersistentRegionalEvolutionProtocolEnvelope { readonly version: ProtocolVersion; readonly message: PersistentRegionalEvolutionSnapshotMessage; }

interface WireSettlementEvolution extends Omit<SettlementEvolutionObservation, 'settlementId'> { readonly settlementId: WireUInt64; }
interface WireParcelEvolution extends Omit<ParcelEvolutionObservation, 'parcelId' | 'settlementId' | 'buildingId'> { readonly parcelId: WireUInt64; readonly settlementId: WireUInt64; readonly buildingId: WireUInt64; }
interface WireBuildingLifecycle extends Omit<BuildingLifecycleObservation, 'buildingId' | 'parcelId'> { readonly buildingId: WireUInt64; readonly parcelId: WireUInt64; }
interface WireServiceCatchment extends Omit<ServiceCatchmentObservation, 'settlementId'> { readonly settlementId: WireUInt64; }
interface WireInfrastructureDemand extends Omit<InfrastructureDemandObservation, 'settlementId'> { readonly settlementId: WireUInt64; }
interface WireRegionalRelation extends Omit<RegionalRelationObservation, 'relationId' | 'fromSettlementId' | 'toSettlementId'> { readonly relationId: WireUInt64; readonly fromSettlementId: WireUInt64; readonly toSettlementId: WireUInt64; }
interface WireRegionalEvolutionEvent extends Omit<RegionalEvolutionEventObservation, 'eventId' | 'settlementId' | 'buildingId'> { readonly eventId: WireUInt64; readonly settlementId: WireUInt64; readonly buildingId: WireUInt64; }
interface WireRegionalCommutingFlow extends Omit<RegionalCommutingFlowObservation, 'fromSettlementId' | 'toSettlementId'> { readonly fromSettlementId: WireUInt64; readonly toSettlementId: WireUInt64; }
interface WireRegionalFreightFlow extends Omit<RegionalFreightFlowObservation, 'fromSettlementId' | 'toSettlementId' | 'commodityId'> { readonly fromSettlementId: WireUInt64; readonly toSettlementId: WireUInt64; readonly commodityId: WireUInt64; }
interface WirePersistentRegionalEvolutionSnapshot {
  readonly currentYear: number;
  readonly tickCount: WireUInt64;
  readonly settlements: readonly WireSettlementEvolution[];
  readonly parcels: readonly WireParcelEvolution[];
  readonly buildings: readonly WireBuildingLifecycle[];
  readonly serviceCatchments: readonly WireServiceCatchment[];
  readonly infrastructureDemands: readonly WireInfrastructureDemand[];
  readonly relations: readonly WireRegionalRelation[];
  readonly events: readonly WireRegionalEvolutionEvent[];
  readonly commutingFlows: readonly WireRegionalCommutingFlow[];
  readonly freightFlows: readonly WireRegionalFreightFlow[];
  readonly isFullSnapshot: boolean;
}

export function isPersistentRegionalEvolutionFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE;
}

export function decodePersistentRegionalEvolutionFrame(frame: ArrayBuffer): PersistentRegionalEvolutionProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('PersistentRegionalEvolution frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC || view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Invalid PersistentRegionalEvolution frame header.');
  if (view.getUint16(8, true) !== PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Unknown PersistentRegionalEvolution message type.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < PERSISTENT_REGIONAL_EVOLUTION_PROTOCOL_MINOR) throw new ProtocolDecodeFailure('PersistentRegionalEvolution snapshots require Protocol 2.19 or newer.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('PersistentRegionalEvolution frame length is invalid.');

  let raw: WirePersistentRegionalEvolutionSnapshot;
  try {
    const json = utf8Decoder.decode(new Uint8Array(frame, PROTOCOL_HEADER_SIZE, payloadLength));
    raw = JSON.parse(quoteLosslessUInt64Properties(json)) as WirePersistentRegionalEvolutionSnapshot;
  } catch (error) {
    if (error instanceof ProtocolDecodeFailure) throw error;
    throw new ProtocolDecodeFailure('PersistentRegionalEvolution snapshot is not valid UTF-8 JSON.');
  }
  return Object.freeze({ version, message: normalizeChunk(raw) });
}

function normalizeChunk(raw: WirePersistentRegionalEvolutionSnapshot): PersistentRegionalEvolutionSnapshotMessage {
  if (!isRecord(raw) || !Array.isArray(raw.settlements) || !Array.isArray(raw.parcels) || !Array.isArray(raw.buildings)
    || !Array.isArray(raw.serviceCatchments) || !Array.isArray(raw.infrastructureDemands) || !Array.isArray(raw.relations)
    || !Array.isArray(raw.events) || !Array.isArray(raw.commutingFlows) || !Array.isArray(raw.freightFlows)
    || typeof raw.isFullSnapshot !== 'boolean') throw new ProtocolDecodeFailure('PersistentRegionalEvolution snapshot shape is invalid.');
  if (!integerAtLeast(raw.currentYear, 0) || raw.settlements.length > MAXIMUM_SETTLEMENTS || raw.parcels.length > MAXIMUM_PARCELS
    || raw.buildings.length > MAXIMUM_BUILDINGS || raw.serviceCatchments.length > MAXIMUM_DERIVED_ITEMS
    || raw.infrastructureDemands.length > MAXIMUM_DERIVED_ITEMS || raw.relations.length > MAXIMUM_DERIVED_ITEMS
    || raw.events.length > MAXIMUM_EVENTS || raw.commutingFlows.length > MAXIMUM_DERIVED_ITEMS || raw.freightFlows.length > MAXIMUM_DERIVED_ITEMS) {
    throw new ProtocolDecodeFailure('PersistentRegionalEvolution snapshot metadata is invalid.');
  }

  const settlements = Object.freeze(raw.settlements.map((item) => normalizeSettlement(item, raw.currentYear)));
  uniquePositiveIds(settlements.map((item) => item.settlementId), 'Settlement');
  const parcels = Object.freeze(raw.parcels.map(normalizeParcel));
  uniquePositiveIds(parcels.map((item) => item.parcelId), 'Parcel');
  const buildings = Object.freeze(raw.buildings.map((item) => normalizeBuilding(item, raw.currentYear)));
  uniquePositiveIds(buildings.map((item) => item.buildingId), 'Building');
  const serviceCatchments = Object.freeze(raw.serviceCatchments.map(normalizeServiceCatchment));
  const infrastructureDemands = Object.freeze(raw.infrastructureDemands.map(normalizeInfrastructureDemand));
  const relations = Object.freeze(raw.relations.map((item) => normalizeRelation(item, raw.currentYear)));
  uniquePositiveIds(relations.map((item) => item.relationId), 'Relation');
  let previousEventId = 0n;
  const events = Object.freeze(raw.events.map((item) => {
    const normalized = normalizeEvent(item, raw.currentYear);
    if (normalized.eventId <= previousEventId) throw new ProtocolDecodeFailure('PersistentRegionalEvolution Event IDs must be strictly increasing within a chunk.');
    previousEventId = normalized.eventId;
    return normalized;
  }));
  const commutingFlows = Object.freeze(raw.commutingFlows.map(normalizeCommutingFlow));
  const freightFlows = Object.freeze(raw.freightFlows.map(normalizeFreightFlow));

  return Object.freeze({
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: raw.currentYear,
    tickCount: parseUInt64(raw.tickCount, 'PersistentRegionalEvolution tick count'),
    settlements,
    parcels,
    buildings,
    serviceCatchments,
    infrastructureDemands,
    relations,
    events,
    commutingFlows,
    freightFlows,
    isFullSnapshot: raw.isFullSnapshot,
  });
}

function normalizeSettlement(raw: WireSettlementEvolution, currentYear: number): SettlementEvolutionObservation {
  if (!isRecord(raw) || !finite(raw.x) || !finite(raw.y) || !finite(raw.z) || !integerAtLeast(raw.population, 0) || !integerAtLeast(raw.jobs, 0)
    || !unit(raw.serviceIndex) || !unit(raw.density) || !unit(raw.accessibility) || !positive(raw.influenceRadiusMeters)
    || !integerRange(raw.scale, SettlementScale.Hamlet, SettlementScale.Metropolis) || !integerRange(raw.trend, SettlementTrend.Growing, SettlementTrend.Dormant)
    || typeof raw.isActive !== 'boolean' || !Number.isInteger(raw.establishedYear) || raw.establishedYear > currentYear
    || (raw.dormantSinceYear !== null && (!Number.isInteger(raw.dormantSinceYear) || raw.dormantSinceYear > currentYear))) {
    throw new ProtocolDecodeFailure('PersistentRegionalEvolution Settlement values are invalid.');
  }
  return Object.freeze({ ...raw, settlementId: parsePositiveUInt64(raw.settlementId, 'Settlement ID') });
}

function normalizeParcel(raw: WireParcelEvolution): ParcelEvolutionObservation {
  if (!isRecord(raw) || !unit(raw.developmentDemand) || !unit(raw.landValue) || !integerRange(raw.developmentState, ParcelDevelopmentState.Vacant, ParcelDevelopmentState.Redeveloping)) throw new ProtocolDecodeFailure('PersistentRegionalEvolution Parcel values are invalid.');
  return Object.freeze({ ...raw, parcelId: parsePositiveUInt64(raw.parcelId, 'Parcel ID'), settlementId: parsePositiveUInt64(raw.settlementId, 'Parcel settlement ID'), buildingId: parseUInt64(raw.buildingId, 'Parcel building ID') });
}

function normalizeBuilding(raw: WireBuildingLifecycle, currentYear: number): BuildingLifecycleObservation {
  if (!isRecord(raw) || !integerRange(raw.use, GeneratedBuildingUse.Residential, GeneratedBuildingUse.Utility) || !Number.isInteger(raw.builtYear) || raw.builtYear > currentYear
    || !Number.isInteger(raw.lastChangedYear) || raw.lastChangedYear > currentYear || !unit(raw.condition) || !unit(raw.occupancy)
    || !integerAtLeast(raw.capacity, 0) || !integerRange(raw.status, BuildingLifecycleStatus.Active, BuildingLifecycleStatus.Demolished)) throw new ProtocolDecodeFailure('PersistentRegionalEvolution Building values are invalid.');
  return Object.freeze({ ...raw, buildingId: parsePositiveUInt64(raw.buildingId, 'Building ID'), parcelId: parsePositiveUInt64(raw.parcelId, 'Building parcel ID') });
}

function normalizeServiceCatchment(raw: WireServiceCatchment): ServiceCatchmentObservation {
  if (!isRecord(raw) || !integerRange(raw.kind, RegionalServiceKind.Commerce, RegionalServiceKind.Medical) || !positive(raw.radiusMeters) || !unit(raw.coverage)) throw new ProtocolDecodeFailure('PersistentRegionalEvolution ServiceCatchment values are invalid.');
  return Object.freeze({ ...raw, settlementId: parsePositiveUInt64(raw.settlementId, 'ServiceCatchment settlement ID') });
}

function normalizeInfrastructureDemand(raw: WireInfrastructureDemand): InfrastructureDemandObservation {
  if (!isRecord(raw) || !integerRange(raw.kind, InfrastructureDemandKind.Road, InfrastructureDemandKind.Utility) || !unit(raw.demand) || !validReason(raw.reason)) throw new ProtocolDecodeFailure('PersistentRegionalEvolution InfrastructureDemand values are invalid.');
  return Object.freeze({ ...raw, settlementId: parsePositiveUInt64(raw.settlementId, 'InfrastructureDemand settlement ID') });
}

function normalizeRelation(raw: WireRegionalRelation, currentYear: number): RegionalRelationObservation {
  const fromSettlementId = parsePositiveUInt64(raw.fromSettlementId, 'Relation from settlement ID');
  const toSettlementId = parsePositiveUInt64(raw.toSettlementId, 'Relation to settlement ID');
  if (!isRecord(raw) || !integerRange(raw.kind, RegionalRelationKind.Commuting, RegionalRelationKind.Metro) || !unit(raw.strength) || typeof raw.isActive !== 'boolean'
    || !Number.isInteger(raw.sinceYear) || raw.sinceYear > currentYear || fromSettlementId === toSettlementId) throw new ProtocolDecodeFailure('PersistentRegionalEvolution Relation values are invalid.');
  return Object.freeze({ ...raw, relationId: parsePositiveUInt64(raw.relationId, 'Relation ID'), fromSettlementId, toSettlementId });
}

function normalizeEvent(raw: WireRegionalEvolutionEvent, currentYear: number): RegionalEvolutionEventObservation {
  if (!isRecord(raw) || !Number.isInteger(raw.year) || raw.year > currentYear || !integerRange(raw.kind, RegionalEvolutionEventKind.Growth, RegionalEvolutionEventKind.RegionalRelationEnded) || !validReason(raw.reason)) throw new ProtocolDecodeFailure('PersistentRegionalEvolution Event values are invalid.');
  return Object.freeze({ ...raw, eventId: parsePositiveUInt64(raw.eventId, 'Event ID'), settlementId: parsePositiveUInt64(raw.settlementId, 'Event settlement ID'), buildingId: parseUInt64(raw.buildingId, 'Event building ID') });
}

function normalizeCommutingFlow(raw: WireRegionalCommutingFlow): RegionalCommutingFlowObservation {
  const fromSettlementId = parsePositiveUInt64(raw.fromSettlementId, 'Commuting from settlement ID');
  const toSettlementId = parsePositiveUInt64(raw.toSettlementId, 'Commuting to settlement ID');
  if (!isRecord(raw) || !integerAtLeast(raw.workerCount, 1) || fromSettlementId === toSettlementId) throw new ProtocolDecodeFailure('PersistentRegionalEvolution commuting flow values are invalid.');
  return Object.freeze({ ...raw, fromSettlementId, toSettlementId });
}

function normalizeFreightFlow(raw: WireRegionalFreightFlow): RegionalFreightFlowObservation {
  const fromSettlementId = parsePositiveUInt64(raw.fromSettlementId, 'Freight from settlement ID');
  const toSettlementId = parsePositiveUInt64(raw.toSettlementId, 'Freight to settlement ID');
  if (!isRecord(raw) || fromSettlementId === toSettlementId || !nonNegative(raw.quantity) || !integerAtLeast(raw.shipmentCount, 1) || !nonNegative(raw.deliveredQuantity) || raw.deliveredQuantity > raw.quantity) throw new ProtocolDecodeFailure('PersistentRegionalEvolution freight flow values are invalid.');
  return Object.freeze({ ...raw, fromSettlementId, toSettlementId, commodityId: parsePositiveUInt64(raw.commodityId, 'Freight commodity ID') });
}

function validReason(value: unknown): value is string { return typeof value === 'string' && value.trim().length > 0 && value.length <= MAXIMUM_REASON_LENGTH; }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null && !Array.isArray(value); }
function finite(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value); }
function positive(value: unknown): value is number { return finite(value) && value > 0; }
function nonNegative(value: unknown): value is number { return finite(value) && value >= 0; }
function unit(value: unknown): value is number { return finite(value) && value >= 0 && value <= 1; }
function integerAtLeast(value: unknown, minimum: number): value is number { return typeof value === 'number' && Number.isInteger(value) && value >= minimum; }
function integerRange(value: unknown, minimum: number, maximum: number): value is number { return integerAtLeast(value, minimum) && value <= maximum; }

function uniquePositiveIds(ids: readonly bigint[], label: string): void {
  const set = new Set(ids);
  if (set.size !== ids.length || set.has(0n)) throw new ProtocolDecodeFailure(`PersistentRegionalEvolution ${label} IDs are invalid.`);
}

function quoteLosslessUInt64Properties(json: string): string {
  return json.replace(/("(?:tickCount|settlementId|parcelId|buildingId|relationId|eventId|fromSettlementId|toSettlementId|commodityId)"\s*:\s*)(\d+)/g, '$1"$2"');
}

function parsePositiveUInt64(value: WireUInt64, label: string): bigint {
  const parsed = parseUInt64(value, label);
  if (parsed === 0n) throw new ProtocolDecodeFailure(`${label} must be greater than zero.`);
  return parsed;
}

function parseUInt64(value: WireUInt64, label: string): bigint {
  if (typeof value === 'number') {
    if (!Number.isSafeInteger(value) || value < 0) throw new ProtocolDecodeFailure(`${label} is outside the exact JavaScript integer range.`);
    return BigInt(value);
  }
  if (typeof value !== 'string' || !/^\d+$/.test(value)) throw new ProtocolDecodeFailure(`${label} is invalid.`);
  try {
    const parsed = BigInt(value);
    if (parsed < 0n || parsed > 18_446_744_073_709_551_615n) throw new ProtocolDecodeFailure(`${label} is outside UInt64 range.`);
    return parsed;
  } catch (error) {
    if (error instanceof ProtocolDecodeFailure) throw error;
    throw new ProtocolDecodeFailure(`${label} is invalid.`);
  }
}
