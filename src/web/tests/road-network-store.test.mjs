import test from 'node:test';
import assert from 'node:assert/strict';
import { RoadNetworkStore } from '../src/road-network-store.ts';
import { MessageType, RoadKind, RoadNodeKind } from '../src/protocol.ts';

test('RoadNetworkStore keeps coincident XY nodes distinct by stable ID and altitude', () => {
  const store = new RoadNetworkStore();
  store.replace({ type: MessageType.RoadNetworkSnapshot, tickCount: 1n, nodes: [{ id: 1n, kind: RoadNodeKind.Endpoint, x: 0, y: 0, z: 0 }, { id: 2n, kind: RoadNodeKind.Endpoint, x: 0, y: 0, z: 20 }], segments: [{ id: 1n, kind: RoadKind.Local, startNodeId: 1n, endNodeId: 2n }], lanes: [], connections: [], accessPoints: [] });
  assert.equal(store.getNode(1n).z, 0); assert.equal(store.getNode(2n).z, 20); assert.equal(store.segmentCount, 1);
  const revision = store.revision; store.clear(); assert.equal(store.segmentCount, 0); assert.equal(store.revision, revision + 1);
});
