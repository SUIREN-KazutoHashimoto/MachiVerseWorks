import * as THREE from 'three';

export interface ViewRenderingQualityProfile {
  readonly backgroundColor: number;
  readonly fogColor: number;
  readonly fogNear: number;
  readonly fogFar: number;
  readonly fogScaleReferenceAltitude: number;
  readonly exposure: number;
  readonly hemisphereSkyColor: number;
  readonly hemisphereGroundColor: number;
  readonly hemisphereIntensity: number;
  readonly sunColor: number;
  readonly sunIntensity: number;
  readonly shadowMapSize: number;
  readonly shadowDistance: number;
  readonly shadowBias: number;
  readonly shadowNormalBias: number;
}

/**
 * View-local presentation quality. These values must never affect Simulation fidelity or Gateway delivery semantics.
 */
export const DEFAULT_VIEW_RENDERING_QUALITY: Readonly<ViewRenderingQualityProfile> = Object.freeze({
  backgroundColor: 0xb9d5e8,
  fogColor: 0xc7d7e2,
  fogNear: 1_800,
  fogFar: 10_000,
  fogScaleReferenceAltitude: 2_000,
  exposure: 1.02,
  hemisphereSkyColor: 0xeaf6ff,
  hemisphereGroundColor: 0x61705c,
  hemisphereIntensity: 1.2,
  sunColor: 0xfff3d6,
  sunIntensity: 1.7,
  shadowMapSize: 2_048,
  shadowDistance: 4_000,
  shadowBias: -0.00012,
  shadowNormalBias: 0.8,
});

export function configureRendererPresentation(
  renderer: THREE.WebGLRenderer,
  quality: ViewRenderingQualityProfile = DEFAULT_VIEW_RENDERING_QUALITY,
): void {
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = quality.exposure;
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
}

export function installEnvironmentLighting(
  scene: THREE.Scene,
  quality: ViewRenderingQualityProfile = DEFAULT_VIEW_RENDERING_QUALITY,
): Readonly<{ hemisphere: THREE.HemisphereLight; sun: THREE.DirectionalLight }> {
  scene.background = new THREE.Color(quality.backgroundColor);
  scene.fog = new THREE.Fog(quality.fogColor, quality.fogNear, quality.fogFar);

  const hemisphere = new THREE.HemisphereLight(
    quality.hemisphereSkyColor,
    quality.hemisphereGroundColor,
    quality.hemisphereIntensity,
  );
  hemisphere.name = 'view-environment-hemisphere-light';

  const sun = new THREE.DirectionalLight(quality.sunColor, quality.sunIntensity);
  sun.name = 'view-environment-directional-light';
  sun.position.set(-1_800, 2_800, 1_600);
  sun.castShadow = true;
  sun.shadow.mapSize.set(quality.shadowMapSize, quality.shadowMapSize);
  sun.shadow.camera.near = 50;
  sun.shadow.camera.far = quality.shadowDistance * 2;
  sun.shadow.camera.left = -quality.shadowDistance;
  sun.shadow.camera.right = quality.shadowDistance;
  sun.shadow.camera.top = quality.shadowDistance;
  sun.shadow.camera.bottom = -quality.shadowDistance;
  sun.shadow.bias = quality.shadowBias;
  sun.shadow.normalBias = quality.shadowNormalBias;

  scene.add(hemisphere, sun);
  return Object.freeze({ hemisphere, sun });
}

/**
 * Fog is presentation-only and follows observation scale. A fixed 10 km fog plane makes a
 * continent-scale camera completely opaque, while disabling fog loses urban depth cues.
 */
export function updateEnvironmentFog(
  scene: THREE.Scene,
  camera: THREE.PerspectiveCamera,
  quality: ViewRenderingQualityProfile = DEFAULT_VIEW_RENDERING_QUALITY,
): void {
  if (!(scene.fog instanceof THREE.Fog)) return;
  const altitude = Math.max(0, Math.abs(camera.position.y));
  const scale = Math.max(1, altitude / quality.fogScaleReferenceAltitude);
  scene.fog.near = quality.fogNear * scale;
  scene.fog.far = quality.fogFar * scale;
}
