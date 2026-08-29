export interface AgentSnapshot {
  readonly agentId: bigint;
  readonly x: number;
  readonly y: number;
  readonly velocityX: number;
  readonly velocityY: number;
  readonly tickCount: bigint;
}

export interface SampledAgent {
  readonly agentId: bigint;
  readonly x: number;
  readonly y: number;
  readonly velocityX: number;
  readonly velocityY: number;
  readonly tickCount: bigint;
}

interface ClientAgent {
  readonly agentId: bigint;
  previousX: number;
  previousY: number;
  currentX: number;
  currentY: number;
  velocityX: number;
  velocityY: number;
  tickCount: bigint;
  receivedAt: number;
  interpolationDurationMs: number;
}

export class EntityStore {
  private readonly agents = new Map<bigint, ClientAgent>();

  public get size(): number {
    return this.agents.size;
  }

  public spawn(snapshot: AgentSnapshot, receivedAt = performance.now()): void {
    validateSnapshot(snapshot);
    this.agents.set(snapshot.agentId, {
      agentId: snapshot.agentId,
      previousX: snapshot.x,
      previousY: snapshot.y,
      currentX: snapshot.x,
      currentY: snapshot.y,
      velocityX: snapshot.velocityX,
      velocityY: snapshot.velocityY,
      tickCount: snapshot.tickCount,
      receivedAt,
      interpolationDurationMs: 100,
    });
  }

  public update(snapshot: AgentSnapshot, receivedAt = performance.now()): boolean {
    validateSnapshot(snapshot);
    const agent = this.agents.get(snapshot.agentId);
    if (agent === undefined) {
      return false;
    }

    const observedInterval = receivedAt - agent.receivedAt;
    agent.previousX = agent.currentX;
    agent.previousY = agent.currentY;
    agent.currentX = snapshot.x;
    agent.currentY = snapshot.y;
    agent.velocityX = snapshot.velocityX;
    agent.velocityY = snapshot.velocityY;
    agent.tickCount = snapshot.tickCount;
    agent.receivedAt = receivedAt;
    if (Number.isFinite(observedInterval) && observedInterval > 0) {
      agent.interpolationDurationMs = clamp(observedInterval, 33, 500);
    }

    return true;
  }

  public remove(agentId: bigint): boolean {
    return this.agents.delete(agentId);
  }

  public clear(): void {
    this.agents.clear();
  }

public writeSampledPositions(now: number, target: Float32Array): number {
  const requiredValues = this.agents.size * 2;
  if (target.length < requiredValues) {
    throw new RangeError(`Target position buffer requires at least ${requiredValues} values.`);
  }

  let offset = 0;
  for (const agent of this.agents.values()) {
    const alpha = clamp((now - agent.receivedAt) / agent.interpolationDurationMs, 0, 1);
    target[offset] = lerp(agent.previousX, agent.currentX, alpha);
    target[offset + 1] = lerp(agent.previousY, agent.currentY, alpha);
    offset += 2;
  }
  return offset / 2;
}

public *sample(now = performance.now()): IterableIterator<SampledAgent> {
    for (const agent of this.agents.values()) {
      const alpha = clamp((now - agent.receivedAt) / agent.interpolationDurationMs, 0, 1);
      yield {
        agentId: agent.agentId,
        x: lerp(agent.previousX, agent.currentX, alpha),
        y: lerp(agent.previousY, agent.currentY, alpha),
        velocityX: agent.velocityX,
        velocityY: agent.velocityY,
        tickCount: agent.tickCount,
      };
    }
  }
}

function validateSnapshot(snapshot: AgentSnapshot): void {
  if (
    !Number.isFinite(snapshot.x) ||
    !Number.isFinite(snapshot.y) ||
    !Number.isFinite(snapshot.velocityX) ||
    !Number.isFinite(snapshot.velocityY)
  ) {
    throw new RangeError('Agent snapshot contains a non-finite value.');
  }
}

function lerp(from: number, to: number, alpha: number): number {
  return from + (to - from) * alpha;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
