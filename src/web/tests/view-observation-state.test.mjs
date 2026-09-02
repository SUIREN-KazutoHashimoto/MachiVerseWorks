import test from 'node:test';
import assert from 'node:assert/strict';

import { PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE, SettlementScale, SettlementTrend } from '../src/persistent-regional-evolution-protocol.ts';
import { MessageType } from '../src/protocol.ts';
import { ViewObservationState } from '../src/view-observation-state.ts';

const agent = (type, x, tickCount) => ({
  type,
  agentId: 7n,
  x,
  y: 0,
  z: x * 2,
  velocityX: 1,
  velocityY: 0,
  velocityZ: 2,
  tickCount,
});

test('observation messages are applied through one state boundary', () => {
  const state = new ViewObservationState();

  assert.equal(state.apply(agent(MessageType.AgentSpawn, 0, 1n), 0), true);
  assert.equal(state.apply(agent(MessageType.AgentUpdate, 10, 2n), 100), true);
  assert.equal(state.entities.size, 1);

  const [sampled] = [...state.entities.sample(150)];
  assert.equal(sampled.x, 5);
  assert.equal(sampled.z, 10);
  assert.equal(sampled.tickCount, 2n);
});

test('persistent regional evolution enters through the same read-only observation boundary', () => {
  const state = new ViewObservationState();
  const message = {
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25,
    tickCount: 88n,
    settlements: [{ settlementId: 101n, x: 1, y: 2, z: 3, population: 100, jobs: 20, serviceIndex: 0.4, density: 0.2, accessibility: 0.3, influenceRadiusMeters: 600, scale: SettlementScale.Village, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null }],
    parcels: [],
    buildings: [],
    serviceCatchments: [],
    infrastructureDemands: [],
    relations: [],
    events: [],
    commutingFlows: [],
    freightFlows: [],
  };

  assert.equal(state.apply(message), true);
  assert.equal(state.persistentRegionalEvolution.snapshot?.currentYear, 25);
  assert.equal(state.persistentRegionalEvolution.getSettlement(101n)?.scale, SettlementScale.Village);
});

test('connection reset clears authoritative observations and interpolation history', () => {
  const state = new ViewObservationState();
  state.apply(agent(MessageType.AgentSpawn, 0, 1n), 0);
  state.apply(agent(MessageType.AgentUpdate, 10, 2n), 100);
  state.apply({
    type: PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE,
    currentYear: 25,
    tickCount: 88n,
    settlements: [{ settlementId: 101n, x: 1, y: 2, z: 3, population: 100, jobs: 20, serviceIndex: 0.4, density: 0.2, accessibility: 0.3, influenceRadiusMeters: 600, scale: SettlementScale.Village, trend: SettlementTrend.Growing, isActive: true, establishedYear: 0, dormantSinceYear: null }],
    parcels: [], buildings: [], serviceCatchments: [], infrastructureDemands: [], relations: [], events: [], commutingFlows: [], freightFlows: [],
  });

  state.resetConnectionState();
  assert.equal(state.entities.size, 0);
  assert.equal(state.persistentRegionalEvolution.snapshot, null);

  state.apply(agent(MessageType.AgentSpawn, 100, 3n), 1_000);
  const [sampled] = [...state.entities.sample(1_000)];
  assert.equal(sampled.x, 100);
  assert.equal(sampled.z, 200);
});

test('non-observation control messages are not consumed by view state', () => {
  const state = new ViewObservationState();
  assert.equal(state.apply({ type: MessageType.Hello }), false);
  assert.equal(state.entities.size, 0);
});