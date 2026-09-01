import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const sourceUrl = (name) => new URL(`../src/${name}`, import.meta.url);

test('View transport exposes observation and inspection requests only', async () => {
  const source = await readFile(sourceUrl('connection.ts'), 'utf8');
  const publicMethods = [...source.matchAll(/public\s+([A-Za-z0-9_]+)\s*\(/g)].map((match) => match[1]);

  assert.deepEqual(
    publicMethods,
    ['connect', 'disconnect', 'setSubscription', 'inspectPerson', 'clearPersonInspection'],
    'Adding a transport API requires an explicit read-only boundary review.',
  );
});

test('Application cannot bypass ViewObservationState for core observation mutation', async () => {
  const source = await readFile(sourceUrl('application.ts'), 'utf8');
  const forbidden = [
    'observation.entities.spawn',
    'observation.entities.update',
    'observation.entities.remove',
    'observation.entities.clear',
    'observation.pedestrians.spawn',
    'observation.pedestrians.update',
    'observation.pedestrians.remove',
    'observation.vehicles.spawn',
    'observation.vehicles.update',
    'observation.vehicles.remove',
    'observation.intersections.apply',
    'observation.roadNetwork.replace',
  ];

  for (const token of forbidden) assert.equal(source.includes(token), false, `${token} bypasses the View state ingress.`);
  assert.equal(source.includes('this.observation.apply(message)'), true);
  assert.equal(source.includes('this.observation.resetConnectionState()'), true);
});
