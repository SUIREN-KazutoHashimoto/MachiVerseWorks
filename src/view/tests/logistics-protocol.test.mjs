import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import { LOGISTICS_SNAPSHOT_MESSAGE_TYPE, ShipmentState, decodeLogisticsFrame, isLogisticsFrame } from '../src/logistics-protocol.ts';

function createFrame() {
  const payloadLength = 68 + 32 + 65;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 11, true);
  view.setUint16(8, LOGISTICS_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  [1, 2, 1, 1, 1, 1].forEach((value, index) => view.setUint32(o + index * 4, value, true));
  view.setFloat64(o + 24, 18, true); view.setFloat64(o + 32, 10, true);
  view.setBigUint64(o + 40, 4n, true); view.setBigUint64(o + 48, 7n, true); view.setBigUint64(o + 56, 900n, true);
  view.setUint16(o + 64, 1, true); view.setUint16(o + 66, 1, true);
  let c = o + 68;
  view.setBigUint64(c, 10n, true); view.setBigUint64(c + 8, 1n, true); view.setFloat64(c + 16, 8, true); view.setFloat64(c + 24, 20, true);
  c += 32;
  view.setBigUint64(c, 5n, true); view.setBigUint64(c + 8, 4n, true); view.setBigUint64(c + 16, 10n, true); view.setBigUint64(c + 24, 11n, true); view.setBigUint64(c + 32, 1n, true); view.setFloat64(c + 40, 10, true); view.setUint8(c + 48, ShipmentState.InTransit); view.setBigUint64(c + 49, 3n, true); view.setBigUint64(c + 57, 12n, true);
  return frame;
}

test('Logistics snapshot decodes inventory and shipment debug entries', () => {
  const frame = createFrame();
  assert.equal(isLogisticsFrame(frame), true);
  const { version, message } = decodeLogisticsFrame(frame);
  assert.deepEqual(version, { major: 2, minor: 11 });
  assert.equal(message.statistics.delayedShipmentCount, 1);
  assert.equal(message.inventories[0].quantity, 8);
  assert.equal(message.shipments[0].state, ShipmentState.InTransit);
  assert.equal(message.shipments[0].vehicleId, 3n);
  assert.equal(message.shipments[0].delayTicks, 12n);
});

test('Logistics snapshot rejects pre-2.11 frames and invalid counts', () => {
  const old = createFrame(); new DataView(old).setUint16(6, 10, true);
  assert.throws(() => decodeLogisticsFrame(old), /2.11/);
  const invalid = createFrame(); new DataView(invalid).setUint16(PROTOCOL_HEADER_SIZE + 64, 2, true);
  assert.throws(() => decodeLogisticsFrame(invalid), /counts/);
});
