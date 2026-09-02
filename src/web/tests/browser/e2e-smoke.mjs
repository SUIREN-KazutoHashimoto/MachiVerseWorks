import * as THREE from 'three';

import { ClientPerformanceMetrics } from '../../src/client-performance.ts';
import { MachiVerseConnection } from '../../src/connection.ts';
import { MessageType, PedestrianMovementState } from '../../src/protocol.ts';
import { ViewObservationState } from '../../src/view-observation-state.ts';
import { WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const expectedTotal = Number.parseInt(parameters.get('agents') ?? '1000', 10);
const mode = parameters.get('mode') ?? 'full';
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5080/ws';

if (!Number.isInteger(expectedTotal) || expectedTotal < 0 || (!['road', 'pedestrian'].includes(mode) && expectedTotal === 0)) throw new Error('agents must be positive except in road or pedestrian mode.');
if (!['full', 'near', 'altitude', 'road', 'pedestrian'].includes(mode)) throw new Error('mode must be full, near, altitude, road, or pedestrian.');
const host = document.querySelector('#host'), result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('E2E host elements are missing.');

const observation = new ViewObservationState(); const store = observation.entities; const pedestrians = observation.pedestrians; const clientMetrics = new ClientPerformanceMetrics(); const view = new WorldView(host); const initialSpawnStates = new Map();
let connectionState = 'disconnected', protocolError = null, clientError = null, negotiatedTickRate = null, sawUpdate = false, sawRemove = false, altitudeSeparation = null, roadSummary = null, pedestrianSummary = null, firstPedestrianSpawn = null, sawPedestrianUpdate = false;
const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (state) => { connectionState = state; },
  onMessage: (message) => {
    switch (message.type) {
      case MessageType.AgentSpawn:
        if (!initialSpawnStates.has(message.agentId)) initialSpawnStates.set(message.agentId, { ...message });
        observation.apply(message); break;
      case MessageType.AgentUpdate: sawUpdate = true; observation.apply(message); break;
      case MessageType.AgentRemove: sawRemove = true; observation.apply(message); break;
      case MessageType.PedestrianSpawn:
        if (firstPedestrianSpawn === null) firstPedestrianSpawn = { ...message };
        observation.apply(message); break;
      case MessageType.PedestrianUpdate:
        sawPedestrianUpdate = true; observation.apply(message); break;
      case MessageType.PedestrianRemove: observation.apply(message); break;
      case MessageType.RoadNetworkSnapshot: roadSnapshot = message; observation.apply(message); break;
      default: break;
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => { observation.resetConnectionState(); },
  onHelloAck: (_version, tickRate) => { negotiatedTickRate = tickRate; },
  onFrameDecoded: (metrics) => clientMetrics.recordDecode(metrics.frameBytes, metrics.decodeTimeMs),
});
let roadSnapshot = null;

try {
  connection.connect(); await waitUntil(() => connectionState === 'connected', 'browser connected to server');
  if (mode === 'full') await runFullScenario();
  else if (mode === 'near') await runNearbyScenario();
  else if (mode === 'altitude') altitudeSeparation = await runAltitudeScenario();
  else if (mode === 'road') roadSummary = await runRoadScenario();
  else pedestrianSummary = await runPedestrianScenario();
  await recordAnimationFrames(); const performanceSnapshot = clientMetrics.snapshot();
  assert(performanceSnapshot.decodeSampleCount > 0, 'client decode metrics were recorded'); assert(performanceSnapshot.decodedBytes > 0, 'decoded bytes were recorded'); assert(performanceSnapshot.frameSampleCount > 0, 'client frame metrics were recorded');
  result.dataset.status = 'passed'; result.textContent = JSON.stringify({ status: 'passed', mode, expectedTotal, visibleAgents: store.size, visiblePedestrians: pedestrians.size, sawUpdate, sawRemove, sawPedestrianUpdate, altitudeSeparation, roadSummary, pedestrianSummary, performance: performanceSnapshot });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error)); result.dataset.status = 'failed'; result.textContent = JSON.stringify({ status: 'failed', message: normalized.message }); console.error(normalized);
} finally { connection.disconnect(); view.dispose(); }

async function runFullScenario() {
  connection.setSubscription({ minX: -600, minY: -600, minZ: -128, maxX: 600, maxY: 600, maxZ: 512 }); await waitUntil(() => store.size === expectedTotal, `${String(expectedTotal)} agents received`); renderView(performance.now());
  // Perspective observation reaches roughly 3 km, so move well beyond the origin before asserting removals.
  view.camera.position.x = 10_000; view.camera.position.z = 10_000; connection.setSubscription(view.getSubscriptionVolume()); await waitUntil(() => store.size === 0, 'out-of-range agents removed after camera move'); assert(sawRemove, 'AgentRemove was observed after camera move');
  view.camera.position.x = 0; view.camera.position.z = 0; view.camera.zoom = 8; view.camera.updateProjectionMatrix(); connection.setSubscription(view.getSubscriptionVolume()); await waitUntil(() => store.size > 0 && store.size < expectedTotal, 'nearby subscription restored after camera return');
  connection.disconnect(); observation.resetConnectionState(); await sleep(100); connection.connect(); await waitUntil(() => connectionState === 'connected', 'browser reconnected'); await waitUntil(() => store.size > 0 && store.size < expectedTotal, 'client state restored from retained subscription after reconnect'); renderView(performance.now());
}
async function runNearbyScenario() { view.camera.position.x = 0; view.camera.position.z = 0; view.camera.zoom = 8; view.camera.updateProjectionMatrix(); connection.setSubscription(view.getSubscriptionVolume()); await waitUntil(() => store.size > 0 && store.size < expectedTotal, 'only nearby agents were received'); await waitUntil(() => sawUpdate, 'nearby agents received updates'); renderView(performance.now()); }
async function runAltitudeScenario() {
  connection.setSubscription({ minX: -128, minY: -128, minZ: 0, maxX: 128, maxY: 128, maxZ: 120 }); await waitUntil(() => store.size === expectedTotal, 'altitude agents received'); await waitUntil(() => initialSpawnStates.size === expectedTotal, 'initial altitude AgentSpawn states recorded'); await waitUntil(() => sawUpdate, 'altitude agents received updates'); assert(Number.isInteger(negotiatedTickRate) && negotiatedTickRate > 0, 'server tick rate was negotiated');
  const spawnStates = [...initialSpawnStates.values()]; const reconstructedOrigins = spawnStates.map((agent) => { const elapsedSeconds = Number(agent.tickCount) / negotiatedTickRate; return { x: agent.x - agent.velocityX * elapsedSeconds, y: agent.y - agent.velocityY * elapsedSeconds, z: agent.z - agent.velocityZ * elapsedSeconds }; });
  assert(reconstructedOrigins.every((agent) => nearlyEqual(agent.x, 0) && nearlyEqual(agent.y, 0)), 'delivered AgentSpawn state reconstructs the same initial horizontal position'); const initialAltitudes = new Set(reconstructedOrigins.map((agent) => agent.z.toFixed(6))); assert(initialAltitudes.size > 1, 'AgentSpawn preserves distinct initial altitudes');
  const sampleTime = performance.now(); const agents = [...store.sample(sampleTime)]; assert(agents.length === expectedTotal, 'all altitude agents are present in EntityStore'); const simulationAltitudes = new Set(agents.map((agent) => agent.z.toFixed(6))); assert(simulationAltitudes.size === initialAltitudes.size, 'AgentUpdate and EntityStore preserve altitude separation'); renderView(sampleTime); const rendererAltitudes = readRenderedAltitudes(expectedTotal); assert(rendererAltitudes.size === simulationAltitudes.size, 'actual InstancedMesh transforms preserve altitude separation');
  return { initialAltitudes: [...initialAltitudes], simulationAltitudes: [...simulationAltitudes], rendererAltitudes: [...rendererAltitudes] };
}
async function runRoadScenario() {
  connection.setSubscription({ minX: -160, minY: -160, minZ: -40, maxX: 160, maxY: 160, maxZ: 40 }); await waitUntil(() => roadSnapshot !== null && roadSnapshot.segments.length === 5, 'five RoadSegments received from Save fixture');
  assert(roadSnapshot.nodes.length === 9, 'RoadNode fixture count is preserved'); assert(roadSnapshot.lanes.length === 2, 'Lane fixture count is preserved'); assert(roadSnapshot.connections.length === 1, 'explicit turn connection is preserved'); assert(roadSnapshot.accessPoints.length === 1, 'Building/POI road access is preserved');
  const ground = roadSnapshot.segments.find((segment) => segment.id === 1n), elevated = roadSnapshot.segments.find((segment) => segment.id === 2n); assert(ground !== undefined && elevated !== undefined, 'ground and elevated segments are present'); const groundNodes = new Set([ground.startNodeId, ground.endNodeId]); assert(!groundNodes.has(elevated.startNodeId) && !groundNodes.has(elevated.endNodeId), 'same-XY grade-separated crossing has no implicit topology');
  renderView(performance.now()); const roadAltitudes = readGeometryAltitudes('road-segments'); assert(roadAltitudes.has('-15.000000') && roadAltitudes.has('0.000000') && roadAltitudes.has('20.000000'), 'renderer preserves underground, ground, and elevated road heights'); const laneGeometry = view.scene.getObjectByName('road-lanes'); assert(laneGeometry?.geometry?.getAttribute('position')?.count === 4, 'two lanes render as two line segments'); const intersections = view.scene.getObjectByName('road-intersections'); assert(intersections?.geometry?.getAttribute('position')?.count === 1, 'one explicit intersection renders');
  return { nodes: roadSnapshot.nodes.length, segments: roadSnapshot.segments.length, lanes: roadSnapshot.lanes.length, connections: roadSnapshot.connections.length, accessPoints: roadSnapshot.accessPoints.length, rendererAltitudes: [...roadAltitudes] };
}
async function runPedestrianScenario() {
  connection.setSubscription({ minX: -80, minY: -40, minZ: -20, maxX: 80, maxY: 40, maxZ: 40 });
  await waitUntil(() => pedestrians.size === 1 && firstPedestrianSpawn !== null, 'pedestrian spawn received');
  const firstObserved = firstPedestrianSpawn;
  await waitUntil(() => sawPedestrianUpdate, 'pedestrian update received');
  await waitUntil(() => [...pedestrians.sample()].some((pedestrian) => pedestrian.state === PedestrianMovementState.Arrived && nearlyEqual(pedestrian.x, 20) && nearlyEqual(pedestrian.y, 0) && nearlyEqual(pedestrian.z, 0)), 'pedestrian arrived at destination building and interpolation settled');
  const arrived = [...pedestrians.sample()].find((pedestrian) => pedestrian.pedestrianId === firstObserved.pedestrianId);
  assert(arrived !== undefined, 'arrived pedestrian remains in client store');
  const observedDistance = Math.hypot(arrived.x - firstObserved.x, arrived.y - firstObserved.y, arrived.z - firstObserved.z);
  const fixtureJourneyDistance = Math.hypot(arrived.x - (-20), arrived.y, arrived.z);
  assert(fixtureJourneyDistance > 20, `pedestrian completed the seeded Building-to-Building journey (${String(fixtureJourneyDistance)}m)`);
  assert(nearlyEqual(arrived.x, 20) && nearlyEqual(arrived.y, 0) && nearlyEqual(arrived.z, 0), 'pedestrian arrived at the seeded destination RoadAccessPoint');
  const sampleTime = performance.now(); renderView(sampleTime);
  const mesh = view.scene.getObjectByName('pedestrians');
  assert(mesh instanceof THREE.InstancedMesh, 'WorldView contains the Pedestrian InstancedMesh');
  assert(mesh.count === 1, 'one pedestrian is rendered through instancing');
  return { pedestrianId: arrived.pedestrianId.toString(), tripRequestId: arrived.tripRequestId.toString(), state: arrived.state, observedDistance, fixtureJourneyDistance, rendererCount: mesh.count };
}
function renderView(now) { view.render(store, now, pedestrians, null, null, observation.roadNetwork); }
function readRenderedAltitudes(expectedCount) { const mesh = view.scene.getObjectByName('agents'); assert(mesh instanceof THREE.InstancedMesh, 'WorldView contains the Agent InstancedMesh'); assert(mesh.count === expectedCount, `InstancedMesh contains ${String(expectedCount)} rendered agents`); const matrix = new THREE.Matrix4(), altitudes = new Set(); for (let index = 0; index < mesh.count; index += 1) { mesh.getMatrixAt(index, matrix); altitudes.add(matrix.elements[13].toFixed(6)); } return altitudes; }
function readGeometryAltitudes(name) { const object = view.scene.getObjectByName(name), attribute = object?.geometry?.getAttribute('position'); assert(attribute !== undefined, `${name} contains position geometry`); const altitudes = new Set(); for (let index = 0; index < attribute.count; index += 1) altitudes.add(attribute.getY(index).toFixed(6)); return altitudes; }
async function recordAnimationFrames() { await new Promise((resolve) => { window.requestAnimationFrame((first) => { clientMetrics.recordAnimationFrame(first); renderView(first); window.requestAnimationFrame((second) => { clientMetrics.recordAnimationFrame(second); renderView(second); resolve(); }); }); }); }
async function waitUntil(predicate, description, timeoutMs = 90_000) { const deadline = performance.now() + timeoutMs; while (!predicate()) { throwIfConnectionFailed(); if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}. Current agent count: ${String(store.size)}, pedestrian count: ${String(pedestrians.size)}.`); await sleep(50); } throwIfConnectionFailed(); }
function throwIfConnectionFailed() { if (protocolError !== null) throw protocolError; if (clientError !== null) throw clientError; }
function assert(condition, description) { if (!condition) throw new Error(`Assertion failed: ${description}.`); }
function nearlyEqual(left, right, epsilon = 1e-6) { return Math.abs(left - right) <= epsilon; }
function sleep(durationMs) { return new Promise((resolve) => window.setTimeout(resolve, durationMs)); }
