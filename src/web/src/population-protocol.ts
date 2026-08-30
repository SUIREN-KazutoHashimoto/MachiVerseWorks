import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolVersion,
} from './protocol.ts';

const POPULATION_STATISTICS_LENGTH = 56;
const PERSON_DEBUG_LENGTH = 100;
const NULL_ENUM = 0xff;

export const WEB_POPULATION_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 5 });

export enum PopulationMessageType {
  InspectPerson = 4,
  PopulationStatistics = 600,
  PersonDebug = 601,
}

export enum ActivityKind { Home = 0, Work = 1, Education = 2, Shopping = 3, Healthcare = 4, Recreation = 5, Errand = 6 }
export enum PersonTravelState { AtActivity = 0, Walking = 1, Driving = 2 }
export enum TravelMode { Any = 0, Foot = 1, Motor = 2 }

export interface PopulationStatisticsMessage {
  readonly type: PopulationMessageType.PopulationStatistics;
  readonly householdCount: number;
  readonly personCount: number;
  readonly atActivityCount: number;
  readonly walkingCount: number;
  readonly drivingCount: number;
  readonly homeCount: number;
  readonly workCount: number;
  readonly educationCount: number;
  readonly shoppingCount: number;
  readonly healthcareCount: number;
  readonly recreationCount: number;
  readonly errandCount: number;
  readonly tickCount: bigint;
}

export interface PersonDebugMessage {
  readonly type: PopulationMessageType.PersonDebug;
  readonly personId: bigint;
  readonly householdId: bigint;
  readonly residenceBuildingId: bigint | null;
  readonly residencePoiId: bigint | null;
  readonly currentBuildingId: bigint | null;
  readonly currentPoiId: bigint | null;
  readonly currentActivity: ActivityKind;
  readonly travelState: PersonTravelState;
  readonly destinationBuildingId: bigint | null;
  readonly destinationPoiId: bigint | null;
  readonly destinationActivity: ActivityKind | null;
  readonly activeTripRequestId: bigint | null;
  readonly activeTravelMode: TravelMode | null;
  readonly pedestrianId: bigint | null;
  readonly vehicleId: bigint | null;
  readonly tickCount: bigint;
}

export type PopulationProtocolMessage = PopulationStatisticsMessage | PersonDebugMessage;
export interface PopulationProtocolEnvelope { readonly version: ProtocolVersion; readonly message: PopulationProtocolMessage; }

export function encodeInspectPerson(personId: bigint, version: ProtocolVersion): ArrayBuffer {
  if (personId <= 0n) throw new RangeError('Person ID must be greater than zero.');
  const frame = createFrame(PopulationMessageType.InspectPerson, 8, version);
  new DataView(frame).setBigUint64(PROTOCOL_HEADER_SIZE, personId, true);
  return frame;
}

export function isPopulationFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const type = new DataView(frame).getUint16(8, true);
  return type === PopulationMessageType.PopulationStatistics || type === PopulationMessageType.PersonDebug;
}

export function decodePopulationFrame(frame: ArrayBuffer): PopulationProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Population frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Population frame magic is invalid.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Population frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Population frame payload length is invalid.');
  const version = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 5) throw new ProtocolDecodeFailure('Population frames require Protocol 2.5 or newer.');
  const type = view.getUint16(8, true) as PopulationMessageType;
  const offset = PROTOCOL_HEADER_SIZE;
  if (type === PopulationMessageType.PopulationStatistics) return { version, message: decodeStatistics(view, offset, payloadLength) };
  if (type === PopulationMessageType.PersonDebug) return { version, message: decodePerson(view, offset, payloadLength) };
  throw new ProtocolDecodeFailure(`Unknown population message type: ${String(type)}.`);
}

function decodeStatistics(view: DataView, offset: number, payloadLength: number): PopulationStatisticsMessage {
  assertPayloadLength(payloadLength, POPULATION_STATISTICS_LENGTH);
  return {
    type: PopulationMessageType.PopulationStatistics,
    householdCount: view.getUint32(offset, true),
    personCount: view.getUint32(offset + 4, true),
    atActivityCount: view.getUint32(offset + 8, true),
    walkingCount: view.getUint32(offset + 12, true),
    drivingCount: view.getUint32(offset + 16, true),
    homeCount: view.getUint32(offset + 20, true),
    workCount: view.getUint32(offset + 24, true),
    educationCount: view.getUint32(offset + 28, true),
    shoppingCount: view.getUint32(offset + 32, true),
    healthcareCount: view.getUint32(offset + 36, true),
    recreationCount: view.getUint32(offset + 40, true),
    errandCount: view.getUint32(offset + 44, true),
    tickCount: view.getBigUint64(offset + 48, true),
  };
}

function decodePerson(view: DataView, offset: number, payloadLength: number): PersonDebugMessage {
  assertPayloadLength(payloadLength, PERSON_DEBUG_LENGTH);
  const personId = view.getBigUint64(offset, true);
  const householdId = view.getBigUint64(offset + 8, true);
  const residenceBuildingId = nullableId(view.getBigUint64(offset + 16, true));
  const residencePoiId = nullableId(view.getBigUint64(offset + 24, true));
  const currentBuildingId = nullableId(view.getBigUint64(offset + 32, true));
  const currentPoiId = nullableId(view.getBigUint64(offset + 40, true));
  const currentActivity = view.getUint8(offset + 48) as ActivityKind;
  const travelState = view.getUint8(offset + 49) as PersonTravelState;
  const destinationBuildingId = nullableId(view.getBigUint64(offset + 50, true));
  const destinationPoiId = nullableId(view.getBigUint64(offset + 58, true));
  const destinationActivityRaw = view.getUint8(offset + 66);
  const activeTripRequestId = nullableId(view.getBigUint64(offset + 67, true));
  const activeTravelModeRaw = view.getUint8(offset + 75);
  const pedestrianId = nullableId(view.getBigUint64(offset + 76, true));
  const vehicleId = nullableId(view.getBigUint64(offset + 84, true));
  const tickCount = view.getBigUint64(offset + 92, true);
  if (personId === 0n || householdId === 0n
    || !validEndpoint(residenceBuildingId, residencePoiId, false)
    || !validEndpoint(currentBuildingId, currentPoiId, false)
    || !validEndpoint(destinationBuildingId, destinationPoiId, true)
    || !isActivityKind(currentActivity)
    || !isTravelState(travelState)
    || (destinationActivityRaw !== NULL_ENUM && !isActivityKind(destinationActivityRaw as ActivityKind))
    || (activeTravelModeRaw !== NULL_ENUM && !isTravelMode(activeTravelModeRaw as TravelMode))) {
    throw new ProtocolDecodeFailure('Person debug payload is invalid.');
  }
  return {
    type: PopulationMessageType.PersonDebug,
    personId,
    householdId,
    residenceBuildingId,
    residencePoiId,
    currentBuildingId,
    currentPoiId,
    currentActivity,
    travelState,
    destinationBuildingId,
    destinationPoiId,
    destinationActivity: destinationActivityRaw === NULL_ENUM ? null : destinationActivityRaw as ActivityKind,
    activeTripRequestId,
    activeTravelMode: activeTravelModeRaw === NULL_ENUM ? null : activeTravelModeRaw as TravelMode,
    pedestrianId,
    vehicleId,
    tickCount,
  };
}

function createFrame(messageType: PopulationMessageType, payloadLength: number, version: ProtocolVersion): ArrayBuffer {
  if (version.major !== 2 || version.minor < 5) throw new RangeError('Population messages require Protocol 2.5 or newer.');
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE + payloadLength);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, messageType, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, payloadLength, true);
  return frame;
}

function nullableId(value: bigint): bigint | null { return value === 0n ? null : value; }
function validEndpoint(buildingId: bigint | null, poiId: bigint | null, allowEmpty: boolean): boolean {
  if (buildingId === null && poiId === null) return allowEmpty;
  return (buildingId === null) !== (poiId === null);
}
function isActivityKind(value: ActivityKind): boolean { return value >= ActivityKind.Home && value <= ActivityKind.Errand; }
function isTravelState(value: PersonTravelState): boolean { return value >= PersonTravelState.AtActivity && value <= PersonTravelState.Driving; }
function isTravelMode(value: TravelMode): boolean { return value >= TravelMode.Any && value <= TravelMode.Motor; }
function assertPayloadLength(actual: number, expected: number): void { if (actual !== expected) throw new ProtocolDecodeFailure(`Population payload length must be ${String(expected)} bytes.`); }
