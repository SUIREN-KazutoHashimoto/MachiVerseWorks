import {
  PROTOCOL_HEADER_SIZE,
  PROTOCOL_MAGIC,
  type ProtocolVersion,
} from './protocol.ts';

export const WEB_CURRENT_PROTOCOL_VERSION: ProtocolVersion = Object.freeze({ major: 2, minor: 19 });
export const CLEAR_PERSON_INSPECTION_MESSAGE_TYPE = 5;

export function encodeClearPersonInspection(version: ProtocolVersion): ArrayBuffer {
  if (version.major !== 2 || version.minor < 9) {
    throw new RangeError('ClearPersonInspection requires Protocol 2.9 or newer.');
  }
  const frame = new ArrayBuffer(PROTOCOL_HEADER_SIZE);
  const view = new DataView(frame);
  view.setUint32(0, PROTOCOL_MAGIC, true);
  view.setUint16(4, version.major, true);
  view.setUint16(6, version.minor, true);
  view.setUint16(8, CLEAR_PERSON_INSPECTION_MESSAGE_TYPE, true);
  view.setUint16(10, 0, true);
  view.setUint32(12, 0, true);
  return frame;
}
