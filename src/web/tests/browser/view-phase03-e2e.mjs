import { EntityStore } from '../../src/entity-store.ts';
import { MessageType, decodeFrame, encodeHello, encodeSubscribeVolume } from '../../src/protocol.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE } from '../../src/regional-generation-protocol.ts';
import { RegionalGenerationStore } from '../../src/regional-generation-store.ts';
import { SettlementStructureRenderer } from '../../src/settlement-structure-renderer.ts';
import { WorldView } from '../../src/world-view.ts';
import { WorldEnvironmentStore } from '../../src/world-environment-store.ts';
import { decodeWorldEnvironmentFrame, isWorldEnvironmentFrame } from '../../src/world-environment-protocol.ts';

const result = document.querySelector('#result');
const viewport = document.querySelector('#viewport');
const parameters = new URLSearchParams(location.search);
const server = parameters.get('server');
const visualCheckpoint = parameters.get('visualCheckpoint') ?? 'physical-world';
const integratedVisual = visualCheckpoint !== 'physical-world';
if (!(result instanceof HTMLElement) || !(viewport instanceof HTMLElement) || server === null) throw new Error('View Phase 3 E2E harness is invalid.');

const view = new WorldView(viewport);
const entities = new EntityStore();
const environment = new WorldEnvironmentStore();
const regionalGeneration = new RegionalGenerationStore();
let settlementRenderer = null;
let integratedFixture = null;
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
      const volume = integratedVisual
        ? { minX: -4_000, minY: -4_000, minZ: -2_000, maxX: 4_000, maxY: 4_000, maxZ: 2_000 }
        : { minX: -500_000, minY: -500_000, minZ: -12_000, maxX: 500_000, maxY: 500_000, maxZ: 12_000 };
      socket.send(encodeSubscribeVolume(volume, { major: 2, minor: 17 }));
      return;
    }

    if (!isWorldEnvironmentFrame(frame)) return;
    const snapshot = decodeWorldEnvironmentFrame(frame).message;
    environment.replace(snapshot);

    let visualCameraSpan;
    if (integratedVisual) {
      integratedFixture = createIntegratedFixture(snapshot);
      for (const agent of integratedFixture.agents) entities.spawn(agent, performance.now());
      regionalGeneration.replace(integratedFixture.regionalGeneration);
      settlementRenderer = new SettlementStructureRenderer(view.scene);
      settlementRenderer.update(regionalGeneration);
      visualCameraSpan = positionIntegratedVisualCamera(visualCheckpoint, integratedFixture);
    } else {
      visualCameraSpan = positionVisualCamera(snapshot);
    }

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

    if (integratedFixture !== null) {
      const agentMesh = view.scene.getObjectByName('agents');
      const buildingMesh = view.scene.getObjectByName('regional-buildings');
      if (agentMesh === undefined || buildingMesh === undefined) throw new Error('Integrated visual fixture did not create Agent and Building presentation.');
      if (settlementRenderer?.metrics.buildings !== 1) throw new Error('Integrated visual fixture did not render exactly one Building.');
      if (entities.size !== integratedFixture.agents.length) throw new Error('Integrated visual fixture Agent count changed unexpectedly.');
      for (const agent of integratedFixture.agents) {
        const groundingDelta = Math.abs(agent.z - agent.groundZ);
        if (groundingDelta > 0.000_001) throw new Error(`Agent ${agent.agentId.toString()} is not grounded: ${String(groundingDelta)}m.`);
      }
    }

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
    result.dataset.visualCheckpoint = visualCheckpoint;
    result.dataset.agentCount = String(entities.size);
    result.dataset.buildingCount = String(settlementRenderer?.metrics.buildings ?? 0);
    result.textContent = `View Phase 3 E2E passed: checkpoint=${visualCheckpoint}, frame=${result.dataset.frameTimeMs}ms, draws=${result.dataset.drawCalls}, geometryBytes=${result.dataset.geometryBytes}, triangles=${result.dataset.terrainTriangles}, water=${result.dataset.waterSamples}, features=${result.dataset.featureSegments}, labels=${result.dataset.toponymLabels}, agents=${result.dataset.agentCount}, buildings=${result.dataset.buildingCount}`;

    window.__MACHIVERSE_VISUAL_TEST__ = Object.freeze({
      getSceneDiagnostics: () => Object.freeze({
        checkpoint: visualCheckpoint,
        integrated: integratedVisual,
        agents: integratedFixture?.agents.map((agent) => Object.freeze({
          agentId: agent.agentId.toString(),
          x: agent.x,
          y: agent.y,
          z: agent.z,
          groundZ: agent.groundZ,
          groundingDeltaMeters: Math.abs(agent.z - agent.groundZ),
        })) ?? [],
        buildingCount: settlementRenderer?.metrics.buildings ?? 0,
        terrainTriangles: metrics.physicalWorld.terrainTriangles,
      }),
    });
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

function positionIntegratedVisualCamera(checkpoint, fixture) {
  const focus = fixture.focus;
  const camera = view.camera;
  if (checkpoint === 'world-overview') {
    camera.position.set(focus.x + 1_500, focus.z + 1_300, focus.y + 1_800);
    camera.lookAt(focus.x, focus.z + 20, focus.y);
    camera.far = Math.max(camera.far, 10_000);
    camera.updateProjectionMatrix();
    return 2_500;
  }
  if (checkpoint === 'terrain-closeup') {
    camera.position.set(focus.x + 650, focus.z + 420, focus.y + 650);
    camera.lookAt(focus.x, focus.z, focus.y);
    camera.updateProjectionMatrix();
    return 900;
  }
  if (checkpoint === 'city-center') {
    camera.position.set(focus.x + 260, focus.z + 190, focus.y + 300);
    camera.lookAt(focus.x, focus.z + 25, focus.y);
    camera.updateProjectionMatrix();
    return 420;
  }
  if (checkpoint === 'agent-grounding') {
    const agent = fixture.agents[0];
    camera.position.set(agent.x + 28, agent.z + 18, agent.y + 34);
    camera.lookAt(agent.x, agent.z + 2.5, agent.y);
    camera.updateProjectionMatrix();
    return 48;
  }
  throw new Error(`Unknown integrated visual checkpoint: ${checkpoint}`);
}

function createIntegratedFixture(snapshot) {
  const samples = uniqueTerrainSamples(snapshot.terrainSamples);
  if (samples.length < 3) throw new Error('Integrated visual fixture requires at least three terrain columns.');
  const centerX = samples.reduce((sum, sample) => sum + sample.x, 0) / samples.length;
  const centerY = samples.reduce((sum, sample) => sum + sample.y, 0) / samples.length;
  const nearest = [...samples]
    .sort((left, right) => squaredDistance(left, centerX, centerY) - squaredDistance(right, centerX, centerY))
    .slice(0, 3);
  const focus = nearest[0];
  if (focus === undefined) throw new Error('Integrated visual fixture could not select a terrain focus sample.');

  const nowTick = 42n;
  const agents = Object.freeze(nearest.map((sample, index) => Object.freeze({
    agentId: BigInt(index + 1),
    x: sample.x,
    y: sample.y,
    z: sample.z,
    groundZ: sample.z,
    velocityX: 0,
    velocityY: 0,
    velocityZ: 0,
    tickCount: nowTick,
  })));

  const halfParcel = 80;
  const halfBuilding = 34;
  const baseZ = focus.z;
  const regionalGenerationSnapshot = Object.freeze({
    type: REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE,
    tickCount: nowTick,
    worldSeed: 29_027n,
    preset: 1,
    iterations: 0,
    minX: focus.x - 500,
    minY: focus.y - 500,
    minZ: baseZ - 20,
    maxX: focus.x + 500,
    maxY: focus.y + 500,
    maxZ: baseZ + 120,
    settlements: Object.freeze([
      settlement(101n, focus.x, focus.y, baseZ, 3, 0, 800, 1001n, 8_000, 4_200),
    ]),
    growthEvents: Object.freeze([]),
    corridors: Object.freeze([]),
    districts: Object.freeze([
      { districtId: 201n, settlementId: 101n, kind: 1, minX: focus.x - 220, minY: focus.y - 220, minZ: baseZ, maxX: focus.x + 220, maxY: focus.y + 220, maxZ: baseZ + 2, nameId: 1002n, accessibility: 1 },
    ]),
    parcels: Object.freeze([
      { parcelId: 301n, settlementId: 101n, districtId: 201n, minX: focus.x - halfParcel, minY: focus.y - halfParcel, minZ: baseZ, maxX: focus.x + halfParcel, maxY: focus.y + halfParcel, maxZ: baseZ + 1, zone: 1, developmentState: 2, developmentSuitability: 1, landValue: 1, buildingId: 401n },
    ]),
    buildings: Object.freeze([
      { buildingId: 401n, parcelId: 301n, use: 1, minX: focus.x - halfBuilding, minY: focus.y - halfBuilding, minZ: baseZ, maxX: focus.x + halfBuilding, maxY: focus.y + halfBuilding, maxZ: baseZ + 72, floors: 18, capacity: 360, historicalStage: 2 },
    ]),
    pois: Object.freeze([
      { poiId: 501n, settlementId: 101n, kind: 1, x: focus.x, y: focus.y, z: baseZ + 72, buildingId: 401n, nameId: 1003n },
    ]),
    toponyms: Object.freeze([
      toponym(1001n, 0, 'Visual Test City', 0n),
      toponym(1002n, 1, 'Grounding District', 1001n),
      toponym(1003n, 5, 'Reference Tower', 1002n),
    ]),
    roadSigns: Object.freeze([]),
    quality: Object.freeze({
      terrainAdaptation: 1,
      roadConnectivity: 1,
      averageSlopeCost: 0.2,
      accessibility: 1,
      congestionRisk: 0.1,
      landUseConsistency: 1,
      floodExposure: 0,
      urbanCompactness: 0.8,
      polycentricBalance: 1,
      overallScore: 0.95,
    }),
  });

  return Object.freeze({ focus, agents, regionalGeneration: regionalGenerationSnapshot });
}

function uniqueTerrainSamples(samples) {
  const unique = new Map();
  for (const sample of samples) {
    const key = `${String(sample.x)}:${String(sample.y)}`;
    if (!unique.has(key)) unique.set(key, sample);
  }
  return [...unique.values()];
}

function squaredDistance(sample, x, y) {
  const dx = sample.x - x;
  const dy = sample.y - y;
  return dx * dx + dy * dy;
}

function settlement(settlementId, x, y, z, role, environmentKind, influenceRadiusMeters, nameId, population, jobs) {
  return Object.freeze({
    settlementId,
    x,
    y,
    z,
    environment: environmentKind,
    origin: 0,
    role,
    initialEconomy: 0,
    suitability: Object.freeze({
      flatness: 1,
      waterAccess: 1,
      transportPotential: 1,
      buildability: 1,
      resourceAccess: 1,
      floodRisk: 0,
      steepSlopeRisk: 0,
      isolation: 0,
      constructionCost: 0,
      totalScore: 1,
    }),
    population,
    jobs,
    influenceRadiusMeters,
    nameId,
  });
}

function toponym(toponymId, kind, name, parentHumanToponymId) {
  return Object.freeze({
    toponymId,
    kind,
    name,
    sourceNaturalToponymId: 0n,
    sourceNaturalName: '',
    sourceFeatureId: 0n,
    parentHumanToponymId,
    generatorKey: 'view-phase03-integrated-visual-e2e',
  });
}

function fail(error) {
  clearTimeout(timeout);
  if (socket.readyState < WebSocket.CLOSING) socket.close();
  settlementRenderer?.dispose();
  view.dispose();
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
}
