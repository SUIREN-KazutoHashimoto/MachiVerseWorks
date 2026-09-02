import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import { computePerspectiveSubscriptionVolume } from '../src/world-view.ts';

function createCamera(aspect = 16 / 9) {
  const camera = new THREE.PerspectiveCamera(55, aspect, 0.1, 50_000);
  camera.position.set(0, 500, 0);
  camera.lookAt(0, 0, -250);
  camera.updateProjectionMatrix();
  camera.updateMatrixWorld(true);
  return camera;
}

test('perspective subscription is finite and bounded by observation distance instead of visual far plane', () => {
  const camera = createCamera();
  const volume = computePerspectiveSubscriptionVolume(camera, 3_000, 1.2);

  for (const value of Object.values(volume)) assert.ok(Number.isFinite(value));
  assert.ok(volume.maxX - volume.minX < 8_000);
  assert.ok(volume.maxY - volume.minY < 8_000);
  assert.ok(volume.maxZ - volume.minZ < 8_000);
});

test('perspective subscription follows camera translation in Simulation XYZ coordinates', () => {
  const camera = createCamera();
  const original = computePerspectiveSubscriptionVolume(camera, 3_000, 1.2);

  camera.position.add(new THREE.Vector3(125, 300, -450));
  camera.updateMatrixWorld(true);
  const moved = computePerspectiveSubscriptionVolume(camera, 3_000, 1.2);

  assert.ok(Math.abs((moved.minX - original.minX) - 125) < 1e-6);
  assert.ok(Math.abs((moved.maxX - original.maxX) - 125) < 1e-6);
  assert.ok(Math.abs((moved.minY - original.minY) + 450) < 1e-6);
  assert.ok(Math.abs((moved.maxY - original.maxY) + 450) < 1e-6);
  assert.ok(Math.abs((moved.minZ - original.minZ) - 300) < 1e-6);
  assert.ok(Math.abs((moved.maxZ - original.maxZ) - 300) < 1e-6);
});

test('wide perspective subscription stays within the default server cell budget', () => {
  const camera = createCamera(21 / 9);
  const volume = computePerspectiveSubscriptionVolume(camera, 3_000, 1.2);
  const cellSize = 64;
  const cellCount = countCells(volume.minX, volume.maxX, cellSize)
    * countCells(volume.minY, volume.maxY, cellSize)
    * countCells(volume.minZ, volume.maxZ, cellSize);

  assert.ok(cellCount <= 1_048_576, `subscription covers ${cellCount} cells`);
});

test('observation distance must remain inside a valid perspective range', () => {
  const camera = createCamera();
  assert.throws(() => computePerspectiveSubscriptionVolume(camera, 0.05, 1.2), RangeError);
  assert.throws(() => computePerspectiveSubscriptionVolume(camera, 3_000, 0.9), RangeError);
});

function countCells(minimum, maximum, cellSize) {
  return Math.floor(maximum / cellSize) - Math.floor(minimum / cellSize) + 1;
}
