import { VisualInterpolationState } from './visual-interpolation-state.ts';

export interface AgentSnapshot {
  readonly agentId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly velocityX: number;
  readonly velocityY: number;
  readonly velocityZ: number;
  readonly tickCount: bigint;
}

export interface SampledAgent {
  readonly agentId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly velocityX: number;
  readonly velocityY: number;
  readonly velocityZ: number;
  readonly tickCount: bigint;
}

export interface ReadonlyEntityStore {
  readonly size: number;
  writeSampledPositions(now: number, target: Float32Array): number;
  sampleById(agentId: bigint, now?: number): SampledAgent | undefined;
  sample(now?: number): IterableIterator<SampledAgent>;
}

/** Authoritative observation state received from the server. */
export class EntityStore implements ReadonlyEntityStore {
  private readonly agents = new Map<bigint, AgentSnapshot>();
  private readonly interpolation = new VisualInterpolationState<bigint>();

  public get size(): number { return this.agents.size; }

  public spawn(snapshot: AgentSnapshot, receivedAt = performance.now()): void {
    validateSnapshot(snapshot);
    this.agents.set(snapshot.agentId, snapshot);
    this.interpolation.upsert(snapshot.agentId, snapshot, receivedAt);
  }

  public update(snapshot: AgentSnapshot, receivedAt = performance.now()): boolean {
    validateSnapshot(snapshot);
    if (!this.agents.has(snapshot.agentId)) return false;
    this.agents.set(snapshot.agentId, snapshot);
    this.interpolation.upsert(snapshot.agentId, snapshot, receivedAt);
    return true;
  }

  public remove(agentId: bigint): boolean {
    this.interpolation.remove(agentId);
    return this.agents.delete(agentId);
  }

  public clear(): void {
    this.agents.clear();
    this.interpolation.clear();
  }

  public writeSampledPositions(now: number, target: Float32Array): number {
    const requiredValues = this.agents.size * 3;
    if (target.length < requiredValues) throw new RangeError(`Target position buffer requires at least ${requiredValues} values.`);

    let offset = 0;
    for (const agentId of this.agents.keys()) {
      if (!this.interpolation.writeSampledPosition(agentId, now, target, offset)) continue;
      offset += 3;
    }
    return offset / 3;
  }

  public sampleById(agentId: bigint, now = performance.now()): SampledAgent | undefined {
    const agent = this.agents.get(agentId);
    if (agent === undefined) return undefined;
    const position = this.interpolation.sample(agentId, now);
    if (position === undefined) return undefined;
    return {
      agentId,
      x: position.x,
      y: position.y,
      z: position.z,
      velocityX: agent.velocityX,
      velocityY: agent.velocityY,
      velocityZ: agent.velocityZ,
      tickCount: agent.tickCount,
    };
  }

  public *sample(now = performance.now()): IterableIterator<SampledAgent> {
    for (const agentId of this.agents.keys()) {
      const sampled = this.sampleById(agentId, now);
      if (sampled !== undefined) yield sampled;
    }
  }
}

function validateSnapshot(snapshot: AgentSnapshot): void {
  if (![snapshot.x, snapshot.y, snapshot.z, snapshot.velocityX, snapshot.velocityY, snapshot.velocityZ].every(Number.isFinite)) {
    throw new RangeError('Agent snapshot contains a non-finite value.');
  }
}
