import test from 'node:test';
import assert from 'node:assert/strict';

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

test('connection reset clears authoritative observations and interpolation history', () => {
  const state = new ViewObservationState();
  state.apply(agent(MessageType.AgentSpawn, 0, 1n), 0);
  state.apply(agent(MessageType.AgentUpdate, 10, 2n), 100);

  state.resetConnectionState();
  assert.equal(state.entities.size, 0);

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
