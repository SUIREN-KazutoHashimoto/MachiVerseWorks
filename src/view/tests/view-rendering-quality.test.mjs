import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';

import {
  DEFAULT_VIEW_RENDERING_QUALITY,
  installEnvironmentLighting,
  updateEnvironmentFog,
} from '../src/view-rendering-quality.ts';

test('VQ-1 quality profile keeps fog and shadow settings View-local and bounded', () => {
  const quality = DEFAULT_VIEW_RENDERING_QUALITY;
  assert.ok(quality.fogNear > 0);
  assert.ok(quality.fogFar > quality.fogNear);
  assert.ok(quality.fogScaleReferenceAltitude > 0);
  assert.ok(quality.exposure > 0);
  assert.equal(quality.shadowMapSize, 2_048);
  assert.ok(quality.shadowDistance >= 3_000);
});

test('VQ-1 environment installs daylight background, depth fog and a shadowed sun', () => {
  const scene = new THREE.Scene();
  const lights = installEnvironmentLighting(scene);

  assert.ok(scene.background instanceof THREE.Color);
  assert.ok(scene.fog instanceof THREE.Fog);
  assert.equal(scene.fog.near, DEFAULT_VIEW_RENDERING_QUALITY.fogNear);
  assert.equal(scene.fog.far, DEFAULT_VIEW_RENDERING_QUALITY.fogFar);
  assert.equal(lights.sun.castShadow, true);
  assert.equal(lights.sun.shadow.mapSize.x, DEFAULT_VIEW_RENDERING_QUALITY.shadowMapSize);
  assert.equal(lights.sun.shadow.mapSize.y, DEFAULT_VIEW_RENDERING_QUALITY.shadowMapSize);
  assert.ok(scene.children.includes(lights.hemisphere));
  assert.ok(scene.children.includes(lights.sun));
});

test('VQ-1 fog expands with wide observation altitude instead of washing out the world', () => {
  const scene = new THREE.Scene();
  installEnvironmentLighting(scene);
  const camera = new THREE.PerspectiveCamera(55, 16 / 9, 0.1, 5_000_000);
  camera.position.y = 700_000;

  updateEnvironmentFog(scene, camera);

  assert.ok(scene.fog instanceof THREE.Fog);
  assert.ok(scene.fog.near > 500_000);
  assert.ok(scene.fog.far > 3_000_000);
});
