import test from 'node:test';
import assert from 'node:assert/strict';
import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC } from '../src/protocol.ts';
import {
  ActivityKind,
  PersonTravelState,
  PopulationMessageType,
  TravelMode,
  WEB_POPULATION_PROTOCOL_VERSION,
  decodePopulationFrame,
  encodeInspectPerson,
  isPopulationFrame,
} from '../src/population-protocol.ts';

function createPopulationFrame(type, payloadLength) {
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, 2, true);
  view.setUint16(6, 5, true);
  view.setUint16(8, type, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  return { frame, view };
}

test('InspectPerson encodes Protocol 2.5 request and rejects zero ID', () => {
  const frame = encodeInspectPerson(42n, WEB_POPULATION_PROTOCOL_VERSION);
  const view = new DataView(frame);
  assert.equal(view.getUint16(6, true), 5);
  assert.equal(view.getUint16(8, true), PopulationMessageType.InspectPerson);
  assert.equal(view.getBigUint64(PROTOCOL_HEADER_SIZE, true), 42n);
  assert.throws(() => encodeInspectPerson(0n, WEB_POPULATION_PROTOCOL_VERSION), /greater than zero/);
});

test('PopulationStatistics decodes counts and tick', () => {
  const { frame, view } = createPopulationFrame(PopulationMessageType.PopulationStatistics, 56);
  const offset = PROTOCOL_HEADER_SIZE;
  const counts = [25, 100, 70, 20, 10, 40, 30, 5, 8, 2, 10, 5];
  counts.forEach((value, index) => view.setUint32(offset + (index * 4), value, true));
  view.setBigUint64(offset + 48, 1234n, true);

  assert.equal(isPopulationFrame(frame), true);
  assert.deepEqual(decodePopulationFrame(frame).message, {
    type: PopulationMessageType.PopulationStatistics,
    householdCount: 25,
    personCount: 100,
    atActivityCount: 70,
    walkingCount: 20,
    drivingCount: 10,
    homeCount: 40,
    workCount: 30,
    educationCount: 5,
    shoppingCount: 8,
    healthcareCount: 2,
    recreationCount: 10,
    errandCount: 5,
    tickCount: 1234n,
  });
});

test('PersonDebug decodes destination and active walking trip state', () => {
  const { frame, view } = createPopulationFrame(PopulationMessageType.PersonDebug, 100);
  const offset = PROTOCOL_HEADER_SIZE;
  view.setBigUint64(offset, 7n, true);
  view.setBigUint64(offset + 8, 3n, true);
  view.setBigUint64(offset + 16, 11n, true);
  view.setBigUint64(offset + 24, 0n, true);
  view.setBigUint64(offset + 32, 11n, true);
  view.setBigUint64(offset + 40, 0n, true);
  view.setUint8(offset + 48, ActivityKind.Home);
  view.setUint8(offset + 49, PersonTravelState.Walking);
  view.setBigUint64(offset + 50, 22n, true);
  view.setBigUint64(offset + 58, 0n, true);
  view.setUint8(offset + 66, ActivityKind.Work);
  view.setBigUint64(offset + 67, 99n, true);
  view.setUint8(offset + 75, TravelMode.Foot);
  view.setBigUint64(offset + 76, 5n, true);
  view.setBigUint64(offset + 84, 0n, true);
  view.setBigUint64(offset + 92, 456n, true);

  assert.deepEqual(decodePopulationFrame(frame).message, {
    type: PopulationMessageType.PersonDebug,
    personId: 7n,
    householdId: 3n,
    residenceBuildingId: 11n,
    residencePoiId: null,
    currentBuildingId: 11n,
    currentPoiId: null,
    currentActivity: ActivityKind.Home,
    travelState: PersonTravelState.Walking,
    destinationBuildingId: 22n,
    destinationPoiId: null,
    destinationActivity: ActivityKind.Work,
    activeTripRequestId: 99n,
    activeTravelMode: TravelMode.Foot,
    pedestrianId: 5n,
    vehicleId: null,
    tickCount: 456n,
  });
});

test('Population frames reject Protocol 2.4', () => {
  const { frame, view } = createPopulationFrame(PopulationMessageType.PopulationStatistics, 56);
  view.setUint16(6, 4, true);
  assert.throws(() => decodePopulationFrame(frame), /2\.5/);
});
