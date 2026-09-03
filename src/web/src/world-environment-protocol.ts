import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

export const WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE = 800;
const WORLD_ENVIRONMENT_PROTOCOL_MINOR = 17;
const MAXIMUM_SAMPLES = 1_024;
const MAXIMUM_FEATURES = 256;
const MAXIMUM_GEOMETRY_POINTS_PER_FEATURE = 256;
const MAXIMUM_TOPONYMS = 256;
const MAXIMUM_TEXT_LENGTH = 128;
const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

export enum GlobalLandformKind { Ocean = 0, Continent = 1, Island = 2 }
export enum SurfaceWaterKind { None = 0, Ocean = 1, Lake = 2, River = 3, Tributary = 4, Floodplain = 5 }
export enum TerrainMaterialKind { Water = 0, Sand = 1, Soil = 2, Rock = 3, Snow = 4, Gravel = 5 }
export enum GeographicFeatureType { Mountain = 0, MountainRange = 1, River = 2, Tributary = 3, Lake = 4, Valley = 5, Basin = 6, Plain = 7, Plateau = 8, Pass = 9, Cape = 10, Bay = 11, Coast = 12, Island = 13, Peninsula = 14, Cave = 15 }
export enum ToponymProvenanceKind { GeneratedNaturalFeature = 0, InheritedNaturalFeature = 1 }

export interface WorldEnvironmentConfigObservation {
  readonly worldSeed: bigint;
  readonly geographicNorthX: number;
  readonly geographicNorthY: number;
  readonly latitudeDegrees: number;
  readonly hemisphere: number;
  readonly seaLevelMeters: number;
  readonly continentality: number;
  readonly maritimeInfluence: number;
  readonly meanAnnualTemperatureCelsius: number;
  readonly seasonalityCelsius: number;
  readonly annualPrecipitationMillimeters: number;
  readonly configuredCoastlineDistanceMeters: number;
  readonly hasConfiguredCoastlineDistance: boolean;
  readonly globalScaleMeters: number;
  readonly terrainDetailScaleMeters: number;
}

export interface EnvironmentSampleObservation {
  readonly x: number;
  readonly y: number;
  readonly elevationMeters: number;
  readonly landform: GlobalLandformKind;
  readonly coastlineDistanceMeters: number;
  readonly latitudeDegrees: number;
  readonly meanAnnualTemperatureCelsius: number;
  readonly seasonalAmplitudeCelsius: number;
  readonly annualPrecipitationMillimeters: number;
  readonly maritimeInfluence: number;
  readonly continentality: number;
  readonly surfaceWater: SurfaceWaterKind;
  readonly drainage: number;
  readonly riverStrength: number;
  readonly floodRisk: number;
  readonly flowDirectionX: number;
  readonly flowDirectionY: number;
  readonly terrainRuggedness: number;
  readonly buildability: number;
  readonly settlementScore: number;
}

export interface TerrainSurfaceSampleObservation {
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly normalX: number;
  readonly normalY: number;
  readonly normalZ: number;
  readonly slopeDegrees: number;
  readonly roughness: number;
  readonly material: TerrainMaterialKind;
  readonly surfaceWater: SurfaceWaterKind;
}

export interface WorldPointObservation { readonly x: number; readonly y: number; readonly z: number; }

export interface GeographicFeatureObservation {
  readonly featureId: bigint;
  readonly featureType: GeographicFeatureType;
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
  readonly areaSquareMeters: number;
  readonly parentFeatureId: bigint;
  readonly minimumElevationMeters: number;
  readonly maximumElevationMeters: number;
  readonly geometry: readonly WorldPointObservation[];
}

export interface NaturalToponymObservation {
  readonly toponymId: bigint;
  readonly featureId: bigint;
  readonly name: string;
  readonly provenanceKind: ToponymProvenanceKind;
  readonly sourceFeatureId: bigint;
  readonly parentToponymId: bigint;
  readonly generatorKey: string;
}

export interface WorldEnvironmentSnapshotMessage {
  readonly type: typeof WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE;
  readonly tickCount: bigint;
  readonly config: WorldEnvironmentConfigObservation;
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
  readonly samples: readonly EnvironmentSampleObservation[];
  readonly terrainSamples: readonly TerrainSurfaceSampleObservation[];
  readonly features: readonly GeographicFeatureObservation[];
  readonly toponyms: readonly NaturalToponymObservation[];
}

export interface WorldEnvironmentProtocolEnvelope {
  readonly version: ProtocolVersion;
  readonly message: WorldEnvironmentSnapshotMessage;
}

type WireUInt64 = string | number;
interface WireWorldEnvironmentConfig extends Omit<WorldEnvironmentConfigObservation, 'worldSeed'> { readonly worldSeed: WireUInt64; }
interface WireGeographicFeature extends Omit<GeographicFeatureObservation, 'featureId' | 'parentFeatureId'> { readonly featureId: WireUInt64; readonly parentFeatureId: WireUInt64; }
interface WireNaturalToponym extends Omit<NaturalToponymObservation, 'toponymId' | 'featureId' | 'sourceFeatureId' | 'parentToponymId'> { readonly toponymId: WireUInt64; readonly featureId: WireUInt64; readonly sourceFeatureId: WireUInt64; readonly parentToponymId: WireUInt64; }
interface WireWorldEnvironmentSnapshot extends Omit<WorldEnvironmentSnapshotMessage, 'type' | 'tickCount' | 'config' | 'features' | 'toponyms'> {
  readonly tickCount: WireUInt64;
  readonly config: WireWorldEnvironmentConfig;
  readonly features: readonly WireGeographicFeature[];
  readonly toponyms: readonly WireNaturalToponym[];
}

export function isWorldEnvironmentFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeWorldEnvironmentFrame(frame: ArrayBuffer): WorldEnvironmentProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('WorldEnvironment frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC || view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Invalid WorldEnvironment frame header.');
  if (view.getUint16(8, true) !== WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Unknown WorldEnvironment message type.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < WORLD_ENVIRONMENT_PROTOCOL_MINOR) throw new ProtocolDecodeFailure('WorldEnvironment snapshots require Protocol 2.17 or newer.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('WorldEnvironment frame length is invalid.');

  let raw: WireWorldEnvironmentSnapshot;
  try {
    const json = utf8Decoder.decode(new Uint8Array(frame, PROTOCOL_HEADER_SIZE, payloadLength));
    raw = JSON.parse(quoteLosslessUInt64Properties(json)) as WireWorldEnvironmentSnapshot;
  } catch (error) {
    if (error instanceof ProtocolDecodeFailure) throw error;
    throw new ProtocolDecodeFailure('WorldEnvironment snapshot is not valid UTF-8 JSON.');
  }
  return { version, message: normalizeAndValidateSnapshot(raw) };
}

function normalizeAndValidateSnapshot(raw: WireWorldEnvironmentSnapshot): WorldEnvironmentSnapshotMessage {
  if (!isRecord(raw) || !isRecord(raw.config) || !Array.isArray(raw.samples) || !Array.isArray(raw.terrainSamples) || !Array.isArray(raw.features) || !Array.isArray(raw.toponyms)) throw new ProtocolDecodeFailure('WorldEnvironment snapshot shape is invalid.');
  if (raw.samples.length > MAXIMUM_SAMPLES || raw.terrainSamples.length > MAXIMUM_SAMPLES || raw.features.length > MAXIMUM_FEATURES || raw.toponyms.length > MAXIMUM_TOPONYMS || raw.samples.length !== raw.terrainSamples.length) throw new ProtocolDecodeFailure('WorldEnvironment snapshot collection counts are invalid.');

  const config = normalizeConfig(raw.config);
  validateVolume(raw);
  const samples = Object.freeze(raw.samples.map((sample) => normalizeEnvironmentSample(sample)));
  const terrainSamples = Object.freeze(raw.terrainSamples.map((sample) => normalizeTerrainSample(sample)));
  const features = Object.freeze(raw.features.map((feature) => normalizeFeature(feature)));
  const featureIds = new Set(features.map((feature) => feature.featureId));
  if (featureIds.size !== features.length || featureIds.has(0n)) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature IDs are invalid.');
  for (const feature of features) if (feature.parentFeatureId !== 0n && !featureIds.has(feature.parentFeatureId)) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature parent reference is invalid.');
  assertAcyclicParents(features.map((feature) => [feature.featureId, feature.parentFeatureId] as const), 'WorldEnvironment GeographicFeature');

  const toponyms = Object.freeze(raw.toponyms.map((toponym) => normalizeToponym(toponym)));
  const toponymIds = new Set(toponyms.map((toponym) => toponym.toponymId));
  if (toponymIds.size !== toponyms.length || toponymIds.has(0n)) throw new ProtocolDecodeFailure('WorldEnvironment Toponym IDs are invalid.');
  for (const toponym of toponyms) {
    if (!featureIds.has(toponym.featureId) || !featureIds.has(toponym.sourceFeatureId)) throw new ProtocolDecodeFailure('WorldEnvironment Toponym feature reference is invalid.');
    if (toponym.parentToponymId !== 0n && !toponymIds.has(toponym.parentToponymId)) throw new ProtocolDecodeFailure('WorldEnvironment Toponym parent reference is invalid.');
  }
  assertAcyclicParents(toponyms.map((toponym) => [toponym.toponymId, toponym.parentToponymId] as const), 'WorldEnvironment Toponym');

  return Object.freeze({
    type: WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE,
    tickCount: parseUInt64(raw.tickCount, 'WorldEnvironment tick count'),
    config,
    minX: raw.minX,
    minY: raw.minY,
    minZ: raw.minZ,
    maxX: raw.maxX,
    maxY: raw.maxY,
    maxZ: raw.maxZ,
    samples,
    terrainSamples,
    features,
    toponyms,
  });
}

function normalizeConfig(raw: WireWorldEnvironmentConfig): WorldEnvironmentConfigObservation {
  const numericValues = [raw.geographicNorthX, raw.geographicNorthY, raw.latitudeDegrees, raw.seaLevelMeters, raw.continentality, raw.maritimeInfluence, raw.meanAnnualTemperatureCelsius, raw.seasonalityCelsius, raw.annualPrecipitationMillimeters, raw.configuredCoastlineDistanceMeters, raw.globalScaleMeters, raw.terrainDetailScaleMeters];
  if (numericValues.some((value) => !finite(value)) || raw.latitudeDegrees < -90 || raw.latitudeDegrees > 90 || !enumRange(raw.hemisphere, 0, 1) || !unit(raw.continentality) || !unit(raw.maritimeInfluence) || !nonNegative(raw.seasonalityCelsius) || !nonNegative(raw.annualPrecipitationMillimeters) || !positive(raw.globalScaleMeters) || !positive(raw.terrainDetailScaleMeters) || typeof raw.hasConfiguredCoastlineDistance !== 'boolean' || (raw.hasConfiguredCoastlineDistance && !nonNegative(raw.configuredCoastlineDistanceMeters))) throw new ProtocolDecodeFailure('WorldEnvironment config is invalid.');
  return Object.freeze({ ...raw, worldSeed: parsePositiveUInt64(raw.worldSeed, 'WorldEnvironment world seed') });
}

function normalizeEnvironmentSample(raw: EnvironmentSampleObservation): EnvironmentSampleObservation {
  if (!isRecord(raw)) throw new ProtocolDecodeFailure('WorldEnvironment sample is invalid.');
  const numericValues = [raw.x, raw.y, raw.elevationMeters, raw.coastlineDistanceMeters, raw.latitudeDegrees, raw.meanAnnualTemperatureCelsius, raw.seasonalAmplitudeCelsius, raw.annualPrecipitationMillimeters, raw.maritimeInfluence, raw.continentality, raw.drainage, raw.riverStrength, raw.floodRisk, raw.flowDirectionX, raw.flowDirectionY, raw.terrainRuggedness, raw.buildability, raw.settlementScore];
  if (numericValues.some((value) => !finite(value)) || !enumRange(raw.landform, GlobalLandformKind.Ocean, GlobalLandformKind.Island) || !enumRange(raw.surfaceWater, SurfaceWaterKind.None, SurfaceWaterKind.Floodplain) || !nonNegative(raw.coastlineDistanceMeters) || raw.latitudeDegrees < -90 || raw.latitudeDegrees > 90 || !nonNegative(raw.seasonalAmplitudeCelsius) || !nonNegative(raw.annualPrecipitationMillimeters) || !unit(raw.maritimeInfluence) || !unit(raw.continentality) || !unit(raw.drainage) || !unit(raw.riverStrength) || !unit(raw.floodRisk) || !unit(raw.terrainRuggedness) || !unit(raw.buildability) || !unit(raw.settlementScore)) throw new ProtocolDecodeFailure('WorldEnvironment sample contains invalid values.');
  return Object.freeze({ ...raw });
}

function normalizeTerrainSample(raw: TerrainSurfaceSampleObservation): TerrainSurfaceSampleObservation {
  if (!isRecord(raw)) throw new ProtocolDecodeFailure('WorldEnvironment terrain sample is invalid.');
  const numericValues = [raw.x, raw.y, raw.z, raw.normalX, raw.normalY, raw.normalZ, raw.slopeDegrees, raw.roughness];
  if (numericValues.some((value) => !finite(value)) || !nonNegative(raw.slopeDegrees) || !unit(raw.roughness) || !enumRange(raw.material, TerrainMaterialKind.Water, TerrainMaterialKind.Gravel) || !enumRange(raw.surfaceWater, SurfaceWaterKind.None, SurfaceWaterKind.Floodplain)) throw new ProtocolDecodeFailure('WorldEnvironment terrain sample contains invalid values.');
  return Object.freeze({ ...raw });
}

function normalizeFeature(raw: WireGeographicFeature): GeographicFeatureObservation {
  if (!isRecord(raw) || !Array.isArray(raw.geometry) || raw.geometry.length === 0 || raw.geometry.length > MAXIMUM_GEOMETRY_POINTS_PER_FEATURE) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature shape is invalid.');
  validateVolume(raw);
  if (!positive(raw.areaSquareMeters) || !finite(raw.minimumElevationMeters) || !finite(raw.maximumElevationMeters) || raw.maximumElevationMeters < raw.minimumElevationMeters || !enumRange(raw.featureType, GeographicFeatureType.Mountain, GeographicFeatureType.Cave)) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature values are invalid.');
  const geometry = Object.freeze(raw.geometry.map((point) => {
    if (!isRecord(point) || !finite(point.x) || !finite(point.y) || !finite(point.z)) throw new ProtocolDecodeFailure('WorldEnvironment GeographicFeature geometry is invalid.');
    return Object.freeze({ x: point.x, y: point.y, z: point.z });
  }));
  return Object.freeze({
    ...raw,
    featureId: parsePositiveUInt64(raw.featureId, 'GeographicFeature ID'),
    parentFeatureId: parseUInt64(raw.parentFeatureId, 'GeographicFeature parent ID'),
    geometry,
  });
}

function normalizeToponym(raw: WireNaturalToponym): NaturalToponymObservation {
  if (!isRecord(raw) || typeof raw.name !== 'string' || typeof raw.generatorKey !== 'string' || raw.name.trim().length === 0 || raw.name.length > MAXIMUM_TEXT_LENGTH || raw.generatorKey.trim().length === 0 || raw.generatorKey.length > MAXIMUM_TEXT_LENGTH || !enumRange(raw.provenanceKind, ToponymProvenanceKind.GeneratedNaturalFeature, ToponymProvenanceKind.InheritedNaturalFeature)) throw new ProtocolDecodeFailure('WorldEnvironment Toponym values are invalid.');
  return Object.freeze({
    ...raw,
    toponymId: parsePositiveUInt64(raw.toponymId, 'Toponym ID'),
    featureId: parsePositiveUInt64(raw.featureId, 'Toponym feature ID'),
    sourceFeatureId: parsePositiveUInt64(raw.sourceFeatureId, 'Toponym source feature ID'),
    parentToponymId: parseUInt64(raw.parentToponymId, 'Toponym parent ID'),
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

function validateVolume(value: { readonly minX: number; readonly minY: number; readonly minZ: number; readonly maxX: number; readonly maxY: number; readonly maxZ: number }): void {
  if (![value.minX, value.minY, value.minZ, value.maxX, value.maxY, value.maxZ].every(finite) || value.maxX < value.minX || value.maxY < value.minY || value.maxZ < value.minZ) throw new ProtocolDecodeFailure('WorldEnvironment volume is invalid.');
}

function quoteLosslessUInt64Properties(json: string): string {
  return json.replace(/("(?:tickCount|worldSeed|featureId|toponymId|sourceFeatureId|parentFeatureId|parentToponymId)"\s*:\s*)(\d+)/g, '$1"$2"');
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

function isRecord(value: unknown): value is Record<string, unknown> { return typeof value === 'object' && value !== null; }
function finite(value: unknown): value is number { return typeof value === 'number' && Number.isFinite(value); }
function positive(value: unknown): value is number { return finite(value) && value > 0; }
function nonNegative(value: unknown): value is number { return finite(value) && value >= 0; }
function unit(value: unknown): value is number { return finite(value) && value >= 0 && value <= 1; }
function enumRange(value: unknown, minimum: number, maximum: number): value is number { return Number.isInteger(value) && typeof value === 'number' && value >= minimum && value <= maximum; }
