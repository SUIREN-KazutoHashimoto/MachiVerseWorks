import {
  SurfaceWaterKind,
  type TerrainMaterialKind,
  type TerrainSurfaceSampleObservation,
} from './world-environment-protocol.ts';

export type TerrainSurfaceRole = 'primary-ground' | 'water-surface' | 'cavity-boundary' | 'overhang';

/**
 * View-local geometry boundary for delivered terrain observations.
 * A column may contain multiple authoritative surfaces; the View never derives a missing surface.
 */
export interface ObservedTerrainSurface {
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly normalX: number;
  readonly normalY: number;
  readonly normalZ: number;
  readonly material: TerrainMaterialKind;
  readonly surfaceWater: SurfaceWaterKind;
  readonly role: TerrainSurfaceRole;
  readonly layer: number;
}

export interface TerrainColumnObservation {
  readonly x: number;
  readonly y: number;
  readonly surfaces: readonly ObservedTerrainSurface[];
}

export interface TriangulatedTerrainSurface {
  readonly positions: Float32Array;
  readonly normals: Float32Array;
  readonly materialKinds: Uint8Array;
  readonly surfaceWaterKinds: Uint8Array;
  readonly indices: Uint32Array;
  readonly vertexCount: number;
  readonly triangleCount: number;
}

export function createPrimaryTerrainColumns(samples: readonly TerrainSurfaceSampleObservation[]): readonly TerrainColumnObservation[] {
  const columnsByCoordinate = new Map<string, TerrainColumnObservation>();
  for (const sample of samples) {
    const key = coordinateKey(sample.x, sample.y);
    if (columnsByCoordinate.has(key)) continue;
    columnsByCoordinate.set(key, Object.freeze({
      x: sample.x,
      y: sample.y,
      surfaces: Object.freeze([Object.freeze({
        x: sample.x,
        y: sample.y,
        z: sample.z,
        normalX: sample.normalX,
        normalY: sample.normalY,
        normalZ: sample.normalZ,
        material: sample.material,
        surfaceWater: sample.surfaceWater,
        role: 'primary-ground' as const,
        layer: 0,
      })]),
    }));
  }
  return Object.freeze([...columnsByCoordinate.values()]);
}

export function triangulateTerrainSurface(
  columns: readonly TerrainColumnObservation[],
  role: TerrainSurfaceRole = 'primary-ground',
  layer = 0,
): TriangulatedTerrainSurface {
  if (!Number.isInteger(layer) || layer < 0) throw new RangeError('Terrain surface layer must be a non-negative integer.');
  const selected = new Map<string, ObservedTerrainSurface>();
  const xValues = new Set<number>();
  const yValues = new Set<number>();

  for (const column of columns) {
    const surface = column.surfaces.find((item) => item.role === role && item.layer === layer);
    if (surface === undefined) continue;
    const key = coordinateKey(column.x, column.y);
    if (selected.has(key)) throw new RangeError('Terrain geometry contains duplicate coordinates for the same surface layer.');
    selected.set(key, surface);
    xValues.add(column.x);
    yValues.add(column.y);
  }

  const sortedX = [...xValues].sort((left, right) => left - right);
  const sortedY = [...yValues].sort((left, right) => left - right);
  const positions = new Float32Array(selected.size * 3);
  const normals = new Float32Array(selected.size * 3);
  const materialKinds = new Uint8Array(selected.size);
  const surfaceWaterKinds = new Uint8Array(selected.size);
  const vertexIndices = new Map<string, number>();
  let vertexIndex = 0;

  for (const y of sortedY) {
    for (const x of sortedX) {
      const key = coordinateKey(x, y);
      const surface = selected.get(key);
      if (surface === undefined) continue;
      const offset = vertexIndex * 3;
      positions[offset] = surface.x;
      positions[offset + 1] = surface.z;
      positions[offset + 2] = surface.y;
      normals[offset] = surface.normalX;
      normals[offset + 1] = surface.normalZ;
      normals[offset + 2] = surface.normalY;
      materialKinds[vertexIndex] = surface.material;
      surfaceWaterKinds[vertexIndex] = surface.surfaceWater;
      vertexIndices.set(key, vertexIndex);
      vertexIndex += 1;
    }
  }

  const triangleIndices: number[] = [];
  for (let yIndex = 0; yIndex + 1 < sortedY.length; yIndex += 1) {
    for (let xIndex = 0; xIndex + 1 < sortedX.length; xIndex += 1) {
      const x0 = sortedX[xIndex];
      const x1 = sortedX[xIndex + 1];
      const y0 = sortedY[yIndex];
      const y1 = sortedY[yIndex + 1];
      if (x0 === undefined || x1 === undefined || y0 === undefined || y1 === undefined) continue;
      const a = vertexIndices.get(coordinateKey(x0, y0));
      const b = vertexIndices.get(coordinateKey(x1, y0));
      const c = vertexIndices.get(coordinateKey(x0, y1));
      const d = vertexIndices.get(coordinateKey(x1, y1));
      if (a === undefined || b === undefined || c === undefined || d === undefined) continue;
      triangleIndices.push(a, c, b, b, c, d);
    }
  }

  return Object.freeze({
    positions,
    normals,
    materialKinds,
    surfaceWaterKinds,
    indices: Uint32Array.from(triangleIndices),
    vertexCount: selected.size,
    triangleCount: triangleIndices.length / 3,
  });
}

export function countObservedWaterSamples(surface: TriangulatedTerrainSurface): number {
  let count = 0;
  for (const kind of surface.surfaceWaterKinds) if (kind !== SurfaceWaterKind.None) count += 1;
  return count;
}

function coordinateKey(x: number, y: number): string {
  if (!Number.isFinite(x) || !Number.isFinite(y)) throw new RangeError('Terrain coordinates must be finite.');
  return `${x}:${y}`;
}
