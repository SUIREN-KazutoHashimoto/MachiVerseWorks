import test from 'node:test';
import assert from 'node:assert/strict';

import { EntityEmitterIndex } from '../src/entity-emitter-index.ts';

test('EntityEmitterIndex keeps one-to-many emitter links isolated per entity', () => {
  const index = new EntityEmitterIndex();

  index.add(1n, 'a');
  index.add(1n, 'b');
  index.add(2n, 'c');

  assert.deepEqual([...index.get(1n)], ['a', 'b']);
  assert.deepEqual([...index.get(2n)], ['c']);
  assert.equal(index.has(3n), false);

  index.remove(1n, 'a');
  assert.deepEqual([...index.get(1n)], ['b']);
  assert.equal(index.has(2n), true);

  index.remove(1n, 'b');
  assert.equal(index.get(1n), undefined);
  assert.equal(index.has(1n), false);
  assert.deepEqual([...index.get(2n)], ['c']);

  index.clear();
  assert.equal(index.has(2n), false);
});
