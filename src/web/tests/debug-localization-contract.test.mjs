import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const overlays = ['logistics-debug.ts', 'power-debug.ts', 'optical-debug.ts', 'radio-debug.ts'];

test('infrastructure debug overlays receive Localizer and avoid hard-coded waiting text', async () => {
  for (const file of overlays) {
    const source = await readFile(new URL(`../src/${file}`, import.meta.url), 'utf8');
    assert.match(source, /Localizer/);
    assert.match(source, /localizer\.t\('/);
    assert.match(source, /localizer\.formatNumber\(/);
    assert.doesNotMatch(source, /waiting for snapshot/);
  }
  const application = await readFile(new URL('../src/application.ts', import.meta.url), 'utf8');
  for (const name of ['LogisticsDebugOverlay', 'PowerDebugOverlay', 'OpticalDebugOverlay', 'RadioDebugOverlay']) {
    assert.match(application, new RegExp(`new ${name}\\(host, this\\.localizer\\)`));
  }
});

test('Japanese locale defines every infrastructure debug key', async () => {
  const resource = JSON.parse(await readFile(new URL('../locales/ja-JP.json', import.meta.url), 'utf8'));
  for (const prefix of ['logisticsDebug.', 'powerDebug.', 'opticalDebug.', 'radioDebug.']) {
    assert.ok(Object.keys(resource).some((key) => key.startsWith(prefix)), `missing ${prefix}`);
  }
});
