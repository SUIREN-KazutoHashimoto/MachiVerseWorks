import { decodeFrame, encodeHello, encodeSubscribeVolume, MessageType } from '../../src/protocol.ts';

const WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE = 800;
const PROTOCOL_HEADER_SIZE = 16;
const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 29 E2E harness is invalid.');

const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for the Phase 29 WorldEnvironment snapshot.')), 60_000);
let handshaken = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 17 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 17) throw new Error('Protocol 2.17 handshake failed.');
      handshaken = true;
      socket.send(encodeSubscribeVolume({ minX: -500_000, minY: -500_000, minZ: -12_000, maxX: 500_000, maxY: 500_000, maxZ: 12_000 }, { major: 2, minor: 17 }));
      return;
    }

    const view = new DataView(frame);
    if (frame.byteLength < PROTOCOL_HEADER_SIZE || view.getUint16(8, true) !== WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE) return;
    if (view.getUint16(4, true) !== 2 || view.getUint16(6, true) !== 17) throw new Error('WorldEnvironment snapshot protocol version mismatch.');
    const payloadLength = view.getUint32(12, true);
    if (payloadLength + PROTOCOL_HEADER_SIZE !== frame.byteLength) throw new Error('WorldEnvironment snapshot frame length mismatch.');
    const payloadBytes = new Uint8Array(frame, PROTOCOL_HEADER_SIZE, payloadLength);
    const snapshot = JSON.parse(new TextDecoder('utf-8', { fatal: true }).decode(payloadBytes));
    validateSnapshot(snapshot);
    const stableSnapshot = { ...snapshot, tickCount: 0 };
    const digest = await sha256(JSON.stringify(stableSnapshot));

    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.dataset.hash = digest;
    result.textContent = `Phase 29 E2E passed: hash=${digest}, samples=${snapshot.samples.length}, terrain=${snapshot.terrainSamples.length}, features=${snapshot.features.length}, toponyms=${snapshot.toponyms.length}`;
  } catch (error) {
    fail(error);
  }
});
socket.addEventListener('error', () => fail(new Error('WebSocket transport failed.')));

function validateSnapshot(snapshot) {
  if (snapshot === null || typeof snapshot !== 'object') throw new Error('WorldEnvironment snapshot is not an object.');
  if (snapshot.config?.worldSeed !== 29027) throw new Error(`Unexpected world seed: ${String(snapshot.config?.worldSeed)}`);
  if (!Array.isArray(snapshot.samples) || snapshot.samples.length !== 64) throw new Error('Expected 64 global environment samples.');
  if (!Array.isArray(snapshot.terrainSamples) || snapshot.terrainSamples.length !== snapshot.samples.length) throw new Error('Detailed terrain sample count does not match global samples.');
  if (!Array.isArray(snapshot.features) || snapshot.features.length === 0) throw new Error('No deterministic GeographicFeature entities were observed.');
  if (!Array.isArray(snapshot.toponyms) || snapshot.toponyms.length !== snapshot.features.length) throw new Error('Natural toponyms do not match GeographicFeature entities.');
  for (const sample of snapshot.samples) {
    if (!Number.isFinite(sample.elevationMeters) || !Number.isFinite(sample.coastlineDistanceMeters) || sample.buildability < 0 || sample.buildability > 1 || sample.settlementScore < 0 || sample.settlementScore > 1) throw new Error('Global environment sample is invalid.');
  }
  for (const sample of snapshot.terrainSamples) {
    if (!Number.isFinite(sample.z) || !Number.isFinite(sample.slopeDegrees) || !Number.isFinite(sample.normalZ)) throw new Error('Detailed terrain sample is invalid.');
  }
  for (const feature of snapshot.features) {
    if (!Array.isArray(feature.geometry) || feature.geometry.length === 0 || !(feature.areaSquareMeters > 0)) throw new Error('GeographicFeature geometry is invalid.');
  }
  for (const toponym of snapshot.toponyms) {
    if (typeof toponym.name !== 'string' || toponym.name.length === 0 || toponym.generatorKey !== 'phase29-natural-v1') throw new Error('Natural toponym provenance is invalid.');
  }
}

async function sha256(value) {
  const bytes = new TextEncoder().encode(value);
  const digest = await crypto.subtle.digest('SHA-256', bytes);
  return Array.from(new Uint8Array(digest), (item) => item.toString(16).padStart(2, '0')).join('');
}

function fail(error) {
  clearTimeout(timeout);
  if (socket.readyState < WebSocket.CLOSING) socket.close();
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
}
