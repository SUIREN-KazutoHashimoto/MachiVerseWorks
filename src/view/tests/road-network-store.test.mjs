import test from 'node:test';
import assert from 'node:assert/strict';
import { RoadNetworkStore } from '../src/road-network-store.ts';
import { MessageType, RoadKind, RoadNodeKind } from '../src/protocol.ts';

function createSnapshot(tickCount = 1n) {
  return {
    type: MessageType.RoadNetworkSnapshot,
    tickCount,
    nodes: [
      { id: 1n, kind: RoadNodeKind.Endpoint, x: 0, y: 0, z: 0 },
      { id: 2n, kind: RoadNodeKind.Endpoint, x: 0, y: 0, z: 20 },
    ],
    segments: [{ id: 1n, kind: RoadKind.Local, startNodeId: 1n, endNodeId: 2n }],
    lanes: [],
    connections: [],
    accessPoints: [],
  };
}

test('RoadNetworkStore keeps coincident XY nodes distinct by stable ID and altitude', () => {
  const store = new RoadNetworkStore();
  store.replace(createSnapshot());
  assert.equal(store.getNode(1n).z, 0); assert.equal(store.getNode(2n).z, 20); assert.equal(store.segmentCount, 1);
  const revision = store.revision; store.clear(); assert.equal(store.segmentCount, 0); assert.equal(store.revision, revision + 1);
});

test('RoadNetworkStore does not rebuild unchanged topology when only snapshot tick changes', () => {
  const store = new RoadNetworkStore();
  store.replace(createSnapshot(1n));
  const revision = store.revision;

  store.replace(createSnapshot(99n));

  assert.equal(store.revision, revision);
  assert.equal(store.snapshot.tickCount, 99n);
});

test('RoadNetworkStore rebuilds when topology changes', () => {
  const store = new RoadNetworkStore();
  store.replace(createSnapshot());
  const revision = store.revision;
  const changed = createSnapshot(2n);
  changed.nodes = [...changed.nodes, { id: 3n, kind: RoadNodeKind.Endpoint, x: 10, y: 0, z: 0 }];

  store.replace(changed);

  assert.equal(store.revision, revision + 1);
  assert.equal(store.getNode(3n).x, 10);
});
