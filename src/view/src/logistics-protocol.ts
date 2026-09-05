import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

export const LOGISTICS_SNAPSHOT_MESSAGE_TYPE = 740;
const FIXED_PAYLOAD_LENGTH = 68;
const INVENTORY_PAYLOAD_LENGTH = 32;
const SHIPMENT_PAYLOAD_LENGTH = 65;

export enum ShipmentState {
  Pickup = 0,
  Loading = 1,
  InTransit = 2,
  Unloading = 3,
  Delivered = 4,
}

export interface LogisticsStatistics {
  readonly commodityCount: number;
  readonly inventoryCount: number;
  readonly openOrderCount: number;
  readonly shipmentCount: number;
  readonly inTransitShipmentCount: number;
  readonly delayedShipmentCount: number;
  readonly inventoryUnits: number;
  readonly inTransitUnits: number;
  readonly deliveredShipmentCount: bigint;
  readonly logisticsCycle: bigint;
  readonly tickCount: bigint;
}

export interface LogisticsInventory {
  readonly establishmentId: bigint;
  readonly commodityId: bigint;
  readonly quantity: number;
  readonly capacity: number;
}

export interface LogisticsShipment {
  readonly shipmentId: bigint;
  readonly orderId: bigint;
  readonly sourceEstablishmentId: bigint;
  readonly destinationEstablishmentId: bigint;
  readonly commodityId: bigint;
  readonly quantity: number;
  readonly state: ShipmentState;
  readonly vehicleId: bigint;
  readonly delayTicks: bigint;
}

export interface LogisticsSnapshotMessage {
  readonly type: typeof LOGISTICS_SNAPSHOT_MESSAGE_TYPE;
  readonly statistics: LogisticsStatistics;
  readonly inventories: readonly LogisticsInventory[];
  readonly shipments: readonly LogisticsShipment[];
}

export interface LogisticsProtocolEnvelope {
  readonly version: ProtocolVersion;
  readonly message: LogisticsSnapshotMessage;
}

export type LogisticsProtocolMessage = LogisticsSnapshotMessage;

export function isLogisticsFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === LOGISTICS_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeLogisticsFrame(frame: ArrayBuffer): LogisticsProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Logistics frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Logistics frame magic is invalid.');
  const version: ProtocolVersion = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 11) throw new ProtocolDecodeFailure('Logistics snapshots require Protocol 2.11 or newer.');
  if (view.getUint16(8, true) !== LOGISTICS_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Frame is not a Logistics snapshot.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Logistics frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Logistics frame length is invalid.');
  if (payloadLength < FIXED_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Logistics payload is too short.');

  const offset = PROTOCOL_HEADER_SIZE;
  const inventoryCount = view.getUint16(offset + 64, true);
  const shipmentCount = view.getUint16(offset + 66, true);
  const expectedLength = FIXED_PAYLOAD_LENGTH + inventoryCount * INVENTORY_PAYLOAD_LENGTH + shipmentCount * SHIPMENT_PAYLOAD_LENGTH;
  if (payloadLength !== expectedLength) throw new ProtocolDecodeFailure('Logistics payload counts do not match its length.');

  const statistics: LogisticsStatistics = {
    commodityCount: view.getUint32(offset, true),
    inventoryCount: view.getUint32(offset + 4, true),
    openOrderCount: view.getUint32(offset + 8, true),
    shipmentCount: view.getUint32(offset + 12, true),
    inTransitShipmentCount: view.getUint32(offset + 16, true),
    delayedShipmentCount: view.getUint32(offset + 20, true),
    inventoryUnits: view.getFloat64(offset + 24, true),
    inTransitUnits: view.getFloat64(offset + 32, true),
    deliveredShipmentCount: view.getBigUint64(offset + 40, true),
    logisticsCycle: view.getBigUint64(offset + 48, true),
    tickCount: view.getBigUint64(offset + 56, true),
  };
  if (!Number.isFinite(statistics.inventoryUnits) || statistics.inventoryUnits < 0 || !Number.isFinite(statistics.inTransitUnits) || statistics.inTransitUnits < 0) throw new ProtocolDecodeFailure('Logistics statistics contain invalid values.');

  let cursor = offset + FIXED_PAYLOAD_LENGTH;
  const inventories: LogisticsInventory[] = [];
  for (let index = 0; index < inventoryCount; index += 1) {
    const entry: LogisticsInventory = {
      establishmentId: view.getBigUint64(cursor, true),
      commodityId: view.getBigUint64(cursor + 8, true),
      quantity: view.getFloat64(cursor + 16, true),
      capacity: view.getFloat64(cursor + 24, true),
    };
    if (entry.establishmentId === 0n || entry.commodityId === 0n || !Number.isFinite(entry.quantity) || entry.quantity < 0 || !Number.isFinite(entry.capacity) || entry.capacity <= 0 || entry.quantity > entry.capacity) throw new ProtocolDecodeFailure('Logistics inventory entry is invalid.');
    inventories.push(entry);
    cursor += INVENTORY_PAYLOAD_LENGTH;
  }

  const shipments: LogisticsShipment[] = [];
  for (let index = 0; index < shipmentCount; index += 1) {
    const entry: LogisticsShipment = {
      shipmentId: view.getBigUint64(cursor, true),
      orderId: view.getBigUint64(cursor + 8, true),
      sourceEstablishmentId: view.getBigUint64(cursor + 16, true),
      destinationEstablishmentId: view.getBigUint64(cursor + 24, true),
      commodityId: view.getBigUint64(cursor + 32, true),
      quantity: view.getFloat64(cursor + 40, true),
      state: view.getUint8(cursor + 48) as ShipmentState,
      vehicleId: view.getBigUint64(cursor + 49, true),
      delayTicks: view.getBigUint64(cursor + 57, true),
    };
    if (entry.shipmentId === 0n || entry.orderId === 0n || entry.sourceEstablishmentId === 0n || entry.destinationEstablishmentId === 0n || entry.commodityId === 0n || !Number.isFinite(entry.quantity) || entry.quantity <= 0 || entry.state < ShipmentState.Pickup || entry.state > ShipmentState.Delivered) throw new ProtocolDecodeFailure('Logistics shipment entry is invalid.');
    shipments.push(entry);
    cursor += SHIPMENT_PAYLOAD_LENGTH;
  }

  return { version, message: { type: LOGISTICS_SNAPSHOT_MESSAGE_TYPE, statistics, inventories, shipments } };
}
