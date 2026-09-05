import test from 'node:test';
import assert from 'node:assert/strict';

import {
  REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
  ZoneKind,
  decodeRegionalGenerationFrame,
  isRegionalGenerationFrame,
} from '../src/regional-generation-protocol.ts';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, ProtocolDecodeFailure } from '../src/protocol.ts';

const SETTLEMENT_ID = 18_446_744_073_709_551_610n;
const TOPONYM_ID = 18_446_744_073_709_551_609n;
const DISTRICT_ID = 18_446_744_073_709_551_608n;
const PARCEL_ID = 18_446_744_073_709_551_607n;
const BUILDING_ID = 18_446_744_073_709_551_606n;
const CORRIDOR_ID = 18_446_744_073_709_551_605n;

test('Protocol 2.18 RegionalGeneration keeps UInt64 stable IDs exact', () => {
  const frame = createFrame(createSnapshotJson());
  assert.equal(isRegionalGenerationFrame(frame), true);
  const envelope = decodeRegionalGenerationFrame(frame);
  assert.deepEqual(envelope.version, { major: 2, minor: 18 });
  assert.equal(envelope.message.type, REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE);
  assert.equal(envelope.message.settlements[1].settlementId, SETTLEMENT_ID);
  assert.equal(envelope.message.parcels[0].districtId, DISTRICT_ID);
  assert.equal(envelope.message.parcels[0].buildingId, BUILDING_ID);
  assert.equal(envelope.message.parcels[0].zone, ZoneKind.MixedUse);
});

test('RegionalGeneration rejects versions older than Protocol 2.18', () => {
  assert.throws(() => decodeRegionalGenerationFrame(createFrame(createSnapshotJson(), { major: 2, minor: 17 })), ProtocolDecodeFailure);
});

test('RegionalGeneration rejects broken stable ID relationships', () => {
  const broken = createSnapshotJson().replace(`"districtId":${DISTRICT_ID.toString()},"minX"`, '"districtId":999,"minX"');
  assert.throws(() => decodeRegionalGenerationFrame(createFrame(broken)), /Parcel hierarchy/);
});

test('RegionalGeneration rejects Int32 overflow from wire JSON', () => {
  const overflow = createSnapshotJson().replace('\"population\":500', '\"population\":2147483648');
  assert.throws(() => decodeRegionalGenerationFrame(createFrame(overflow)), ProtocolDecodeFailure);
});

function createFrame(json, version = { major: 2, minor: 18 }) {
  const payload = new TextEncoder().encode(json);
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payload.byteLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payload.byteLength, true);
  new Uint8Array(frame, PROTOCOL_HEADER_SIZE).set(payload);
  return frame;
}

function createSnapshotJson() {
  return `{
    "tickCount":42,"worldSeed":30034,"preset":1,"iterations":2,
    "minX":0,"minY":0,"minZ":0,"maxX":1000,"maxY":1000,"maxZ":100,
    "settlements":[
      {"settlementId":7,"x":800,"y":800,"z":5,"environment":1,"origin":1,"role":0,"initialEconomy":1,
       "suitability":{"flatness":0.8,"waterAccess":0.6,"transportPotential":0.7,"buildability":0.9,"resourceAccess":0.5,"floodRisk":0.1,"steepSlopeRisk":0.1,"isolation":0.2,"constructionCost":0.2,"totalScore":0.8},
       "population":500,"jobs":120,"influenceRadiusMeters":2500,"nameId":9},
      {"settlementId":${SETTLEMENT_ID},"x":100,"y":100,"z":4,"environment":0,"origin":0,"role":2,"initialEconomy":2,
       "suitability":{"flatness":0.8,"waterAccess":0.6,"transportPotential":0.7,"buildability":0.9,"resourceAccess":0.5,"floodRisk":0.1,"steepSlopeRisk":0.1,"isolation":0.2,"constructionCost":0.2,"totalScore":0.8},
       "population":2000,"jobs":900,"influenceRadiusMeters":5000,"nameId":${TOPONYM_ID}}
    ],
    "growthEvents":[],
    "corridors":[{"corridorId":${CORRIDOR_ID},"kind":0,"fromSettlementId":${SETTLEMENT_ID},"toSettlementId":7,"geometry":[{"x":100,"y":100,"z":4},{"x":800,"y":800,"z":5}],"terrainAdaptation":0.8,"constructionCost":0.2,"nameId":0}],
    "districts":[{"districtId":${DISTRICT_ID},"settlementId":${SETTLEMENT_ID},"kind":0,"minX":50,"minY":50,"minZ":0,"maxX":250,"maxY":250,"maxZ":20,"nameId":8,"accessibility":0.9}],
    "parcels":[{"parcelId":${PARCEL_ID},"settlementId":${SETTLEMENT_ID},"districtId":${DISTRICT_ID},"minX":70,"minY":70,"minZ":0,"maxX":120,"maxY":120,"maxZ":1,"zone":3,"developmentState":2,"developmentSuitability":0.9,"landValue":0.8,"buildingId":${BUILDING_ID}}],
    "buildings":[{"buildingId":${BUILDING_ID},"parcelId":${PARCEL_ID},"use":3,"minX":75,"minY":75,"minZ":0,"maxX":115,"maxY":115,"maxZ":24,"floors":6,"capacity":80,"historicalStage":2}],
    "pois":[],
    "toponyms":[
      {"toponymId":9,"kind":0,"name":"Second Settlement","sourceNaturalToponymId":0,"sourceNaturalName":"","sourceFeatureId":0,"parentHumanToponymId":0,"generatorKey":"phase30-regional-v1"},
      {"toponymId":${TOPONYM_ID},"kind":0,"name":"Central Settlement","sourceNaturalToponymId":0,"sourceNaturalName":"","sourceFeatureId":0,"parentHumanToponymId":0,"generatorKey":"phase30-regional-v1"},
      {"toponymId":8,"kind":1,"name":"Old Town","sourceNaturalToponymId":0,"sourceNaturalName":"","sourceFeatureId":0,"parentHumanToponymId":${TOPONYM_ID},"generatorKey":"phase30-regional-v1"}
    ],
    "roadSigns":[],
    "quality":{"terrainAdaptation":0.8,"roadConnectivity":0.9,"averageSlopeCost":0.2,"accessibility":0.8,"congestionRisk":0.2,"landUseConsistency":0.9,"floodExposure":0.1,"urbanCompactness":0.7,"polycentricBalance":0.8,"overallScore":0.85}
  }`;
}
