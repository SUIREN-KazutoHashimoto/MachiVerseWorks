import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import { LaneDirection } from '../src/protocol.ts';
import {
  computeLaneCenterOffsets,
  computeOrthographicSubscriptionVolume,
  simulationToThreePosition,
} from '../src/world-view.ts';

test('Simulation XYZ maps horizontal Y to Three Z and altitude Z to Three Y', () => {
  const position = simulationToThreePosition(12, 34, 56);

  assert.equal(position.x, 12);
  assert.equal(position.y, 56);
  assert.equal(position.z, 34);
});

test('tilted camera subscription covers the full 3D frustum instead of a fixed altitude slab', () => {
  const camera = createCamera(8);
  const volume = computeOrthographicSubscriptionVolume(camera, 1.2);

  assert.ok(volume.minX <= 0 && volume.maxX >= 0);
  assert.ok(volume.minY <= -250 && volume.maxY >= -250);
  assert.ok(volume.minZ < -128, `expected minZ below the old fixed band, got ${String(volume.minZ)}`);
  assert.ok(volume.maxZ > 512, `expected maxZ above the old fixed band, got ${String(volume.maxZ)}`);
});

test('subscription altitude follows camera altitude', () => {
  const camera = createCamera(8);
  const original = computeOrthographicSubscriptionVolume(camera, 1.2);

  camera.position.y += 1_000;
  camera.updateMatrixWorld(true);
  const moved = computeOrthographicSubscriptionVolume(camera, 1.2);

  assert.ok(Math.abs((moved.minZ - original.minZ) - 1_000) < 1e-6);
  assert.ok(Math.abs((moved.maxZ - original.maxZ) - 1_000) < 1e-6);
});

for (const [label, aspect] of [['16:9', 16 / 9], ['21:9', 21 / 9]]) {
  test(`minimum zoom full-frustum subscription stays within the default server cell budget at ${label}`, () => {
    const camera = createCamera(0.25, aspect);
    const volume = computeOrthographicSubscriptionVolume(camera, 1.2);
    const cellSize = 64;
    const cellCount = countCells(volume.minX, volume.maxX, cellSize) *
      countCells(volume.minY, volume.maxY, cellSize) *
      countCells(volume.minZ, volume.maxZ, cellSize);

    assert.ok(cellCount <= 1_048_576, `subscription covers ${cellCount} cells`);
  });
}

test('variable lane widths use cumulative preceding widths instead of order multiplied by own width', () => {
  const offsets = computeLaneCenterOffsets([
    { id: 10n, segmentId: 1n, direction: LaneDirection.Forward, order: 0, widthMeters: 3, speedLimitMetersPerSecond: 10 },
    { id: 11n, segmentId: 1n, direction: LaneDirection.Forward, order: 4, widthMeters: 5, speedLimitMetersPerSecond: 10 },
    { id: 12n, segmentId: 1n, direction: LaneDirection.Forward, order: 9, widthMeters: 4, speedLimitMetersPerSecond: 10 },
  ]);

  assert.equal(offsets.get(10n), 1.5);
  assert.equal(offsets.get(11n), 5.5);
  assert.equal(offsets.get(12n), 10);
});

test('reverse lanes mirror cumulative lane-center offsets without artificial order gaps', () => {
  const offsets = computeLaneCenterOffsets([
    { id: 20n, segmentId: 1n, direction: LaneDirection.Reverse, order: 2, widthMeters: 3.2, speedLimitMetersPerSecond: 10 },
    { id: 21n, segmentId: 1n, direction: LaneDirection.Reverse, order: 8, widthMeters: 4.8, speedLimitMetersPerSecond: 10 },
  ]);

  assert.equal(offsets.get(20n), -1.6);
  assert.equal(offsets.get(21n), -5.6);
});

test('subscription remains valid for a horizontal camera direction', () => {
  const camera = createCamera(2);
  camera.lookAt(0, 500, -250);
  camera.updateMatrixWorld(true);

  const volume = computeOrthographicSubscriptionVolume(camera, 1.2);

  assert.ok(Number.isFinite(volume.minX));
  assert.ok(Number.isFinite(volume.minY));
  assert.ok(Number.isFinite(volume.minZ));
  assert.ok(Number.isFinite(volume.maxX));
  assert.ok(Number.isFinite(volume.maxY));
  assert.ok(Number.isFinite(volume.maxZ));
});

function createCamera(zoom, aspect = 16 / 9) {
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
