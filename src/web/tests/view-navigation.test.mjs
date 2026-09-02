import test from 'node:test';
import assert from 'node:assert/strict';

import {
  ViewNavigationController,
  createEntityNavigationTarget,
  createStaticNavigationTarget,
  getCameraFocusAtSimulationAltitude,
} from '../src/view-navigation.ts';

class FakeCamera {
  position = { x: 0, y: 500, z: 0 };
  zoom = 1;
  matrixWorld = { elements: new Float64Array(16) };
  direction = normalize({ x: 0, y: -500, z: -250 });

  constructor() { this.updateMatrixWorld(true); }
  lookAt(x, y, z) {
    this.direction = normalize({ x: x - this.position.x, y: y - this.position.y, z: z - this.position.z });
    this.updateMatrixWorld(true);
  }
  updateMatrixWorld() {
    this.matrixWorld.elements[8] = -this.direction.x;
    this.matrixWorld.elements[9] = -this.direction.y;
    this.matrixWorld.elements[10] = -this.direction.z;
  }
  updateProjectionMatrix() {}
}

class FakeSurface {
  listeners = new Map();
  addEventListener(type, listener) { this.listeners.set(type, listener); }
  removeEventListener(type, listener) { if (this.listeners.get(type) === listener) this.listeners.delete(type); }
  setPointerCapture() {}
}

test('jump centers a remote target without depending on distance from the world origin', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const target = createStaticNavigationTarget('settlement', 'remote-town', { x: 1_000_000_000, y: -2_000_000_000, z: 75 });

  assert.equal(navigation.jump(target, 0), true);
  const focus = getCameraFocusAtSimulationAltitude(camera, 75);
  assert.ok(focus !== undefined);
  assert.ok(Math.abs(focus.x - 1_000_000_000) < 1e-6);
  assert.ok(Math.abs(focus.y + 2_000_000_000) < 1e-6);
  assert.equal(focus.z, 75);
});

test('focus applies target preferred zoom while keeping target centered', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const target = createStaticNavigationTarget('geographic-feature', 'ridge', { x: 250, y: -400, z: 120 }, 6);

  assert.equal(navigation.focus(target, 0), true);
  assert.equal(camera.zoom, 6);
  const focus = getCameraFocusAtSimulationAltitude(camera, 120);
  assert.ok(focus !== undefined);
  assert.ok(Math.abs(focus.x - 250) < 1e-9);
  assert.ok(Math.abs(focus.y + 400) < 1e-9);
});

test('rotate and altitude are camera-local and do not require simulation mutation', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const beforeVertical = camera.direction.y;

  navigation.rotateBy(Math.PI / 2);
  navigation.adjustAltitude(125);

  assert.ok(Math.abs(camera.direction.y - beforeVertical) < 1e-12);
  assert.ok(Math.abs(camera.direction.x) > 0.1);
  assert.equal(camera.position.y, 625);
});

test('entity follow samples the current authoritative observation by id', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  let position = { x: 10, y: 20, z: 0 };
  const store = {
    size: 1,
    writeSampledPositions: () => 0,
    sample: function* () {},
    sampleById: (agentId) => agentId === 7n ? { agentId, ...position, velocityX: 0, velocityY: 0, velocityZ: 0, tickCount: 1n } : undefined,
  };
  const target = createEntityNavigationTarget(7n, store, 4);

  assert.equal(navigation.follow(target, 0), true);
  position = { x: 500, y: -750, z: 40 };
  navigation.update(100);

  const focus = getCameraFocusAtSimulationAltitude(camera, 40);
  assert.ok(focus !== undefined);
  assert.ok(Math.abs(focus.x - 500) < 1e-9);
  assert.ok(Math.abs(focus.y + 750) < 1e-9);
  assert.equal(camera.zoom, 4);
});

function normalize(vector) {
  const length = Math.hypot(vector.x, vector.y, vector.z);
  return { x: vector.x / length, y: vector.y / length, z: vector.z / length };
}
