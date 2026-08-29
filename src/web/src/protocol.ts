export const PROTOCOL_MAGIC = 0x5057564d;
export const PROTOCOL_HEADER_SIZE = 16;
export const PROTOCOL_MAX_PAYLOAD_LENGTH = 1_048_576;
export const CURRENT_PROTOCOL_VERSION = Object.freeze({ major: 1, minor: 0 });

export enum MessageType {
  Hello = 1,
  HelloAck = 2,
  SubscribeArea = 3,
  AgentSpawn = 100,
  AgentUpdate = 101,
  AgentRemove = 102,
  Error = 900,
}

export enum ProtocolErrorCode {
  UnsupportedProtocolVersion = 1,
  InvalidFrame = 2,
  UnknownMessageType = 3,
  InvalidPayload = 4,
  InvalidRequest = 5,
  InternalServerError = 1000,
}

export interface ProtocolVersion {
  readonly major: number;
  readonly minor: number;
}

export interface WorldRect {
  readonly minX: number;
  readonly minY: number;
  readonly maxX: number;
  readonly maxY: number;
}

export interface ProtocolErrorParameter {
  readonly key: string;
  readonly value: string;
}

export interface HelloMessage {
  readonly type: MessageType.Hello;
}

export interface HelloAckMessage {
  readonly type: MessageType.HelloAck;
  readonly protocolVersion: ProtocolVersion;
  readonly tickRate: number;
}

export interface SubscribeAreaMessage extends WorldRect {
  readonly type: MessageType.SubscribeArea;
}

export interface AgentStateMessage {
  readonly type: MessageType.AgentSpawn | MessageType.AgentUpdate;
  readonly agentId: bigint;
  readonly x: number;
  readonly y: number;
  readonly velocityX: number;
  readonly velocityY: number;
  readonly tickCount: bigint;
}

export interface AgentRemoveMessage {
  readonly type: MessageType.AgentRemove;
  readonly agentId: bigint;
  readonly tickCount: bigint;
}

export interface ProtocolErrorMessage {
  readonly type: MessageType.Error;
  readonly code: ProtocolErrorCode;
  readonly parameters: readonly ProtocolErrorParameter[];
}

export type ProtocolMessage =
  | HelloMessage
  | HelloAckMessage
  | SubscribeAreaMessage
  | AgentStateMessage
  | AgentRemoveMessage
  | ProtocolErrorMessage;

export interface ProtocolEnvelope {
  readonly version: ProtocolVersion;
  readonly message: ProtocolMessage;
}

export class ProtocolDecodeFailure extends Error {
  public constructor(message: string) {
    super(message);
    this.name = 'ProtocolDecodeFailure';
  }
}

const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

export function encodeHello(version: ProtocolVersion = CURRENT_PROTOCOL_VERSION): ArrayBuffer {
  return createFrame(MessageType.Hello, 0, version);
}

export function encodeSubscribeArea(
  area: WorldRect,
  version: ProtocolVersion = CURRENT_PROTOCOL_VERSION,
): ArrayBuffer {
  validateWorldRect(area);
  const frame = createFrame(MessageType.SubscribeArea, 32, version);
  const view = new DataView(frame);
  view.setFloat64(PROTOCOL_HEADER_SIZE, area.minX, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 8, area.minY, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 16, area.maxX, true);
  view.setFloat64(PROTOCOL_HEADER_SIZE + 24, area.maxY, true);
  return frame;
}

export function decodeFrame(frame: ArrayBuffer): ProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) {
    throw new ProtocolDecodeFailure('Protocol frame is shorter than the 16-byte header.');
  }

  const view = new DataView(frame);
  if (view.getUint32(0, true) !== PROTOCOL_MAGIC) {
    throw new ProtocolDecodeFailure('Protocol frame magic is invalid.');
  }

  const flags = view.getUint16(10, true);
  if (flags !== 0) {
    throw new ProtocolDecodeFailure('Protocol frame contains unsupported flags.');
  }

  const payloadLength = view.getUint32(12, true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH) {
    throw new ProtocolDecodeFailure('Protocol payload exceeds the supported limit.');
  }

  if (PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) {
    throw new ProtocolDecodeFailure('Protocol frame length does not match its payload length.');
  }

  const version = Object.freeze({
    major: view.getUint16(4, true),
    minor: view.getUint16(6, true),
  });
  const messageType = view.getUint16(8, true) as MessageType;
  const payloadOffset = PROTOCOL_HEADER_SIZE;

  return {
    version,
    message: decodeMessage(view, messageType, payloadOffset, payloadLength),
  };
}

export function protocolVersionToString(version: ProtocolVersion): string {
  return `${version.major}.${version.minor}`;
}

function createFrame(
  messageType: MessageType,
  payloadLength: number,
  version: ProtocolVersion,
): ArrayBuffer {
  validateUInt16(version.major, 'Protocol major version');
  validateUInt16(version.minor, 'Protocol minor version');
  if (!Number.isInteger(payloadLength) || payloadLength < 0 || payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH) {
    throw new RangeError('Protocol payload length is invalid.');
  }

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

function decodeMessage(
  view: DataView,
  messageType: MessageType,
  offset: number,
  payloadLength: number,
): ProtocolMessage {
  switch (messageType) {
    case MessageType.Hello:
      assertPayloadLength(payloadLength, 0, messageType);
      return { type: MessageType.Hello };
    case MessageType.HelloAck:
      assertPayloadLength(payloadLength, 6, messageType);
      return {
        type: MessageType.HelloAck,
        protocolVersion: {
          major: view.getUint16(offset, true),
          minor: view.getUint16(offset + 2, true),
        },
        tickRate: view.getUint16(offset + 4, true),
      };
    case MessageType.SubscribeArea: {
      assertPayloadLength(payloadLength, 32, messageType);
      const area: WorldRect = {
        minX: view.getFloat64(offset, true),
        minY: view.getFloat64(offset + 8, true),
        maxX: view.getFloat64(offset + 16, true),
        maxY: view.getFloat64(offset + 24, true),
      };
      validateWorldRect(area);
      return { type: MessageType.SubscribeArea, ...area };
    }
    case MessageType.AgentSpawn:
    case MessageType.AgentUpdate:
      return decodeAgentState(view, messageType, offset, payloadLength);
    case MessageType.AgentRemove:
      assertPayloadLength(payloadLength, 16, messageType);
      return {
        type: MessageType.AgentRemove,
        agentId: view.getBigUint64(offset, true),
        tickCount: view.getBigUint64(offset + 8, true),
      };
    case MessageType.Error:
      return decodeProtocolError(view, offset, payloadLength);
    default:
      throw new ProtocolDecodeFailure(`Unknown protocol message type: ${String(messageType)}.`);
  }
}

function decodeAgentState(
  view: DataView,
  type: MessageType.AgentSpawn | MessageType.AgentUpdate,
  offset: number,
  payloadLength: number,
): AgentStateMessage {
  assertPayloadLength(payloadLength, 48, type);
  const message: AgentStateMessage = {
    type,
    agentId: view.getBigUint64(offset, true),
    x: view.getFloat64(offset + 8, true),
    y: view.getFloat64(offset + 16, true),
    velocityX: view.getFloat64(offset + 24, true),
    velocityY: view.getFloat64(offset + 32, true),
    tickCount: view.getBigUint64(offset + 40, true),
  };

  if (
    !Number.isFinite(message.x) ||
    !Number.isFinite(message.y) ||
    !Number.isFinite(message.velocityX) ||
    !Number.isFinite(message.velocityY)
  ) {
    throw new ProtocolDecodeFailure('Agent state contains a non-finite value.');
  }

  return message;
}

function decodeProtocolError(
  view: DataView,
  offset: number,
  payloadLength: number,
): ProtocolErrorMessage {
  if (payloadLength < 4) {
    throw new ProtocolDecodeFailure('Protocol error payload is too short.');
  }

  const end = offset + payloadLength;
  const code = view.getUint16(offset, true) as ProtocolErrorCode;
  const parameterCount = view.getUint16(offset + 2, true);
  if (parameterCount > 16) {
    throw new ProtocolDecodeFailure('Protocol error contains too many parameters.');
  }

  let cursor = offset + 4;
  const parameters: ProtocolErrorParameter[] = [];
  for (let index = 0; index < parameterCount; index += 1) {
    const key = readUtf8String(view, cursor, end, 64);
    cursor = key.nextOffset;
    const value = readUtf8String(view, cursor, end, 256);
    cursor = value.nextOffset;
    parameters.push({ key: key.value, value: value.value });
  }

  if (cursor !== end) {
    throw new ProtocolDecodeFailure('Protocol error payload contains trailing bytes.');
  }

  return { type: MessageType.Error, code, parameters };
}

function readUtf8String(
  view: DataView,
  offset: number,
  end: number,
  maximumByteLength: number,
): { readonly value: string; readonly nextOffset: number } {
  if (offset + 2 > end) {
    throw new ProtocolDecodeFailure('Protocol string length is truncated.');
  }

  const byteLength = view.getUint16(offset, true);
  const valueOffset = offset + 2;
  const nextOffset = valueOffset + byteLength;
  if (byteLength > maximumByteLength || nextOffset > end) {
    throw new ProtocolDecodeFailure('Protocol string exceeds its allowed bounds.');
  }

  try {
    return {
      value: utf8Decoder.decode(new Uint8Array(view.buffer, view.byteOffset + valueOffset, byteLength)),
      nextOffset,
    };
  } catch {
    throw new ProtocolDecodeFailure('Protocol string is not valid UTF-8.');
  }
}

function validateWorldRect(area: WorldRect): void {
  if (
    !Number.isFinite(area.minX) ||
    !Number.isFinite(area.minY) ||
    !Number.isFinite(area.maxX) ||
    !Number.isFinite(area.maxY) ||
    area.maxX < area.minX ||
    area.maxY < area.minY
  ) {
    throw new RangeError('World rectangle coordinates must be finite and ordered.');
  }
}

function validateUInt16(value: number, label: string): void {
  if (!Number.isInteger(value) || value < 0 || value > 0xffff) {
    throw new RangeError(`${label} must fit in an unsigned 16-bit integer.`);
  }
}

function assertPayloadLength(actual: number, expected: number, type: MessageType): void {
  if (actual !== expected) {
    throw new ProtocolDecodeFailure(
      `Protocol message ${String(type)} has payload length ${String(actual)}; expected ${String(expected)}.`,
    );
  }
}
