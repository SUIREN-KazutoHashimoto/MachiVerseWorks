import test from 'node:test';
import assert from 'node:assert/strict';

import { createPrimaryTerrainColumns, triangulateTerrainSurface } from '../src/terrain-geometry.ts';
import { SurfaceWaterKind, TerrainMaterialKind } from '../src/world-environment-protocol.ts';

test('primary terrain observations become a 3D surface without flattening elevation', () => {
  const columns = createPrimaryTerrainColumns([
    sample(0, 0, 10), sample(100, 0, 20), sample(0, 100, 30), sample(100, 100, 40),
  ]);
  const mesh = triangulateTerrainSurface(columns);

  assert.equal(mesh.vertexCount, 4);
  assert.equal(mesh.triangleCount, 2);
  assert.deepEqual([...mesh.positions], [0, 10, 0, 100, 20, 0, 0, 30, 100, 100, 40, 100]);
  assert.deepEqual([...mesh.indices], [0, 2, 1, 1, 2, 3]);
});

test('geometry boundary can consume a separate cavity surface layer without deriving it', () => {
  const columns = [
    column(0, 0, 5, -20), column(100, 0, 6, -21), column(0, 100, 7, -22), column(100, 100, 8, -23),
  ];
  const cavity = triangulateTerrainSurface(columns, 'cavity-boundary', 0);

  assert.equal(cavity.vertexCount, 4);
  assert.equal(cavity.triangleCount, 2);
  assert.deepEqual([...cavity.positions], [0, -20, 0, 100, -21, 0, 0, -22, 100, 100, -23, 100]);
});

function sample(x, y, z) {
  return { x, y, z, normalX: 0, normalY: 0, normalZ: 1, slopeDegrees: 0, roughness: 0.1, material: TerrainMaterialKind.Soil, surfaceWater: SurfaceWaterKind.None };
}

function column(x, y, groundZ, caveZ) {
  return {
    x,
    y,
    surfaces: [
      { x, y, z: groundZ, normalX: 0, normalY: 0, normalZ: 1, material: TerrainMaterialKind.Soil, surfaceWater: SurfaceWaterKind.None, role: 'primary-ground', layer: 0 },
      { x, y, z: caveZ, normalX: 0, normalY: 0, normalZ: -1, material: TerrainMaterialKind.Rock, surfaceWater: SurfaceWaterKind.None, role: 'cavity-boundary', layer: 0 },
    ],
  };
}
