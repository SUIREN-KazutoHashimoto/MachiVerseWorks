import { ClientPerformanceMetrics } from '../../src/client-performance.ts';
import { MachiVerseConnection } from '../../src/connection.ts';
import { EntityStore } from '../../src/entity-store.ts';
import { MessageType } from '../../src/protocol.ts';
import { simulationToThreePosition, WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const expectedTotal = Number.parseInt(parameters.get('agents') ?? '1000', 10);
const mode = parameters.get('mode') ?? 'full';
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5080/ws';

if (!Number.isInteger(expectedTotal) || expectedTotal <= 0) {
  throw new Error('agents must be a positive integer.');
}
if (mode !== 'full' && mode !== 'near' && mode !== 'altitude') {
  throw new Error('mode must be full, near, or altitude.');
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
let altitudeSeparation = null;

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
  } else if (mode === 'near') {
    await runNearbyScenario();
  } else {
    altitudeSeparation = await runAltitudeScenario();
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
    altitudeSeparation,
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
  connection.setSubscription({
    minX: -600,
    minY: -600,
    minZ: -128,
    maxX: 600,
    maxY: 600,
    maxZ: 512,
  });
  await waitUntil(() => store.size === expectedTotal, `${String(expectedTotal)} agents received`);
  view.render(store, performance.now());

  view.camera.position.x = 2_000;
  view.camera.position.z = 2_000;
  connection.setSubscription(view.getSubscriptionVolume());
  await waitUntil(() => store.size === 0, 'out-of-range agents removed after camera move');
  assert(sawRemove, 'AgentRemove was observed after camera move');

  view.camera.position.x = 0;
  view.camera.position.z = 0;
  view.camera.zoom = 8;
  view.camera.updateProjectionMatrix();
  connection.setSubscription(view.getSubscriptionVolume());
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
  connection.setSubscription(view.getSubscriptionVolume());

  await waitUntil(
    () => store.size > 0 && store.size < expectedTotal,
    'only nearby agents were received',
  );
  await waitUntil(() => sawUpdate, 'nearby agents received updates');
  view.render(store, performance.now());
}

async function runAltitudeScenario() {
  connection.setSubscription({
    minX: -1,
    minY: -1,
    minZ: 0,
    maxX: 1,
    maxY: 1,
    maxZ: 120,
  });
  await waitUntil(() => store.size === expectedTotal, 'same-horizontal-position altitude agents received');
  await waitUntil(() => sawUpdate, 'altitude agents received updates');

  const agents = [...store.sample(performance.now())];
  assert(agents.length === expectedTotal, 'all altitude agents are present in EntityStore');
  assert(agents.every((agent) => Math.abs(agent.x) < 0.000001 && Math.abs(agent.y) < 0.000001), 'altitude agents share horizontal position');

  const simulationAltitudes = new Set(agents.map((agent) => agent.z.toFixed(6)));
  assert(simulationAltitudes.size > 1, 'Protocol and EntityStore preserve distinct altitudes');

  const rendererAltitudes = new Set(
    agents.map((agent) => simulationToThreePosition(agent.x, agent.y, agent.z).y.toFixed(6)),
  );
  assert(rendererAltitudes.size === simulationAltitudes.size, 'renderer mapping preserves altitude separation');
  view.render(store, performance.now());

  return {
    simulationAltitudes: [...simulationAltitudes],
    rendererAltitudes: [...rendererAltitudes],
  };
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
