import test from 'node:test';
import assert from 'node:assert/strict';

import { resolveAmbientLayers, selectVoiceIds } from '../src/audio-policy.ts';

test('voice budget prefers priority, then nearest emitters', () => {
  const selected = selectVoiceIds([
    { id: 'far', position: { x: 100, y: 0 }, priority: 0 },
    { id: 'near', position: { x: 1, y: 0 }, priority: 0 },
    { id: 'priority', position: { x: 500, y: 0 }, priority: 10 },
  ], { x: 0, y: 0 }, 2);

  assert.deepEqual([...selected].sort(), ['near', 'priority']);
});

test('voice selection rejects non-finite position and priority values', () => {
  assert.throws(
    () => selectVoiceIds([{ id: 'bad', position: { x: Number.NaN, y: 0 }, priority: 0 }], { x: 0, y: 0 }, 1),
    RangeError,
  );
  assert.throws(
    () => selectVoiceIds([{ id: 'bad', position: { x: 0, y: 0 }, priority: Number.POSITIVE_INFINITY }], { x: 0, y: 0 }, 1),
    RangeError,
  );
});

test('ambient zones blend with priority, edge fade, and external parameters', () => {
  const mix = resolveAmbientLayers(
    [{ key: 'wind', cueId: 'ambient.wind', gain: 0.2 }],
    [{
      id: 'station',
      minX: 0,
      minY: 0,
      maxX: 100,
      maxY: 100,
      priority: 5,
      fadeDistance: 20,
      layers: [
        { key: 'station', cueId: 'ambient.station', gain: 1 },
        { key: 'rain', cueId: 'ambient.rain', gain: 1, parameter: 'rain' },
      ],
    }],
    { x: 10, y: 50 },
    { rain: 0.5 },
  );

  assert.deepEqual(mix, [
    { key: 'rain', cueId: 'ambient.rain', gain: 0.25 },
    { key: 'station', cueId: 'ambient.station', gain: 0.5 },
    { key: 'wind', cueId: 'ambient.wind', gain: 0.2 },
  ]);
});

test('ambient policy rejects non-finite gain, priority, parameter, and listener values', () => {
  assert.throws(
    () => resolveAmbientLayers([{ key: 'wind', cueId: 'ambient.wind', gain: Number.NaN }], [], { x: 0, y: 0 }),
    RangeError,
  );
  assert.throws(
    () => resolveAmbientLayers([], [{
      id: 'bad', minX: 0, minY: 0, maxX: 1, maxY: 1,
      priority: Number.POSITIVE_INFINITY, fadeDistance: 0, layers: [],
    }], { x: 0, y: 0 }),
    RangeError,
  );
  assert.throws(
    () => resolveAmbientLayers([], [], { x: 0, y: 0 }, { rain: Number.NEGATIVE_INFINITY }),
    RangeError,
  );
  assert.throws(
    () => resolveAmbientLayers([], [], { x: Number.NaN, y: 0 }),
    RangeError,
  );
});
