import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import { RegionalGenerationStore } from '../src/regional-generation-store.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../src/regional-generation-protocol.ts';
import { SettlementStructureRenderer } from '../src/settlement-structure-renderer.ts';

test('SettlementStructureRenderer keeps heterogeneous settlements separate in one read model', () => {
  const store = new RegionalGenerationStore();
  store.replace(createSnapshot());
  const scene = new THREE.Scene();
  const renderer = new SettlementStructureRenderer(scene);

  renderer.update(store);

  assert.deepEqual(renderer.metrics, {
    settlements: 2,
    corridors: 1,
    districts: 2,
    parcels: 2,
    buildings: 2,
    pois: 2,
    labels: 0,
    roadSigns: 1,
  });

  const settlementMesh = scene.getObjectByName('regional-settlements');
  assert.ok(settlementMesh instanceof THREE.InstancedMesh);
  assert.equal(settlementMesh.count, 2);

  const firstMatrix = new THREE.Matrix4();
  const secondMatrix = new THREE.Matrix4();
  settlementMesh.getMatrixAt(0, firstMatrix);
  settlementMesh.getMatrixAt(1, secondMatrix);
  const firstPosition = new THREE.Vector3().setFromMatrixPosition(firstMatrix);
  const secondPosition = new THREE.Vector3().setFromMatrixPosition(secondMatrix);
  assert.equal(firstPosition.x, 10);
  assert.equal(secondPosition.x, 2_000);
  assert.notEqual(firstPosition.x, secondPosition.x);

  const firstColor = new THREE.Color();
  const secondColor = new THREE.Color();
  settlementMesh.getColorAt(0, firstColor);
  settlementMesh.getColorAt(1, secondColor);
  assert.notEqual(firstColor.getHex(), secondColor.getHex());

  renderer.dispose();
  assert.equal(scene.getObjectByName('regional-generation'), undefined);
});

test('SettlementStructureRenderer exposes exact stable-ID relations on presentation primitives', () => {
  const store = new RegionalGenerationStore();
  store.replace(createSnapshot());
  const scene = new THREE.Scene();
  const renderer = new SettlementStructureRenderer(scene);

  renderer.update(store);

  assert.deepEqual(renderer.relations.parcels[0], {
    parcelId: 301n,
    settlementId: 101n,
    districtId: 201n,
    buildingId: 401n,
  });
  assert.deepEqual(renderer.relations.buildings[1], { buildingId: 402n, parcelId: 302n });
  assert.deepEqual(renderer.relations.pois[1], {
    poiId: 502n,
    settlementId: 102n,
    buildingId: 402n,
    nameId: 0n,
  });

  const parcelMesh = scene.getObjectByName('regional-parcels');
  const buildingMesh = scene.getObjectByName('regional-buildings');
  const poiPoints = scene.getObjectByName('regional-pois');
  assert.deepEqual(parcelMesh?.userData.relations, renderer.relations.parcels);
  assert.deepEqual(buildingMesh?.userData.relations, renderer.relations.buildings);
  assert.deepEqual(poiPoints?.userData.relations, renderer.relations.pois);

  store.clear();
  renderer.update(store);
  assert.deepEqual(renderer.relations, {
    settlements: [], districts: [], parcels: [], buildings: [], pois: [],
  });

  renderer.dispose();
});

function createSnapshot() {
  return Object.freeze({
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 10n,
    worldSeed: 30_034n,
    preset: 1,
    iterations: 0,
    minX: 0, minY: 0, minZ: 0, maxX: 3_000, maxY: 2_000, maxZ: 100,
    settlements: Object.freeze([
      settlement(101n, 10, 20, 3, 3, 0, 3_600, 0n),
      settlement(102n, 2_000, 1_200, 8, 1, 1, 700, 0n),
    ]),
    growthEvents: Object.freeze([]),
    corridors: Object.freeze([{
      corridorId: 601n, kind: 1, fromSettlementId: 101n, toSettlementId: 102n,
      geometry: Object.freeze([{ x: 10, y: 20, z: 3 }, { x: 2_000, y: 1_200, z: 8 }]),
      terrainAdaptation: 1, constructionCost: 10, nameId: 0n,
    }]),
    districts: Object.freeze([
      { districtId: 201n, settlementId: 101n, kind: 1, minX: 0, minY: 0, minZ: 0, maxX: 200, maxY: 200, maxZ: 2, nameId: 0n, accessibility: 1 },
      { districtId: 202n, settlementId: 102n, kind: 5, minX: 1_900, minY: 1_100, minZ: 5, maxX: 2_100, maxY: 1_300, maxZ: 7, nameId: 0n, accessibility: 0.4 },
    ]),
    parcels: Object.freeze([
      { parcelId: 301n, settlementId: 101n, districtId: 201n, minX: 20, minY: 20, minZ: 0, maxX: 80, maxY: 80, maxZ: 1, zone: 1, developmentState: 2, developmentSuitability: 1, landValue: 1, buildingId: 401n },
      { parcelId: 302n, settlementId: 102n, districtId: 202n, minX: 1_950, minY: 1_150, minZ: 5, maxX: 2_020, maxY: 1_220, maxZ: 6, zone: 5, developmentState: 2, developmentSuitability: 0.6, landValue: 0.3, buildingId: 402n },
    ]),
    buildings: Object.freeze([
      { buildingId: 401n, parcelId: 301n, use: 1, minX: 30, minY: 30, minZ: 0, maxX: 70, maxY: 70, maxZ: 48, floors: 12, capacity: 200, historicalStage: 2 },
      { buildingId: 402n, parcelId: 302n, use: 0, minX: 1_970, minY: 1_170, minZ: 5, maxX: 2_000, maxY: 1_200, maxZ: 12, floors: 2, capacity: 8, historicalStage: 1 },
    ]),
    pois: Object.freeze([
      { poiId: 501n, settlementId: 101n, kind: 1, x: 50, y: 50, z: 48, buildingId: 401n, nameId: 0n },
      { poiId: 502n, settlementId: 102n, kind: 0, x: 1_985, y: 1_185, z: 12, buildingId: 402n, nameId: 0n },
    ]),
    toponyms: Object.freeze([]),
    roadSigns: Object.freeze([{
      roadSignId: 701n, kind: 0, x: 1_000, y: 600, z: 5, corridorId: 601n,
      destinationSettlementId: 102n, featureId: 0n, text: 'Settlement B',
    }]),
    quality: Object.freeze({
      terrainAdaptation: 1, roadConnectivity: 1, averageSlopeCost: 0.2, accessibility: 0.8,
      congestionRisk: 0.2, landUseConsistency: 1, floodExposure: 0, urbanCompactness: 0.7,
      polycentricBalance: 1, overallScore: 0.9,
    }),
  });
}

function settlement(settlementId, x, y, z, role, environment, influenceRadiusMeters, nameId) {
  return Object.freeze({
    settlementId, x, y, z, environment, origin: 0, role, initialEconomy: 0,
    suitability: Object.freeze({
      flatness: 1, waterAccess: 1, transportPotential: 1, buildability: 1, resourceAccess: 1,
      floodRisk: 0, steepSlopeRisk: 0, isolation: 0, constructionCost: 0, totalScore: 1,
    }),
    population: role === 3 ? 8_000 : 400,
    jobs: role === 3 ? 4_000 : 120,
    influenceRadiusMeters,
    nameId,
  });
}
