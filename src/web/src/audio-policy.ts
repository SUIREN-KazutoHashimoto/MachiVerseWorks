export interface Point2D {
  readonly x: number;
  readonly y: number;
}

export interface VoiceCandidate {
  readonly id: string;
  readonly position: Point2D;
  readonly priority: number;
}

export interface AmbientLayerDefinition {
  readonly key: string;
  readonly cueId: string;
  readonly gain: number;
  readonly parameter?: string;
}

export interface AmbientZoneDefinition {
  readonly id: string;
  readonly minX: number;
  readonly minY: number;
  readonly maxX: number;
  readonly maxY: number;
  readonly priority: number;
  readonly fadeDistance: number;
  readonly layers: readonly AmbientLayerDefinition[];
}

export interface ResolvedAmbientLayer {
  readonly key: string;
  readonly cueId: string;
  readonly gain: number;
}

export function selectVoiceIds(
  candidates: readonly VoiceCandidate[],
  listener: Point2D,
  budget: number,
): ReadonlySet<string> {
  if (!Number.isInteger(budget) || budget < 0) {
    throw new RangeError('Voice budget must be a non-negative integer.');
  }

  const ranked = candidates.map((candidate) => ({
    candidate,
    distanceSquared:
      (candidate.position.x - listener.x) ** 2 +
      (candidate.position.y - listener.y) ** 2,
  }));
  ranked.sort((left, right) => {
    const priorityDifference = right.candidate.priority - left.candidate.priority;
    if (priorityDifference !== 0) {
      return priorityDifference;
    }
    if (left.distanceSquared !== right.distanceSquared) {
      return left.distanceSquared - right.distanceSquared;
    }
    return left.candidate.id.localeCompare(right.candidate.id);
  });

  return new Set(ranked.slice(0, budget).map((entry) => entry.candidate.id));
}

export function resolveAmbientLayers(
  globalLayers: readonly AmbientLayerDefinition[],
  zones: readonly AmbientZoneDefinition[],
  listener: Point2D,
  parameters: Readonly<Record<string, number>> = {},
): readonly ResolvedAmbientLayer[] {
  const resolved = new Map<string, ResolvedAmbientLayer>();

  for (const layer of globalLayers) {
    accumulateLayer(resolved, layer, clamp01(resolveParameter(layer, parameters)));
  }

  const activeZones = zones
    .map((zone) => ({ zone, weight: calculateZoneWeight(zone, listener) }))
    .filter((entry) => entry.weight > 0);
  const highestPriority = activeZones.reduce(
    (current, entry) => Math.max(current, entry.zone.priority),
    Number.NEGATIVE_INFINITY,
  );

  for (const { zone, weight } of activeZones) {
    const priorityWeight = 2 ** Math.min(0, zone.priority - highestPriority);
    for (const layer of zone.layers) {
      const parameterWeight = resolveParameter(layer, parameters);
      accumulateLayer(resolved, layer, clamp01(weight * priorityWeight * parameterWeight));
    }
  }

  return [...resolved.values()].sort((left, right) => left.key.localeCompare(right.key));
}

function calculateZoneWeight(zone: AmbientZoneDefinition, listener: Point2D): number {
  if (
    listener.x < zone.minX ||
    listener.x > zone.maxX ||
    listener.y < zone.minY ||
    listener.y > zone.maxY
  ) {
    return 0;
  }

  if (zone.fadeDistance <= 0) {
    return 1;
  }

  const edgeDistance = Math.min(
    listener.x - zone.minX,
    zone.maxX - listener.x,
    listener.y - zone.minY,
    zone.maxY - listener.y,
  );
  return clamp01(edgeDistance / zone.fadeDistance);
}

function resolveParameter(
  layer: AmbientLayerDefinition,
  parameters: Readonly<Record<string, number>>,
): number {
  if (layer.parameter === undefined) {
    return 1;
  }

  return clamp01(parameters[layer.parameter] ?? 0);
}

function accumulateLayer(
  target: Map<string, ResolvedAmbientLayer>,
  layer: AmbientLayerDefinition,
  weight: number,
): void {
  const gain = clamp01(layer.gain * weight);
  if (gain <= 0) {
    return;
  }

  const existing = target.get(layer.key);
  if (existing === undefined) {
    target.set(layer.key, { key: layer.key, cueId: layer.cueId, gain });
    return;
  }

  if (existing.cueId !== layer.cueId) {
    if (gain > existing.gain) {
      target.set(layer.key, { key: layer.key, cueId: layer.cueId, gain });
    }
    return;
  }

  target.set(layer.key, { ...existing, gain: clamp01(existing.gain + gain) });
}

function clamp01(value: number): number {
  return Math.min(1, Math.max(0, value));
}
