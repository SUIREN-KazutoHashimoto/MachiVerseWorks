import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import {
  computeOrthographicSubscriptionVolume,
  simulationToThreePosition,
} from '../src/world-view.ts';

test('Simulation XYZ maps horizontal Y to Three Z and altitude Z to Three Y', () => {
  const position = simulationToThreePosition(12, 34, 56);

  assert.equal(position.x, 12);
  assert.equal(position.y, 56);
  assert.equal(position.z, 34);
});

test('tilted camera subscription covers the projected ground focus', () => {
  const camera = createCamera(8);
  const volume = computeOrthographicSubscriptionVolume(camera, -128, 512, 1.2);

  assert.ok(volume.minX <= 0 && volume.maxX >= 0);
  assert.ok(volume.minY <= -250 && volume.maxY >= -250);
  assert.equal(volume.minZ, -128);
  assert.equal(volume.maxZ, 512);
});

test('minimum zoom subscription stays within the default server cell budget', () => {
  const camera = createCamera(0.25);
  const volume = computeOrthographicSubscriptionVolume(camera, -128, 512, 1.2);
  const cellSize = 64;
  const cellCount = countCells(volume.minX, volume.maxX, cellSize) *
    countCells(volume.minY, volume.maxY, cellSize) *
    countCells(volume.minZ, volume.maxZ, cellSize);

  assert.ok(cellCount <= 65_536, `subscription covers ${cellCount} cells`);
});

function createCamera(zoom) {
  const aspect = 16 / 9;
  const camera = new THREE.OrthographicCamera(-300 * aspect, 300 * aspect, 300, -300, 0.1, 2_000);
  camera.position.set(0, 500, 0);
  camera.up.set(0, 1, 0);
  camera.lookAt(0, 0, -250);
  camera.zoom = zoom;
  camera.updateProjectionMatrix();
  camera.updateMatrixWorld(true);
  return camera;
}

function countCells(minimum, maximum, cellSize) {
  return Math.floor(maximum / cellSize) - Math.floor(minimum / cellSize) + 1;
}
