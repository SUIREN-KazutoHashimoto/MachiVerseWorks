import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  SewerServiceState,
  UtilityFacilityKind,
  UtilityNetworkKind,
  UtilityNodeKind,
  UtilityOperatingState,
  WATER_SEWER_SNAPSHOT_MESSAGE_TYPE,
  WaterServiceState,
  decodeWaterSewerFrame,
  isWaterSewerFrame,
} from '../src/water-sewer-protocol.ts';

function createFrame() {
  const payloadLength = 112 + (2 * 34) + 34 + 42 + 106;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true); view.setUint16(6, 13, true);
  view.setUint16(8, WATER_SEWER_SNAPSHOT_MESSAGE_TYPE, true); view.setUint16(10, 0, true); view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  [2, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0].forEach((value, index) => view.setUint32(o + index * 4, value, true));
  view.setFloat64(o + 48, 100, true); view.setFloat64(o + 56, 10, true); view.setFloat64(o + 64, 10, true);
  view.setFloat64(o + 72, 9, true); view.setFloat64(o + 80, 9, true); view.setFloat64(o + 88, 0, true); view.setBigUint64(o + 96, 24n, true);
  view.setUint16(o + 104, 2, true); view.setUint16(o + 106, 1, true); view.setUint16(o + 108, 1, true); view.setUint16(o + 110, 1, true);
  let c = o + 112;
  view.setUint8(c, UtilityNetworkKind.Water); view.setBigUint64(c + 1, 1n, true); view.setUint8(c + 9, UtilityNodeKind.Source); view.setFloat64(c + 10, 0, true); view.setFloat64(c + 18, 0, true); view.setFloat64(c + 26, 0, true); c += 34;
  view.setUint8(c, UtilityNetworkKind.Water); view.setBigUint64(c + 1, 2n, true); view.setUint8(c + 9, UtilityNodeKind.Service); view.setFloat64(c + 10, 10, true); view.setFloat64(c + 18, 0, true); view.setFloat64(c + 26, 0, true); c += 34;
  view.setUint8(c, UtilityNetworkKind.Water); view.setBigUint64(c + 1, 1n, true); view.setBigUint64(c + 9, 1n, true); view.setBigUint64(c + 17, 2n, true); view.setFloat64(c + 25, 100, true); view.setUint8(c + 33, 1); c += 34;
  view.setUint8(c, UtilityFacilityKind.WaterSource); view.setBigUint64(c + 1, 1n, true); view.setBigUint64(c + 9, 1n, true); view.setBigUint64(c + 17, 0n, true); view.setFloat64(c + 25, 100, true); view.setFloat64(c + 33, 10, true); view.setUint8(c + 41, UtilityOperatingState.Online); c += 42;
  view.setBigUint64(c, 1n, true); view.setBigUint64(c + 8, 2n, true); view.setBigUint64(c + 16, 1n, true); view.setBigUint64(c + 24, 5n, true); view.setBigUint64(c + 32, 0n, true);
  view.setFloat64(c + 40, 10, true); view.setFloat64(c + 48, 0.9, true); view.setFloat64(c + 56, 10, true); view.setFloat64(c + 64, 10, true); view.setFloat64(c + 72, 0, true); view.setUint8(c + 80, WaterServiceState.Supplied);
  view.setFloat64(c + 81, 9, true); view.setFloat64(c + 89, 9, true); view.setFloat64(c + 97, 0, true); view.setUint8(c + 105, SewerServiceState.Available);
  return frame;
}

test('Water/Sewer snapshot decodes topology, facility and service state', () => {
  const frame = createFrame();
  assert.equal(isWaterSewerFrame(frame), true);
  const { version, message } = decodeWaterSewerFrame(frame);
  assert.deepEqual(version, { major: 2, minor: 13 });
  assert.equal(message.nodes.length, 2);
  assert.equal(message.pipes[0].isInService, true);
  assert.equal(message.facilities[0].operatingState, UtilityOperatingState.Online);
  assert.equal(message.servicePoints[0].waterState, WaterServiceState.Supplied);
});

test('Water/Sewer snapshot rejects pre-2.13 frames and invalid service states', () => {
  const old = createFrame(); new DataView(old).setUint16(6, 12, true);
  assert.throws(() => decodeWaterSewerFrame(old), /2.13/);
  const invalid = createFrame(); new DataView(invalid).setUint8(invalid.byteLength - 1, 255);
  assert.throws(() => decodeWaterSewerFrame(invalid), /service point/);
});
