import { EntityStore } from '../../src/entity-store.ts';
import { MessageType, decodeFrame, encodeHello, encodeSubscribeVolume } from '../../src/protocol.ts';
import { WorldView } from '../../src/world-view.ts';
import { WorldEnvironmentStore } from '../../src/world-environment-store.ts';
import { decodeWorldEnvironmentFrame, isWorldEnvironmentFrame } from '../../src/world-environment-protocol.ts';

const result = document.querySelector('#result');
const viewport = document.querySelector('#viewport');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || !(viewport instanceof HTMLElement) || server === null) throw new Error('View Phase 3 E2E harness is invalid.');

const view = new WorldView(viewport);
const entities = new EntityStore();
const environment = new WorldEnvironmentStore();
const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for the View Phase 3 WorldEnvironment snapshot.')), 20_000);
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

    if (!isWorldEnvironmentFrame(frame)) return;
    const snapshot = decodeWorldEnvironmentFrame(frame).message;
    environment.replace(snapshot);
    const visualCameraSpan = positionVisualCamera(snapshot);
    view.render(entities, performance.now(), null, null, null, null, environment);

    const physicalRoot = view.scene.getObjectByName('physical-world');
    const terrain = view.scene.getObjectByName('terrain-surface');
    const flatGrid = view.scene.children.find((child) => child.type === 'GridHelper');
    const metrics = view.getRenderingMetrics();
    if (physicalRoot === undefined || terrain === undefined) throw new Error('Physical World renderer did not create terrain geometry.');
    if (flatGrid !== undefined) throw new Error('Legacy flat GridHelper is still present.');
    if (metrics.physicalWorld.terrainTriangles <= 0) throw new Error('Physical World terrain contains no triangles.');
    if (metrics.physicalWorld.geographicFeatureSegments <= 0) throw new Error('No GeographicFeature geometry was rendered.');
    if (metrics.physicalWorld.naturalToponymLabels !== snapshot.toponyms.length) throw new Error('Natural toponym labels do not match the authoritative observation.');
    if (!(metrics.frameTimeMs >= 0) || metrics.drawCalls <= 0 || metrics.geometries <= 0 || metrics.physicalWorld.geometryByteLength <= 0) throw new Error('Physical World rendering baseline metrics are invalid.');

    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.dataset.frameTimeMs = metrics.frameTimeMs.toFixed(3);
    result.dataset.drawCalls = String(metrics.drawCalls);
    result.dataset.geometries = String(metrics.geometries);
    result.dataset.textures = String(metrics.textures);
    result.dataset.geometryBytes = String(metrics.physicalWorld.geometryByteLength);
    result.dataset.terrainTriangles = String(metrics.physicalWorld.terrainTriangles);
    result.dataset.waterSamples = String(metrics.physicalWorld.waterSamples);
    result.dataset.featureSegments = String(metrics.physicalWorld.geographicFeatureSegments);
    result.dataset.toponymLabels = String(metrics.physicalWorld.naturalToponymLabels);
    result.dataset.visualCameraSpan = visualCameraSpan.toFixed(3);
    result.textContent = `View Phase 3 E2E passed: frame=${result.dataset.frameTimeMs}ms, draws=${result.dataset.drawCalls}, geometryBytes=${result.dataset.geometryBytes}, triangles=${result.dataset.terrainTriangles}, water=${result.dataset.waterSamples}, features=${result.dataset.featureSegments}, labels=${result.dataset.toponymLabels}`;
  } catch (error) {
    fail(error);
  }
});
socket.addEventListener('error', () => fail(new Error('WebSocket transport failed.')));

function positionVisualCamera(snapshot) {
  const samples = snapshot.terrainSamples;
  if (samples.length === 0) throw new Error('Physical World visual fixture contains no terrain samples.');

  let minX = Number.POSITIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let minZ = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  let maxZ = Number.NEGATIVE_INFINITY;
  for (const sample of samples) {
    minX = Math.min(minX, sample.x);
    minY = Math.min(minY, sample.y);
    minZ = Math.min(minZ, sample.z);
    maxX = Math.max(maxX, sample.x);
    maxY = Math.max(maxY, sample.y);
    maxZ = Math.max(maxZ, sample.z);
  }

  const centerX = (minX + maxX) * 0.5;
  const centerY = (minY + maxY) * 0.5;
  const centerZ = (minZ + maxZ) * 0.5;
  const span = Math.max(maxX - minX, maxY - minY, 1);
  view.camera.position.set(centerX + span * 0.55, maxZ + span * 0.8, centerY + span * 0.85);
  view.camera.lookAt(centerX, centerZ, centerY);
  view.camera.far = Math.max(view.camera.far, span * 5);
  view.camera.updateProjectionMatrix();
  return span;
}

function fail(error) {
  clearTimeout(timeout);
  if (socket.readyState < WebSocket.CLOSING) socket.close();
  view.dispose();
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
}
