import test from 'node:test';
import assert from 'node:assert/strict';

import { buildPhysicalWorldGeometry, SURFACE_WATER_PRESENTATION_OFFSET_METERS } from '../src/physical-world-renderer.ts';
import { SurfaceWaterKind, TerrainMaterialKind, WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE } from '../src/world-environment-protocol.ts';

test('surface water presentation is lifted above authoritative terrain coordinates', () => {
  const model = buildPhysicalWorldGeometry({
    type: WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE,
    tickCount: 1n,
    config: {
      worldSeed: 1n, geographicNorthX: 0, geographicNorthY: 1, latitudeDegrees: 45, hemisphere: 0,
      seaLevelMeters: 0, continentality: 0.5, maritimeInfluence: 0.5, meanAnnualTemperatureCelsius: 10,
      seasonalityCelsius: 20, annualPrecipitationMillimeters: 900, configuredCoastlineDistanceMeters: 0,
      hasConfiguredCoastlineDistance: false, globalScaleMeters: 250000, terrainDetailScaleMeters: 512,
    },
    minX: 0, minY: 0, minZ: -10, maxX: 100, maxY: 100, maxZ: 100,
    samples: [],
    terrainSamples: [{
      x: 10, y: 30, z: 20, normalX: 0, normalY: 0, normalZ: 1, slopeDegrees: 0, roughness: 0.1,
      material: TerrainMaterialKind.Water, surfaceWater: SurfaceWaterKind.Lake,
    }],
    features: [],
    toponyms: [],
  });

  assert.equal(model.water.length, 1);
  assert.deepEqual([...model.water[0].positions], [10, 20 + SURFACE_WATER_PRESENTATION_OFFSET_METERS, 30]);
});
