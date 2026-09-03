import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../src/regional-generation-protocol.ts';
import { SettlementStructureRenderer } from '../src/settlement-structure-renderer.ts';

function installCanvasStub() {
  const previousDocument = globalThis.document;
  globalThis.document = {
    createElement(tagName) {
      assert.equal(tagName, 'canvas');
      return {
        width: 0,
        height: 0,
        getContext(kind) {
          assert.equal(kind, '2d');
          return {
            font: '',
            textAlign: '',
            textBaseline: '',
            fillStyle: '',
            clearRect() {},
            fillRect() {},
            fillText() {},
          };
        },
      };
    },
  };
  return () => {
    if (previousDocument === undefined) delete globalThis.document;
    else globalThis.document = previousDocument;
  };
}

function createSnapshot(offset = 0) {
  const pois = [];
  const toponyms = [];
  for (let index = 0; index < 1_000; index += 1) {
    const id = BigInt(index + 1);
    pois.push({
      poiId: 10_000n + id,
      settlementId: 1n,
      kind: 0,
      x: offset + (index % 50) * 50,
      y: Math.floor(index / 50) * 50,
      z: 0,
      buildingId: 0n,
      nameId: id,
    });
    toponyms.push({
      toponymId: id,
      kind: 0,
      name: `Toponym ${String(index + 1)}`,
      sourceNaturalToponymId: 0n,
      sourceNaturalName: '',
      sourceFeatureId: 0n,
      parentHumanToponymId: 0n,
      generatorKey: 'test',
    });
  }
  return {
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 1n,
    worldSeed: 1n,
    preset: 0,
    iterations: 0,
    minX: 0,
    minY: 0,
    minZ: 0,
    maxX: 5_000,
    maxY: 5_000,
    maxZ: 100,
    settlements: [],
    growthEvents: [],
    corridors: [],
    districts: [],
    parcels: [],
    buildings: [],
    pois,
    toponyms,
    roadSigns: [],
    quality: {
      terrainAdaptation: 1,
      roadConnectivity: 1,
      averageSlopeCost: 0,
      accessibility: 1,
      congestionRisk: 0,
      landUseConsistency: 1,
      floodExposure: 0,
      urbanCompactness: 1,
      polycentricBalance: 1,
      overallScore: 1,
    },
  };
}

test('SettlementStructureRenderer batches large Toponym sets into one shared atlas with distance LOD', () => {
  const restoreDocument = installCanvasStub();
  try {
    const scene = new THREE.Scene();
    const renderer = new SettlementStructureRenderer(scene);
    const generation = { revision: 1, snapshot: createSnapshot() };

    renderer.update(generation);

    const batch = scene.getObjectByName('regional-toponym-labels');
    assert.ok(batch instanceof THREE.Mesh);
    assert.ok(batch.geometry instanceof THREE.InstancedBufferGeometry);
    assert.ok(batch.material instanceof THREE.ShaderMaterial);
    assert.ok(batch.userData.atlasTexture instanceof THREE.Texture);
    assert.equal(renderer.metrics.labels, 192);
    assert.equal(batch.geometry.instanceCount, 192);
    assert.equal(batch.userData.labelCount, 192);
    assert.equal(scene.getObjectsByProperty('type', 'Sprite').length, 0);

    const initialTexture = batch.userData.atlasTexture;
    const evolution = {
      revision: 1,
      snapshot: null,
      getParcel() { return undefined; },
      getBuilding() { return undefined; },
      getSettlement() { return undefined; },
    };
    renderer.update(generation, evolution);
    assert.equal(scene.getObjectByName('regional-toponym-labels'), batch);
    assert.equal(batch.userData.atlasTexture, initialTexture);

    const camera = new THREE.PerspectiveCamera();
    camera.position.copy(batch.userData.labelCenter).add(new THREE.Vector3(0, 0, 10_000_000));
    batch.onBeforeRender(null, scene, camera, batch.geometry, batch.material, null);
    assert.ok(batch.geometry.instanceCount <= 24);
    camera.position.copy(batch.userData.labelCenter);
    batch.onBeforeRender(null, scene, camera, batch.geometry, batch.material, null);
    assert.equal(batch.geometry.instanceCount, 192);

    let disposed = 0;
    initialTexture.addEventListener('dispose', () => { disposed += 1; });
    generation.revision = 2;
    generation.snapshot = createSnapshot(10_000);
    renderer.update(generation, evolution);
    const replacement = scene.getObjectByName('regional-toponym-labels');
    assert.ok(replacement instanceof THREE.Mesh);
    assert.notEqual(replacement, batch);
    assert.notEqual(replacement.userData.atlasTexture, initialTexture);
    assert.equal(disposed, 1);

    renderer.dispose();
  } finally {
    restoreDocument();
  }
});
