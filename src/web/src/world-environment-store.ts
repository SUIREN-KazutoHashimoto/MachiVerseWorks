import type {
  GeographicFeatureObservation,
  NaturalToponymObservation,
  WorldEnvironmentSnapshotMessage,
} from './world-environment-protocol.ts';

export interface ReadonlyWorldEnvironmentStore {
  readonly revision: number;
  readonly snapshot: WorldEnvironmentSnapshotMessage | null;
  getFeature(featureId: bigint): GeographicFeatureObservation | undefined;
  getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined;
}

export class WorldEnvironmentStore implements ReadonlyWorldEnvironmentStore {
  private currentSnapshot: WorldEnvironmentSnapshotMessage | null = null;
  private readonly featuresById = new Map<bigint, GeographicFeatureObservation>();
  private readonly toponymsByFeatureId = new Map<bigint, NaturalToponymObservation>();
  private currentRevision = 0;

  public get revision(): number { return this.currentRevision; }
  public get snapshot(): WorldEnvironmentSnapshotMessage | null { return this.currentSnapshot; }

  public replace(snapshot: WorldEnvironmentSnapshotMessage): void {
    this.featuresById.clear();
    this.toponymsByFeatureId.clear();
    for (const feature of snapshot.features) this.featuresById.set(feature.featureId, feature);
    for (const toponym of snapshot.toponyms) this.toponymsByFeatureId.set(toponym.featureId, toponym);
    this.currentSnapshot = snapshot;
    this.currentRevision += 1;
  }

  public getFeature(featureId: bigint): GeographicFeatureObservation | undefined {
    return this.featuresById.get(featureId);
  }

  public getToponymForFeature(featureId: bigint): NaturalToponymObservation | undefined {
    return this.toponymsByFeatureId.get(featureId);
  }

  public clear(): void {
    if (this.currentSnapshot === null && this.featuresById.size === 0 && this.toponymsByFeatureId.size === 0) return;
    this.currentSnapshot = null;
    this.featuresById.clear();
    this.toponymsByFeatureId.clear();
    this.currentRevision += 1;
  }
}
