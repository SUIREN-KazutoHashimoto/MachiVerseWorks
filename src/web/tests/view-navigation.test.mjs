import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import {
  ViewNavigationController,
  createEntityNavigationTarget,
  createStaticNavigationTarget,
  getCameraFocusAtSimulationAltitude,
} from '../src/view-navigation.ts';

class FakeSurface {
  listeners = new Map();
  clientWidth = 800;
  clientHeight = 600;
  addEventListener(type, listener) { this.listeners.set(type, listener); }
  removeEventListener(type, listener) { if (this.listeners.get(type) === listener) this.listeners.delete(type); }
  setPointerCapture() {}
  emit(type, event) {
    const listener = this.listeners.get(type);
    if (listener) listener(event);
  }
}

function createCamera() {
  const camera = new THREE.PerspectiveCamera(55, 16 / 9, 0.1, 50_000);
  camera.position.set(0, 500, 0);
  camera.lookAt(0, 0, -250);
  camera.updateProjectionMatrix();
  camera.updateMatrixWorld(true);
  return camera;
}

test('jump centers a remote target without depending on distance from the world origin', () => {
  const camera = createCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const target = createStaticNavigationTarget('settlement', 'remote-town', { x: 1_000_000_000, y: -2_000_000_000, z: 75 });

  assert.equal(navigation.jump(target, 0), true);
  const focus = getCameraFocusAtSimulationAltitude(camera, 75);
  assert.ok(focus !== undefined);
  assert.ok(Math.abs(focus.x - 1_000_000_000) < 1e-4);
  assert.ok(Math.abs(focus.y + 2_000_000_000) < 1e-4);
  assert.equal(focus.z, 75);
});

test('focus maps legacy preferred zoom to perspective camera distance', () => {
  const camera = createCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const target = createStaticNavigationTarget('geographic-feature', 'ridge', { x: 250, y: -400, z: 120 }, 5);

  assert.equal(navigation.focus(target, 0), true);
  const targetThree = new THREE.Vector3(250, 120, -400);
  assert.ok(Math.abs(camera.position.distanceTo(targetThree) - 50) < 1e-6);
  const focus = getCameraFocusAtSimulationAltitude(camera, 120);
  assert.ok(focus !== undefined);
  assert.ok(Math.abs(focus.x - 250) < 1e-6);
  assert.ok(Math.abs(focus.y + 400) < 1e-6);
});

test('mouse look changes yaw and pitch while clamping before inversion', () => {
  const camera = createCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const before = new THREE.Vector3();
  camera.getWorldDirection(before);

  navigation.lookBy(100, -100_000);
  const after = new THREE.Vector3();
  camera.getWorldDirection(after);

  assert.notEqual(after.x, before.x);
  assert.ok(after.y < 1);
  assert.ok(after.y > 0.99);
  navigation.lookBy(0, 200_000);
  camera.getWorldDirection(after);
  assert.ok(after.y > -1);
  assert.ok(after.y < -0.99);
});

test('WASD movement follows camera basis and Shift multiplies movement speed', () => {
  const camera = createCamera();
  camera.position.set(0, 100, 0);
  camera.lookAt(0, 100, -100);
  camera.updateMatrixWorld(true);
  const navigation = new ViewNavigationController(camera, new FakeSurface());

  navigation.setKeyState('KeyW', true);
  navigation.update(0);
  navigation.update(100);
  assert.ok(Math.abs(camera.position.z + 4) < 1e-6);

  navigation.setKeyState('ShiftLeft', true);
  navigation.update(200);
  assert.ok(Math.abs(camera.position.z + 20) < 1e-6);

  navigation.setKeyState('KeyW', false);
  navigation.setKeyState('ShiftLeft', false);
  navigation.setKeyState('KeyD', true);
  navigation.update(300);
  assert.ok(Math.abs(camera.position.x - 4) < 1e-6);
});

test('vertical movement supports E/Q and enforces minimum camera height', () => {
  const camera = createCamera();
  camera.position.set(0, 2, 0);
  const navigation = new ViewNavigationController(camera, new FakeSurface());

  navigation.setKeyState('KeyQ', true);
  navigation.update(0);
  navigation.update(100);
  assert.equal(camera.position.y, 1.7);

  navigation.setKeyState('KeyQ', false);
  navigation.setKeyState('KeyE', true);
  navigation.update(200);
  assert.ok(camera.position.y > 1.7);
});

test('wheel changes free movement speed and follow distance by mode', () => {
  const camera = createCamera();
  const surface = new FakeSurface();
  const navigation = new ViewNavigationController(camera, surface);
  const stop = () => {};
  const wheel = (deltaY) => surface.emit('wheel', {
    deltaY,
    preventDefault: stop,
    stopImmediatePropagation: stop,
  });

  const speed = navigation.moveSpeed;
  wheel(-100);
  assert.ok(navigation.moveSpeed > speed);

  const target = createStaticNavigationTarget('position', undefined, { x: 10, y: 20, z: 0 });
  assert.equal(navigation.follow(target, 0), true);
  const followDistance = navigation.followDistance;
  wheel(-100);
  assert.ok(navigation.followDistance > followDistance);
});

test('entity follow uses allocation-free position writes, orbits the target, and tracks later samples', () => {
  const camera = createCamera();
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
  assert.ok(Math.abs(focus.x - 500) < 1e-6);
  assert.ok(Math.abs(focus.y + 750) < 1e-6);
  assert.ok(Math.abs(navigation.followDistance - 12) < 1e-9);
});

test('clearing follow preserves the current camera orientation for free flight', () => {
  const camera = createCamera();
  const navigation = new ViewNavigationController(camera, new FakeSurface());
  const target = createStaticNavigationTarget('position', undefined, { x: 0, y: 0, z: 0 });

  assert.equal(navigation.follow(target, 0), true);
  navigation.lookBy(80, -20);
  navigation.update(16);
  const before = new THREE.Vector3();
  camera.getWorldDirection(before);

  navigation.clearFollow();
  navigation.update(32);
  const after = new THREE.Vector3();
  camera.getWorldDirection(after);

  assert.ok(before.distanceTo(after) < 1e-9);
});

test('failed follow does not retain a latent target that can activate later', () => {
  const camera = createCamera();
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
  const before = camera.position.clone();
  available = true;
  navigation.update(100);
  assert.ok(camera.position.distanceTo(before) < 1e-12);
});
