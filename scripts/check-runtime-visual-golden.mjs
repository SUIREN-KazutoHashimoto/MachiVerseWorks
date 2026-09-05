#!/usr/bin/env node

import { readFile, writeFile } from 'node:fs/promises';
import { join } from 'node:path';

const [artifactDirectory, goldenFile] = process.argv.slice(2);
if (!artifactDirectory || !goldenFile) {
  console.error('Usage: check-runtime-visual-golden.mjs <artifact-dir> <golden-json>');
  process.exit(2);
}

const golden = JSON.parse(await readFile(goldenFile, 'utf8'));
const summaryPath = join(artifactDirectory, 'summary.json');
const summary = JSON.parse(await readFile(summaryPath, 'utf8'));
const actual = summary.initialDiagnostics;
const required = golden.required;
if (!actual || !required) throw new Error('Runtime visual Golden or diagnostics are incomplete.');

const failures = [];
const exact = (name, actualValue, expectedValue) => {
  if (actualValue !== expectedValue) failures.push(`${name}: expected ${String(expectedValue)}, actual ${String(actualValue)}`);
};
const minimum = (name, actualValue, expectedValue) => {
  if (!(actualValue >= expectedValue)) failures.push(`${name}: expected >= ${String(expectedValue)}, actual ${String(actualValue)}`);
};
const maximum = (name, actualValue, expectedValue) => {
  if (!(actualValue <= expectedValue)) failures.push(`${name}: expected <= ${String(expectedValue)}, actual ${String(actualValue)}`);
};

exact('genericAgentCount', actual.genericAgentCount, required.genericAgentCount);
minimum('terrainSampleCount', actual.terrainSampleCount, required.minimumTerrainSamples);
minimum('settlementCount', actual.settlementCount, required.minimumSettlements);
minimum('buildingCount', actual.buildingCount, required.minimumBuildings);
minimum('roadSegmentCount', actual.roadSegmentCount, required.minimumRoadSegments);
minimum('pedestrianCount', actual.pedestrianCount, required.minimumPedestrians);
minimum('vehicleCount', actual.vehicleCount, required.minimumVehicles);
minimum('trainCount', actual.trainCount, required.minimumTrains);
maximum('visibleDebugOverlayCount', actual.visibleDebugOverlayCount, required.maximumVisibleDebugOverlays);
if (required.requireJapaneseFont === true && actual.japaneseFontReady !== true) failures.push('japaneseFontReady: expected true, actual false');

if (failures.length > 0) {
  throw new Error(`Runtime integrated Golden mismatch:\n${failures.map((failure) => `- ${failure}`).join('\n')}`);
}

summary.goldenComparisonEnabled = true;
summary.goldenKind = 'integrated-structural-and-visual-contract';
summary.goldenSchemaVersion = golden.schemaVersion;
summary.note = 'The actual normal-runtime FHD screenshots are paired with a required integrated Golden contract for major visible layers.';
await writeFile(summaryPath, `${JSON.stringify(summary, null, 2)}\n`, 'utf8');
console.log('Runtime integrated Golden contract passed.');
