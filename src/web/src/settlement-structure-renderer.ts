import * as THREE from 'three';

import {
  BuildingLifecycleStatus,
  RegionalRelationKind,
  SettlementScale,
  SettlementTrend,
  type BuildingLifecycleObservation,
  type PersistentRegionalEvolutionSnapshotMessage,
  type SettlementEvolutionObservation,
} from './persistent-regional-evolution-protocol.ts';
import type { ReadonlyPersistentRegionalEvolutionStore } from './persistent-regional-evolution-store.ts';
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
const MAX_VISIBLE_TOPONYM_LABELS = 192;
const TOPONYM_ATLAS_CELL_WIDTH = 256;
const TOPONYM_ATLAS_CELL_HEIGHT = 64;
const TOPONYM_ATLAS_COLUMNS = 8;

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

export interface SettlementStructureStableRelations {
  readonly settlements: readonly Readonly<{ settlementId: bigint; nameId: bigint }>[];
  readonly districts: readonly Readonly<{ districtId: bigint; settlementId: bigint; nameId: bigint }>[];
  readonly parcels: readonly Readonly<{ parcelId: bigint; settlementId: bigint; districtId: bigint; buildingId: bigint }>[];
  readonly buildings: readonly Readonly<{ buildingId: bigint; parcelId: bigint }>[];
  readonly pois: readonly Readonly<{ poiId: bigint; settlementId: bigint; buildingId: bigint; nameId: bigint }>[];
}

/**
 * Maps authoritative Regional Generation geometry plus optional Protocol 2.19 evolution state to presentation primitives.
 * Static baseline resources and dynamic evolution resources are kept in separate groups so annual evolution updates do not
 * rebuild immutable corridor/district/POI/sign/toponym presentation data.
 */
export class SettlementStructureRenderer {
  private readonly root = new THREE.Group();
  private readonly staticRoot = new THREE.Group();
  private readonly dynamicRoot = new THREE.Group();
  private renderedRevision = -1;
  private renderedEvolutionRevision = -1;
  private lastMetrics: SettlementStructureRenderingMetrics = emptyMetrics();
  private lastRelations: SettlementStructureStableRelations = emptyRelations();

  public constructor(private readonly scene: THREE.Scene) {
    this.root.name = 'regional-generation';
    this.staticRoot.name = 'regional-generation-static';
    this.dynamicRoot.name = 'regional-generation-evolution';
    this.root.add(this.staticRoot, this.dynamicRoot);
    this.scene.add(this.root);
  }

  public get metrics(): SettlementStructureRenderingMetrics { return this.lastMetrics; }
  public get relations(): SettlementStructureStableRelations { return this.lastRelations; }

  public update(store: ReadonlyRegionalGenerationStore, evolution: ReadonlyPersistentRegionalEvolutionStore | null = null): void {
    const evolutionRevision = evolution?.revision ?? -1;
    const baselineChanged = store.revision !== this.renderedRevision;
    const evolutionChanged = evolutionRevision !== this.renderedEvolutionRevision;
    if (!baselineChanged && !evolutionChanged) return;

    const snapshot = store.snapshot;
    this.renderedRevision = store.revision;
    this.renderedEvolutionRevision = evolutionRevision;
    this.root.userData.currentYear = evolution?.snapshot?.currentYear ?? null;

    if (snapshot === null) {
      this.clearGroup(this.staticRoot);
      this.clearGroup(this.dynamicRoot);
      this.lastMetrics = emptyMetrics();
      this.lastRelations = emptyRelations();
      return;
    }

    this.lastRelations = createRelations(snapshot, evolution?.snapshot ?? null);

    let labelCount = this.lastMetrics.labels;
    if (baselineChanged) {
      this.clearGroup(this.staticRoot);
      this.addCorridors(snapshot);
      this.addDistricts(snapshot);
      this.addPois(snapshot);
      this.addRoadSigns(snapshot);
      labelCount = this.addToponymLabels(snapshot);
    }

    this.clearGroup(this.dynamicRoot);
    this.addEvolutionRelations(evolution?.snapshot ?? null);
    this.addParcels(snapshot, evolution);
    const buildingCount = this.addBuildings(snapshot, evolution);
    const settlementCount = this.addSettlements(snapshot, evolution);

    this.lastMetrics = Object.freeze({
      settlements: settlementCount,
      corridors: snapshot.corridors.length,
      districts: snapshot.districts.length,
      parcels: snapshot.parcels.length,
      buildings: buildingCount,
      pois: snapshot.pois.length,
      labels: labelCount,
      roadSigns: snapshot.roadSigns.length,
    });
  }

  public dispose(): void {
    this.clearGroup(this.staticRoot);
    this.clearGroup(this.dynamicRoot);
    this.lastRelations = emptyRelations();
    this.root.remove(this.staticRoot, this.dynamicRoot);
    this.scene.remove(this.root);
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
      this.staticRoot.add(lines);
    }
  }

  private addEvolutionRelations(snapshot: PersistentRegionalEvolutionSnapshotMessage | null): void {
    if (snapshot === null || snapshot.relations.length === 0) return;
    const settlements = new Map(snapshot.settlements.map((item) => [item.settlementId, item] as const));
    const groups = new Map<RegionalRelationKind, { positions: number[]; relations: object[] }>();
    for (const relation of snapshot.relations) {
      const from = settlements.get(relation.fromSettlementId);
      const to = settlements.get(relation.toSettlementId);
      if (from === undefined || to === undefined) continue;
      const group = groups.get(relation.kind) ?? { positions: [], relations: [] };
      appendPosition(group.positions, from.x, from.y, from.z + 3);
      appendPosition(group.positions, to.x, to.y, to.z + 3);
      group.relations.push(Object.freeze({ relationId: relation.relationId, fromSettlementId: relation.fromSettlementId, toSettlementId: relation.toSettlementId, kind: relation.kind, strength: relation.strength, isActive: relation.isActive, sinceYear: relation.sinceYear }));
      groups.set(relation.kind, group);
    }
    for (const [kind, group] of groups) {
      const geometry = new THREE.BufferGeometry();
      geometry.setAttribute('position', new THREE.Float32BufferAttribute(group.positions, 3));
      const material = new THREE.LineBasicMaterial({ color: regionalRelationColor(kind), transparent: true, opacity: 0.58 });
      const lines = new THREE.LineSegments(geometry, material);
      lines.name = `regional-evolution-relations-${String(kind)}`;
      lines.frustumCulled = false;
      lines.userData.relations = Object.freeze([...group.relations]);
      this.dynamicRoot.add(lines);
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
    lines.userData.relations = this.lastRelations.districts;
    this.staticRoot.add(lines);
  }

  private addParcels(snapshot: RegionalGenerationSnapshotMessage, evolution: ReadonlyPersistentRegionalEvolutionStore | null): void {
    const geometry = new THREE.BoxGeometry(1, 1, 1);
    const material = new THREE.MeshBasicMaterial({ vertexColors: true, transparent: true, opacity: 0.24, depthWrite: false });
    const mesh = new THREE.InstancedMesh(geometry, material, Math.max(1, snapshot.parcels.length));
    const matrix = new THREE.Matrix4();
    mesh.name = 'regional-parcels';
    mesh.count = snapshot.parcels.length;
    mesh.frustumCulled = false;
    mesh.userData.relations = this.lastRelations.parcels;
    mesh.userData.evolution = snapshot.parcels.map((parcel) => {
      const current = evolution?.getParcel(parcel.parcelId);
      return current === undefined ? null : Object.freeze({ parcelId: current.parcelId, settlementId: current.settlementId, developmentDemand: current.developmentDemand, landValue: current.landValue, developmentState: current.developmentState, buildingId: current.buildingId });
    });
    for (let index = 0; index < snapshot.parcels.length; index += 1) {
      const parcel = snapshot.parcels[index];
      const current = evolution?.getParcel(parcel.parcelId);
      boundsMatrix(parcel, Math.max(0.35, parcel.maxZ - parcel.minZ), matrix, PRESENTATION_OFFSET * 0.25);
      mesh.setMatrixAt(index, matrix);
      mesh.setColorAt(index, zoneColor(parcel.zone, (current?.developmentState ?? parcel.developmentState) as ParcelDevelopmentState));
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) mesh.instanceColor.needsUpdate = true;
    this.dynamicRoot.add(mesh);
  }

  private addBuildings(snapshot: RegionalGenerationSnapshotMessage, evolution: ReadonlyPersistentRegionalEvolutionStore | null): number {
    const baselineById = new Map(snapshot.buildings.map((item) => [item.buildingId, item] as const));
    const parcelById = new Map(snapshot.parcels.map((item) => [item.parcelId, item] as const));
    const effective: Array<{ baseline: RegionalGenerationSnapshotMessage['buildings'][number] | null; current: BuildingLifecycleObservation | null }> = snapshot.buildings.map((baseline) => ({ baseline, current: evolution?.getBuilding(baseline.buildingId) ?? null }));
    for (const current of evolution?.snapshot?.buildings ?? []) {
      if (!baselineById.has(current.buildingId) && parcelById.has(current.parcelId)) effective.push({ baseline: null, current });
    }

    const geometry = new THREE.BoxGeometry(1, 1, 1);
    const material = new THREE.MeshBasicMaterial({ vertexColors: true });
    const mesh = new THREE.InstancedMesh(geometry, material, Math.max(1, effective.length));
    const matrix = new THREE.Matrix4();
    mesh.name = 'regional-buildings';
    mesh.count = effective.length;
    mesh.frustumCulled = false;
    mesh.userData.relations = Object.freeze(effective.map(({ baseline, current }) => Object.freeze({ buildingId: current?.buildingId ?? baseline!.buildingId, parcelId: current?.parcelId ?? baseline!.parcelId })));
    mesh.userData.evolution = Object.freeze(effective.map(({ current }) => current === null ? null : Object.freeze({ buildingId: current.buildingId, parcelId: current.parcelId, use: current.use, condition: current.condition, occupancy: current.occupancy, capacity: current.capacity, status: current.status, builtYear: current.builtYear, lastChangedYear: current.lastChangedYear })));

    for (let index = 0; index < effective.length; index += 1) {
      const { baseline, current } = effective[index];
      const bounds = baseline ?? parcelById.get(current!.parcelId)!;
      const status = current?.status ?? BuildingLifecycleStatus.Active;
      const baselineHeight = baseline === null ? evolutionBuildingHeight(current!) : Math.max(1, baseline.maxZ - baseline.minZ);
      const visualHeight = status === BuildingLifecycleStatus.Demolished ? 0.15 : baselineHeight;
      boundsMatrix(bounds, visualHeight, matrix, 0);
      mesh.setMatrixAt(index, matrix);
      const use = (current?.use ?? baseline!.use) as GeneratedBuildingUse;
      mesh.setColorAt(index, buildingLifecycleColor(use, status, current?.condition ?? 1));
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) mesh.instanceColor.needsUpdate = true;
    this.dynamicRoot.add(mesh);
    return effective.length;
  }

  private addSettlements(snapshot: RegionalGenerationSnapshotMessage, evolution: ReadonlyPersistentRegionalEvolutionStore | null): number {
    const baselineById = new Map(snapshot.settlements.map((item) => [item.settlementId, item] as const));
    const effective: Array<{ baseline: RegionalGenerationSnapshotMessage['settlements'][number] | null; current: SettlementEvolutionObservation | null }> = snapshot.settlements.map((baseline) => ({ baseline, current: evolution?.getSettlement(baseline.settlementId) ?? null }));
    for (const current of evolution?.snapshot?.settlements ?? []) {
      if (!baselineById.has(current.settlementId)) effective.push({ baseline: null, current });
    }

    const geometry = new THREE.SphereGeometry(1, 10, 8);
    const material = new THREE.MeshBasicMaterial({ vertexColors: true });
    const mesh = new THREE.InstancedMesh(geometry, material, Math.max(1, effective.length));
    const matrix = new THREE.Matrix4();
    const scale = new THREE.Vector3();
    const position = new THREE.Vector3();
    mesh.name = 'regional-settlements';
    mesh.count = effective.length;
    mesh.frustumCulled = false;
    mesh.userData.relations = Object.freeze(effective.map(({ baseline, current }) => Object.freeze({ settlementId: current?.settlementId ?? baseline!.settlementId, nameId: baseline?.nameId ?? 0n })));
    mesh.userData.evolution = Object.freeze(effective.map(({ current }) => current === null ? null : Object.freeze({ settlementId: current.settlementId, scale: current.scale, trend: current.trend, isActive: current.isActive, population: current.population, jobs: current.jobs, influenceRadiusMeters: current.influenceRadiusMeters, establishedYear: current.establishedYear, dormantSinceYear: current.dormantSinceYear })));

    for (let index = 0; index < effective.length; index += 1) {
      const { baseline, current } = effective[index];
      const influenceRadius = current?.influenceRadiusMeters ?? baseline!.influenceRadiusMeters;
      const scaleMultiplier = current === null ? 1 : settlementScaleMultiplier(current.scale);
      const markerRadius = clamp(Math.sqrt(influenceRadius) * 0.18 * scaleMultiplier, 4, 34);
      position.set(current?.x ?? baseline!.x, (current?.z ?? baseline!.z) + markerRadius + PRESENTATION_OFFSET, current?.y ?? baseline!.y);
      scale.set(markerRadius, markerRadius, markerRadius);
      matrix.compose(position, IDENTITY_QUATERNION, scale);
      mesh.setMatrixAt(index, matrix);
      mesh.setColorAt(index, current === null ? settlementRoleColor(baseline!.role) : settlementEvolutionColor(current.scale, current.trend, current.isActive));
    }
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor !== null) mesh.instanceColor.needsUpdate = true;
    this.dynamicRoot.add(mesh);
    return effective.length;
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
    points.userData.relations = this.lastRelations.pois;
    this.staticRoot.add(points);
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
    this.staticRoot.add(points);
  }

  private addToponymLabels(snapshot: RegionalGenerationSnapshotMessage): number {
    const toponyms = new Map(snapshot.toponyms.map((item) => [item.toponymId, item] as const));
    const anchors = new Map<bigint, { position: THREE.Vector3; priority: number }>();
    const setAnchor = (id: bigint, position: THREE.Vector3, priority: number): void => {
      if (id === 0n) return;
      const existing = anchors.get(id);
      if (existing === undefined || priority < existing.priority) anchors.set(id, { position, priority });
    };
    for (const settlement of snapshot.settlements) setAnchor(settlement.nameId, simulationPosition(settlement.x, settlement.y, settlement.z + 28), 0);
    for (const district of snapshot.districts) setAnchor(district.nameId, simulationPosition((district.minX + district.maxX) * 0.5, (district.minY + district.maxY) * 0.5, district.maxZ + 9), 1);
    for (const corridor of snapshot.corridors) {
      if (corridor.nameId === 0n) continue;
      const middle = corridor.geometry[Math.floor(corridor.geometry.length / 2)];
      setAnchor(corridor.nameId, simulationPosition(middle.x, middle.y, middle.z + 7), 2);
    }
    for (const poi of snapshot.pois) setAnchor(poi.nameId, simulationPosition(poi.x, poi.y, poi.z + 8), 3);

    const selected = [...anchors.entries()]
      .filter(([toponymId]) => toponyms.has(toponymId))
      .sort((a, b) => a[1].priority - b[1].priority || (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0))
      .slice(0, MAX_VISIBLE_TOPONYM_LABELS);

    if (selected.length > 0) this.staticRoot.add(createToponymLabelBatch(selected, toponyms));
    this.staticRoot.userData.labelCapacity = MAX_VISIBLE_TOPONYM_LABELS;
    this.staticRoot.userData.totalLabelAnchors = anchors.size;
    return selected.length;
  }

  private clearGroup(group: THREE.Group): void {
    for (const child of [...group.children]) {
      group.remove(child);
      disposeObject(child);
    }
  }
}

const IDENTITY_QUATERNION = new THREE.Quaternion();

function createRelations(snapshot: RegionalGenerationSnapshotMessage, evolution: PersistentRegionalEvolutionSnapshotMessage | null): SettlementStructureStableRelations {
  const parcelEvolution = new Map((evolution?.parcels ?? []).map((item) => [item.parcelId, item] as const));
  const buildingRelations = snapshot.buildings.map((item) => Object.freeze({ buildingId: item.buildingId, parcelId: item.parcelId }));
  const baselineBuildingIds = new Set(snapshot.buildings.map((item) => item.buildingId));
  for (const item of evolution?.buildings ?? []) {
    if (!baselineBuildingIds.has(item.buildingId)) buildingRelations.push(Object.freeze({ buildingId: item.buildingId, parcelId: item.parcelId }));
  }
  return Object.freeze({
    settlements: Object.freeze(snapshot.settlements.map((item) => Object.freeze({ settlementId: item.settlementId, nameId: item.nameId }))),
    districts: Object.freeze(snapshot.districts.map((item) => Object.freeze({ districtId: item.districtId, settlementId: item.settlementId, nameId: item.nameId }))),
    parcels: Object.freeze(snapshot.parcels.map((item) => Object.freeze({ parcelId: item.parcelId, settlementId: item.settlementId, districtId: item.districtId, buildingId: parcelEvolution.get(item.parcelId)?.buildingId ?? item.buildingId }))),
    buildings: Object.freeze(buildingRelations),
    pois: Object.freeze(snapshot.pois.map((item) => Object.freeze({ poiId: item.poiId, settlementId: item.settlementId, buildingId: item.buildingId, nameId: item.nameId }))),
  });
}

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

function regionalRelationColor(kind: RegionalRelationKind): number {
  switch (kind) {
    case RegionalRelationKind.Commuting: return 0x38bdf8;
    case RegionalRelationKind.Trade: return 0xfbbf24;
    case RegionalRelationKind.Service: return 0x4ade80;
    case RegionalRelationKind.Metro: return 0xc084fc;
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

function settlementEvolutionColor(scale: SettlementScale, trend: SettlementTrend, isActive: boolean): THREE.Color {
  const color = new THREE.Color();
  switch (scale) {
    case SettlementScale.Hamlet: color.setHex(0x86efac); break;
    case SettlementScale.Village: color.setHex(0x4ade80); break;
    case SettlementScale.Town: color.setHex(0x38bdf8); break;
    case SettlementScale.City: color.setHex(0x818cf8); break;
    case SettlementScale.Metropolis: color.setHex(0xe879f9); break;
  }
  switch (trend) {
    case SettlementTrend.Growing: color.lerp(new THREE.Color(0x22c55e), 0.22); break;
    case SettlementTrend.Stable: break;
    case SettlementTrend.Declining: color.lerp(new THREE.Color(0xef4444), 0.30); break;
    case SettlementTrend.Recovering: color.lerp(new THREE.Color(0x22d3ee), 0.28); break;
    case SettlementTrend.Dormant: color.multiplyScalar(0.42); break;
  }
  if (!isActive) color.multiplyScalar(0.55);
  return color;
}

function settlementScaleMultiplier(scale: SettlementScale): number {
  switch (scale) {
    case SettlementScale.Hamlet: return 0.75;
    case SettlementScale.Village: return 0.9;
    case SettlementScale.Town: return 1;
    case SettlementScale.City: return 1.14;
    case SettlementScale.Metropolis: return 1.3;
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

function buildingLifecycleColor(use: GeneratedBuildingUse, status: BuildingLifecycleStatus, condition: number): THREE.Color {
  const color = buildingColor(use).multiplyScalar(0.55 + clamp(condition, 0, 1) * 0.45);
  switch (status) {
    case BuildingLifecycleStatus.Active: return color;
    case BuildingLifecycleStatus.Vacant: return color.multiplyScalar(0.58);
    case BuildingLifecycleStatus.Renovating: return color.lerp(new THREE.Color(0xffffff), 0.38);
    case BuildingLifecycleStatus.Repurposing: return color.lerp(new THREE.Color(0xc084fc), 0.42);
    case BuildingLifecycleStatus.Abandoned: return color.setHex(0x475569);
    case BuildingLifecycleStatus.Demolished: return color.setHex(0x7f1d1d);
  }
}

function evolutionBuildingHeight(building: BuildingLifecycleObservation): number {
  return clamp(3 + Math.log2(Math.max(1, building.capacity) + 1), 3, 20);
}

function createToponymLabelBatch(
  selected: readonly [bigint, { position: THREE.Vector3; priority: number }][],
  toponyms: ReadonlyMap<bigint, HumanToponymObservation>,
): THREE.Mesh<THREE.InstancedBufferGeometry, THREE.ShaderMaterial> {
  const rows = Math.ceil(selected.length / TOPONYM_ATLAS_COLUMNS);
  const canvas = document.createElement('canvas');
  canvas.width = TOPONYM_ATLAS_COLUMNS * TOPONYM_ATLAS_CELL_WIDTH;
  canvas.height = Math.max(1, rows) * TOPONYM_ATLAS_CELL_HEIGHT;
  const context = canvas.getContext('2d');
  const instancePositions = new Float32Array(selected.length * 3);
  const atlasRects = new Float32Array(selected.length * 4);
  const metadata: object[] = [];
  const center = new THREE.Vector3();

  if (context !== null) {
    context.clearRect(0, 0, canvas.width, canvas.height);
    context.font = '600 26px sans-serif';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
  }

  for (let index = 0; index < selected.length; index += 1) {
    const [toponymId, anchor] = selected[index];
    const toponym = toponyms.get(toponymId)!;
    const column = index % TOPONYM_ATLAS_COLUMNS;
    const row = Math.floor(index / TOPONYM_ATLAS_COLUMNS);
    const x = column * TOPONYM_ATLAS_CELL_WIDTH;
    const y = row * TOPONYM_ATLAS_CELL_HEIGHT;
    if (context !== null) {
      context.fillStyle = 'rgba(15,23,42,0.80)';
      context.fillRect(x + 2, y + 5, TOPONYM_ATLAS_CELL_WIDTH - 4, TOPONYM_ATLAS_CELL_HEIGHT - 10);
      context.fillStyle = '#f8fafc';
      context.fillText(toponym.name, x + TOPONYM_ATLAS_CELL_WIDTH / 2, y + TOPONYM_ATLAS_CELL_HEIGHT / 2, TOPONYM_ATLAS_CELL_WIDTH - 16);
    }
    instancePositions.set([anchor.position.x, anchor.position.y, anchor.position.z], index * 3);
    atlasRects.set([
      x / canvas.width,
      1 - ((row + 1) * TOPONYM_ATLAS_CELL_HEIGHT) / canvas.height,
      TOPONYM_ATLAS_CELL_WIDTH / canvas.width,
      TOPONYM_ATLAS_CELL_HEIGHT / canvas.height,
    ], index * 4);
    center.add(anchor.position);
    metadata.push(Object.freeze({
      toponymId: toponym.toponymId.toString(),
      kind: toponym.kind,
      sourceNaturalToponymId: toponym.sourceNaturalToponymId.toString(),
      sourceFeatureId: toponym.sourceFeatureId.toString(),
      parentHumanToponymId: toponym.parentHumanToponymId.toString(),
      priority: anchor.priority,
    }));
  }
  center.multiplyScalar(1 / selected.length);
  let radius = 0;
  for (const [, anchor] of selected) radius = Math.max(radius, center.distanceTo(anchor.position));

  const texture = new THREE.CanvasTexture(canvas);
  texture.needsUpdate = true;
  texture.generateMipmaps = false;
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;

  const geometry = new THREE.InstancedBufferGeometry();
  geometry.setAttribute('position', new THREE.Float32BufferAttribute([
    -0.5, -0.5, 0,
    0.5, -0.5, 0,
    0.5, 0.5, 0,
    -0.5, 0.5, 0,
  ], 3));
  geometry.setAttribute('uv', new THREE.Float32BufferAttribute([
    0, 0,
    1, 0,
    1, 1,
    0, 1,
  ], 2));
  geometry.setIndex([0, 1, 2, 0, 2, 3]);
  geometry.setAttribute('labelPosition', new THREE.InstancedBufferAttribute(instancePositions, 3));
  geometry.setAttribute('atlasRect', new THREE.InstancedBufferAttribute(atlasRects, 4));
  geometry.instanceCount = selected.length;

  const material = new THREE.ShaderMaterial({
    uniforms: { labelAtlas: { value: texture } },
    vertexShader: `
      attribute vec3 labelPosition;
      attribute vec4 atlasRect;
      varying vec2 vLabelUv;
      void main() {
        vec4 viewCenter = viewMatrix * modelMatrix * vec4(labelPosition, 1.0);
        viewCenter.xy += position.xy * vec2(${String(LABEL_SCALE * 4.5)}, ${String(LABEL_SCALE)});
        gl_Position = projectionMatrix * viewCenter;
        vLabelUv = atlasRect.xy + uv * atlasRect.zw;
      }
    `,
    fragmentShader: `
      uniform sampler2D labelAtlas;
      varying vec2 vLabelUv;
      void main() {
        vec4 sampled = texture2D(labelAtlas, vLabelUv);
        if (sampled.a < 0.02) discard;
        gl_FragColor = sampled;
      }
    `,
    transparent: true,
    depthTest: false,
    depthWrite: false,
  });

  const mesh = new THREE.Mesh(geometry, material);
  mesh.name = 'regional-toponym-labels';
  mesh.frustumCulled = false;
  mesh.renderOrder = 100;
  mesh.userData.atlasTexture = texture;
  mesh.userData.toponyms = Object.freeze(metadata);
  mesh.userData.labelCount = selected.length;
  mesh.userData.atlasWidth = canvas.width;
  mesh.userData.atlasHeight = canvas.height;
  mesh.userData.labelCenter = center.clone();
  mesh.userData.labelRadius = radius;
  mesh.onBeforeRender = (_renderer, _scene, camera) => {
    const distance = camera.position.distanceTo(center);
    geometry.instanceCount = visibleToponymLabelCount(distance, radius, selected.length);
  };
  return mesh;
}

function visibleToponymLabelCount(distance: number, radius: number, total: number): number {
  const referenceDistance = Math.max(500, radius * 0.75);
  if (distance > referenceDistance * 6) return Math.min(total, 24);
  if (distance > referenceDistance * 3) return Math.min(total, 64);
  if (distance > referenceDistance * 1.5) return Math.min(total, 128);
  return total;
}

function disposeObject(object: THREE.Object3D): void {
  object.traverse((item) => {
    const mesh = item as THREE.Mesh;
    mesh.geometry?.dispose();
    const atlasTexture = item.userData.atlasTexture as THREE.Texture | undefined;
    if (atlasTexture?.isTexture === true) atlasTexture.dispose();
    const material = (item as THREE.Mesh).material as THREE.Material | THREE.Material[] | undefined;
    if (material === undefined) return;
    const materials = Array.isArray(material) ? material : [material];
    for (const current of materials) {
      const texture = (current as THREE.SpriteMaterial).map;
      if (texture !== null && texture !== undefined && texture !== atlasTexture) texture.dispose();
      current.dispose();
    }
  });
}

function emptyMetrics(): SettlementStructureRenderingMetrics { return Object.freeze({ settlements: 0, corridors: 0, districts: 0, parcels: 0, buildings: 0, pois: 0, labels: 0, roadSigns: 0 }); }
function emptyRelations(): SettlementStructureStableRelations { return Object.freeze({ settlements: Object.freeze([]), districts: Object.freeze([]), parcels: Object.freeze([]), buildings: Object.freeze([]), pois: Object.freeze([]) }); }
function clamp(value: number, minimum: number, maximum: number): number { return Math.max(minimum, Math.min(maximum, value)); }
