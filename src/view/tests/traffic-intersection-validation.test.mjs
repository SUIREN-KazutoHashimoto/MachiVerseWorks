import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import { TrafficMessageType, decodeTrafficFrame } from '../src/traffic-protocol.ts';

test('traffic decoder rejects Intersection movement/connection identity mismatch', () => {
  const payloadLength = 31 + 63;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 4, true);
  view.setUint16(8, TrafficMessageType.IntersectionControlSnapshot, true); view.setUint16(10, 0, true); view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  view.setBigUint64(o, 1n, true); view.setBigUint64(o + 8, 5n, true); view.setUint8(o + 16, 0); view.setUint16(o + 17, 0, true); view.setBigUint64(o + 19, 0n, true); view.setUint32(o + 27, 1, true);
  const m = o + 31;
  view.setBigUint64(m, 10n, true); view.setBigUint64(m + 8, 11n, true); view.setBigUint64(m + 16, 21n, true); view.setBigUint64(m + 24, 22n, true);
  view.setUint8(m + 32, 1); view.setFloat64(m + 33, 0, true); view.setFloat64(m + 41, 0, true); view.setFloat64(m + 49, 0, true); view.setUint8(m + 57, 2); view.setUint32(m + 58, 0, true); view.setUint8(m + 62, 0);
  assert.throws(() => decodeTrafficFrame(frame), /identity/);
});
