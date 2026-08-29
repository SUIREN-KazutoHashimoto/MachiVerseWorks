import test from 'node:test';
import assert from 'node:assert/strict';

import {
  protocolVersionsEqual,
  resolveNegotiatedProtocolVersion,
} from '../src/connection.ts';

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
