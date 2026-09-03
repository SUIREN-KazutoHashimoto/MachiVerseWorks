import { performance } from 'node:perf_hooks';
import * as THREE from 'three';

import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../src/regional-generation-protocol.ts';
import { SettlementStructureRenderer } from '../src/settlement-structure-renderer.ts';

globalThis.document = {
  createElement() {
    return {
      width: 0,
      height: 0,
      getContext() {
        return {
          font: '', textAlign: '', textBaseline: '', fillStyle: '',
          clearRect() {}, fillRect() {}, fillText() {},
        };
      },
    };
  },
};

function snapshot(labelCount) {
  const pois = Array.from({ length: labelCount }, (_, index) => {
    const id = BigInt(index + 1);
    return { poiId: 100_000n + id, settlementId: 1n, kind: 0, x: (index % 100) * 50, y: Math.floor(index / 100) * 50, z: 0, buildingId: 0n, nameId: id };
  });
  const toponyms = Array.from({ length: labelCount }, (_, index) => {
    const id = BigInt(index + 1);
    return { toponymId: id, kind: 0, name: `Toponym ${String(index + 1)}`, sourceNaturalToponymId: 0n, sourceNaturalName: '', sourceFeatureId: 0n, parentHumanToponymId: 0n, generatorKey: 'benchmark' };
  });
  return {
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 1n, worldSeed: 1n, preset: 0, iterations: 0,
    minX: 0, minY: 0, minZ: 0, maxX: 5_000, maxY: 5_000, maxZ: 100,
    settlements: [], growthEvents: [], corridors: [], districts: [], parcels: [], buildings: [],
    pois, toponyms, roadSigns: [],
    quality: { terrainAdaptation: 1, roadConnectivity: 1, averageSlopeCost: 0, accessibility: 1, congestionRisk: 0, landUseConsistency: 1, floodExposure: 0, urbanCompactness: 1, polycentricBalance: 1, overallScore: 1 },
  };
}

for (const labelCount of [256, 512, 1_000, 2_000]) {
  const scene = new THREE.Scene();
  const renderer = new SettlementStructureRenderer(scene);
  const start = performance.now();
  renderer.update({ revision: 1, snapshot: snapshot(labelCount) });
  const elapsedMilliseconds = performance.now() - start;
  const batch = scene.getObjectByName('regional-toponym-labels');
  console.log(JSON.stringify({
    inputLabels: labelCount,
    visibleLabels: renderer.metrics.labels,
    renderObjects: batch === undefined ? 0 : 1,
    atlasTextures: batch?.userData.atlasTexture === undefined ? 0 : 1,
    elapsedMilliseconds: Number(elapsedMilliseconds.toFixed(3)),
  }));
  renderer.dispose();
}
