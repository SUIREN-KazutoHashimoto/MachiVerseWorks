import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

test('Application guards a single animation loop and cannot reschedule after dispose', async () => {
  const source = await readFile(new URL('../src/application.ts', import.meta.url), 'utf8');
  assert.match(source, /if \(this\.started\) return;/);
  assert.match(source, /this\.started = false; this\.disposed = true;/);
  assert.match(source, /if \(this\.disposed \|\| !this\.started\) return;/);
});
