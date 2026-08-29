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
    this.globalLayers = [...layers];
  }

  public setZones(zones: readonly AmbientZoneDefinition[]): void {
    for (const zone of zones) {
      validateZone(zone);
    }
    this.zones = [...zones];
  }

  public setParameters(parameters: Readonly<Record<string, number>>): void {
    this.parameters = { ...parameters };
  }

  public async update(listener: Point2D): Promise<void> {
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
    !Number.isFinite(zone.fadeDistance) ||
    zone.fadeDistance < 0
  ) {
    throw new RangeError(`Ambient zone ${zone.id} has invalid bounds.`);
  }
}
