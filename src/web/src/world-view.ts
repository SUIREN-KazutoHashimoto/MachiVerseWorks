import * as THREE from 'three';

import type { EntityStore } from './entity-store.ts';
import type { WorldVolume } from './protocol.ts';

const CAMERA_HEIGHT = 500;
const CAMERA_TILT_DISTANCE = 250;
const INITIAL_HALF_HEIGHT = 300;
const SUBSCRIPTION_PADDING = 1.2;
const SUBSCRIPTION_MIN_ALTITUDE = -128;
const SUBSCRIPTION_MAX_ALTITUDE = 512;
const MINIMUM_ZOOM = 0.25;
const MAXIMUM_ZOOM = 8;
const AGENT_HALF_SIZE = 2.5;

export interface WorldPosition {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

export class WorldView {
  public readonly scene = new THREE.Scene();
  public readonly camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 2_000);
  public readonly renderer = new THREE.WebGLRenderer({ antialias: true });

  private readonly agentRenderer: AgentRenderer;
  private aspect = 1;
  private dragPointerId: number | null = null;
  private lastPointerX = 0;
  private lastPointerY = 0;

  public constructor(private readonly host: HTMLElement) {
    this.scene.background = new THREE.Color(0x0b1020);
    this.camera.position.set(0, CAMERA_HEIGHT, 0);
    this.camera.up.set(0, 1, 0);
    this.camera.lookAt(0, 0, -CAMERA_TILT_DISTANCE);

    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.domElement.setAttribute('aria-label', 'MachiVerseWorks world view');
    this.host.append(this.renderer.domElement);

    const grid = new THREE.GridHelper(2_000, 40, 0x334155, 0x1e293b);
    this.scene.add(grid);

    this.agentRenderer = new AgentRenderer(this.scene);
    this.installControls();
    this.resize();
  }

  public resize(): void {
    const width = Math.max(this.host.clientWidth, 1);
    const height = Math.max(this.host.clientHeight, 1);
    this.aspect = width / height;
    this.camera.left = -INITIAL_HALF_HEIGHT * this.aspect;
    this.camera.right = INITIAL_HALF_HEIGHT * this.aspect;
    this.camera.top = INITIAL_HALF_HEIGHT;
    this.camera.bottom = -INITIAL_HALF_HEIGHT;
    this.camera.updateProjectionMatrix();
    this.renderer.setSize(width, height, false);
  }

  public render(store: EntityStore, now: number): void {
    this.agentRenderer.update(store, now);
    this.renderer.render(this.scene, this.camera);
  }

  public getSubscriptionVolume(): WorldVolume {
    return computeOrthographicSubscriptionVolume(
      this.camera,
      SUBSCRIPTION_MIN_ALTITUDE,
      SUBSCRIPTION_MAX_ALTITUDE,
      SUBSCRIPTION_PADDING,
    );
  }

  public getListenerPosition(): WorldPosition {
    return {
      x: this.camera.position.x,
      y: this.camera.position.z,
      z: this.camera.position.y,
    };
  }

  public dispose(): void {
    this.agentRenderer.dispose();
    this.renderer.dispose();
  }

  private installControls(): void {
    const canvas = this.renderer.domElement;
    canvas.addEventListener('pointerdown', (event) => {
      if (event.button !== 0) {
        return;
      }
      this.dragPointerId = event.pointerId;
      this.lastPointerX = event.clientX;
      this.lastPointerY = event.clientY;
      canvas.setPointerCapture(event.pointerId);
    });

    canvas.addEventListener('pointermove', (event) => {
      if (event.pointerId !== this.dragPointerId) {
        return;
      }

      const width = Math.max(canvas.clientWidth, 1);
      const height = Math.max(canvas.clientHeight, 1);
      const worldWidth = (this.camera.right - this.camera.left) / this.camera.zoom;
      const worldHeight = (this.camera.top - this.camera.bottom) / this.camera.zoom;
      const deltaX = event.clientX - this.lastPointerX;
      const deltaY = event.clientY - this.lastPointerY;
      this.lastPointerX = event.clientX;
      this.lastPointerY = event.clientY;
      this.camera.position.x -= (deltaX / width) * worldWidth;
      this.camera.position.z -= (deltaY / height) * worldHeight;
    });

    const releasePointer = (event: PointerEvent): void => {
      if (event.pointerId === this.dragPointerId) {
        this.dragPointerId = null;
      }
    };
    canvas.addEventListener('pointerup', releasePointer);
    canvas.addEventListener('pointercancel', releasePointer);

    canvas.addEventListener('wheel', (event) => {
      event.preventDefault();
      const scale = Math.exp(-event.deltaY * 0.001);
      this.camera.zoom = clamp(this.camera.zoom * scale, MINIMUM_ZOOM, MAXIMUM_ZOOM);
      this.camera.updateProjectionMatrix();
    }, { passive: false });
  }
}

export function computeOrthographicSubscriptionVolume(
  camera: THREE.OrthographicCamera,
  minimumAltitude: number,
  maximumAltitude: number,
  padding = 1,
): WorldVolume {
  if (!Number.isFinite(minimumAltitude) || !Number.isFinite(maximumAltitude) || maximumAltitude < minimumAltitude) {
    throw new RangeError('Subscription altitude bounds must be finite and ordered.');
  }
  if (!Number.isFinite(padding) || padding < 1) {
    throw new RangeError('Subscription padding must be finite and at least 1.');
  }

  camera.updateMatrixWorld(true);
  const direction = camera.getWorldDirection(new THREE.Vector3());
  if (Math.abs(direction.y) < 1e-9) {
    throw new RangeError('Subscription projection requires a camera direction that intersects altitude planes.');
  }

  const projectedPoint = new THREE.Vector3();
  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;

  for (const altitude of [minimumAltitude, maximumAltitude]) {
    for (const normalizedX of [-1, 1]) {
      for (const normalizedY of [-1, 1]) {
        projectedPoint.set(normalizedX, normalizedY, 0).unproject(camera);
        const distance = (altitude - projectedPoint.y) / direction.y;
        const simulationX = projectedPoint.x + direction.x * distance;
        const simulationY = projectedPoint.z + direction.z * distance;
        minX = Math.min(minX, simulationX);
        minY = Math.min(minY, simulationY);
        maxX = Math.max(maxX, simulationX);
        maxY = Math.max(maxY, simulationY);
      }
    }
  }

  const paddingX = (maxX - minX) * (padding - 1) * 0.5;
  const paddingY = (maxY - minY) * (padding - 1) * 0.5;
  return {
    minX: minX - paddingX,
    minY: minY - paddingY,
    minZ: minimumAltitude,
    maxX: maxX + paddingX,
    maxY: maxY + paddingY,
    maxZ: maximumAltitude,
  };
}

export function simulationToThreePosition(
  x: number,
  y: number,
  z: number,
  target = new THREE.Vector3(),
): THREE.Vector3 {
  return target.set(x, z, y);
}

class AgentRenderer {
  private readonly geometry = new THREE.BoxGeometry(5, 5, 5);
  private readonly material = new THREE.MeshBasicMaterial({ color: 0x67e8f9 });
  private mesh: THREE.InstancedMesh;
  private capacity = 1_024;
  private readonly matrix = new THREE.Matrix4();
  private positions = new Float32Array(this.capacity * 3);

  public constructor(private readonly scene: THREE.Scene) {
    this.mesh = this.createMesh(this.capacity);
    this.scene.add(this.mesh);
  }

  public update(store: EntityStore, now: number): void {
    this.ensureCapacity(store.size);
    const count = store.writeSampledPositions(now, this.positions);
    for (let index = 0; index < count; index += 1) {
      const positionOffset = index * 3;
      this.matrix.makeTranslation(
        this.positions[positionOffset],
        this.positions[positionOffset + 2] + AGENT_HALF_SIZE,
        this.positions[positionOffset + 1],
      );
      this.mesh.setMatrixAt(index, this.matrix);
    }
    this.mesh.count = count;
    this.mesh.instanceMatrix.needsUpdate = true;
  }

  public dispose(): void {
    this.scene.remove(this.mesh);
    this.mesh.dispose();
    this.geometry.dispose();
    this.material.dispose();
  }

  private ensureCapacity(required: number): void {
    if (required <= this.capacity) {
      return;
    }

    let nextCapacity = this.capacity;
    while (nextCapacity < required) {
      nextCapacity *= 2;
    }

    const previousMesh = this.mesh;
    this.capacity = nextCapacity;
    this.positions = new Float32Array(nextCapacity * 3);
    this.mesh = this.createMesh(nextCapacity);
    this.scene.remove(previousMesh);
    previousMesh.dispose();
    this.scene.add(this.mesh);
  }

  private createMesh(capacity: number): THREE.InstancedMesh {
    const mesh = new THREE.InstancedMesh(this.geometry, this.material, capacity);
    mesh.count = 0;
    mesh.frustumCulled = false;
    return mesh;
  }
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(maximum, Math.max(minimum, value));
}
