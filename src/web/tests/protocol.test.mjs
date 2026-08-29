import test from 'node:test';
import assert from 'node:assert/strict';
import * as protocol from '../src/protocol.ts';

const {
  CURRENT_PROTOCOL_VERSION,
  MessageType,
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  decodeFrame,
  encodeHello,
  encodeSubscribeVolume,
} = protocol;

test('Hello frame matches Protocol 2.0 header contract', () => {
  const frame = encodeHello();
  const view = new DataView(frame);
  assert.equal(frame.byteLength, PROTOCOL_HEADER_SIZE);
  assert.equal(CURRENT_PROTOCOL_VERSION.major, 2);
  assert.equal(CURRENT_PROTOCOL_VERSION.minor, 0);
  assert.equal(view.getUint32(0, true), PROTOCOL_MAGIC);
  assert.equal(view.getUint16(8, true), MessageType.Hello);
});

test('SubscribeVolume round-trips a native 3D volume through the client codec', () => {
  const volume = { minX: -100, minY: -50, minZ: -20, maxX: 200, maxY: 150, maxZ: 80 };
  const envelope = decodeFrame(encodeSubscribeVolume(volume));
  assert.equal(envelope.message.type, MessageType.SubscribeVolume);
  assert.deepEqual(envelope.message, { type: MessageType.SubscribeVolume, ...volume });
});

test('legacy 2D subscription API is not exported', () => {
  assert.equal('encodeSubscribeArea' in protocol, false);
  assert.equal('SubscribeArea' in MessageType, false);
});

test('Agent update decoder reads the Protocol 2.0 3D payload layout', () => {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + 64);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, CURRENT_PROTOCOL_VERSION.major, true);
  view.setUint16(6, CURRENT_PROTOCOL_VERSION.minor, true);
  view.setUint16(8, MessageType.AgentUpdate, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, 64, true);
  view.setBigUint64(16, 42n, true);
  view.setFloat64(24, 12.5, true);
  view.setFloat64(32, -8.25, true);
  view.setFloat64(40, 75.5, true);
  view.setFloat64(48, 1.5, true);
  view.setFloat64(56, -2, true);
  view.setFloat64(64, 3.25, true);
  view.setBigUint64(72, 99n, true);
  const envelope = decodeFrame(frame);
  assert.deepEqual(envelope.message, {
    type: MessageType.AgentUpdate,
    agentId: 42n,
    x: 12.5,
    y: -8.25,
    z: 75.5,
    velocityX: 1.5,
    velocityY: -2,
    velocityZ: 3.25,
    tickCount: 99n,
  });
});
