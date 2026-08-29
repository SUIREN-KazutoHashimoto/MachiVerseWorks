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
      id: 'bad', minX: 0, minY: 0, maxX: 1, maxY: 1,
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
    ambient.update({ x: Number.NaN, y: 0 }),
    RangeError,
  );
});
