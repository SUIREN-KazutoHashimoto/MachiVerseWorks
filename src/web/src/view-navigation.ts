import * as THREE from 'three';

import type { ReadonlyEntityStore } from './entity-store.ts';
import type { MutablePositionBuffer } from './visual-interpolation-state.ts';

const DEFAULT_MOVE_SPEED = 40;
const DEFAULT_MINIMUM_MOVE_SPEED = 2;
const DEFAULT_MAXIMUM_MOVE_SPEED = 800;
const DEFAULT_SPRINT_MULTIPLIER = 4;
const DEFAULT_LOOK_SENSITIVITY = 0.0035;
const DEFAULT_MINIMUM_HEIGHT = 1.7;
const DEFAULT_FOLLOW_DISTANCE = 12;
const DEFAULT_MINIMUM_FOLLOW_DISTANCE = 3;
const DEFAULT_MAXIMUM_FOLLOW_DISTANCE = 120;
const DEFAULT_FOCUS_DISTANCE = 250;
const MAXIMUM_FRAME_DELTA_SECONDS = 0.1;
const PITCH_LIMIT = Math.PI / 2 - 0.01;
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
  /**
   * Compatibility hint from the former orthographic navigation API.
   * Perspective navigation maps larger values to a shorter focus/follow distance.
   */
  readonly preferredZoom?: number;
  writePosition(now: number, target: MutablePositionBuffer): boolean;
}

export interface ViewNavigationOptions {
  readonly moveSpeed?: number;
  readonly minimumMoveSpeed?: number;
  readonly maximumMoveSpeed?: number;
  readonly sprintMultiplier?: number;
  readonly lookSensitivity?: number;
  readonly minimumHeight?: number;
  readonly followDistance?: number;
  readonly minimumFollowDistance?: number;
  readonly maximumFollowDistance?: number;
}

export class ViewNavigationController {
  private readonly sampledTargetPosition = new Float64Array(3);
  private readonly keys = new Set<string>();
  private readonly movementForward = new THREE.Vector3();
  private readonly movementRight = new THREE.Vector3();
  private readonly movementDirection = new THREE.Vector3();
  private readonly minimumMoveSpeed: number;
  private readonly maximumMoveSpeed: number;
  private readonly sprintMultiplier: number;
  private readonly lookSensitivity: number;
  private readonly minimumHeight: number;
  private readonly minimumFollowDistance: number;
  private readonly maximumFollowDistance: number;
  private readonly keyboardTarget: Window | null;
  private followTarget: ViewNavigationTarget | null = null;
  private lookPointerId: number | null = null;
  private lastPointerX = 0;
  private lastPointerY = 0;
  private lastUpdateAt: number | null = null;
  private yaw = 0;
  private pitch = 0;
  private currentMoveSpeed: number;
  private currentFollowDistance: number;

  public constructor(
    private readonly camera: THREE.PerspectiveCamera,
    private readonly surface: HTMLCanvasElement,
    options: ViewNavigationOptions = {},
  ) {
    this.minimumMoveSpeed = validatePositive(options.minimumMoveSpeed ?? DEFAULT_MINIMUM_MOVE_SPEED, 'minimum move speed');
    this.maximumMoveSpeed = validatePositive(options.maximumMoveSpeed ?? DEFAULT_MAXIMUM_MOVE_SPEED, 'maximum move speed');
    if (this.maximumMoveSpeed < this.minimumMoveSpeed) throw new RangeError('Maximum move speed must be greater than or equal to minimum move speed.');
    this.currentMoveSpeed = clamp(validatePositive(options.moveSpeed ?? DEFAULT_MOVE_SPEED, 'move speed'), this.minimumMoveSpeed, this.maximumMoveSpeed);
    this.sprintMultiplier = validatePositive(options.sprintMultiplier ?? DEFAULT_SPRINT_MULTIPLIER, 'sprint multiplier');
    this.lookSensitivity = validatePositive(options.lookSensitivity ?? DEFAULT_LOOK_SENSITIVITY, 'look sensitivity');
    this.minimumHeight = validatePositive(options.minimumHeight ?? DEFAULT_MINIMUM_HEIGHT, 'minimum height');
    this.minimumFollowDistance = validatePositive(options.minimumFollowDistance ?? DEFAULT_MINIMUM_FOLLOW_DISTANCE, 'minimum follow distance');
    this.maximumFollowDistance = validatePositive(options.maximumFollowDistance ?? DEFAULT_MAXIMUM_FOLLOW_DISTANCE, 'maximum follow distance');
    if (this.maximumFollowDistance < this.minimumFollowDistance) throw new RangeError('Maximum follow distance must be greater than or equal to minimum follow distance.');
    this.currentFollowDistance = clamp(validatePositive(options.followDistance ?? DEFAULT_FOLLOW_DISTANCE, 'follow distance'), this.minimumFollowDistance, this.maximumFollowDistance);
    validatePositive(this.camera.near, 'camera near plane');
    validatePositive(this.camera.far, 'camera far plane');
    if (this.camera.far <= this.camera.near) throw new RangeError('Camera far plane must be greater than the near plane.');

    this.keyboardTarget = typeof window === 'undefined' ? null : window;
    this.syncAnglesFromCamera();
    this.installControls();
  }

  public get moveSpeed(): number { return this.currentMoveSpeed; }
  public get followDistance(): number { return this.currentFollowDistance; }
  public get isFollowing(): boolean { return this.followTarget !== null; }

  /** View-local input seam used by keyboard handling and deterministic controller tests. */
  public setKeyState(code: string, pressed: boolean): void {
    if (pressed) this.keys.add(code);
    else this.keys.delete(code);
  }

  /** Applies a mouse-look delta without coupling the camera state machine to DOM event construction. */
  public lookBy(deltaX: number, deltaY: number): void {
    validateFinite(deltaX, 'look delta X');
    validateFinite(deltaY, 'look delta Y');
    this.yaw -= deltaX * this.lookSensitivity;
    this.pitch -= deltaY * this.lookSensitivity;
    this.pitch = clamp(this.pitch, -PITCH_LIMIT, PITCH_LIMIT);
    if (this.followTarget === null) this.applyRotation();
  }

  public dispose(): void {
    this.surface.removeEventListener('pointerdown', this.handlePointerDown, true);
    this.surface.removeEventListener('pointermove', this.handlePointerMove, true);
    this.surface.removeEventListener('pointerup', this.handlePointerRelease, true);
    this.surface.removeEventListener('pointercancel', this.handlePointerRelease, true);
    this.surface.removeEventListener('wheel', this.handleWheel, true);
    this.surface.removeEventListener('contextmenu', this.handleContextMenu);
    if (this.keyboardTarget !== null) {
      this.keyboardTarget.removeEventListener('keydown', this.handleKeyDown);
      this.keyboardTarget.removeEventListener('keyup', this.handleKeyUp);
      this.keyboardTarget.removeEventListener('blur', this.handleBlur);
    }
    this.keys.clear();
    this.followTarget = null;
    this.lookPointerId = null;
    this.lastUpdateAt = null;
  }

  public update(now: number): void {
    validateFinite(now, 'navigation timestamp');
    const deltaSeconds = this.lastUpdateAt === null ? 0 : clamp((now - this.lastUpdateAt) / 1_000, 0, MAXIMUM_FRAME_DELTA_SECONDS);
    this.lastUpdateAt = now;

    const target = this.followTarget;
    if (target !== null) {
      if (!target.writePosition(now, this.sampledTargetPosition)) {
        this.clearFollow();
        return;
      }
      this.orbitSampledPosition(this.sampledTargetPosition);
      return;
    }

    if (deltaSeconds > 0) this.updateFreeMovement(deltaSeconds);
  }

  public jump(target: ViewNavigationTarget, now = performance.now()): boolean {
    if (!target.writePosition(now, this.sampledTargetPosition)) return false;
    this.followTarget = null;
    this.placeBehindSampledPosition(this.sampledTargetPosition, DEFAULT_FOCUS_DISTANCE);
    return true;
  }

  public focus(target: ViewNavigationTarget, now = performance.now()): boolean {
    if (!target.writePosition(now, this.sampledTargetPosition)) return false;
    this.followTarget = null;
    const distance = resolveFocusDistance(target.preferredZoom);
    this.placeBehindSampledPosition(this.sampledTargetPosition, distance);
    return true;
  }

  public follow(target: ViewNavigationTarget, now = performance.now()): boolean {
    if (!target.writePosition(now, this.sampledTargetPosition)) return false;
    if (target.preferredZoom !== undefined) {
      this.currentFollowDistance = clamp(
        DEFAULT_FOLLOW_DISTANCE * 4 / validatePositive(target.preferredZoom, 'preferred zoom'),
        this.minimumFollowDistance,
        this.maximumFollowDistance,
      );
    }
    this.followTarget = target;
    this.orbitSampledPosition(this.sampledTargetPosition);
    return true;
  }

  public clearFollow(): void {
    if (this.followTarget !== null) this.syncAnglesFromCamera();
    this.followTarget = null;
  }

  public focusEntity(entityId: bigint, store: ReadonlyEntityStore, now = performance.now(), preferredZoom?: number): boolean {
    return this.focus(createEntityNavigationTarget(entityId, store, preferredZoom), now);
  }

  public followEntity(entityId: bigint, store: ReadonlyEntityStore, now = performance.now(), preferredZoom?: number): boolean {
    return this.follow(createEntityNavigationTarget(entityId, store, preferredZoom), now);
  }

  private updateFreeMovement(deltaSeconds: number): void {
    let forwardAmount = 0;
    let rightAmount = 0;
    let verticalAmount = 0;

    if (this.keys.has('KeyW')) forwardAmount += 1;
    if (this.keys.has('KeyS')) forwardAmount -= 1;
    if (this.keys.has('KeyD')) rightAmount += 1;
    if (this.keys.has('KeyA')) rightAmount -= 1;
    if (this.keys.has('KeyE') || this.keys.has('Space')) verticalAmount += 1;
    if (this.keys.has('KeyQ') || this.keys.has('ControlLeft') || this.keys.has('ControlRight')) verticalAmount -= 1;

    if (forwardAmount === 0 && rightAmount === 0 && verticalAmount === 0) return;

    this.movementForward.set(0, 0, -1).applyQuaternion(this.camera.quaternion);
    this.movementRight.set(1, 0, 0).applyQuaternion(this.camera.quaternion);
    this.movementDirection
      .copy(this.movementForward)
      .multiplyScalar(forwardAmount)
      .addScaledVector(this.movementRight, rightAmount);
    this.movementDirection.y += verticalAmount;
    this.movementDirection.normalize();

    const sprinting = this.keys.has('ShiftLeft') || this.keys.has('ShiftRight');
    const distance = this.currentMoveSpeed * (sprinting ? this.sprintMultiplier : 1) * deltaSeconds;
    this.camera.position.addScaledVector(this.movementDirection, distance);
    this.camera.position.y = Math.max(this.minimumHeight, this.camera.position.y);
    this.camera.updateMatrixWorld(true);
  }

  private placeBehindSampledPosition(position: MutablePositionBuffer, distance: number): void {
    validateSampledWorldPosition(position);
    const target = simulationPositionToThree(position);
    const forward = new THREE.Vector3(0, 0, -1).applyQuaternion(this.camera.quaternion).normalize();
    this.camera.position.copy(target).addScaledVector(forward, -distance);
    this.camera.position.y = Math.max(this.minimumHeight, this.camera.position.y);
    this.camera.lookAt(target);
    this.camera.updateMatrixWorld(true);
    this.syncAnglesFromCamera();
  }

  private orbitSampledPosition(position: MutablePositionBuffer): void {
    validateSampledWorldPosition(position);
    const target = simulationPositionToThree(position);
    const cp = Math.cos(this.pitch);
    const forward = new THREE.Vector3(
      -Math.sin(this.yaw) * cp,
      Math.sin(this.pitch),
      -Math.cos(this.yaw) * cp,
    );
    this.camera.position.copy(target).addScaledVector(forward, -this.currentFollowDistance);
    this.camera.position.y = Math.max(this.minimumHeight, this.camera.position.y);
    this.camera.lookAt(target);
    this.camera.updateMatrixWorld(true);
  }

  private applyRotation(): void {
    this.pitch = clamp(this.pitch, -PITCH_LIMIT, PITCH_LIMIT);
    this.camera.quaternion.setFromEuler(new THREE.Euler(this.pitch, this.yaw, 0, 'YXZ'));
    this.camera.updateMatrixWorld(true);
  }

  private syncAnglesFromCamera(): void {
    const euler = new THREE.Euler().setFromQuaternion(this.camera.quaternion, 'YXZ');
    this.pitch = clamp(euler.x, -PITCH_LIMIT, PITCH_LIMIT);
    this.yaw = euler.y;
  }

  private installControls(): void {
    this.surface.addEventListener('pointerdown', this.handlePointerDown, true);
    this.surface.addEventListener('pointermove', this.handlePointerMove, true);
    this.surface.addEventListener('pointerup', this.handlePointerRelease, true);
    this.surface.addEventListener('pointercancel', this.handlePointerRelease, true);
    this.surface.addEventListener('wheel', this.handleWheel, { capture: true, passive: false });
    this.surface.addEventListener('contextmenu', this.handleContextMenu);
    if (this.keyboardTarget !== null) {
      this.keyboardTarget.addEventListener('keydown', this.handleKeyDown);
      this.keyboardTarget.addEventListener('keyup', this.handleKeyUp);
      this.keyboardTarget.addEventListener('blur', this.handleBlur);
    }
  }

  private readonly handlePointerDown = (event: PointerEvent): void => {
    if (event.button !== 0) return;
    this.lookPointerId = event.pointerId;
    this.lastPointerX = event.clientX;
    this.lastPointerY = event.clientY;
    this.surface.setPointerCapture(event.pointerId);
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly handlePointerMove = (event: PointerEvent): void => {
    if (event.pointerId !== this.lookPointerId) return;
    const deltaX = event.clientX - this.lastPointerX;
    const deltaY = event.clientY - this.lastPointerY;
    this.lastPointerX = event.clientX;
    this.lastPointerY = event.clientY;
    this.lookBy(deltaX, deltaY);
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly handlePointerRelease = (event: PointerEvent): void => {
    if (event.pointerId !== this.lookPointerId) return;
    this.lookPointerId = null;
    event.stopImmediatePropagation();
  };

  private readonly handleWheel = (event: WheelEvent): void => {
    const factor = Math.exp(-event.deltaY * 0.001);
    if (this.followTarget === null) {
      this.currentMoveSpeed = clamp(this.currentMoveSpeed * factor, this.minimumMoveSpeed, this.maximumMoveSpeed);
    } else {
      this.currentFollowDistance = clamp(this.currentFollowDistance * factor, this.minimumFollowDistance, this.maximumFollowDistance);
    }
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  private readonly handleKeyDown = (event: KeyboardEvent): void => {
    if (isNavigationKeyboardInputTarget(event.target)) return;
    if (event.code === 'Space') event.preventDefault();
    this.setKeyState(event.code, true);
  };

  private readonly handleKeyUp = (event: KeyboardEvent): void => {
    this.setKeyState(event.code, false);
  };

  private readonly handleBlur = (): void => {
    this.keys.clear();
    this.lookPointerId = null;
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

export function getCameraFocusAtSimulationAltitude(camera: THREE.PerspectiveCamera, simulationAltitude: number): WorldPosition | undefined {
  validateFinite(simulationAltitude, 'simulation altitude');
  camera.updateMatrixWorld(true);
  const direction = new THREE.Vector3();
  camera.getWorldDirection(direction);
  if (Math.abs(direction.y) <= DIRECTION_EPSILON) return undefined;
  const distance = (simulationAltitude - camera.position.y) / direction.y;
  if (!Number.isFinite(distance) || distance <= 0) return undefined;
  const focus = camera.position.clone().addScaledVector(direction, distance);
  return { x: focus.x, y: focus.z, z: simulationAltitude };
}

function simulationPositionToThree(position: MutablePositionBuffer): THREE.Vector3 {
  return new THREE.Vector3(position[0], position[2], position[1]);
}

function resolveFocusDistance(preferredZoom: number | undefined): number {
  if (preferredZoom === undefined) return DEFAULT_FOCUS_DISTANCE;
  return clamp(DEFAULT_FOCUS_DISTANCE / validatePositive(preferredZoom, 'preferred zoom'), 10, 10_000);
}

export function isNavigationKeyboardInputTarget(target: EventTarget | null): boolean {
  let current: EventTarget | null = target;
  while (current !== null && typeof current === 'object') {
    const element = current as Partial<HTMLElement>;
    const tagName = typeof element.tagName === 'string' ? element.tagName.toUpperCase() : '';
    if (tagName === 'INPUT' || tagName === 'TEXTAREA' || tagName === 'SELECT' || tagName === 'BUTTON' || element.isContentEditable === true) {
      return true;
    }
    current = element.parentElement ?? null;
  }
  return false;
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
