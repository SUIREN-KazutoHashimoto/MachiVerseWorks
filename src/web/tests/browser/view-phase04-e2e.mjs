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
if (!(result instanceof HTMLElement) || !(viewport instanceof HTMLElement)) throw new Error('View Phase 4 browser harness is invalid.');

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0b1020);
const camera = new THREE.PerspectiveCamera(55, 1024 / 768, 0.1, 10_000);
camera.position.set(1_000, 1_500, 2_400);
camera.lookAt(1_000, 0, 600);
const webgl = new THREE.WebGLRenderer({ antialias: false });
webgl.setSize(1024, 768, false);
viewport.appendChild(webgl.domElement);

const store = new RegionalGenerationStore();
const evolution = new PersistentRegionalEvolutionStore();
const settlementRenderer = new SettlementStructureRenderer(scene);

try {
  store.replace(createSnapshot());
  evolution.replace(createEvolutionSnapshot());
  settlementRenderer.update(store, evolution);
  webgl.render(scene, camera);

  const metrics = settlementRenderer.metrics;
  assert(metrics.settlements === 2, 'Expected two separately rendered Settlements.');
  assert(metrics.corridors === 1, 'Expected one authoritative corridor.');
  assert(metrics.districts === 2, 'Expected two Districts.');
  assert(metrics.parcels === 2, 'Expected two Parcels.');
  assert(metrics.buildings === 2, 'Expected two Buildings.');
  assert(metrics.pois === 2, 'Expected two POIs.');
  assert(metrics.labels === 7, 'Expected all named Settlement/District/Corridor/POI labels.');
  assert(metrics.roadSigns === 1, 'Expected one Road Sign.');

  const regionalRoot = requireObject('regional-generation', THREE.Group);
  assert(regionalRoot.userData.currentYear === 25, 'Protocol 2.19 current year was not applied to the rendering revision.');

  const settlements = requireObject('regional-settlements', THREE.InstancedMesh);
  assert(settlements.count === 2, 'Settlement instances were collapsed.');
  assert(settlements.userData.evolution[0].scale === SettlementScale.City, 'Authoritative City classification was not retained.');
  assert(settlements.userData.evolution[0].trend === SettlementTrend.Growing, 'Authoritative Settlement trend was not retained.');
  assert(settlements.userData.evolution[1].scale === SettlementScale.Hamlet, 'Authoritative Hamlet classification was not retained.');
  assert(settlements.userData.evolution[1].isActive === false, 'Dormant Settlement activity state was not retained.');

  const firstMatrix = new THREE.Matrix4();
  const secondMatrix = new THREE.Matrix4();
  settlements.getMatrixAt(0, firstMatrix);
  settlements.getMatrixAt(1, secondMatrix);
  const firstPosition = new THREE.Vector3().setFromMatrixPosition(firstMatrix);
  const secondPosition = new THREE.Vector3().setFromMatrixPosition(secondMatrix);
  assert(firstPosition.distanceTo(secondPosition) > 1_000, 'Remote Settlement was aggregated into the primary Settlement.');

  const firstColor = new THREE.Color();
  const secondColor = new THREE.Color();
  settlements.getColorAt(0, firstColor);
  settlements.getColorAt(1, secondColor);
  assert(firstColor.getHex() !== secondColor.getHex(), 'Authoritative Settlement scale/trend did not produce distinct presentation.');

  const parcels = requireObject('regional-parcels', THREE.InstancedMesh);
  const buildings = requireObject('regional-buildings', THREE.InstancedMesh);
  const pois = requireObject('regional-pois', THREE.Points);
  assert(parcels.userData.relations[0].parcelId === 301n, 'Parcel stable ID was not retained in the renderer.');
  assert(parcels.userData.relations[0].districtId === 201n, 'Parcel→District relation was not retained.');
  assert(parcels.userData.relations[0].settlementId === 101n, 'Parcel→Settlement relation was not retained.');
  assert(parcels.userData.evolution[0].developmentState === 3, 'Authoritative Parcel redevelopment state was not applied.');
  assert(buildings.userData.relations[1].parcelId === 302n, 'Building→Parcel relation was not retained.');
  assert(buildings.userData.evolution[1].status === BuildingLifecycleStatus.Demolished, 'Authoritative Building demolition state was not applied.');
  assert(pois.userData.relations[1].buildingId === 402n, 'POI→Building relation was not retained.');
  assert(store.getSettlementForBuilding(402n)?.settlementId === 102n, 'Building→Settlement traversal did not preserve the authoritative stable IDs.');
  assert(store.getDistrictForBuilding(402n)?.districtId === 202n, 'Building→District traversal did not preserve the authoritative stable IDs.');

  const demolishedMatrix = new THREE.Matrix4();
  buildings.getMatrixAt(1, demolishedMatrix);
  const demolishedScale = new THREE.Vector3();
  demolishedMatrix.decompose(new THREE.Vector3(), new THREE.Quaternion(), demolishedScale);
  assert(demolishedScale.y < 0.2, 'Demolished Building was not reduced to the lifecycle presentation baseline.');

  const metroRelations = requireObject('regional-evolution-relations-3', THREE.LineSegments);
  assert(metroRelations.userData.relations[0].fromSettlementId === 101n, 'Regional relation source stable ID was not retained.');
  assert(metroRelations.userData.relations[0].toSettlementId === 102n, 'Regional relation destination stable ID was not retained.');

  const roadSigns = requireObject('regional-road-signs', THREE.Points);
  assert(roadSigns.userData.labels[0].roadSignId === '701', 'Road Sign stable ID was not retained.');
  assert(roadSigns.userData.labels[0].destinationSettlementId === '102', 'Road Sign destination relation was not retained.');
  assert(scene.getObjectByName('regional-toponym-1001') instanceof THREE.Sprite, 'Settlement Toponym was not rendered as a browser sprite.');
  assert(scene.getObjectByName('regional-toponym-1007') instanceof THREE.Sprite, 'POI Toponym was not rendered as a browser sprite.');
  assert(webgl.info.render.calls > 0, 'Three.js produced no browser draw calls.');
  assert(webgl.info.memory.geometries > 0, 'Three.js produced no browser geometry.');

  result.dataset.status = 'passed';
  result.dataset.drawCalls = String(webgl.info.render.calls);
  result.dataset.geometries = String(webgl.info.memory.geometries);
  result.dataset.settlements = String(metrics.settlements);
  result.dataset.parcels = String(metrics.parcels);
  result.dataset.buildings = String(metrics.buildings);
  result.dataset.labels = String(metrics.labels);
  result.dataset.roadSigns = String(metrics.roadSigns);
  result.dataset.currentYear = String(regionalRoot.userData.currentYear);
  result.textContent = `View Phase 4 browser E2E passed: draws=${result.dataset.drawCalls}, geometries=${result.dataset.geometries}, settlements=${result.dataset.settlements}, parcels=${result.dataset.parcels}, buildings=${result.dataset.buildings}, labels=${result.dataset.labels}, signs=${result.dataset.roadSigns}, year=${result.dataset.currentYear}`;
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
} finally {
  settlementRenderer.dispose();
  webgl.dispose();
}

function requireObject(name, type) {
  const object = scene.getObjectByName(name);
  assert(object instanceof type, `Missing ${name} presentation primitive.`);
  return object;
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function createEvolutionSnapshot() {
  return Object.freeze({
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25,
    tickCount: 84n,
    settlements: Object.freeze([
      Object.freeze({ settlementId: 101n, x: 0, y: 0, z: 2, population: 25_000, jobs: 14_000, serviceIndex: 0.82, density: 0.74, accessibility: 0.91, influenceRadiusMeters: 4_600, scale: SettlementScale.City, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null }),
      Object.freeze({ settlementId: 102n, x: 2_000, y: 1_200, z: 8, population: 18, jobs: 5, serviceIndex: 0.12, density: 0.05, accessibility: 0.24, influenceRadiusMeters: 500, scale: SettlementScale.Hamlet, trend: SettlementTrend.Dormant, isActive: false, establishedYear: 0, dormantSinceYear: 24 }),
    ]),
    parcels: Object.freeze([
      Object.freeze({ parcelId: 301n, settlementId: 101n, developmentDemand: 0.91, landValue: 0.88, developmentState: 3, buildingId: 401n }),
      Object.freeze({ parcelId: 302n, settlementId: 102n, developmentDemand: 0.12, landValue: 0.15, developmentState: 2, buildingId: 402n }),
    ]),
    buildings: Object.freeze([
      Object.freeze({ buildingId: 401n, parcelId: 301n, use: 1, builtYear: 0, lastChangedYear: 25, condition: 0.92, occupancy: 0.86, capacity: 240, status: BuildingLifecycleStatus.Active }),
      Object.freeze({ buildingId: 402n, parcelId: 302n, use: 4, builtYear: -18, lastChangedYear: 25, condition: 0.06, occupancy: 0, capacity: 12, status: BuildingLifecycleStatus.Demolished }),
    ]),
    serviceCatchments: Object.freeze([]),
    infrastructureDemands: Object.freeze([]),
    relations: Object.freeze([
      Object.freeze({ relationId: 801n, fromSettlementId: 101n, toSettlementId: 102n, kind: 3, strength: 0.63, isActive: true, sinceYear: 20 }),
    ]),
    events: Object.freeze([
      Object.freeze({ eventId: 901n, year: 25, kind: 9, settlementId: 102n, buildingId: 402n, reason: 'Abandoned->Demolished' }),
    ]),
    commutingFlows: Object.freeze([]),
    freightFlows: Object.freeze([]),
  });
}

function createSnapshot() {
  const toponyms = Object.freeze([
    toponym(1001n, 0, 'Central Settlement', 0n),
    toponym(1002n, 0, 'Remote Settlement', 0n),
    toponym(1003n, 1, 'Central District', 1001n),
    toponym(1004n, 1, 'Remote District', 1002n),
    toponym(1005n, 2, 'Regional Link', 0n),
    toponym(1006n, 5, 'Central Market', 1003n),
    toponym(1007n, 5, 'Remote Hall', 1004n),
  ]);
  return Object.freeze({
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 42n,
    worldSeed: 30_034n,
    preset: 1,
    iterations: 0,
    minX: -200, minY: -200, minZ: 0, maxX: 2_400, maxY: 1_500, maxZ: 120,
    settlements: Object.freeze([
      settlement(101n, 0, 0, 2, 3, 0, 3_600, 1001n, 8_000, 4_200),
      settlement(102n, 2_000, 1_200, 8, 1, 1, 700, 1002n, 420, 120),
    ]),
    growthEvents: Object.freeze([]),
    corridors: Object.freeze([{
      corridorId: 601n,
      kind: 1,
      fromSettlementId: 101n,
      toSettlementId: 102n,
      geometry: Object.freeze([{ x: 0, y: 0, z: 2 }, { x: 1_000, y: 580, z: 5 }, { x: 2_000, y: 1_200, z: 8 }]),
      terrainAdaptation: 0.9,
      constructionCost: 10,
      nameId: 1005n,
    }]),
    districts: Object.freeze([
      { districtId: 201n, settlementId: 101n, kind: 1, minX: -120, minY: -120, minZ: 0, maxX: 180, maxY: 180, maxZ: 2, nameId: 1003n, accessibility: 1 },
      { districtId: 202n, settlementId: 102n, kind: 5, minX: 1_900, minY: 1_100, minZ: 5, maxX: 2_120, maxY: 1_320, maxZ: 7, nameId: 1004n, accessibility: 0.4 },
    ]),
    parcels: Object.freeze([
      { parcelId: 301n, settlementId: 101n, districtId: 201n, minX: -40, minY: -40, minZ: 0, maxX: 80, maxY: 80, maxZ: 1, zone: 1, developmentState: 2, developmentSuitability: 1, landValue: 1, buildingId: 401n },
      { parcelId: 302n, settlementId: 102n, districtId: 202n, minX: 1_950, minY: 1_150, minZ: 5, maxX: 2_030, maxY: 1_230, maxZ: 6, zone: 5, developmentState: 2, developmentSuitability: 0.6, landValue: 0.3, buildingId: 402n },
    ]),
    buildings: Object.freeze([
      { buildingId: 401n, parcelId: 301n, use: 1, minX: -20, minY: -20, minZ: 0, maxX: 60, maxY: 60, maxZ: 52, floors: 13, capacity: 240, historicalStage: 2 },
      { buildingId: 402n, parcelId: 302n, use: 4, minX: 1_970, minY: 1_170, minZ: 5, maxX: 2_010, maxY: 1_210, maxZ: 14, floors: 2, capacity: 12, historicalStage: 1 },
    ]),
    pois: Object.freeze([
      { poiId: 501n, settlementId: 101n, kind: 1, x: 20, y: 20, z: 52, buildingId: 401n, nameId: 1006n },
      { poiId: 502n, settlementId: 102n, kind: 3, x: 1_990, y: 1_190, z: 14, buildingId: 402n, nameId: 1007n },
    ]),
    toponyms,
    roadSigns: Object.freeze([{
      roadSignId: 701n,
      kind: 0,
      x: 1_020, y: 600, z: 5,
      corridorId: 601n,
      destinationSettlementId: 102n,
      featureId: 0n,
      text: 'Remote Settlement',
    }]),
    quality: Object.freeze({
      terrainAdaptation: 0.9, roadConnectivity: 1, averageSlopeCost: 0.2, accessibility: 0.8,
      congestionRisk: 0.2, landUseConsistency: 1, floodExposure: 0, urbanCompactness: 0.7,
      polycentricBalance: 1, overallScore: 0.9,
    }),
  });
}

function settlement(settlementId, x, y, z, role, environment, influenceRadiusMeters, nameId, population, jobs) {
  return Object.freeze({
    settlementId, x, y, z, environment, origin: 0, role, initialEconomy: 0,
    suitability: Object.freeze({
      flatness: 1, waterAccess: 1, transportPotential: 1, buildability: 1, resourceAccess: 1,
      floodRisk: 0, steepSlopeRisk: 0, isolation: 0, constructionCost: 0, totalScore: 1,
    }),
    population, jobs, influenceRadiusMeters, nameId,
  });
}

function toponym(toponymId, kind, name, parentHumanToponymId) {
  return Object.freeze({
    toponymId, kind, name,
    sourceNaturalToponymId: 0n,
    sourceNaturalName: '',
    sourceFeatureId: 0n,
    parentHumanToponymId,
    generatorKey: 'view-phase04-browser-e2e',
  });
}