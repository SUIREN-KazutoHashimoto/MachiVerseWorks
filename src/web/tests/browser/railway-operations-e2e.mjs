import { MachiVerseConnection } from '../../src/connection.ts';
import { RailwayInfrastructureLayer, RailwayMessageType } from '../../src/railway-infrastructure.ts';
import { RailwayOperationsLayer, RailwayOperationsMessageType, RailwayServiceState, TrainMovementState } from '../../src/railway-operations.ts';
import { WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5080/ws';
const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('Railway operations E2E host elements are missing.');

const view = new WorldView(host);
const infrastructureLayer = new RailwayInfrastructureLayer(view.scene);
const operationsLayer = new RailwayOperationsLayer(view.scene);
let state = 'disconnected';
let protocolError = null;
let clientError = null;
let negotiatedVersion = null;
let infrastructure = null;
let operations = null;
let observedDelay = false;
let observedPlatform = false;
let observedDwell = false;
let observedMovement = false;
const initialPositions = new Map();

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (nextState) => { state = nextState; },
  onMessage: (message) => {
    if (message.type === RailwayMessageType.RailwayInfrastructureSnapshot) {
      infrastructure = message;
      infrastructureLayer.apply(message);
      return;
    }
    if (message.type !== RailwayOperationsMessageType.RailwayOperationsSnapshot) return;
    operations = message;
    operationsLayer.apply(message);
    if (message.services.some((service) => service.delayTicks > 0n)) observedDelay = true;
    if (message.trains.some((train) => train.currentPlatformId !== null || train.assignedPlatformId !== null)) observedPlatform = true;
    if (message.trains.some((train) => train.state === TrainMovementState.Dwelling)) observedDwell = true;
    for (const train of message.trains) {
      const initial = initialPositions.get(train.id);
      if (initial === undefined) initialPositions.set(train.id, [train.x, train.y, train.z]);
      else if (Math.hypot(train.x - initial[0], train.y - initial[1], train.z - initial[2]) > 1) observedMovement = true;
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => { infrastructureLayer.clear(); operationsLayer.clear(); },
  onHelloAck: (version) => { negotiatedVersion = version; },
});

try {
  connection.connect();
  await waitUntil(() => state === 'connected', 'Protocol 2.13 connection');
  connection.setSubscription({ minX: -120, minY: 0, minZ: -10, maxX: 120, maxY: 50, maxZ: 15 });
  await waitUntil(() => infrastructure !== null && infrastructure.stations.length === 2 && infrastructure.platforms.length === 2, 'Phase 18 railway infrastructure');
  await waitUntil(() => operations !== null && operations.trains.length === 2 && operations.services.length === 2 && operations.timetables.length === 2, 'two trains and services');
  await waitUntil(() => observedMovement && observedPlatform && observedDelay, 'movement, platform assignment, and delay');
  await waitUntil(() => operations !== null && operations.services.every((service) => service.state === RailwayServiceState.Completed), 'both services completing', 70_000);

  assert(negotiatedVersion?.major === 2 && negotiatedVersion?.minor === 13, 'Protocol 2.13 was negotiated');
  assert(infrastructure.stations.length === 2, 'two Stations were published');
  assert(infrastructure.platforms.length === 2, 'two Platforms were published');
  assert(operations.trains.length === 2, 'two Trains were published');
  assert(operations.timetables.every((timetable) => timetable.stops.length === 2), 'both Timetables contain two stops');
  assert(observedMovement, 'train 3D position changed');
  assert(observedPlatform, 'platform assignment or occupancy was observed');
  assert(observedDelay, 'delay propagation was observed');
  assert(observedDwell, 'station dwell was observed');
  assert(operations.services.every((service) => service.state === RailwayServiceState.Completed), 'both Services completed');
  assert(operations.trains.every((train) => train.state === TrainMovementState.Completed), 'both Trains completed');
  assert(operations.trains.every((train) => train.currentDepotId !== null), 'both Trains returned to a Depot');

  const group = view.scene.getObjectByName('railway-trains');
  assert(group?.children.length === 2, 'two train meshes remain rendered');
  const renderedHeights = new Set(group.children.map((child) => child.position.y.toFixed(3)));
  assert(renderedHeights.has('2.000'), 'train altitude is rendered in Three.js Y');

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    protocol: negotiatedVersion,
    trains: operations.trains.length,
    services: operations.services.length,
    stations: infrastructure.stations.length,
    platforms: infrastructure.platforms.length,
    observedMovement,
    observedPlatform,
    observedDelay,
    observedDwell,
    delays: operations.services.map((service) => service.delayTicks.toString()),
  });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  connection.disconnect();
  operationsLayer.dispose();
  infrastructureLayer.dispose();
  view.dispose();
}

async function waitUntil(predicate, description, timeoutMs = 90_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    if (protocolError !== null) throw protocolError;
    if (clientError !== null) throw clientError;
    if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}.`);
    await new Promise((resolve) => window.setTimeout(resolve, 50));
  }
  if (protocolError !== null) throw protocolError;
  if (clientError !== null) throw clientError;
}

function assert(condition, description) {
  if (!condition) throw new Error(`Assertion failed: ${description}.`);
}
