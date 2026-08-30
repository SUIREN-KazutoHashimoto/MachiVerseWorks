import { AmbientSystem } from './ambient-system.ts';
import { AudioEngine } from './audio-engine.ts';
import { ClientPerformanceMetrics } from './client-performance.ts';
import { loadClientConfig } from './config.ts';
import { MachiVerseConnection } from './connection.ts';
import { EntityStore } from './entity-store.ts';
import { initializeLocalization, type LocaleParameters } from './localization.ts';
import { MessageType, type AgentStateMessage, type ProtocolErrorMessage, type ProtocolMessage, type WorldVolume } from './protocol.ts';
import { ClientUi } from './ui.ts';
import { WorldView } from './world-view.ts';

export class Application {
  private readonly localizer = initializeLocalization();
  private readonly config = loadClientConfig();
  private readonly store = new EntityStore();
  private readonly audio = new AudioEngine();
  private readonly ambient = new AmbientSystem(this.audio);
  private readonly performanceMetrics = import.meta.env.DEV ? new ClientPerformanceMetrics() : null;
  private readonly view: WorldView;
  private readonly ui: ClientUi;
  private readonly connection: MachiVerseConnection;
  private animationFrame = 0;
  private lastSubscriptionAt = Number.NEGATIVE_INFINITY;
  private lastSubscription: WorldVolume | null = null;
  private lastAudioSyncAt = Number.NEGATIVE_INFINITY;
  private lastPerformanceUiAt = Number.NEGATIVE_INFINITY;
  private audioSyncPending = false;

  public constructor(host: HTMLElement) {
    const performanceMetrics = this.performanceMetrics;
    this.view = new WorldView(host);
    this.ui = new ClientUi(host, this.localizer, performanceMetrics !== null);
    this.connection = new MachiVerseConnection(
      this.config.serverUrl,
      { minimumDelayMs: this.config.reconnectMinimumDelayMs, maximumDelayMs: this.config.reconnectMaximumDelayMs },
      {
        onStateChanged: (state) => this.ui.setConnectionState(state),
        onMessage: (message) => this.handleProtocolMessage(message),
        onProtocolError: (message) => this.handleProtocolError(message),
        onClientError: (error) => this.ui.showError(this.localizer.t('error.client', { detail: error.message })),
        onDisconnected: () => { this.store.clear(); this.view.clearRoadNetwork(); this.ui.setAgentCount(0); this.ui.setProtocol(null); },
        onHelloAck: (version) => { this.ui.clearError(); this.ui.setProtocol(version); },
        ...(performanceMetrics === null ? {} : { onFrameDecoded: (metrics: { readonly frameBytes: number; readonly decodeTimeMs: number }) => performanceMetrics.recordDecode(metrics.frameBytes, metrics.decodeTimeMs) }),
      },
    );
    this.audio.onStateChanged((state) => this.ui.setAudioState(state));
    this.ui.onAudioUnlock(() => { void this.audio.unlock().catch((error: unknown) => { const detail = error instanceof Error ? error.message : String(error); this.ui.showError(this.localizer.t('error.client', { detail })); }); });
    window.addEventListener('resize', this.handleResize);
  }

  public start(): void { this.connection.connect(); this.animationFrame = window.requestAnimationFrame(this.animate); }
  public dispose(): void { window.cancelAnimationFrame(this.animationFrame); window.removeEventListener('resize', this.handleResize); this.connection.disconnect(); this.audio.dispose(); this.view.dispose(); }
  private readonly handleResize = (): void => { this.view.resize(); };

  private readonly animate = (now: number): void => {
    const performanceMetrics = this.performanceMetrics;
    if (performanceMetrics !== null) performanceMetrics.recordAnimationFrame(now);
    this.updateSubscription(now);
    this.view.render(this.store, now);
    this.audio.syncListenerFromCamera(this.view.camera);
    this.updateAudio(now);
    if (performanceMetrics !== null) this.updatePerformanceUi(now, performanceMetrics);
    this.animationFrame = window.requestAnimationFrame(this.animate);
  };

  private updateSubscription(now: number): void {
    if (now - this.lastSubscriptionAt < this.config.subscriptionRefreshMs) return;
    this.lastSubscriptionAt = now;
    const volume = this.view.getSubscriptionVolume();
    if (this.lastSubscription !== null && volumesNearlyEqual(this.lastSubscription, volume)) return;
    this.lastSubscription = volume;
    this.connection.setSubscription(volume);
  }

  private updateAudio(now: number): void {
    if (this.audioSyncPending || now - this.lastAudioSyncAt < 200) return;
    this.lastAudioSyncAt = now; this.audioSyncPending = true;
    const listener = this.view.getListenerPosition();
    void Promise.all([this.audio.syncSpatialVoices(listener), this.ambient.update(listener)])
      .catch((error: unknown) => { const detail = error instanceof Error ? error.message : String(error); this.ui.showError(this.localizer.t('error.client', { detail })); })
      .finally(() => { this.audioSyncPending = false; });
  }

  private updatePerformanceUi(now: number, metrics: ClientPerformanceMetrics): void {
    if (now - this.lastPerformanceUiAt < 500) return;
    this.lastPerformanceUiAt = now; this.ui.setPerformanceMetrics(metrics.snapshot());
  }

  private handleProtocolMessage(message: ProtocolMessage): void {
    switch (message.type) {
      case MessageType.AgentSpawn: this.applyAgentSpawn(message); return;
      case MessageType.AgentUpdate: this.applyAgentUpdate(message); return;
      case MessageType.AgentRemove: {
        const removed = this.store.remove(message.agentId); this.audio.removeEntity(message.agentId);
        if (removed) this.ui.setAgentCount(this.store.size); return;
      }
      case MessageType.RoadNetworkSnapshot: this.view.applyRoadNetwork(message); return;
      case MessageType.Hello:
      case MessageType.HelloAck:
      case MessageType.SubscribeVolume:
      case MessageType.Error:
        return;
    }
  }

  private applyAgentSpawn(message: AgentStateMessage): void {
    const previousSize = this.store.size; this.store.spawn(message); this.updateEntityAudioPosition(message);
    if (this.store.size !== previousSize) this.ui.setAgentCount(this.store.size);
  }
  private applyAgentUpdate(message: AgentStateMessage): void { if (!this.store.update(message)) { this.store.spawn(message); this.ui.setAgentCount(this.store.size); } this.updateEntityAudioPosition(message); }
  private updateEntityAudioPosition(message: AgentStateMessage): void { if (this.audio.hasEntityEmitters(message.agentId)) this.audio.updateEntityPosition(message.agentId, { x: message.x, y: message.y, z: message.z }); }
  private handleProtocolError(message: ProtocolErrorMessage): void {
    const parameters: Record<string, string> = {}; for (const parameter of message.parameters) parameters[parameter.key] = parameter.value; parameters.code = String(message.code);
    const key = `error.protocol.${String(message.code)}`; const localized = this.localizer.t(key, parameters as LocaleParameters);
    this.ui.showError(localized === key ? this.localizer.t('error.protocol.unknown', { code: message.code }) : localized);
  }
}

function volumesNearlyEqual(left: WorldVolume, right: WorldVolume): boolean {
  const epsilon = 0.5;
  return Math.abs(left.minX - right.minX) < epsilon && Math.abs(left.minY - right.minY) < epsilon && Math.abs(left.minZ - right.minZ) < epsilon && Math.abs(left.maxX - right.maxX) < epsilon && Math.abs(left.maxY - right.maxY) < epsilon && Math.abs(left.maxZ - right.maxZ) < epsilon;
}
