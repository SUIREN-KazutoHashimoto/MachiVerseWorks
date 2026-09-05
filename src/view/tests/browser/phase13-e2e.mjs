import * as THREE from 'three';

import { MachiVerseConnection } from '../../src/connection.ts';
import { MessageType } from '../../src/protocol.ts';
import { TrafficMessageType, VehicleMovementState } from '../../src/traffic-protocol.ts';
import { ViewObservationState } from '../../src/view-observation-state.ts';
import { WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5083/ws';
const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('Phase 13 E2E host elements are missing.');

const observation = new ViewObservationState();
const agents = observation.entities;
const pedestrians = observation.pedestrians;
const vehicles = observation.vehicles;
const intersections = observation.intersections;
const view = new WorldView(host);
const spawnedVehicleIds = new Set();
const updatedVehicleIds = new Set();
const arrivedVehicleIds = new Set();
const spawnPositions = new Map();
const movedVehicleIds = new Set();
let connectionState = 'disconnected';
let protocolError = null;
let clientError = null;
let roadSnapshot = null;

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (state) => { connectionState = state; },
  onMessage: (message) => {
    switch (message.type) {
      case MessageType.RoadNetworkSnapshot:
        roadSnapshot = message;
        observation.apply(message);
        break;
      case TrafficMessageType.VehicleSpawn:
        observation.apply(message);
        spawnedVehicleIds.add(message.vehicleId);
        spawnPositions.set(message.vehicleId, [message.x, message.y, message.z]);
        observeVehicle(message);
        break;
      case TrafficMessageType.VehicleUpdate:
        updatedVehicleIds.add(message.vehicleId);
        observation.apply(message);
        observeMovement(message);
        observeVehicle(message);
        break;
      case TrafficMessageType.VehicleRemove:
        observation.apply(message);
        break;
      default:
        break;
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => { observation.resetConnectionState(); },
  onHelloAck: () => {},
});

try {
  connection.connect();
  await waitUntil(() => connectionState === 'connected', 'Vehicle protocol connection');
  connection.setSubscription({ minX: -80, minY: -40, minZ: -20, maxX: 80, maxY: 40, maxZ: 40 });

  await waitUntil(() => roadSnapshot !== null && roadSnapshot.segments.length === 3 && roadSnapshot.lanes.length === 3, 'Road Traffic fixture topology');
  await waitUntil(() => vehicles.size === 3 && spawnedVehicleIds.size === 3, 'three Vehicle spawn messages');
  await waitUntil(
    () => movedVehicleIds.size === 3 || arrivedVehicleIds.size === 3,
    'Vehicle movement updates or an authoritative already-arrived snapshot');

  renderView(performance.now());
  assertRenderedVehicles();

  await waitUntil(() => arrivedVehicleIds.size === 3, 'all fixture Vehicles reaching Arrived', 20_000);
  renderView(performance.now());
  assertRenderedVehicles();

  assert(
    movedVehicleIds.size === 3 || arrivedVehicleIds.size === 3,
    'each fixture Vehicle was observed moving or already arrived when subscription began');

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    vehicles: vehicles.size,
    roadSegments: roadSnapshot.segments.length,
    spawnedVehicles: spawnedVehicleIds.size,
    updatedVehicles: updatedVehicleIds.size,
    movedVehicles: movedVehicleIds.size,
    arrivedVehicles: arrivedVehicleIds.size,
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

function renderView(now) {
  view.render(agents, now, pedestrians, vehicles, intersections, observation.roadNetwork);
}

function observeMovement(message) {
  const spawnPosition = spawnPositions.get(message.vehicleId);
  if (spawnPosition === undefined) return;
  const dx = message.x - spawnPosition[0];
  const dy = message.y - spawnPosition[1];
  const dz = message.z - spawnPosition[2];
  if ((dx * dx) + (dy * dy) + (dz * dz) > 0.01) movedVehicleIds.add(message.vehicleId);
}

function observeVehicle(message) {
  if (message.state === VehicleMovementState.Arrived) arrivedVehicleIds.add(message.vehicleId);
}

function assertRenderedVehicles() {
  const vehicleMesh = view.scene.getObjectByName('vehicles');
  assert(vehicleMesh instanceof THREE.InstancedMesh, 'WorldView contains Vehicle InstancedMesh');
  assert(vehicleMesh.count === vehicles.size && vehicleMesh.count === 3, 'three Vehicles are rendered through instancing');
  assert(view.renderer.domElement.dataset.vehicleCount === '3', 'renderer exposes Vehicle count');
}

async function waitUntil(predicate, description, timeoutMs = 12_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    throwIfConnectionFailed();
    if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}. Vehicles=${String(vehicles.size)} arrived=${String(arrivedVehicleIds.size)}.`);
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