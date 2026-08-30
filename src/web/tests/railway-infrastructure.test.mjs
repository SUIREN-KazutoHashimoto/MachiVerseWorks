import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from 'three';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  RailwayInfrastructureLayer,
  RailwayMessageType,
  TrackDirection,
  TrackElectrification,
  TrackUsage,
  decodeRailwayFrame,
  isRailwayFrame,
} from '../src/railway-infrastructure.ts';

function createFixtureFrame() {
  const payloadLength = 41 + (2 * 33) + 43 + 56 + 88;
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 6, true);
  view.setUint16(8, RailwayMessageType.RailwayInfrastructureSnapshot, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  let cursor = PROTOCOL_HEADER_SIZE;
  const u8 = (value) => { view.setUint8(cursor, value); cursor += 1; };
  const u32 = (value) => { view.setUint32(cursor, value, true); cursor += 4; };
  const u64 = (value) => { view.setBigUint64(cursor, BigInt(value), true); cursor += 8; };
  const f64 = (value) => { view.setFloat64(cursor, value, true); cursor += 8; };
  const bounds = (minX, minY, minZ, maxX, maxY, maxZ) => { f64(minX); f64(minY); f64(minZ); f64(maxX); f64(maxY); f64(maxZ); };

  u64(9); u8(1);
  u32(2); u32(1); u32(0); u32(0); u32(1); u32(1); u32(0); u32(0);
  u64(1); u8(0); f64(-10); f64(0); f64(8);
  u64(2); u8(0); f64(10); f64(0); f64(8);
  u64(3); u64(1); u64(2); u8(TrackDirection.Bidirectional); f64(1.067); f64(20); u8(TrackElectrification.Overhead); u8(TrackUsage.Mainline);
  u64(4); bounds(-5, -4, 7, 5, 4, 10);
  u64(5); u64(4); u64(3); f64(0.2); f64(0.8); bounds(-4, -2, 8, 4, -1, 9);
  assert.equal(cursor, frame.byteLength);
  return frame;
}

test('Protocol 2.6 railway snapshot decodes 3D track station and platform', () => {
  const frame = createFixtureFrame();
  assert.equal(isRailwayFrame(frame), true);
  const envelope = decodeRailwayFrame(frame);
  assert.deepEqual(envelope.version, { major: 2, minor: 6 });
  assert.equal(envelope.message.revision, 9n);
  assert.equal(envelope.message.nodes[0].z, 8);
  assert.equal(envelope.message.segments[0].gaugeMeters, 1.067);
  assert.equal(envelope.message.stations[0].maxZ, 10);
  assert.equal(envelope.message.platforms[0].trackSegmentId, 3n);
});

test('railway layer renders track and 3D bounds as static geometry', () => {
  const scene = new THREE.Scene();
  const layer = new RailwayInfrastructureLayer(scene);
  const snapshot = decodeRailwayFrame(createFixtureFrame()).message;
  layer.apply(snapshot);

  const tracks = scene.getObjectByName('railway-tracks');
  const stations = scene.getObjectByName('railway-stations');
  const platforms = scene.getObjectByName('railway-platforms');
  assert.equal(tracks.geometry.getAttribute('position').count, 2);
  assert.equal(stations.geometry.getAttribute('position').count, 24);
  assert.equal(platforms.geometry.getAttribute('position').count, 24);

  layer.clear();
  assert.equal(tracks.geometry.getAttribute('position').count, 0);
  layer.dispose();
  assert.equal(scene.getObjectByName('railway-tracks'), undefined);
});

test('railway decoder rejects snapshots negotiated below 2.6', () => {
  const frame = createFixtureFrame();
  new DataView(frame).setUint16(6, 5, true);
  assert.throws(() => decodeRailwayFrame(frame), /2\.6/);
});
