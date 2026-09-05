import assert from 'node:assert/strict';
import test from 'node:test';

import {
  ROAD_SNAPSHOT_TOO_LARGE_DETAIL_CODE,
  SUBSCRIPTION_TOO_LARGE_DETAIL_CODE,
  isRetryableSubscriptionDetailCode,
} from '../src/subscription-error-policy.ts';

test('subscription volume and road snapshot overflow are retryable', () => {
  assert.equal(isRetryableSubscriptionDetailCode(SUBSCRIPTION_TOO_LARGE_DETAIL_CODE), true);
  assert.equal(isRetryableSubscriptionDetailCode(ROAD_SNAPSHOT_TOO_LARGE_DETAIL_CODE), true);
});

test('unrelated protocol errors are not retryable', () => {
  assert.equal(isRetryableSubscriptionDetailCode('personNotFound'), false);
  assert.equal(isRetryableSubscriptionDetailCode(undefined), false);
});
