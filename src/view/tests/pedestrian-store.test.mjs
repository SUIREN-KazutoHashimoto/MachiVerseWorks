import test from 'node:test';
import assert from 'node:assert/strict';

import { PedestrianStore } from '../src/pedestrian-store.ts';
import { MessageType, PedestrianMovementState } from '../src/protocol.ts';

function state(type, x, tickCount, movementState = PedestrianMovementState.Walking) {
  return {
    type,
    pedestrianId: 1n,
    tripRequestId: 10n,
    x,
    y: 2,
    z: 3,
    velocityX: 1,
    velocityY: 0,
    velocityZ: 0,
    walkingSpeedMetersPerSecond: 1.4,
    state: movementState,
    tickCount,
  };
}

test('pedestrian store interpolates updates and preserves movement state', () => {
  const store = new PedestrianStore();
  store.spawn(state(MessageType.PedestrianSpawn, 0, 1n), 100);
  assert.equal(store.update(state(MessageType.PedestrianUpdate, 10, 2n, PedestrianMovementState.WaitingForCrossing), 200), true);
  const sampled = [...store.sample(250)][0];
  assert.equal(sampled.x, 5);
  assert.equal(sampled.state, PedestrianMovementState.WaitingForCrossing);
  assert.equal(sampled.tickCount, 2n);
});

test('pedestrian store supports instanced-render position buffers and removal', () => {
  const store = new PedestrianStore();
  store.spawn(state(MessageType.PedestrianSpawn, 4, 1n), 100);
  const positions = new Float32Array(3);
  assert.equal(store.writeSampledPositions(100, positions), 1);
  assert.deepEqual([...positions], [4, 2, 3]);
  assert.equal(store.remove(1n), true);
  assert.equal(store.size, 0);
});
