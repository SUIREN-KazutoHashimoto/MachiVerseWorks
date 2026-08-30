import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  MultimodalTransitMessageType,
  TransitMode,
  TransitStopKind,
  TransitVehicleKind,
  TransitVehicleState,
  decodeMultimodalTransitFrame,
  isMultimodalTransitFrame,
} from '../src/multimodal-transit.ts';

function createFixtureFrame() {
  const payloadLength = 28 + 9 + (57 * 2) + 28 + (24 * 2) + (70 * 2) + 32;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 8, true);
  view.setUint16(8, MultimodalTransitMessageType.MultimodalTransitSnapshot, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  let cursor = PROTOCOL_HEADER_SIZE;
  const u8 = (value) => { view.setUint8(cursor, value); cursor += 1; };
  const i32 = (value) => { view.setInt32(cursor, value, true); cursor += 4; };
  const u32 = (value) => { view.setUint32(cursor, value, true); cursor += 4; };
  const u64 = (value) => { view.setBigUint64(cursor, BigInt(value), true); cursor += 8; };
  const f64 = (value) => { view.setFloat64(cursor, value, true); cursor += 8; };

  u64(500); u32(1); u32(2); u32(1); u32(2); u32(1);
  u64(10); u8(TransitMode.Bus);
  u64(20); u8(TransitStopKind.Bus); f64(-10); f64(0); f64(1); u64(5); u64(0); u64(0);
  u64(21); u8(TransitStopKind.Bus); f64(25); f64(0); f64(1); u64(5); u64(0); u64(0);
  u64(30); u64(10); u64(0); u32(2);
  u64(20); u64(0); u64(15);
  u64(21); u64(90); u64(20);
  u64(40); u8(TransitVehicleKind.Bus); u64(31); u64(100); i32(0); f64(-5); f64(0); f64(1); u8(TransitVehicleState.EnRouteToStop); u64(550); u64(0);
  u64(41); u8(TransitVehicleKind.Taxi); u64(0); u64(101); i32(0); f64(2); f64(3); f64(1); u8(TransitVehicleState.EnRouteToPickup); u64(0); u64(0);
  u64(21); u64(10); u64(40); u64(550);
  assert.equal(cursor, frame.byteLength);
  return frame;
}

test('Protocol 2.8 multimodal transit decodes routes stops bus taxi and arrivals', () => {
  const frame = createFixtureFrame();
  assert.equal(isMultimodalTransitFrame(frame), true);
  const envelope = decodeMultimodalTransitFrame(frame);
  assert.deepEqual(envelope.version, { major: 2, minor: 8 });
  assert.equal(envelope.message.tickCount, 500n);
  assert.deepEqual(envelope.message.lines, [{ id: 10n, mode: TransitMode.Bus }]);
  assert.equal(envelope.message.stops.length, 2);
  assert.equal(envelope.message.patterns[0].stops[1].travelTicksFromPrevious, 90n);
  assert.equal(envelope.message.vehicles[0].kind, TransitVehicleKind.Bus);
  assert.equal(envelope.message.vehicles[1].kind, TransitVehicleKind.Taxi);
  assert.equal(envelope.message.arrivalEstimates[0].estimatedArrivalTick, 550n);
});

test('multimodal transit rejects Protocol 2.7 and invalid stop references', () => {
  const oldFrame = createFixtureFrame();
  new DataView(oldFrame).setUint16(6, 7, true);
  assert.throws(() => decodeMultimodalTransitFrame(oldFrame), /2\.8/);

  const malformed = createFixtureFrame();
  const firstPatternStopIdOffset = PROTOCOL_HEADER_SIZE + 28 + 9 + (57 * 2) + 28;
  new DataView(malformed).setBigUint64(firstPatternStopIdOffset, 999n, true);
  assert.throws(() => decodeMultimodalTransitFrame(malformed), /Pattern stop/);
});
