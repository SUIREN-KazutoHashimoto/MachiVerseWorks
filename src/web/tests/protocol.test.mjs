import test from 'node:test';
import assert from 'node:assert/strict';
import * as protocol from '../src/protocol.ts';

const { CURRENT_PROTOCOL_VERSION, MessageType, PedestrianMovementState, PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, decodeFrame, encodeHello, encodeSubscribeVolume } = protocol;

test('Hello frame matches current Protocol 2.20 header contract', () => {
  const frame = encodeHello(); const view = new DataView(frame);
  assert.equal(frame.byteLength, PROTOCOL_HEADER_SIZE); assert.equal(CURRENT_PROTOCOL_VERSION.major, 2); assert.equal(CURRENT_PROTOCOL_VERSION.minor, 20);
  assert.equal(view.getUint32(0, true), PROTOCOL_MAGIC); assert.equal(view.getUint16(8, true), MessageType.Hello);
});

test('SubscribeVolume round-trips a native 3D volume through the client codec', () => {
  const volume = { minX: -100, minY: -50, minZ: -20, maxX: 200, maxY: 150, maxZ: 80 };
  const envelope = decodeFrame(encodeSubscribeVolume(volume)); assert.equal(envelope.message.type, MessageType.SubscribeVolume); assert.deepEqual(envelope.message, { type: MessageType.SubscribeVolume, ...volume });
});

test('legacy 2D subscription API is not exported', () => { assert.equal('encodeSubscribeArea' in protocol, false); assert.equal('SubscribeArea' in MessageType, false); });

test('Agent update decoder keeps the Protocol 2.1 3D payload compatible', () => {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + 64); const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 1, true); view.setUint16(8, MessageType.AgentUpdate, true); view.setUint16(10, 0, true); view.setUint32(12, 64, true);
  view.setBigUint64(16, 42n, true); view.setFloat64(24, 12.5, true); view.setFloat64(32, -8.25, true); view.setFloat64(40, 75.5, true); view.setFloat64(48, 1.5, true); view.setFloat64(56, -2, true); view.setFloat64(64, 3.25, true); view.setBigUint64(72, 99n, true);
  const envelope = decodeFrame(frame);
  assert.deepEqual(envelope.message, { type: MessageType.AgentUpdate, agentId: 42n, x: 12.5, y: -8.25, z: 75.5, velocityX: 1.5, velocityY: -2, velocityZ: 3.25, tickCount: 99n });
});

test('Pedestrian update decoder reads Protocol 2.2 state and 3D movement', () => {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + 81); const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 2, true); view.setUint16(8, MessageType.PedestrianUpdate, true); view.setUint16(10, 0, true); view.setUint32(12, 81, true);
  const offset = PROTOCOL_HEADER_SIZE;
  view.setBigUint64(offset, 7n, true); view.setBigUint64(offset + 8, 9n, true);
  view.setFloat64(offset + 16, 1.25, true); view.setFloat64(offset + 24, 2.5, true); view.setFloat64(offset + 32, 3.75, true);
  view.setFloat64(offset + 40, 0.5, true); view.setFloat64(offset + 48, 0.25, true); view.setFloat64(offset + 56, 0.125, true);
  view.setFloat64(offset + 64, 1.4, true); view.setUint8(offset + 72, PedestrianMovementState.WaitingForCrossing); view.setBigUint64(offset + 73, 123n, true);
  assert.deepEqual(decodeFrame(frame).message, {
    type: MessageType.PedestrianUpdate,
    pedestrianId: 7n,
    tripRequestId: 9n,
    x: 1.25,
    y: 2.5,
    z: 3.75,
    velocityX: 0.5,
    velocityY: 0.25,
    velocityZ: 0.125,
    walkingSpeedMetersPerSecond: 1.4,
    state: PedestrianMovementState.WaitingForCrossing,
    tickCount: 123n,
  });
});

test('Pedestrian messages are rejected below Protocol 2.2', () => {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + 16); const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 1, true); view.setUint16(8, MessageType.PedestrianRemove, true); view.setUint32(12, 16, true); view.setBigUint64(16, 1n, true);
  assert.throws(() => decodeFrame(frame), /2\.2/);
});

test('Road Network decoder preserves 3D grade separation and explicit references', () => {
  const payloadLength = 28 + (4 * 33) + (2 * 25) + (2 * 35) + 33 + 41;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength); const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 1, true); view.setUint16(8, MessageType.RoadNetworkSnapshot, true); view.setUint32(12, payloadLength, true);
  let offset = PROTOCOL_HEADER_SIZE; view.setBigUint64(offset, 7n, true); view.setUint32(offset + 8, 4, true); view.setUint32(offset + 12, 2, true); view.setUint32(offset + 16, 2, true); view.setUint32(offset + 20, 1, true); view.setUint32(offset + 24, 1, true); offset += 28;
  for (const node of [[1n, 0, -10, 0, 0], [2n, 1, 0, 0, 0], [3n, 0, 10, 0, 0], [4n, 0, 0, 10, 20]]) { view.setBigUint64(offset, node[0], true); view.setUint8(offset + 8, node[1]); view.setFloat64(offset + 9, node[2], true); view.setFloat64(offset + 17, node[3], true); view.setFloat64(offset + 25, node[4], true); offset += 33; }
  for (const segment of [[1n, 2, 1n, 2n], [2n, 0, 2n, 3n]]) { view.setBigUint64(offset, segment[0], true); view.setUint8(offset + 8, segment[1]); view.setBigUint64(offset + 9, segment[2], true); view.setBigUint64(offset + 17, segment[3], true); offset += 25; }
  for (const lane of [[1n, 1n], [2n, 2n]]) { view.setBigUint64(offset, lane[0], true); view.setBigUint64(offset + 8, lane[1], true); view.setUint8(offset + 16, 0); view.setUint16(offset + 17, 0, true); view.setFloat64(offset + 19, 3.5, true); view.setFloat64(offset + 27, 15, true); offset += 35; }
  view.setBigUint64(offset, 1n, true); view.setBigUint64(offset + 8, 1n, true); view.setBigUint64(offset + 16, 2n, true); view.setBigUint64(offset + 24, 2n, true); view.setUint8(offset + 32, 1); offset += 33;
  view.setBigUint64(offset, 1n, true); view.setBigUint64(offset + 8, 1n, true); view.setFloat64(offset + 16, 0.5, true); view.setBigUint64(offset + 24, 9n, true); view.setBigUint64(offset + 32, 0n, true); view.setUint8(offset + 40, 1);
  const message = decodeFrame(frame).message; assert.equal(message.type, MessageType.RoadNetworkSnapshot); assert.equal(message.nodes[3].z, 20); assert.equal(message.connections[0].viaNodeId, 2n); assert.equal(message.accessPoints[0].buildingId, 9n); assert.equal(message.accessPoints[0].poiId, null);
});
