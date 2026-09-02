import fs from 'node:fs';

const [baselinePath, actualPath] = process.argv.slice(2);
if (!baselinePath || !actualPath) {
  throw new Error('Usage: node scripts/check-view-phase04-rendering-baseline.mjs <baseline.json> <actual.txt>');
}

const baseline = JSON.parse(fs.readFileSync(baselinePath, 'utf8'));
const actual = Object.fromEntries(
  fs.readFileSync(actualPath, 'utf8')
    .split(/\r?\n/)
    .filter(Boolean)
    .map((line) => {
      const separator = line.indexOf('=');
      if (separator <= 0) throw new Error(`Invalid rendering baseline metric line: ${line}`);
      const key = line.slice(0, separator);
      const value = Number(line.slice(separator + 1));
      if (!Number.isFinite(value)) throw new Error(`Rendering baseline metric ${key} is not numeric.`);
      return [key, value];
    }),
);

const failures = [];
for (const section of ['static', 'evolution']) {
  for (const [key, expected] of Object.entries(baseline[section]?.exact ?? {})) {
    if (!(key in actual)) failures.push(`${key}: missing (expected ${expected})`);
    else if (actual[key] !== expected) failures.push(`${key}: expected ${expected}, got ${actual[key]}`);
  }

  for (const [key, minimum] of Object.entries(baseline[section]?.minimum ?? {})) {
    if (!(key in actual)) failures.push(`${key}: missing (minimum ${minimum})`);
    else if (actual[key] < minimum) failures.push(`${key}: expected >= ${minimum}, got ${actual[key]}`);
  }
}

if (failures.length > 0) {
  throw new Error(`View Phase 4 rendering baseline mismatch:\n- ${failures.join('\n- ')}`);
}

console.log(`View Phase 4 rendering baseline matched ${baseline.fixture ?? 'checked-in baseline'}.`);
