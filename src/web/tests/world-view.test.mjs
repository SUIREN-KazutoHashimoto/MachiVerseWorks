import test from 'node:test';
import assert from 'node:assert/strict';

import { simulationToThreePosition } from '../src/world-view.ts';

test('Simulation XYZ maps horizontal Y to Three Z and altitude Z to Three Y', () => {
  const position = simulationToThreePosition(12, 34, 56);

  assert.equal(position.x, 12);
  assert.equal(position.y, 56);
  assert.equal(position.z, 34);
});
