import type { Application } from './application.ts';
import { RoadNodeKind, type RoadNode, type RoadSegment } from './protocol.ts';
import { createStaticNavigationTarget } from './view-navigation.ts';

export type UserFacingVisualCheckpoint =
  | 'world-overview'
  | 'dense-urban'
  | 'road-interchange'
  | 'railway'
  | 'street-activity';

export interface UserFacingVisualDiagnostics {
  readonly ready: boolean;
  readonly terrainSampleCount: number;
  readonly settlementCount: number;
  readonly buildingCount: number;
  readonly roadSegmentCount: number;
  readonly roadSnapshotSequence: number;
  readonly railwayNodeCount: number;
  readonly railwayStationCount: number;
  readonly pedestrianCount: number;
  readonly vehicleCount: number;
  readonly trainCount: number;
  readonly hiddenDebugChromeCount: number;
}

export interface UserFacingVisualTestApi {
  getDiagnostics(): UserFacingVisualDiagnostics;
  prepareCapture(): number;
  setCheckpoint(checkpoint: UserFacingVisualCheckpoint): boolean;
}

type UserFacingVisualWindow = Window & {
  __MACHIVERSE_USER_FACING_VISUAL_TEST__?: UserFacingVisualTestApi;
};

type RailwayMeshLike = {
  readonly position: {
    readonly x: number;
    readonly y: number;
    readonly z: number;
  };
};

type RailwayNodeLike = {
  readonly x: number;
  readonly y: number;
  readonly z: number;
};

type RailwayBoundsLike = {
  readonly minX: number;
  readonly minY: number;
  readonly minZ: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly maxZ: number;
};

type UserFacingApplicationInternals = {
  readonly railway: {
    readonly nodes: ReadonlyMap<bigint, RailwayNodeLike>;
    readonly stationBounds: ReadonlyMap<bigint, RailwayBoundsLike>;
  };
  readonly railwayOperations: {
    readonly trainCount: number;
    readonly meshes: ReadonlyMap<bigint, RailwayMeshLike>;
  };
};

const MAXIMUM_CHECKPOINT_FOCUS_DISTANCE = 2_400;
const DEBUG_CHROME_SELECTOR = [
  '.person-debug',
  '.railway-debug',
  '.transit-debug',
  '.economy-debug',
  '.performance-overlay',
].join(',');
const CAPTURE_STYLE_ID = 'machiverse-user-facing-visual-capture-style';

export function installUserFacingVisualTest(application: Application): void {
  const api: UserFacingVisualTestApi = Object.freeze({
    getDiagnostics: () => collectDiagnostics(application),
    prepareCapture: () => prepareCapture(),
    setCheckpoint: (checkpoint: UserFacingVisualCheckpoint) => positionCheckpoint(application, checkpoint),
  });
  (window as UserFacingVisualWindow).__MACHIVERSE_USER_FACING_VISUAL_TEST__ = api;
}

function prepareCapture(): number {
  if (document.getElementById(CAPTURE_STYLE_ID) === null) {
    const style = document.createElement('style');
    style.id = CAPTURE_STYLE_ID;
    style.textContent = `${DEBUG_CHROME_SELECTOR} { display: none !important; }`;
    document.head.append(style);
  }
  return document.querySelectorAll(DEBUG_CHROME_SELECTOR).length;
}

function positionCheckpoint(application: Application, checkpoint: UserFacingVisualCheckpoint): boolean {
  const regional = application.state.regionalGeneration.snapshot;
  const road = application.state.roadNetwork.snapshot;
  if (regional === null || regional.settlements.length === 0 || regional.buildings.length === 0 || road === null || road.nodes.length === 0) return false;

  switch (checkpoint) {
    case 'world-overview': {
      const settlement = selectPrimarySettlement(application, road.nodes);
      return focusAt(application, 'vq0-world-overview', settlement, MAXIMUM_CHECKPOINT_FOCUS_DISTANCE);
    }
    case 'dense-urban': {
      const settlement = selectPrimarySettlement(application, road.nodes);
      const buildings = buildingsForSettlement(application, settlement.settlementId);
      const focus = centerOfBuildings(buildings) ?? settlement;
      const span = buildingSpan(buildings);
      const distance = clamp(Math.max(span * 0.9, 700), 700, 1_600);
      return focusAt(application, 'vq0-dense-urban', focus, distance);
    }
    case 'road-interchange': {
      const settlement = selectPrimarySettlement(application, road.nodes);
      const node = selectInterchangeNode(road.nodes, road.segments)
        ?? findNearestRoadNode(settlement.x, settlement.y, road.nodes);
      return node !== undefined && focusAt(application, 'vq0-road-interchange', node, 650);
    }
    case 'railway': {
      const railway = selectRailwayPosition(application);
      if (railway !== undefined) return focusAt(application, 'vq0-railway', railway, 700);
      const settlement = selectPrimarySettlement(application, road.nodes);
      return focusAt(application, 'vq0-railway-discovery', settlement, 900);
    }
    case 'street-activity': {
      const settlement = selectPrimarySettlement(application, road.nodes);
      const node = findNearestRoadNode(settlement.x, settlement.y, road.nodes) ?? settlement;
      return focusAt(application, 'vq0-street-activity', node, 360);
    }
  }
}

function focusAt(
  application: Application,
  id: string,
  position: { readonly x: number; readonly y: number; readonly z: number },
  distance: number,
): boolean {
  return application.focus(createStaticNavigationTarget(
    'position',
    id,
    { x: position.x, y: position.y, z: position.z },
    250 / clamp(distance, 250, MAXIMUM_CHECKPOINT_FOCUS_DISTANCE),
  ));
}

function selectPrimarySettlement(application: Application, roadNodes: readonly RoadNode[]) {
  const snapshot = application.state.regionalGeneration.snapshot!;
  const settlementBuildingCounts = new Map<bigint, number>();
  for (const building of snapshot.buildings) {
    const settlementId = application.state.regionalGeneration.getSettlementForBuilding(building.buildingId)?.settlementId;
    if (settlementId !== undefined) settlementBuildingCounts.set(settlementId, (settlementBuildingCounts.get(settlementId) ?? 0) + 1);
  }

  return [...snapshot.settlements].sort((left, right) =>
    (settlementBuildingCounts.get(right.settlementId) ?? 0) - (settlementBuildingCounts.get(left.settlementId) ?? 0)
      || right.population - left.population
      || nearestRoadDistanceSquared(left.x, left.y, roadNodes) - nearestRoadDistanceSquared(right.x, right.y, roadNodes)
      || compareBigInt(left.settlementId, right.settlementId))[0]!;
}

function buildingsForSettlement(application: Application, settlementId: bigint) {
  const snapshot = application.state.regionalGeneration.snapshot;
  if (snapshot === null) return [];
  return snapshot.buildings.filter((building) =>
    application.state.regionalGeneration.getSettlementForBuilding(building.buildingId)?.settlementId === settlementId);
}

function centerOfBuildings(buildings: readonly { readonly minX: number; readonly minY: number; readonly minZ: number; readonly maxX: number; readonly maxY: number; readonly maxZ: number }[]) {
  if (buildings.length === 0) return undefined;
  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let minZ = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  let maxZ = Number.NEGATIVE_INFINITY;
  for (const building of buildings) {
    minX = Math.min(minX, building.minX); minY = Math.min(minY, building.minY); minZ = Math.min(minZ, building.minZ);
    maxX = Math.max(maxX, building.maxX); maxY = Math.max(maxY, building.maxY); maxZ = Math.max(maxZ, building.maxZ);
  }
  return Object.freeze({ x: (minX + maxX) / 2, y: (minY + maxY) / 2, z: (minZ + maxZ) / 2 });
}

function buildingSpan(buildings: readonly { readonly minX: number; readonly minY: number; readonly maxX: number; readonly maxY: number }[]): number {
  if (buildings.length === 0) return 0;
  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  for (const building of buildings) {
    minX = Math.min(minX, building.minX); minY = Math.min(minY, building.minY);
    maxX = Math.max(maxX, building.maxX); maxY = Math.max(maxY, building.maxY);
  }
  return Math.max(maxX - minX, maxY - minY);
}

function selectInterchangeNode(nodes: readonly RoadNode[], segments: readonly RoadSegment[]): RoadNode | undefined {
  const degree = new Map<bigint, number>();
  for (const segment of segments) {
    degree.set(segment.startNodeId, (degree.get(segment.startNodeId) ?? 0) + 1);
    degree.set(segment.endNodeId, (degree.get(segment.endNodeId) ?? 0) + 1);
  }
  return [...nodes]
    .filter((node) => node.kind === RoadNodeKind.Intersection)
    .sort((left, right) => (degree.get(right.id) ?? 0) - (degree.get(left.id) ?? 0) || compareBigInt(left.id, right.id))[0];
}

function selectRailwayPosition(application: Application): { readonly x: number; readonly y: number; readonly z: number } | undefined {
  const internals = application as unknown as UserFacingApplicationInternals;
  const train = [...internals.railwayOperations.meshes.entries()]
    .sort(([left], [right]) => compareBigInt(left, right))[0]?.[1];
  if (train !== undefined) return Object.freeze({ x: train.position.x, y: train.position.z, z: train.position.y });

  const station = [...internals.railway.stationBounds.entries()]
    .sort(([left], [right]) => compareBigInt(left, right))[0]?.[1];
  if (station !== undefined) {
    return Object.freeze({
      x: (station.minX + station.maxX) / 2,
      y: (station.minY + station.maxY) / 2,
      z: (station.minZ + station.maxZ) / 2,
    });
  }

  const node = [...internals.railway.nodes.entries()]
    .sort(([left], [right]) => compareBigInt(left, right))[0]?.[1];
  return node === undefined ? undefined : Object.freeze({ x: node.x, y: node.y, z: node.z });
}

function nearestRoadDistanceSquared(x: number, y: number, nodes: readonly RoadNode[]): number {
  const nearest = findNearestRoadNode(x, y, nodes);
  return nearest === undefined ? Number.POSITIVE_INFINITY : squaredDistance(x, y, nearest.x, nearest.y);
}

function findNearestRoadNode(x: number, y: number, nodes: readonly RoadNode[]): RoadNode | undefined {
  let nearest: RoadNode | undefined;
  let nearestDistance = Number.POSITIVE_INFINITY;
  for (const node of nodes) {
    const distance = squaredDistance(x, y, node.x, node.y);
    if (distance >= nearestDistance) continue;
    nearest = node;
    nearestDistance = distance;
  }
  return nearest;
}

function collectDiagnostics(application: Application): UserFacingVisualDiagnostics {
  const state = application.state;
  const regional = state.regionalGeneration.snapshot;
  const environment = state.worldEnvironment.snapshot;
  const internals = application as unknown as UserFacingApplicationInternals;
  const diagnostics = {
    terrainSampleCount: environment?.terrainSamples.length ?? 0,
    settlementCount: regional?.settlements.length ?? 0,
    buildingCount: regional?.buildings.length ?? 0,
    roadSegmentCount: state.roadNetwork.segmentCount,
    roadSnapshotSequence: state.roadSnapshotSequence,
    railwayNodeCount: internals.railway.nodes.size,
    railwayStationCount: internals.railway.stationBounds.size,
    pedestrianCount: state.pedestrians.size,
    vehicleCount: state.vehicles.size,
    trainCount: internals.railwayOperations.trainCount,
    hiddenDebugChromeCount: document.querySelectorAll(DEBUG_CHROME_SELECTOR).length,
  };
  return Object.freeze({
    ready: diagnostics.terrainSampleCount > 0
      && diagnostics.settlementCount > 0
      && diagnostics.buildingCount > 0
      && diagnostics.roadSegmentCount > 0
      && diagnostics.roadSnapshotSequence > 0,
    ...diagnostics,
  });
}

function squaredDistance(leftX: number, leftY: number, rightX: number, rightY: number): number {
  const deltaX = leftX - rightX;
  const deltaY = leftY - rightY;
  return deltaX * deltaX + deltaY * deltaY;
}

function compareBigInt(left: bigint, right: bigint): number {
  return left < right ? -1 : left > right ? 1 : 0;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
