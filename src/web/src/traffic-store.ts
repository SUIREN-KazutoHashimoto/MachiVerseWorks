import {
  type IntersectionControlSnapshotMessage,
  type VehicleStateMessage,
} from './traffic-protocol.ts';
import { VisualInterpolationState } from './visual-interpolation-state.ts';

interface ClientIntersection {
  snapshot: IntersectionControlSnapshotMessage;
  receivedAt: number;
}

export interface ReadonlyVehicleStore {
  readonly size: number;
  writeSampledTransforms(now: number, positions: Float32Array, scales: Float32Array, yawRadians: Float32Array): number;
}

export interface ReadonlyIntersectionControlStore {
  active(now?: number, staleAfterMs?: number): IterableIterator<IntersectionControlSnapshotMessage>;
}

/** Authoritative vehicle observations. Visual history is kept separately. */
export class VehicleStore implements ReadonlyVehicleStore {
  private readonly vehicles = new Map<bigint, VehicleStateMessage>();
  private readonly interpolation = new VisualInterpolationState<bigint>();
  public get size(): number { return this.vehicles.size; }

  public spawn(snapshot: VehicleStateMessage, receivedAt = performance.now()): void {
    validateVehicle(snapshot);
    this.vehicles.set(snapshot.vehicleId, snapshot);
    this.interpolation.upsert(snapshot.vehicleId, snapshot, receivedAt);
  }

  public update(snapshot: VehicleStateMessage, receivedAt = performance.now()): boolean {
    validateVehicle(snapshot);
    if (!this.vehicles.has(snapshot.vehicleId)) return false;
    this.vehicles.set(snapshot.vehicleId, snapshot);
    this.interpolation.upsert(snapshot.vehicleId, snapshot, receivedAt);
    return true;
  }

  public remove(vehicleId: bigint): boolean {
    this.interpolation.remove(vehicleId);
    return this.vehicles.delete(vehicleId);
  }

  public clear(): void {
    this.vehicles.clear();
    this.interpolation.clear();
  }

  public writeSampledTransforms(now: number, positions: Float32Array, scales: Float32Array, yawRadians: Float32Array): number {
    const required = this.vehicles.size * 3;
    if (positions.length < required || scales.length < required || yawRadians.length < this.vehicles.size) throw new RangeError('Target vehicle transform buffers are too small.');
    let index = 0;
    for (const [vehicleId, vehicle] of this.vehicles) {
      const position = this.interpolation.sample(vehicleId, now);
      if (position === undefined) continue;
      const offset = index * 3;
      positions[offset] = position.x;
      positions[offset + 1] = position.y;
      positions[offset + 2] = position.z;
      scales[offset] = vehicle.widthMeters;
      scales[offset + 1] = vehicle.heightMeters;
      scales[offset + 2] = vehicle.lengthMeters;
      yawRadians[index] = Math.atan2(vehicle.forwardX, vehicle.forwardY);
      index += 1;
    }
    return index;
  }
}

export class IntersectionControlStore implements ReadonlyIntersectionControlStore {
  private readonly intersections = new Map<bigint, ClientIntersection>();

  public apply(snapshot: IntersectionControlSnapshotMessage, receivedAt = performance.now()): void {
    this.intersections.set(snapshot.intersectionNodeId, { snapshot, receivedAt });
  }

  public clear(): void { this.intersections.clear(); }

  public *active(now = performance.now(), staleAfterMs = 1_500): IterableIterator<IntersectionControlSnapshotMessage> {
    for (const value of this.intersections.values()) {
      if (now - value.receivedAt <= staleAfterMs) yield value.snapshot;
    }
  }
}

function validateVehicle(snapshot: VehicleStateMessage): void {
  if (snapshot.vehicleId === 0n || snapshot.laneId === 0n) throw new RangeError('Vehicle and Lane IDs must be greater than zero.');
  if (![snapshot.x, snapshot.y, snapshot.z, snapshot.forwardX, snapshot.forwardY, snapshot.forwardZ, snapshot.lengthMeters, snapshot.widthMeters, snapshot.heightMeters].every(Number.isFinite)) throw new RangeError('Vehicle snapshot contains a non-finite value.');
}
