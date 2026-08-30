import {
  type IntersectionControlSnapshotMessage,
  type VehicleStateMessage,
} from './traffic-protocol.ts';

interface ClientVehicle {
  readonly vehicleId: bigint;
  previousX: number;
  previousY: number;
  previousZ: number;
  currentX: number;
  currentY: number;
  currentZ: number;
  forwardX: number;
  forwardY: number;
  forwardZ: number;
  lengthMeters: number;
  widthMeters: number;
  heightMeters: number;
  receivedAt: number;
  interpolationDurationMs: number;
}

interface ClientIntersection {
  snapshot: IntersectionControlSnapshotMessage;
  receivedAt: number;
}

export class VehicleStore {
  private readonly vehicles = new Map<bigint, ClientVehicle>();
  public get size(): number { return this.vehicles.size; }

  public spawn(snapshot: VehicleStateMessage, receivedAt = performance.now()): void {
    validateVehicle(snapshot);
    this.vehicles.set(snapshot.vehicleId, {
      vehicleId: snapshot.vehicleId,
      previousX: snapshot.x,
      previousY: snapshot.y,
      previousZ: snapshot.z,
      currentX: snapshot.x,
      currentY: snapshot.y,
      currentZ: snapshot.z,
      forwardX: snapshot.forwardX,
      forwardY: snapshot.forwardY,
      forwardZ: snapshot.forwardZ,
      lengthMeters: snapshot.lengthMeters,
      widthMeters: snapshot.widthMeters,
      heightMeters: snapshot.heightMeters,
      receivedAt,
      interpolationDurationMs: 100,
    });
  }

  public update(snapshot: VehicleStateMessage, receivedAt = performance.now()): boolean {
    validateVehicle(snapshot);
    const vehicle = this.vehicles.get(snapshot.vehicleId);
    if (vehicle === undefined) return false;
    const observedInterval = receivedAt - vehicle.receivedAt;
    vehicle.previousX = vehicle.currentX;
    vehicle.previousY = vehicle.currentY;
    vehicle.previousZ = vehicle.currentZ;
    vehicle.currentX = snapshot.x;
    vehicle.currentY = snapshot.y;
    vehicle.currentZ = snapshot.z;
    vehicle.forwardX = snapshot.forwardX;
    vehicle.forwardY = snapshot.forwardY;
    vehicle.forwardZ = snapshot.forwardZ;
    vehicle.lengthMeters = snapshot.lengthMeters;
    vehicle.widthMeters = snapshot.widthMeters;
    vehicle.heightMeters = snapshot.heightMeters;
    vehicle.receivedAt = receivedAt;
    if (Number.isFinite(observedInterval) && observedInterval > 0) vehicle.interpolationDurationMs = clamp(observedInterval, 33, 500);
    return true;
  }

  public remove(vehicleId: bigint): boolean { return this.vehicles.delete(vehicleId); }
  public clear(): void { this.vehicles.clear(); }

  public writeSampledTransforms(
    now: number,
    positions: Float32Array,
    scales: Float32Array,
    yawRadians: Float32Array,
  ): number {
    const required = this.vehicles.size * 3;
    if (positions.length < required || scales.length < required || yawRadians.length < this.vehicles.size) throw new RangeError('Target vehicle transform buffers are too small.');
    let index = 0;
    for (const vehicle of this.vehicles.values()) {
      const alpha = clamp((now - vehicle.receivedAt) / vehicle.interpolationDurationMs, 0, 1);
      const offset = index * 3;
      positions[offset] = lerp(vehicle.previousX, vehicle.currentX, alpha);
      positions[offset + 1] = lerp(vehicle.previousY, vehicle.currentY, alpha);
      positions[offset + 2] = lerp(vehicle.previousZ, vehicle.currentZ, alpha);
      scales[offset] = vehicle.widthMeters;
      scales[offset + 1] = vehicle.heightMeters;
      scales[offset + 2] = vehicle.lengthMeters;
      yawRadians[index] = Math.atan2(vehicle.forwardX, vehicle.forwardY);
      index += 1;
    }
    return index;
  }
}

export class IntersectionControlStore {
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

function lerp(from: number, to: number, alpha: number): number { return from + (to - from) * alpha; }
function clamp(value: number, minimum: number, maximum: number): number { return Math.min(maximum, Math.max(minimum, value)); }
