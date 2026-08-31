import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  PROTOCOL_MAX_PAYLOAD_LENGTH,
  ProtocolDecodeFailure,
  type ProtocolEnvelope,
  type ProtocolVersion,
} from './protocol.ts';

export const ECONOMY_SNAPSHOT_MESSAGE_TYPE = 730;
const FIXED_PAYLOAD_LENGTH = 96;
const COMPANY_PAYLOAD_LENGTH = 57;
const HOUSEHOLD_PAYLOAD_LENGTH = 32;

export enum IndustrySector {
  Generic = 0,
  Retail = 1,
  Services = 2,
  Manufacturing = 3,
  Transport = 4,
  Public = 5,
}

export interface EconomyStatistics {
  readonly companyCount: number;
  readonly establishmentCount: number;
  readonly jobCount: number;
  readonly employedPersonCount: number;
  readonly vacantPositionCount: number;
  readonly householdCashBalance: bigint;
  readonly householdIncome: bigint;
  readonly householdSpending: bigint;
  readonly companyCashBalance: bigint;
  readonly companyRevenue: bigint;
  readonly companyExpense: bigint;
  readonly producedUnits: number;
  readonly economicCycle: bigint;
  readonly tickCount: bigint;
}

export interface CompanyEconomy {
  readonly companyId: bigint;
  readonly sector: IndustrySector;
  readonly cashBalance: bigint;
  readonly revenue: bigint;
  readonly expense: bigint;
  readonly dailyProductionCapacity: number;
  readonly producedUnits: number;
  readonly establishmentCount: number;
  readonly employeeCount: number;
}

export interface HouseholdEconomy {
  readonly householdId: bigint;
  readonly cashBalance: bigint;
  readonly income: bigint;
  readonly spending: bigint;
}

export interface EconomySnapshotMessage {
  readonly type: typeof ECONOMY_SNAPSHOT_MESSAGE_TYPE;
  readonly statistics: EconomyStatistics;
  readonly companies: readonly CompanyEconomy[];
  readonly households: readonly HouseholdEconomy[];
}

export type EconomyProtocolMessage = EconomySnapshotMessage;

export function isEconomyFrame(frame: ArrayBuffer): boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  return view.getUint32(0, true) === PROTOCOL_MAGIC && view.getUint16(8, true) === ECONOMY_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeEconomyFrame(frame: ArrayBuffer): ProtocolEnvelope & { readonly message: EconomySnapshotMessage } {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Economy frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Economy frame magic is invalid.');
  const version: ProtocolVersion = Object.freeze({ major: view.getUint16(4, true), minor: view.getUint16(6, true) });
  if (version.major !== 2 || version.minor < 10) throw new ProtocolDecodeFailure('Economy snapshots require Protocol 2.10 or newer.');
  if (view.getUint16(8, true) !== ECONOMY_SNAPSHOT_MESSAGE_TYPE) throw new ProtocolDecodeFailure('Frame is not an Economy snapshot.');
  if (view.getUint16(10, true) !== 0) throw new ProtocolDecodeFailure('Economy frame contains unsupported flags.');
  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Economy frame length is invalid.');
  if (payloadLength < FIXED_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Economy payload is too short.');

  const offset = PROTOCOL_HEADER_SIZE;
  const companyDebugCount = view.getUint16(offset + 92, true);
  const householdDebugCount = view.getUint16(offset + 94, true);
  const expectedLength = FIXED_PAYLOAD_LENGTH + companyDebugCount * COMPANY_PAYLOAD_LENGTH + householdDebugCount * HOUSEHOLD_PAYLOAD_LENGTH;
  if (payloadLength !== expectedLength) throw new ProtocolDecodeFailure('Economy payload counts do not match its length.');

  const statistics: EconomyStatistics = {
    companyCount: view.getUint32(offset, true),
    establishmentCount: view.getUint32(offset + 4, true),
    jobCount: view.getUint32(offset + 8, true),
    employedPersonCount: view.getUint32(offset + 12, true),
    vacantPositionCount: view.getUint32(offset + 16, true),
    householdCashBalance: view.getBigInt64(offset + 20, true),
    householdIncome: view.getBigInt64(offset + 28, true),
    householdSpending: view.getBigInt64(offset + 36, true),
    companyCashBalance: view.getBigInt64(offset + 44, true),
    companyRevenue: view.getBigInt64(offset + 52, true),
    companyExpense: view.getBigInt64(offset + 60, true),
    producedUnits: view.getFloat64(offset + 68, true),
    economicCycle: view.getBigUint64(offset + 76, true),
    tickCount: view.getBigUint64(offset + 84, true),
  };
  validateStatistics(statistics);

  let cursor = offset + FIXED_PAYLOAD_LENGTH;
  const companies: CompanyEconomy[] = [];
  for (let index = 0; index < companyDebugCount; index += 1) {
    const company: CompanyEconomy = {
      companyId: view.getBigUint64(cursor, true),
      sector: view.getUint8(cursor + 8) as IndustrySector,
      cashBalance: view.getBigInt64(cursor + 9, true),
      revenue: view.getBigInt64(cursor + 17, true),
      expense: view.getBigInt64(cursor + 25, true),
      dailyProductionCapacity: view.getFloat64(cursor + 33, true),
      producedUnits: view.getFloat64(cursor + 41, true),
      establishmentCount: view.getUint32(cursor + 49, true),
      employeeCount: view.getUint32(cursor + 53, true),
    };
    validateCompany(company);
    companies.push(company);
    cursor += COMPANY_PAYLOAD_LENGTH;
  }

  const households: HouseholdEconomy[] = [];
  for (let index = 0; index < householdDebugCount; index += 1) {
    const household: HouseholdEconomy = {
      householdId: view.getBigUint64(cursor, true),
      cashBalance: view.getBigInt64(cursor + 8, true),
      income: view.getBigInt64(cursor + 16, true),
      spending: view.getBigInt64(cursor + 24, true),
    };
    validateHousehold(household);
    households.push(household);
    cursor += HOUSEHOLD_PAYLOAD_LENGTH;
  }

  return { version, message: { type: ECONOMY_SNAPSHOT_MESSAGE_TYPE, statistics, companies, households } };
}

function validateStatistics(value: EconomyStatistics): void {
  if (value.householdCashBalance < 0n || value.householdIncome < 0n || value.householdSpending < 0n || value.companyCashBalance < 0n || value.companyRevenue < 0n || value.companyExpense < 0n || !Number.isFinite(value.producedUnits) || value.producedUnits < 0) throw new ProtocolDecodeFailure('Economy statistics contain invalid values.');
}

function validateCompany(value: CompanyEconomy): void {
  if (value.companyId === 0n || value.sector < IndustrySector.Generic || value.sector > IndustrySector.Public || value.cashBalance < 0n || value.revenue < 0n || value.expense < 0n || !Number.isFinite(value.dailyProductionCapacity) || value.dailyProductionCapacity < 0 || !Number.isFinite(value.producedUnits) || value.producedUnits < 0) throw new ProtocolDecodeFailure('Company economy entry is invalid.');
}

function validateHousehold(value: HouseholdEconomy): void {
  if (value.householdId === 0n || value.cashBalance < 0n || value.income < 0n || value.spending < 0n) throw new ProtocolDecodeFailure('Household economy entry is invalid.');
}
