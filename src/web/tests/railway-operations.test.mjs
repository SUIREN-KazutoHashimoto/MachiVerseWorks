import test from 'node:test';
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
