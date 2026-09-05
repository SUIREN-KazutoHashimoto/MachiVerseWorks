import type {
  BuildingLifecycleObservation,
  InfrastructureDemandObservation,
  ParcelEvolutionObservation,
  PersistentRegionalEvolutionSnapshotMessage,
  RegionalCommutingFlowObservation,
  RegionalEvolutionEventObservation,
  RegionalFreightFlowObservation,
  RegionalRelationObservation,
  ServiceCatchmentObservation,
  SettlementEvolutionObservation,
} from './persistent-regional-evolution-protocol.ts';
import { PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE } from './persistent-regional-evolution-protocol.ts';
import { ProtocolDecodeFailure } from './protocol.ts';

const MAXIMUM_SETTLEMENTS = 256;
const MAXIMUM_PARCELS = 16_384;
const MAXIMUM_BUILDINGS = 16_384;
const MAXIMUM_DERIVED_ITEMS = 65_536;
const MAXIMUM_EVENTS = 262_144;
const MAXIMUM_BATCH_CHUNKS = 8_192;

export interface ReadonlyPersistentRegionalEvolutionStore {
  readonly snapshot: PersistentRegionalEvolutionSnapshotMessage | null;
  readonly revision: number;
  getSettlement(id: bigint): SettlementEvolutionObservation | undefined;
  getParcel(id: bigint): ParcelEvolutionObservation | undefined;
  getBuilding(id: bigint): BuildingLifecycleObservation | undefined;
  getSettlementForParcel(parcelId: bigint): SettlementEvolutionObservation | undefined;
  getParcelForBuilding(buildingId: bigint): ParcelEvolutionObservation | undefined;
  getSettlementForBuilding(buildingId: bigint): SettlementEvolutionObservation | undefined;
  getRelationsForSettlement(settlementId: bigint): readonly RegionalRelationObservation[];
  getEventsForSettlement(settlementId: bigint): readonly RegionalEvolutionEventObservation[];
}

interface PendingBatch {
  readonly snapshotId: bigint;
  readonly currentYear: number;
  readonly tickCount: bigint;
  readonly chunkCount: number;
  nextChunkIndex: number;
  lastEventId: bigint;
  readonly settlements: Map<bigint, SettlementEvolutionObservation>;
  readonly parcels: Map<bigint, ParcelEvolutionObservation>;
  readonly buildings: Map<bigint, BuildingLifecycleObservation>;
  readonly serviceCatchments: ServiceCatchmentObservation[];
  readonly infrastructureDemands: InfrastructureDemandObservation[];
  readonly relations: Map<bigint, RegionalRelationObservation>;
  readonly events: Map<bigint, RegionalEvolutionEventObservation>;
  readonly commutingFlows: RegionalCommutingFlowObservation[];
  readonly freightFlows: RegionalFreightFlowObservation[];
}

/**
 * Read-only View index over authoritative persistent regional evolution state.
 * Batch-aware chunks are accumulated in private mutable staging and become visible atomically
 * only after the final ordered chunk arrives. Legacy Protocol 2.19 chunks without batch metadata
 * remain supported, but are preflighted against the same logical resource and integrity bounds.
 */
export class PersistentRegionalEvolutionStore implements ReadonlyPersistentRegionalEvolutionStore {
  private current: PersistentRegionalEvolutionSnapshotMessage | null = null;
  private currentRevision = 0;
  private settlements = new Map<bigint, SettlementEvolutionObservation>();
  private parcels = new Map<bigint, ParcelEvolutionObservation>();
  private buildings = new Map<bigint, BuildingLifecycleObservation>();
  private serviceCatchments: ServiceCatchmentObservation[] = [];
  private infrastructureDemands: InfrastructureDemandObservation[] = [];
  private relations = new Map<bigint, RegionalRelationObservation>();
  private events = new Map<bigint, RegionalEvolutionEventObservation>();
  private commutingFlows: RegionalCommutingFlowObservation[] = [];
  private freightFlows: RegionalFreightFlowObservation[] = [];
  private relationsBySettlement = new Map<bigint, readonly RegionalRelationObservation[]>();
  private eventsBySettlement = new Map<bigint, readonly RegionalEvolutionEventObservation[]>();
  private pending: PendingBatch | null = null;

  public get snapshot(): PersistentRegionalEvolutionSnapshotMessage | null { return this.current; }
  public get revision(): number { return this.currentRevision; }
  public getSettlement(id: bigint): SettlementEvolutionObservation | undefined { return this.settlements.get(id); }
  public getParcel(id: bigint): ParcelEvolutionObservation | undefined { return this.parcels.get(id); }
  public getBuilding(id: bigint): BuildingLifecycleObservation | undefined { return this.buildings.get(id); }

  public getSettlementForParcel(parcelId: bigint): SettlementEvolutionObservation | undefined {
    const parcel = this.parcels.get(parcelId);
    return parcel === undefined ? undefined : this.settlements.get(parcel.settlementId);
  }

  public getParcelForBuilding(buildingId: bigint): ParcelEvolutionObservation | undefined {
    const building = this.buildings.get(buildingId);
    return building === undefined ? undefined : this.parcels.get(building.parcelId);
  }

  public getSettlementForBuilding(buildingId: bigint): SettlementEvolutionObservation | undefined {
    const parcel = this.getParcelForBuilding(buildingId);
    return parcel === undefined ? undefined : this.settlements.get(parcel.settlementId);
  }

  public getRelationsForSettlement(settlementId: bigint): readonly RegionalRelationObservation[] {
    return this.relationsBySettlement.get(settlementId) ?? EMPTY_RELATIONS;
  }

  public getEventsForSettlement(settlementId: bigint): readonly RegionalEvolutionEventObservation[] {
    return this.eventsBySettlement.get(settlementId) ?? EMPTY_EVENTS;
  }

  public apply(chunk: PersistentRegionalEvolutionSnapshotMessage): void {
    const snapshotId = chunk.snapshotId ?? 0n;
    const chunkIndex = chunk.chunkIndex ?? 0;
    const chunkCount = chunk.chunkCount ?? 1;
    if (snapshotId === 0n && chunkIndex === 0 && chunkCount === 1) {
      this.applyLegacy(chunk);
      return;
    }
    this.applyBatch(chunk, snapshotId, chunkIndex, chunkCount);
  }

  /** Compatibility seam for deterministic tests and callers that already hold one full message. */
  public replace(snapshot: PersistentRegionalEvolutionSnapshotMessage): void {
    if (!snapshot.isFullSnapshot) throw new ProtocolDecodeFailure('PersistentRegionalEvolution replace requires a full snapshot chunk.');
    const chunkCount = snapshot.chunkCount ?? 1;
    if (chunkCount !== 1) throw new ProtocolDecodeFailure('PersistentRegionalEvolution replace requires one complete logical snapshot.');
    this.pending = null;
    this.applyLegacy(snapshot);
  }

  public clear(): void {
    this.pending = null;
    if (this.current === null) return;
    this.current = null;
    this.resetCollections();
    this.currentRevision += 1;
  }

  private applyBatch(chunk: PersistentRegionalEvolutionSnapshotMessage, snapshotId: bigint, chunkIndex: number, chunkCount: number): void {
    if (snapshotId <= 0n || !Number.isInteger(chunkIndex) || !Number.isInteger(chunkCount) || chunkCount <= 0
      || chunkCount > MAXIMUM_BATCH_CHUNKS || chunkIndex < 0 || chunkIndex >= chunkCount) {
      this.pending = null;
      throw new ProtocolDecodeFailure('PersistentRegionalEvolution batch metadata is invalid.');
    }

    if (chunkIndex === 0) {
      if (!chunk.isFullSnapshot) {
        this.pending = null;
        throw new ProtocolDecodeFailure('PersistentRegionalEvolution batch must start with a full snapshot chunk.');
      }
      this.pending = createPendingBatch(snapshotId, chunk.currentYear, chunk.tickCount, chunkCount);
    } else {
      const pending = this.pending;
      if (chunk.isFullSnapshot || pending === null || pending.snapshotId !== snapshotId || pending.currentYear !== chunk.currentYear
        || pending.tickCount !== chunk.tickCount || pending.chunkCount !== chunkCount || pending.nextChunkIndex !== chunkIndex) {
        this.pending = null;
        throw new ProtocolDecodeFailure('PersistentRegionalEvolution continuation chunk is missing, duplicated, out of order, or belongs to a different batch.');
      }
    }

    const pending = this.pending;
    if (pending === null) throw new ProtocolDecodeFailure('PersistentRegionalEvolution batch staging is unavailable.');
    try {
      validateAppend(pending, chunk);
      appendChunk(pending, chunk);
    } catch (error) {
      this.pending = null;
      throw error;
    }
    pending.nextChunkIndex = chunkIndex + 1;
    if (pending.nextChunkIndex === pending.chunkCount) this.commitPending(pending);
  }

  private commitPending(pending: PendingBatch): void {
    const settlements = Object.freeze([...pending.settlements.values()]);
    const parcels = Object.freeze([...pending.parcels.values()]);
    const buildings = Object.freeze([...pending.buildings.values()]);
    const serviceCatchments = Object.freeze([...pending.serviceCatchments]);
    const infrastructureDemands = Object.freeze([...pending.infrastructureDemands]);
    const relations = Object.freeze([...pending.relations.values()]);
    const events = Object.freeze([...pending.events.values()]);
    const commutingFlows = Object.freeze([...pending.commutingFlows]);
    const freightFlows = Object.freeze([...pending.freightFlows]);

    this.settlements = pending.settlements;
    this.parcels = pending.parcels;
    this.buildings = pending.buildings;
    this.serviceCatchments = pending.serviceCatchments;
    this.infrastructureDemands = pending.infrastructureDemands;
    this.relations = pending.relations;
    this.events = pending.events;
    this.commutingFlows = pending.commutingFlows;
    this.freightFlows = pending.freightFlows;
    this.relationsBySettlement = groupRelations(relations);
    this.eventsBySettlement = groupBy(events, (item) => item.settlementId);
    this.current = Object.freeze({
      type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
      currentYear: pending.currentYear,
      tickCount: pending.tickCount,
      settlements,
      parcels,
      buildings,
      serviceCatchments,
      infrastructureDemands,
      relations,
      events,
      commutingFlows,
      freightFlows,
      isFullSnapshot: true,
      snapshotId: pending.snapshotId,
      chunkIndex: 0,
      chunkCount: 1,
    });
    this.pending = null;
    this.currentRevision += 1;
  }

  private applyLegacy(chunk: PersistentRegionalEvolutionSnapshotMessage): void {
    this.pending = null;
    if (!chunk.isFullSnapshot && (this.current === null || this.current.currentYear !== chunk.currentYear || this.current.tickCount !== chunk.tickCount)) {
      throw new ProtocolDecodeFailure('PersistentRegionalEvolution continuation chunk does not match an active full snapshot batch.');
    }

    const base = chunk.isFullSnapshot ? createPendingBatch(0n, chunk.currentYear, chunk.tickCount, 1) : {
      snapshotId: 0n,
      currentYear: chunk.currentYear,
      tickCount: chunk.tickCount,
      chunkCount: 1,
      nextChunkIndex: 0,
      lastEventId: lastMapKey(this.events),
      settlements: this.settlements,
      parcels: this.parcels,
      buildings: this.buildings,
      serviceCatchments: this.serviceCatchments,
      infrastructureDemands: this.infrastructureDemands,
      relations: this.relations,
      events: this.events,
      commutingFlows: this.commutingFlows,
      freightFlows: this.freightFlows,
    } satisfies PendingBatch;

    validateAppend(base, chunk);
    if (chunk.isFullSnapshot) this.resetCollections();

    for (const item of chunk.settlements) this.settlements.set(item.settlementId, item);
    for (const item of chunk.parcels) this.parcels.set(item.parcelId, item);
    for (const item of chunk.buildings) this.buildings.set(item.buildingId, item);
    this.serviceCatchments.push(...chunk.serviceCatchments);
    this.infrastructureDemands.push(...chunk.infrastructureDemands);
    for (const item of chunk.relations) this.relations.set(item.relationId, item);
    for (const item of chunk.events) this.events.set(item.eventId, item);
    this.commutingFlows.push(...chunk.commutingFlows);
    this.freightFlows.push(...chunk.freightFlows);

    this.relationsBySettlement = groupRelations([...this.relations.values()]);
    this.eventsBySettlement = groupBy([...this.events.values()], (item) => item.settlementId);
    this.current = Object.freeze({
      type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
      currentYear: chunk.currentYear,
      tickCount: chunk.tickCount,
      settlements: Object.freeze([...this.settlements.values()]),
      parcels: Object.freeze([...this.parcels.values()]),
      buildings: Object.freeze([...this.buildings.values()]),
      serviceCatchments: Object.freeze([...this.serviceCatchments]),
      infrastructureDemands: Object.freeze([...this.infrastructureDemands]),
      relations: Object.freeze([...this.relations.values()]),
      events: Object.freeze([...this.events.values()]),
      commutingFlows: Object.freeze([...this.commutingFlows]),
      freightFlows: Object.freeze([...this.freightFlows]),
      isFullSnapshot: true,
      snapshotId: chunk.snapshotId ?? 0n,
      chunkIndex: 0,
      chunkCount: 1,
    });
    this.currentRevision += 1;
  }

  private resetCollections(): void {
    this.settlements.clear();
    this.parcels.clear();
    this.buildings.clear();
    this.serviceCatchments = [];
    this.infrastructureDemands = [];
    this.relations.clear();
    this.events.clear();
    this.commutingFlows = [];
    this.freightFlows = [];
    this.relationsBySettlement.clear();
    this.eventsBySettlement.clear();
  }
}

function createPendingBatch(snapshotId: bigint, currentYear: number, tickCount: bigint, chunkCount: number): PendingBatch {
  return {
    snapshotId,
    currentYear,
    tickCount,
    chunkCount,
    nextChunkIndex: 0,
    lastEventId: 0n,
    settlements: new Map(),
    parcels: new Map(),
    buildings: new Map(),
    serviceCatchments: [],
    infrastructureDemands: [],
    relations: new Map(),
    events: new Map(),
    commutingFlows: [],
    freightFlows: [],
  };
}

function validateAppend(pending: PendingBatch, chunk: PersistentRegionalEvolutionSnapshotMessage): void {
  ensureWithinLimit(pending.settlements.size, chunk.settlements.length, MAXIMUM_SETTLEMENTS, 'Settlement');
  ensureWithinLimit(pending.parcels.size, chunk.parcels.length, MAXIMUM_PARCELS, 'Parcel');
  ensureWithinLimit(pending.buildings.size, chunk.buildings.length, MAXIMUM_BUILDINGS, 'Building');
  ensureWithinLimit(pending.serviceCatchments.length, chunk.serviceCatchments.length, MAXIMUM_DERIVED_ITEMS, 'ServiceCatchment');
  ensureWithinLimit(pending.infrastructureDemands.length, chunk.infrastructureDemands.length, MAXIMUM_DERIVED_ITEMS, 'InfrastructureDemand');
  ensureWithinLimit(pending.relations.size, chunk.relations.length, MAXIMUM_DERIVED_ITEMS, 'Relation');
  ensureWithinLimit(pending.events.size, chunk.events.length, MAXIMUM_EVENTS, 'Event');
  ensureWithinLimit(pending.commutingFlows.length, chunk.commutingFlows.length, MAXIMUM_DERIVED_ITEMS, 'CommutingFlow');
  ensureWithinLimit(pending.freightFlows.length, chunk.freightFlows.length, MAXIMUM_DERIVED_ITEMS, 'FreightFlow');

  validateUniqueIds(pending.settlements, chunk.settlements.map((item) => item.settlementId), 'Settlement');
  validateUniqueIds(pending.parcels, chunk.parcels.map((item) => item.parcelId), 'Parcel');
  validateUniqueIds(pending.buildings, chunk.buildings.map((item) => item.buildingId), 'Building');
  validateUniqueIds(pending.relations, chunk.relations.map((item) => item.relationId), 'Relation');
  validateUniqueIds(pending.events, chunk.events.map((item) => item.eventId), 'Event');

  let previousEventId = pending.lastEventId;
  for (const item of chunk.events) {
    if (item.eventId <= previousEventId) throw new ProtocolDecodeFailure('PersistentRegionalEvolution Event IDs must increase across the complete batch.');
    previousEventId = item.eventId;
  }
}

function appendChunk(pending: PendingBatch, chunk: PersistentRegionalEvolutionSnapshotMessage): void {
  for (const item of chunk.settlements) pending.settlements.set(item.settlementId, item);
  for (const item of chunk.parcels) pending.parcels.set(item.parcelId, item);
  for (const item of chunk.buildings) pending.buildings.set(item.buildingId, item);
  pending.serviceCatchments.push(...chunk.serviceCatchments);
  pending.infrastructureDemands.push(...chunk.infrastructureDemands);
  for (const item of chunk.relations) pending.relations.set(item.relationId, item);
  for (const item of chunk.events) {
    pending.events.set(item.eventId, item);
    pending.lastEventId = item.eventId;
  }
  pending.commutingFlows.push(...chunk.commutingFlows);
  pending.freightFlows.push(...chunk.freightFlows);
}

function ensureWithinLimit(current: number, incoming: number, maximum: number, label: string): void {
  if (incoming > maximum - current) {
    throw new ProtocolDecodeFailure(`PersistentRegionalEvolution logical ${label} count exceeds the protocol limit.`);
  }
}

function validateUniqueIds<T>(map: Map<bigint, T>, ids: readonly bigint[], label: string): void {
  const incoming = new Set<bigint>();
  for (const id of ids) {
    if (map.has(id) || !incoming.add(id)) {
      throw new ProtocolDecodeFailure(`PersistentRegionalEvolution ${label} ID is duplicated across chunks.`);
    }
  }
}

function lastMapKey<T>(map: Map<bigint, T>): bigint {
  let last = 0n;
  for (const key of map.keys()) if (key > last) last = key;
  return last;
}

const EMPTY_RELATIONS: readonly RegionalRelationObservation[] = Object.freeze([]);
const EMPTY_EVENTS: readonly RegionalEvolutionEventObservation[] = Object.freeze([]);

function groupRelations(items: readonly RegionalRelationObservation[]): Map<bigint, readonly RegionalRelationObservation[]> {
  const mutable = new Map<bigint, RegionalRelationObservation[]>();
  for (const item of items) {
    append(mutable, item.fromSettlementId, item);
    append(mutable, item.toSettlementId, item);
  }
  return freezeGroups(mutable);
}

function groupBy<T>(items: readonly T[], key: (item: T) => bigint): Map<bigint, readonly T[]> {
  const mutable = new Map<bigint, T[]>();
  for (const item of items) append(mutable, key(item), item);
  return freezeGroups(mutable);
}

function append<T>(map: Map<bigint, T[]>, key: bigint, item: T): void {
  const group = map.get(key);
  if (group === undefined) map.set(key, [item]);
  else group.push(item);
}

function freezeGroups<T>(source: Map<bigint, T[]>): Map<bigint, readonly T[]> {
  const result = new Map<bigint, readonly T[]>();
  for (const [key, value] of source) result.set(key, Object.freeze([...value]));
  return result;
}
