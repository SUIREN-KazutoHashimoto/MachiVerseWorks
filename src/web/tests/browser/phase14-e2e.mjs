import * as THREE from 'three';

import { MachiVerseConnection } from '../../src/connection.ts';
import { EntityStore } from '../../src/entity-store.ts';
import { PedestrianStore } from '../../src/pedestrian-store.ts';
import { MessageType } from '../../src/protocol.ts';
import {
  SignalIndication,
  TrafficMessageType,
  VehicleMovementState,
} from '../../src/traffic-protocol.ts';
import { IntersectionControlStore, VehicleStore } from '../../src/traffic-store.ts';
import { WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5082/ws';
const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('Phase 14 E2E host elements are missing.');

const agents = new EntityStore();
const pedestrians = new PedestrianStore();
const vehicles = new VehicleStore();
const intersections = new IntersectionControlStore();
const view = new WorldView(host);
const waitingVehicleIds = new Set();
const restartedVehicleIds = new Set();
const phaseIndexes = new Set();
let connectionState = 'disconnected';
let protocolError = null;
let clientError = null;
let roadSnapshot = null;
let sawVehicleUpdate = false;
let sawRed = false;
let sawGreen = false;
let sawQueue = false;

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (state) => { connectionState = state; },
  onMessage: (message) => {
    switch (message.type) {
      case MessageType.RoadNetworkSnapshot:
        roadSnapshot = message;
        view.applyRoadNetwork(message);
        break;
      case TrafficMessageType.VehicleSpawn:
        vehicles.spawn(message);
        observeVehicle(message);
        break;
      case TrafficMessageType.VehicleUpdate:
        sawVehicleUpdate = true;
        if (!vehicles.update(message)) vehicles.spawn(message);
        observeVehicle(message);
        break;
      case TrafficMessageType.VehicleRemove:
        vehicles.remove(message.vehicleId);
        break;
      case TrafficMessageType.IntersectionControlSnapshot:
        intersections.apply(message);
        phaseIndexes.add(message.phaseIndex);
        for (const movement of message.movements) {
          if (movement.indication === SignalIndication.Red) sawRed = true;
          if (movement.indication === SignalIndication.Green) sawGreen = true;
          if (movement.queueLength > 0) sawQueue = true;
        }
        break;
      default:
        break;
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => {
    agents.clear();
    pedestrians.clear();
    vehicles.clear();
    intersections.clear();
    view.clearRoadNetwork();
  },
  onHelloAck: () => {},
});

try {
  connection.connect();
  await waitUntil(() => connectionState === 'connected', 'Protocol 2.4 connection');
  connection.setSubscription({ minX: -80, minY: -80, minZ: -20, maxX: 80, maxY: 80, maxZ: 40 });

  await waitUntil(() => roadSnapshot !== null && roadSnapshot.connections.length === 4, 'traffic Road Network');
  await waitUntil(() => vehicles.size === 4 && [...intersections.active()].length === 1, 'Vehicle and intersection snapshots');
  await waitUntil(() => sawVehicleUpdate && sawRed && sawGreen, 'Vehicle updates and mixed signal indications');
  await waitUntil(() => waitingVehicleIds.size > 0 && sawQueue, 'red-signal Vehicle queue');
  await waitUntil(() => hasRenderableTrafficState(true), 'mixed signal snapshot with queue');

  const queuedRenderTime = performance.now();
  view.render(agents, queuedRenderTime, pedestrians, vehicles, intersections);
  assertRenderedTraffic(true);

  await waitUntil(() => restartedVehicleIds.size > 0, 'a queued Vehicle restarting on green', 45_000);
  await waitUntil(() => phaseIndexes.size > 1, 'fixed signal phase transition', 45_000);
  await waitUntil(() => hasRenderableTrafficState(false), 'mixed signal snapshot after phase transition', 45_000);

  const finalRenderTime = performance.now();
  view.render(agents, finalRenderTime, pedestrians, vehicles, intersections);
  assertRenderedTraffic(false);

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    vehicles: vehicles.size,
    roadConnections: roadSnapshot.connections.length,
    waitingVehicles: waitingVehicleIds.size,
    restartedVehicles: restartedVehicleIds.size,
    observedPhases: [...phaseIndexes],
    sawRed,
    sawGreen,
    sawQueue,
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

function observeVehicle(message) {
  if (message.state === VehicleMovementState.WaitingForTraffic) {
    waitingVehicleIds.add(message.vehicleId);
    return;
  }
  if (waitingVehicleIds.has(message.vehicleId) && message.speedMetersPerSecond > 0) restartedVehicleIds.add(message.vehicleId);
}

function hasRenderableTrafficState(requireQueue) {
  const active = [...intersections.active()];
  if (active.length !== 1) return false;
  const movements = active[0].movements;
  const hasRed = movements.some((movement) => movement.indication === SignalIndication.Red);
  const hasGreen = movements.some((movement) => movement.indication === SignalIndication.Green);
  const hasQueue = movements.some((movement) => movement.queueLength > 0);
  return hasRed && hasGreen && (!requireQueue || hasQueue);
}

function assertRenderedTraffic(requireQueue) {
  const vehicleMesh = view.scene.getObjectByName('vehicles');
  assert(vehicleMesh instanceof THREE.InstancedMesh, 'WorldView contains Vehicle InstancedMesh');
  assert(vehicleMesh.count === vehicles.size && vehicleMesh.count > 0, 'VehicleStore is rendered through instancing');

  const stopLines = readPositionCount('traffic-stop-lines');
  const redSignals = readPositionCount('traffic-signal-red');
  const yellowSignals = readPositionCount('traffic-signal-yellow');
  const greenSignals = readPositionCount('traffic-signal-green');
  const queueVertices = readPositionCount('traffic-queues');
  assert(stopLines === 4, 'four stop-line debug points are rendered');
  assert(redSignals + yellowSignals + greenSignals === 4, 'every movement has a rendered signal indication');
  assert(redSignals > 0 && greenSignals > 0, 'red and green signal debug points are both rendered');
  if (requireQueue) assert(queueVertices >= 2, 'queued movement renders queue geometry');
  assert(view.renderer.domElement.dataset.vehicleCount === String(vehicles.size), 'renderer exposes Vehicle count');
  assert(view.renderer.domElement.dataset.intersectionControlCount === '1', 'renderer exposes intersection controller count');
}

function readPositionCount(name) {
  const object = view.scene.getObjectByName(name);
  const attribute = object?.geometry?.getAttribute('position');
  assert(attribute !== undefined, `${name} contains position geometry`);
  return attribute.count;
}

async function waitUntil(predicate, description, timeoutMs = 20_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    throwIfConnectionFailed();
    if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}. Vehicles=${String(vehicles.size)} phases=${JSON.stringify([...phaseIndexes])}.`);
    await sleep(50);
  }
  throwIfConnectionFailed();
}

function throwIfConnectionFailed() {
  if (protocolError !== null) throw protocolError;
  if (clientError !== null) throw clientError;
}

function assert(condition, description) {
  if (!condition) throw new Error(`Assertion failed: ${description}.`);
}

function sleep(durationMs) {
  return new Promise((resolve) => window.setTimeout(resolve, durationMs));
}
