import {
  resolveAmbientLayers,
  type AmbientLayerDefinition,
  type AmbientZoneDefinition,
  type Point3D,
  type ResolvedAmbientLayer,
} from './audio-policy.ts';
import type { AudioEngine } from './audio-engine.ts';

const AmbientFadeSeconds = 1.5;

export class AmbientSystem {
  private globalLayers: readonly AmbientLayerDefinition[] = [];
  private zones: readonly AmbientZoneDefinition[] = [];
  private parameters: Readonly<Record<string, number>> = {};
  private activeLayers = new Map<string, ResolvedAmbientLayer>();

  public constructor(private readonly audio: AudioEngine) {}

  public setGlobalLayers(layers: readonly AmbientLayerDefinition[]): void {
    for (const layer of layers) {
      validateLayer(layer, 'Global ambient layer');
    }
    this.globalLayers = [...layers];
  }

  public setZones(zones: readonly AmbientZoneDefinition[]): void {
    for (const zone of zones) {
      validateZone(zone);
    }
    this.zones = [...zones];
  }

  public setParameters(parameters: Readonly<Record<string, number>>): void {
    for (const [name, value] of Object.entries(parameters)) {
      validateFinite(value, `Ambient parameter ${name}`);
    }
    this.parameters = { ...parameters };
  }

  public async update(listener: Point3D): Promise<void> {
    validatePoint(listener, 'Ambient listener');
    const mix = resolveAmbientLayers(this.globalLayers, this.zones, listener, this.parameters);
    const previousLayers = new Map(this.activeLayers);
    const desiredLayers = new Map(mix.map((layer) => [layer.key, layer]));
    const appliedLayers: ResolvedAmbientLayer[] = [];
    let pendingLayer: ResolvedAmbientLayer | undefined;

    try {
      for (const layer of mix) {
        pendingLayer = layer;
        await this.audio.setAmbientLayer(layer.key, layer.cueId, layer.gain, AmbientFadeSeconds);
        appliedLayers.push(layer);
        pendingLayer = undefined;
      }
    } catch (error) {
      const rollbackFailures: unknown[] = [];
      const trackedLayers = new Map(previousLayers);
      const rollbackLayers = pendingLayer === undefined
        ? [...appliedLayers].reverse()
        : [pendingLayer, ...[...appliedLayers].reverse()];
      for (const layer of rollbackLayers) {
        const previous = previousLayers.get(layer.key);
        try {
          if (previous === undefined) {
            await this.audio.clearAmbientLayer(layer.key, AmbientFadeSeconds);
          } else {
            await this.audio.setAmbientLayer(previous.key, previous.cueId, previous.gain, AmbientFadeSeconds);
          }
        } catch (rollbackError) {
          rollbackFailures.push(rollbackError);
          if (previous === undefined) {
            trackedLayers.set(layer.key, layer);
          }
        }
      }
      this.activeLayers = trackedLayers;
      if (rollbackFailures.length > 0) {
        throw new AggregateError([error, ...rollbackFailures], 'Ambient layer update failed and rollback was incomplete.');
      }
      throw error;
    }

    const staleLayers = [...previousLayers.values()].filter((layer) => !desiredLayers.has(layer.key));
    for (let index = 0; index < staleLayers.length; index += 1) {
      const stale = staleLayers[index]!;
      try {
        await this.audio.clearAmbientLayer(stale.key, AmbientFadeSeconds);
      } catch (error) {
        const trackedLayers = new Map(desiredLayers);
        for (let remaining = index; remaining < staleLayers.length; remaining += 1) {
          const uncleared = staleLayers[remaining]!;
          trackedLayers.set(uncleared.key, uncleared);
        }
        this.activeLayers = trackedLayers;
        throw error;
      }
    }

    this.activeLayers = desiredLayers;
  }
}

function validateZone(zone: AmbientZoneDefinition): void {
  if (
    !Number.isFinite(zone.minX) ||
    !Number.isFinite(zone.minY) ||
    !Number.isFinite(zone.minZ) ||
    !Number.isFinite(zone.maxX) ||
    !Number.isFinite(zone.maxY) ||
    !Number.isFinite(zone.maxZ) ||
    zone.maxX < zone.minX ||
    zone.maxY < zone.minY ||
    zone.maxZ < zone.minZ ||
    !Number.isFinite(zone.priority) ||
    !Number.isFinite(zone.fadeDistance) ||
    zone.fadeDistance < 0
  ) {
    throw new RangeError(`Ambient zone ${zone.id} has invalid bounds or priority.`);
  }
  for (const layer of zone.layers) {
    validateLayer(layer, `Ambient zone ${zone.id} layer`);
  }
}

function validateLayer(layer: AmbientLayerDefinition, label: string): void {
  validateFinite(layer.gain, `${label} ${layer.key} gain`);
}

function validatePoint(point: Point3D, label: string): void {
  if (!Number.isFinite(point.x) || !Number.isFinite(point.y) || !Number.isFinite(point.z)) {
    throw new RangeError(`${label} coordinates must be finite.`);
  }
}

function validateFinite(value: number, label: string): void {
  if (!Number.isFinite(value)) {
    throw new RangeError(`${label} must be finite.`);
  }
}
