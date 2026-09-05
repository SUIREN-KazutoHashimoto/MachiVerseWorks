import * as THREE from 'three';

import {
  BuildingLifecycleStatus,
  PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
  SettlementScale,
  SettlementTrend,
} from '../../src/persistent-regional-evolution-protocol.ts';
import { PersistentRegionalEvolutionStore } from '../../src/persistent-regional-evolution-store.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../../src/regional-generation-protocol.ts';
import { RegionalGenerationStore } from '../../src/regional-generation-store.ts';
import { SettlementStructureRenderer } from '../../src/settlement-structure-renderer.ts';

const result = document.querySelector('#result');
const viewport = document.querySelector('#viewport');
if (!(result instanceof HTMLElement) || !(viewport instanceof HTMLElement)) throw new Error('Evolution browser harness is invalid.');

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(55, 1024 / 768, 0.1, 10_000);
camera.position.set(1_000, 1_500, 2_400);
camera.lookAt(1_000, 0, 600);
const webgl = new THREE.WebGLRenderer({ antialias: false });
webgl.setSize(1024, 768, false);
viewport.appendChild(webgl.domElement);

const generation = new RegionalGenerationStore();
const evolution = new PersistentRegionalEvolutionStore();
const renderer = new SettlementStructureRenderer(scene);

try {
  generation.replace(createGenerationSnapshot());
  evolution.apply(createFullChunk());
  evolution.apply(createContinuationChunk());
  renderer.update(generation, evolution);
  webgl.render(scene, camera);

  const root = requireObject('regional-generation', THREE.Group);
  const settlements = requireObject('regional-settlements', THREE.InstancedMesh);
  const parcels = requireObject('regional-parcels', THREE.InstancedMesh);
  const buildings = requireObject('regional-buildings', THREE.InstancedMesh);
  const relations = requireObject('regional-evolution-relations-3', THREE.LineSegments);

  assert(root.userData.currentYear === 25, 'Evolution currentYear was not applied.');
  assert(evolution.snapshot?.settlements.length === 2, 'Continuation chunk was not assembled.');
  assert(settlements.userData.evolution[0].scale === SettlementScale.City, 'Authoritative City scale was not applied.');
  assert(settlements.userData.evolution[1].scale === SettlementScale.Hamlet, 'Authoritative Hamlet scale was not applied.');
  assert(settlements.userData.evolution[1].isActive === false, 'Dormant settlement state was not applied.');
  assert(parcels.userData.evolution[0].developmentState === 3, 'Parcel redevelopment state was not applied.');
  assert(buildings.userData.evolution[1].status === BuildingLifecycleStatus.Demolished, 'Building demolition state was not applied.');
  assert(relations.userData.relations[0].fromSettlementId === 101n && relations.userData.relations[0].toSettlementId === 102n, 'Regional relation stable IDs were not retained.');

  const a = instancePosition(settlements, 0);
  const b = instancePosition(settlements, 1);
  assert(a.distanceTo(b) > 1_000, 'Time evolution collapsed remote settlements into one city.');
  const demolishedScale = instanceScale(buildings, 1);
  assert(demolishedScale.y < 0.2, 'Demolished building was not represented as demolished.');
  assert(webgl.info.render.calls > 0, 'Three.js produced no draw calls for evolution rendering.');

  result.dataset.status = 'passed';
  result.dataset.currentYear = String(root.userData.currentYear);
  result.dataset.settlements = String(settlements.count);
  result.dataset.drawCalls = String(webgl.info.render.calls);
  result.textContent = `View Phase 4 evolution E2E passed: year=${result.dataset.currentYear}, settlements=${result.dataset.settlements}, draws=${result.dataset.drawCalls}`;
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
} finally {
  renderer.dispose();
  webgl.dispose();
}

function instancePosition(mesh, index) {
  const matrix = new THREE.Matrix4();
  mesh.getMatrixAt(index, matrix);
  return new THREE.Vector3().setFromMatrixPosition(matrix);
}

function instanceScale(mesh, index) {
  const matrix = new THREE.Matrix4();
  mesh.getMatrixAt(index, matrix);
  const position = new THREE.Vector3();
  const quaternion = new THREE.Quaternion();
  const scale = new THREE.Vector3();
  matrix.decompose(position, quaternion, scale);
  return scale;
}

function requireObject(name, type) {
  const object = scene.getObjectByName(name);
  assert(object instanceof type, `Missing ${name}.`);
  return object;
}

function assert(condition, message) { if (!condition) throw new Error(message); }

function createFullChunk() {
  return {
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25, tickCount: 84n, isFullSnapshot: true,
    settlements: [{ settlementId: 101n, x: 0, y: 0, z: 2, population: 25_000, jobs: 14_000, serviceIndex: 0.82, density: 0.74, accessibility: 0.91, influenceRadiusMeters: 4_600, scale: SettlementScale.City, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null }],
    parcels: [], buildings: [], serviceCatchments: [], infrastructureDemands: [], relations: [], events: [], commutingFlows: [], freightFlows: [],
  };
}

function createContinuationChunk() {
  return {
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25, tickCount: 84n, isFullSnapshot: false,
    settlements: [{ settlementId: 102n, x: 2_000, y: 1_200, z: 8, population: 18, jobs: 5, serviceIndex: 0.12, density: 0.05, accessibility: 0.24, influenceRadiusMeters: 500, scale: SettlementScale.Hamlet, trend: SettlementTrend.Dormant, isActive: false, establishedYear: 0, dormantSinceYear: 24 }],
    parcels: [
      { parcelId: 301n, settlementId: 101n, developmentDemand: 0.91, landValue: 0.88, developmentState: 3, buildingId: 401n },
      { parcelId: 302n, settlementId: 102n, developmentDemand: 0.12, landValue: 0.15, developmentState: 2, buildingId: 402n },
    ],
    buildings: [
      { buildingId: 401n, parcelId: 301n, use: 1, builtYear: 0, lastChangedYear: 25, condition: 0.92, occupancy: 0.86, capacity: 240, status: BuildingLifecycleStatus.Active },
      { buildingId: 402n, parcelId: 302n, use: 4, builtYear: -18, lastChangedYear: 25, condition: 0.06, occupancy: 0, capacity: 12, status: BuildingLifecycleStatus.Demolished },
    ],
    serviceCatchments: [], infrastructureDemands: [],
    relations: [{ relationId: 801n, fromSettlementId: 101n, toSettlementId: 102n, kind: 3, strength: 0.63, isActive: true, sinceYear: 20 }],
    events: [{ eventId: 901n, year: 25, kind: 9, settlementId: 102n, buildingId: 402n, reason: 'Abandoned->Demolished' }],
    commutingFlows: [], freightFlows: [],
  };
}

function createGenerationSnapshot() {
  return {
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 42n, worldSeed: 30_034n, preset: 1, iterations: 0,
    minX: -200, minY: -200, minZ: 0, maxX: 2_400, maxY: 1_500, maxZ: 120,
    settlements: [settlement(101n, 0, 0, 2, 3, 3_600), settlement(102n, 2_000, 1_200, 8, 1, 700)],
    growthEvents: [], corridors: [],
    districts: [
      { districtId: 201n, settlementId: 101n, kind: 1, minX: -120, minY: -120, minZ: 0, maxX: 180, maxY: 180, maxZ: 2, nameId: 0n, accessibility: 1 },
      { districtId: 202n, settlementId: 102n, kind: 5, minX: 1_900, minY: 1_100, minZ: 5, maxX: 2_120, maxY: 1_320, maxZ: 7, nameId: 0n, accessibility: 0.4 },
    ],
    parcels: [
      { parcelId: 301n, settlementId: 101n, districtId: 201n, minX: -40, minY: -40, minZ: 0, maxX: 80, maxY: 80, maxZ: 1, zone: 1, developmentState: 2, developmentSuitability: 1, landValue: 1, buildingId: 401n },
      { parcelId: 302n, settlementId: 102n, districtId: 202n, minX: 1_950, minY: 1_150, minZ: 5, maxX: 2_030, maxY: 1_230, maxZ: 6, zone: 5, developmentState: 2, developmentSuitability: 0.6, landValue: 0.3, buildingId: 402n },
    ],
    buildings: [
      { buildingId: 401n, parcelId: 301n, use: 1, minX: -20, minY: -20, minZ: 0, maxX: 60, maxY: 60, maxZ: 52, floors: 13, capacity: 240, historicalStage: 2 },
      { buildingId: 402n, parcelId: 302n, use: 4, minX: 1_970, minY: 1_170, minZ: 5, maxX: 2_010, maxY: 1_210, maxZ: 14, floors: 2, capacity: 12, historicalStage: 1 },
    ],
    pois: [], toponyms: [], roadSigns: [],
    quality: { terrainAdaptation: 0.9, roadConnectivity: 1, averageSlopeCost: 0.2, accessibility: 0.8, congestionRisk: 0.2, landUseConsistency: 1, floodExposure: 0, urbanCompactness: 0.7, polycentricBalance: 1, overallScore: 0.9 },
  };
}

function settlement(settlementId, x, y, z, role, influenceRadiusMeters) {
  return {
    settlementId, x, y, z, environment: 0, origin: 0, role, initialEconomy: 0,
    suitability: { flatness: 1, waterAccess: 1, transportPotential: 1, buildability: 1, resourceAccess: 1, floodRisk: 0, steepSlopeRisk: 0, isolation: 0, constructionCost: 0, totalScore: 1 },
    population: role === 3 ? 8_000 : 200, jobs: role === 3 ? 4_000 : 40, influenceRadiusMeters, nameId: 0n,
  };
}
