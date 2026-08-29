import {
  resolveAmbientLayers,
  type AmbientLayerDefinition,
  type AmbientZoneDefinition,
  type Point2D,
} from './audio-policy.ts';
import type { AudioEngine } from './audio-engine.ts';

export class AmbientSystem {
  private globalLayers: readonly AmbientLayerDefinition[] = [];
  private zones: readonly AmbientZoneDefinition[] = [];
  private parameters: Readonly<Record<string, number>> = {};
  private activeKeys = new Set<string>();

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

  public async update(listener: Point2D): Promise<void> {
    validatePoint(listener, 'Ambient listener');
    const mix = resolveAmbientLayers(this.globalLayers, this.zones, listener, this.parameters);
    const nextKeys = new Set(mix.map((layer) => layer.key));
    await Promise.all(
      mix.map((layer) => this.audio.setAmbientLayer(layer.key, layer.cueId, layer.gain, 1.5)),
    );
    await Promise.all(
      [...this.activeKeys]
        .filter((key) => !nextKeys.has(key))
        .map((key) => this.audio.clearAmbientLayer(key, 1.5)),
    );
    this.activeKeys = nextKeys;
  }

}

function validateZone(zone: AmbientZoneDefinition): void {
  if (
    !Number.isFinite(zone.minX) ||
    !Number.isFinite(zone.minY) ||
    !Number.isFinite(zone.maxX) ||
    !Number.isFinite(zone.maxY) ||
    zone.maxX < zone.minX ||
    zone.maxY < zone.minY ||
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

function validatePoint(point: Point2D, label: string): void {
  validateFinite(point.x, `${label} x`);
  validateFinite(point.y, `${label} y`);
}

function validateFinite(value: number, label: string): void {
  if (!Number.isFinite(value)) {
    throw new RangeError(`${label} must be finite.`);
  }
}
