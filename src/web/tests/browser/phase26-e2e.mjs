import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import { decodeOpticalFrame, isOpticalFrame } from '../../src/optical-protocol.ts';

const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 26 E2E harness is invalid.');

const socket = new WebSocket(server); socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for optical congestion, reroute, outage, and recovery.')), 60_000);
let handshaken = false;
let sawConnected = false;
let sawCongestion = false;
let sawReroute = false;
let sawOutage = false;
let sawRecovery = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 15 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 15) throw new Error('Protocol 2.15 handshake failed.');
      handshaken = true; return;
    }
    if (!isOpticalFrame(frame)) return;
    const { message } = decodeOpticalFrame(frame);
    if (message.demands.length < 2 || message.fiberCables.length < 4 || message.backhauls.length === 0) return;
    const connected = message.statistics.connectedDemandCount > 0 && message.statistics.allocatedGigabitsPerSecond > 0;
    if (connected) sawConnected = true;
    if (message.statistics.peakFiberUtilization >= 0.85 || message.statistics.congestedDemandCount > 0 || message.fiberCables.some((cable) => cable.isCongested)) sawCongestion = true;
    const primaryCut = !message.fiberCables[0].isInService;
    const alternateLoaded = message.fiberCables.slice(2).some((cable) => cable.isInService && cable.loadGigabitsPerSecond > 0);
    if (sawConnected && primaryCut && alternateLoaded && connected) sawReroute = true;
    if (sawReroute && message.statistics.unavailableDemandCount > 0 && message.statistics.allocatedGigabitsPerSecond <= 1e-9) sawOutage = true;
    if (sawOutage && message.fiberCables.every((cable) => cable.isInService) && connected) sawRecovery = true;
    if (!sawConnected || !sawCongestion || !sawReroute || !sawOutage || !sawRecovery) return;
    clearTimeout(timeout); socket.close(1000, 'done'); result.dataset.status = 'passed';
    result.textContent = `Phase 26 E2E passed: tick=${message.statistics.tickCount.toString()}, allocated=${message.statistics.allocatedGigabitsPerSecond.toFixed(2)}Gbps, peak=${(message.statistics.peakFiberUtilization * 100).toFixed(1)}%`;
  } catch (error) { fail(error); }
});
socket.addEventListener('error', () => fail(new Error('WebSocket transport failed.')));
function fail(error) { clearTimeout(timeout); if (socket.readyState < WebSocket.CLOSING) socket.close(); const normalized = error instanceof Error ? error : new Error(String(error)); result.dataset.status = 'failed'; result.textContent = normalized.stack ?? normalized.message; }
