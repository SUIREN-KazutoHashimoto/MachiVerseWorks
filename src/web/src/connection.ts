import { MessageType, ProtocolDecodeFailure, ProtocolErrorCode, type ProtocolErrorMessage, type ProtocolMessage, type ProtocolVersion, type WorldVolume, decodeFrame, encodeHello, encodeSubscribeVolume } from './protocol.ts';
import { decodePopulationFrame, encodeInspectPerson, isPopulationFrame, type PopulationProtocolMessage } from './population-protocol.ts';
import { WEB_CURRENT_PROTOCOL_VERSION, encodeClearPersonInspection } from './person-inspection-protocol.ts';
import { decodeRailwayFrame, isRailwayFrame, type RailwayProtocolMessage } from './railway-infrastructure.ts';
import { decodeRailwayOperationsFrame, isRailwayOperationsFrame, type RailwayOperationsProtocolMessage } from './railway-operations.ts';
import { decodeMultimodalTransitFrame, isMultimodalTransitFrame, type MultimodalTransitProtocolMessage } from './multimodal-transit.ts';
import { decodeTrafficFrame, isTrafficFrame, type TrafficProtocolMessage } from './traffic-protocol.ts';
import { decodeEconomyFrame, isEconomyFrame, type EconomyProtocolMessage } from './economy-protocol.ts';
import { decodeLogisticsFrame, isLogisticsFrame, type LogisticsProtocolMessage } from './logistics-protocol.ts';
import { decodePowerFrame, isPowerFrame, type PowerProtocolMessage } from './power-protocol.ts';
import { decodeWaterSewerFrame, isWaterSewerFrame, type WaterSewerProtocolMessage } from './water-sewer-protocol.ts';
import { decodeGasFrame, isGasFrame, type GasProtocolMessage } from './gas-protocol.ts';
import { decodeOpticalFrame, isOpticalFrame, type OpticalProtocolMessage } from './optical-protocol.ts';
import { decodeRadioFrame, isRadioFrame, type RadioProtocolMessage } from './radio-protocol.ts';
import { decodePersistentRegionalEvolutionFrame, isPersistentRegionalEvolutionFrame, type PersistentRegionalEvolutionSnapshotMessage } from './persistent-regional-evolution-protocol.ts';
import { decodeRegionalGenerationFrame, isRegionalGenerationFrame, type RegionalGenerationSnapshotMessage } from './regional-generation-protocol.ts';
import { decodeWorldEnvironmentFrame, isWorldEnvironmentFrame, type WorldEnvironmentSnapshotMessage } from './world-environment-protocol.ts';

export type ConnectionState = 'disconnected' | 'connecting' | 'handshaking' | 'connected' | 'reconnecting';
export interface FrameDecodeMetrics { readonly frameBytes: number; readonly decodeTimeMs: number; }
export interface ConnectionCallbacks {
  readonly onStateChanged: (state: ConnectionState) => void;
  readonly onMessage: (message: ProtocolMessage | TrafficProtocolMessage | PopulationProtocolMessage | RailwayProtocolMessage | RailwayOperationsProtocolMessage | MultimodalTransitProtocolMessage | EconomyProtocolMessage | LogisticsProtocolMessage | PowerProtocolMessage | WaterSewerProtocolMessage | GasProtocolMessage | OpticalProtocolMessage | RadioProtocolMessage | WorldEnvironmentSnapshotMessage | RegionalGenerationSnapshotMessage | PersistentRegionalEvolutionSnapshotMessage) => void;
  readonly onProtocolError: (message: ProtocolErrorMessage) => void;
  readonly onClientError: (error: Error) => void;
  readonly onDisconnected: () => void;
  readonly onHelloAck: (version: ProtocolVersion, tickRate: number) => void;
  readonly onFrameDecoded?: (metrics: FrameDecodeMetrics) => void;
}
export interface ReconnectOptions { readonly minimumDelayMs: number; readonly maximumDelayMs: number; }

export class MachiVerseConnection {
  private socket: WebSocket | null = null; private state: ConnectionState = 'disconnected'; private reconnectTimer: number | null = null; private reconnectAttempt = 0; private shouldReconnect = false; private negotiatedVersion: ProtocolVersion | null = null; private requestedHandshakeVersion: ProtocolVersion = WEB_CURRENT_PROTOCOL_VERSION; private desiredSubscription: WorldVolume | null = null; private desiredPersonId: bigint | null = null;
  public constructor(private readonly serverUrl: string, private readonly reconnectOptions: ReconnectOptions, private readonly callbacks: ConnectionCallbacks) {}
  public connect(): void { this.shouldReconnect = true; this.requestedHandshakeVersion = WEB_CURRENT_PROTOCOL_VERSION; this.cancelReconnect(); this.openSocket(false); }
  public disconnect(): void { this.shouldReconnect = false; this.cancelReconnect(); const socket = this.socket; this.socket = null; this.negotiatedVersion = null; if (socket !== null && socket.readyState < WebSocket.CLOSING) socket.close(1000, 'Client shutdown'); this.setState('disconnected'); }
  public setSubscription(volume: WorldVolume): void { this.desiredSubscription = { ...volume }; this.sendDesiredSubscription(); }
  public inspectPerson(personId: bigint): void { if (personId <= 0n) throw new RangeError('Person ID must be greater than zero.'); this.desiredPersonId = personId; this.sendDesiredInspection(); }
  public clearPersonInspection(): void { this.desiredPersonId = null; const socket = this.socket; const version = this.negotiatedVersion; if (this.state !== 'connected' || socket === null || socket.readyState !== WebSocket.OPEN || version === null || version.major !== 2 || version.minor < 9) return; socket.send(encodeClearPersonInspection(version)); }

  private openSocket(isReconnect: boolean): void {
    const currentSocket = this.socket; if (currentSocket !== null && currentSocket.readyState < WebSocket.CLOSING) currentSocket.close(1000, 'Connection replaced'); this.setState(isReconnect ? 'reconnecting' : 'connecting'); const socket = new WebSocket(this.serverUrl); socket.binaryType = 'arraybuffer'; this.socket = socket;
    socket.addEventListener('open', () => { if (this.socket !== socket) return; this.setState('handshaking'); socket.send(encodeHello(this.requestedHandshakeVersion)); });
    socket.addEventListener('message', (event) => { void this.handleMessage(socket, event.data); });
    socket.addEventListener('error', () => { if (this.socket === socket) this.callbacks.onClientError(new Error('WebSocket transport error.')); });
    socket.addEventListener('close', () => { if (this.socket !== socket) return; this.socket = null; this.negotiatedVersion = null; this.callbacks.onDisconnected(); if (this.shouldReconnect) this.scheduleReconnect(); else this.setState('disconnected'); });
  }

  private async handleMessage(socket: WebSocket, data: unknown): Promise<void> {
    if (this.socket !== socket) return;
    try {
      const buffer = await toArrayBuffer(data); const onFrameDecoded = this.callbacks.onFrameDecoded; const decodeStartedAt = onFrameDecoded === undefined ? 0 : performance.now();
      if (this.state === 'handshaking') {
        const envelope = decodeFrame(buffer); if (onFrameDecoded !== undefined) onFrameDecoded({ frameBytes: buffer.byteLength, decodeTimeMs: Math.max(0, performance.now() - decodeStartedAt) });
        if (envelope.message.type === MessageType.Error) {
          const fallback = resolveProtocolFallbackVersion(envelope.message, this.requestedHandshakeVersion);
          if (fallback !== null) { this.requestedHandshakeVersion = fallback; if (socket.readyState < WebSocket.CLOSING) socket.close(1000, 'Protocol fallback'); return; }
          this.callbacks.onProtocolError(envelope.message); return;
        }
        if (envelope.message.type !== MessageType.HelloAck) throw new ProtocolDecodeFailure('Expected HelloAck as the first server message.');
        const negotiatedVersion = resolveNegotiatedProtocolVersion(envelope.version, envelope.message.protocolVersion); this.negotiatedVersion = negotiatedVersion; this.requestedHandshakeVersion = negotiatedVersion; this.reconnectAttempt = 0; this.setState('connected'); this.callbacks.onHelloAck(negotiatedVersion, envelope.message.tickRate); this.sendDesiredSubscription(); this.sendDesiredInspection(); return;
      }

      const persistentRegionalEvolutionFrame = isPersistentRegionalEvolutionFrame(buffer);
      const regionalGenerationFrame = !persistentRegionalEvolutionFrame && isRegionalGenerationFrame(buffer);
      const worldEnvironmentFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && isWorldEnvironmentFrame(buffer);
      const radioFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && isRadioFrame(buffer);
      const opticalFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && isOpticalFrame(buffer);
      const gasFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && isGasFrame(buffer);
      const waterSewerFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && isWaterSewerFrame(buffer);
      const powerFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && isPowerFrame(buffer);
      const logisticsFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && isLogisticsFrame(buffer);
      const economyFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && !logisticsFrame && isEconomyFrame(buffer);
      const multimodalTransitFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && !logisticsFrame && !economyFrame && isMultimodalTransitFrame(buffer);
      const railwayFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && !logisticsFrame && !economyFrame && !multimodalTransitFrame && isRailwayFrame(buffer);
      const railwayOperationsFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && !logisticsFrame && !economyFrame && !multimodalTransitFrame && !railwayFrame && isRailwayOperationsFrame(buffer);
      const populationFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && !logisticsFrame && !economyFrame && !multimodalTransitFrame && !railwayFrame && !railwayOperationsFrame && isPopulationFrame(buffer);
      const trafficFrame = !persistentRegionalEvolutionFrame && !regionalGenerationFrame && !worldEnvironmentFrame && !radioFrame && !opticalFrame && !gasFrame && !waterSewerFrame && !powerFrame && !logisticsFrame && !economyFrame && !multimodalTransitFrame && !railwayFrame && !railwayOperationsFrame && !populationFrame && isTrafficFrame(buffer);
      const envelope = persistentRegionalEvolutionFrame ? decodePersistentRegionalEvolutionFrame(buffer) : regionalGenerationFrame ? decodeRegionalGenerationFrame(buffer) : worldEnvironmentFrame ? decodeWorldEnvironmentFrame(buffer) : radioFrame ? decodeRadioFrame(buffer) : opticalFrame ? decodeOpticalFrame(buffer) : gasFrame ? decodeGasFrame(buffer) : waterSewerFrame ? decodeWaterSewerFrame(buffer) : powerFrame ? decodePowerFrame(buffer) : logisticsFrame ? decodeLogisticsFrame(buffer) : economyFrame ? decodeEconomyFrame(buffer) : multimodalTransitFrame ? decodeMultimodalTransitFrame(buffer) : railwayFrame ? decodeRailwayFrame(buffer) : railwayOperationsFrame ? decodeRailwayOperationsFrame(buffer) : populationFrame ? decodePopulationFrame(buffer) : trafficFrame ? decodeTrafficFrame(buffer) : decodeFrame(buffer);
      if (onFrameDecoded !== undefined) onFrameDecoded({ frameBytes: buffer.byteLength, decodeTimeMs: Math.max(0, performance.now() - decodeStartedAt) });
      const specializedFrame = persistentRegionalEvolutionFrame || regionalGenerationFrame || worldEnvironmentFrame || radioFrame || opticalFrame || gasFrame || waterSewerFrame || powerFrame || logisticsFrame || economyFrame || multimodalTransitFrame || railwayFrame || railwayOperationsFrame || populationFrame || trafficFrame;
      if (this.state !== 'connected') { if (!specializedFrame && envelope.message.type === MessageType.Error) this.callbacks.onProtocolError(envelope.message as ProtocolErrorMessage); return; }
      const negotiatedVersion = this.negotiatedVersion; if (negotiatedVersion === null || !protocolVersionsEqual(envelope.version, negotiatedVersion)) throw new ProtocolDecodeFailure('Server frame version changed after protocol negotiation.');
      if (!specializedFrame && envelope.message.type === MessageType.Error) { this.callbacks.onProtocolError(envelope.message as ProtocolErrorMessage); return; }
      this.callbacks.onMessage(envelope.message);
    } catch (error) { const normalizedError = error instanceof Error ? error : new Error(String(error)); this.callbacks.onClientError(normalizedError); if (socket.readyState < WebSocket.CLOSING) socket.close(1002, 'Invalid protocol frame'); }
  }

  private sendDesiredSubscription(): void { const socket = this.socket; const version = this.negotiatedVersion; const volume = this.desiredSubscription; if (this.state !== 'connected' || socket === null || socket.readyState !== WebSocket.OPEN || version === null || volume === null) return; socket.send(encodeSubscribeVolume(volume, version)); }
  private sendDesiredInspection(): void { const socket = this.socket; const version = this.negotiatedVersion; const personId = this.desiredPersonId; if (this.state !== 'connected' || socket === null || socket.readyState !== WebSocket.OPEN || version === null || version.major !== 2 || version.minor < 5 || personId === null) return; socket.send(encodeInspectPerson(personId, version)); }
  private scheduleReconnect(): void { if (this.reconnectTimer !== null) return; const delay = Math.min(this.reconnectOptions.maximumDelayMs, this.reconnectOptions.minimumDelayMs * (2 ** this.reconnectAttempt)); this.reconnectAttempt += 1; this.setState('reconnecting'); this.reconnectTimer = window.setTimeout(() => { this.reconnectTimer = null; if (this.shouldReconnect) this.openSocket(true); }, delay); }
  private cancelReconnect(): void { if (this.reconnectTimer === null) return; window.clearTimeout(this.reconnectTimer); this.reconnectTimer = null; }
  private setState(state: ConnectionState): void { if (this.state === state) return; this.state = state; this.callbacks.onStateChanged(state); }
}

export function resolveProtocolFallbackVersion(message: ProtocolErrorMessage, requestedVersion: ProtocolVersion, supportedVersion: ProtocolVersion = WEB_CURRENT_PROTOCOL_VERSION): ProtocolVersion | null {
  if (message.code !== ProtocolErrorCode.UnsupportedProtocolVersion) return null;
  const value = message.parameters.find((parameter) => parameter.key === 'supportedVersion')?.value;
  if (value === undefined) return null;
  const match = /^(\d+)\.(\d+)$/.exec(value);
  if (match === null) return null;
  const major = Number(match[1]); const minor = Number(match[2]);
  if (!Number.isInteger(major) || !Number.isInteger(minor) || major !== requestedVersion.major || major !== supportedVersion.major || minor < 0 || minor >= requestedVersion.minor || minor > supportedVersion.minor) return null;
  return Object.freeze({ major, minor });
}
export function resolveNegotiatedProtocolVersion(frameVersion: ProtocolVersion, acknowledgedVersion: ProtocolVersion, supportedVersion: ProtocolVersion = WEB_CURRENT_PROTOCOL_VERSION): ProtocolVersion { if (!protocolVersionsEqual(frameVersion, acknowledgedVersion)) throw new ProtocolDecodeFailure('HelloAck frame version and payload version do not match.'); if (frameVersion.major !== supportedVersion.major || frameVersion.minor > supportedVersion.minor) throw new ProtocolDecodeFailure('Server selected an unsupported protocol version.'); return Object.freeze({ ...frameVersion }); }
export function protocolVersionsEqual(left: ProtocolVersion, right: ProtocolVersion): boolean { return left.major === right.major && left.minor === right.minor; }
async function toArrayBuffer(data: unknown): Promise<ArrayBuffer> { if (data instanceof ArrayBuffer) return data; if (data instanceof Blob) return data.arrayBuffer(); if (ArrayBuffer.isView(data)) return data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength) as ArrayBuffer; throw new ProtocolDecodeFailure('WebSocket frame must be binary.'); }