import type { Application } from './application.ts';
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

export function installRuntimeVisualTest(application: Application): void {
  const api: RuntimeVisualTestApi = Object.freeze({
    getDiagnostics: () => collectRuntimeDiagnostics(application),
    setCheckpoint: (checkpoint: RuntimeVisualCheckpoint) => positionCheckpoint(application, checkpoint),
  });
  (window as RuntimeVisualWindow).__MACHIVERSE_RUNTIME_VISUAL_TEST__ = api;
}

function positionCheckpoint(application: Application, checkpoint: RuntimeVisualCheckpoint): boolean {
  const snapshot = application.state.regionalGeneration.snapshot;
  if (snapshot === null || snapshot.buildings.length === 0) return false;

  if (checkpoint === 'street-activity' && snapshot.settlements.length > 0) {
    const settlement = [...snapshot.settlements].sort((left, right) => right.population - left.population)[0]!;
    return application.focus(createStaticNavigationTarget(
      'position',
      'runtime-street-activity',
      { x: settlement.x, y: settlement.y, z: settlement.z },
      0.72,
    ));
  }

  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let minZ = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  let maxZ = Number.NEGATIVE_INFINITY;
  for (const building of snapshot.buildings) {
    minX = Math.min(minX, building.minX); minY = Math.min(minY, building.minY); minZ = Math.min(minZ, building.minZ);
    maxX = Math.max(maxX, building.maxX); maxY = Math.max(maxY, building.maxY); maxZ = Math.max(maxZ, building.maxZ);
  }
  const span = Math.max(maxX - minX, maxY - minY, 250);
  const distance = clamp(span * 1.15, 450, 8_000);
  return application.focus(createStaticNavigationTarget(
    'position',
    'runtime-city-overview',
    { x: (minX + maxX) * 0.5, y: (minY + maxY) * 0.5, z: (minZ + maxZ) * 0.5 },
    250 / distance,
  ));
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
  const pedestrianCount = state.pedestrians.size;
  const vehicleCount = state.vehicles.size;
  const trainCount = (application as unknown as RuntimeVisualApplicationInternals).railwayOperations.trainCount;
  const genericAgentCount = state.entities.size;
  const visibleDebugOverlayCount = countVisibleElements(DEBUG_OVERLAY_SELECTOR);
  const japaneseFontReady = document.fonts.check('16px "Noto Sans CJK JP"', '日本語')
    || document.fonts.check('16px "Noto Sans JP"', '日本語');

  return Object.freeze({
    ready: terrainSampleCount > 0
      && settlementCount > 0
      && buildingCount > 0
      && roadSegmentCount > 0
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
    pedestrianCount,
    vehicleCount,
    trainCount,
    visibleDebugOverlayCount,
    japaneseFontReady,
  });
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
