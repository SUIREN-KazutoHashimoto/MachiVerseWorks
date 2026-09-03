import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  TrafficMessageType,
  VehicleMovementState,
  decodeTrafficFrame,
} from '../src/traffic-protocol.ts';

function createVehicleFrame(forwardX = 1, forwardY = 0, forwardZ = 0) {
  const payloadLength = 105;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 3, true);
  view.setUint16(8, TrafficMessageType.VehicleUpdate, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  view.setBigUint64(o, 10n, true);
  view.setBigUint64(o + 8, 20n, true);
  view.setFloat64(o + 16, 100, true);
  view.setFloat64(o + 24, 200, true);
  view.setFloat64(o + 32, 5, true);
  view.setFloat64(o + 40, forwardX, true);
  view.setFloat64(o + 48, forwardY, true);
  view.setFloat64(o + 56, forwardZ, true);
  view.setFloat64(o + 64, 12, true);
  view.setFloat64(o + 72, 4.2, true);
  view.setFloat64(o + 80, 1.8, true);
  view.setFloat64(o + 88, 1.5, true);
  view.setUint8(o + 96, VehicleMovementState.Driving);
  view.setBigUint64(o + 97, 99n, true);
  return frame;
}

test('Traffic vehicle decoder accepts a normal finite non-zero forward vector', () => {
  const envelope = decodeTrafficFrame(createVehicleFrame(0.6, 0.8, 0));
  assert.equal(envelope.message.type, TrafficMessageType.VehicleUpdate);
  assert.equal(envelope.message.forwardX, 0.6);
  assert.equal(envelope.message.forwardY, 0.8);
});

test('Traffic vehicle decoder rejects finite components whose squared length overflows', () => {
  assert.throws(
    () => decodeTrafficFrame(createVehicleFrame(1e308, 1e308, 0)),
    /Vehicle state payload is invalid/,
  );
});
