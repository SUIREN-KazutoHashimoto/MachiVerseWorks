import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import { ECONOMY_SNAPSHOT_MESSAGE_TYPE, IndustrySector, decodeEconomyFrame, isEconomyFrame } from '../src/economy-protocol.ts';

function createFrame() {
  const payloadLength = 96 + 57 + 32;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 10, true);
  view.setUint16(8, ECONOMY_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  [1, 1, 1, 1, 0].forEach((value, index) => view.setUint32(o + index * 4, value, true));
  view.setBigInt64(o + 20, 1400n, true); view.setBigInt64(o + 28, 1500n, true); view.setBigInt64(o + 36, 300n, true);
  view.setBigInt64(o + 44, 8800n, true); view.setBigInt64(o + 52, 300n, true); view.setBigInt64(o + 60, 1500n, true);
  view.setFloat64(o + 68, 30, true); view.setBigUint64(o + 76, 3n, true); view.setBigUint64(o + 84, 86400n, true);
  view.setUint16(o + 92, 1, true); view.setUint16(o + 94, 1, true);
  let c = o + 96;
  view.setBigUint64(c, 1n, true); view.setUint8(c + 8, IndustrySector.Retail); view.setBigInt64(c + 9, 8800n, true); view.setBigInt64(c + 17, 300n, true); view.setBigInt64(c + 25, 1500n, true); view.setFloat64(c + 33, 10, true); view.setFloat64(c + 41, 30, true); view.setUint32(c + 49, 1, true); view.setUint32(c + 53, 1, true);
  c += 57;
  view.setBigUint64(c, 1n, true); view.setBigInt64(c + 8, 1400n, true); view.setBigInt64(c + 16, 1500n, true); view.setBigInt64(c + 24, 300n, true);
  return frame;
}

test('Economy snapshot decodes statistics and bounded debug entries', () => {
  const frame = createFrame();
  assert.equal(isEconomyFrame(frame), true);
  const { version, message } = decodeEconomyFrame(frame);
  assert.deepEqual(version, { major: 2, minor: 10 });
  assert.equal(message.statistics.companyCount, 1);
  assert.equal(message.statistics.companyRevenue, 300n);
  assert.equal(message.statistics.producedUnits, 30);
  assert.equal(message.companies[0].sector, IndustrySector.Retail);
  assert.equal(message.companies[0].employeeCount, 1);
  assert.equal(message.households[0].income, 1500n);
});

test('Economy snapshot rejects pre-2.10 frames and invalid counts', () => {
  const old = createFrame(); new DataView(old).setUint16(6, 9, true);
  assert.throws(() => decodeEconomyFrame(old), /2.10/);
  const invalid = createFrame(); new DataView(invalid).setUint16(PROTOCOL_HEADER_SIZE + 92, 2, true);
  assert.throws(() => decodeEconomyFrame(invalid), /counts/);
});
