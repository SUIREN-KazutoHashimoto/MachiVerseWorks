import test from 'node:test';
import assert from 'node:assert/strict';

import { WorldEnvironmentStore } from '../src/world-environment-store.ts';
import { GeographicFeatureType, SurfaceWaterKind, TerrainMaterialKind, ToponymProvenanceKind, WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE } from '../src/world-environment-protocol.ts';

test('WorldEnvironmentStore indexes authoritative features and toponyms and clears atomically', () => {
  const store = new WorldEnvironmentStore();
  const snapshot = createSnapshot();

  store.replace(snapshot);
  assert.equal(store.revision, 1);
  assert.equal(store.snapshot, snapshot);
  assert.equal(store.getFeature(11n)?.featureType, GeographicFeatureType.Valley);
  assert.equal(store.getToponymForFeature(11n)?.name, 'Test Valley');

  store.clear();
  assert.equal(store.revision, 2);
  assert.equal(store.snapshot, null);
  assert.equal(store.getFeature(11n), undefined);
  assert.equal(store.getToponymForFeature(11n), undefined);
});

test('WorldEnvironmentStore keeps the latest tick without invalidating unchanged rendering content', () => {
  const store = new WorldEnvironmentStore();
  store.replace(createSnapshot());
  const nextTick = createSnapshot({ tickCount: 2n });

  store.replace(nextTick);

  assert.equal(store.revision, 1);
  assert.equal(store.snapshot, nextTick);
  assert.equal(store.snapshot?.tickCount, 2n);
});

test('WorldEnvironmentStore invalidates rendering when physical presentation content changes', () => {
  const store = new WorldEnvironmentStore();
  store.replace(createSnapshot());
  const changed = createSnapshot({
    tickCount: 2n,
    terrainSamples: [{
      x: 0, y: 0, z: 12, normalX: 0, normalY: 0, normalZ: 1, slopeDegrees: 0, roughness: 0.1,
      material: TerrainMaterialKind.Soil, surfaceWater: SurfaceWaterKind.None,
    }],
  });

  store.replace(changed);

  assert.equal(store.revision, 2);
  assert.equal(store.snapshot, changed);
});

function createSnapshot(overrides = {}) {
  return {
    type: WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 1n,
    config: {
      worldSeed: 1n, geographicNorthX: 0, geographicNorthY: 1, latitudeDegrees: 45, hemisphere: 0,
      seaLevelMeters: 0, continentality: 0.5, maritimeInfluence: 0.5, meanAnnualTemperatureCelsius: 10,
      seasonalityCelsius: 20, annualPrecipitationMillimeters: 900, configuredCoastlineDistanceMeters: 0,
      hasConfiguredCoastlineDistance: false, globalScaleMeters: 250000, terrainDetailScaleMeters: 512,
    },
    minX: 0, minY: 0, minZ: -10, maxX: 100, maxY: 100, maxZ: 100,
    samples: [], terrainSamples: [],
    features: [{
      featureId: 11n, featureType: GeographicFeatureType.Valley, minX: 0, minY: 0, minZ: 0, maxX: 100, maxY: 100, maxZ: 20,
      areaSquareMeters: 10000, parentFeatureId: 0n, minimumElevationMeters: 0, maximumElevationMeters: 20,
      geometry: [{ x: 0, y: 0, z: 5 }, { x: 100, y: 100, z: 6 }],
    }],
    toponyms: [{
      toponymId: 21n, featureId: 11n, name: 'Test Valley', provenanceKind: ToponymProvenanceKind.GeneratedNaturalFeature,
      sourceFeatureId: 11n, parentToponymId: 0n, generatorKey: 'test',
    }],
    ...overrides,
  };
}
