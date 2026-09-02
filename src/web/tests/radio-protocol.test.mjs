import assert from 'node:assert/strict';
import test from 'node:test';
import { WEB_CURRENT_PROTOCOL_VERSION } from '../src/person-inspection-protocol.ts';
import { RADIO_SNAPSHOT_MESSAGE_TYPE, SPECTRUM_SNAPSHOT_MESSAGE_TYPE, decodeRadioFrame, isRadioFrame } from '../src/radio-protocol.ts';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';

test('web negotiates current protocol 2.19 while retaining radio support', () => {
  assert.deepEqual(WEB_CURRENT_PROTOCOL_VERSION, { major: 2, minor: 19 });
});

test('decodes empty radio snapshot frame', () => {
  const frame = createFrame(RADIO_SNAPSHOT_MESSAGE_TYPE, 66);
  const view = new DataView(frame);
  const payload = PROTOCOL_HEADER_SIZE;
  view.setFloat64(payload + 36, 0, true);
  assert.equal(isRadioFrame(frame), true);
  const envelope = decodeRadioFrame(frame);
  assert.equal(envelope.message.type, RADIO_SNAPSHOT_MESSAGE_TYPE);
  assert.equal(envelope.message.sites.length, 0);
  assert.equal(envelope.message.emissions.length, 0);
  assert.equal(envelope.message.links.length, 0);
});

test('decodes empty spectrum snapshot frame', () => {
  const frame = createFrame(SPECTRUM_SNAPSHOT_MESSAGE_TYPE, 14);
  const envelope = decodeRadioFrame(frame);
  assert.equal(envelope.message.type, SPECTRUM_SNAPSHOT_MESSAGE_TYPE);
  assert.equal(envelope.message.bands.length, 0);
  assert.equal(envelope.message.frequencyBlocks.length, 0);
  assert.equal(envelope.message.conflicts.length, 0);
});

test('rejects radio snapshot advertised as protocol 2.15', () => {
  const frame = createFrame(RADIO_SNAPSHOT_MESSAGE_TYPE, 66, 15);
  assert.throws(() => decodeRadioFrame(frame), /2\.16/);
});

function createFrame(messageType, payloadLength, minor = 16) {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, minor, true);
  view.setUint16(8, messageType, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  return frame;
}