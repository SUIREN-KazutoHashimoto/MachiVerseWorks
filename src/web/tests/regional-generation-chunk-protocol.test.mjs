import test from 'node:test';
import assert from 'node:assert/strict';

import {
  REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE,
  decodeRegionalGenerationChunkFrame,
} from '../src/regional-generation-chunk-protocol.ts';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, ProtocolDecodeFailure } from '../src/protocol.ts';

const CHUNK_METADATA_LENGTH = 20;

test('RegionalGeneration chunk decoder rejects excessive chunkCount before assembly', () => {
  const frame = createChunkFrame({ chunkCount: 8_193, chunkIndex: 0, totalPayloadBytes: 1, data: new Uint8Array([1]) });
  assert.throws(() => decodeRegionalGenerationChunkFrame(frame), ProtocolDecodeFailure);
});

test('RegionalGeneration chunk decoder accepts the supported chunkCount boundary', () => {
  const frame = createChunkFrame({ chunkCount: 8_192, chunkIndex: 8_191, totalPayloadBytes: 1, data: new Uint8Array([1]) });
  const envelope = decodeRegionalGenerationChunkFrame(frame);
  assert.equal(envelope.message.chunkCount, 8_192);
  assert.equal(envelope.message.chunkIndex, 8_191);
});

function createChunkFrame({ chunkCount, chunkIndex, totalPayloadBytes, data }) {
  const payloadLength = CHUNK_METADATA_LENGTH + data.byteLength;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 22, true);
  view.setUint16(8, REGIONAL_GENERATION_SNAPSHOT_CHUNK_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  view.setBigUint64(PROTOCOL_HEADER_SIZE, 1n, true);
  view.setInt32(PROTOCOL_HEADER_SIZE + 8, chunkIndex, true);
  view.setInt32(PROTOCOL_HEADER_SIZE + 12, chunkCount, true);
  view.setInt32(PROTOCOL_HEADER_SIZE + 16, totalPayloadBytes, true);
  new Uint8Array(frame, PROTOCOL_HEADER_SIZE + CHUNK_METADATA_LENGTH).set(data);
  return frame;
}
