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

/**
 * Read-only View index over authoritative Protocol 2.19 persistent regional evolution state.
 * The server publishes a full logical snapshot as ordered chunks. The first chunk resets the
 * batch (`isFullSnapshot=true`); following chunks append to the same currentYear/tickCount.
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
    if (chunk.isFullSnapshot) {
      this.resetCollections();
    } else if (this.current === null || this.current.currentYear !== chunk.currentYear || this.current.tickCount !== chunk.tickCount) {
      throw new ProtocolDecodeFailure('PersistentRegionalEvolution continuation chunk does not match an active full snapshot batch.');
    }

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
    });
    this.currentRevision += 1;
  }

  /** Compatibility seam for deterministic tests and callers that already hold one full message. */
  public replace(snapshot: PersistentRegionalEvolutionSnapshotMessage): void {
    if (!snapshot.isFullSnapshot) throw new ProtocolDecodeFailure('PersistentRegionalEvolution replace requires a full snapshot chunk.');
    this.apply(snapshot);
  }

  public clear(): void {
    if (this.current === null) return;
    this.current = null;
    this.resetCollections();
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
