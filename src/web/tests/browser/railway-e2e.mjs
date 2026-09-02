import { MachiVerseConnection } from '../../src/connection.ts';
import { RailwayInfrastructureLayer, RailwayMessageType } from '../../src/railway-infrastructure.ts';
import { WorldView } from '../../src/world-view.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5080/ws';
const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('Railway E2E host elements are missing.');

const view = new WorldView(host);
const railwayLayer = new RailwayInfrastructureLayer(view.scene);
let state = 'disconnected';
let protocolError = null;
let clientError = null;
let negotiatedVersion = null;
let snapshot = null;

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (nextState) => { state = nextState; },
  onMessage: (message) => {
    if (message.type !== RailwayMessageType.RailwayInfrastructureSnapshot) return;
    snapshot = message;
    railwayLayer.apply(message);
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => { railwayLayer.clear(); },
  onHelloAck: (version) => { negotiatedVersion = version; },
});

try {
  connection.connect();
  await waitUntil(() => state === 'connected', 'Protocol 2.17 connection');
  connection.setSubscription({ minX: -100, minY: -60, minZ: -20, maxX: 100, maxY: 60, maxZ: 20 });
  await waitUntil(() => snapshot !== null && snapshot.segments.length === 5, 'railway snapshot from Save fixture');

  assert(negotiatedVersion?.major === 2 && negotiatedVersion?.minor === 17, 'Protocol 2.17 was negotiated');
  assert(snapshot.nodes.length === 10, 'ten TrackNodes were restored from Save');
  assert(snapshot.blocks.length === 5, 'five BlockSections were restored from Save');
  assert(snapshot.stations.length === 1, 'Station was restored from Save');
  assert(snapshot.platforms.length === 1, 'Platform was restored from Save');
  assert(snapshot.platformAccessPoints.length === 1, 'PlatformAccessPoint was restored from Save');
  assert(snapshot.depots.length === 1, 'Depot was restored from Save');
  assert(snapshot.connections.length === 0, 'grade-separated crossings do not create implicit TrackConnections');

  const elevated = segmentWithAltitude(snapshot, 8);
  const underground = segmentWithAltitude(snapshot, -8);
  assert(elevated !== null && underground !== null, 'elevated and underground tracks are present');
  assert(elevated.id !== underground.id, 'grade-separated crossing remains distinct topology');

  const tracks = view.scene.getObjectByName('railway-tracks');
  const stations = view.scene.getObjectByName('railway-stations');
  const platforms = view.scene.getObjectByName('railway-platforms');
  assert(tracks?.geometry?.getAttribute('position')?.count === 10, 'five TrackSegments render as line segments');
  assert(stations?.geometry?.getAttribute('position')?.count === 24, 'Station renders as a 3D wireframe');
  assert(platforms?.geometry?.getAttribute('position')?.count === 24, 'Platform renders as a 3D wireframe');

  const renderedAltitudes = geometryAltitudes(tracks);
  assert(renderedAltitudes.has('8.000000'), 'elevated track height is rendered');
  assert(renderedAltitudes.has('-8.000000'), 'underground track height is rendered');
  assert(renderedAltitudes.has('0.000000'), 'ground track height is rendered');

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    protocol: negotiatedVersion,
    nodes: snapshot.nodes.length,
    segments: snapshot.segments.length,
    blocks: snapshot.blocks.length,
    stations: snapshot.stations.length,
    platforms: snapshot.platforms.length,
    accessPoints: snapshot.platformAccessPoints.length,
    depots: snapshot.depots.length,
    renderedAltitudes: [...renderedAltitudes],
  });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  connection.disconnect();
  railwayLayer.dispose();
  view.dispose();
}

function segmentWithAltitude(message, altitude) {
  const nodes = new Map(message.nodes.map((node) => [node.id, node]));
  return message.segments.find((segment) => {
    const start = nodes.get(segment.startNodeId);
    const end = nodes.get(segment.endNodeId);
    return start?.z === altitude && end?.z === altitude;
  }) ?? null;
}

function geometryAltitudes(object) {
  const attribute = object?.geometry?.getAttribute('position');
  assert(attribute !== undefined, 'railway track geometry contains positions');
  const result = new Set();
  for (let index = 0; index < attribute.count; index += 1) result.add(attribute.getY(index).toFixed(6));
  return result;
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
