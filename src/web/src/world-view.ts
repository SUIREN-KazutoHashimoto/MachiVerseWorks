import * as THREE from 'three';

import type { ReadonlyEntityStore } from './entity-store.ts';
import type { ReadonlyPedestrianStore } from './pedestrian-store.ts';
import { PhysicalWorldRenderer, type PhysicalWorldRenderingMetrics } from './physical-world-renderer.ts';
import type { ReadonlyRoadNetworkStore } from './road-network-store.ts';
import { LaneDirection, RoadNodeKind, type Lane, type WorldVolume } from './protocol.ts';
import type { ReadonlyIntersectionControlStore, ReadonlyVehicleStore } from './traffic-store.ts';
import { SignalIndication } from './traffic-protocol.ts';
import type { ReadonlyWorldEnvironmentStore } from './world-environment-store.ts';

const CAMERA_HEIGHT = 500;
const CAMERA_TILT_DISTANCE = 250;
const INITIAL_HALF_HEIGHT = 300;
const SUBSCRIPTION_PADDING = 1.2;
const MINIMUM_ZOOM = 0.25;
const MAXIMUM_ZOOM = 8;
const SUBSCRIPTION_RETRY_ZOOM_FACTOR = 1.25;
const AGENT_HALF_SIZE = 2.5;
const PEDESTRIAN_HALF_HEIGHT = 1.5;

export interface WorldPosition {
  readonly x: number;
  readonly y: number;
  readonly z: number;
}

export interface WorldViewRenderingMetrics {
  readonly frameTimeMs: number;
  readonly drawCalls: number;
  readonly geometries: number;
  readonly textures: number;
  readonly physicalWorld: PhysicalWorldRenderingMetrics;
}

export class WorldView {
  public readonly scene = new THREE.Scene();
  public readonly camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 2_000);
  public readonly renderer = new THREE.WebGLRenderer({ antialias: true });

  private readonly physicalWorldRenderer: PhysicalWorldRenderer;
  private readonly agentRenderer: AgentRenderer;
  private readonly pedestrianRenderer: PedestrianRenderer;
  private readonly vehicleRenderer: VehicleRenderer;
  private readonly intersectionRenderer: IntersectionControlRenderer;
  private readonly roadRenderer: RoadNetworkRenderer;
  private aspect = 1;
  private dragPointerId: number | null = null;
  private lastPointerX = 0;
  private lastPointerY = 0;
  private lastFrameTimeMs = 0;

  public constructor(private readonly host: HTMLElement) {
    this.scene.background = new THREE.Color(0x0b1020);
    this.camera.position.set(0, CAMERA_HEIGHT, 0);
    this.camera.up.set(0, 1, 0);
    this.camera.lookAt(0, 0, -CAMERA_TILT_DISTANCE);

    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    this.renderer.domElement.setAttribute('aria-label', 'MachiVerseWorks world view');
    this.host.append(this.renderer.domElement);

    this.physicalWorldRenderer = new PhysicalWorldRenderer(this.scene);
    this.roadRenderer = new RoadNetworkRenderer(this.scene);
    this.agentRenderer = new AgentRenderer(this.scene);
    this.pedestrianRenderer = new PedestrianRenderer(this.scene);
    this.vehicleRenderer = new VehicleRenderer(this.scene);
    this.intersectionRenderer = new IntersectionControlRenderer(this.scene);
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

  public render(
    store: ReadonlyEntityStore,
    now: number,
    pedestrians: ReadonlyPedestrianStore | null = null,
    vehicles: ReadonlyVehicleStore | null = null,
    intersections: ReadonlyIntersectionControlStore | null = null,
    roadNetwork: ReadonlyRoadNetworkStore | null = null,
    worldEnvironment: ReadonlyWorldEnvironmentStore | null = null,
  ): void {
    if (worldEnvironment !== null) this.physicalWorldRenderer.update(worldEnvironment);
    if (roadNetwork !== null) this.roadRenderer.update(roadNetwork);
    this.agentRenderer.update(store, now);
    this.pedestrianRenderer.update(pedestrians, now);
    const vehicleCount = this.vehicleRenderer.update(vehicles, now);
    const intersectionCount = this.intersectionRenderer.update(intersections, now);
    this.renderer.domElement.dataset.vehicleCount = String(vehicleCount);
    this.renderer.domElement.dataset.intersectionControlCount = String(intersectionCount);
    const renderStartedAt = performance.now();
    this.renderer.render(this.scene, this.camera);
    this.lastFrameTimeMs = Math.max(0, performance.now() - renderStartedAt);
    const metrics = this.getRenderingMetrics();
    this.renderer.domElement.dataset.physicalWorldFrameTimeMs = metrics.frameTimeMs.toFixed(3);
    this.renderer.domElement.dataset.physicalWorldDrawCalls = String(metrics.drawCalls);
    this.renderer.domElement.dataset.physicalWorldGeometryBytes = String(metrics.physicalWorld.geometryByteLength);
    this.renderer.domElement.dataset.terrainTriangles = String(metrics.physicalWorld.terrainTriangles);
  }

  public getRenderingMetrics(): WorldViewRenderingMetrics {
    return Object.freeze({
      frameTimeMs: this.lastFrameTimeMs,
      drawCalls: this.renderer.info.render.calls,
      geometries: this.renderer.info.memory.geometries,
      textures: this.renderer.info.memory.textures,
      physicalWorld: this.physicalWorldRenderer.metrics,
    });
  }

  public getSubscriptionVolume(): WorldVolume { return computeOrthographicSubscriptionVolume(this.camera, SUBSCRIPTION_PADDING); }

  public zoomInForSubscriptionRetry(): boolean {
    if (this.camera.zoom >= MAXIMUM_ZOOM) return false;
    this.camera.zoom = clamp(this.camera.zoom * SUBSCRIPTION_RETRY_ZOOM_FACTOR, MINIMUM_ZOOM, MAXIMUM_ZOOM);
    this.camera.updateProjectionMatrix();
    return true;
  }

  public getListenerPosition(): WorldPosition {
    return { x: this.camera.position.x, y: this.camera.position.z, z: this.camera.position.y };
  }

  public dispose(): void {
    this.physicalWorldRenderer.dispose();
    this.roadRenderer.dispose();
    this.agentRenderer.dispose();
    this.pedestrianRenderer.dispose();
    this.vehicleRenderer.dispose();
    this.intersectionRenderer.dispose();
    this.renderer.dispose();
  }

  private installControls(): void {
    const canvas = this.renderer.domElement;
    canvas.addEventListener('pointerdown', (event) => {
      if (event.button !== 0) return;
      this.dragPointerId = event.pointerId;
      this.lastPointerX = event.clientX;
      this.lastPointerY = event.clientY;
      canvas.setPointerCapture(event.pointerId);
    });

    canvas.addEventListener('pointermove', (event) => {
      if (event.pointerId !== this.dragPointerId) return;
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

    const releasePointer = (event: PointerEvent): void => { if (event.pointerId === this.dragPointerId) this.dragPointerId = null; };
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

export function computeOrthographicSubscriptionVolume(camera: THREE.OrthographicCamera, padding = 1): WorldVolume {
  if (!Number.isFinite(padding) || padding < 1) throw new RangeError('Subscription padding must be finite and at least 1.');
  camera.updateProjectionMatrix(); camera.updateMatrixWorld(true);
  const corner = new THREE.Vector3();
  let minX = Number.POSITIVE_INFINITY, minY = Number.POSITIVE_INFINITY, minZ = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY, maxY = Number.NEGATIVE_INFINITY, maxZ = Number.NEGATIVE_INFINITY;
  for (const normalizedZ of [-1, 1]) for (const normalizedY of [-1, 1]) for (const normalizedX of [-1, 1]) {
    corner.set(normalizedX, normalizedY, normalizedZ).unproject(camera);
    const simulationX = corner.x, simulationY = corner.z, simulationZ = corner.y;
    minX = Math.min(minX, simulationX); minY = Math.min(minY, simulationY); minZ = Math.min(minZ, simulationZ);
    maxX = Math.max(maxX, simulationX); maxY = Math.max(maxY, simulationY); maxZ = Math.max(maxZ, simulationZ);
  }
  const paddingX = (maxX - minX) * (padding - 1) * 0.5;
  const paddingY = (maxY - minY) * (padding - 1) * 0.5;
  const paddingZ = (maxZ - minZ) * (padding - 1) * 0.5;
  return { minX: minX - paddingX, minY: minY - paddingY, minZ: minZ - paddingZ, maxX: maxX + paddingX, maxY: maxY + paddingY, maxZ: maxZ + paddingZ };
}

export function computeLaneCenterOffsets(lanes: readonly Lane[]): ReadonlyMap<bigint, number> {
  const groups = new Map<string, Lane[]>();
  for (const lane of lanes) {
    const key = `${lane.segmentId.toString()}:${String(lane.direction)}`;
    const group = groups.get(key);
    if (group === undefined) groups.set(key, [lane]); else group.push(lane);
  }

  const offsets = new Map<bigint, number>();
  for (const group of groups.values()) {
    group.sort((left, right) => left.order - right.order || compareBigInt(left.id, right.id));
    let innerEdge = 0;
    for (const lane of group) {
      const magnitude = innerEdge + lane.widthMeters / 2;
      offsets.set(lane.id, lane.direction === LaneDirection.Forward ? magnitude : -magnitude);
      innerEdge += lane.widthMeters;
    }
  }
  return offsets;
}

export function simulationToThreePosition(x: number, y: number, z: number, target = new THREE.Vector3()): THREE.Vector3 { return target.set(x, z, y); }

class RoadNetworkRenderer {
  private readonly roadMaterial = new THREE.LineBasicMaterial({ color: 0x94a3b8 });
  private readonly laneMaterial = new THREE.LineBasicMaterial({ color: 0xf8fafc });
  private readonly intersectionMaterial = new THREE.PointsMaterial({ color: 0xf59e0b, size: 9, sizeAttenuation: false });
  private readonly roadLines = new THREE.LineSegments(new THREE.BufferGeometry(), this.roadMaterial);
  private readonly laneLines = new THREE.LineSegments(new THREE.BufferGeometry(), this.laneMaterial);
  private readonly intersections = new THREE.Points(new THREE.BufferGeometry(), this.intersectionMaterial);
  private renderedRevision = -1;

  public constructor(private readonly scene: THREE.Scene) {
    this.roadLines.name = 'road-segments';
    this.laneLines.name = 'road-lanes';
    this.intersections.name = 'road-intersections';
    this.roadLines.frustumCulled = false;
    this.laneLines.frustumCulled = false;
    this.intersections.frustumCulled = false;
    this.scene.add(this.roadLines, this.laneLines, this.intersections);
  }

  public update(store: ReadonlyRoadNetworkStore): void {
    if (store.revision === this.renderedRevision) return;
    this.renderedRevision = store.revision;
    const snapshot = store.snapshot;
    if (snapshot === null) {
      replacePositions(this.roadLines.geometry, []);
      replacePositions(this.laneLines.geometry, []);
      replacePositions(this.intersections.geometry, []);
      return;
    }

    const roadPositions: number[] = [];
    for (const segment of snapshot.segments) {
      const start = store.getNode(segment.startNodeId), end = store.getNode(segment.endNodeId);
      if (start === undefined || end === undefined) continue;
      appendSimulationPosition(roadPositions, start.x, start.y, start.z);
      appendSimulationPosition(roadPositions, end.x, end.y, end.z);
    }

    const laneOffsets = computeLaneCenterOffsets(snapshot.lanes);
    const lanePositions: number[] = [];
    for (const lane of snapshot.lanes) {
      const segment = store.getSegment(lane.segmentId);
      if (segment === undefined) continue;
      const start = store.getNode(segment.startNodeId), end = store.getNode(segment.endNodeId);
      if (start === undefined || end === undefined) continue;
      const dx = end.x - start.x, dy = end.y - start.y;
      const horizontalLength = Math.hypot(dx, dy);
      const offset = laneOffsets.get(lane.id) ?? 0;
      const offsetX = horizontalLength > 0 ? (-dy / horizontalLength) * offset : 0;
      const offsetY = horizontalLength > 0 ? (dx / horizontalLength) * offset : 0;
      appendSimulationPosition(lanePositions, start.x + offsetX, start.y + offsetY, start.z);
      appendSimulationPosition(lanePositions, end.x + offsetX, end.y + offsetY, end.z);
    }

    const intersectionPositions: number[] = [];
    for (const node of snapshot.nodes) if (node.kind === RoadNodeKind.Intersection) appendSimulationPosition(intersectionPositions, node.x, node.y, node.z);
    replacePositions(this.roadLines.geometry, roadPositions);
    replacePositions(this.laneLines.geometry, lanePositions);
    replacePositions(this.intersections.geometry, intersectionPositions);
  }

  public dispose(): void {
    this.scene.remove(this.roadLines, this.laneLines, this.intersections);
    this.roadLines.geometry.dispose(); this.laneLines.geometry.dispose(); this.intersections.geometry.dispose();
    this.roadMaterial.dispose(); this.laneMaterial.dispose(); this.intersectionMaterial.dispose();
  }
}

class AgentRenderer {
  private readonly geometry = new THREE.BoxGeometry(5, 5, 5);
  private readonly material = new THREE.MeshBasicMaterial({ color: 0x67e8f9 });
  private mesh: THREE.InstancedMesh;
  private capacity = 1_024;
  private readonly matrix = new THREE.Matrix4();
  private positions = new Float32Array(this.capacity * 3);

  public constructor(private readonly scene: THREE.Scene) { this.mesh = this.createMesh(this.capacity); this.scene.add(this.mesh); }
  public update(store: ReadonlyEntityStore, now: number): void {
    this.ensureCapacity(store.size);
    const count = store.writeSampledPositions(now, this.positions);
    for (let index = 0; index < count; index += 1) {
      const positionOffset = index * 3;
      this.matrix.makeTranslation(this.positions[positionOffset], this.positions[positionOffset + 2] + AGENT_HALF_SIZE, this.positions[positionOffset + 1]);
      this.mesh.setMatrixAt(index, this.matrix);
    }
    this.mesh.count = count; this.mesh.instanceMatrix.needsUpdate = true;
  }
  public dispose(): void { this.scene.remove(this.mesh); this.mesh.dispose(); this.geometry.dispose(); this.material.dispose(); }
  private ensureCapacity(required: number): void {
    if (required <= this.capacity) return;
    let nextCapacity = this.capacity; while (nextCapacity < required) nextCapacity *= 2;
    const previousMesh = this.mesh; this.capacity = nextCapacity; this.positions = new Float32Array(nextCapacity * 3); this.mesh = this.createMesh(nextCapacity); this.scene.remove(previousMesh); previousMesh.dispose(); this.scene.add(this.mesh);
  }
  private createMesh(capacity: number): THREE.InstancedMesh { const mesh = new THREE.InstancedMesh(this.geometry, this.material, capacity); mesh.name = 'agents'; mesh.count = 0; mesh.frustumCulled = false; return mesh; }
}

class PedestrianRenderer {
  private readonly geometry = new THREE.BoxGeometry(1.2, 3, 1.2);
  private readonly material = new THREE.MeshBasicMaterial({ color: 0xa7f3d0 });
  private mesh: THREE.InstancedMesh;
  private capacity = 1_024;
  private readonly matrix = new THREE.Matrix4();
  private positions = new Float32Array(this.capacity * 3);

  public constructor(private readonly scene: THREE.Scene) { this.mesh = this.createMesh(this.capacity); this.scene.add(this.mesh); }
  public update(store: ReadonlyPedestrianStore | null, now: number): void {
    if (store === null) { this.mesh.count = 0; this.mesh.instanceMatrix.needsUpdate = true; return; }
    this.ensureCapacity(store.size);
    const count = store.writeSampledPositions(now, this.positions);
    for (let index = 0; index < count; index += 1) {
      const positionOffset = index * 3;
      this.matrix.makeTranslation(this.positions[positionOffset], this.positions[positionOffset + 2] + PEDESTRIAN_HALF_HEIGHT, this.positions[positionOffset + 1]);
      this.mesh.setMatrixAt(index, this.matrix);
    }
    this.mesh.count = count; this.mesh.instanceMatrix.needsUpdate = true;
  }
  public dispose(): void { this.scene.remove(this.mesh); this.mesh.dispose(); this.geometry.dispose(); this.material.dispose(); }
  private ensureCapacity(required: number): void {
    if (required <= this.capacity) return;
    let nextCapacity = this.capacity; while (nextCapacity < required) nextCapacity *= 2;
    const previousMesh = this.mesh; this.capacity = nextCapacity; this.positions = new Float32Array(nextCapacity * 3); this.mesh = this.createMesh(nextCapacity); this.scene.remove(previousMesh); previousMesh.dispose(); this.scene.add(this.mesh);
  }
  private createMesh(capacity: number): THREE.InstancedMesh { const mesh = new THREE.InstancedMesh(this.geometry, this.material, capacity); mesh.name = 'pedestrians'; mesh.count = 0; mesh.frustumCulled = false; return mesh; }
}

class VehicleRenderer {
  private readonly geometry = new THREE.BoxGeometry(1, 1, 1);
  private readonly material = new THREE.MeshBasicMaterial({ color: 0x60a5fa });
  private mesh: THREE.InstancedMesh;
  private capacity = 256;
  private positions = new Float32Array(this.capacity * 3);
  private scales = new Float32Array(this.capacity * 3);
  private yaws = new Float32Array(this.capacity);
  private readonly position = new THREE.Vector3();
  private readonly scale = new THREE.Vector3();
  private readonly rotation = new THREE.Quaternion();
  private readonly matrix = new THREE.Matrix4();
  private readonly axis = new THREE.Vector3(0, 1, 0);

  public constructor(private readonly scene: THREE.Scene) { this.mesh = this.createMesh(this.capacity); this.scene.add(this.mesh); }

  public update(store: ReadonlyVehicleStore | null, now: number): number {
    if (store === null) { this.mesh.count = 0; this.mesh.instanceMatrix.needsUpdate = true; return 0; }
    this.ensureCapacity(store.size);
    const count = store.writeSampledTransforms(now, this.positions, this.scales, this.yaws);
    for (let index = 0; index < count; index += 1) {
      const offset = index * 3;
      this.position.set(this.positions[offset], this.positions[offset + 2] + this.scales[offset + 1] * 0.5, this.positions[offset + 1]);
      this.scale.set(this.scales[offset], this.scales[offset + 1], this.scales[offset + 2]);
      this.rotation.setFromAxisAngle(this.axis, this.yaws[index] ?? 0);
      this.matrix.compose(this.position, this.rotation, this.scale);
      this.mesh.setMatrixAt(index, this.matrix);
    }
    this.mesh.count = count; this.mesh.instanceMatrix.needsUpdate = true;
    return count;
  }

  public dispose(): void { this.scene.remove(this.mesh); this.mesh.dispose(); this.geometry.dispose(); this.material.dispose(); }

  private ensureCapacity(required: number): void {
    if (required <= this.capacity) return;
    let nextCapacity = this.capacity; while (nextCapacity < required) nextCapacity *= 2;
    const previousMesh = this.mesh;
    this.capacity = nextCapacity;
    this.positions = new Float32Array(nextCapacity * 3);
    this.scales = new Float32Array(nextCapacity * 3);
    this.yaws = new Float32Array(nextCapacity);
    this.mesh = this.createMesh(nextCapacity);
    this.scene.remove(previousMesh); previousMesh.dispose(); this.scene.add(this.mesh);
  }

  private createMesh(capacity: number): THREE.InstancedMesh { const mesh = new THREE.InstancedMesh(this.geometry, this.material, capacity); mesh.name = 'vehicles'; mesh.count = 0; mesh.frustumCulled = false; return mesh; }
}

class IntersectionControlRenderer {
  private readonly stopMaterial = new THREE.PointsMaterial({ color: 0xffffff, size: 4, sizeAttenuation: false });
  private readonly redMaterial = new THREE.PointsMaterial({ color: 0xef4444, size: 10, sizeAttenuation: false });
  private readonly yellowMaterial = new THREE.PointsMaterial({ color: 0xfacc15, size: 10, sizeAttenuation: false });
  private readonly greenMaterial = new THREE.PointsMaterial({ color: 0x22c55e, size: 10, sizeAttenuation: false });
  private readonly queueMaterial = new THREE.LineBasicMaterial({ color: 0x38bdf8 });
  private readonly stopLines = new THREE.Points(new THREE.BufferGeometry(), this.stopMaterial);
  private readonly red = new THREE.Points(new THREE.BufferGeometry(), this.redMaterial);
  private readonly yellow = new THREE.Points(new THREE.BufferGeometry(), this.yellowMaterial);
  private readonly green = new THREE.Points(new THREE.BufferGeometry(), this.greenMaterial);
  private readonly queues = new THREE.LineSegments(new THREE.BufferGeometry(), this.queueMaterial);

  public constructor(private readonly scene: THREE.Scene) {
    this.stopLines.name = 'traffic-stop-lines';
    this.red.name = 'traffic-signal-red';
    this.yellow.name = 'traffic-signal-yellow';
    this.green.name = 'traffic-signal-green';
    this.queues.name = 'traffic-queues';
    for (const item of [this.stopLines, this.red, this.yellow, this.green, this.queues]) item.frustumCulled = false;
    this.scene.add(this.stopLines, this.red, this.yellow, this.green, this.queues);
  }

  public update(store: ReadonlyIntersectionControlStore | null, now: number): number {
    const stopPositions: number[] = [], redPositions: number[] = [], yellowPositions: number[] = [], greenPositions: number[] = [], queuePositions: number[] = [];
    let controllerCount = 0;
    if (store !== null) for (const controller of store.active(now)) {
      controllerCount += 1;
      for (const movement of controller.movements) {
        appendSimulationPosition(stopPositions, movement.stopLineX, movement.stopLineY, movement.stopLineZ);
        const target = movement.indication === SignalIndication.Red ? redPositions : movement.indication === SignalIndication.Yellow ? yellowPositions : greenPositions;
        appendSimulationPosition(target, movement.stopLineX, movement.stopLineY, movement.stopLineZ + 1);
        if (movement.queueLength > 0) {
          appendSimulationPosition(queuePositions, movement.stopLineX, movement.stopLineY, movement.stopLineZ);
          appendSimulationPosition(queuePositions, movement.stopLineX, movement.stopLineY, movement.stopLineZ + Math.min(movement.queueLength, 10) * 2);
        }
      }
    }
    replacePositions(this.stopLines.geometry, stopPositions);
    replacePositions(this.red.geometry, redPositions);
    replacePositions(this.yellow.geometry, yellowPositions);
    replacePositions(this.green.geometry, greenPositions);
    replacePositions(this.queues.geometry, queuePositions);
    return controllerCount;
  }

  public dispose(): void {
    this.scene.remove(this.stopLines, this.red, this.yellow, this.green, this.queues);
    for (const item of [this.stopLines, this.red, this.yellow, this.green, this.queues]) item.geometry.dispose();
    this.stopMaterial.dispose(); this.redMaterial.dispose(); this.yellowMaterial.dispose(); this.greenMaterial.dispose(); this.queueMaterial.dispose();
  }
}

function appendSimulationPosition(target: number[], x: number, y: number, z: number): void { target.push(x, z, y); }
function replacePositions(geometry: THREE.BufferGeometry, values: readonly number[]): void { geometry.setAttribute('position', new THREE.Float32BufferAttribute(values, 3)); geometry.computeBoundingSphere(); }
function compareBigInt(left: bigint, right: bigint): number { return left < right ? -1 : left > right ? 1 : 0; }
function clamp(value: number, minimum: number, maximum: number): number { return Math.min(maximum, Math.max(minimum, value)); }