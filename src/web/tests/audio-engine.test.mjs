import test from 'node:test';
import assert from 'node:assert/strict';

import { AudioEngine, resolveAudioListenerPose, resolveMasterGain } from '../src/audio-engine.ts';

test('AudioEngine falls back when Web Audio API is unavailable', async () => {
  const engine = new AudioEngine();
  assert.equal(engine.state, 'unavailable');
  assert.equal(await engine.unlock(), false);
});

test('master mixer gain respects mute and clamps volume', () => {
  assert.equal(resolveMasterGain(false, 0.6), 0.6);
  assert.equal(resolveMasterGain(false, 2), 1);
  assert.equal(resolveMasterGain(true, 0.6), 0);
});

test('audio gain boundaries reject non-finite values', () => {
  const engine = new AudioEngine();
  assert.throws(() => resolveMasterGain(false, Number.NaN), RangeError);
  assert.throws(() => resolveMasterGain(true, Number.POSITIVE_INFINITY), RangeError);
  assert.throws(() => engine.setMasterVolume(Number.NaN), RangeError);
  assert.throws(() => engine.setCategoryVolume('world', Number.NEGATIVE_INFINITY), RangeError);
});

test('listener pose uses the ground-plane position and orthogonal camera basis', () => {
  const pose = resolveAudioListenerPose({
    matrixWorld: {
      elements: [
        1, 0, 0, 0,
        0, 0, -1, 0,
        0, 1, 0, 0,
        12, 500, 34, 1,
      ],
    },
  });

  assert.deepEqual(pose?.position, { x: 12, y: 0, z: 34 });
  assert.equal(Math.abs(pose?.direction.x ?? Number.NaN), 0);
  assert.equal(pose?.direction.y, -1);
  assert.equal(Math.abs(pose?.direction.z ?? Number.NaN), 0);
  assert.deepEqual(pose?.up, { x: 0, y: 0, z: -1 });
  const dot = (pose?.direction.x ?? 0) * (pose?.up.x ?? 0) +
    (pose?.direction.y ?? 0) * (pose?.up.y ?? 0) +
    (pose?.direction.z ?? 0) * (pose?.up.z ?? 0);
  assert.equal(Math.abs(dot), 0);
});

test('listener pose rejects non-finite camera matrix values', () => {
  const elements = [
    1, 0, 0, 0,
    0, 0, -1, 0,
    0, 1, 0, 0,
    12, 500, 34, 1,
  ];
  elements[12] = Number.NaN;

  assert.equal(resolveAudioListenerPose({ matrixWorld: { elements } }), null);
});
