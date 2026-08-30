import {
  MessageType,
  ProtocolDecodeFailure,
  type ProtocolErrorMessage,
  type ProtocolMessage,
  type ProtocolVersion,
  type WorldVolume,
  decodeFrame,
  encodeHello,
  encodeSubscribeVolume,
} from './protocol.ts';
import {
  WEB_PROTOCOL_VERSION,
  decodeTrafficFrame,
  isTrafficFrame,
  type TrafficProtocolMessage,
} from './traffic-protocol.ts';

export type ConnectionState = 'disconnected' | 'connecting' | 'handshaking' | 'connected' | 'reconnecting';
export interface FrameDecodeMetrics { readonly frameBytes: number; readonly decodeTimeMs: number; }
export interface ConnectionCallbacks { readonly onStateChanged: (state: ConnectionState) => void; readonly onMessage: (message: ProtocolMessage | TrafficProtocolMessage) => void; readonly onProtocolError: (message: ProtocolErrorMessage) => void; readonly onClientError: (error: Error) => void; readonly onDisconnected: () => void; readonly onHelloAck: (version: ProtocolVersion, tickRate: number) => void; readonly onFrameDecoded?: (metrics: FrameDecodeMetrics) => void; }
export interface ReconnectOptions { readonly minimumDelayMs: number; readonly maximumDelayMs: number; }

export class MachiVerseConnection {
  private socket: WebSocket | null = null;
  private state: ConnectionState = 'disconnected';
  private reconnectTimer: number | null = null;
  private reconnectAttempt = 0;
  private shouldReconnect = false;
  private negotiatedVersion: ProtocolVersion | null = null;
  private desiredSubscription: WorldVolume | null = null;

  public constructor(private readonly serverUrl: string, private readonly reconnectOptions: ReconnectOptions, private readonly callbacks: ConnectionCallbacks) {}

  public connect(): void { this.shouldReconnect = true; this.cancelReconnect(); this.openSocket(false); }

  public disconnect(): void {
    this.shouldReconnect = false;
    this.cancelReconnect();
    const socket = this.socket;
    this.socket = null;
    this.negotiatedVersion = null;
    if (socket !== null && socket.readyState < WebSocket.CLOSING) socket.close(1000, 'Client shutdown');
    this.setState('disconnected');
  }

  public setSubscription(volume: WorldVolume): void { this.desiredSubscription = { ...volume }; this.sendDesiredSubscription(); }

  private openSocket(isReconnect: boolean): void {
    const currentSocket = this.socket;
    if (currentSocket !== null && currentSocket.readyState < WebSocket.CLOSING) currentSocket.close(1000, 'Connection replaced');
    this.setState(isReconnect ? 'reconnecting' : 'connecting');
    const socket = new WebSocket(this.serverUrl);
    socket.binaryType = 'arraybuffer';
    this.socket = socket;
    socket.addEventListener('open', () => { if (this.socket !== socket) return; this.setState('handshaking'); socket.send(encodeHello(WEB_PROTOCOL_VERSION)); });
    socket.addEventListener('message', (event) => { void this.handleMessage(socket, event.data); });
    socket.addEventListener('error', () => { if (this.socket === socket) this.callbacks.onClientError(new Error('WebSocket transport error.')); });
    socket.addEventListener('close', () => {
      if (this.socket !== socket) return;
      this.socket = null;
      this.negotiatedVersion = null;
      this.callbacks.onDisconnected();
      if (this.shouldReconnect) this.scheduleReconnect(); else this.setState('disconnected');
    });
  }

  private async handleMessage(socket: WebSocket, data: unknown): Promise<void> {
    if (this.socket !== socket) return;
    try {
      const buffer = await toArrayBuffer(data);
      const onFrameDecoded = this.callbacks.onFrameDecoded;
      const decodeStartedAt = onFrameDecoded === undefined ? 0 : performance.now();

      if (this.state === 'handshaking') {
        const envelope = decodeFrame(buffer);
        if (onFrameDecoded !== undefined) onFrameDecoded({ frameBytes: buffer.byteLength, decodeTimeMs: Math.max(0, performance.now() - decodeStartedAt) });
        if (envelope.message.type === MessageType.Error) {
          this.callbacks.onProtocolError(envelope.message);
          return;
        }
        if (envelope.message.type !== MessageType.HelloAck) throw new ProtocolDecodeFailure('Expected HelloAck as the first server message.');
        const negotiatedVersion = resolveNegotiatedProtocolVersion(envelope.version, envelope.message.protocolVersion);
        this.negotiatedVersion = negotiatedVersion;
        this.reconnectAttempt = 0;
        this.setState('connected');
        this.callbacks.onHelloAck(negotiatedVersion, envelope.message.tickRate);
        this.sendDesiredSubscription();
        return;
      }

      const envelope = isTrafficFrame(buffer) ? decodeTrafficFrame(buffer) : decodeFrame(buffer);
      if (onFrameDecoded !== undefined) onFrameDecoded({ frameBytes: buffer.byteLength, decodeTimeMs: Math.max(0, performance.now() - decodeStartedAt) });
      if (this.state !== 'connected') {
        if (!isTrafficFrame(buffer) && envelope.message.type === MessageType.Error) this.callbacks.onProtocolError(envelope.message as ProtocolErrorMessage);
        return;
      }

      const negotiatedVersion = this.negotiatedVersion;
      if (negotiatedVersion === null || !protocolVersionsEqual(envelope.version, negotiatedVersion)) {
        throw new ProtocolDecodeFailure('Server frame version changed after protocol negotiation.');
      }
      if (!isTrafficFrame(buffer) && envelope.message.type === MessageType.Error) {
        this.callbacks.onProtocolError(envelope.message as ProtocolErrorMessage);
        return;
      }
      this.callbacks.onMessage(envelope.message);
    } catch (error) {
      const normalizedError = error instanceof Error ? error : new Error(String(error));
      this.callbacks.onClientError(normalizedError);
      if (socket.readyState < WebSocket.CLOSING) socket.close(1002, 'Invalid protocol frame');
    }
  }

  private sendDesiredSubscription(): void {
    const socket = this.socket;
    const version = this.negotiatedVersion;
    const volume = this.desiredSubscription;
    if (this.state !== 'connected' || socket === null || socket.readyState !== WebSocket.OPEN || version === null || volume === null) return;
    socket.send(encodeSubscribeVolume(volume, version));
  }

  private scheduleReconnect(): void {
    if (this.reconnectTimer !== null) return;
    const delay = Math.min(this.reconnectOptions.maximumDelayMs, this.reconnectOptions.minimumDelayMs * (2 ** this.reconnectAttempt));
    this.reconnectAttempt += 1;
    this.setState('reconnecting');
    this.reconnectTimer = window.setTimeout(() => { this.reconnectTimer = null; if (this.shouldReconnect) this.openSocket(true); }, delay);
  }

  private cancelReconnect(): void { if (this.reconnectTimer === null) return; window.clearTimeout(this.reconnectTimer); this.reconnectTimer = null; }
  private setState(state: ConnectionState): void { if (this.state === state) return; this.state = state; this.callbacks.onStateChanged(state); }
}

export function resolveNegotiatedProtocolVersion(
  frameVersion: ProtocolVersion,
  acknowledgedVersion: ProtocolVersion,
  supportedVersion: ProtocolVersion = WEB_PROTOCOL_VERSION,
): ProtocolVersion {
  if (!protocolVersionsEqual(frameVersion, acknowledgedVersion)) {
    throw new ProtocolDecodeFailure('HelloAck frame version and payload version do not match.');
  }
  if (frameVersion.major !== supportedVersion.major || frameVersion.minor > supportedVersion.minor) {
    throw new ProtocolDecodeFailure('Server selected an unsupported protocol version.');
  }
  return Object.freeze({ ...frameVersion });
}

export function protocolVersionsEqual(left: ProtocolVersion, right: ProtocolVersion): boolean {
  return left.major === right.major && left.minor === right.minor;
}

async function toArrayBuffer(data: unknown): Promise<ArrayBuffer> {
  if (data instanceof ArrayBuffer) return data;
  if (data instanceof Blob) return data.arrayBuffer();
  if (ArrayBuffer.isView(data)) return data.buffer.slice(data.byteOffset, data.byteOffset + data.byteLength) as ArrayBuffer;
  throw new ProtocolDecodeFailure('WebSocket frame must be binary.');
}
