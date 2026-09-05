import test from 'node:test';
import assert from 'node:assert/strict';
import { Localizer } from '../src/localization.ts';

test('Localizer formats numbers with its configured locale', () => {
  const localizer = new Localizer('en-US', { detail: 'Revenue {revenue}' });
  assert.equal(localizer.t('detail', { revenue: localizer.formatNumber(1234567) }), 'Revenue 1,234,567');
});
