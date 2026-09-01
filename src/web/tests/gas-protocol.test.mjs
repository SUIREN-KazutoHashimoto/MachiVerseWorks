import test from 'node:test';
import assert from 'node:assert/strict';
import { GAS_SNAPSHOT_MESSAGE_TYPE, GasDeliveryMode, GasFacilityKind, GasNodeKind, GasOperatingState, GasServiceState, decodeGasFrame, isGasFrame } from '../dist-test/gas-protocol.js';
import { PROTOCOL_MAGIC } from '../dist-test/protocol.js';

function createFrame() {
  const payloadLength = 92 + 33 * 2 + 33 + 42 + 74;
  const frame = new ArrayBuffer(16 + payloadLength); const view = new DataView(frame); const o = 16;
  view.setUint32(0, PROTOCOL_MAGIC, true); view.setUint16(4, 2, true); view.setUint16(6, 14, true); view.setUint16(8, GAS_SNAPSHOT_MESSAGE_TYPE, true); view.setUint32(12, payloadLength, true);
  [2,1,1,0,0,1,1,0,0].forEach((value, i) => view.setUint32(o + i * 4, value, true));
  view.setFloat64(o+36,20,true); view.setFloat64(o+44,10,true); view.setFloat64(o+52,10,true); view.setFloat64(o+60,0,true); view.setFloat64(o+68,0,true); view.setBigUint64(o+76,5n,true);
  view.setUint16(o+84,2,true); view.setUint16(o+86,1,true); view.setUint16(o+88,1,true); view.setUint16(o+90,1,true);
  let c=o+92; view.setBigUint64(c,1n,true); view.setUint8(c+8,GasNodeKind.Source); c+=33; view.setBigUint64(c,2n,true); view.setUint8(c+8,GasNodeKind.Service); view.setFloat64(c+9,10,true); c+=33;
  view.setBigUint64(c,1n,true); view.setBigUint64(c+8,1n,true); view.setBigUint64(c+16,2n,true); view.setFloat64(c+24,20,true); view.setUint8(c+32,1); c+=33;
  view.setUint8(c,GasFacilityKind.Source); view.setBigUint64(c+1,1n,true); view.setBigUint64(c+9,1n,true); view.setFloat64(c+17,20,true); view.setFloat64(c+25,10,true); view.setUint8(c+41,GasOperatingState.Online); c+=42;
  view.setBigUint64(c,1n,true); view.setBigUint64(c+8,2n,true); view.setBigUint64(c+16,7n,true); view.setUint8(c+32,GasDeliveryMode.Piped); view.setFloat64(c+41,10,true); view.setFloat64(c+49,10,true); view.setFloat64(c+57,10,true); view.setUint8(c+73,GasServiceState.Supplied);
  return frame;
}

test('gas snapshot decodes protocol 2.14 payload', () => { const frame=createFrame(); assert.equal(isGasFrame(frame),true); const envelope=decodeGasFrame(frame); assert.deepEqual(envelope.version,{major:2,minor:14}); assert.equal(envelope.message.statistics.servedCubicMetersPerDay,10); assert.equal(envelope.message.servicePoints[0].serviceState,GasServiceState.Supplied); });
test('gas decoder rejects protocol 2.13', () => { const frame=createFrame(); new DataView(frame).setUint16(6,13,true); assert.throws(()=>decodeGasFrame(frame)); });
