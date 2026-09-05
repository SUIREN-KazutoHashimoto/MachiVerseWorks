export class EntityEmitterIndex {
  private readonly emitterIdsByEntity = new Map<bigint, Set<string>>();

  public add(entityId: bigint, emitterId: string): void {
    let emitterIds = this.emitterIdsByEntity.get(entityId);
    if (emitterIds === undefined) {
      emitterIds = new Set<string>();
      this.emitterIdsByEntity.set(entityId, emitterIds);
    }
    emitterIds.add(emitterId);
  }

  public remove(entityId: bigint, emitterId: string): void {
    const emitterIds = this.emitterIdsByEntity.get(entityId);
    if (emitterIds === undefined) {
      return;
    }
    emitterIds.delete(emitterId);
    if (emitterIds.size === 0) {
      this.emitterIdsByEntity.delete(entityId);
    }
  }

  public get(entityId: bigint): ReadonlySet<string> | undefined {
    return this.emitterIdsByEntity.get(entityId);
  }

  public has(entityId: bigint): boolean {
    return (this.emitterIdsByEntity.get(entityId)?.size ?? 0) > 0;
  }

  public clear(): void {
    this.emitterIdsByEntity.clear();
  }
}
