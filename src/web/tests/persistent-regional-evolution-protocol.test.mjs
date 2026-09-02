import test from 'node:test';
import assert from 'node:assert/strict';

import {
  BuildingLifecycleStatus,
  PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
  RegionalRelationKind,
  SettlementScale,
  SettlementTrend,
  decodePersistentRegionalEvolutionFrame,
} from '../src/persistent-regional-evolution-protocol.ts';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, ProtocolDecodeFailure } from '../src/protocol.ts';

test('PersistentRegionalEvolution decoder preserves UInt64 IDs and authoritative classification', () => {
  const largeId = 9_007_199_254_740_993n;
  const frame = encodeSnapshot(createPayload(largeId), { major: 2, minor: 19 });

  const envelope = decodePersistentRegionalEvolutionFrame(frame);

  assert.deepEqual(envelope.version, { major: 2, minor: 19 });
  assert.equal(envelope.message.type, PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE);
  assert.equal(envelope.message.tickCount, largeId + 50n);
  assert.equal(envelope.message.settlements[0].settlementId, largeId);
  assert.equal(envelope.message.settlements[0].scale, SettlementScale.City);
  assert.equal(envelope.message.settlements[0].trend, SettlementTrend.Growing);
  assert.equal(envelope.message.buildings[0].status, BuildingLifecycleStatus.Demolished);
  assert.equal(envelope.message.relations[0].kind, RegionalRelationKind.Metro);
  assert.equal(envelope.message.freightFlows[0].commodityId, largeId + 100n);
});

test('PersistentRegionalEvolution decoder rejects Protocol versions older than 2.19', () => {
  const frame = encodeSnapshot(createPayload(100n), { major: 2, minor: 18 });
  assert.throws(() => decodePersistentRegionalEvolutionFrame(frame), ProtocolDecodeFailure);
});

test('PersistentRegionalEvolution decoder rejects broken stable-ID references', () => {
  const payload = createPayload(100n);
  payload.parcels[0].settlementId = '999';
  const frame = encodeSnapshot(payload, { major: 2, minor: 19 });
  assert.throws(() => decodePersistentRegionalEvolutionFrame(frame), /Parcel settlement reference/);
});

function createPayload(baseId) {
  const settlementA = baseId;
  const settlementB = baseId + 1n;
  const parcelId = baseId + 10n;
  const buildingId = baseId + 20n;
  return {
    currentYear: 25,
    tickCount: String(baseId + 50n),
    settlements: [
      { settlementId: String(settlementA), x: 10, y: 20, z: 3, population: 25_000, jobs: 14_000, serviceIndex: 0.8, density: 0.7, accessibility: 0.9, influenceRadiusMeters: 4_200, scale: SettlementScale.City, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null },
      { settlementId: String(settlementB), x: 2_000, y: 1_200, z: 8, population: 500, jobs: 120, serviceIndex: 0.4, density: 0.2, accessibility: 0.35, influenceRadiusMeters: 700, scale: SettlementScale.Village, trend: SettlementTrend.Stable, isActive: true, establishedYear: 0, dormantSinceYear: null },
    ],
    parcels: [
      { parcelId: String(parcelId), settlementId: String(settlementA), developmentDemand: 0.84, landValue: 0.76, developmentState: 3, buildingId: String(buildingId) },
    ],
    buildings: [
      { buildingId: String(buildingId), parcelId: String(parcelId), use: 1, builtYear: -12, lastChangedYear: 25, condition: 0.08, occupancy: 0, capacity: 200, status: BuildingLifecycleStatus.Demolished },
    ],
    serviceCatchments: [
      { settlementId: String(settlementA), kind: 0, radiusMeters: 3_000, coverage: 0.88 },
    ],
    infrastructureDemands: [
      { settlementId: String(settlementA), kind: 1, demand: 0.67, reason: 'density/services/accessibility' },
    ],
    relations: [
      { relationId: String(baseId + 30n), fromSettlementId: String(settlementA), toSettlementId: String(settlementB), kind: RegionalRelationKind.Metro, strength: 0.72, isActive: true, sinceYear: 12 },
    ],
    events: [
      { eventId: String(baseId + 40n), year: 25, kind: 9, settlementId: String(settlementA), buildingId: String(buildingId), reason: 'Abandoned->Demolished' },
    ],
    commutingFlows: [
      { fromSettlementId: String(settlementA), toSettlementId: String(settlementB), workerCount: 80 },
    ],
    freightFlows: [
      { fromSettlementId: String(settlementA), toSettlementId: String(settlementB), commodityId: String(baseId + 100n), quantity: 120, shipmentCount: 3, deliveredQuantity: 115 },
    ],
  };
}

function encodeSnapshot(payload, version) {
  const json = JSON.stringify(payload).replace(/("(?:tickCount|settlementId|parcelId|buildingId|relationId|eventId|fromSettlementId|toSettlementId|commodityId)"\s*:\s*)"(\d+)"/g, '$1$2');
  const bytes = new TextEncoder().encode(json);
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + bytes.byteLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, bytes.byteLength, true);
  new Uint8Array(frame, PROTOCOL_HEADER_SIZE).set(bytes);
  return frame;
}
