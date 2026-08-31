import { decodeFrame, encodeHello, MessageType } from '../../src/protocol.ts';
import { decodeLogisticsFrame, isLogisticsFrame, ShipmentState } from '../../src/logistics-protocol.ts';

const result = document.querySelector('#result');
const server = new URLSearchParams(location.search).get('server');
if (!(result instanceof HTMLElement) || server === null) throw new Error('Phase 22 E2E harness is invalid.');

const socket = new WebSocket(server);
socket.binaryType = 'arraybuffer';
const timeout = window.setTimeout(() => fail(new Error('Timed out waiting for delivered Shipment.')), 60_000);
let handshaken = false;

socket.addEventListener('open', () => socket.send(encodeHello({ major: 2, minor: 11 })));
socket.addEventListener('message', async (event) => {
  try {
    const frame = event.data instanceof ArrayBuffer ? event.data : await event.data.arrayBuffer();
    if (!handshaken) {
      const envelope = decodeFrame(frame);
      if (envelope.message.type !== MessageType.HelloAck || envelope.version.major !== 2 || envelope.version.minor !== 11) throw new Error('Protocol 2.11 handshake failed.');
      handshaken = true;
      return;
    }
    if (!isLogisticsFrame(frame)) return;
    const { message } = decodeLogisticsFrame(frame);
    const delivered = message.shipments.find((shipment) => shipment.state === ShipmentState.Delivered);
    if (delivered === undefined || message.statistics.deliveredShipmentCount < 1n) return;
    const destination = message.inventories.find((inventory) => inventory.establishmentId === delivered.destinationEstablishmentId && inventory.commodityId === delivered.commodityId);
    if (destination === undefined || destination.quantity <= 0) throw new Error('Delivered Shipment did not replenish destination inventory.');
    if (delivered.vehicleId === 0n) throw new Error('Delivered Shipment has no Freight Vehicle ID.');
    clearTimeout(timeout);
    socket.close(1000, 'done');
    result.dataset.status = 'passed';
    result.textContent = `Phase 22 E2E passed: shipment=${delivered.shipmentId.toString()}, vehicle=${delivered.vehicleId.toString()}, inventory=${destination.quantity.toFixed(1)}`;
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
