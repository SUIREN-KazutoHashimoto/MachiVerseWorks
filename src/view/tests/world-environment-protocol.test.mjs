import test from 'node:test';
import assert from 'node:assert/strict';

import {
  GeographicFeatureType,
  SurfaceWaterKind,
  TerrainMaterialKind,
  WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE,
  decodeWorldEnvironmentFrame,
  isWorldEnvironmentFrame,
} from '../src/world-environment-protocol.ts';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, ProtocolDecodeFailure } from '../src/protocol.ts';

const LARGE_FEATURE_ID = 18_446_744_073_709_551_614n;
const LARGE_TOPONYM_ID = 18_446_744_073_709_551_613n;

test('Protocol 2.17 WorldEnvironment JSON keeps UInt64 IDs exact', () => {
  const frame = createWorldEnvironmentFrame(createSnapshotJson());
  assert.equal(isWorldEnvironmentFrame(frame), true);

  const envelope = decodeWorldEnvironmentFrame(frame);
  assert.deepEqual(envelope.version, { major: 2, minor: 17 });
  assert.equal(envelope.message.type, WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE);
  assert.equal(envelope.message.tickCount, 9_007_199_254_740_999n);
  assert.equal(envelope.message.features[0].featureId, LARGE_FEATURE_ID);
  assert.equal(envelope.message.toponyms[0].toponymId, LARGE_TOPONYM_ID);
  assert.equal(envelope.message.toponyms[0].featureId, LARGE_FEATURE_ID);
  assert.equal(envelope.message.terrainSamples[0].material, TerrainMaterialKind.Soil);
  assert.equal(envelope.message.terrainSamples[0].surfaceWater, SurfaceWaterKind.River);
  assert.equal(envelope.message.features[0].featureType, GeographicFeatureType.River);
});

test('WorldEnvironment frames reject Protocol versions older than 2.17', () => {
  const frame = createWorldEnvironmentFrame(createSnapshotJson(), { major: 2, minor: 16 });
  assert.throws(() => decodeWorldEnvironmentFrame(frame), ProtocolDecodeFailure);
});

test('WorldEnvironment frames reject broken authoritative references', () => {
  const json = createSnapshotJson().replace(`"featureId":${LARGE_FEATURE_ID.toString()}`, '"featureId":999');
  const frame = createWorldEnvironmentFrame(json);
  assert.throws(() => decodeWorldEnvironmentFrame(frame), /Toponym feature reference/);
});

function createWorldEnvironmentFrame(json, version = { major: 2, minor: 17 }) {
  const payload = new TextEncoder().encode(json);
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payload.byteLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payload.byteLength, true);
  new Uint8Array(frame, PROTOCOL_HEADER_SIZE).set(payload);
  return frame;
}

function createSnapshotJson() {
  return `{
    "tickCount":9007199254740999,
    "config":{
      "worldSeed":29027,"geographicNorthX":0,"geographicNorthY":1,"latitudeDegrees":45,"hemisphere":0,
      "seaLevelMeters":0,"continentality":0.55,"maritimeInfluence":0.45,"meanAnnualTemperatureCelsius":11,
      "seasonalityCelsius":20,"annualPrecipitationMillimeters":900,"configuredCoastlineDistanceMeters":0,
      "hasConfiguredCoastlineDistance":false,"globalScaleMeters":250000,"terrainDetailScaleMeters":512
    },
    "minX":0,"minY":0,"minZ":-100,"maxX":100,"maxY":100,"maxZ":100,
    "samples":[{
      "x":0,"y":0,"elevationMeters":12,"landform":1,"coastlineDistanceMeters":1000,"latitudeDegrees":45,
      "meanAnnualTemperatureCelsius":11,"seasonalAmplitudeCelsius":20,"annualPrecipitationMillimeters":900,
      "maritimeInfluence":0.45,"continentality":0.55,"surfaceWater":3,"drainage":0.5,"riverStrength":0.7,
      "floodRisk":0.2,"flowDirectionX":1,"flowDirectionY":0,"terrainRuggedness":0.3,"buildability":0.6,"settlementScore":0.5
    }],
    "terrainSamples":[{
      "x":0,"y":0,"z":12,"normalX":0,"normalY":0,"normalZ":1,"slopeDegrees":0,"roughness":0.2,"material":2,"surfaceWater":3
    }],
    "features":[{
      "featureId":${LARGE_FEATURE_ID.toString()},"featureType":2,"minX":0,"minY":0,"minZ":10,"maxX":100,"maxY":100,"maxZ":20,
      "areaSquareMeters":10000,"parentFeatureId":0,"minimumElevationMeters":10,"maximumElevationMeters":20,
      "geometry":[{"x":0,"y":0,"z":12},{"x":100,"y":100,"z":14}]
    }],
    "toponyms":[{
      "toponymId":${LARGE_TOPONYM_ID.toString()},"featureId":${LARGE_FEATURE_ID.toString()},"name":"Test River","provenanceKind":0,
      "sourceFeatureId":${LARGE_FEATURE_ID.toString()},"parentToponymId":0,"generatorKey":"phase29-natural-v1"
    }]
  }`;
}
