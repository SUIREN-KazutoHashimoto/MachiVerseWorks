import type { RoadNetworkSnapshotMessage, RoadNode, RoadSegment } from './protocol.ts';

export class RoadNetworkStore {
  private current: RoadNetworkSnapshotMessage | null = null;
  private nodeById = new Map<bigint, RoadNode>();
  private segmentById = new Map<bigint, RoadSegment>();
  private generation = 0;

  public get snapshot(): RoadNetworkSnapshotMessage | null { return this.current; }
  public get revision(): number { return this.generation; }
  public get segmentCount(): number { return this.current?.segments.length ?? 0; }

  public replace(snapshot: RoadNetworkSnapshotMessage): void {
    this.current = snapshot;
    this.nodeById = new Map(snapshot.nodes.map((node) => [node.id, node]));
    this.segmentById = new Map(snapshot.segments.map((segment) => [segment.id, segment]));
    this.generation += 1;
  }

  public clear(): void {
    if (this.current === null) return;
    this.current = null;
    this.nodeById.clear();
    this.segmentById.clear();
    this.generation += 1;
  }

  public getNode(id: bigint): RoadNode | undefined { return this.nodeById.get(id); }
  public getSegment(id: bigint): RoadSegment | undefined { return this.segmentById.get(id); }
}
