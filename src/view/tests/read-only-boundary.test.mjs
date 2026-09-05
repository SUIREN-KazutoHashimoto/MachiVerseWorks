import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const sourceUrl = (name) => new URL(`../src/${name}`, import.meta.url);

test('View transport exposes observation and inspection requests only', async () => {
  const source = await readFile(sourceUrl('connection.ts'), 'utf8');
  const publicMethods = [...source.matchAll(/public\s+([A-Za-z0-9_]+)\s*\(/g)]
    .map((match) => match[1])
    .filter((name) => name !== 'constructor');

  assert.deepEqual(
    publicMethods,
    ['connect', 'disconnect', 'setSubscription', 'inspectPerson', 'clearPersonInspection'],
    'Adding a transport API requires an explicit read-only boundary review.',
  );
});

test('ViewObservationState exposes read-only stores and keeps mutable stores private', async () => {
  const source = await readFile(sourceUrl('view-observation-state.ts'), 'utf8');
  const expectedGetters = [
    'public get entities(): ReadonlyEntityStore',
    'public get pedestrians(): ReadonlyPedestrianStore',
    'public get vehicles(): ReadonlyVehicleStore',
    'public get intersections(): ReadonlyIntersectionControlStore',
    'public get roadNetwork(): ReadonlyRoadNetworkStore',
  ];

  for (const getter of expectedGetters) assert.equal(source.includes(getter), true, `${getter} must remain read-only.`);
  for (const name of ['entityStore', 'pedestrianStore', 'vehicleStore', 'intersectionStore', 'roadNetworkStore']) {
    assert.equal(source.includes(`private readonly ${name}`), true, `${name} must remain private.`);
  }
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
    'view.applyRoadNetwork',
  ];

  for (const token of forbidden) assert.equal(source.includes(token), false, `${token} bypasses the View state ingress.`);
  assert.equal(source.includes('this.observation.apply(message)'), true);
  assert.equal(source.includes('this.observation.resetConnectionState()'), true);
});

test('WorldView renders the shared read-only road observation without a duplicate store', async () => {
  const source = await readFile(sourceUrl('world-view.ts'), 'utf8');
  assert.equal(source.includes('new RoadNetworkStore'), false);
  assert.equal(source.includes('ReadonlyRoadNetworkStore'), true);
  assert.equal(source.includes('this.roadRenderer.update(roadNetwork)'), true);
});

test('dynamic render buffers use allocation-free interpolation writes', async () => {
  const sources = await Promise.all([
    readFile(sourceUrl('entity-store.ts'), 'utf8'),
    readFile(sourceUrl('pedestrian-store.ts'), 'utf8'),
    readFile(sourceUrl('traffic-store.ts'), 'utf8'),
  ]);
  for (const source of sources) assert.equal(source.includes('interpolation.writeSampledPosition('), true);
});
