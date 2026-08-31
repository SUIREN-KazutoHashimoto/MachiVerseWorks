import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import { decodeEconomyFrame, isEconomyFrame } from '../../src/economy-protocol.ts';

const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 21 E2E harness is invalid.');

const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for Economy snapshot.')), 10_000);
let handshaken = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 10 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 10) throw new Error('Protocol 2.10 handshake failed.');
      handshaken = true;
      return;
    }
    if (!isEconomyFrame(frame)) return;
    const envelope = decodeEconomyFrame(frame);
    const statistics = envelope.message.statistics;
    if (statistics.companyCount < 1 || statistics.establishmentCount < 1 || statistics.jobCount < 1 || statistics.employedPersonCount < 1) throw new Error(`Economy fixture is incomplete: ${JSON.stringify({ companies: statistics.companyCount, establishments: statistics.establishmentCount, jobs: statistics.jobCount, employed: statistics.employedPersonCount })}`);
    if (envelope.message.companies.length < 1 || envelope.message.households.length < 1) throw new Error('Economy debug entries were not published.');
    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.textContent = `Phase 21 E2E passed: companies=${statistics.companyCount}, employed=${statistics.employedPersonCount}, vacancies=${statistics.vacantPositionCount}`;
  } catch (error) {
    fail(error);
  }
});
socket.addEventListener('error', () => fail(new Error('WebSocket transport failed.')));

function fail(error) {
  clearTimeout(timeout);
  if (socket.readyState < WebSocket.CLOSING) socket.close();
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
}
