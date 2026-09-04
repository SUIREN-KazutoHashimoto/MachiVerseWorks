import type { Application } from './application.ts';
import { createStaticNavigationTarget } from './view-navigation.ts';

export type RuntimeVisualCheckpoint = 'agent-cloud' | 'worst-grounding';

interface AgentTerrainObservation {
  readonly agentId: string;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly terrainZ: number;
  readonly deltaMeters: number;
}

export interface RuntimeVisualDiagnostics {
  readonly ready: boolean;
  readonly agentCount: number;
  readonly terrainSampleCount: number;
  readonly agentZ: Readonly<{ minimum: number | null; maximum: number | null }>;
  readonly agentTerrainDelta: Readonly<{
    matchedCount: number;
    withinHalfMeterCount: number;
    aboveFiveMetersCount: number;
    belowFiveMetersCount: number;
    minimumMeters: number | null;
    maximumMeters: number | null;
    maximumAbsoluteMeters: number | null;
    worst: AgentTerrainObservation | null;
  }>;
}

export interface RuntimeVisualTestApi {
  getDiagnostics(): RuntimeVisualDiagnostics;
  setCheckpoint(checkpoint: RuntimeVisualCheckpoint): boolean;
}

type RuntimeVisualWindow = Window & {
  __MACHIVERSE_RUNTIME_VISUAL_TEST__?: RuntimeVisualTestApi;
};

export function installRuntimeVisualTest(application: Application): void {
  const api: RuntimeVisualTestApi = Object.freeze({
    getDiagnostics: () => collectRuntimeObservation(application).diagnostics,
    setCheckpoint: (checkpoint: RuntimeVisualCheckpoint) => positionCheckpoint(application, checkpoint),
  });
  (window as RuntimeVisualWindow).__MACHIVERSE_RUNTIME_VISUAL_TEST__ = api;
}

function positionCheckpoint(application: Application, checkpoint: RuntimeVisualCheckpoint): boolean {
  const observation = collectRuntimeObservation(application);
  if (!observation.diagnostics.ready || observation.agents.length === 0) return false;

  if (checkpoint === 'agent-cloud') {
    let minX = Number.POSITIVE_INFINITY;
    let minY = Number.POSITIVE_INFINITY;
    let minZ = Number.POSITIVE_INFINITY;
    let maxX = Number.NEGATIVE_INFINITY;
    let maxY = Number.NEGATIVE_INFINITY;
    let maxZ = Number.NEGATIVE_INFINITY;
    for (const agent of observation.agents) {
      minX = Math.min(minX, agent.x); maxX = Math.max(maxX, agent.x);
      minY = Math.min(minY, agent.y); maxY = Math.max(maxY, agent.y);
      minZ = Math.min(minZ, agent.z); maxZ = Math.max(maxZ, agent.z);
    }
    const span = Math.max(maxX - minX, maxY - minY, maxZ - minZ, 100);
    const distance = clamp(span * 1.35, 250, 8_000);
    return application.focus(createStaticNavigationTarget(
      'position',
      'runtime-agent-cloud',
      { x: (minX + maxX) * 0.5, y: (minY + maxY) * 0.5, z: (minZ + maxZ) * 0.5 },
      250 / distance,
    ));
  }

  const worst = observation.diagnostics.agentTerrainDelta.worst;
  if (worst === null) return false;
  const verticalGap = Math.abs(worst.deltaMeters);
  const distance = clamp(Math.max(90, verticalGap * 3), 90, 4_000);
  return application.focus(createStaticNavigationTarget(
    'position',
    `runtime-worst-grounding-${worst.agentId}`,
    { x: worst.x, y: worst.y, z: (worst.z + worst.terrainZ) * 0.5 },
    250 / distance,
  ));
}

function collectRuntimeObservation(application: Application): {
  readonly agents: readonly Readonly<{ readonly x: number; readonly y: number; readonly z: number }>[];
  readonly diagnostics: RuntimeVisualDiagnostics;
} {
  const state = application.state;
  const agents = [...state.entities.sample(performance.now())];
  const terrainSampleCount = state.worldEnvironment.snapshot?.terrainSamples.length ?? 0;

  let minimumAgentZ = Number.POSITIVE_INFINITY;
  let maximumAgentZ = Number.NEGATIVE_INFINITY;
  let matchedCount = 0;
  let withinHalfMeterCount = 0;
  let aboveFiveMetersCount = 0;
  let belowFiveMetersCount = 0;
  let minimumDelta = Number.POSITIVE_INFINITY;
  let maximumDelta = Number.NEGATIVE_INFINITY;
  let maximumAbsoluteDelta = Number.NEGATIVE_INFINITY;
  let worst: AgentTerrainObservation | null = null;

  for (const agent of agents) {
    minimumAgentZ = Math.min(minimumAgentZ, agent.z);
    maximumAgentZ = Math.max(maximumAgentZ, agent.z);
    const terrainZ = state.worldEnvironment.getNearestTerrainElevation(agent.x, agent.y);
    if (terrainZ === undefined) continue;

    const deltaMeters = agent.z - terrainZ;
    const absoluteDelta = Math.abs(deltaMeters);
    matchedCount += 1;
    if (absoluteDelta <= 0.5) withinHalfMeterCount += 1;
    if (deltaMeters > 5) aboveFiveMetersCount += 1;
    if (deltaMeters < -5) belowFiveMetersCount += 1;
    minimumDelta = Math.min(minimumDelta, deltaMeters);
    maximumDelta = Math.max(maximumDelta, deltaMeters);
    if (absoluteDelta > maximumAbsoluteDelta) {
      maximumAbsoluteDelta = absoluteDelta;
      worst = Object.freeze({
        agentId: agent.agentId.toString(),
        x: agent.x,
        y: agent.y,
        z: agent.z,
        terrainZ,
        deltaMeters,
      });
    }
  }

  return {
    agents,
    diagnostics: Object.freeze({
      ready: agents.length > 0 && terrainSampleCount > 0 && matchedCount > 0,
      agentCount: agents.length,
      terrainSampleCount,
      agentZ: Object.freeze({
        minimum: agents.length === 0 ? null : minimumAgentZ,
        maximum: agents.length === 0 ? null : maximumAgentZ,
      }),
      agentTerrainDelta: Object.freeze({
        matchedCount,
        withinHalfMeterCount,
        aboveFiveMetersCount,
        belowFiveMetersCount,
        minimumMeters: matchedCount === 0 ? null : minimumDelta,
        maximumMeters: matchedCount === 0 ? null : maximumDelta,
        maximumAbsoluteMeters: matchedCount === 0 ? null : maximumAbsoluteDelta,
        worst,
      }),
    }),
  };
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
