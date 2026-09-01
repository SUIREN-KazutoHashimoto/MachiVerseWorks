import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import {
  decodePowerFrame,
  GeneratorOperatingState,
  isPowerFrame,
  PowerSupplyState,
} from '../../src/power-protocol.ts';

const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 23 E2E harness is invalid.');

const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for Power demand, outage, and recovery.')), 60_000);
let handshaken = false;
let initialDemand = null;
let sawDemandChange = false;
let sawOnline = false;
let sawOutage = false;
let sawRecovered = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 12 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 12) throw new Error('Protocol 2.12 handshake failed.');
      handshaken = true;
      return;
    }
    if (!isPowerFrame(frame)) return;
    const { message } = decodePowerFrame(frame);
    if (message.statistics.demandMegawatts <= 0) return;

    if (initialDemand === null) initialDemand = message.statistics.demandMegawatts;
    else if (Math.abs(message.statistics.demandMegawatts - initialDemand) > 1e-9) sawDemandChange = true;

    const hasOnlineGenerator = message.generators.some((generator) => generator.operatingState === GeneratorOperatingState.Online);
    const hasOfflineGenerator = message.generators.some((generator) => generator.operatingState === GeneratorOperatingState.Offline);
    const hasOutageLoad = message.loads.some((load) => load.supplyState === PowerSupplyState.Outage && load.unservedMegawatts > 0);
    const hasPoweredLoad = message.loads.some((load) => load.supplyState !== PowerSupplyState.Outage && load.servedMegawatts > 0);

    if (hasOnlineGenerator && hasPoweredLoad && message.statistics.servedMegawatts > 0) sawOnline = true;
    if (hasOfflineGenerator && hasOutageLoad && message.statistics.outageLoadCount > 0 && message.statistics.unservedMegawatts > 0) sawOutage = true;
    if (sawOutage && hasOnlineGenerator && hasPoweredLoad && message.statistics.outageLoadCount === 0 && message.statistics.unservedMegawatts <= 1e-9) sawRecovered = true;

    if (!sawDemandChange || !sawOnline || !sawOutage || !sawRecovered) return;
    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.textContent = `Phase 23 E2E passed: demand=${message.statistics.demandMegawatts.toFixed(2)}MW, served=${message.statistics.servedMegawatts.toFixed(2)}MW, recoveryTick=${message.statistics.tickCount.toString()}`;
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
