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
  left = -400;
  right = 400;
  top = 300;
  bottom = -300;
  near = 0.1;
  far = 2_000;
  matrixWorld = { elements: new Float64Array(16) };
  direction = normalize({ x: 0, y: -500, z: -250 });

  constructor() { this.updateMatrixWorld(true); }
  lookAt(x, y, z) {
    this.direction = normalize({ x: x - this.position.x, y: y - this.position.y, z: z - this.position.z });
    this.updateMatrixWorld(true);
  }
  updateMatrixWorld() {
    const right = normalize(cross(this.direction, { x: 0, y: 1, z: 0 }));
    const up = normalize(cross(right, this.direction));
    const elements = this.matrixWorld.elements;
    elements[0] = right.x; elements[1] = right.y; elements[2] = right.z;
    elements[4] = up.x; elements[5] = up.y; elements[6] = up.z;
    elements[8] = -this.direction.x; elements[9] = -this.direction.y; elements[10] = -this.direction.z;
  }
  updateProjectionMatrix() {}
}

class FakeSurface {
  listeners = new Map();
  clientWidth = 800;
  clientHeight = 600;
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

test('rotate keeps pitch and pan follows camera-local screen axes', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const beforeVertical = camera.direction.y;

  navigation.rotateBy(Math.PI / 2);
  const beforePanX = camera.position.x;
  const beforePanZ = camera.position.z;
  navigation.pan(100, 0);

  assert.ok(Math.abs(camera.direction.y - beforeVertical) < 1e-12);
  assert.ok(Math.abs(camera.direction.x) > 0.1);
  assert.ok(Math.abs(camera.position.x - beforePanX) < 1e-9);
  assert.ok(Math.abs(camera.position.z - beforePanZ) > 99);
});

test('altitude remains within the near/far visibility range of the observation plane', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());

  navigation.adjustAltitude(100_000);
  const highFocus = getCameraFocusAtSimulationAltitude(camera, 0);
  assert.ok(highFocus !== undefined);
  const highDistance = distance3(camera.position, { x: highFocus.x, y: highFocus.z, z: highFocus.y });
  assert.ok(highDistance < camera.far);

  navigation.adjustAltitude(-100_000);
  const lowFocus = getCameraFocusAtSimulationAltitude(camera, 0);
  assert.ok(lowFocus !== undefined);
  const lowDistance = distance3(camera.position, { x: lowFocus.x, y: lowFocus.z, z: lowFocus.y });
  assert.ok(lowDistance > camera.near);
  assert.ok(camera.position.y > 0);
});

test('entity follow uses allocation-free position writes and tracks later samples', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const position = new Float64Array([10, 20, 0]);
  const store = {
    size: 1,
    writeSampledPositions: () => 0,
    writeSampledPositionById: (agentId, _now, target, offset = 0) => {
      if (agentId !== 7n) return false;
      target[offset] = position[0]; target[offset + 1] = position[1]; target[offset + 2] = position[2];
      return true;
    },
    sample: function* () {},
    sampleById: () => { throw new Error('follow hot path must not allocate a sampled agent object'); },
  };
  const target = createEntityNavigationTarget(7n, store, 4);

  assert.equal(navigation.follow(target, 0), true);
  position[0] = 500; position[1] = -750; position[2] = 40;
  navigation.update(100);

  const focus = getCameraFocusAtSimulationAltitude(camera, 40);
  assert.ok(focus !== undefined);
  assert.ok(Math.abs(focus.x - 500) < 1e-9);
  assert.ok(Math.abs(focus.y + 750) < 1e-9);
  assert.equal(camera.zoom, 4);
});

test('failed follow does not retain a latent target that can activate later', () => {
  const camera = new FakeCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  let available = false;
  const target = {
    kind: 'entity',
    id: 9n,
    writePosition: (_now, output) => {
      if (!available) return false;
      output[0] = 900; output[1] = 800; output[2] = 0;
      return true;
    },
  };

  assert.equal(navigation.follow(target, 0), false);
  const before = { ...camera.position };
  available = true;
  navigation.update(100);
  assert.deepEqual(camera.position, before);
});

function normalize(vector) {
  const length = Math.hypot(vector.x, vector.y, vector.z);
  return { x: vector.x / length, y: vector.y / length, z: vector.z / length };
}

function cross(left, right) {
  return {
    x: left.y * right.z - left.z * right.y,
    y: left.z * right.x - left.x * right.z,
    z: left.x * right.y - left.y * right.x,
  };
}

function distance3(left, right) {
  return Math.hypot(left.x - right.x, left.y - right.y, left.z - right.z);
}
