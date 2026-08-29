import test from 'node:test';
import assert from 'node:assert/strict';

import {
  CURRENT_PROTOCOL_VERSION,
  MessageType,
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  decodeFrame,
  encodeHello,
  encodeSubscribeArea,
} from '../src/protocol.ts';

test('Hello frame matches Protocol 1.0 header contract', () => {
  const frame = encodeHello();
  const view = new DataView(frame);
  assert.equal(frame.byteLength, PROTOCOL_HEADER_SIZE);
  assert.equal(view.getUint32(0, true), PROTOCOL_MAGIC);
  assert.equal(view.getUint16(4, true), CURRENT_PROTOCOL_VERSION.major);
  assert.equal(view.getUint16(6, true), CURRENT_PROTOCOL_VERSION.minor);
  assert.equal(view.getUint16(8, true), MessageType.Hello);
  assert.equal(view.getUint32(12, true), 0);
});

test('SubscribeArea round-trips through the client codec', () => {
  const area = { minX: -100, minY: -50, maxX: 200, maxY: 150 };
  const envelope = decodeFrame(encodeSubscribeArea(area));
  assert.equal(envelope.message.type, MessageType.SubscribeArea);
  assert.deepEqual(envelope.message, { type: MessageType.SubscribeArea, ...area });
});

test('Agent update decoder reads the server payload layout', () => {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + 48);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 1, true);
  view.setUint16(6, 0, true);
  view.setUint16(8, MessageType.AgentUpdate, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, 48, true);
  view.setBigUint64(16, 42n, true);
  view.setFloat64(24, 12.5, true);
  view.setFloat64(32, -8.25, true);
  view.setFloat64(40, 1.5, true);
  view.setFloat64(48, -2, true);
  view.setBigUint64(56, 99n, true);

  const envelope = decodeFrame(frame);
  assert.deepEqual(envelope.message, {
    type: MessageType.AgentUpdate,
    agentId: 42n,
    x: 12.5,
    y: -8.25,
    velocityX: 1.5,
    velocityY: -2,
    tickCount: 99n,
  });
});
