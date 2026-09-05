import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import {
  BuildingLifecycleStatus,
  PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
  SettlementScale,
  SettlementTrend,
} from '../src/persistent-regional-evolution-protocol.ts';
import { PersistentRegionalEvolutionStore } from '../src/persistent-regional-evolution-store.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../src/regional-generation-protocol.ts';
import { RegionalGenerationStore } from '../src/regional-generation-store.ts';
import { SettlementStructureRenderer } from '../src/settlement-structure-renderer.ts';

test('SettlementStructureRenderer applies authoritative Phase31 scale, development and lifecycle state without rebuilding static geometry', () => {
  const generation = new RegionalGenerationStore();
  generation.replace(createGenerationSnapshot());
  const evolution = new PersistentRegionalEvolutionStore();
  evolution.replace(createEvolutionSnapshot(BuildingLifecycleStatus.Demolished, SettlementScale.City, SettlementTrend.Growing));
  const scene = new THREE.Scene();
  const renderer = new SettlementStructureRenderer(scene);

  renderer.update(generation, evolution);

  const root = scene.getObjectByName('regional-generation');
  const districtLines = scene.getObjectByName('regional-districts');
  const settlementMesh = scene.getObjectByName('regional-settlements');
  const parcelMesh = scene.getObjectByName('regional-parcels');
  const buildingMesh = scene.getObjectByName('regional-buildings');
  const relationLines = scene.getObjectByName('regional-evolution-relations-3');
  assert.equal(root?.userData.currentYear, 25);
  assert.ok(districtLines instanceof THREE.LineSegments);
  assert.ok(settlementMesh instanceof THREE.InstancedMesh);
  assert.ok(parcelMesh instanceof THREE.InstancedMesh);
  assert.ok(buildingMesh instanceof THREE.InstancedMesh);
  assert.ok(relationLines instanceof THREE.LineSegments);
  assert.equal(settlementMesh.userData.evolution[0].scale, SettlementScale.City);
  assert.equal(settlementMesh.userData.evolution[1].scale, SettlementScale.Hamlet);
  assert.equal(parcelMesh.userData.evolution[0].developmentState, 3);
  assert.equal(buildingMesh.userData.evolution[0].status, BuildingLifecycleStatus.Demolished);
  assert.equal(relationLines.userData.relations[0].fromSettlementId, 101n);
  assert.equal(relationLines.userData.relations[0].toSettlementId, 102n);
  assert.ok(instanceScale(buildingMesh, 0).y < 0.2);

  evolution.replace(createEvolutionSnapshot(BuildingLifecycleStatus.Active, SettlementScale.Town, SettlementTrend.Declining));
  renderer.update(generation, evolution);
  const updatedSettlementMesh = scene.getObjectByName('regional-settlements');
  const updatedBuildingMesh = scene.getObjectByName('regional-buildings');
  assert.ok(updatedSettlementMesh instanceof THREE.InstancedMesh);
  assert.ok(updatedBuildingMesh instanceof THREE.InstancedMesh);
  assert.equal(updatedSettlementMesh.userData.evolution[0].scale, SettlementScale.Town);
  assert.equal(updatedSettlementMesh.userData.evolution[0].trend, SettlementTrend.Declining);
  assert.equal(updatedBuildingMesh.userData.evolution[0].status, BuildingLifecycleStatus.Active);
  assert.ok(instanceScale(updatedBuildingMesh, 0).y > 20);
  assert.strictEqual(scene.getObjectByName('regional-districts'), districtLines);
  assert.notStrictEqual(updatedBuildingMesh, buildingMesh);
  renderer.dispose();
});

test('SettlementStructureRenderer renders evolution-only buildings and settlements with aligned relation metadata', () => {
  const generation = new RegionalGenerationStore();
  generation.replace(createGenerationSnapshot());
  const evolution = new PersistentRegionalEvolutionStore();
  evolution.replace(createEvolutionWithEmergentEntities());
  const scene = new THREE.Scene();
  const renderer = new SettlementStructureRenderer(scene);

  renderer.update(generation, evolution);

  assert.equal(renderer.metrics.settlements, 3);
  assert.equal(renderer.metrics.buildings, 2);
  const settlementMesh = scene.getObjectByName('regional-settlements');
  const buildingMesh = scene.getObjectByName('regional-buildings');
  assert.ok(settlementMesh instanceof THREE.InstancedMesh);
  assert.ok(buildingMesh instanceof THREE.InstancedMesh);
  assert.equal(settlementMesh.count, 3);
  assert.equal(buildingMesh.count, 2);
  assert.deepEqual(buildingMesh.userData.relations[1], { buildingId: 499n, parcelId: 302n });
  assert.deepEqual(renderer.relations.parcels[1], { parcelId: 302n, settlementId: 102n, districtId: 202n, buildingId: 499n });
  assert.deepEqual(renderer.relations.buildings[1], { buildingId: 499n, parcelId: 302n });

  const emergentPosition = instancePosition(settlementMesh, 2);
  assert.equal(emergentPosition.x, 3_000);
  assert.equal(emergentPosition.z, 2_500);
  const newBuildingPosition = instancePosition(buildingMesh, 1);
  assert.equal(newBuildingPosition.x, 2_160);
  assert.equal(newBuildingPosition.z, 1_360);

  const metroRelations = scene.getObjectByName('regional-evolution-relations-3');
  const tradeRelations = scene.getObjectByName('regional-evolution-relations-1');
  assert.ok(metroRelations instanceof THREE.LineSegments);
  assert.ok(tradeRelations instanceof THREE.LineSegments);
  assert.equal(metroRelations.userData.relations.length, 1);
  assert.equal(tradeRelations.userData.relations.length, 1);
  assert.equal(metroRelations.userData.relations[0].kind, 3);
  assert.equal(tradeRelations.userData.relations[0].kind, 1);
  assert.equal(tradeRelations.userData.relations[0].fromSettlementId, 103n);

  renderer.dispose();
});

test('SettlementStructureRenderer clears baseline Parcel building relation after demolition', () => {
  const generation = new RegionalGenerationStore();
  generation.replace(createGenerationSnapshot());
  const evolution = new PersistentRegionalEvolutionStore();
  const demolished = createEvolutionSnapshot(BuildingLifecycleStatus.Demolished, SettlementScale.City, SettlementTrend.Declining);
  evolution.replace({
    ...demolished,
    parcels: [
      { ...demolished.parcels[0], developmentState: 0, buildingId: 0n },
    ],
  });
  const scene = new THREE.Scene();
  const renderer = new SettlementStructureRenderer(scene);

  renderer.update(generation, evolution);

  const parcelRelation = renderer.relations.parcels.find((relation) => relation.parcelId === 301n);
  assert.ok(parcelRelation);
  assert.equal(parcelRelation.buildingId, 0n);
  const parcelMesh = scene.getObjectByName('regional-parcels');
  assert.ok(parcelMesh instanceof THREE.InstancedMesh);
  assert.equal(parcelMesh.userData.relations[0].buildingId, 0n);

  renderer.dispose();
});

function instanceScale(mesh, index) {
  const matrix = new THREE.Matrix4();
  mesh.getMatrixAt(index, matrix);
  const position = new THREE.Vector3();
  const quaternion = new THREE.Quaternion();
  const scale = new THREE.Vector3();
  matrix.decompose(position, quaternion, scale);
  return scale;
}

function instancePosition(mesh, index) {
  const matrix = new THREE.Matrix4();
  mesh.getMatrixAt(index, matrix);
  return new THREE.Vector3().setFromMatrixPosition(matrix);
}

function createGenerationSnapshot() {
  return {
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 10n, worldSeed: 31_000n, preset: 1, iterations: 0,
    minX: 0, minY: 0, minZ: 0, maxX: 4_000, maxY: 3_000, maxZ: 100,
    settlements: [settlement(101n, 10, 20, 3, 3, 4_000), settlement(102n, 2_000, 1_200, 8, 0, 600)],
    growthEvents: [], corridors: [],
    districts: [
      { districtId: 201n, settlementId: 101n, kind: 1, minX: 0, minY: 0, minZ: 0, maxX: 200, maxY: 200, maxZ: 2, nameId: 0n, accessibility: 1 },
      { districtId: 202n, settlementId: 102n, kind: 1, minX: 2_000, minY: 1_200, minZ: 0, maxX: 2_300, maxY: 1_500, maxZ: 2, nameId: 0n, accessibility: 0.7 },
    ],
    parcels: [
      { parcelId: 301n, settlementId: 101n, districtId: 201n, minX: 20, minY: 20, minZ: 0, maxX: 80, maxY: 80, maxZ: 1, zone: 1, developmentState: 2, developmentSuitability: 1, landValue: 1, buildingId: 401n },
      { parcelId: 302n, settlementId: 102n, districtId: 202n, minX: 2_120, minY: 1_320, minZ: 0, maxX: 2_200, maxY: 1_400, maxZ: 1, zone: 0, developmentState: 0, developmentSuitability: 0.8, landValue: 0.4, buildingId: 0n },
    ],
    buildings: [{ buildingId: 401n, parcelId: 301n, use: 1, minX: 30, minY: 30, minZ: 0, maxX: 70, maxY: 70, maxZ: 48, floors: 12, capacity: 200, historicalStage: 2 }],
    pois: [], toponyms: [], roadSigns: [],
    quality: { terrainAdaptation: 1, roadConnectivity: 1, averageSlopeCost: 0.2, accessibility: 0.8, congestionRisk: 0.2, landUseConsistency: 1, floodExposure: 0, urbanCompactness: 0.7, polycentricBalance: 1, overallScore: 0.9 },
  };
}

function createEvolutionSnapshot(status, scale, trend) {
  return {
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25, tickCount: 20n, isFullSnapshot: true,
    settlements: [
      { settlementId: 101n, x: 10, y: 20, z: 3, population: 25_000, jobs: 14_000, serviceIndex: 0.8, density: 0.7, accessibility: 0.9, influenceRadiusMeters: 4_600, scale, trend, isActive: true, establishedYear: 0, dormantSinceYear: null },
      { settlementId: 102n, x: 2_000, y: 1_200, z: 8, population: 180, jobs: 40, serviceIndex: 0.2, density: 0.1, accessibility: 0.25, influenceRadiusMeters: 500, scale: SettlementScale.Hamlet, trend: SettlementTrend.Dormant, isActive: false, establishedYear: 0, dormantSinceYear: 24 },
    ],
    parcels: [{ parcelId: 301n, settlementId: 101n, developmentDemand: 0.9, landValue: 0.8, developmentState: 3, buildingId: 401n }],
    buildings: [{ buildingId: 401n, parcelId: 301n, use: 1, builtYear: 0, lastChangedYear: 25, condition: status === BuildingLifecycleStatus.Active ? 0.9 : 0.08, occupancy: status === BuildingLifecycleStatus.Active ? 0.8 : 0, capacity: 200, status }],
    serviceCatchments: [], infrastructureDemands: [],
    relations: [{ relationId: 501n, fromSettlementId: 101n, toSettlementId: 102n, kind: 3, strength: 0.7, isActive: true, sinceYear: 20 }],
    events: [], commutingFlows: [], freightFlows: [],
  };
}

function createEvolutionWithEmergentEntities() {
  const snapshot = createEvolutionSnapshot(BuildingLifecycleStatus.Active, SettlementScale.City, SettlementTrend.Growing);
  return {
    ...snapshot,
    settlements: [
      ...snapshot.settlements,
      { settlementId: 103n, x: 3_000, y: 2_500, z: 6, population: 900, jobs: 300, serviceIndex: 0.4, density: 0.2, accessibility: 0.5, influenceRadiusMeters: 900, scale: SettlementScale.Village, trend: SettlementTrend.Growing, isActive: true, establishedYear: 25, dormantSinceYear: null },
    ],
    parcels: [
      ...snapshot.parcels,
      { parcelId: 302n, settlementId: 102n, developmentDemand: 1, landValue: 0.5, developmentState: 2, buildingId: 499n },
    ],
    buildings: [
      ...snapshot.buildings,
      { buildingId: 499n, parcelId: 302n, use: 0, builtYear: 25, lastChangedYear: 25, condition: 1, occupancy: 0.7, capacity: 24, status: BuildingLifecycleStatus.Active },
    ],
    relations: [
      ...snapshot.relations,
      { relationId: 502n, fromSettlementId: 103n, toSettlementId: 101n, kind: 1, strength: 0.4, isActive: true, sinceYear: 25 },
    ],
  };
}

function settlement(settlementId, x, y, z, role, influenceRadiusMeters) {
  return {
    settlementId, x, y, z, environment: 0, origin: 0, role, initialEconomy: 0,
    suitability: { flatness: 1, waterAccess: 1, transportPotential: 1, buildability: 1, resourceAccess: 1, floodRisk: 0, steepSlopeRisk: 0, isolation: 0, constructionCost: 0, totalScore: 1 },
    population: role === 3 ? 8_000 : 200, jobs: role === 3 ? 4_000 : 40, influenceRadiusMeters, nameId: 0n,
  };
}
