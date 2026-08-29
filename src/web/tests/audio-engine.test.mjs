import test from 'node:test';
import assert from 'node:assert/strict';

import { AudioEngine, resolveMasterGain } from '../src/audio-engine.ts';

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
