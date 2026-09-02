import type {
  BuildingLifecycleObservation,
  ParcelEvolutionObservation,
  PersistentRegionalEvolutionSnapshotMessage,
  RegionalEvolutionEventObservation,
  RegionalRelationObservation,
  SettlementEvolutionObservation,
} from './persistent-regional-evolution-protocol.ts';

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

/** Read-only View index over authoritative Protocol 2.19 persistent regional evolution state. */
export class PersistentRegionalEvolutionStore implements ReadonlyPersistentRegionalEvolutionStore {
  private current: PersistentRegionalEvolutionSnapshotMessage | null = null;
  private currentRevision = 0;
  private settlements = new Map<bigint, SettlementEvolutionObservation>();
  private parcels = new Map<bigint, ParcelEvolutionObservation>();
  private buildings = new Map<bigint, BuildingLifecycleObservation>();
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

  public replace(snapshot: PersistentRegionalEvolutionSnapshotMessage): void {
    this.current = snapshot;
    this.settlements = indexBy(snapshot.settlements, (item) => item.settlementId);
    this.parcels = indexBy(snapshot.parcels, (item) => item.parcelId);
    this.buildings = indexBy(snapshot.buildings, (item) => item.buildingId);
    this.relationsBySettlement = groupRelations(snapshot.relations);
    this.eventsBySettlement = groupBy(snapshot.events, (item) => item.settlementId);
    this.currentRevision += 1;
  }

  public clear(): void {
    if (this.current === null) return;
    this.current = null;
    this.settlements.clear();
    this.parcels.clear();
    this.buildings.clear();
    this.relationsBySettlement.clear();
    this.eventsBySettlement.clear();
    this.currentRevision += 1;
  }
}

const EMPTY_RELATIONS: readonly RegionalRelationObservation[] = Object.freeze([]);
const EMPTY_EVENTS: readonly RegionalEvolutionEventObservation[] = Object.freeze([]);

function indexBy<T>(items: readonly T[], key: (item: T) => bigint): Map<bigint, T> {
  const result = new Map<bigint, T>();
  for (const item of items) result.set(key(item), item);
  return result;
}

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
