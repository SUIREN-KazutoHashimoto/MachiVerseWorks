import * as THREE from 'three';

import {
  createPrimaryTerrainColumns,
  triangulateTerrainSurface,
  type TriangulatedTerrainSurface,
} from './terrain-geometry.ts';
import {
  resolveGeographicFeatureVisual,
  resolveSurfaceWaterVisual,
  resolveTerrainMaterialVisual,
} from './physical-world-visuals.ts';
import type { ReadonlyWorldEnvironmentStore } from './world-environment-store.ts';
import {
  GeographicFeatureType,
  SurfaceWaterKind,
  type WorldEnvironmentSnapshotMessage,
} from './world-environment-protocol.ts';

export const SURFACE_WATER_PRESENTATION_OFFSET_METERS = 0.25;
const TOPONYM_FONT = '600 28px "Noto Sans JP", "Noto Sans CJK JP", sans-serif';

export interface WaterPointBatch {
  readonly kind: SurfaceWaterKind;
  readonly positions: Float32Array;
  readonly pointCount: number;
}

export interface GeographicFeatureLineBatch {
  readonly featureType: GeographicFeatureType;
  readonly positions: Float32Array;
  readonly segmentCount: number;
}

export interface PhysicalWorldGeometryModel {
  readonly terrain: TriangulatedTerrainSurface;
  readonly water: readonly WaterPointBatch[];
  readonly features: readonly GeographicFeatureLineBatch[];
  readonly geometryByteLength: number;
}

export interface PhysicalWorldRenderingMetrics {
  readonly revision: number;
  readonly buildTimeMs: number;
  readonly terrainTriangles: number;
  readonly waterSamples: number;
  readonly geographicFeatureSegments: number;
  readonly naturalToponymLabels: number;
  readonly geometryByteLength: number;
}

const EMPTY_METRICS: PhysicalWorldRenderingMetrics = Object.freeze({
  revision: -1,
  buildTimeMs: 0,
  terrainTriangles: 0,
  waterSamples: 0,
  geographicFeatureSegments: 0,
  naturalToponymLabels: 0,
  geometryByteLength: 0,
});

export class PhysicalWorldRenderer {
  private readonly root = new THREE.Group();
  private readonly hemisphereLight = new THREE.HemisphereLight(0xe0f2fe, 0x243044, 1.15);
  private readonly directionalLight = new THREE.DirectionalLight(0xffffff, 1.25);
  private renderedRevision = -1;
  private currentMetrics = EMPTY_METRICS;

  public constructor(private readonly scene: THREE.Scene) {
    this.root.name = 'physical-world';
    this.hemisphereLight.name = 'physical-world-hemisphere-light';
    this.directionalLight.name = 'physical-world-directional-light';
    this.directionalLight.position.set(-600, 900, 450);
    this.scene.add(this.hemisphereLight, this.directionalLight, this.root);
  }

  public get metrics(): PhysicalWorldRenderingMetrics { return this.currentMetrics; }

  public update(store: ReadonlyWorldEnvironmentStore): void {
    if (store.revision === this.renderedRevision) return;
    const startedAt = performance.now();
    this.renderedRevision = store.revision;
    this.clearRoot();
    const snapshot = store.snapshot;
    if (snapshot === null) {
      this.currentMetrics = Object.freeze({ ...EMPTY_METRICS, revision: store.revision });
      return;
    }

    const model = buildPhysicalWorldGeometry(snapshot);
    this.addTerrain(model.terrain);
    this.addWater(model.water);
    this.addGeographicFeatures(model.features);
    const labelCount = this.addToponyms(store, snapshot);
    this.currentMetrics = Object.freeze({
      revision: store.revision,
      buildTimeMs: Math.max(0, performance.now() - startedAt),
      terrainTriangles: model.terrain.triangleCount,
      waterSamples: model.water.reduce((sum, batch) => sum + batch.pointCount, 0),
      geographicFeatureSegments: model.features.reduce((sum, batch) => sum + batch.segmentCount, 0),
      naturalToponymLabels: labelCount,
      geometryByteLength: model.geometryByteLength,
    });
  }

  public dispose(): void {
    this.clearRoot();
    this.scene.remove(this.root, this.hemisphereLight, this.directionalLight);
  }

  private addTerrain(surface: TriangulatedTerrainSurface): void {
    if (surface.vertexCount === 0 || surface.triangleCount === 0) return;
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(surface.positions, 3));
    geometry.setAttribute('normal', new THREE.BufferAttribute(surface.normals, 3));
    geometry.setIndex(new THREE.BufferAttribute(surface.indices, 1));
    const colors = new Float32Array(surface.vertexCount * 3);
    const color = new THREE.Color();
    for (let index = 0; index < surface.vertexCount; index += 1) {
      color.setHex(resolveTerrainMaterialVisual(surface.materialKinds[index]!).color);
      const offset = index * 3;
      colors[offset] = color.r;
      colors[offset + 1] = color.g;
      colors[offset + 2] = color.b;
    }
    geometry.setAttribute('color', new THREE.BufferAttribute(colors, 3));
    geometry.computeBoundingSphere();
    const material = new THREE.MeshLambertMaterial({ vertexColors: true, side: THREE.DoubleSide });
    const mesh = new THREE.Mesh(geometry, material);
    mesh.name = 'terrain-surface';
    this.root.add(mesh);
  }

  private addWater(batches: readonly WaterPointBatch[]): void {
    for (const batch of batches) {
      if (batch.pointCount === 0) continue;
      const visual = resolveSurfaceWaterVisual(batch.kind);
      if (visual === null) continue;
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.BufferAttribute(batch.positions, 3));
      const material = new THREE.PointsMaterial({
        color: visual.color,
        size: visual.pointSize,
        sizeAttenuation: true,
        transparent: true,
        opacity: 0.88,
        depthTest: true,
        depthWrite: false,
      });
      const points = new THREE.Points(geometry, material);
      points.name = `surface-water-${visual.label}`;
      points.renderOrder = 2;
      this.root.add(points);
    }
  }

  private addGeographicFeatures(batches: readonly GeographicFeatureLineBatch[]): void {
    for (const batch of batches) {
      if (batch.segmentCount === 0) continue;
      const visual = resolveGeographicFeatureVisual(batch.featureType);
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.BufferAttribute(batch.positions, 3));
      const material = new THREE.LineBasicMaterial({ color: visual.color, transparent: true, opacity: 0.95 });
      const lines = new THREE.LineSegments(geometry, material);
      lines.name = `geographic-feature-${visual.label}`;
      lines.renderOrder = 3;
      this.root.add(lines);
    }
  }

  private addToponyms(store: ReadonlyWorldEnvironmentStore, snapshot: WorldEnvironmentSnapshotMessage): number {
    let count = 0;
    for (const toponym of snapshot.toponyms) {
      const feature = store.getFeature(toponym.featureId);
      if (feature === undefined) continue;
      const label = createTextSprite(toponym.name);
      label.name = `natural-toponym-${toponym.toponymId.toString()}`;
      label.userData.featureId = feature.featureId.toString();
      label.position.set((feature.minX + feature.maxX) * 0.5, feature.maximumElevationMeters, (feature.minY + feature.maxY) * 0.5);
      label.renderOrder = 4;
      this.root.add(label);
      count += 1;
    }
    return count;
  }

  private clearRoot(): void {
    for (const child of [...this.root.children]) {
      this.root.remove(child);
      child.traverse((object) => {
        if (object instanceof THREE.Mesh || object instanceof THREE.Points || object instanceof THREE.LineSegments) {
          object.geometry.dispose();
          disposeMaterial(object.material);
        } else if (object instanceof THREE.Sprite) {
          object.material.map?.dispose();
          object.material.dispose();
        }
      });
    }
  }
}

export function buildPhysicalWorldGeometry(snapshot: WorldEnvironmentSnapshotMessage): PhysicalWorldGeometryModel {
  const terrain = triangulateTerrainSurface(createPrimaryTerrainColumns(snapshot.terrainSamples));
  const water = buildWaterPointBatches(snapshot);
  const features = buildFeatureLineBatches(snapshot);
  const geometryByteLength = terrain.positions.byteLength + terrain.normals.byteLength + terrain.materialKinds.byteLength + terrain.surfaceWaterKinds.byteLength + terrain.indices.byteLength
    + water.reduce((sum, batch) => sum + batch.positions.byteLength, 0)
    + features.reduce((sum, batch) => sum + batch.positions.byteLength, 0);
  return Object.freeze({ terrain, water, features, geometryByteLength });
}

function buildWaterPointBatches(snapshot: WorldEnvironmentSnapshotMessage): readonly WaterPointBatch[] {
  const byKind = new Map<SurfaceWaterKind, number[]>();
  for (const sample of snapshot.terrainSamples) {
    if (sample.surfaceWater === SurfaceWaterKind.None) continue;
    let positions = byKind.get(sample.surfaceWater);
    if (positions === undefined) { positions = []; byKind.set(sample.surfaceWater, positions); }
    // This is a presentation-only lift to keep authoritative water markers from being coplanar with terrain.
    positions.push(sample.x, sample.z + SURFACE_WATER_PRESENTATION_OFFSET_METERS, sample.y);
  }
  return Object.freeze([...byKind.entries()].sort(([left], [right]) => left - right).map(([kind, positions]) => Object.freeze({
    kind,
    positions: Float32Array.from(positions),
    pointCount: positions.length / 3,
  })));
}

function buildFeatureLineBatches(snapshot: WorldEnvironmentSnapshotMessage): readonly GeographicFeatureLineBatch[] {
  const byType = new Map<GeographicFeatureType, number[]>();
  for (const feature of snapshot.features) {
    if (feature.geometry.length < 2) continue;
    let positions = byType.get(feature.featureType);
    if (positions === undefined) { positions = []; byType.set(feature.featureType, positions); }
    for (let index = 1; index < feature.geometry.length; index += 1) {
      const previous = feature.geometry[index - 1]!;
      const current = feature.geometry[index]!;
      positions.push(previous.x, previous.z, previous.y, current.x, current.z, current.y);
    }
  }
  return Object.freeze([...byType.entries()].sort(([left], [right]) => left - right).map(([featureType, positions]) => Object.freeze({
    featureType,
    positions: Float32Array.from(positions),
    segmentCount: positions.length / 6,
  })));
}

function createTextSprite(text: string): THREE.Sprite {
  const canvas = document.createElement('canvas');
  const context = canvas.getContext('2d');
  if (context === null) throw new Error('2D canvas context is required for WorldEnvironment toponyms.');
  context.font = TOPONYM_FONT;
  const width = Math.max(96, Math.ceil(context.measureText(text).width + 28));
  canvas.width = width;
  canvas.height = 48;
  context.font = TOPONYM_FONT;
  context.fillStyle = 'rgba(15, 23, 42, 0.78)';
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.fillStyle = '#f8fafc';
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.fillText(text, canvas.width / 2, canvas.height / 2);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false, depthWrite: false });
  const sprite = new THREE.Sprite(material);
  const labelHeight = 24;
  sprite.scale.set(labelHeight * (canvas.width / canvas.height), labelHeight, 1);
  return sprite;
}

function disposeMaterial(material: THREE.Material | THREE.Material[]): void {
  if (Array.isArray(material)) for (const item of material) item.dispose();
  else material.dispose();
}
