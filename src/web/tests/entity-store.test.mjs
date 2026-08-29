import test from 'node:test';
import assert from 'node:assert/strict';

import { EntityStore } from '../src/entity-store.ts';

const snapshot = (x, z, tickCount) => ({
  agentId: 7n,
  x,
  y: 0,
  z,
  velocityX: 1,
  velocityY: 0,
  velocityZ: 2,
  tickCount,
});

test('spawn, 3D update interpolation, and remove are applied', () => {
  const store = new EntityStore();
  store.spawn(snapshot(0, 10, 1n), 0);
  assert.equal(store.size, 1);
  assert.equal(store.update(snapshot(10, 30, 2n), 100), true);

  const [halfway] = [...store.sample(150)];
  assert.equal(halfway.x, 5);
  assert.equal(halfway.z, 20);
  assert.equal(halfway.velocityZ, 2);
  assert.equal(halfway.tickCount, 2n);

  assert.equal(store.remove(7n), true);
  assert.equal(store.size, 0);
});

test('unknown updates do not implicitly create an entity', () => {
  const store = new EntityStore();
  assert.equal(store.update(snapshot(10, 30, 2n), 100), false);
  assert.equal(store.size, 0);
});

test('hot-path interpolation writes XYZ into a reusable position buffer', () => {
  const store = new EntityStore();
  store.spawn(snapshot(0, 10, 1n), 0);
  store.update(snapshot(10, 30, 2n), 100);
  const positions = new Float32Array(3);

  assert.equal(store.writeSampledPositions(150, positions), 1);
  assert.equal(positions[0], 5);
  assert.equal(positions[1], 0);
  assert.equal(positions[2], 20);
  assert.equal(store.writeSampledPositions(200, positions), 1);
  assert.equal(positions[0], 10);
  assert.equal(positions[2], 30);
});
