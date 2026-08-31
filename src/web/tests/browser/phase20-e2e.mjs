import {
  MessageType,
  PROTOCOL_HEADER_SIZE,
  decodeFrame,
  encodeHello,
  encodeSubscribeVolume,
} from '../../src/protocol.ts';
import { WEB_CURRENT_PROTOCOL_VERSION } from '../../src/person-inspection-protocol.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5084/ws';
const result = document.querySelector('#result');
if (!(result instanceof HTMLElement)) throw new Error('E2E result element is missing.');

let sawInitialEmptyTopology = false;
let sawAdminTopology = false;
let roadSnapshotCount = 0;
let failure = null;
let connected = false;

const socket = new WebSocket(serverUrl);
socket.binaryType = 'arraybuffer';
socket.addEventListener('open', () => socket.send(encodeHello(WEB_CURRENT_PROTOCOL_VERSION)));
socket.addEventListener('error', () => { failure = new Error('WebSocket transport error.'); });
socket.addEventListener('close', () => { if (!sawAdminTopology && failure === null) failure = new Error('WebSocket closed before the administration topology was observed.'); });
socket.addEventListener('message', async (event) => {
  try {
    const buffer = await toArrayBuffer(event.data);
    if (!connected) {
      const envelope = decodeFrame(buffer);
      if (envelope.message.type === MessageType.Error) throw new Error(`Protocol error ${String(envelope.message.code)} during handshake.`);
      if (envelope.message.type !== MessageType.HelloAck) throw new Error('Expected HelloAck as the first server message.');
      connected = true;
      socket.send(encodeSubscribeVolume({ minX: -100, minY: -100, minZ: -20, maxX: 100, maxY: 100, maxZ: 20 }, envelope.message.protocolVersion));
      return;
    }

    if (buffer.byteLength < PROTOCOL_HEADER_SIZE) return;
    const type = new DataView(buffer).getUint16(8, true);
    if (type !== MessageType.RoadNetworkSnapshot) return;
    const message = decodeFrame(buffer).message;
    if (message.type !== MessageType.RoadNetworkSnapshot) return;
    roadSnapshotCount += 1;
    if (message.segments.length === 0) sawInitialEmptyTopology = true;
    if (sawInitialEmptyTopology && message.segments.length === 1 && message.nodes.length === 2 && message.lanes.length === 1) sawAdminTopology = true;
  } catch (error) {
    failure = error instanceof Error ? error : new Error(String(error));
  }
});

try {
  await waitUntil(() => connected, 'browser protocol handshake');
  await waitUntil(() => sawInitialEmptyTopology, 'initial empty Road read model');
  await waitUntil(() => sawAdminTopology, 'Road topology created through administration console');
  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({ status: 'passed', roadSnapshotCount, sawInitialEmptyTopology, sawAdminTopology });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  if (socket.readyState < WebSocket.CLOSING) socket.close(1000, 'E2E complete');
}

async function waitUntil(predicate, description, timeoutMs = 60_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    if (failure !== null) throw failure;
    if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}.`);
    await new Promise((resolve) => window.setTimeout(resolve, 50));
  }
}

async function toArrayBuffer(data) {
  if (data instanceof ArrayBuffer) return data;
  if (data instanceof Blob) return data.arrayBuffer();
  throw new Error('WebSocket frame was not binary.');
}
