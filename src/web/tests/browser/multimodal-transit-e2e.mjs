import { MachiVerseConnection } from '../../src/connection.ts';
import { initializeLocalization } from '../../src/localization.ts';
import { MultimodalTransitMessageType, TransitMode, TransitVehicleKind } from '../../src/multimodal-transit.ts';
import { ClientUi } from '../../src/ui.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5080/ws';
const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('Multimodal transit E2E host elements are missing.');

const ui = new ClientUi(host, initializeLocalization());
let state = 'disconnected';
let protocolError = null;
let clientError = null;
let negotiatedVersion = null;
let snapshot = null;
let observedBusMovement = false;
let observedTaxiMovement = false;
const firstPositions = new Map();

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (nextState) => { state = nextState; ui.setConnectionState(nextState); },
  onMessage: (message) => {
    if (message.type !== MultimodalTransitMessageType.MultimodalTransitSnapshot) return;
    snapshot = message;
    ui.setMultimodalTransit(message);
    for (const vehicle of message.vehicles) {
      const initial = firstPositions.get(vehicle.id);
      if (initial === undefined) firstPositions.set(vehicle.id, [vehicle.x, vehicle.y, vehicle.z]);
      else if (Math.hypot(vehicle.x - initial[0], vehicle.y - initial[1], vehicle.z - initial[2]) > 0.5) {
        if (vehicle.kind === TransitVehicleKind.Bus) observedBusMovement = true;
        if (vehicle.kind === TransitVehicleKind.Taxi) observedTaxiMovement = true;
      }
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => { ui.clearMultimodalTransit(); },
  onHelloAck: (version) => { negotiatedVersion = version; ui.setProtocol(version); },
});

try {
  connection.connect();
  await waitUntil(() => state === 'connected', 'Protocol 2.8 connection');
  connection.setSubscription({ minX: -120, minY: -40, minZ: -10, maxX: 120, maxY: 60, maxZ: 15 });
  await waitUntil(() => snapshot !== null
    && snapshot.lines.some((line) => line.mode === TransitMode.Bus)
    && snapshot.lines.some((line) => line.mode === TransitMode.Railway)
    && snapshot.vehicles.some((vehicle) => vehicle.kind === TransitVehicleKind.Bus)
    && snapshot.vehicles.some((vehicle) => vehicle.kind === TransitVehicleKind.Taxi)
    && snapshot.arrivalEstimates.length > 0,
  'Railway, Bus, Taxi, and arrival snapshot');
  await waitUntil(() => observedBusMovement || observedTaxiMovement, 'Road Traffic backed Bus or Taxi movement', 45_000);

  const railwayPattern = snapshot.patterns.find((pattern) => pattern.railwayServiceId !== null);
  const transitDebug = host.querySelector('.transit-debug-value');
  assert(negotiatedVersion?.major === 2 && negotiatedVersion?.minor === 8, 'Protocol 2.8 was negotiated');
  assert(railwayPattern?.stops.length === 2, 'Railway service is exposed through the common Transit pattern');
  assert(snapshot.patterns.some((pattern) => pattern.railwayServiceId === null && pattern.stops.length === 2), 'Bus pattern is published');
  assert(snapshot.vehicles.some((vehicle) => vehicle.kind === TransitVehicleKind.Bus && vehicle.roadVehicleId !== null), 'Bus reuses a Road Traffic vehicle');
  assert(snapshot.vehicles.some((vehicle) => vehicle.kind === TransitVehicleKind.Taxi), 'Taxi vehicle is published');
  assert(snapshot.arrivalEstimates.some((arrival) => arrival.estimatedArrivalTick >= snapshot.tickCount), 'Arrival estimate is published');
  assert(transitDebug instanceof HTMLElement && transitDebug.textContent.includes('Bus') && transitDebug.textContent.includes('Railway'), 'Transit route/stop/vehicle/arrival debug UI was updated');

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    protocol: negotiatedVersion,
    lines: snapshot.lines.length,
    stops: snapshot.stops.length,
    patterns: snapshot.patterns.length,
    vehicles: snapshot.vehicles.length,
    arrivals: snapshot.arrivalEstimates.length,
    observedBusMovement,
    observedTaxiMovement,
    debug: transitDebug.textContent,
  });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  connection.disconnect();
}

async function waitUntil(predicate, description, timeoutMs = 30_000) {
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
