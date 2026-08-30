import { PedestrianMovementState, type PedestrianStateMessage } from './protocol.ts';

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

interface ClientPedestrian {
  readonly pedestrianId: bigint;
  tripRequestId: bigint;
  previousX: number;
  previousY: number;
  previousZ: number;
  currentX: number;
  currentY: number;
  currentZ: number;
  velocityX: number;
  velocityY: number;
  velocityZ: number;
  walkingSpeedMetersPerSecond: number;
  state: PedestrianMovementState;
  tickCount: bigint;
  receivedAt: number;
  interpolationDurationMs: number;
}

export class PedestrianStore {
  private readonly pedestrians = new Map<bigint, ClientPedestrian>();

  public get size(): number { return this.pedestrians.size; }

  public spawn(snapshot: PedestrianStateMessage, receivedAt = performance.now()): void {
    validateSnapshot(snapshot);
    this.pedestrians.set(snapshot.pedestrianId, {
      pedestrianId: snapshot.pedestrianId,
      tripRequestId: snapshot.tripRequestId,
      previousX: snapshot.x,
      previousY: snapshot.y,
      previousZ: snapshot.z,
      currentX: snapshot.x,
      currentY: snapshot.y,
      currentZ: snapshot.z,
      velocityX: snapshot.velocityX,
      velocityY: snapshot.velocityY,
      velocityZ: snapshot.velocityZ,
      walkingSpeedMetersPerSecond: snapshot.walkingSpeedMetersPerSecond,
      state: snapshot.state,
      tickCount: snapshot.tickCount,
      receivedAt,
      interpolationDurationMs: 100,
    });
  }

  public update(snapshot: PedestrianStateMessage, receivedAt = performance.now()): boolean {
    validateSnapshot(snapshot);
    const pedestrian = this.pedestrians.get(snapshot.pedestrianId);
    if (pedestrian === undefined) return false;
    const observedInterval = receivedAt - pedestrian.receivedAt;
    pedestrian.previousX = pedestrian.currentX;
    pedestrian.previousY = pedestrian.currentY;
    pedestrian.previousZ = pedestrian.currentZ;
    pedestrian.currentX = snapshot.x;
    pedestrian.currentY = snapshot.y;
    pedestrian.currentZ = snapshot.z;
    pedestrian.tripRequestId = snapshot.tripRequestId;
    pedestrian.velocityX = snapshot.velocityX;
    pedestrian.velocityY = snapshot.velocityY;
    pedestrian.velocityZ = snapshot.velocityZ;
    pedestrian.walkingSpeedMetersPerSecond = snapshot.walkingSpeedMetersPerSecond;
    pedestrian.state = snapshot.state;
    pedestrian.tickCount = snapshot.tickCount;
    pedestrian.receivedAt = receivedAt;
    if (Number.isFinite(observedInterval) && observedInterval > 0) pedestrian.interpolationDurationMs = clamp(observedInterval, 33, 500);
    return true;
  }

  public remove(pedestrianId: bigint): boolean { return this.pedestrians.delete(pedestrianId); }
  public clear(): void { this.pedestrians.clear(); }

  public writeSampledPositions(now: number, target: Float32Array): number {
    const requiredValues = this.pedestrians.size * 3;
    if (target.length < requiredValues) throw new RangeError(`Target pedestrian position buffer requires at least ${String(requiredValues)} values.`);
    let offset = 0;
    for (const pedestrian of this.pedestrians.values()) {
      const alpha = clamp((now - pedestrian.receivedAt) / pedestrian.interpolationDurationMs, 0, 1);
      target[offset] = lerp(pedestrian.previousX, pedestrian.currentX, alpha);
      target[offset + 1] = lerp(pedestrian.previousY, pedestrian.currentY, alpha);
      target[offset + 2] = lerp(pedestrian.previousZ, pedestrian.currentZ, alpha);
      offset += 3;
    }
    return offset / 3;
  }

  public *sample(now = performance.now()): IterableIterator<SampledPedestrian> {
    for (const pedestrian of this.pedestrians.values()) {
      const alpha = clamp((now - pedestrian.receivedAt) / pedestrian.interpolationDurationMs, 0, 1);
      yield {
        pedestrianId: pedestrian.pedestrianId,
        tripRequestId: pedestrian.tripRequestId,
        x: lerp(pedestrian.previousX, pedestrian.currentX, alpha),
        y: lerp(pedestrian.previousY, pedestrian.currentY, alpha),
        z: lerp(pedestrian.previousZ, pedestrian.currentZ, alpha),
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

function lerp(from: number, to: number, alpha: number): number { return from + (to - from) * alpha; }
function clamp(value: number, minimum: number, maximum: number): number { return Math.min(maximum, Math.max(minimum, value)); }
