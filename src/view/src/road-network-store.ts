import type { Lane, LaneConnection, RoadAccessPoint, RoadNetworkSnapshotMessage, RoadNode, RoadSegment } from './protocol.ts';

export interface ReadonlyRoadNetworkStore {
  readonly snapshot: RoadNetworkSnapshotMessage | null;
  readonly revision: number;
  readonly segmentCount: number;
  getNode(id: bigint): RoadNode | undefined;
  getSegment(id: bigint): RoadSegment | undefined;
}

export class RoadNetworkStore implements ReadonlyRoadNetworkStore {
  private current: RoadNetworkSnapshotMessage | null = null;
  private nodeById = new Map<bigint, RoadNode>();
  private segmentById = new Map<bigint, RoadSegment>();
  private generation = 0;

  public get snapshot(): RoadNetworkSnapshotMessage | null { return this.current; }
  public get revision(): number { return this.generation; }
  public get segmentCount(): number { return this.current?.segments.length ?? 0; }

  public replace(snapshot: RoadNetworkSnapshotMessage): void {
    if (this.current !== null && sameTopology(this.current, snapshot)) {
      this.current = snapshot;
      return;
    }
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

function sameTopology(left: RoadNetworkSnapshotMessage, right: RoadNetworkSnapshotMessage): boolean {
  return sameArray(left.nodes, right.nodes, sameNode)
    && sameArray(left.segments, right.segments, sameSegment)
    && sameArray(left.lanes, right.lanes, sameLane)
    && sameArray(left.connections, right.connections, sameConnection)
    && sameArray(left.accessPoints, right.accessPoints, sameAccessPoint);
}

function sameArray<T>(left: readonly T[], right: readonly T[], equals: (left: T, right: T) => boolean): boolean {
  if (left.length !== right.length) return false;
  for (let index = 0; index < left.length; index += 1) if (!equals(left[index], right[index])) return false;
  return true;
}

function sameNode(left: RoadNode, right: RoadNode): boolean {
  return left.id === right.id && left.kind === right.kind && left.x === right.x && left.y === right.y && left.z === right.z;
}
function sameSegment(left: RoadSegment, right: RoadSegment): boolean {
  return left.id === right.id && left.kind === right.kind && left.startNodeId === right.startNodeId && left.endNodeId === right.endNodeId;
}
function sameLane(left: Lane, right: Lane): boolean {
  return left.id === right.id && left.segmentId === right.segmentId && left.direction === right.direction && left.order === right.order && left.widthMeters === right.widthMeters && left.speedLimitMetersPerSecond === right.speedLimitMetersPerSecond;
}
function sameConnection(left: LaneConnection, right: LaneConnection): boolean {
  return left.id === right.id && left.fromLaneId === right.fromLaneId && left.toLaneId === right.toLaneId && left.viaNodeId === right.viaNodeId && left.movement === right.movement;
}
function sameAccessPoint(left: RoadAccessPoint, right: RoadAccessPoint): boolean {
  return left.id === right.id && left.segmentId === right.segmentId && left.segmentOffset === right.segmentOffset && left.buildingId === right.buildingId && left.poiId === right.poiId && left.mode === right.mode;
}
