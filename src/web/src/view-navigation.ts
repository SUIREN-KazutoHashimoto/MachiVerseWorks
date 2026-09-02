import type { ReadonlyEntityStore } from './entity-store.ts';

const DEFAULT_MINIMUM_ZOOM = 0.25;
const DEFAULT_MAXIMUM_ZOOM = 8;
const DEFAULT_ROTATION_RADIANS_PER_PIXEL = 0.004;
const DEFAULT_ALTITUDE_METERS_PER_PIXEL = 2;
const FOCUS_FALLBACK_DISTANCE = 250;
const DIRECTION_EPSILON = 1e-9;

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
  getPosition(now: number): WorldPosition | undefined;
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
  private followTarget: ViewNavigationTarget | null = null;
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
    this.installControls();
  }

  public dispose(): void {
    this.surface.removeEventListener('pointerdown', this.handlePointerDown);
    this.surface.removeEventListener('pointermove', this.handlePointerMove);
    this.surface.removeEventListener('pointerup', this.handlePointerRelease);
    this.surface.removeEventListener('pointercancel', this.handlePointerRelease);
    this.surface.removeEventListener('contextmenu', this.handleContextMenu);
    this.followTarget = null;
    this.orbitPointerId = null;
  }

  public update(now: number): void {
    const target = this.followTarget;
    if (target === null) return;
    const position = target.getPosition(now);
    if (position !== undefined) this.centerOn(position);
  }

  public pan(deltaX: number, deltaY: number): void {
    validateFinite(deltaX, 'pan X');
    validateFinite(deltaY, 'pan Y');
    this.followTarget = null;
    this.camera.position.x += deltaX;
    this.camera.position.z += deltaY;
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
    const direction = getCameraWorldDirection(this.camera);
    const cosine = Math.cos(radians);
    const sine = Math.sin(radians);
    const rotatedX = direction.x * cosine + direction.z * sine;
    const rotatedZ = -direction.x * sine + direction.z * cosine;
    this.camera.lookAt(
      this.camera.position.x + rotatedX,
      this.camera.position.y + direction.y,
      this.camera.position.z + rotatedZ,
    );
    this.camera.updateMatrixWorld(true);
  }

  public adjustAltitude(deltaMeters: number): void {
    validateFinite(deltaMeters, 'altitude delta');
    const nextAltitude = this.camera.position.y + deltaMeters;
    if (!Number.isFinite(nextAltitude)) throw new RangeError('Camera altitude must remain finite.');
    this.camera.position.y = nextAltitude;
    this.camera.updateMatrixWorld(true);
  }

  public jump(target: ViewNavigationTarget, now = performance.now()): boolean {
    this.followTarget = null;
    const position = target.getPosition(now);
    if (position === undefined) return false;
    this.centerOn(position);
    return true;
  }

  public focus(target: ViewNavigationTarget, now = performance.now()): boolean {
    const focused = this.jump(target, now);
    if (!focused) return false;
    if (target.preferredZoom !== undefined) this.setZoom(target.preferredZoom);
    return true;
  }

  public follow(target: ViewNavigationTarget, now = performance.now()): boolean {
    this.followTarget = target;
    const position = target.getPosition(now);
    if (position === undefined) return false;
    this.centerOn(position);
    if (target.preferredZoom !== undefined) this.setZoom(target.preferredZoom);
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

  private centerOn(position: WorldPosition): void {
    validateWorldPosition(position);
    const focus = getCameraFocusAtSimulationAltitude(this.camera, position.z);
    if (focus !== undefined) {
      this.camera.position.x += position.x - focus.x;
      this.camera.position.z += position.y - focus.y;
      this.camera.position.y += position.z - focus.z;
    } else {
      const direction = getCameraWorldDirection(this.camera);
      this.camera.position.x = position.x - direction.x * FOCUS_FALLBACK_DISTANCE;
      this.camera.position.z = position.y - direction.z * FOCUS_FALLBACK_DISTANCE;
      this.camera.position.y = position.z - direction.y * FOCUS_FALLBACK_DISTANCE;
    }
    this.camera.updateMatrixWorld(true);
  }

  private installControls(): void {
    this.surface.addEventListener('pointerdown', this.handlePointerDown);
    this.surface.addEventListener('pointermove', this.handlePointerMove);
    this.surface.addEventListener('pointerup', this.handlePointerRelease);
    this.surface.addEventListener('pointercancel', this.handlePointerRelease);
    this.surface.addEventListener('contextmenu', this.handleContextMenu);
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.button === 0) {
      this.followTarget = null;
      return;
    }
    if (event.button !== 2) return;
    this.followTarget = null;
    this.orbitPointerId = event.pointerId;
    this.lastPointerX = event.clientX;
    this.lastPointerY = event.clientY;
    this.surface.setPointerCapture(event.pointerId);
    event.preventDefault();
  };

  private readonly handlePointerMove = (event: PointerEvent): void => {
    if (event.pointerId !== this.orbitPointerId) return;
    const deltaX = event.clientX - this.lastPointerX;
    const deltaY = event.clientY - this.lastPointerY;
    this.lastPointerX = event.clientX;
    this.lastPointerY = event.clientY;
    if (deltaX !== 0) this.rotateBy(-deltaX * this.rotationRadiansPerPixel);
    if (deltaY !== 0) this.adjustAltitude(-deltaY * this.altitudeMetersPerPixel);
    event.preventDefault();
  };

  private readonly handlePointerRelease = (event: PointerEvent): void => {
    if (event.pointerId === this.orbitPointerId) this.orbitPointerId = null;
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
    getPosition: () => stablePosition,
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
    getPosition: (now) => {
      const entity = store.sampleById(entityId, now);
      return entity === undefined ? undefined : { x: entity.x, y: entity.y, z: entity.z };
    },
  };
}

export function getCameraFocusAtSimulationAltitude(camera: ViewNavigationCamera, simulationAltitude: number): WorldPosition | undefined {
  validateFinite(simulationAltitude, 'simulation altitude');
  const direction = getCameraWorldDirection(camera);
  if (Math.abs(direction.y) <= DIRECTION_EPSILON) return undefined;
  const distance = (simulationAltitude - camera.position.y) / direction.y;
  if (!Number.isFinite(distance) || distance <= 0) return undefined;
  return {
    x: camera.position.x + direction.x * distance,
    y: camera.position.z + direction.z * distance,
    z: simulationAltitude,
  };
}

function getCameraWorldDirection(camera: ViewNavigationCamera): WorldPosition {
  camera.updateMatrixWorld(true);
  const elements = camera.matrixWorld.elements;
  const x = -Number(elements[8]);
  const y = -Number(elements[9]);
  const z = -Number(elements[10]);
  const length = Math.hypot(x, y, z);
  if (!Number.isFinite(length) || length <= DIRECTION_EPSILON) throw new RangeError('Camera direction is invalid.');
  return { x: x / length, y: y / length, z: z / length };
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
