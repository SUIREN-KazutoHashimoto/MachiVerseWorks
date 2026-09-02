import test from 'node:test';
import assert from 'node:assert/strict';

import {
  BuildingLifecycleStatus,
  PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
  SettlementScale,
  SettlementTrend,
} from '../src/persistent-regional-evolution-protocol.ts';
import { PersistentRegionalEvolutionStore } from '../src/persistent-regional-evolution-store.ts';

test('PersistentRegionalEvolutionStore resolves stable-ID relations and grouped events', () => {
  const store = new PersistentRegionalEvolutionStore();
  store.replace(createSnapshot());

  assert.equal(store.getSettlement(101n)?.scale, SettlementScale.City);
  assert.equal(store.getSettlementForParcel(201n)?.settlementId, 101n);
  assert.equal(store.getParcelForBuilding(301n)?.parcelId, 201n);
  assert.equal(store.getSettlementForBuilding(301n)?.settlementId, 101n);
  assert.deepEqual(store.getRelationsForSettlement(101n).map((item) => item.relationId), [401n]);
  assert.deepEqual(store.getRelationsForSettlement(102n).map((item) => item.relationId), [401n]);
  assert.deepEqual(store.getEventsForSettlement(101n).map((item) => item.eventId), [501n, 502n]);
});

test('PersistentRegionalEvolutionStore replaces and clears connection-local state', () => {
  const store = new PersistentRegionalEvolutionStore();
  store.replace(createSnapshot());
  const revision = store.revision;

  const next = createSnapshot();
  next.settlements[0].trend = SettlementTrend.Declining;
  next.buildings[0].status = BuildingLifecycleStatus.Abandoned;
  store.replace(next);

  assert.equal(store.revision, revision + 1);
  assert.equal(store.getSettlement(101n)?.trend, SettlementTrend.Declining);
  assert.equal(store.getBuilding(301n)?.status, BuildingLifecycleStatus.Abandoned);

  store.clear();
  assert.equal(store.snapshot, null);
  assert.equal(store.getSettlement(101n), undefined);
  assert.deepEqual(store.getRelationsForSettlement(101n), []);
  assert.deepEqual(store.getEventsForSettlement(101n), []);
});

function createSnapshot() {
  return {
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25,
    tickCount: 100n,
    settlements: [
      { settlementId: 101n, x: 0, y: 0, z: 0, population: 20_000, jobs: 10_000, serviceIndex: 0.8, density: 0.7, accessibility: 0.9, influenceRadiusMeters: 4_000, scale: SettlementScale.City, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null },
      { settlementId: 102n, x: 2_000, y: 1_000, z: 0, population: 600, jobs: 180, serviceIndex: 0.4, density: 0.2, accessibility: 0.4, influenceRadiusMeters: 800, scale: SettlementScale.Village, trend: SettlementTrend.Stable, isActive: true, establishedYear: 0, dormantSinceYear: null },
    ],
    parcels: [
      { parcelId: 201n, settlementId: 101n, developmentDemand: 0.8, landValue: 0.7, developmentState: 3, buildingId: 301n },
    ],
    buildings: [
      { buildingId: 301n, parcelId: 201n, use: 1, builtYear: 0, lastChangedYear: 25, condition: 0.2, occupancy: 0.1, capacity: 100, status: BuildingLifecycleStatus.Vacant },
    ],
    serviceCatchments: [],
    infrastructureDemands: [],
    relations: [
      { relationId: 401n, fromSettlementId: 101n, toSettlementId: 102n, kind: 3, strength: 0.75, isActive: true, sinceYear: 10 },
    ],
    events: [
      { eventId: 501n, year: 24, kind: 0, settlementId: 101n, buildingId: 0n, reason: 'population +10' },
      { eventId: 502n, year: 25, kind: 7, settlementId: 101n, buildingId: 301n, reason: 'Active->Vacant' },
    ],
    commutingFlows: [],
    freightFlows: [],
  };
}
