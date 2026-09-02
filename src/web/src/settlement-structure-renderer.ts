import * as THREE from 'three';

import {
  GeneratedBuildingUse,
  ParcelDevelopmentState,
  RegionalCorridorKind,
  RegionalRole,
  ZoneKind,
  type DistrictObservation,
  type HumanToponymObservation,
  type RegionalGenerationSnapshotMessage,
} from './regional-generation-protocol.ts';
import type { ReadonlyRegionalGenerationStore } from './regional-generation-store.ts';

const PRESENTATION_OFFSET = 0.8;
const LABEL_SCALE = 42;

export interface SettlementStructureRenderingMetrics {
  readonly settlements: number;
  readonly corridors: number;
  readonly districts: number;
  readonly parcels: number;
  readonly buildings: number;
  readonly pois: number;
  readonly labels: number;
  readonly roadSigns: number;
}

/**
 * Maps the authoritative Regional Generation read model to presentation primitives.
 * It deliberately does not infer settlement class, zone, building use, or development state.
 */
export class SettlementStructureRenderer {
  private readonly root = new THREE.Group();
  private renderedRevision = -1;
  private lastMetrics: SettlementStructureRenderingMetrics = emptyMetrics();

  public constructor(private readonly scene: THREE.Scene) {
    this.root.name = 'regional-generation';
    this.scene.add(this.root);
  }

  public get metrics(): SettlementStructureRenderingMetrics { return this.lastMetrics; }

  public update(store: ReadonlyRegionalGenerationStore): void {
    if (store.revision === this.renderedRevision) return;
    this.renderedRevision = store.revision;
    this.clearRoot();
    const snapshot = store.snapshot;
    if (snapshot === null) {
      this.lastMetrics = emptyMetrics();
      return;
    }
    this.renderSnapshot(snapshot);
  }

  public dispose(): void {
    this.clearRoot();
    this.scene.remove(this.root);
  }

  private renderSnapshot(snapshot: RegionalGenerationSnapshotMessage): void {
    this.addCorridors(snapshot);
    this.addDistricts(snapshot);
    this.addParcels(snapshot);
    this.addBuildings(snapshot);
    this.addSettlements(snapshot);
    this.addPois(snapshot);
    this.addRoadSigns(snapshot);
    const labelCount = this.addToponymLabels(snapshot);
    this.lastMetrics = Object.freeze({
      settlements: snapshot.settlements.length,
      corridors: snapshot.corridors.length,
      districts: snapshot.districts.length,
      parcels: snapshot.parcels.length,
      buildings: snapshot.buildings.length,
      pois: snapshot.pois.length,
      labels: labelCount,
      roadSigns: snapshot.roadSigns.length,
    });
  }

  private addCorridors(snapshot: RegionalGenerationSnapshotMessage): void {
    const groups = new Map<RegionalCorridorKind, number[]>();
    for (const corridor of snapshot.corridors) {
      const positions = groups.get(corridor.kind) ?? [];
      for (let index = 1; index < corridor.geometry.length; index += 1) {
        const a = corridor.geometry[index - 1], b = corridor.geometry[index];
        appendPosition(positions, a.x, a.y, a.z + PRESENTATION_OFFSET);
        appendPosition(positions, b.x, b.y, b.z + PRESENTATION_OFFSET);
      }
      groups.set(corridor.kind, positions);
    }
    for (const [kind, positions] of groups) {
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
      const material = new THREE.LineBasicMaterial({ color: corridorColor(kind) });
      const lines = new THREE.LineSegments(geometry, material);
      lines.name = `regional-corridors-${String(kind)}`;
      lines.frustumCulled = false;
      this.root.add(lines);
    }
  }

  private addDistricts(snapshot: RegionalGenerationSnapshotMessage): void {
    const positions: number[] = [];
    for (const district of snapshot.districts) appendBoundsOutline(positions, district, district.maxZ + PRESENTATION_OFFSET * 0.5);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    const material = new THREE.LineBasicMaterial({ color: 0xe2e8f0, transparent: true, opacity: 0.36 });
    const lines = new THREE.LineSegments(geometry, material);
    lines.name = 'regional-districts';
    lines.frustumCulled = false;
    this.root.add(lines);
  }

  private addParcels(snapshot: RegionalGenerationSnapshotMessage): void {
    const geometry = new THREE.BoxGeometry(1, 1, 1);
    const material = new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.24, depthWrite: false });
    const mesh = new THREE.InstancedMesh(geometry, material, Math.max(1, snapshot.parcels.length));
    const matrix = new THREE.Matrix4();
    mesh.name = 'regional-parcels';
    mesh.count = snapshot.parcels.length;
    mesh.frustumCulled = false;
    for (let index = 0; index < snapshot.parcels.length; index += 1) {
      const parcel = snapshot.parcels[index];
      boundsMatrix(parcel, Math.max(0.35, parcel.maxZ - parcel.minZ), matrix, PRESENTATION_OFFSET * 0.25);
      mesh.setMatrixAt(index, matrix);
      mesh.setColorAt(index, zoneColor(parcel.zone, parcel.developmentState));
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) mesh.instanceColor.needsUpdate = true;
    this.root.add(mesh);
  }

  private addBuildings(snapshot: RegionalGenerationSnapshotMessage): void {
    const geometry = new THREE.BoxGeometry(1, 1, 1);
    const material = new THREE.MeshBasicMaterial({ vertexColors: true });
    const mesh = new THREE.InstancedMesh(geometry, material, Math.max(1, snapshot.buildings.length));
    const matrix = new THREE.Matrix4();
    mesh.name = 'regional-buildings';
    mesh.count = snapshot.buildings.length;
    mesh.frustumCulled = false;
    for (let index = 0; index < snapshot.buildings.length; index += 1) {
      const building = snapshot.buildings[index];
      boundsMatrix(building, Math.max(1, building.maxZ - building.minZ), matrix, 0);
      mesh.setMatrixAt(index, matrix);
      mesh.setColorAt(index, buildingColor(building.use));
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) mesh.instanceColor.needsUpdate = true;
    this.root.add(mesh);
  }

  private addSettlements(snapshot: RegionalGenerationSnapshotMessage): void {
    const geometry = new THREE.SphereGeometry(1, 10, 8);
    const material = new THREE.MeshBasicMaterial({ vertexColors: true });
    const mesh = new THREE.InstancedMesh(geometry, material, Math.max(1, snapshot.settlements.length));
    const matrix = new THREE.Matrix4();
    const scale = new THREE.Vector3();
    const position = new THREE.Vector3();
    mesh.name = 'regional-settlements';
    mesh.count = snapshot.settlements.length;
    mesh.frustumCulled = false;
    for (let index = 0; index < snapshot.settlements.length; index += 1) {
      const settlement = snapshot.settlements[index];
      // Scale is presentation-only and uses Simulation-provided influence radius; no City/Town/Village inference occurs here.
      const markerRadius = clamp(Math.sqrt(settlement.influenceRadiusMeters) * 0.18, 5, 24);
      position.set(settlement.x, settlement.z + markerRadius + PRESENTATION_OFFSET, settlement.y);
      scale.set(markerRadius, markerRadius, markerRadius);
      matrix.compose(position, IDENTITY_QUATERNION, scale);
      mesh.setMatrixAt(index, matrix);
      mesh.setColorAt(index, settlementRoleColor(settlement.role));
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) mesh.instanceColor.needsUpdate = true;
    this.root.add(mesh);
  }

  private addPois(snapshot: RegionalGenerationSnapshotMessage): void {
    const positions: number[] = [];
    for (const poi of snapshot.pois) appendPosition(positions, poi.x, poi.y, poi.z + 4);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    const material = new THREE.PointsMaterial({ color: 0xf8fafc, size: 6, sizeAttenuation: false });
    const points = new THREE.Points(geometry, material);
    points.name = 'regional-pois';
    points.frustumCulled = false;
    this.root.add(points);
  }

  private addRoadSigns(snapshot: RegionalGenerationSnapshotMessage): void {
    const positions: number[] = [];
    for (const sign of snapshot.roadSigns) appendPosition(positions, sign.x, sign.y, sign.z + 2.5);
    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.Float32BufferAttribute(positions, 3));
    const material = new THREE.PointsMaterial({ color: 0xfde68a, size: 8, sizeAttenuation: false });
    const points = new THREE.Points(geometry, material);
    points.name = 'regional-road-signs';
    points.frustumCulled = false;
    points.userData.labels = snapshot.roadSigns.map((sign) => ({ roadSignId: sign.roadSignId.toString(), corridorId: sign.corridorId.toString(), destinationSettlementId: sign.destinationSettlementId.toString(), text: sign.text, kind: sign.kind }));
    this.root.add(points);
  }

  private addToponymLabels(snapshot: RegionalGenerationSnapshotMessage): number {
    const toponyms = new Map(snapshot.toponyms.map((item) => [item.toponymId, item] as const));
    const anchors = new Map<bigint, THREE.Vector3>();
    for (const settlement of snapshot.settlements) anchors.set(settlement.nameId, simulationPosition(settlement.x, settlement.y, settlement.z + 28));
    for (const district of snapshot.districts) anchors.set(district.nameId, simulationPosition((district.minX + district.maxX) * 0.5, (district.minY + district.maxY) * 0.5, district.maxZ + 9));
    for (const corridor of snapshot.corridors) {
      if (corridor.nameId === 0n) continue;
      const middle = corridor.geometry[Math.floor(corridor.geometry.length / 2)];
      anchors.set(corridor.nameId, simulationPosition(middle.x, middle.y, middle.z + 7));
    }
    for (const poi of snapshot.pois) if (poi.nameId !== 0n) anchors.set(poi.nameId, simulationPosition(poi.x, poi.y, poi.z + 8));

    let count = 0;
    for (const [toponymId, anchor] of anchors) {
      const toponym = toponyms.get(toponymId);
      if (toponym === undefined) continue;
      const sprite = createTextSprite(toponym);
      sprite.position.copy(anchor);
      this.root.add(sprite);
      count += 1;
    }
    return count;
  }

  private clearRoot(): void {
    for (const child of [...this.root.children]) {
      this.root.remove(child);
      disposeObject(child);
    }
  }
}

const IDENTITY_QUATERNION = new THREE.Quaternion();

function boundsMatrix(bounds: { readonly minX: number; readonly minY: number; readonly minZ: number; readonly maxX: number; readonly maxY: number; readonly maxZ: number }, height: number, target: THREE.Matrix4, zOffset: number): void {
  const width = Math.max(0.1, bounds.maxX - bounds.minX);
  const depth = Math.max(0.1, bounds.maxY - bounds.minY);
  const visualHeight = Math.max(0.1, height);
  const position = new THREE.Vector3((bounds.minX + bounds.maxX) * 0.5, bounds.minZ + visualHeight * 0.5 + zOffset, (bounds.minY + bounds.maxY) * 0.5);
  const scale = new THREE.Vector3(width, visualHeight, depth);
  target.compose(position, IDENTITY_QUATERNION, scale);
}

function appendBoundsOutline(target: number[], bounds: DistrictObservation, z: number): void {
  const corners = [[bounds.minX, bounds.minY], [bounds.maxX, bounds.minY], [bounds.maxX, bounds.maxY], [bounds.minX, bounds.maxY]] as const;
  for (let index = 0; index < corners.length; index += 1) {
    const a = corners[index], b = corners[(index + 1) % corners.length];
    appendPosition(target, a[0], a[1], z); appendPosition(target, b[0], b[1], z);
  }
}

function appendPosition(target: number[], x: number, y: number, z: number): void { target.push(x, z, y); }
function simulationPosition(x: number, y: number, z: number): THREE.Vector3 { return new THREE.Vector3(x, z, y); }

function corridorColor(kind: RegionalCorridorKind): number {
  switch (kind) {
    case RegionalCorridorKind.PrimaryRoad: return 0xf8fafc;
    case RegionalCorridorKind.RegionalRoad: return 0xcbd5e1;
    case RegionalCorridorKind.IntercityRoad: return 0xfbbf24;
    case RegionalCorridorKind.Railway: return 0xc084fc;
  }
}

function settlementRoleColor(role: RegionalRole): THREE.Color {
  const color = new THREE.Color();
  switch (role) {
    case RegionalRole.LocalService: return color.setHex(0x7dd3fc);
    case RegionalRole.Agricultural: return color.setHex(0x86efac);
    case RegionalRole.Market: return color.setHex(0xfde047);
    case RegionalRole.Administrative: return color.setHex(0xa5b4fc);
    case RegionalRole.Industrial: return color.setHex(0x94a3b8);
    case RegionalRole.Port: return color.setHex(0x22d3ee);
    case RegionalRole.TransportHub: return color.setHex(0xfb923c);
    case RegionalRole.Resource: return color.setHex(0xf0abfc);
  }
}

function zoneColor(zone: ZoneKind, developmentState: ParcelDevelopmentState): THREE.Color {
  const base = new THREE.Color();
  switch (zone) {
    case ZoneKind.Residential: base.setHex(0x60a5fa); break;
    case ZoneKind.Commercial: base.setHex(0xfbbf24); break;
    case ZoneKind.Industrial: base.setHex(0x94a3b8); break;
    case ZoneKind.MixedUse: base.setHex(0xc084fc); break;
    case ZoneKind.Civic: base.setHex(0xf472b6); break;
    case ZoneKind.Agricultural: base.setHex(0x4ade80); break;
    case ZoneKind.OpenSpace: base.setHex(0x2dd4bf); break;
  }
  if (developmentState === ParcelDevelopmentState.Vacant) base.multiplyScalar(0.55);
  else if (developmentState === ParcelDevelopmentState.Developing) base.lerp(new THREE.Color(0xffffff), 0.18);
  else if (developmentState === ParcelDevelopmentState.Redeveloping) base.lerp(new THREE.Color(0xf97316), 0.28);
  return base;
}

function buildingColor(use: GeneratedBuildingUse): THREE.Color {
  const color = new THREE.Color();
  switch (use) {
    case GeneratedBuildingUse.Residential: return color.setHex(0x93c5fd);
    case GeneratedBuildingUse.Commercial: return color.setHex(0xfcd34d);
    case GeneratedBuildingUse.Industrial: return color.setHex(0x9ca3af);
    case GeneratedBuildingUse.MixedUse: return color.setHex(0xd8b4fe);
    case GeneratedBuildingUse.Civic: return color.setHex(0xf9a8d4);
    case GeneratedBuildingUse.Transport: return color.setHex(0xfdba74);
    case GeneratedBuildingUse.Utility: return color.setHex(0x67e8f9);
  }
}

function createTextSprite(toponym: HumanToponymObservation): THREE.Sprite {
  const canvas = document.createElement('canvas');
  canvas.width = 512; canvas.height = 96;
  const context = canvas.getContext('2d');
  if (context !== null) {
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.font = '600 38px sans-serif';
    context.textAlign = 'center'; context.textBaseline = 'middle';
    context.fillStyle = 'rgba(15,23,42,0.80)'; context.fillRect(0, 8, canvas.width, 80);
    context.fillStyle = '#f8fafc'; context.fillText(toponym.name, canvas.width / 2, canvas.height / 2, canvas.width - 24);
  }
  const texture = new THREE.CanvasTexture(canvas);
  texture.needsUpdate = true;
  const material = new THREE.SpriteMaterial({ map: texture, transparent: true, depthTest: false });
  const sprite = new THREE.Sprite(material);
  sprite.name = `regional-toponym-${toponym.toponymId.toString()}`;
  sprite.scale.set(LABEL_SCALE * 4.5, LABEL_SCALE, 1);
  sprite.userData.toponymId = toponym.toponymId.toString();
  sprite.userData.kind = toponym.kind;
  sprite.userData.sourceNaturalToponymId = toponym.sourceNaturalToponymId.toString();
  sprite.userData.sourceFeatureId = toponym.sourceFeatureId.toString();
  sprite.userData.parentHumanToponymId = toponym.parentHumanToponymId.toString();
  return sprite;
}

function disposeObject(object: THREE.Object3D): void {
  object.traverse((item) => {
    const mesh = item as THREE.Mesh;
    mesh.geometry?.dispose();
    const material = (item as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
    if (material === undefined) return;
    const materials = Array.isArray(material) ? material : [material];
    for (const current of materials) {
      const texture = (current as THREE.SpriteMaterial).map;
      if (texture !== null && texture !== undefined) texture.dispose();
      current.dispose();
    }
  });
}

function emptyMetrics(): SettlementStructureRenderingMetrics { return Object.freeze({ settlements: 0, corridors: 0, districts: 0, parcels: 0, buildings: 0, pois: 0, labels: 0, roadSigns: 0 }); }
function clamp(value: number, minimum: number, maximum: number): number { return Math.max(minimum, Math.min(maximum, value)); }
