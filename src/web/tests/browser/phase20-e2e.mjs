import { MachiVerseConnection } from '../../src/connection.ts';
import { MessageType } from '../../src/protocol.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5084/ws';
const result = document.querySelector('#result');
if (!(result instanceof HTMLElement)) throw new Error('E2E result element is missing.');

let state = 'disconnected';
let protocolError = null;
let clientError = null;
let initialRevision = null;
let finalRevision = null;
let sawInitialEmptyTopology = false;
let sawAdminTopology = false;

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (next) => { state = next; },
  onMessage: (message) => {
    if (message.type !== MessageType.RoadNetworkSnapshot) return;
    if (initialRevision === null) initialRevision = message.revision;
    if (message.segments.length === 0) sawInitialEmptyTopology = true;
    if (message.segments.length === 1 && message.nodes.length === 2 && message.lanes.length === 1) {
      finalRevision = message.revision;
      sawAdminTopology = true;
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
});

try {
  connection.connect();
  await waitUntil(() => state === 'connected', 'browser connection');
  connection.setSubscription({ minX: -100, minY: -100, minZ: -20, maxX: 100, maxY: 100, maxZ: 20 });
  await waitUntil(() => sawInitialEmptyTopology, 'initial empty Road read model');
  await waitUntil(() => sawAdminTopology, 'Road topology created through administration console');
  if (initialRevision === null || finalRevision === null || finalRevision <= initialRevision) throw new Error('Road read-model revision did not advance after administration mutation.');
  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({ status: 'passed', initialRevision: initialRevision.toString(), finalRevision: finalRevision.toString(), sawInitialEmptyTopology, sawAdminTopology });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  connection.disconnect();
}

async function waitUntil(predicate, description, timeoutMs = 60_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    if (protocolError !== null) throw protocolError;
    if (clientError !== null) throw clientError;
    if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}.`);
    await new Promise((resolve) => window.setTimeout(resolve, 50));
  }
}
