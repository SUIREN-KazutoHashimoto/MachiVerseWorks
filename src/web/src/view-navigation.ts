import type { ReadonlyEntityStore } from './entity-store.ts';
import type { MutablePositionBuffer } from './visual-interpolation-state.ts';

const DEFAULT_MINIMUM_ZOOM = 0.25;
const DEFAULT_MAXIMUM_ZOOM = 8;
const DEFAULT_ROTATION_RADIANS_PER_PIXEL = 0.004;
const DEFAULT_ALTITUDE_METERS_PER_PIXEL = 2;
const FOCUS_FALLBACK_DISTANCE = 250;
const DIRECTION_EPSILON = 1e-9;
const MINIMUM_FOCUS_DISTANCE = 1;
const MAXIMUM_FOCUS_DISTANCE_RATIO = 0.9;

export interface WorldPosition {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

export type ViewNavigationTargetKind = 'entity' | 'settlement' | 'geographic-feature' | 'position';

export interface ViewNavigationTarget {
  readonly kind: ViewNavigationTargetKind;
  readonly id?: bigint | string;
  readonly preferredZoom?: number;
  writePosition(now: number, target: MutablePositionBuffer): boolean;
}

export interface ViewNavigationOptions {
  readonly minimumZoom?: number;
  readonly maximumZoom?: number;
  readonly rotationRadiansPerPixel?: number;
  readonly altitudeMetersPerPixel?: number;
}

interface MutablePosition {
  x: number;
  y: number;
  z: number;
}

export interface ViewNavigationCamera {
  readonly position: MutablePosition;
  readonly matrixWorld: { readonly elements: ArrayLike<number> };
  readonly left: number;
  readonly right: number;
  readonly top: number;
  readonly bottom: number;
  readonly near: number;
  readonly far: number;
  zoom: number;
  lookAt(x: number, y: number, z: number): void;
  updateMatrixWorld(force?: boolean): void;
  updateProjectionMatrix(): void;
}

export class ViewNavigationController {
  private readonly minimumZoom: number;
  private readonly maximumZoom: number;
  private readonly rotationRadiansPerPixel: number;
  private readonly altitudeMetersPerPixel: number;
  private readonly sampledTargetPosition = new Float64Array(3);
  private readonly sampledFocusPosition = new Float64Array(3);
  private readonly sampledDirection = new Float64Array(3);
  private followTarget: ViewNavigationTarget | null = null;
  private focusAltitude = 0;
  private panPointerId: number | null = null;
  private orbitPointerId: number | null = null;
  private lastPointerX = 0;
  private lastPointerY = 0;

  public constructor(
    private readonly camera: ViewNavigationCamera,
    private readonly surface: HTMLCanvasElement,
    options: ViewNavigationOptions = {},
  ) {
    this.minimumZoom = validatePositive(options.minimumZoom ?? DEFAULT_MINIMUM_ZOOM, 'minimum zoom');
    this.maximumZoom = validatePositive(options.maximumZoom ?? DEFAULT_MAXIMUM_ZOOM, 'maximum zoom');
    if (this.maximumZoom < this.minimumZoom) throw new RangeError('Maximum zoom must be greater than or equal to minimum zoom.');
    this.rotationRadiansPerPixel = validatePositive(options.rotationRadiansPerPixel ?? DEFAULT_ROTATION_RADIANS_PER_PIXEL, 'rotation sensitivity');
    this.altitudeMetersPerPixel = validatePositive(options.altitudeMetersPerPixel ?? DEFAULT_ALTITUDE_METERS_PER_PIXEL, 'altitude sensitivity');
    validatePositive(this.camera.near, 'camera near plane');
    validatePositive(this.camera.far, 'camera far plane');
    if (this.camera.far <= this.camera.near) throw new RangeError('Camera far plane must be greater than the near plane.');
    this.installControls();
  }

  public dispose(): void {
    this.surface.removeEventListener('pointerdown', this.handlePointerDown, true);
    this.surface.removeEventListener('pointermove', this.handlePointerMove, true);
    this.surface.removeEventListener('pointerup', this.handlePointerRelease, true);
    this.surface.removeEventListener('pointercancel', this.handlePointerRelease, true);
    this.surface.removeEventListener('wheel', this.handleWheel, true);
    this.surface.removeEventListener('contextmenu', this.handleContextMenu);
    this.followTarget = null;
    this.panPointerId = null;
    this.orbitPointerId = null;
  }

  public update(now: number): void {
    const target = this.followTarget;
    if (target === null || !target.writePosition(now, this.sampledTargetPosition)) return;
    this.centerOnSampledPosition(this.sampledTargetPosition);
  }

  /** Moves the camera along its current screen-right and screen-up axes projected onto the World X/Y plane. */
  public pan(deltaRight: number, deltaUp: number): void {
    validateFinite(deltaRight, 'pan right');
    validateFinite(deltaUp, 'pan up');
    this.followTarget = null;
    this.camera.updateMatrixWorld(true);
    const elements = this.camera.matrixWorld.elements;
    const right = normalizeHorizontal(Number(elements[0]), Number(elements[2]), 1, 0);
    const up = normalizeHorizontal(Number(elements[4]), Number(elements[6]), -right.z, right.x);
    this.camera.position.x += right.x * deltaRight + up.x * deltaUp;
    this.camera.position.z += right.z * deltaRight + up.z * deltaUp;
    this.camera.updateMatrixWorld(true);
  }

  public zoomBy(factor: number): void {
    validatePositive(factor, 'zoom factor');
    this.setZoom(this.camera.zoom * factor);
  }

  public setZoom(zoom: number): void {
    validatePositive(zoom, 'zoom');
    this.camera.zoom = clamp(zoom, this.minimumZoom, this.maximumZoom);
    this.camera.updateProjectionMatrix();
  }

  public rotateBy(radians: number): void {
    validateFinite(radians, 'rotation');
    if (radians === 0) return;
    writeCameraWorldDirection(this.camera, this.sampledDirection);
    const cosine = Math.cos(radians);
    const sine = Math.sin(radians);
    const rotatedX = this.sampledDirection[0] * cosine + this.sampledDirection[2] * sine;
    const rotatedZ = -this.sampledDirection[0] * sine + this.sampledDirection[2] * cosine;
    this.camera.lookAt(
      this.camera.position.x + rotatedX,
      this.camera.position.y + this.sampledDirection[1],
      this.camera.position.z + rotatedZ,
    );
    this.camera.updateMatrixWorld(true);
  }

  public adjustAltitude(deltaMeters: number): void {
    validateFinite(deltaMeters, 'altitude delta');
    writeCameraWorldDirection(this.camera, this.sampledDirection);
    const downwardComponent = -this.sampledDirection[1];
    if (downwardComponent <= DIRECTION_EPSILON) throw new RangeError('Camera must face downward to adjust observation altitude.');

    const minimumDistance = Math.max(MINIMUM_FOCUS_DISTANCE, this.camera.near * 2);
    const maximumDistance = this.camera.far * MAXIMUM_FOCUS_DISTANCE_RATIO;
    if (maximumDistance <= minimumDistance) throw new RangeError('Camera clipping range is too small for altitude navigation.');

    const minimumAltitude = this.focusAltitude + downwardComponent * minimumDistance;
    const maximumAltitude = this.focusAltitude + downwardComponent * maximumDistance;
    const nextAltitude = this.camera.position.y + deltaMeters;
    if (!Number.isFinite(nextAltitude)) throw new RangeError('Camera altitude must remain finite.');
    this.camera.position.y = clamp(nextAltitude, minimumAltitude, maximumAltitude);
    this.camera.updateMatrixWorld(true);
  }

  public jump(target: ViewNavigationTarget, now = performance.now()): boolean {
    if (!target.writePosition(now, this.sampledTargetPosition)) return false;
    this.followTarget = null;
    this.centerOnSampledPosition(this.sampledTargetPosition);
    return true;
  }

  public focus(target: ViewNavigationTarget, now = performance.now()): boolean {
    if (!this.jump(target, now)) return false;
    if (target.preferredZoom !== undefined) this.setZoom(target.preferredZoom);
    return true;
  }

  public follow(target: ViewNavigationTarget, now = performance.now()): boolean {
    if (!target.writePosition(now, this.sampledTargetPosition)) return false;
    this.centerOnSampledPosition(this.sampledTargetPosition);
    if (target.preferredZoom !== undefined) this.setZoom(target.preferredZoom);
    this.followTarget = target;
    return true;
  }

  public clearFollow(): void {
    this.followTarget = null;
  }

  public focusEntity(entityId: bigint, store: ReadonlyEntityStore, now = performance.now(), preferredZoom?: number): boolean {
    return this.focus(createEntityNavigationTarget(entityId, store, preferredZoom), now);
  }

  public followEntity(entityId: bigint, store: ReadonlyEntityStore, now = performance.now(), preferredZoom?: number): boolean {
    return this.follow(createEntityNavigationTarget(entityId, store, preferredZoom), now);
  }

  private centerOnSampledPosition(position: MutablePositionBuffer): void {
    validateSampledWorldPosition(position);
    const simulationAltitude = position[2];
    if (writeCameraFocusAtSimulationAltitude(this.camera, simulationAltitude, this.sampledFocusPosition)) {
      this.camera.position.x += position[0] - this.sampledFocusPosition[0];
      this.camera.position.z += position[1] - this.sampledFocusPosition[1];
      this.camera.position.y += simulationAltitude - this.sampledFocusPosition[2];
    } else {
      writeCameraWorldDirection(this.camera, this.sampledDirection);
      this.camera.position.x = position[0] - this.sampledDirection[0] * FOCUS_FALLBACK_DISTANCE;
      this.camera.position.z = position[1] - this.sampledDirection[2] * FOCUS_FALLBACK_DISTANCE;
      this.camera.position.y = simulationAltitude - this.sampledDirection[1] * FOCUS_FALLBACK_DISTANCE;
    }
    this.focusAltitude = simulationAltitude;
    this.camera.updateMatrixWorld(true);
  }

  private installControls(): void {
    // Capture-phase handlers own Phase 2 camera input before WorldView's legacy target-phase handlers.
    this.surface.addEventListener('pointerdown', this.handlePointerDown, true);
    this.surface.addEventListener('pointermove', this.handlePointerMove, true);
    this.surface.addEventListener('pointerup', this.handlePointerRelease, true);
    this.surface.addEventListener('pointercancel', this.handlePointerRelease, true);
    this.surface.addEventListener('wheel', this.handleWheel, { capture: true, passive: false });
    this.surface.addEventListener('contextmenu', this.handleContextMenu);
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.button !== 0 && event.button !== 2) return;
    this.followTarget = null;
    if (event.button === 0) this.panPointerId = event.pointerId;
    else this.orbitPointerId = event.pointerId;
    this.lastPointerX = event.clientX;
    this.lastPointerY = event.clientY;
    this.surface.setPointerCapture(event.pointerId);
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly handlePointerMove = (event: PointerEvent): void => {
    if (event.pointerId !== this.panPointerId && event.pointerId !== this.orbitPointerId) return;
    const deltaX = event.clientX - this.lastPointerX;
    const deltaY = event.clientY - this.lastPointerY;
    this.lastPointerX = event.clientX;
    this.lastPointerY = event.clientY;

    if (event.pointerId === this.panPointerId) {
      const width = Math.max(this.surface.clientWidth, 1);
      const height = Math.max(this.surface.clientHeight, 1);
      const worldWidth = (this.camera.right - this.camera.left) / this.camera.zoom;
      const worldHeight = (this.camera.top - this.camera.bottom) / this.camera.zoom;
      this.pan(-(deltaX / width) * worldWidth, (deltaY / height) * worldHeight);
    } else {
      if (deltaX !== 0) this.rotateBy(-deltaX * this.rotationRadiansPerPixel);
      if (deltaY !== 0) this.adjustAltitude(-deltaY * this.altitudeMetersPerPixel);
    }

    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly handlePointerRelease = (event: PointerEvent): void => {
    const handled = event.pointerId === this.panPointerId || event.pointerId === this.orbitPointerId;
    if (event.pointerId === this.panPointerId) this.panPointerId = null;
    if (event.pointerId === this.orbitPointerId) this.orbitPointerId = null;
    if (handled) event.stopImmediatePropagation();
  };

  private readonly handleWheel = (event: WheelEvent): void => {
    const scale = Math.exp(-event.deltaY * 0.001);
    this.zoomBy(scale);
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly handleContextMenu = (event: MouseEvent): void => {
    event.preventDefault();
  };
}

export function createStaticNavigationTarget(
  kind: Exclude<ViewNavigationTargetKind, 'entity'>,
  id: bigint | string | undefined,
  position: WorldPosition,
  preferredZoom?: number,
): ViewNavigationTarget {
  validateWorldPosition(position);
  const stablePosition = Object.freeze({ ...position });
  return {
    kind,
    ...(id === undefined ? {} : { id }),
    ...(preferredZoom === undefined ? {} : { preferredZoom }),
    writePosition: (_now, target) => {
      target[0] = stablePosition.x;
      target[1] = stablePosition.y;
      target[2] = stablePosition.z;
      return true;
    },
  };
}

export function createEntityNavigationTarget(
  entityId: bigint,
  store: ReadonlyEntityStore,
  preferredZoom?: number,
): ViewNavigationTarget {
  if (entityId <= 0n) throw new RangeError('Entity ID must be greater than zero.');
  return {
    kind: 'entity',
    id: entityId,
    ...(preferredZoom === undefined ? {} : { preferredZoom }),
    writePosition: (now, target) => store.writeSampledPositionById(entityId, now, target),
  };
}

export function getCameraFocusAtSimulationAltitude(camera: ViewNavigationCamera, simulationAltitude: number): WorldPosition | undefined {
  const focus = new Float64Array(3);
  if (!writeCameraFocusAtSimulationAltitude(camera, simulationAltitude, focus)) return undefined;
  return { x: focus[0], y: focus[1], z: focus[2] };
}

function writeCameraFocusAtSimulationAltitude(camera: ViewNavigationCamera, simulationAltitude: number, target: MutablePositionBuffer): boolean {
  validateFinite(simulationAltitude, 'simulation altitude');
  const direction = new Float64Array(3);
  writeCameraWorldDirection(camera, direction);
  if (Math.abs(direction[1]) <= DIRECTION_EPSILON) return false;
  const distance = (simulationAltitude - camera.position.y) / direction[1];
  if (!Number.isFinite(distance) || distance <= 0) return false;
  target[0] = camera.position.x + direction[0] * distance;
  target[1] = camera.position.z + direction[2] * distance;
  target[2] = simulationAltitude;
  return true;
}

function writeCameraWorldDirection(camera: ViewNavigationCamera, target: MutablePositionBuffer): void {
  camera.updateMatrixWorld(true);
  const elements = camera.matrixWorld.elements;
  const x = -Number(elements[8]);
  const y = -Number(elements[9]);
  const z = -Number(elements[10]);
  const length = Math.hypot(x, y, z);
  if (!Number.isFinite(length) || length <= DIRECTION_EPSILON) throw new RangeError('Camera direction is invalid.');
  target[0] = x / length;
  target[1] = y / length;
  target[2] = z / length;
}

function normalizeHorizontal(x: number, z: number, fallbackX: number, fallbackZ: number): { readonly x: number; readonly z: number } {
  const length = Math.hypot(x, z);
  if (Number.isFinite(length) && length > DIRECTION_EPSILON) return { x: x / length, z: z / length };
  const fallbackLength = Math.hypot(fallbackX, fallbackZ);
  if (!Number.isFinite(fallbackLength) || fallbackLength <= DIRECTION_EPSILON) throw new RangeError('Camera horizontal basis is invalid.');
  return { x: fallbackX / fallbackLength, z: fallbackZ / fallbackLength };
}

function validateSampledWorldPosition(position: MutablePositionBuffer): void {
  validateFinite(position[0], 'world position X');
  validateFinite(position[1], 'world position Y');
  validateFinite(position[2], 'world position Z');
}

function validateWorldPosition(position: WorldPosition): void {
  validateFinite(position.x, 'world position X');
  validateFinite(position.y, 'world position Y');
  validateFinite(position.z, 'world position Z');
}

function validateFinite(value: number, label: string): void {
  if (!Number.isFinite(value)) throw new RangeError(`${label} must be finite.`);
}

function validatePositive(value: number, label: string): number {
  if (!Number.isFinite(value) || value <= 0) throw new RangeError(`${label} must be finite and greater than zero.`);
  return value;
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
