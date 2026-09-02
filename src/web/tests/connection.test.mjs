import test from 'node:test';
import assert from 'node:assert/strict';

import {
  MachiVerseConnection,
  protocolVersionsEqual,
  resolveNegotiatedProtocolVersion,
  resolveProtocolFallbackVersion,
} from '../src/connection.ts';
import { MessageType, PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, ProtocolErrorCode, decodeFrame } from '../src/protocol.ts';

const WEB_VERSION = { major: 2, minor: 16 };

test('protocol negotiation keeps the accepted requested minor version', () => {
  const negotiated = resolveNegotiatedProtocolVersion(
    { major: 2, minor: 1 },
    { major: 2, minor: 1 },
    { major: 2, minor: 3 },
  );

  assert.deepEqual(negotiated, { major: 2, minor: 1 });
  assert.equal(protocolVersionsEqual(negotiated, { major: 2, minor: 1 }), true);
});

test('HelloAck rejects a mismatch between frame and payload versions', () => {
  assert.throws(
    () => resolveNegotiatedProtocolVersion(
      { major: 2, minor: 1 },
      { major: 2, minor: 2 },
      { major: 2, minor: 3 },
    ),
    /frame version and payload version do not match/,
  );
});

test('HelloAck rejects versions newer than the client supports', () => {
  assert.throws(
    () => resolveNegotiatedProtocolVersion(
      { major: 2, minor: 4 },
      { major: 2, minor: 4 },
      { major: 2, minor: 3 },
    ),
    /unsupported protocol version/,
  );
});

test('unsupported newer minor falls back once to the server-supported same-major version', () => {
  const fallback = resolveProtocolFallbackVersion(
    {
      type: MessageType.Error,
      code: ProtocolErrorCode.UnsupportedProtocolVersion,
      parameters: [
        { key: 'requestedVersion', value: '2.19' },
        { key: 'supportedVersion', value: '2.18' },
      ],
    },
    { major: 2, minor: 19 },
    { major: 2, minor: 19 },
  );

  assert.deepEqual(fallback, { major: 2, minor: 18 });
});

test('protocol fallback rejects invalid, newer, or cross-major server versions', () => {
  const message = (value) => ({
    type: MessageType.Error,
    code: ProtocolErrorCode.UnsupportedProtocolVersion,
    parameters: [{ key: 'supportedVersion', value }],
  });

  assert.equal(resolveProtocolFallbackVersion(message('2.19'), { major: 2, minor: 19 }), null);
  assert.equal(resolveProtocolFallbackVersion(message('3.0'), { major: 2, minor: 19 }), null);
  assert.equal(resolveProtocolFallbackVersion(message('invalid'), { major: 2, minor: 19 }), null);
  assert.equal(resolveProtocolFallbackVersion({ type: MessageType.Error, code: ProtocolErrorCode.InvalidRequest, parameters: [{ key: 'supportedVersion', value: '2.18' }] }, { major: 2, minor: 19 }), null);
});

test('handshake sends only the latest desired observation volume accumulated while disconnected', async () => {
  const originalWebSocket = globalThis.WebSocket;
  FakeWebSocket.instances.length = 0;
  globalThis.WebSocket = FakeWebSocket;

  const connection = new MachiVerseConnection('ws://example.test/ws', { minimumDelayMs: 1_000, maximumDelayMs: 5_000 }, createCallbacks());
  const staleVolume = { minX: -100, minY: -100, minZ: -100, maxX: 100, maxY: 100, maxZ: 100 };
  const latestVolume = { minX: 999_900, minY: -2_000_100, minZ: 900, maxX: 1_000_100, maxY: -1_999_900, maxZ: 1_100 };

  try {
    connection.setSubscription(staleVolume);
    connection.setSubscription(latestVolume);
    connection.connect();
    const socket = FakeWebSocket.instances[0];
    assert.ok(socket !== undefined);
    socket.open();
    assert.equal(socket.sent.length, 1, 'only Hello should be sent before protocol negotiation');

    socket.receive(createHelloAckFrame(WEB_VERSION));
    await new Promise((resolve) => setImmediate(resolve));

    assert.equal(socket.sent.length, 2, 'only one desired subscription should be sent after HelloAck');
    const envelope = decodeFrame(socket.sent[1]);
    assert.equal(envelope.message.type, MessageType.SubscribeVolume);
    assert.deepEqual(
      {
        minX: envelope.message.minX,
        minY: envelope.message.minY,
        minZ: envelope.message.minZ,
        maxX: envelope.message.maxX,
        maxY: envelope.message.maxY,
        maxZ: envelope.message.maxZ,
      },
      latestVolume,
    );
  } finally {
    connection.disconnect();
    globalThis.WebSocket = originalWebSocket;
  }
});

class FakeWebSocket {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSING = 2;
  static CLOSED = 3;
  static instances = [];

  readyState = FakeWebSocket.CONNECTING;
  binaryType = 'blob';
  sent = [];
  listeners = new Map();

  constructor(url) {
    this.url = url;
    FakeWebSocket.instances.push(this);
  }

  addEventListener(type, listener) {
    const listeners = this.listeners.get(type) ?? [];
    listeners.push(listener);
    this.listeners.set(type, listeners);
  }

  send(data) {
    assert.equal(this.readyState, FakeWebSocket.OPEN);
    this.sent.push(data);
  }

  close() {
    this.readyState = FakeWebSocket.CLOSED;
  }

  open() {
    this.readyState = FakeWebSocket.OPEN;
    this.dispatch('open', {});
  }

  receive(data) {
    this.dispatch('message', { data });
  }

  dispatch(type, event) {
    for (const listener of this.listeners.get(type) ?? []) listener(event);
  }
}

function createCallbacks() {
  return {
    onStateChanged: () => {},
    onMessage: () => {},
    onProtocolError: () => {},
    onClientError: (error) => { throw error; },
    onDisconnected: () => {},
    onHelloAck: () => {},
  };
}

function createHelloAckFrame(version) {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + 6);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, MessageType.HelloAck, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, 6, true);
  view.setUint16(PROTOCOL_HEADER_SIZE, version.major, true);
  view.setUint16(PROTOCOL_HEADER_SIZE + 2, version.minor, true);
  view.setUint16(PROTOCOL_HEADER_SIZE + 4, 20, true);
  return frame;
}