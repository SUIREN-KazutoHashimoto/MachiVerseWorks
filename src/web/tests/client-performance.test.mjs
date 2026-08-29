import assert from 'node:assert/strict';
import test from 'node:test';

import { ClientPerformanceMetrics } from '../src/client-performance.ts';

test('records decode bytes and rolling timing statistics', () => {
  const metrics = new ClientPerformanceMetrics();
  metrics.recordDecode(64, 1.5);
  metrics.recordDecode(80, 2.5);

  const snapshot = metrics.snapshot();
  assert.equal(snapshot.decodeSampleCount, 2);
  assert.equal(snapshot.decodedBytes, 144);
  assert.equal(snapshot.decodeAverageMs, 2);
  assert.equal(snapshot.decodeP95Ms, 2.5);
  assert.equal(snapshot.decodeMaximumMs, 2.5);
});

test('records animation frame intervals after the first timestamp', () => {
  const metrics = new ClientPerformanceMetrics();
  metrics.recordAnimationFrame(100);
  metrics.recordAnimationFrame(116);
  metrics.recordAnimationFrame(134);

  const snapshot = metrics.snapshot();
  assert.equal(snapshot.frameSampleCount, 2);
  assert.equal(snapshot.frameAverageMs, 17);
  assert.equal(snapshot.frameP95Ms, 18);
  assert.equal(snapshot.frameMaximumMs, 18);
});

test('uses a bounded rolling timing window', () => {
  const metrics = new ClientPerformanceMetrics();
  for (let index = 0; index < 300; index += 1) {
    metrics.recordDecode(1, index);
  }

  const snapshot = metrics.snapshot();
  assert.equal(snapshot.decodeSampleCount, 300);
  assert.equal(snapshot.decodeAverageMs, 179.5);
  assert.equal(snapshot.decodeMaximumMs, 299);
});
