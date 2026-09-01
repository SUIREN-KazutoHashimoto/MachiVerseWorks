import { PedestrianMovementState, type PedestrianStateMessage } from './protocol.ts';
import { VisualInterpolationState } from './visual-interpolation-state.ts';

export interface SampledPedestrian {
  readonly pedestrianId: bigint;
  readonly tripRequestId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly velocityX: number;
  readonly velocityY: number;
  readonly velocityZ: number;
  readonly walkingSpeedMetersPerSecond: number;
  readonly state: PedestrianMovementState;
  readonly tickCount: bigint;
}

export interface ReadonlyPedestrianStore {
  readonly size: number;
  writeSampledPositions(now: number, target: Float32Array): number;
  sample(now?: number): IterableIterator<SampledPedestrian>;
}

/** Authoritative pedestrian observations. Visual history is kept separately. */
export class PedestrianStore implements ReadonlyPedestrianStore {
  private readonly pedestrians = new Map<bigint, PedestrianStateMessage>();
  private readonly interpolation = new VisualInterpolationState<bigint>();

  public get size(): number { return this.pedestrians.size; }

  public spawn(snapshot: PedestrianStateMessage, receivedAt = performance.now()): void {
    validateSnapshot(snapshot);
    this.pedestrians.set(snapshot.pedestrianId, snapshot);
    this.interpolation.upsert(snapshot.pedestrianId, snapshot, receivedAt);
  }

  public update(snapshot: PedestrianStateMessage, receivedAt = performance.now()): boolean {
    validateSnapshot(snapshot);
    if (!this.pedestrians.has(snapshot.pedestrianId)) return false;
    this.pedestrians.set(snapshot.pedestrianId, snapshot);
    this.interpolation.upsert(snapshot.pedestrianId, snapshot, receivedAt);
    return true;
  }

  public remove(pedestrianId: bigint): boolean {
    this.interpolation.remove(pedestrianId);
    return this.pedestrians.delete(pedestrianId);
  }

  public clear(): void {
    this.pedestrians.clear();
    this.interpolation.clear();
  }

  public writeSampledPositions(now: number, target: Float32Array): number {
    const requiredValues = this.pedestrians.size * 3;
    if (target.length < requiredValues) throw new RangeError(`Target pedestrian position buffer requires at least ${String(requiredValues)} values.`);
    let offset = 0;
    for (const pedestrianId of this.pedestrians.keys()) {
      const position = this.interpolation.sample(pedestrianId, now);
      if (position === undefined) continue;
      target[offset] = position.x;
      target[offset + 1] = position.y;
      target[offset + 2] = position.z;
      offset += 3;
    }
    return offset / 3;
  }

  public *sample(now = performance.now()): IterableIterator<SampledPedestrian> {
    for (const [pedestrianId, pedestrian] of this.pedestrians) {
      const position = this.interpolation.sample(pedestrianId, now);
      if (position === undefined) continue;
      yield {
        pedestrianId,
        tripRequestId: pedestrian.tripRequestId,
        x: position.x,
        y: position.y,
        z: position.z,
        velocityX: pedestrian.velocityX,
        velocityY: pedestrian.velocityY,
        velocityZ: pedestrian.velocityZ,
        walkingSpeedMetersPerSecond: pedestrian.walkingSpeedMetersPerSecond,
        state: pedestrian.state,
        tickCount: pedestrian.tickCount,
      };
    }
  }
}

function validateSnapshot(snapshot: PedestrianStateMessage): void {
  if (snapshot.pedestrianId === 0n || snapshot.tripRequestId === 0n) throw new RangeError('Pedestrian IDs must be greater than zero.');
  if (!Number.isFinite(snapshot.x) || !Number.isFinite(snapshot.y) || !Number.isFinite(snapshot.z) || !Number.isFinite(snapshot.velocityX) || !Number.isFinite(snapshot.velocityY) || !Number.isFinite(snapshot.velocityZ) || !Number.isFinite(snapshot.walkingSpeedMetersPerSecond) || snapshot.walkingSpeedMetersPerSecond <= 0) throw new RangeError('Pedestrian snapshot contains an invalid numeric value.');
}
