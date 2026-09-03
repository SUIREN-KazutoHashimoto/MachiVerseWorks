import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

export const REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE = 810;
const REGIONAL_GENERATION_PROTOCOL_MINOR = 18;
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
const MAXIMUM_TEXT_LENGTH = 256;
const INT32_MAX = 2_147_483_647;
const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

type WireUInt64 = string | number;

export enum SettlementOriginKind { InlandPlain = 0, RiverPlain = 1, Estuary = 2, Bay = 3, Basin = 4, Valley = 5, MountainPass = 6, ResourceAccess = 7, Coastal = 8, Island = 9 }
export enum RegionalRole { LocalService = 0, Agricultural = 1, Market = 2, Administrative = 3, Industrial = 4, Port = 5, TransportHub = 6, Resource = 7 }
export enum InitialEconomyKind { Subsistence = 0, Agriculture = 1, Trade = 2, Manufacturing = 3, PortTrade = 4, Transport = 5, ResourceExtraction = 6, Services = 7 }
export enum HistoricalGrowthStage { Origin = 0, CenterFormation = 1, UrbanExpansion = 2, Suburbanization = 3, Redevelopment = 4, NewCenterFormation = 5 }
export enum RegionalCorridorKind { PrimaryRoad = 0, RegionalRoad = 1, IntercityRoad = 2, Railway = 3 }
export enum DistrictKind { OldTown = 0, CentralBusiness = 1, StationDistrict = 2, IndustrialArea = 3, Suburb = 4, ResidentialQuarter = 5 }
export enum ZoneKind { Residential = 0, Commercial = 1, Industrial = 2, MixedUse = 3, Civic = 4, Agricultural = 5, OpenSpace = 6 }
export enum ParcelDevelopmentState { Vacant = 0, Developing = 1, Occupied = 2, Redeveloping = 3 }
export enum GeneratedBuildingUse { Residential = 0, Commercial = 1, Industrial = 2, MixedUse = 3, Civic = 4, Transport = 5, Utility = 6 }
export enum GeneratedPoiKind { SettlementCenter = 0, Market = 1, Station = 2, CivicCenter = 3, IndustrialHub = 4, Port = 5 }
export enum HumanToponymKind { Settlement = 0, District = 1, Road = 2, Bridge = 3, Tunnel = 4, Station = 5 }
export enum RoadSignKind { Direction = 0, PlaceName = 1, SteepGrade = 2, SharpCurve = 3, FloodWarning = 4, RiverCrossing = 5, MountainPass = 6, Tunnel = 7, CoastalLowland = 8, RockSlope = 9 }
export enum RegionalGenerationQualityPreset { Draft = 0, Standard = 1, HighQuality = 2 }

export interface SettlementSuitabilityObservation {
  readonly flatness: number;
  readonly waterAccess: number;
  readonly transportPotential: number;
  readonly buildability: number;
  readonly resourceAccess: number;
  readonly floodRisk: number;
  readonly steepSlopeRisk: number;
  readonly isolation: number;
  readonly constructionCost: number;
  readonly totalScore: number;
}

export interface SettlementObservation {
  readonly settlementId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  /** Authoritative Simulation environment classification. View must not reclassify it. */
  readonly environment: number;
  readonly origin: SettlementOriginKind;
  readonly role: RegionalRole;
  readonly initialEconomy: InitialEconomyKind;
  readonly suitability: SettlementSuitabilityObservation;
  readonly population: number;
  readonly jobs: number;
  readonly influenceRadiusMeters: number;
  readonly nameId: bigint;
}

export interface HistoricalGrowthEventObservation {
  readonly eventId: bigint;
  readonly settlementId: bigint;
  readonly stage: HistoricalGrowthStage;
  readonly sequence: number;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly populationDelta: number;
  readonly jobDelta: number;
  readonly reason: string;
}

export interface RegionalPointObservation { readonly x: number; readonly y: number; readonly z: number; }

export interface RegionalCorridorObservation {
  readonly corridorId: bigint;
  readonly kind: RegionalCorridorKind;
  readonly fromSettlementId: bigint;
  readonly toSettlementId: bigint;
  readonly geometry: readonly RegionalPointObservation[];
  readonly terrainAdaptation: number;
  readonly constructionCost: number;
  readonly nameId: bigint;
}

export interface DistrictObservation {
  readonly districtId: bigint;
  readonly settlementId: bigint;
  readonly kind: DistrictKind;
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
  readonly nameId: bigint;
  readonly accessibility: number;
}

export interface ParcelObservation {
  readonly parcelId: bigint;
  readonly settlementId: bigint;
  readonly districtId: bigint;
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
  readonly zone: ZoneKind;
  readonly developmentState: ParcelDevelopmentState;
  readonly developmentSuitability: number;
  readonly landValue: number;
  readonly buildingId: bigint;
}

export interface GeneratedBuildingObservation {
  readonly buildingId: bigint;
  readonly parcelId: bigint;
  readonly use: GeneratedBuildingUse;
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
  readonly floors: number;
  readonly capacity: number;
  readonly historicalStage: number;
}

export interface GeneratedPoiObservation {
  readonly poiId: bigint;
  readonly settlementId: bigint;
  readonly kind: GeneratedPoiKind;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly buildingId: bigint;
  readonly nameId: bigint;
}

export interface HumanToponymObservation {
  readonly toponymId: bigint;
  readonly kind: HumanToponymKind;
  readonly name: string;
  readonly sourceNaturalToponymId: bigint;
  readonly sourceNaturalName: string;
  readonly sourceFeatureId: bigint;
  readonly parentHumanToponymId: bigint;
  readonly generatorKey: string;
}

export interface RoadSignObservation {
  readonly roadSignId: bigint;
  readonly kind: RoadSignKind;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly corridorId: bigint;
  readonly destinationSettlementId: bigint;
  readonly featureId: bigint;
  readonly text: string;
}

export interface RegionalQualityObservation {
  readonly terrainAdaptation: number;
  readonly roadConnectivity: number;
  readonly averageSlopeCost: number;
  readonly accessibility: number;
  readonly congestionRisk: number;
  readonly landUseConsistency: number;
  readonly floodExposure: number;
  readonly urbanCompactness: number;
  readonly polycentricBalance: number;
  readonly overallScore: number;
}

export interface RegionalGenerationSnapshotMessage {
  readonly type: typeof REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE;
  readonly tickCount: bigint;
  readonly worldSeed: bigint;
  readonly preset: RegionalGenerationQualityPreset;
  readonly iterations: number;
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
  readonly settlements: readonly SettlementObservation[];
  readonly growthEvents: readonly HistoricalGrowthEventObservation[];
  readonly corridors: readonly RegionalCorridorObservation[];
  readonly districts: readonly DistrictObservation[];
  readonly parcels: readonly ParcelObservation[];
  readonly buildings: readonly GeneratedBuildingObservation[];
  readonly pois: readonly GeneratedPoiObservation[];
  readonly toponyms: readonly HumanToponymObservation[];
  readonly roadSigns: readonly RoadSignObservation[];
  readonly quality: RegionalQualityObservation;
}

export interface RegionalGenerationProtocolEnvelope {
  readonly version: ProtocolVersion;
  readonly message: RegionalGenerationSnapshotMessage;
}

interface WireSettlement extends Omit<SettlementObservation, 'settlementId' | 'nameId'> { readonly settlementId: WireUInt64; readonly nameId: WireUInt64; }
interface WireGrowthEvent extends Omit<HistoricalGrowthEventObservation, 'eventId' | 'settlementId'> { readonly eventId: WireUInt64; readonly settlementId: WireUInt64; }
interface WireCorridor extends Omit<RegionalCorridorObservation, 'corridorId' | 'fromSettlementId' | 'toSettlementId' | 'nameId'> { readonly corridorId: WireUInt64; readonly fromSettlementId: WireUInt64; readonly toSettlementId: WireUInt64; readonly nameId: WireUInt64; }
interface WireDistrict extends Omit<DistrictObservation, 'districtId' | 'settlementId' | 'nameId'> { readonly districtId: WireUInt64; readonly settlementId: WireUInt64; readonly nameId: WireUInt64; }
interface WireParcel extends Omit<ParcelObservation, 'parcelId' | 'settlementId' | 'districtId' | 'buildingId'> { readonly parcelId: WireUInt64; readonly settlementId: WireUInt64; readonly districtId: WireUInt64; readonly buildingId: WireUInt64; }
interface WireBuilding extends Omit<GeneratedBuildingObservation, 'buildingId' | 'parcelId'> { readonly buildingId: WireUInt64; readonly parcelId: WireUInt64; }
interface WirePoi extends Omit<GeneratedPoiObservation, 'poiId' | 'settlementId' | 'buildingId' | 'nameId'> { readonly poiId: WireUInt64; readonly settlementId: WireUInt64; readonly buildingId: WireUInt64; readonly nameId: WireUInt64; }
interface WireToponym extends Omit<HumanToponymObservation, 'toponymId' | 'sourceNaturalToponymId' | 'sourceFeatureId' | 'parentHumanToponymId'> { readonly toponymId: WireUInt64; readonly sourceNaturalToponymId: WireUInt64; readonly sourceFeatureId: WireUInt64; readonly parentHumanToponymId: WireUInt64; }
interface WireRoadSign extends Omit<RoadSignObservation, 'roadSignId' | 'corridorId' | 'destinationSettlementId' | 'featureId'> { readonly roadSignId: WireUInt64; readonly corridorId: WireUInt64; readonly destinationSettlementId: WireUInt64; readonly featureId: WireUInt64; }
interface WireRegionalGenerationSnapshot extends Omit<RegionalGenerationSnapshotMessage, 'type' | 'tickCount' | 'worldSeed' | 'settlements' | 'growthEvents' | 'corridors' | 'districts' | 'parcels' | 'buildings' | 'pois' | 'toponyms' | 'roadSigns'> {
  readonly tickCount: WireUInt64;
  readonly worldSeed: WireUInt64;
  readonly settlements: readonly WireSettlement[];
  readonly growthEvents: readonly WireGrowthEvent[];
  readonly corridors: readonly WireCorridor[];
  readonly districts: readonly WireDistrict[];
  readonly parcels: readonly WireParcel[];
  readonly buildings: readonly WireBuilding[];
  readonly pois: readonly WirePoi[];
  readonly toponyms: readonly WireToponym[];
  readonly roadSigns: readonly WireRoadSign[];
}

export function isRegionalGenerationFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeRegionalGenerationFrame(frame: ArrayBuffer): RegionalGenerationProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('RegionalGeneration frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC || view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Invalid RegionalGeneration frame header.');
  if (view.getUint16(8, true) !== REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Unknown RegionalGeneration message type.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < REGIONAL_GENERATION_PROTOCOL_MINOR) throw new ProtocolDecodeFailure('RegionalGeneration snapshots require Protocol 2.18 or newer.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('RegionalGeneration frame length is invalid.');

  let raw: WireRegionalGenerationSnapshot;
  try {
    const json = utf8Decoder.decode(new Uint8Array(frame, PROTOCOL_HEADER_SIZE, payloadLength));
    raw = JSON.parse(quoteLosslessUInt64Properties(json)) as WireRegionalGenerationSnapshot;
  } catch (error) {
    if (error instanceof ProtocolDecodeFailure) throw error;
    throw new ProtocolDecodeFailure('RegionalGeneration snapshot is not valid UTF-8 JSON.');
  }
  return { version, message: normalizeAndValidateSnapshot(raw) };
}

function normalizeAndValidateSnapshot(raw: WireRegionalGenerationSnapshot): RegionalGenerationSnapshotMessage {
  if (!isRecord(raw) || !Array.isArray(raw.settlements) || !Array.isArray(raw.growthEvents) || !Array.isArray(raw.corridors)
    || !Array.isArray(raw.districts) || !Array.isArray(raw.parcels) || !Array.isArray(raw.buildings) || !Array.isArray(raw.pois)
    || !Array.isArray(raw.toponyms) || !Array.isArray(raw.roadSigns) || !isRecord(raw.quality)) throw new ProtocolDecodeFailure('RegionalGeneration snapshot shape is invalid.');
  if (raw.settlements.length > MAXIMUM_SETTLEMENTS || raw.growthEvents.length > MAXIMUM_GROWTH_EVENTS || raw.corridors.length > MAXIMUM_CORRIDORS
    || raw.districts.length > MAXIMUM_DISTRICTS || raw.parcels.length > MAXIMUM_PARCELS || raw.buildings.length > MAXIMUM_BUILDINGS
    || raw.pois.length > MAXIMUM_POIS || raw.toponyms.length > MAXIMUM_TOPONYMS || raw.roadSigns.length > MAXIMUM_ROAD_SIGNS) throw new ProtocolDecodeFailure('RegionalGeneration snapshot collection counts are invalid.');
  validateVolume(raw, true);
  if (!enumRange(raw.preset, RegionalGenerationQualityPreset.Draft, RegionalGenerationQualityPreset.HighQuality) || !integerRange(raw.iterations, 0, 32)) throw new ProtocolDecodeFailure('RegionalGeneration generation metadata is invalid.');
  const quality = normalizeQuality(raw.quality as unknown as RegionalQualityObservation);
  const toponyms = Object.freeze(raw.toponyms.map(normalizeToponym));
  const toponymIds = uniquePositiveIds(toponyms.map((item) => item.toponymId), 'Toponym');
  for (const item of toponyms) if (item.parentHumanToponymId !== 0n && !toponymIds.has(item.parentHumanToponymId)) throw new ProtocolDecodeFailure('RegionalGeneration Toponym parent reference is invalid.');
  assertAcyclicParents(toponyms.map((item) => [item.toponymId, item.parentHumanToponymId] as const), 'RegionalGeneration Toponym');

  const settlements = Object.freeze(raw.settlements.map(normalizeSettlement));
  const settlementIds = uniquePositiveIds(settlements.map((item) => item.settlementId), 'Settlement');
  for (const item of settlements) if (!toponymIds.has(item.nameId)) throw new ProtocolDecodeFailure('RegionalGeneration Settlement name reference is invalid.');

  const growthEvents = Object.freeze(raw.growthEvents.map(normalizeGrowthEvent));
  uniquePositiveIds(growthEvents.map((item) => item.eventId), 'GrowthEvent');
  for (const item of growthEvents) if (!settlementIds.has(item.settlementId)) throw new ProtocolDecodeFailure('RegionalGeneration GrowthEvent settlement reference is invalid.');

  const corridors = Object.freeze(raw.corridors.map(normalizeCorridor));
  const corridorIds = uniquePositiveIds(corridors.map((item) => item.corridorId), 'Corridor');
  for (const item of corridors) {
    if (!settlementIds.has(item.fromSettlementId) || !settlementIds.has(item.toSettlementId) || item.fromSettlementId === item.toSettlementId) throw new ProtocolDecodeFailure('RegionalGeneration Corridor settlement reference is invalid.');
    if (item.nameId !== 0n && !toponymIds.has(item.nameId)) throw new ProtocolDecodeFailure('RegionalGeneration Corridor name reference is invalid.');
  }

  const districts = Object.freeze(raw.districts.map(normalizeDistrict));
  uniquePositiveIds(districts.map((item) => item.districtId), 'District');
  for (const item of districts) {
    if (!settlementIds.has(item.settlementId)) throw new ProtocolDecodeFailure('RegionalGeneration District settlement reference is invalid.');
    if (!toponymIds.has(item.nameId)) throw new ProtocolDecodeFailure('RegionalGeneration District name reference is invalid.');
  }

  const districtById = new Map(districts.map((item) => [item.districtId, item] as const));
  const parcels = Object.freeze(raw.parcels.map(normalizeParcel));
  const parcelIds = uniquePositiveIds(parcels.map((item) => item.parcelId), 'Parcel');
  for (const item of parcels) {
    const district = districtById.get(item.districtId);
    if (!settlementIds.has(item.settlementId) || district === undefined || district.settlementId !== item.settlementId) throw new ProtocolDecodeFailure('RegionalGeneration Parcel hierarchy is invalid.');
  }

  const buildings = Object.freeze(raw.buildings.map(normalizeBuilding));
  const buildingIds = uniquePositiveIds(buildings.map((item) => item.buildingId), 'Building');
  const parcelById = new Map(parcels.map((item) => [item.parcelId, item] as const));
  const buildingById = new Map(buildings.map((item) => [item.buildingId, item] as const));
  const occupiedParcels = new Set<bigint>();
  for (const building of buildings) {
    const parcel = parcelById.get(building.parcelId);
    if (parcel === undefined || parcel.buildingId !== building.buildingId || occupiedParcels.has(building.parcelId) || !containsHorizontal(parcel, building)) throw new ProtocolDecodeFailure('RegionalGeneration Building ownership or containment is invalid.');
    occupiedParcels.add(building.parcelId);
  }
  for (const parcel of parcels) if (parcel.buildingId !== 0n && buildingById.get(parcel.buildingId)?.parcelId !== parcel.parcelId) throw new ProtocolDecodeFailure('RegionalGeneration Parcel/Building reciprocal reference is invalid.');
  for (const item of buildings) if (!parcelIds.has(item.parcelId)) throw new ProtocolDecodeFailure('RegionalGeneration Building parcel reference is invalid.');
  for (const item of parcels) if (item.buildingId !== 0n && !buildingIds.has(item.buildingId)) throw new ProtocolDecodeFailure('RegionalGeneration Parcel building reference is invalid.');

  const pois = Object.freeze(raw.pois.map(normalizePoi));
  uniquePositiveIds(pois.map((item) => item.poiId), 'POI');
  for (const item of pois) {
    if (!settlementIds.has(item.settlementId)) throw new ProtocolDecodeFailure('RegionalGeneration POI settlement reference is invalid.');
    if (item.buildingId !== 0n) {
      const building = buildingById.get(item.buildingId); const parcel = building === undefined ? undefined : parcelById.get(building.parcelId);
      if (building === undefined || parcel === undefined || parcel.settlementId !== item.settlementId) throw new ProtocolDecodeFailure('RegionalGeneration POI building hierarchy is invalid.');
    }
    if (item.nameId !== 0n && !toponymIds.has(item.nameId)) throw new ProtocolDecodeFailure('RegionalGeneration POI name reference is invalid.');
  }

  const roadSigns = Object.freeze(raw.roadSigns.map(normalizeRoadSign));
  uniquePositiveIds(roadSigns.map((item) => item.roadSignId), 'RoadSign');
  for (const item of roadSigns) {
    if (!corridorIds.has(item.corridorId)) throw new ProtocolDecodeFailure('RegionalGeneration RoadSign corridor reference is invalid.');
    if (item.destinationSettlementId !== 0n && !settlementIds.has(item.destinationSettlementId)) throw new ProtocolDecodeFailure('RegionalGeneration RoadSign destination reference is invalid.');
  }

  return Object.freeze({
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: parseUInt64(raw.tickCount, 'RegionalGeneration tick count'),
    worldSeed: parsePositiveUInt64(raw.worldSeed, 'RegionalGeneration world seed'),
    preset: raw.preset,
    iterations: raw.iterations,
    minX: raw.minX, minY: raw.minY, minZ: raw.minZ, maxX: raw.maxX, maxY: raw.maxY, maxZ: raw.maxZ,
    settlements, growthEvents, corridors, districts, parcels, buildings, pois, toponyms, roadSigns, quality,
  });
}

function assertAcyclicParents(nodes: readonly (readonly [bigint, bigint])[], name: string): void {
  const parents = new Map(nodes);
  for (const start of parents.keys()) {
    const seen = new Set<bigint>(); let current = start;
    while (true) {
      const parent = parents.get(current); if (parent === undefined || parent === 0n) break;
      if (seen.has(current)) throw new ProtocolDecodeFailure(`${name} parent graph contains a cycle.`);
      seen.add(current); current = parent;
    }
  }
}

function containsHorizontal(outer: { readonly minX:number; readonly minY:number; readonly maxX:number; readonly maxY:number }, inner: { readonly minX:number; readonly minY:number; readonly maxX:number; readonly maxY:number }): boolean {
  return inner.minX >= outer.minX && inner.maxX <= outer.maxX && inner.minY >= outer.minY && inner.maxY <= outer.maxY;
}

function normalizeSettlement(raw: WireSettlement): SettlementObservation {
  if (!isRecord(raw) || !isRecord(raw.suitability) || !validPoint(raw) || !enumRange(raw.environment, 0, 7)
    || !enumRange(raw.origin, SettlementOriginKind.InlandPlain, SettlementOriginKind.Island) || !enumRange(raw.role, RegionalRole.LocalService, RegionalRole.Resource)
    || !enumRange(raw.initialEconomy, InitialEconomyKind.Subsistence, InitialEconomyKind.Services) || !integerRange(raw.population, 0, INT32_MAX)
    || !integerRange(raw.jobs, 0, INT32_MAX) || !positive(raw.influenceRadiusMeters)) throw new ProtocolDecodeFailure('RegionalGeneration Settlement values are invalid.');
  return Object.freeze({ ...raw, settlementId: parsePositiveUInt64(raw.settlementId, 'Settlement ID'), nameId: parsePositiveUInt64(raw.nameId, 'Settlement name ID'), suitability: normalizeSuitability(raw.suitability as unknown as SettlementSuitabilityObservation) });
}

function normalizeGrowthEvent(raw: WireGrowthEvent): HistoricalGrowthEventObservation {
  if (!isRecord(raw) || !validPoint(raw) || !enumRange(raw.stage, HistoricalGrowthStage.Origin, HistoricalGrowthStage.NewCenterFormation)
    || !integerRange(raw.sequence, 0, INT32_MAX) || !integerRange(raw.populationDelta, 0, INT32_MAX)
    || !integerRange(raw.jobDelta, 0, INT32_MAX) || !validText(raw.reason, MAXIMUM_TEXT_LENGTH)) throw new ProtocolDecodeFailure('RegionalGeneration GrowthEvent values are invalid.');
  return Object.freeze({ ...raw, eventId: parsePositiveUInt64(raw.eventId, 'GrowthEvent ID'), settlementId: parsePositiveUInt64(raw.settlementId, 'GrowthEvent settlement ID') });
}

function normalizeCorridor(raw: WireCorridor): RegionalCorridorObservation {
  if (!isRecord(raw) || !Array.isArray(raw.geometry) || raw.geometry.length < 2 || raw.geometry.length > MAXIMUM_CORRIDOR_GEOMETRY_POINTS
    || !enumRange(raw.kind, RegionalCorridorKind.PrimaryRoad, RegionalCorridorKind.Railway) || !unit(raw.terrainAdaptation) || !nonNegative(raw.constructionCost)) throw new ProtocolDecodeFailure('RegionalGeneration Corridor values are invalid.');
  const geometry = Object.freeze(raw.geometry.map((point) => {
    if (!validPoint(point)) throw new ProtocolDecodeFailure('RegionalGeneration Corridor geometry is invalid.');
    return Object.freeze({ x: point.x, y: point.y, z: point.z });
  }));
  return Object.freeze({ ...raw, corridorId: parsePositiveUInt64(raw.corridorId, 'Corridor ID'), fromSettlementId: parsePositiveUInt64(raw.fromSettlementId, 'Corridor from settlement ID'), toSettlementId: parsePositiveUInt64(raw.toSettlementId, 'Corridor to settlement ID'), nameId: parseUInt64(raw.nameId, 'Corridor name ID'), geometry });
}

function normalizeDistrict(raw: WireDistrict): DistrictObservation {
  if (!isRecord(raw) || !validVolume(raw, true) || !enumRange(raw.kind, DistrictKind.OldTown, DistrictKind.ResidentialQuarter) || !unit(raw.accessibility)) throw new ProtocolDecodeFailure('RegionalGeneration District values are invalid.');
  return Object.freeze({ ...raw, districtId: parsePositiveUInt64(raw.districtId, 'District ID'), settlementId: parsePositiveUInt64(raw.settlementId, 'District settlement ID'), nameId: parsePositiveUInt64(raw.nameId, 'District name ID') });
}

function normalizeParcel(raw: WireParcel): ParcelObservation {
  if (!isRecord(raw) || !validVolume(raw, true) || !enumRange(raw.zone, ZoneKind.Residential, ZoneKind.OpenSpace)
    || !enumRange(raw.developmentState, ParcelDevelopmentState.Vacant, ParcelDevelopmentState.Redeveloping) || !unit(raw.developmentSuitability) || !unit(raw.landValue)) throw new ProtocolDecodeFailure('RegionalGeneration Parcel values are invalid.');
  return Object.freeze({ ...raw, parcelId: parsePositiveUInt64(raw.parcelId, 'Parcel ID'), settlementId: parsePositiveUInt64(raw.settlementId, 'Parcel settlement ID'), districtId: parsePositiveUInt64(raw.districtId, 'Parcel district ID'), buildingId: parseUInt64(raw.buildingId, 'Parcel building ID') });
}

function normalizeBuilding(raw: WireBuilding): GeneratedBuildingObservation {
  if (!isRecord(raw) || !validVolume(raw, true) || !enumRange(raw.use, GeneratedBuildingUse.Residential, GeneratedBuildingUse.Utility)
    || !integerRange(raw.floors, 1, 256) || !integerRange(raw.capacity, 0, INT32_MAX) || !integerRange(raw.historicalStage, 0, INT32_MAX)) throw new ProtocolDecodeFailure('RegionalGeneration Building values are invalid.');
  return Object.freeze({ ...raw, buildingId: parsePositiveUInt64(raw.buildingId, 'Building ID'), parcelId: parsePositiveUInt64(raw.parcelId, 'Building parcel ID') });
}

function normalizePoi(raw: WirePoi): GeneratedPoiObservation {
  if (!isRecord(raw) || !validPoint(raw) || !enumRange(raw.kind, GeneratedPoiKind.SettlementCenter, GeneratedPoiKind.Port)) throw new ProtocolDecodeFailure('RegionalGeneration POI values are invalid.');
  return Object.freeze({ ...raw, poiId: parsePositiveUInt64(raw.poiId, 'POI ID'), settlementId: parsePositiveUInt64(raw.settlementId, 'POI settlement ID'), buildingId: parseUInt64(raw.buildingId, 'POI building ID'), nameId: parseUInt64(raw.nameId, 'POI name ID') });
}

function normalizeToponym(raw: WireToponym): HumanToponymObservation {
  if (!isRecord(raw) || !enumRange(raw.kind, HumanToponymKind.Settlement, HumanToponymKind.Station) || !validText(raw.name, 160)
    || typeof raw.sourceNaturalName !== 'string' || raw.sourceNaturalName.length > 160 || !validText(raw.generatorKey, 128)) throw new ProtocolDecodeFailure('RegionalGeneration Toponym values are invalid.');
  const sourceNaturalToponymId = parseUInt64(raw.sourceNaturalToponymId, 'Toponym natural source ID');
  if (sourceNaturalToponymId === 0n && raw.sourceNaturalName.length !== 0) throw new ProtocolDecodeFailure('RegionalGeneration Toponym natural provenance is invalid.');
  if (sourceNaturalToponymId !== 0n && raw.sourceNaturalName.trim().length === 0) throw new ProtocolDecodeFailure('RegionalGeneration Toponym natural provenance is invalid.');
  return Object.freeze({ ...raw, toponymId: parsePositiveUInt64(raw.toponymId, 'Toponym ID'), sourceNaturalToponymId, sourceFeatureId: parseUInt64(raw.sourceFeatureId, 'Toponym source feature ID'), parentHumanToponymId: parseUInt64(raw.parentHumanToponymId, 'Toponym parent ID') });
}

function normalizeRoadSign(raw: WireRoadSign): RoadSignObservation {
  // Protocol 2.18 currently validates 0..8 server-side. Accept 9 as well so the View matches the authoritative Simulation enum when the codec bug is corrected.
  if (!isRecord(raw) || !validPoint(raw) || !enumRange(raw.kind, RoadSignKind.Direction, RoadSignKind.RockSlope) || !validText(raw.text, MAXIMUM_TEXT_LENGTH)) throw new ProtocolDecodeFailure('RegionalGeneration RoadSign values are invalid.');
  return Object.freeze({ ...raw, roadSignId: parsePositiveUInt64(raw.roadSignId, 'RoadSign ID'), corridorId: parsePositiveUInt64(raw.corridorId, 'RoadSign corridor ID'), destinationSettlementId: parseUInt64(raw.destinationSettlementId, 'RoadSign destination settlement ID'), featureId: parseUInt64(raw.featureId, 'RoadSign feature ID') });
}

function normalizeSuitability(raw: SettlementSuitabilityObservation): SettlementSuitabilityObservation {
  const values = [raw.flatness, raw.waterAccess, raw.transportPotential, raw.buildability, raw.resourceAccess, raw.floodRisk, raw.steepSlopeRisk, raw.isolation, raw.constructionCost, raw.totalScore];
  if (!values.every(unit)) throw new ProtocolDecodeFailure('RegionalGeneration Settlement suitability is invalid.');
  return Object.freeze({ ...raw });
}

function normalizeQuality(raw: RegionalQualityObservation): RegionalQualityObservation {
  const values = [raw.terrainAdaptation, raw.roadConnectivity, raw.averageSlopeCost, raw.accessibility, raw.congestionRisk, raw.landUseConsistency, raw.floodExposure, raw.urbanCompactness, raw.polycentricBalance, raw.overallScore];
  if (!values.every(unit)) throw new ProtocolDecodeFailure('RegionalGeneration quality report is invalid.');
  return Object.freeze({ ...raw });
}

function uniquePositiveIds(ids: readonly bigint[], label: string): Set<bigint> {
  const set = new Set(ids);
  if (set.size !== ids.length || set.has(0n)) throw new ProtocolDecodeFailure(`RegionalGeneration ${label} IDs are invalid.`);
  return set;
}

function quoteLosslessUInt64Properties(json: string): string {
  return json.replace(/("(?:tickCount|worldSeed|settlementId|eventId|corridorId|fromSettlementId|toSettlementId|districtId|parcelId|buildingId|poiId|toponymId|roadSignId|nameId|sourceNaturalToponymId|sourceFeatureId|parentHumanToponymId|destinationSettlementId|featureId)"\s*:\s*)(\d+)/g, '$1"$2"');
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

function validPoint(value: unknown): value is { readonly x: number; readonly y: number; readonly z: number } {
  return isRecord(value) && finite(value.x) && finite(value.y) && finite(value.z);
}
function validVolume(value: { readonly minX: unknown; readonly minY: unknown; readonly minZ: unknown; readonly maxX: unknown; readonly maxY: unknown; readonly maxZ: unknown }, requireHorizontalArea: boolean): boolean {
  if (![value.minX, value.minY, value.minZ, value.maxX, value.maxY, value.maxZ].every(finite)) return false;
  const minX = value.minX as number, minY = value.minY as number, minZ = value.minZ as number, maxX = value.maxX as number, maxY = value.maxY as number, maxZ = value.maxZ as number;
  return maxX >= minX && maxY >= minY && maxZ >= minZ && (!requireHorizontalArea || (maxX > minX && maxY > minY));
}
function validateVolume(value: { readonly minX: unknown; readonly minY: unknown; readonly minZ: unknown; readonly maxX: unknown; readonly maxY: unknown; readonly maxZ: unknown }, requireHorizontalArea: boolean): void { if (!validVolume(value, requireHorizontalArea)) throw new ProtocolDecodeFailure('RegionalGeneration volume is invalid.'); }
function validText(value: unknown, maximumLength: number): value is string { return typeof value === 'string' && value.trim().length > 0 && value.length <= maximumLength; }
function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null; }
function finite(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value); }
function positive(value: unknown): value is number { return finite(value) && value > 0; }
function nonNegative(value: unknown): value is number { return finite(value) && value >= 0; }
function unit(value: unknown): value is number { return finite(value) && value >= 0 && value <= 1; }
function enumRange(value: unknown, minimum: number, maximum: number): value is number { return Number.isInteger(value) && typeof value === 'number' && value >= minimum && value <= maximum; }
function integerRange(value: unknown, minimum: number, maximum: number): value is number { return Number.isSafeInteger(value) && typeof value === 'number' && value >= minimum && value <= maximum; }