import type { Application } from './application.ts';
import type { RoadNode } from './protocol.ts';
import { createStaticNavigationTarget } from './view-navigation.ts';

export type RuntimeVisualCheckpoint = 'city-overview' | 'street-activity';

export interface RuntimeVisualDiagnostics {
  readonly ready: boolean;
  readonly genericAgentCount: number;
  readonly terrainSampleCount: number;
  readonly waterSampleCount: number;
  readonly settlementCount: number;
  readonly buildingCount: number;
  readonly roadSegmentCount: number;
  readonly roadSnapshotSequence: number;
  readonly pedestrianCount: number;
  readonly vehicleCount: number;
  readonly trainCount: number;
  readonly visibleDebugOverlayCount: number;
  readonly japaneseFontReady: boolean;
}

export interface RuntimeVisualTestApi {
  getDiagnostics(): RuntimeVisualDiagnostics;
  setCheckpoint(checkpoint: RuntimeVisualCheckpoint): boolean;
}

type RuntimeVisualWindow = Window & {
  __MACHIVERSE_RUNTIME_VISUAL_TEST__?: RuntimeVisualTestApi;
};

type RuntimeVisualApplicationInternals = {
  readonly railwayOperations: { readonly trainCount: number };
};

const DEBUG_OVERLAY_SELECTOR = [
  '.performance-overlay',
  '[data-logistics-debug="true"]',
  '[data-gas-debug="true"]',
  '[data-power-debug="true"]',
  '[data-optical-debug="true"]',
  '[data-water-sewer-debug="true"]',
  '[data-radio-debug="true"]',
].join(',');

// WorldView currently observes up to 3,000 simulation units. Keep deterministic
// checkpoints comfortably inside that depth so moving the camera cannot put the
// checkpoint target itself outside the next subscription volume.
const MAXIMUM_RUNTIME_CHECKPOINT_FOCUS_DISTANCE = 2_400;

export function installRuntimeVisualTest(application: Application): void {
  const api: RuntimeVisualTestApi = Object.freeze({
    getDiagnostics: () => collectRuntimeDiagnostics(application),
    setCheckpoint: (checkpoint: RuntimeVisualCheckpoint) => positionCheckpoint(application, checkpoint),
  });
  (window as RuntimeVisualWindow).__MACHIVERSE_RUNTIME_VISUAL_TEST__ = api;
}

function positionCheckpoint(application: Application, checkpoint: RuntimeVisualCheckpoint): boolean {
  const snapshot = application.state.regionalGeneration.snapshot;
  if (snapshot === null || snapshot.buildings.length === 0 || snapshot.settlements.length === 0) return false;

  const roadNodes = application.state.roadNetwork.snapshot?.nodes ?? [];
  const settlementsWithBuildings = snapshot.settlements.filter((candidate) =>
    snapshot.buildings.some((building) =>
      application.state.regionalGeneration.getSettlementForBuilding(building.buildingId)?.settlementId === candidate.settlementId));
  const settlementCandidates = settlementsWithBuildings.length > 0 ? settlementsWithBuildings : snapshot.settlements;
  const settlement = [...settlementCandidates].sort((left, right) =>
    nearestRoadDistanceSquared(left.x, left.y, roadNodes) - nearestRoadDistanceSquared(right.x, right.y, roadNodes)
      || right.population - left.population
      || (left.settlementId < right.settlementId ? -1 : left.settlementId > right.settlementId ? 1 : 0))[0]!;
  const nearestRoadNode = findNearestRoadNode(settlement.x, settlement.y, roadNodes);

  if (checkpoint === 'street-activity') {
    const focus = nearestRoadNode ?? settlement;
    return application.focus(createStaticNavigationTarget(
      'position',
      'runtime-street-activity',
      { x: focus.x, y: focus.y, z: focus.z },
      0.72,
    ));
  }

  const buildings = snapshot.buildings.filter((building) =>
    application.state.regionalGeneration.getSettlementForBuilding(building.buildingId)?.settlementId === settlement.settlementId);
  if (buildings.length === 0) return false;

  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  for (const building of buildings) {
    minX = Math.min(minX, building.minX); minY = Math.min(minY, building.minY);
    maxX = Math.max(maxX, building.maxX); maxY = Math.max(maxY, building.maxY);
  }
  const span = Math.max(maxX - minX, maxY - minY, 250);
  const distance = clamp(span * 1.15, 450, MAXIMUM_RUNTIME_CHECKPOINT_FOCUS_DISTANCE);
  // Anchor the overview on delivered road activity instead of the potentially
  // kilometre-wide building bounds. The selected settlement is already the one
  // nearest to the current Road snapshot, so this keeps both the city and its
  // transport activity inside the deterministic runtime subscription.
  const focus = nearestRoadNode ?? settlement;
  return application.focus(createStaticNavigationTarget(
    'position',
    'runtime-city-overview',
    { x: focus.x, y: focus.y, z: focus.z },
    250 / distance,
  ));
}

function nearestRoadDistanceSquared(x: number, y: number, nodes: readonly RoadNode[]): number {
  const nearest = findNearestRoadNode(x, y, nodes);
  if (nearest === undefined) return Number.POSITIVE_INFINITY;
  return squaredDistance(x, y, nearest.x, nearest.y);
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

function squaredDistance(leftX: number, leftY: number, rightX: number, rightY: number): number {
  const deltaX = leftX - rightX;
  const deltaY = leftY - rightY;
  return deltaX * deltaX + deltaY * deltaY;
}

function collectRuntimeDiagnostics(application: Application): RuntimeVisualDiagnostics {
  const state = application.state;
  const environment = state.worldEnvironment.snapshot;
  const regional = state.regionalGeneration.snapshot;
  const terrainSampleCount = environment?.terrainSamples.length ?? 0;
  const waterSampleCount = environment?.terrainSamples.reduce((count, sample) => count + (sample.surfaceWater === 0 ? 0 : 1), 0) ?? 0;
  const settlementCount = regional?.settlements.length ?? 0;
  const buildingCount = regional?.buildings.length ?? 0;
  const roadSegmentCount = state.roadNetwork.segmentCount;
  const roadSnapshotSequence = state.roadSnapshotSequence;
  const pedestrianCount = state.pedestrians.size;
  const vehicleCount = state.vehicles.size;
  const trainCount = (application as unknown as RuntimeVisualApplicationInternals).railwayOperations.trainCount;
  const genericAgentCount = state.entities.size;
  const visibleDebugOverlayCount = countVisibleElements(DEBUG_OVERLAY_SELECTOR);
  const japaneseFontReady = isJapaneseFontReady();

  return Object.freeze({
    ready: terrainSampleCount > 0
      && settlementCount > 0
      && buildingCount > 0
      && roadSegmentCount > 0
      && roadSnapshotSequence > 0
      && pedestrianCount > 0
      && vehicleCount > 0
      && trainCount > 0
      && genericAgentCount === 0
      && visibleDebugOverlayCount === 0,
    genericAgentCount,
    terrainSampleCount,
    waterSampleCount,
    settlementCount,
    buildingCount,
    roadSegmentCount,
    roadSnapshotSequence,
    pedestrianCount,
    vehicleCount,
    trainCount,
    visibleDebugOverlayCount,
    japaneseFontReady,
  });
}

function isJapaneseFontReady(): boolean {
  const sample = '日本語漢字かなカナ';
  return ['Noto Sans CJK JP', 'Noto Sans JP'].some((family) =>
    document.fonts.check(`32px "${family}"`, sample) && rendersDifferentlyFromMissingFont(family, sample));
}

function rendersDifferentlyFromMissingFont(family: string, sample: string): boolean {
  const canvas = document.createElement('canvas');
  canvas.width = 512;
  canvas.height = 64;
  const context = canvas.getContext('2d', { willReadFrequently: true });
  if (context === null) return false;

  const render = (font: string): Uint8ClampedArray => {
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.font = `32px ${font}`;
    context.textBaseline = 'top';
    context.fillText(sample, 4, 4);
    return context.getImageData(0, 0, canvas.width, canvas.height).data.slice();
  };

  const target = render(`"${family}", monospace`);
  const missing = render('"__MACHIVERSE_MISSING_FONT__", monospace');
  if (target.length !== missing.length) return false;
  for (let index = 0; index < target.length; index += 1) {
    if (target[index] !== missing[index]) return true;
  }
  return false;
}

function countVisibleElements(selector: string): number {
  let count = 0;
  for (const element of document.querySelectorAll<HTMLElement>(selector)) {
    const style = getComputedStyle(element);
    if (style.display !== 'none' && style.visibility !== 'hidden' && Number.parseFloat(style.opacity || '1') > 0) count += 1;
  }
  return count;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
