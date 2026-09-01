import { MachiVerseConnection } from '../../src/connection.ts';
import { initializeLocalization } from '../../src/localization.ts';
import {
  PopulationMessageType,
  decodePopulationFrame,
  encodeInspectPerson,
  isPopulationFrame,
} from '../../src/population-protocol.ts';
import { MessageType, decodeFrame, encodeHello } from '../../src/protocol.ts';
import { ClientUi } from '../../src/ui.ts';

const parameters = new URLSearchParams(window.location.search);
const serverUrl = parameters.get('server') ?? 'ws://127.0.0.1:5082/ws';
const host = document.querySelector('#host');
const result = document.querySelector('#result');
if (!(host instanceof HTMLElement) || !(result instanceof HTMLElement)) throw new Error('Population E2E host elements are missing.');

const ui = new ClientUi(host, initializeLocalization());
let connectionState = 'disconnected';
let protocolVersion = null;
let populationStatistics = null;
let personDebug = null;
let personDebugCount = 0;
let protocolError = null;
let clientError = null;

const connection = new MachiVerseConnection(serverUrl, { minimumDelayMs: 100, maximumDelayMs: 500 }, {
  onStateChanged: (state) => { connectionState = state; ui.setConnectionState(state); },
  onMessage: (message) => {
    if (message.type === PopulationMessageType.PopulationStatistics) {
      populationStatistics = message;
      ui.setPopulationStatistics(message);
    } else if (message.type === PopulationMessageType.PersonDebug) {
      personDebug = message;
      personDebugCount += 1;
      ui.setPersonDebug(message);
    }
  },
  onProtocolError: (message) => { protocolError = new Error(`Protocol error ${String(message.code)}.`); },
  onClientError: (error) => { clientError = error; },
  onDisconnected: () => { ui.clearPopulation(); ui.setProtocol(null); },
  onHelloAck: (version) => { protocolVersion = version; ui.setProtocol(version); },
});
ui.onInspectPerson((personId) => connection.inspectPerson(personId));
ui.onClearPersonInspection(() => connection.clearPersonInspection());

try {
  connection.connect();
  await waitUntil(() => connectionState === 'connected', 'Protocol connection');
  assert(protocolVersion?.major === 2 && protocolVersion?.minor === 12, 'current Browser connection negotiates Protocol 2.12');
  await waitUntil(() => populationStatistics !== null, 'PopulationStatistics');
  assert(populationStatistics.householdCount === 1, 'PopulationStatistics contains one Household');
  assert(populationStatistics.personCount === 1, 'PopulationStatistics contains one Person');
  assert(readPopulationText().includes('人口 1'), 'PopulationStatistics is rendered in ClientUi');

  const personInput = host.querySelector('.person-debug-controls input');
  const inspectorButtons = [...host.querySelectorAll('.person-debug-controls button')];
  assert(personInput instanceof HTMLInputElement, 'Person inspector input exists');
  assert(inspectorButtons.length >= 2, 'Person inspector show/clear controls exist');
  personInput.value = '1';
  inspectorButtons[0].click();
  await waitUntil(() => personDebug?.personId === 1n, 'PersonDebug for Person 1');
  assert(readPersonDebugText().includes('Person 1'), 'PersonDebug is rendered in ClientUi');
  assert(readPersonDebugText().includes('Household 1'), 'PersonDebug renders Household field');
  assert(readPersonDebugText().includes('居住 Building 1'), 'PersonDebug renders residence field');
  assert(readPersonDebugText().includes('Activity 自宅'), 'PersonDebug renders activity field');
  assert(readPersonDebugText().includes('移動 活動中'), 'PersonDebug renders travel-state field');

  const firstDebugCount = personDebugCount;
  connection.disconnect();
  await waitUntil(() => connectionState === 'disconnected', 'client disconnect');
  personDebug = null;
  connection.connect();
  await waitUntil(() => connectionState === 'connected', 'client reconnect');
  await waitUntil(() => personDebug?.personId === 1n && personDebugCount > firstDebugCount, 'Person inspection restoration after reconnect');
  assert(readPersonDebugText().includes('Person 1'), 'Person inspector UI is restored after reconnect');

  const compatibility = await verifyProtocol25Compatibility(serverUrl);
  assert(compatibility.personId === 1n, 'Protocol 2.5 InspectPerson receives PersonDebug');

  result.dataset.status = 'passed';
  result.textContent = JSON.stringify({
    status: 'passed',
    negotiatedProtocol: `${String(protocolVersion.major)}.${String(protocolVersion.minor)}`,
    households: populationStatistics.householdCount,
    persons: populationStatistics.personCount,
    personDebugCount,
    protocol25PersonId: compatibility.personId.toString(),
  });
} catch (error) {
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = JSON.stringify({ status: 'failed', message: normalized.message });
  console.error(normalized);
} finally {
  connection.disconnect();
  ui.dispose();
}

async function verifyProtocol25Compatibility(url) {
  const version25 = Object.freeze({ major: 2, minor: 5 });
  const socket = new WebSocket(url);
  socket.binaryType = 'arraybuffer';
  await new Promise((resolve, reject) => {
    socket.addEventListener('open', resolve, { once: true });
    socket.addEventListener('error', () => reject(new Error('Protocol 2.5 WebSocket connection failed.')), { once: true });
  });
  socket.send(encodeHello(version25));
  const helloAck = await receiveMatching(socket, (buffer) => {
    if (isPopulationFrame(buffer)) return null;
    const envelope = decodeFrame(buffer);
    return envelope.message.type === MessageType.HelloAck ? envelope : null;
  });
  assert(helloAck.version.major === 2 && helloAck.version.minor === 5, 'Server accepts Protocol 2.5 Hello');
  socket.send(encodeInspectPerson(1n, version25));
  const person = await receiveMatching(socket, (buffer) => {
    if (!isPopulationFrame(buffer)) return null;
    const envelope = decodePopulationFrame(buffer);
    return envelope.message.type === PopulationMessageType.PersonDebug ? envelope.message : null;
  });
  socket.close(1000, 'Protocol 2.5 compatibility verified');
  return person;
}

async function receiveMatching(socket, decoder, timeoutMs = 10_000) {
  const deadline = performance.now() + timeoutMs;
  while (performance.now() < deadline) {
    const remaining = Math.max(1, deadline - performance.now());
    const data = await new Promise((resolve, reject) => {
      const timer = window.setTimeout(() => { cleanup(); reject(new Error('Timed out waiting for WebSocket frame.')); }, remaining);
      const onMessage = (event) => { cleanup(); resolve(event.data); };
      const onClose = () => { cleanup(); reject(new Error('WebSocket closed before expected frame.')); };
      const cleanup = () => { window.clearTimeout(timer); socket.removeEventListener('message', onMessage); socket.removeEventListener('close', onClose); };
      socket.addEventListener('message', onMessage);
      socket.addEventListener('close', onClose);
    });
    const buffer = await toArrayBuffer(data);
    const decoded = decoder(buffer);
    if (decoded !== null) return decoded;
  }
  throw new Error('Timed out waiting for matching WebSocket frame.');
}

function readPopulationText() {
  const rows = [...host.querySelectorAll('.status-row')];
  const populationRow = rows.find((row) => row.querySelector('.status-label')?.textContent === '人口');
  return populationRow?.querySelector('.status-value')?.textContent ?? '';
}

function readPersonDebugText() {
  return host.querySelector('.person-debug-value')?.textContent ?? '';
}

async function waitUntil(predicate, description, timeoutMs = 10_000) {
  const deadline = performance.now() + timeoutMs;
  while (!predicate()) {
    throwIfConnectionFailed();
    if (performance.now() >= deadline) throw new Error(`Timed out waiting for ${description}.`);
    await new Promise((resolve) => window.setTimeout(resolve, 50));
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

async function toArrayBuffer(data) {
  if (data instanceof ArrayBuffer) return data;
  if (data instanceof Blob) return data.arrayBuffer();
  if (ArrayBuffer.isView(data)) return data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength);
  throw new Error('WebSocket frame must be binary.');
}
