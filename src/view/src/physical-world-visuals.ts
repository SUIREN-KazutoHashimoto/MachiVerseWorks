import {
  GeographicFeatureType,
  SurfaceWaterKind,
  TerrainMaterialKind,
} from './world-environment-protocol.ts';

export interface TerrainMaterialVisual {
  readonly color: number;
  readonly label: string;
}

export interface SurfaceWaterVisual {
  readonly color: number;
  readonly pointSize: number;
  readonly label: string;
}

export interface GeographicFeatureVisual {
  readonly color: number;
  readonly label: string;
}

export function resolveTerrainMaterialVisual(kind: TerrainMaterialKind): TerrainMaterialVisual {
  switch (kind) {
    case TerrainMaterialKind.Water: return { color: 0x4f88b5, label: 'water' };
    case TerrainMaterialKind.Sand: return { color: 0xd6c18b, label: 'sand' };
    case TerrainMaterialKind.Soil: return { color: 0x6f8d55, label: 'soil' };
    case TerrainMaterialKind.Rock: return { color: 0x7c8387, label: 'rock' };
    case TerrainMaterialKind.Snow: return { color: 0xf5f7f8, label: 'snow' };
    case TerrainMaterialKind.Gravel: return { color: 0xa5a9a6, label: 'gravel' };
  }
}

export function resolveSurfaceWaterVisual(kind: SurfaceWaterKind): SurfaceWaterVisual | null {
  switch (kind) {
    case SurfaceWaterKind.None: return null;
    case SurfaceWaterKind.Ocean: return { color: 0x2f6f9f, pointSize: 18, label: 'ocean' };
    case SurfaceWaterKind.Lake: return { color: 0x3a82b5, pointSize: 16, label: 'lake' };
    case SurfaceWaterKind.River: return { color: 0x2f92c7, pointSize: 14, label: 'river' };
    case SurfaceWaterKind.Tributary: return { color: 0x54add3, pointSize: 12, label: 'tributary' };
    case SurfaceWaterKind.Floodplain: return { color: 0x76bfc4, pointSize: 10, label: 'floodplain' };
  }
}

export function resolveGeographicFeatureVisual(kind: GeographicFeatureType): GeographicFeatureVisual {
  switch (kind) {
    case GeographicFeatureType.Mountain: return { color: 0xe2e8f0, label: 'mountain' };
    case GeographicFeatureType.MountainRange: return { color: 0xcbd5e1, label: 'mountain-range' };
    case GeographicFeatureType.River: return { color: 0x0284c7, label: 'river' };
    case GeographicFeatureType.Tributary: return { color: 0x38bdf8, label: 'tributary' };
    case GeographicFeatureType.Lake: return { color: 0x2563eb, label: 'lake' };
    case GeographicFeatureType.Valley: return { color: 0xa3e635, label: 'valley' };
    case GeographicFeatureType.Basin: return { color: 0x84cc16, label: 'basin' };
    case GeographicFeatureType.Plain: return { color: 0xb6e45a, label: 'plain' };
    case GeographicFeatureType.Plateau: return { color: 0xeab308, label: 'plateau' };
    case GeographicFeatureType.Pass: return { color: 0xf59e0b, label: 'pass' };
    case GeographicFeatureType.Cape: return { color: 0x22d3ee, label: 'cape' };
    case GeographicFeatureType.Bay: return { color: 0x06b6d4, label: 'bay' };
    case GeographicFeatureType.Coast: return { color: 0x67e8f9, label: 'coast' };
    case GeographicFeatureType.Island: return { color: 0x4ade80, label: 'island' };
    case GeographicFeatureType.Peninsula: return { color: 0x2dd4bf, label: 'peninsula' };
    case GeographicFeatureType.Cave: return { color: 0xc4b5fd, label: 'cave' };
  }
}
