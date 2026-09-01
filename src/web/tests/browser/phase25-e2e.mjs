import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import {
  decodeGasFrame,
  GasDeliveryMode,
  GasServiceState,
  isGasFrame,
} from '../../src/gas-protocol.ts';

const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 25 E2E harness is invalid.');

const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for Gas pipeline outage/recovery and delivered-gas stockout/replenishment.')), 60_000);
let handshaken = false;
let sawPipedHealthy = false;
let sawPipedOutage = false;
let sawPipedRecovery = false;
let sawDeliveredHealthy = false;
let sawDeliveredStockout = false;
let sawDeliveredRecovery = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 14 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 14) throw new Error('Protocol 2.14 handshake failed.');
      handshaken = true;
      return;
    }
    if (!isGasFrame(frame)) return;
    const { message } = decodeGasFrame(frame);
    const piped = message.servicePoints.find((point) => point.deliveryMode === GasDeliveryMode.Piped);
    const delivered = message.servicePoints.find((point) => point.deliveryMode === GasDeliveryMode.Delivered);
    if (piped === undefined || delivered === undefined || message.pipelines.length === 0) return;

    const pipeOnline = message.pipelines.every((pipe) => pipe.isInService);
    const pipeCut = message.pipelines.some((pipe) => !pipe.isInService);
    if (pipeOnline && piped.serviceState === GasServiceState.Supplied) sawPipedHealthy = true;
    if (sawPipedHealthy && pipeCut && piped.serviceState === GasServiceState.Unavailable && piped.unservedCubicMetersPerDay > 0) sawPipedOutage = true;
    if (sawPipedOutage && pipeOnline && piped.serviceState === GasServiceState.Supplied) sawPipedRecovery = true;

    if (delivered.commodityId !== 0n && delivered.serviceState === GasServiceState.Supplied && delivered.servedCubicMetersPerDay > 0) {
      if (sawDeliveredStockout) sawDeliveredRecovery = true;
      else sawDeliveredHealthy = true;
    }
    if (sawDeliveredHealthy && delivered.serviceState === GasServiceState.Unavailable && delivered.unservedCubicMetersPerDay > 0) sawDeliveredStockout = true;

    if (!sawPipedHealthy || !sawPipedOutage || !sawPipedRecovery || !sawDeliveredHealthy || !sawDeliveredStockout || !sawDeliveredRecovery) return;
    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.textContent = `Phase 25 E2E passed: tick=${message.statistics.tickCount.toString()}, served=${message.statistics.servedCubicMetersPerDay.toFixed(2)}m3/day, unserved=${message.statistics.unservedCubicMetersPerDay.toFixed(2)}m3/day`;
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
