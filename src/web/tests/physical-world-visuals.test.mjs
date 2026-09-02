import test from 'node:test';
import assert from 'node:assert/strict';

import {
  resolveGeographicFeatureVisual,
  resolveSurfaceWaterVisual,
  resolveTerrainMaterialVisual,
} from '../src/physical-world-visuals.ts';
import {
  GeographicFeatureType,
  SurfaceWaterKind,
  TerrainMaterialKind,
} from '../src/world-environment-protocol.ts';

test('every authoritative terrain material has an explicit visual mapping', () => {
  const labels = [];
  for (let kind = TerrainMaterialKind.Water; kind <= TerrainMaterialKind.Gravel; kind += 1) labels.push(resolveTerrainMaterialVisual(kind).label);
  assert.deepEqual(labels, ['water', 'sand', 'soil', 'rock', 'snow', 'gravel']);
});

test('surface water mapping preserves the delivered water kind', () => {
  assert.equal(resolveSurfaceWaterVisual(SurfaceWaterKind.None), null);
  assert.deepEqual(
    [SurfaceWaterKind.Ocean, SurfaceWaterKind.Lake, SurfaceWaterKind.River, SurfaceWaterKind.Tributary, SurfaceWaterKind.Floodplain].map((kind) => resolveSurfaceWaterVisual(kind).label),
    ['ocean', 'lake', 'river', 'tributary', 'floodplain'],
  );
});

test('every authoritative GeographicFeature type has an explicit visual mapping', () => {
  const labels = [];
  for (let kind = GeographicFeatureType.Mountain; kind <= GeographicFeatureType.Cave; kind += 1) labels.push(resolveGeographicFeatureVisual(kind).label);
  assert.deepEqual(labels, [
    'mountain', 'mountain-range', 'river', 'tributary', 'lake', 'valley', 'basin', 'plain',
    'plateau', 'pass', 'cape', 'bay', 'coast', 'island', 'peninsula', 'cave',
  ]);
});
