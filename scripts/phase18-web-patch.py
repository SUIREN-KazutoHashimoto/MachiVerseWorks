from pathlib import Path
import json

ROOT = Path(__file__).resolve().parents[1]


def read(path: str) -> str:
    return (ROOT / path).read_text(encoding="utf-8")


def write(path: str, content: str) -> None:
    target = ROOT / path
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding="utf-8")


def replace_once(path: str, old: str, new: str) -> None:
    content = read(path)
    count = content.count(old)
    if count != 1:
        raise RuntimeError(f"Expected one match in {path}, found {count}: {old[:100]!r}")
    write(path, content.replace(old, new, 1))


write("src/web/src/railway-operations.ts", r'''import * as THREE from 'three';

import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

const SNAPSHOT_HEADER_LENGTH = 20;
const TRAIN_LENGTH = 129;
const SERVICE_LENGTH = 77;
const TIMETABLE_HEADER_LENGTH = 12;
const TIMETABLE_STOP_LENGTH = 40;

export enum RailwayOperationsMessageType {
  RailwayOperationsSnapshot = 710,
}

export enum RailwayServiceState { Planned = 0, Active = 1, Completed = 2 }
export enum TrainMovementState { InDepot = 0, WaitingForBlock = 1, Running = 2, ApproachingStation = 3, Dwelling = 4, Completed = 5 }

export interface TrainState {
  readonly id: bigint;
  readonly formationId: bigint;
  readonly serviceId: bigint;
  readonly routeId: bigint;
  readonly x: number;
  readonly y: number;
  readonly z: number;
  readonly forwardX: number;
  readonly forwardY: number;
  readonly forwardZ: number;
  readonly speedMetersPerSecond: number;
  readonly state: TrainMovementState;
  readonly currentBlockId: bigint | null;
  readonly currentPlatformId: bigint | null;
  readonly assignedPlatformId: bigint | null;
  readonly currentDepotId: bigint | null;
  readonly dwellDepartureTick: bigint;
}

export interface RailwayServiceStateMessage {
  readonly id: bigint;
  readonly formationId: bigint;
  readonly routeId: bigint;
  readonly timetableId: bigint;
  readonly originDepotId: bigint;
  readonly destinationDepotId: bigint;
  readonly plannedStartTick: bigint;
  readonly state: RailwayServiceState;
  readonly delayTicks: bigint;
  readonly nextStopIndex: number;
  readonly trainId: bigint | null;
}

export interface TimetableStop {
  readonly stationId: bigint;
  readonly plannedArrivalTick: bigint;
  readonly plannedDepartureTick: bigint;
  readonly minimumDwellTicks: bigint;
  readonly preferredPlatformId: bigint | null;
}

export interface RailwayTimetable { readonly id: bigint; readonly stops: readonly TimetableStop[]; }

export interface RailwayOperationsSnapshotMessage {
  readonly type: RailwayOperationsMessageType.RailwayOperationsSnapshot;
  readonly tickCount: bigint;
  readonly trains: readonly TrainState[];
  readonly services: readonly RailwayServiceStateMessage[];
  readonly timetables: readonly RailwayTimetable[];
}

export type RailwayOperationsProtocolMessage = RailwayOperationsSnapshotMessage;
export interface RailwayOperationsProtocolEnvelope { readonly version: ProtocolVersion; readonly message: RailwayOperationsProtocolMessage; }

export function isRailwayOperationsFrame(frame: ArrayBuffer): boolean {
  return frame.byteLength >= PROTOCOL_HEADER_SIZE
    && new DataView(frame).getUint16(8, true) === RailwayOperationsMessageType.RailwayOperationsSnapshot;
}

export function decodeRailwayOperationsFrame(frame: ArrayBuffer): RailwayOperationsProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Railway operations frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Railway operations frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Railway operations frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Railway operations frame payload length is invalid.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 7) throw new ProtocolDecodeFailure('Railway operations frames require Protocol 2.7 or newer.');
  if (view.getUint16(8, true) !== RailwayOperationsMessageType.RailwayOperationsSnapshot) throw new ProtocolDecodeFailure('Unknown railway operations message type.');
  return { version, message: decodeSnapshot(view, PROTOCOL_HEADER_SIZE, payloadLength) };
}

function decodeSnapshot(view: DataView, offset: number, payloadLength: number): RailwayOperationsSnapshotMessage {
  if (payloadLength < SNAPSHOT_HEADER_LENGTH) throw new ProtocolDecodeFailure('Railway operations payload is too short.');
  const end = offset + payloadLength;
  let cursor = offset;
  const requireBytes = (count: number): void => { if (!Number.isSafeInteger(count) || count < 0 || cursor + count > end) throw new ProtocolDecodeFailure('Railway operations payload is truncated.'); };
  const readByte = (): number => { requireBytes(1); return view.getUint8(cursor++); };
  const readInt32 = (): number => { requireBytes(4); const value = view.getInt32(cursor, true); cursor += 4; return value; };
  const readUint32 = (): number => { requireBytes(4); const value = view.getUint32(cursor, true); cursor += 4; return value; };
  const readUint64 = (): bigint => { requireBytes(8); const value = view.getBigUint64(cursor, true); cursor += 8; return value; };
  const readDouble = (): number => { requireBytes(8); const value = view.getFloat64(cursor, true); cursor += 8; return value; };
  const nullableId = (value: bigint): bigint | null => value === 0n ? null : value;

  const tickCount = readUint64();
  const trainCount = readUint32();
  const serviceCount = readUint32();
  const timetableCount = readUint32();
  const fixedBytes = trainCount * TRAIN_LENGTH + serviceCount * SERVICE_LENGTH + timetableCount * TIMETABLE_HEADER_LENGTH;
  if (!Number.isSafeInteger(fixedBytes) || fixedBytes > end - cursor) throw new ProtocolDecodeFailure('Railway operations counts exceed payload length.');

  const trains: TrainState[] = [];
  for (let index = 0; index < trainCount; index += 1) {
    const item: TrainState = {
      id: readUint64(), formationId: readUint64(), serviceId: readUint64(), routeId: readUint64(),
      x: readDouble(), y: readDouble(), z: readDouble(), forwardX: readDouble(), forwardY: readDouble(), forwardZ: readDouble(),
      speedMetersPerSecond: readDouble(), state: readByte() as TrainMovementState,
      currentBlockId: nullableId(readUint64()), currentPlatformId: nullableId(readUint64()), assignedPlatformId: nullableId(readUint64()), currentDepotId: nullableId(readUint64()), dwellDepartureTick: readUint64(),
    };
    if (item.id === 0n || item.formationId === 0n || item.serviceId === 0n || item.routeId === 0n || item.state < TrainMovementState.InDepot || item.state > TrainMovementState.Completed
      || !finite3(item.x, item.y, item.z) || !finite3(item.forwardX, item.forwardY, item.forwardZ) || !Number.isFinite(item.speedMetersPerSecond) || item.speedMetersPerSecond < 0) throw new ProtocolDecodeFailure('Train payload is invalid.');
    trains.push(item);
  }

  const services: RailwayServiceStateMessage[] = [];
  for (let index = 0; index < serviceCount; index += 1) {
    const item: RailwayServiceStateMessage = {
      id: readUint64(), formationId: readUint64(), routeId: readUint64(), timetableId: readUint64(), originDepotId: readUint64(), destinationDepotId: readUint64(), plannedStartTick: readUint64(),
      state: readByte() as RailwayServiceState, delayTicks: readUint64(), nextStopIndex: readInt32(), trainId: nullableId(readUint64()),
    };
    if (item.id === 0n || item.formationId === 0n || item.routeId === 0n || item.timetableId === 0n || item.originDepotId === 0n || item.destinationDepotId === 0n
      || item.state < RailwayServiceState.Planned || item.state > RailwayServiceState.Completed || item.nextStopIndex < 0) throw new ProtocolDecodeFailure('Railway service payload is invalid.');
    services.push(item);
  }

  const timetables: RailwayTimetable[] = [];
  for (let index = 0; index < timetableCount; index += 1) {
    const id = readUint64(); const stopCount = readUint32();
    if (id === 0n || stopCount === 0 || stopCount > Math.floor((end - cursor) / TIMETABLE_STOP_LENGTH)) throw new ProtocolDecodeFailure('Railway timetable payload is invalid.');
    const stops: TimetableStop[] = [];
    let previousDeparture = 0n;
    for (let stopIndex = 0; stopIndex < stopCount; stopIndex += 1) {
      const stationId = readUint64(); const plannedArrivalTick = readUint64(); const plannedDepartureTick = readUint64(); const minimumDwellTicks = readUint64(); const preferredPlatformId = nullableId(readUint64());
      if (stationId === 0n || plannedDepartureTick < plannedArrivalTick || (stopIndex > 0 && plannedArrivalTick < previousDeparture)) throw new ProtocolDecodeFailure('Railway timetable stop payload is invalid.');
      previousDeparture = plannedDepartureTick;
      stops.push({ stationId, plannedArrivalTick, plannedDepartureTick, minimumDwellTicks, preferredPlatformId });
    }
    timetables.push({ id, stops });
  }
  if (cursor !== end) throw new ProtocolDecodeFailure('Railway operations payload contains trailing bytes.');
  return { type: RailwayOperationsMessageType.RailwayOperationsSnapshot, tickCount, trains, services, timetables };
}

export class RailwayOperationsLayer {
  private readonly group = new THREE.Group();
  private readonly geometry = new THREE.BoxGeometry(18, 3, 3);
  private readonly material = new THREE.MeshBasicMaterial();
  private readonly meshes = new Map<bigint, THREE.Mesh>();
  private readonly direction = new THREE.Vector3();
  private readonly forwardAxis = new THREE.Vector3(1, 0, 0);

  public constructor(private readonly scene: THREE.Scene) {
    this.group.name = 'railway-trains';
    this.group.frustumCulled = false;
    this.scene.add(this.group);
  }

  public apply(snapshot: RailwayOperationsSnapshotMessage): void {
    const activeIds = new Set<bigint>();
    for (const train of snapshot.trains) {
      activeIds.add(train.id);
      let mesh = this.meshes.get(train.id);
      if (mesh === undefined) {
        mesh = new THREE.Mesh(this.geometry, this.material);
        mesh.name = `train-${train.id.toString()}`;
        mesh.frustumCulled = false;
        this.meshes.set(train.id, mesh);
        this.group.add(mesh);
      }
      mesh.position.set(train.x, train.z, train.y);
      this.direction.set(train.forwardX, train.forwardZ, train.forwardY);
      if (this.direction.lengthSq() > 1e-12) mesh.quaternion.setFromUnitVectors(this.forwardAxis, this.direction.normalize());
      mesh.userData.state = train.state;
      mesh.userData.serviceId = train.serviceId.toString();
      mesh.userData.platformId = (train.currentPlatformId ?? train.assignedPlatformId)?.toString() ?? '';
    }
    for (const [id, mesh] of this.meshes) {
      if (activeIds.has(id)) continue;
      this.group.remove(mesh);
      this.meshes.delete(id);
    }
    this.group.userData.tickCount = snapshot.tickCount.toString();
    this.group.userData.delayedServices = snapshot.services.filter((service) => service.delayTicks > 0n).length;
    this.group.userData.completedServices = snapshot.services.filter((service) => service.state === RailwayServiceState.Completed).length;
  }

  public clear(): void {
    for (const mesh of this.meshes.values()) this.group.remove(mesh);
    this.meshes.clear();
    this.group.userData.tickCount = '0';
    this.group.userData.delayedServices = 0;
    this.group.userData.completedServices = 0;
  }

  public dispose(): void {
    this.clear();
    this.scene.remove(this.group);
    this.geometry.dispose();
    this.material.dispose();
  }
}

function finite3(x: number, y: number, z: number): boolean { return Number.isFinite(x) && Number.isFinite(y) && Number.isFinite(z); }
''')

replace_once(
    "src/web/src/railway-infrastructure.ts",
    "export const WEB_RAILWAY_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 6 });",
    "export const WEB_RAILWAY_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 7 });",
)

replace_once(
    "src/web/src/connection.ts",
    "import {\n  decodeTrafficFrame,",
    "import {\n  decodeRailwayOperationsFrame,\n  isRailwayOperationsFrame,\n  type RailwayOperationsProtocolMessage,\n} from './railway-operations.ts';\nimport {\n  decodeTrafficFrame,",
)
replace_once(
    "src/web/src/connection.ts",
    "ProtocolMessage | TrafficProtocolMessage | PopulationProtocolMessage | RailwayProtocolMessage) => void;",
    "ProtocolMessage | TrafficProtocolMessage | PopulationProtocolMessage | RailwayProtocolMessage | RailwayOperationsProtocolMessage) => void;",
)
replace_once(
    "src/web/src/connection.ts",
    "      const populationFrame = !railwayFrame && isPopulationFrame(buffer);\n      const trafficFrame = !railwayFrame && !populationFrame && isTrafficFrame(buffer);\n      const envelope = railwayFrame\n        ? decodeRailwayFrame(buffer)\n        : populationFrame",
    "      const railwayOperationsFrame = !railwayFrame && isRailwayOperationsFrame(buffer);\n      const populationFrame = !railwayFrame && !railwayOperationsFrame && isPopulationFrame(buffer);\n      const trafficFrame = !railwayFrame && !railwayOperationsFrame && !populationFrame && isTrafficFrame(buffer);\n      const envelope = railwayFrame\n        ? decodeRailwayFrame(buffer)\n        : railwayOperationsFrame\n          ? decodeRailwayOperationsFrame(buffer)\n          : populationFrame",
)
replace_once(
    "src/web/src/connection.ts",
    "if (!railwayFrame && !populationFrame && !trafficFrame && envelope.message.type === MessageType.Error)",
    "if (!railwayFrame && !railwayOperationsFrame && !populationFrame && !trafficFrame && envelope.message.type === MessageType.Error)",
)
replace_once(
    "src/web/src/connection.ts",
    "if (!railwayFrame && !populationFrame && !trafficFrame && envelope.message.type === MessageType.Error)",
    "if (!railwayFrame && !railwayOperationsFrame && !populationFrame && !trafficFrame && envelope.message.type === MessageType.Error)",
)

replace_once(
    "src/web/src/application.ts",
    "import { RailwayInfrastructureLayer, RailwayMessageType, type RailwayProtocolMessage } from './railway-infrastructure.ts';",
    "import { RailwayInfrastructureLayer, RailwayMessageType, type RailwayProtocolMessage } from './railway-infrastructure.ts';\nimport { RailwayOperationsLayer, RailwayOperationsMessageType, type RailwayOperationsProtocolMessage, type RailwayOperationsSnapshotMessage } from './railway-operations.ts';",
)
replace_once(
    "src/web/src/application.ts",
    "  private readonly railway: RailwayInfrastructureLayer;",
    "  private readonly railway: RailwayInfrastructureLayer;\n  private readonly railwayOperations: RailwayOperationsLayer;",
)
replace_once(
    "src/web/src/application.ts",
    "    this.railway = new RailwayInfrastructureLayer(this.view.scene);",
    "    this.railway = new RailwayInfrastructureLayer(this.view.scene);\n    this.railwayOperations = new RailwayOperationsLayer(this.view.scene);",
)
replace_once(
    "src/web/src/application.ts",
    "          this.railway.clear();",
    "          this.railway.clear();\n          this.railwayOperations.clear();",
)
replace_once(
    "src/web/src/application.ts",
    "          this.ui.clearPopulation();",
    "          this.ui.clearPopulation();\n          this.ui.clearRailwayOperations();",
)
replace_once(
    "src/web/src/application.ts",
    "this.audio.dispose(); this.railway.dispose(); this.view.dispose();",
    "this.audio.dispose(); this.railway.dispose(); this.railwayOperations.dispose(); this.view.dispose();",
)
replace_once(
    "src/web/src/application.ts",
    "private handleProtocolMessage(message: ProtocolMessage | TrafficProtocolMessage | PopulationProtocolMessage | RailwayProtocolMessage): void",
    "private handleProtocolMessage(message: ProtocolMessage | TrafficProtocolMessage | PopulationProtocolMessage | RailwayProtocolMessage | RailwayOperationsProtocolMessage): void",
)
replace_once(
    "src/web/src/application.ts",
    "      case RailwayMessageType.RailwayInfrastructureSnapshot: this.railway.apply(message); return;",
    "      case RailwayMessageType.RailwayInfrastructureSnapshot: this.railway.apply(message); return;\n      case RailwayOperationsMessageType.RailwayOperationsSnapshot: this.applyRailwayOperations(message); return;",
)
replace_once(
    "src/web/src/application.ts",
    "  private applyPersonDebug(message: PersonDebugMessage): void { this.ui.setPersonDebug(message); }",
    "  private applyPersonDebug(message: PersonDebugMessage): void { this.ui.setPersonDebug(message); }\n  private applyRailwayOperations(message: RailwayOperationsSnapshotMessage): void { this.railwayOperations.apply(message); this.ui.setRailwayOperations(message); }",
)

replace_once(
    "src/web/src/ui.ts",
    "import { protocolVersionToString, type ProtocolVersion } from './protocol.ts';",
    "import { protocolVersionToString, type ProtocolVersion } from './protocol.ts';\nimport { RailwayServiceState, type RailwayOperationsSnapshotMessage } from './railway-operations.ts';",
)
replace_once(
    "src/web/src/ui.ts",
    "  private readonly populationValue = document.createElement('span');",
    "  private readonly populationValue = document.createElement('span');\n  private readonly trainsValue = document.createElement('span');\n  private readonly railwayDebugValue = document.createElement('div');",
)
replace_once(
    "src/web/src/ui.ts",
    "      this.createStatusRow('status.population', this.populationValue),",
    "      this.createStatusRow('status.population', this.populationValue),\n      this.createStatusRow('status.trains', this.trainsValue),",
)
replace_once(
    "src/web/src/ui.ts",
    "    panel.append(inspector);",
    "    panel.append(inspector);\n\n    const railwayDebug = document.createElement('div');\n    railwayDebug.className = 'railway-debug';\n    const railwayDebugTitle = document.createElement('strong');\n    railwayDebugTitle.textContent = localizer.t('railwayDebug.title');\n    this.railwayDebugValue.className = 'railway-debug-value';\n    railwayDebug.append(railwayDebugTitle, this.railwayDebugValue);\n    panel.append(railwayDebug);",
)
replace_once(
    "src/web/src/ui.ts",
    "    this.clearPopulation();",
    "    this.clearPopulation();\n    this.clearRailwayOperations();",
)
replace_once(
    "src/web/src/ui.ts",
    "  public setPersonDebug(message: PersonDebugMessage): void {",
    "  public setRailwayOperations(message: RailwayOperationsSnapshotMessage): void {\n    this.trainsValue.textContent = String(message.trains.length);\n    const delayed = message.services.filter((service) => service.delayTicks > 0n).length;\n    const completed = message.services.filter((service) => service.state === RailwayServiceState.Completed).length;\n    const timetableById = new Map(message.timetables.map((timetable) => [timetable.id, timetable] as const));\n    const arrivals: string[] = [];\n    for (const service of message.services) {\n      if (service.state === RailwayServiceState.Completed) continue;\n      const timetable = timetableById.get(service.timetableId);\n      const stop = timetable?.stops[service.nextStopIndex];\n      if (stop === undefined) continue;\n      arrivals.push(`S${stop.stationId.toString()}@${(stop.plannedArrivalTick + service.delayTicks).toString()}`);\n    }\n    this.railwayDebugValue.textContent = this.localizer.t('railwayDebug.summary', { delayed, completed, arrivals: arrivals.length === 0 ? '—' : arrivals.join(', ') });\n  }\n\n  public clearRailwayOperations(): void { this.trainsValue.textContent = '0'; this.railwayDebugValue.textContent = this.localizer.t('railwayDebug.none'); }\n\n  public setPersonDebug(message: PersonDebugMessage): void {",
)

locale_path = ROOT / "src/web/locales/ja-JP.json"
locale = json.loads(locale_path.read_text(encoding="utf-8"))
locale["status.trains"] = "列車"
locale["railwayDebug.title"] = "Railway Debug"
locale["railwayDebug.none"] = "運行情報なし"
locale["railwayDebug.summary"] = "遅延Service {delayed} / 完了 {completed} / 次発着 {arrivals}"
locale_path.write_text(json.dumps(locale, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

write("src/web/tests/railway-operations.test.mjs", r'''import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import { RailwayOperationsLayer, RailwayOperationsMessageType, RailwayServiceState, TrainMovementState, decodeRailwayOperationsFrame, isRailwayOperationsFrame } from '../src/railway-operations.ts';

function createFixtureFrame() {
  const payloadLength = 20 + 129 + 77 + 12 + 40;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 7, true); view.setUint16(8, RailwayOperationsMessageType.RailwayOperationsSnapshot, true); view.setUint16(10, 0, true); view.setUint32(12, payloadLength, true);
  let cursor = PROTOCOL_HEADER_SIZE;
  const u8 = (v) => { view.setUint8(cursor, v); cursor += 1; }; const i32 = (v) => { view.setInt32(cursor, v, true); cursor += 4; }; const u32 = (v) => { view.setUint32(cursor, v, true); cursor += 4; }; const u64 = (v) => { view.setBigUint64(cursor, BigInt(v), true); cursor += 8; }; const f64 = (v) => { view.setFloat64(cursor, v, true); cursor += 8; };
  u64(500); u32(1); u32(1); u32(1);
  u64(1); u64(2); u64(3); u64(4); f64(10); f64(20); f64(8); f64(1); f64(0); f64(0); f64(12); u8(TrainMovementState.ApproachingStation); u64(5); u64(0); u64(9); u64(0); u64(0);
  u64(3); u64(2); u64(4); u64(7); u64(10); u64(11); u64(1); u8(RailwayServiceState.Active); u64(25); i32(0); u64(1);
  u64(7); u32(1); u64(12); u64(450); u64(470); u64(10); u64(9);
  assert.equal(cursor, frame.byteLength); return frame;
}

test('Protocol 2.7 railway operations decodes train service delay platform and timetable', () => {
  const frame = createFixtureFrame();
  assert.equal(isRailwayOperationsFrame(frame), true);
  const envelope = decodeRailwayOperationsFrame(frame);
  assert.deepEqual(envelope.version, { major: 2, minor: 7 });
  assert.equal(envelope.message.tickCount, 500n);
  assert.equal(envelope.message.trains[0].z, 8);
  assert.equal(envelope.message.trains[0].assignedPlatformId, 9n);
  assert.equal(envelope.message.services[0].delayTicks, 25n);
  assert.equal(envelope.message.timetables[0].stops[0].stationId, 12n);
});

test('railway operations layer renders and updates train 3D position', () => {
  const scene = new THREE.Scene(); const layer = new RailwayOperationsLayer(scene); const snapshot = decodeRailwayOperationsFrame(createFixtureFrame()).message;
  layer.apply(snapshot);
  const group = scene.getObjectByName('railway-trains'); const mesh = scene.getObjectByName('train-1');
  assert.equal(group.children.length, 1); assert.deepEqual(mesh.position.toArray(), [10, 8, 20]); assert.equal(group.userData.delayedServices, 1);
  layer.apply({ ...snapshot, trains: [{ ...snapshot.trains[0], x: 15, state: TrainMovementState.Dwelling }] });
  assert.equal(mesh.position.x, 15); assert.equal(mesh.userData.state, TrainMovementState.Dwelling);
  layer.clear(); assert.equal(group.children.length, 0); layer.dispose(); assert.equal(scene.getObjectByName('railway-trains'), undefined);
});

test('railway operations decoder rejects Protocol 2.6', () => {
  const frame = createFixtureFrame(); new DataView(frame).setUint16(6, 6, true); assert.throws(() => decodeRailwayOperationsFrame(frame), /2\.7/);
});
''')

print("Phase18 web integration patch applied.")
