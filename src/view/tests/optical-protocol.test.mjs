import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  OPTICAL_SNAPSHOT_MESSAGE_TYPE,
  OpticalDemandKind,
  OpticalEquipmentKind,
  OpticalNodeKind,
  OpticalQualityState,
  decodeOpticalFrame,
} from '../src/optical-protocol.ts';

function createFrame() {
  const payloadLength = 86 + 33 + 50 + 45 + 42 + 74;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 15, true);
  view.setUint16(8, OPTICAL_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);

  const o = PROTOCOL_HEADER_SIZE;
  [1, 1, 1, 1, 1, 1, 0, 0, 0].forEach((value, index) => view.setUint32(o + index * 4, value, true));
  view.setFloat64(o + 36, 20, true);
  view.setFloat64(o + 44, 1.5, true);
  view.setFloat64(o + 52, 1, true);
  view.setFloat64(o + 60, 0.5, true);
  view.setBigUint64(o + 68, 42n, true);
  [1, 1, 1, 1, 1].forEach((value, index) => view.setUint16(o + 76 + index * 2, value, true));

  let c = o + 86;
  view.setBigUint64(c, 1n, true);
  view.setUint8(c + 8, OpticalNodeKind.Access);
  view.setFloat64(c + 9, 10, true);
  view.setFloat64(c + 17, 20, true);
  view.setFloat64(c + 25, 3, true);
  c += 33;

  view.setBigUint64(c, 2n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setBigUint64(c + 16, 9n, true);
  view.setFloat64(c + 24, 10, true);
  view.setFloat64(c + 32, 5, true);
  view.setFloat64(c + 40, 0.5, true);
  view.setUint8(c + 48, 1);
  view.setUint8(c + 49, 0);
  c += 50;

  view.setBigUint64(c, 3n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setUint8(c + 16, OpticalEquipmentKind.Router);
  view.setBigUint64(c + 17, 4n, true);
  view.setBigUint64(c + 25, 0n, true);
  view.setFloat64(c + 33, 10, true);
  view.setUint8(c + 41, 1);
  view.setUint8(c + 42, 1);
  view.setUint8(c + 43, 1);
  view.setUint8(c + 44, 1);
  c += 45;

  view.setBigUint64(c, 5n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setFloat64(c + 16, 20, true);
  view.setFloat64(c + 24, 5, true);
  view.setFloat64(c + 32, 0.25, true);
  view.setUint8(c + 40, 1);
  view.setUint8(c + 41, 1);
  c += 42;

  view.setBigUint64(c, 6n, true);
  view.setBigUint64(c + 8, 1n, true);
  view.setUint8(c + 16, OpticalDemandKind.Building);
  view.setBigUint64(c + 17, 4n, true);
  view.setBigUint64(c + 25, 0n, true);
  view.setFloat64(c + 33, 2, true);
  view.setFloat64(c + 41, 1.5, true);
  view.setFloat64(c + 49, 1, true);
  view.setUint8(c + 57, OpticalQualityState.Healthy);
  view.setBigUint64(c + 58, 5n, true);
  view.setFloat64(c + 66, 2, true);
  return frame;
}

test('Optical snapshot accepts semantically valid entities', () => {
  const message = decodeOpticalFrame(createFrame()).message;
  assert.equal(message.nodes.length, 1);
  assert.equal(message.fiberCables.length, 1);
  assert.equal(message.equipment.length, 1);
  assert.equal(message.backhauls.length, 1);
  assert.equal(message.demands.length, 1);
});

test('Optical snapshot rejects invalid statistics and entity semantics', () => {
  const o = PROTOCOL_HEADER_SIZE;

  const statistics = createFrame();
  new DataView(statistics).setFloat64(o + 44, Number.POSITIVE_INFINITY, true);
  assert.throws(() => decodeOpticalFrame(statistics), /statistics/);

  const node = createFrame();
  new DataView(node).setUint8(o + 86 + 8, 99);
  assert.throws(() => decodeOpticalFrame(node), /node entry/);

  const cable = createFrame();
  const cableOffset = o + 86 + 33;
  new DataView(cable).setFloat64(cableOffset + 40, 1.1, true);
  assert.throws(() => decodeOpticalFrame(cable), /cable entry/);

  const equipment = createFrame();
  const equipmentOffset = cableOffset + 50;
  new DataView(equipment).setFloat64(equipmentOffset + 33, 0, true);
  assert.throws(() => decodeOpticalFrame(equipment), /equipment entry/);

  const backhaul = createFrame();
  const backhaulOffset = equipmentOffset + 45;
  new DataView(backhaul).setFloat64(backhaulOffset + 24, -1, true);
  assert.throws(() => decodeOpticalFrame(backhaul), /backhaul entry/);

  const demand = createFrame();
  const demandOffset = backhaulOffset + 42;
  new DataView(demand).setUint8(demandOffset + 57, 99);
  assert.throws(() => decodeOpticalFrame(demand), /demand entry/);
});
