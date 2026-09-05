import test from 'node:test';
import assert from 'node:assert/strict';

import {
  BuildingLifecycleStatus,
  PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
  SettlementScale,
  SettlementTrend,
  decodePersistentRegionalEvolutionFrame,
} from '../src/persistent-regional-evolution-protocol.ts';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, ProtocolDecodeFailure } from '../src/protocol.ts';

test('PersistentRegionalEvolution decoder preserves UInt64 IDs, classification, and full-snapshot flag', () => {
  const largeId = 9_007_199_254_740_993n;
  const payload = basePayload(largeId, true);
  payload.settlements.push({ settlementId: String(largeId), x: 10, y: 20, z: 3, population: 25_000, jobs: 14_000, serviceIndex: 0.8, density: 0.7, accessibility: 0.9, influenceRadiusMeters: 4_200, scale: SettlementScale.City, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null });
  const envelope = decodePersistentRegionalEvolutionFrame(encodeSnapshot(payload, { major: 2, minor: 19 }));

  assert.deepEqual(envelope.version, { major: 2, minor: 19 });
  assert.equal(envelope.message.type, PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE);
  assert.equal(envelope.message.isFullSnapshot, true);
  assert.equal(envelope.message.tickCount, largeId + 50n);
  assert.equal(envelope.message.settlements[0].settlementId, largeId);
  assert.equal(envelope.message.settlements[0].scale, SettlementScale.City);
});

test('PersistentRegionalEvolution decoder accepts continuation chunks whose references resolve in earlier chunks', () => {
  const payload = basePayload(100n, false);
  payload.parcels.push({ parcelId: '201', settlementId: '101', developmentDemand: 0.8, landValue: 0.7, developmentState: 3, buildingId: '301' });
  payload.buildings.push({ buildingId: '301', parcelId: '201', use: 1, builtYear: 0, lastChangedYear: 25, condition: 0.2, occupancy: 0.1, capacity: 100, status: BuildingLifecycleStatus.Vacant });

  const envelope = decodePersistentRegionalEvolutionFrame(encodeSnapshot(payload, { major: 2, minor: 19 }));
  assert.equal(envelope.message.isFullSnapshot, false);
  assert.equal(envelope.message.parcels[0].settlementId, 101n);
  assert.equal(envelope.message.buildings[0].parcelId, 201n);
});

test('PersistentRegionalEvolution decoder rejects Int32 overflow', () => {
  const payload = basePayload(100n, true);
  payload.currentYear = 2_147_483_648;
  assert.throws(() => decodePersistentRegionalEvolutionFrame(encodeSnapshot(payload, { major: 2, minor: 19 })), ProtocolDecodeFailure);
});

test('PersistentRegionalEvolution decoder rejects Protocol versions older than 2.19', () => {
  const frame = encodeSnapshot(basePayload(100n, true), { major: 2, minor: 18 });
  assert.throws(() => decodePersistentRegionalEvolutionFrame(frame), ProtocolDecodeFailure);
});

function basePayload(baseId, isFullSnapshot) {
  return {
    currentYear: 25,
    tickCount: String(baseId + 50n),
    settlements: [], parcels: [], buildings: [], serviceCatchments: [], infrastructureDemands: [], relations: [], events: [], commutingFlows: [], freightFlows: [],
    isFullSnapshot,
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
