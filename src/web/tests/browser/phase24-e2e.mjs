import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import {
  decodeWaterSewerFrame,
  isWaterSewerFrame,
  SewerServiceState,
  UtilityFacilityKind,
  UtilityOperatingState,
  WaterServiceState,
} from '../../src/water-sewer-protocol.ts';

const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 24 E2E harness is invalid.');

const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for Water/Sewer demand, outage, cut, and recovery transitions.')), 60_000);
let handshaken = false;
let initialDemand = null;
let sawDemandChange = false;
let sawNormal = false;
let sawTreatmentOutage = false;
let sawTreatmentRecovery = false;
let sawWaterCut = false;
let sawWaterRecovery = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 13 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 13) throw new Error('Protocol 2.13 handshake failed.');
      handshaken = true;
      return;
    }
    if (!isWaterSewerFrame(frame)) return;
    const { message } = decodeWaterSewerFrame(frame);
    if (message.statistics.waterDemandCubicMetersPerDay <= 0 || message.servicePoints.length === 0) return;

    if (initialDemand === null) initialDemand = message.statistics.waterDemandCubicMetersPerDay;
    else if (Math.abs(message.statistics.waterDemandCubicMetersPerDay - initialDemand) > 1e-6) sawDemandChange = true;

    const treatment = message.facilities.find((facility) => facility.kind === UtilityFacilityKind.SewageTreatmentPlant);
    const anyWaterPipeCut = message.pipes.some((pipe) => pipe.networkKind === 0 && !pipe.isInService);
    const allWaterPipesOnline = message.pipes.filter((pipe) => pipe.networkKind === 0).every((pipe) => pipe.isInService);
    const hasWaterService = message.servicePoints.some((point) => point.waterState === WaterServiceState.Supplied && point.waterServedCubicMetersPerDay > 0);
    const hasWaterOutage = message.servicePoints.some((point) => point.waterState === WaterServiceState.Unavailable && point.waterUnservedCubicMetersPerDay > 0);
    const hasSewerFault = message.servicePoints.some((point) => (point.sewerState === SewerServiceState.Unavailable || point.sewerState === SewerServiceState.Overflow) && point.wastewaterOverflowCubicMetersPerDay > 0);
    const sewerHealthy = message.servicePoints.every((point) => point.sewerState === SewerServiceState.Available && point.wastewaterOverflowCubicMetersPerDay <= 1e-9);

    if (treatment?.operatingState === UtilityOperatingState.Online && hasWaterService && sewerHealthy) sawNormal = true;
    if (treatment?.operatingState === UtilityOperatingState.Offline && hasSewerFault && message.statistics.wastewaterOverflowCubicMetersPerDay > 0) sawTreatmentOutage = true;
    if (sawTreatmentOutage && treatment?.operatingState === UtilityOperatingState.Online && sewerHealthy) sawTreatmentRecovery = true;
    if (anyWaterPipeCut && hasWaterOutage && message.statistics.waterUnavailableCount > 0) sawWaterCut = true;
    if (sawWaterCut && allWaterPipesOnline && hasWaterService && message.statistics.waterUnavailableCount === 0) sawWaterRecovery = true;

    if (!sawDemandChange || !sawNormal || !sawTreatmentOutage || !sawTreatmentRecovery || !sawWaterCut || !sawWaterRecovery) return;
    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.textContent = `Phase 24 E2E passed: demand=${message.statistics.waterDemandCubicMetersPerDay.toFixed(2)}m3/day, served=${message.statistics.waterServedCubicMetersPerDay.toFixed(2)}m3/day, recoveryTick=${message.statistics.tickCount.toString()}`;
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
