import test from 'node:test';
import assert from 'node:assert/strict';

import { AgentCountFormatter } from '../src/ui.ts';

test('AgentCountFormatter only formats when the count changes', () => {
  const formatter = new AgentCountFormatter('en-US');

  assert.equal(formatter.formatIfChanged(1000), '1,000');
  assert.equal(formatter.formatIfChanged(1000), null);
  assert.equal(formatter.formatIfChanged(1001), '1,001');
  assert.equal(formatter.formatIfChanged(1001), null);
});
