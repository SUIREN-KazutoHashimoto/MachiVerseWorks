import { ClientPerformanceMetrics } from '../../src/client-performance.ts';
import { MachiVerseConnection } from '../../src/connection.ts';
import { EntityStore } from '../../src/entity-store.ts';
import { MessageType } from '../../src/protocol.ts';
import { WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const expectedTotal = Number.parseInt(parameters.get('agents') ?? '1000', 10);
const mode = parameters.get('mode') ?? 'full';
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5080/ws';

if (!Number.isInteger(expectedTotal) || expectedTotal <= 0) {
  throw new Error('agents must be a positive integer.');
}
if (mode !== 'full' && mode !== 'near') {
  throw new Error('mode must be full or near.');
}

const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) {
  throw new Error('E2E host elements are missing.');
}

const store = new EntityStore();
const clientMetrics = new ClientPerformanceMetrics();
const view = new WorldView(host);
let connectionState = 'disconnected';
let protocolError = null;
let clientError = null;
let sawUpdate = false;
let sawRemove = false;

const connection = new MachiVerseConnection(
  serverUrl,
  { minimumDelayMs: 100, maximumDelayMs: 500 },
  {
    onStateChanged: (state) => { connectionState = state; },
    onMessage: (message) => {
      switch (message.type) {
        case MessageType.AgentSpawn:
          store.spawn(message);
          break;
        case MessageType.AgentUpdate:
          sawUpdate = true;
          if (!store.update(message)) {
            store.spawn(message);
          }
          break;
        case MessageType.AgentRemove:
          sawRemove = true;
          store.remove(message.agentId);
          break;
        default:
          break;
      }
    },
    onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
    onClientError: (error) => { clientError = error; },
    onDisconnected: () => { store.clear(); },
    onHelloAck: () => {},
    onFrameDecoded: (metrics) => clientMetrics.recordDecode(metrics.frameBytes, metrics.decodeTimeMs),
  },
);

try {
  connection.connect();
  await waitUntil(() => connectionState === 'connected', 'browser connected to server');

  if (mode === 'full') {
    await runFullScenario();
  } else {
    await runNearbyScenario();
  }

  await recordAnimationFrames();
  const performanceSnapshot = clientMetrics.snapshot();
  assert(performanceSnapshot.decodeSampleCount > 0, 'client decode metrics were recorded');
  assert(performanceSnapshot.decodedBytes > 0, 'decoded bytes were recorded');
  assert(performanceSnapshot.frameSampleCount > 0, 'client frame metrics were recorded');

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    mode,
    expectedTotal,
    visibleAgents: store.size,
    sawUpdate,
    sawRemove,
    performance: performanceSnapshot,
  });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  connection.disconnect();
  view.dispose();
}

async function runFullScenario() {
  connection.setSubscription({ minX: -600, minY: -600, maxX: 600, maxY: 600 });
  await waitUntil(() => store.size === expectedTotal, `${String(expectedTotal)} agents received`);
  view.render(store, performance.now());

  view.camera.position.x = 2_000;
  view.camera.position.z = 2_000;
  connection.setSubscription(view.getSubscriptionArea());
  await waitUntil(() => store.size === 0, 'out-of-range agents removed after camera move');
  assert(sawRemove, 'AgentRemove was observed after camera move');

  view.camera.position.x = 0;
  view.camera.position.z = 0;
  view.camera.zoom = 8;
  view.camera.updateProjectionMatrix();
  connection.setSubscription(view.getSubscriptionArea());
  await waitUntil(
    () => store.size > 0 && store.size < expectedTotal,
    'nearby subscription restored after camera return',
  );

  connection.disconnect();
  store.clear();
  await sleep(100);
  connection.connect();
  await waitUntil(() => connectionState === 'connected', 'browser reconnected');
  await waitUntil(
    () => store.size > 0 && store.size < expectedTotal,
    'client state restored from retained subscription after reconnect',
  );
  view.render(store, performance.now());
}

async function runNearbyScenario() {
  view.camera.position.x = 0;
  view.camera.position.z = 0;
  view.camera.zoom = 8;
  view.camera.updateProjectionMatrix();
  connection.setSubscription(view.getSubscriptionArea());

  await waitUntil(
    () => store.size > 0 && store.size < expectedTotal,
    'only nearby agents were received',
  );
  await waitUntil(() => sawUpdate, 'nearby agents received updates');
  view.render(store, performance.now());
}

async function recordAnimationFrames() {
  await new Promise((resolve) => {
    window.requestAnimationFrame((first) => {
      clientMetrics.recordAnimationFrame(first);
      view.render(store, first);
      window.requestAnimationFrame((second) => {
        clientMetrics.recordAnimationFrame(second);
        view.render(store, second);
        resolve();
      });
    });
  });
}

async function waitUntil(predicate, description, timeoutMs = 90_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    throwIfConnectionFailed();
    if (performance.now() >= deadline) {
      throw new Error(`Timed out waiting for ${description}. Current agent count: ${String(store.size)}.`);
    }
    await sleep(50);
  }
  throwIfConnectionFailed();
}

function throwIfConnectionFailed() {
  if (protocolError !== null) {
    throw protocolError;
  }
  if (clientError !== null) {
    throw clientError;
  }
}

function assert(condition, description) {
  if (!condition) {
    throw new Error(`Assertion failed: ${description}.`);
  }
}

function sleep(durationMs) {
  return new Promise((resolve) => window.setTimeout(resolve, durationMs));
}
