import test from 'node:test';
import assert from 'node:assert/strict';

import { RegionalGenerationStore } from '../src/regional-generation-store.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../src/regional-generation-protocol.ts';

const settlementId = 11n;
const districtId = 21n;
const parcelId = 31n;
const buildingId = 41n;
const poiId = 61n;

test('RegionalGenerationStore preserves stable-ID relationships without reclassification', () => {
  const store = new RegionalGenerationStore();
  store.replace(createSnapshot());

  assert.equal(store.revision, 1);
  assert.equal(store.getSettlement(settlementId)?.environment, 7);
  assert.equal(store.getSettlementForDistrict(districtId)?.settlementId, settlementId);
  assert.equal(store.getSettlementForParcel(parcelId)?.settlementId, settlementId);
  assert.equal(store.getDistrictForParcel(parcelId)?.districtId, districtId);
  assert.equal(store.getParcelForBuilding(buildingId)?.parcelId, parcelId);
  assert.equal(store.getDistrictForBuilding(buildingId)?.districtId, districtId);
  assert.equal(store.getSettlementForBuilding(buildingId)?.settlementId, settlementId);
  assert.equal(store.getBuildingForPoi(poiId)?.buildingId, buildingId);
  assert.equal(store.getSettlementForPoi(poiId)?.settlementId, settlementId);

  store.clear();
  assert.equal(store.revision, 2);
  assert.equal(store.snapshot, null);
  assert.equal(store.getSettlement(settlementId), undefined);
  assert.equal(store.getBuildingForPoi(poiId), undefined);
});

test('RegionalGenerationStore keeps a POI without a building linked only to its delivered settlement', () => {
  const store = new RegionalGenerationStore();
  const snapshot = createSnapshot();
  store.replace(Object.freeze({
    ...snapshot,
    pois: Object.freeze([{ ...snapshot.pois[0], poiId: 62n, buildingId: 0n }]),
  }));

  assert.equal(store.getBuildingForPoi(62n), undefined);
  assert.equal(store.getSettlementForPoi(62n)?.settlementId, settlementId);
});

function createSnapshot() {
  return Object.freeze({
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 1n, worldSeed: 30_034n, preset: 1, iterations: 0,
    minX: 0, minY: 0, minZ: 0, maxX: 100, maxY: 100, maxZ: 50,
    settlements: Object.freeze([{ settlementId, x: 10, y: 10, z: 1, environment: 7, origin: 0, role: 0, initialEconomy: 0, suitability: Object.freeze({ flatness: 1, waterAccess: 1, transportPotential: 1, buildability: 1, resourceAccess: 1, floodRisk: 0, steepSlopeRisk: 0, isolation: 0, constructionCost: 0, totalScore: 1 }), population: 10, jobs: 3, influenceRadiusMeters: 1000, nameId: 51n }]),
    growthEvents: Object.freeze([]), corridors: Object.freeze([]),
    districts: Object.freeze([{ districtId, settlementId, kind: 0, minX: 0, minY: 0, minZ: 0, maxX: 50, maxY: 50, maxZ: 1, nameId: 52n, accessibility: 1 }]),
    parcels: Object.freeze([{ parcelId, settlementId, districtId, minX: 1, minY: 1, minZ: 0, maxX: 10, maxY: 10, maxZ: 1, zone: 0, developmentState: 2, developmentSuitability: 1, landValue: 1, buildingId }]),
    buildings: Object.freeze([{ buildingId, parcelId, use: 0, minX: 2, minY: 2, minZ: 0, maxX: 9, maxY: 9, maxZ: 8, floors: 2, capacity: 5, historicalStage: 0 }]),
    pois: Object.freeze([{ poiId, settlementId, kind: 0, x: 4, y: 4, z: 8, buildingId, nameId: 53n }]),
    toponyms: Object.freeze([
      { toponymId: 51n, kind: 0, name: 'Settlement', sourceNaturalToponymId: 0n, sourceNaturalName: '', sourceFeatureId: 0n, parentHumanToponymId: 0n, generatorKey: 'test' },
      { toponymId: 52n, kind: 1, name: 'District', sourceNaturalToponymId: 0n, sourceNaturalName: '', sourceFeatureId: 0n, parentHumanToponymId: 51n, generatorKey: 'test' },
      { toponymId: 53n, kind: 5, name: 'POI', sourceNaturalToponymId: 0n, sourceNaturalName: '', sourceFeatureId: 0n, parentHumanToponymId: 52n, generatorKey: 'test' },
    ]),
    roadSigns: Object.freeze([]),
    quality: Object.freeze({ terrainAdaptation: 1, roadConnectivity: 1, averageSlopeCost: 0, accessibility: 1, congestionRisk: 0, landUseConsistency: 1, floodExposure: 0, urbanCompactness: 1, polycentricBalance: 1, overallScore: 1 }),
  });
}
