import type {
  GeographicFeatureObservation,
  NaturalToponymObservation,
  TerrainSurfaceSampleObservation,
  WorldEnvironmentSnapshotMessage,
  WorldPointObservation,
} from './world-environment-protocol.ts';

export interface ReadonlyWorldEnvironmentStore {
  readonly revision: number;
  readonly snapshot: WorldEnvironmentSnapshotMessage | null;
  getFeature(featureId: bigint): GeographicFeatureObservation | undefined;
  getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined;
  getNearestTerrainElevation(x: number, y: number): number | undefined;
}

export class WorldEnvironmentStore implements ReadonlyWorldEnvironmentStore {
  private currentSnapshot: WorldEnvironmentSnapshotMessage | null = null;
  private readonly featuresById = new Map<bigint, GeographicFeatureObservation>();
  private readonly toponymsByFeatureId = new Map<bigint, NaturalToponymObservation>();
  private currentRevision = 0;

  public get revision(): number { return this.currentRevision; }
  public get snapshot(): WorldEnvironmentSnapshotMessage | null { return this.currentSnapshot; }

  public replace(snapshot: WorldEnvironmentSnapshotMessage): void {
    const renderingChanged = this.currentSnapshot === null || !samePhysicalWorldRenderingContent(this.currentSnapshot, snapshot);

    this.featuresById.clear();
    this.toponymsByFeatureId.clear();
    for (const feature of snapshot.features) this.featuresById.set(feature.featureId, feature);
    for (const toponym of snapshot.toponyms) this.toponymsByFeatureId.set(toponym.featureId, toponym);
    this.currentSnapshot = snapshot;

    // WorldEnvironment may be published every tick even when its presentation data is unchanged.
    // Keep the latest authoritative snapshot, but only invalidate GPU resources when rendered content changes.
    if (renderingChanged) this.currentRevision += 1;
  }

  public getFeature(featureId: bigint): GeographicFeatureObservation | undefined {
    return this.featuresById.get(featureId);
  }

  public getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined {
    return this.toponymsByFeatureId.get(featureId);
  }

  public getNearestTerrainElevation(x: number, y: number): number | undefined {
    if (!Number.isFinite(x) || !Number.isFinite(y)) return undefined;
    const samples = this.currentSnapshot?.terrainSamples;
    if (samples === undefined || samples.length === 0) return undefined;
    let nearest = samples[0]!;
    let nearestDistanceSquared = Number.POSITIVE_INFINITY;
    for (const sample of samples) {
      const dx = sample.x - x;
      const dy = sample.y - y;
      const distanceSquared = dx * dx + dy * dy;
      if (distanceSquared < nearestDistanceSquared) {
        nearest = sample;
        nearestDistanceSquared = distanceSquared;
      }
    }
    return nearest.z;
  }

  public clear(): void {
    if (this.currentSnapshot === null && this.featuresById.size === 0 && this.toponymsByFeatureId.size === 0) return;
    this.currentSnapshot = null;
    this.featuresById.clear();
    this.toponymsByFeatureId.clear();
    this.currentRevision += 1;
  }
}

function samePhysicalWorldRenderingContent(left: WorldEnvironmentSnapshotMessage, right: WorldEnvironmentSnapshotMessage): boolean {
  if (left.minX !== right.minX || left.minY !== right.minY || left.minZ !== right.minZ
    || left.maxX !== right.maxX || left.maxY !== right.maxY || left.maxZ !== right.maxZ) return false;
  return sameTerrainSamples(left.terrainSamples, right.terrainSamples)
    && sameFeatures(left.features, right.features)
    && sameToponyms(left.toponyms, right.toponyms);
}

function sameTerrainSamples(left: readonly TerrainSurfaceSampleObservation[], right: readonly TerrainSurfaceSampleObservation[]): boolean {
  if (left.length !== right.length) return false;
  for (let index = 0; index < left.length; index += 1) {
    const a = left[index]!;
    const b = right[index]!;
    if (a.x !== b.x || a.y !== b.y || a.z !== b.z
      || a.normalX !== b.normalX || a.normalY !== b.normalY || a.normalZ !== b.normalZ
      || a.slopeDegrees !== b.slopeDegrees || a.roughness !== b.roughness
      || a.material !== b.material || a.surfaceWater !== b.surfaceWater) return false;
  }
  return true;
}

function sameFeatures(left: readonly GeographicFeatureObservation[], right: readonly GeographicFeatureObservation[]): boolean {
  if (left.length !== right.length) return false;
  for (let index = 0; index < left.length; index += 1) {
    const a = left[index]!;
    const b = right[index]!;
    if (a.featureId !== b.featureId || a.featureType !== b.featureType
      || a.minX !== b.minX || a.minY !== b.minY || a.minZ !== b.minZ
      || a.maxX !== b.maxX || a.maxY !== b.maxY || a.maxZ !== b.maxZ
      || a.areaSquareMeters !== b.areaSquareMeters || a.parentFeatureId !== b.parentFeatureId
      || a.minimumElevationMeters !== b.minimumElevationMeters || a.maximumElevationMeters !== b.maximumElevationMeters
      || !sameGeometry(a.geometry, b.geometry)) return false;
  }
  return true;
}

function sameGeometry(left: readonly WorldPointObservation[], right: readonly WorldPointObservation[]): boolean {
  if (left.length !== right.length) return false;
  for (let index = 0; index < left.length; index += 1) {
    const a = left[index]!;
    const b = right[index]!;
    if (a.x !== b.x || a.y !== b.y || a.z !== b.z) return false;
  }
  return true;
}

function sameToponyms(left: readonly NaturalToponymObservation[], right: readonly NaturalToponymObservation[]): boolean {
  if (left.length !== right.length) return false;
  for (let index = 0; index < left.length; index += 1) {
    const a = left[index]!;
    const b = right[index]!;
    if (a.toponymId !== b.toponymId || a.featureId !== b.featureId || a.name !== b.name
      || a.provenanceKind !== b.provenanceKind || a.sourceFeatureId !== b.sourceFeatureId
      || a.parentToponymId !== b.parentToponymId || a.generatorKey !== b.generatorKey) return false;
  }
  return true;
}
