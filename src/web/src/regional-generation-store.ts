import type {
  DistrictObservation,
  GeneratedBuildingObservation,
  GeneratedPoiObservation,
  HumanToponymObservation,
  ParcelObservation,
  RegionalCorridorObservation,
  RegionalGenerationSnapshotMessage,
  RoadSignObservation,
  SettlementObservation,
} from './regional-generation-protocol.ts';

export interface ReadonlyRegionalGenerationStore {
  readonly snapshot: RegionalGenerationSnapshotMessage | null;
  readonly revision: number;
  getSettlement(id: bigint): SettlementObservation | undefined;
  getDistrict(id: bigint): DistrictObservation | undefined;
  getParcel(id: bigint): ParcelObservation | undefined;
  getBuilding(id: bigint): GeneratedBuildingObservation | undefined;
  getPoi(id: bigint): GeneratedPoiObservation | undefined;
  getToponym(id: bigint): HumanToponymObservation | undefined;
  getCorridor(id: bigint): RegionalCorridorObservation | undefined;
  getRoadSign(id: bigint): RoadSignObservation | undefined;
  getSettlementForDistrict(districtId: bigint): SettlementObservation | undefined;
  getSettlementForParcel(parcelId: bigint): SettlementObservation | undefined;
  getDistrictForParcel(parcelId: bigint): DistrictObservation | undefined;
  getParcelForBuilding(buildingId: bigint): ParcelObservation | undefined;
  getDistrictForBuilding(buildingId: bigint): DistrictObservation | undefined;
  getSettlementForBuilding(buildingId: bigint): SettlementObservation | undefined;
  getBuildingForPoi(poiId: bigint): GeneratedBuildingObservation | undefined;
  getSettlementForPoi(poiId: bigint): SettlementObservation | undefined;
}

/** Read-only View index over Simulation-provided Regional Generation stable IDs. */
export class RegionalGenerationStore implements ReadonlyRegionalGenerationStore {
  private current: RegionalGenerationSnapshotMessage | null = null;
  private currentRevision = 0;
  private settlements = new Map<bigint, SettlementObservation>();
  private districts = new Map<bigint, DistrictObservation>();
  private parcels = new Map<bigint, ParcelObservation>();
  private buildings = new Map<bigint, GeneratedBuildingObservation>();
  private pois = new Map<bigint, GeneratedPoiObservation>();
  private toponyms = new Map<bigint, HumanToponymObservation>();
  private corridors = new Map<bigint, RegionalCorridorObservation>();
  private roadSigns = new Map<bigint, RoadSignObservation>();

  public get snapshot(): RegionalGenerationSnapshotMessage | null { return this.current; }
  public get revision(): number { return this.currentRevision; }
  public getSettlement(id: bigint): SettlementObservation | undefined { return this.settlements.get(id); }
  public getDistrict(id: bigint): DistrictObservation | undefined { return this.districts.get(id); }
  public getParcel(id: bigint): ParcelObservation | undefined { return this.parcels.get(id); }
  public getBuilding(id: bigint): GeneratedBuildingObservation | undefined { return this.buildings.get(id); }
  public getPoi(id: bigint): GeneratedPoiObservation | undefined { return this.pois.get(id); }
  public getToponym(id: bigint): HumanToponymObservation | undefined { return this.toponyms.get(id); }
  public getCorridor(id: bigint): RegionalCorridorObservation | undefined { return this.corridors.get(id); }
  public getRoadSign(id: bigint): RoadSignObservation | undefined { return this.roadSigns.get(id); }

  public getSettlementForDistrict(districtId: bigint): SettlementObservation | undefined {
    const district = this.districts.get(districtId);
    return district === undefined ? undefined : this.settlements.get(district.settlementId);
  }

  public getSettlementForParcel(parcelId: bigint): SettlementObservation | undefined {
    const parcel = this.parcels.get(parcelId);
    return parcel === undefined ? undefined : this.settlements.get(parcel.settlementId);
  }

  public getDistrictForParcel(parcelId: bigint): DistrictObservation | undefined {
    const parcel = this.parcels.get(parcelId);
    return parcel === undefined ? undefined : this.districts.get(parcel.districtId);
  }

  public getParcelForBuilding(buildingId: bigint): ParcelObservation | undefined {
    const building = this.buildings.get(buildingId);
    return building === undefined ? undefined : this.parcels.get(building.parcelId);
  }

  public getDistrictForBuilding(buildingId: bigint): DistrictObservation | undefined {
    const parcel = this.getParcelForBuilding(buildingId);
    return parcel === undefined ? undefined : this.districts.get(parcel.districtId);
  }

  public getSettlementForBuilding(buildingId: bigint): SettlementObservation | undefined {
    const parcel = this.getParcelForBuilding(buildingId);
    return parcel === undefined ? undefined : this.settlements.get(parcel.settlementId);
  }

  public getBuildingForPoi(poiId: bigint): GeneratedBuildingObservation | undefined {
    const poi = this.pois.get(poiId);
    if (poi === undefined || poi.buildingId === 0n) return undefined;
    return this.buildings.get(poi.buildingId);
  }

  public getSettlementForPoi(poiId: bigint): SettlementObservation | undefined {
    const poi = this.pois.get(poiId);
    return poi === undefined ? undefined : this.settlements.get(poi.settlementId);
  }

  public replace(snapshot: RegionalGenerationSnapshotMessage): void {
    this.current = snapshot;
    this.settlements = indexBy(snapshot.settlements, (item) => item.settlementId);
    this.districts = indexBy(snapshot.districts, (item) => item.districtId);
    this.parcels = indexBy(snapshot.parcels, (item) => item.parcelId);
    this.buildings = indexBy(snapshot.buildings, (item) => item.buildingId);
    this.pois = indexBy(snapshot.pois, (item) => item.poiId);
    this.toponyms = indexBy(snapshot.toponyms, (item) => item.toponymId);
    this.corridors = indexBy(snapshot.corridors, (item) => item.corridorId);
    this.roadSigns = indexBy(snapshot.roadSigns, (item) => item.roadSignId);
    this.currentRevision += 1;
  }

  public clear(): void {
    if (this.current === null) return;
    this.current = null;
    this.settlements.clear(); this.districts.clear(); this.parcels.clear(); this.buildings.clear();
    this.pois.clear(); this.toponyms.clear(); this.corridors.clear(); this.roadSigns.clear();
    this.currentRevision += 1;
  }
}

function indexBy<T>(items: readonly T[], key: (item: T) => bigint): Map<bigint, T> {
  const result = new Map<bigint, T>();
  for (const item of items) result.set(key(item), item);
  return result;
}
