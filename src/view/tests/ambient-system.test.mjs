import test from 'node:test';
import assert from 'node:assert/strict';

import { AmbientSystem } from '../src/ambient-system.ts';

const audioStub = {
  async setAmbientLayer() {},
  async clearAmbientLayer() {},
};

test('AmbientSystem rejects non-finite configuration values at its public boundary', () => {
  const ambient = new AmbientSystem(audioStub);

  assert.throws(
    () => ambient.setGlobalLayers([{ key: 'bad', cueId: 'ambient.bad', gain: Number.NaN }]),
    RangeError,
  );
  assert.throws(
    () => ambient.setZones([{
      id: 'bad', minX: 0, minY: 0, minZ: 0, maxX: 1, maxY: 1, maxZ: 1,
      priority: Number.POSITIVE_INFINITY, fadeDistance: 0, layers: [],
    }]),
    RangeError,
  );
  assert.throws(
    () => ambient.setParameters({ rain: Number.NEGATIVE_INFINITY }),
    RangeError,
  );
});

test('AmbientSystem rejects a non-finite listener before touching audio', async () => {
  const ambient = new AmbientSystem(audioStub);

  await assert.rejects(
    ambient.update({ x: Number.NaN, y: 0, z: 0 }),
    RangeError,
  );
});

test('AmbientSystem rolls back layers that started before a later layer fails', async () => {
  const active = new Set();
  const calls = [];
  const audio = {
    async setAmbientLayer(key, cueId) {
      calls.push(`set:${key}`);
      if (cueId === 'ambient.fail') {
        throw new Error('failed to start ambient layer');
      }
      active.add(key);
    },
    async clearAmbientLayer(key) {
      calls.push(`clear:${key}`);
      active.delete(key);
    },
  };
  const ambient = new AmbientSystem(audio);
  ambient.setGlobalLayers([
    { key: 'a', cueId: 'ambient.ok', gain: 1 },
    { key: 'b', cueId: 'ambient.fail', gain: 1 },
  ]);

  await assert.rejects(
    ambient.update({ x: 0, y: 0, z: 0 }),
    /failed to start ambient layer/,
  );

  assert.deepEqual([...active], []);
  assert.deepEqual(calls, ['set:a', 'set:b', 'clear:b', 'clear:a']);

  ambient.setGlobalLayers([]);
  await ambient.update({ x: 0, y: 0, z: 0 });
  assert.deepEqual([...active], []);
});

test('AmbientSystem restores the previous cue when the failing layer already caused side effects', async () => {
  const active = new Map();
  const calls = [];
  const audio = {
    async setAmbientLayer(key, cueId) {
      calls.push(`set:${key}:${cueId}`);
      if (cueId === 'ambient.new') {
        active.delete(key);
        throw new Error('failed after stopping previous cue');
      }
      active.set(key, cueId);
    },
    async clearAmbientLayer(key) {
      calls.push(`clear:${key}`);
      active.delete(key);
    },
  };
  const ambient = new AmbientSystem(audio);
  ambient.setGlobalLayers([{ key: 'station', cueId: 'ambient.old', gain: 1 }]);
  await ambient.update({ x: 0, y: 0, z: 0 });
  calls.length = 0;

  ambient.setGlobalLayers([{ key: 'station', cueId: 'ambient.new', gain: 1 }]);
  await assert.rejects(
    ambient.update({ x: 0, y: 0, z: 0 }),
    /failed after stopping previous cue/,
  );

  assert.equal(active.get('station'), 'ambient.old');
  assert.deepEqual(calls, [
    'set:station:ambient.new',
    'set:station:ambient.old',
  ]);
});
