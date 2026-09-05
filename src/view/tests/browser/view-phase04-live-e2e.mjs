import * as THREE from 'three';

import { MachiVerseConnection } from '../../src/connection.ts';
import { PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE } from '../../src/persistent-regional-evolution-protocol.ts';
import { PersistentRegionalEvolutionStore } from '../../src/persistent-regional-evolution-store.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../../src/regional-generation-protocol.ts';
import { RegionalGenerationStore } from '../../src/regional-generation-store.ts';
import { SettlementStructureRenderer } from '../../src/settlement-structure-renderer.ts';

const result = document.querySelector('#result');
const viewport = document.querySelector('#viewport');
if (!(result instanceof HTMLElement) || !(viewport instanceof HTMLElement)) throw new Error('View Phase 4 live browser harness is invalid.');

const server = new URLSearchParams(location.search).get('server');
if (!server) throw new Error('Missing server WebSocket URL.');

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(55, 1024 / 768, 0.1, 20_000);
camera.position.set(0, 0, 6_500);
camera.lookAt(0, 0, 0);
const webgl = new THREE.WebGLRenderer({ antialias: false });
webgl.setSize(1024, 768, false);
viewport.appendChild(webgl.domElement);
const store = new RegionalGenerationStore();
const evolution = new PersistentRegionalEvolutionStore();
const settlementRenderer = new SettlementStructureRenderer(scene);
let finished = false;

const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for live RegionalGenerationSnapshot + PersistentRegionalEvolutionSnapshot.')), 15_000);
const connection = new MachiVerseConnection(server, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: () => {},
  onProtocolError: (message) => fail(new Error(`Protocol error: ${message.code}`)),
  onClientError: (error) => fail(error),
  onDisconnected: () => {},
  onHelloAck: () => {},
  onMessage: (message) => {
    if (finished) return;
    try {
      if (message.type === REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE) store.replace(message);
      else if (message.type === PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE) evolution.apply(message);
      else return;
      verifyWhenComplete();
    } catch (error) {
      fail(error);
    }
  },
});
connection.connect();

function verifyWhenComplete() {
  const baseline = store.snapshot;
  const overlay = evolution.snapshot;
  if (baseline === null || overlay === null) return;

  settlementRenderer.update(store, evolution);
  webgl.render(scene, camera);
  const metrics = settlementRenderer.metrics;
  assert(metrics.settlements >= 2, 'Live baseline did not render multiple Settlements.');
  assert(metrics.districts > 0, 'Live baseline did not render Districts.');
  assert(metrics.parcels > 0, 'Live baseline did not render Parcels.');
  assert(metrics.buildings > 0, 'Live baseline did not render Buildings.');
  assert(metrics.pois > 0, 'Live baseline did not render POIs.');
  assert(metrics.labels > 0, 'Live baseline did not render Toponyms.');
  assert(webgl.info.render.calls > 0, 'Live baseline produced no Three.js draw calls.');
  assert(overlay.currentYear >= 25, 'Live Phase31 overlay did not contain the advanced regional evolution fixture.');

  for (const parcel of baseline.parcels) {
    assert(store.getSettlementForParcel(parcel.parcelId)?.settlementId === parcel.settlementId, 'Live Parcel→Settlement stable relation was lost.');
    assert(store.getDistrictForParcel(parcel.parcelId)?.districtId === parcel.districtId, 'Live Parcel→District stable relation was lost.');
  }
  for (const building of baseline.buildings) {
    assert(store.getParcel(building.parcelId)?.parcelId === building.parcelId, 'Live Building→Parcel stable relation was lost.');
  }
  for (const poi of baseline.pois) {
    assert(store.getSettlement(poi.settlementId)?.settlementId === poi.settlementId, 'Live POI→Settlement stable relation was lost.');
  }
  for (const sign of baseline.roadSigns) {
    assert(baseline.corridors.some((corridor) => corridor.corridorId === sign.corridorId), 'Live RoadSign→Corridor stable relation was lost.');
  }

  for (const settlement of overlay.settlements) {
    assert(store.getSettlement(settlement.settlementId)?.settlementId === settlement.settlementId, 'Phase31 Settlement did not join to Phase30 geometry by stable ID.');
  }
  for (const parcel of overlay.parcels) {
    assert(store.getParcel(parcel.parcelId)?.parcelId === parcel.parcelId, 'Phase31 Parcel did not join to Phase30 geometry by stable ID.');
    assert(evolution.getSettlementForParcel(parcel.parcelId)?.settlementId === parcel.settlementId, 'Phase31 Parcel→Settlement relation was lost.');
  }
  for (const building of overlay.buildings) {
    assert(evolution.getParcelForBuilding(building.buildingId)?.parcelId === building.parcelId, 'Phase31 Building→Parcel relation was lost.');
  }
  for (const relation of overlay.relations) {
    assert(store.getSettlement(relation.fromSettlementId) !== undefined, 'Phase31 regional relation source did not join to Phase30 Settlement.');
    assert(store.getSettlement(relation.toSettlementId) !== undefined, 'Phase31 regional relation target did not join to Phase30 Settlement.');
  }

  finished = true;
  window.clearTimeout(timeout);
  result.dataset.status = 'passed';
  result.dataset.settlements = String(metrics.settlements);
  result.dataset.districts = String(metrics.districts);
  result.dataset.parcels = String(metrics.parcels);
  result.dataset.buildings = String(metrics.buildings);
  result.dataset.pois = String(metrics.pois);
  result.dataset.labels = String(metrics.labels);
  result.dataset.roadSigns = String(metrics.roadSigns);
  result.dataset.drawCalls = String(webgl.info.render.calls);
  result.dataset.evolutionYear = String(overlay.currentYear);
  result.dataset.evolutionSettlements = String(overlay.settlements.length);
  result.textContent = `View Phase 4 live Browser E2E passed: settlements=${metrics.settlements}, districts=${metrics.districts}, parcels=${metrics.parcels}, buildings=${metrics.buildings}, pois=${metrics.pois}, labels=${metrics.labels}, roadSigns=${metrics.roadSigns}, evolutionYear=${overlay.currentYear}, evolutionSettlements=${overlay.settlements.length}, draws=${webgl.info.render.calls}`;
  connection.disconnect();
}

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function fail(error) {
  if (finished) return;
  finished = true;
  window.clearTimeout(timeout);
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
  connection.disconnect();
}
