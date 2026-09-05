import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  GeneratorOperatingState,
  POWER_SNAPSHOT_MESSAGE_TYPE,
  PowerNodeKind,
  PowerSupplyState,
  decodePowerFrame,
} from '../src/power-protocol.ts';

function createFrame() {
  const payloadLength = 76 + 33 + 33 + 33 + 65;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 12, true);
  view.setUint16(8, POWER_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  const o = PROTOCOL_HEADER_SIZE;
  [1, 1, 1, 1, 1].forEach((value, index) => view.setUint32(o + index * 4, value, true));
  view.setFloat64(o + 20, 20, true);
  view.setFloat64(o + 28, 0, true);
  view.setFloat64(o + 36, 8, true);
  view.setFloat64(o + 44, 0, true);
  view.setFloat64(o + 52, 8, true);
  view.setBigUint64(o + 60, 42n, true);
  view.setUint16(o + 68, 1, true);
  view.setUint16(o + 70, 1, true);
  view.setUint16(o + 72, 1, true);
  view.setUint16(o + 74, 1, true);

  let c = o + 76;
  view.setBigUint64(c, 1n, true);
  view.setUint8(c + 8, PowerNodeKind.Load);
  view.setFloat64(c + 9, 10, true);
  view.setFloat64(c + 17, 5, true);
  view.setFloat64(c + 25, 0, true);
  c += 33;

  view.setBigUint64(c, 1n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setBigUint64(c + 16, 2n, true);
  view.setFloat64(c + 24, 10, true);
  view.setUint8(c + 32, 0);
  c += 33;

  view.setBigUint64(c, 1n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setFloat64(c + 16, 20, true);
  view.setFloat64(c + 24, 0, true);
  view.setUint8(c + 32, GeneratorOperatingState.Offline);
  c += 33;

  view.setBigUint64(c, 1n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setBigUint64(c + 16, 5n, true);
  view.setBigUint64(c + 24, 0n, true);
  view.setFloat64(c + 32, 10, true);
  view.setFloat64(c + 40, 8, true);
  view.setFloat64(c + 48, 0, true);
  view.setFloat64(c + 56, 8, true);
  view.setUint8(c + 64, PowerSupplyState.Outage);
  return frame;
}

test('Power decoder rejects output/served values beyond capacity or demand', () => {
  const o = PROTOCOL_HEADER_SIZE;

  const statisticsCapacity = createFrame();
  new DataView(statisticsCapacity).setFloat64(o + 28, 20.000001, true);
  assert.throws(() => decodePowerFrame(statisticsCapacity), /inconsistent/);

  const statisticsDemand = createFrame();
  new DataView(statisticsDemand).setFloat64(o + 44, 8.000001, true);
  assert.throws(() => decodePowerFrame(statisticsDemand), /inconsistent/);

  const generator = createFrame();
  const generatorOffset = o + 76 + 33 + 33;
  new DataView(generator).setFloat64(generatorOffset + 24, 20.000001, true);
  assert.throws(() => decodePowerFrame(generator), /Generator/);

  const load = createFrame();
  const loadOffset = o + 76 + 33 + 33 + 33;
  new DataView(load).setFloat64(loadOffset + 48, 8.000001, true);
  assert.throws(() => decodePowerFrame(load), /served demand/);
});

test('Power decoder keeps the 1e-9 parity tolerance', () => {
  const o = PROTOCOL_HEADER_SIZE;
  const statistics = createFrame();
  new DataView(statistics).setFloat64(o + 28, 20 + 1e-9, true);
  assert.doesNotThrow(() => decodePowerFrame(statistics));
});
