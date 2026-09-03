import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import { decodeRadioFrame, isRadioFrame, RADIO_SNAPSHOT_MESSAGE_TYPE, SPECTRUM_SNAPSHOT_MESSAGE_TYPE } from '../../src/radio-protocol.ts';
import { RadioDebugOverlay } from '../../src/radio-debug.ts';

const result = document.querySelector('#result');
const host = document.querySelector('#host');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || !(host instanceof HTMLElement) || server === null) throw new Error('Phase 28 E2E harness is invalid.');

const overlay = new RadioDebugOverlay(host);
const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for Radio/Spectrum entities, obstruction, interference, Power outage, backhaul outage, recovery, and debug rendering.')), 60_000);
let handshaken = false;
let latestRadio = null;
let latestSpectrum = null;
let sawExplicitEntities = false;
let sawMultipleFrequencies = false;
let sawObstruction = false;
let sawInterference = false;
let sawConflict = false;
let sawPowerOutage = false;
let sawBackhaulOutage = false;
let sawRecovery = false;
let sawDebugRender = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 16 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 16) throw new Error('Protocol 2.16 handshake failed.');
      handshaken = true;
      return;
    }
    if (!isRadioFrame(frame)) return;
    const { message } = decodeRadioFrame(frame);
    if (message.type === SPECTRUM_SNAPSHOT_MESSAGE_TYPE) {
      latestSpectrum = message;
      overlay.applySpectrum(message);
      if (message.conflicts.length > 0) sawConflict = true;
    } else if (message.type === RADIO_SNAPSHOT_MESSAGE_TYPE) {
      latestRadio = message;
      overlay.applyRadio(message);
      if (message.sites.length >= 4 && message.antennas.length >= 4 && message.transmitters.length >= 2 && message.receivers.length >= 2 && message.emissions.length >= 3 && message.links.length >= 3) sawExplicitEntities = true;
      if (new Set(message.emissions.map((emission) => emission.centerFrequencyMegahertz.toFixed(3))).size >= 3) sawMultipleFrequencies = true;
      if (message.links.some((link) => link.pathLossDb > 115)) sawObstruction = true;
      if (message.links.some((link) => link.interferenceDbm > -250 && Number.isFinite(link.sinrDb))) sawInterference = true;

      const downCount = message.emissions.filter((emission) => !emission.isOperational).length;
      if (downCount >= 2) sawPowerOutage = true;
      if (downCount === 1) sawBackhaulOutage = true;
      if (sawPowerOutage && sawBackhaulOutage && downCount === 0) sawRecovery = true;

      const debug = host.querySelector('[data-radio-debug="true"]');
      const svg = debug?.querySelector('svg');
      if (debug instanceof HTMLElement && svg instanceof SVGElement && svg.children.length > 0) sawDebugRender = true;
    }

    if (!sawExplicitEntities || !sawMultipleFrequencies || !sawObstruction || !sawInterference || !sawConflict || !sawPowerOutage || !sawBackhaulOutage || !sawRecovery || !sawDebugRender || latestRadio === null || latestSpectrum === null) return;
    clearTimeout(timeout);
    socket.close(1000, 'done');
    overlay.dispose();
    result.dataset.status = 'passed';
    result.textContent = `Phase 28 E2E passed: tick=${latestRadio.statistics.tickCount.toString()}, sites=${latestRadio.sites.length}, emissions=${latestRadio.emissions.length}, links=${latestRadio.links.length}, conflicts=${latestSpectrum.conflicts.length}, powerOutage=${String(sawPowerOutage)}, backhaulOutage=${String(sawBackhaulOutage)}`;
  } catch (error) {
    fail(error);
  }
});
socket.addEventListener('error', () => fail(new Error('WebSocket transport failed.')));

function fail(error) {
  clearTimeout(timeout);
  if (socket.readyState < WebSocket.CLOSING) socket.close();
  overlay.dispose();
  const normalized = error instanceof Error ? error : new Error(String(error));
  result.dataset.status = 'failed';
  result.textContent = normalized.stack ?? normalized.message;
}
