import { AmbientSystem } from './ambient-system.ts';
import { AudioEngine } from './audio-engine.ts';
import { ClientPerformanceMetrics } from './client-performance.ts';
import { loadClientConfig } from './config.ts';
import { MachiVerseConnection } from './connection.ts';
import { EntityStore } from './entity-store.ts';
import { initializeLocalization, type LocaleParameters } from './localization.ts';
import {
  MessageType,
  type AgentStateMessage,
  type ProtocolErrorMessage,
  type ProtocolMessage,
  type WorldRect,
} from './protocol.ts';
import { ClientUi } from './ui.ts';
import { WorldView } from './world-view.ts';

export class Application {
  private readonly localizer = initializeLocalization();
  private readonly config = loadClientConfig();
  private readonly store = new EntityStore();
  private readonly audio = new AudioEngine();
  private readonly ambient = new AmbientSystem(this.audio);
  private readonly performanceMetrics = new ClientPerformanceMetrics();
  private readonly view: WorldView;
  private readonly ui: ClientUi;
  private readonly connection: MachiVerseConnection;
  private animationFrame = 0;
  private lastSubscriptionAt = Number.NEGATIVE_INFINITY;
  private lastSubscription: WorldRect | null = null;
  private lastAudioSyncAt = Number.NEGATIVE_INFINITY;
  private lastPerformanceUiAt = Number.NEGATIVE_INFINITY;
  private audioSyncPending = false;

  public constructor(host: HTMLElement) {
    this.view = new WorldView(host);
    this.ui = new ClientUi(host, this.localizer, import.meta.env.DEV);
    this.connection = new MachiVerseConnection(
      this.config.serverUrl,
      {
        minimumDelayMs: this.config.reconnectMinimumDelayMs,
        maximumDelayMs: this.config.reconnectMaximumDelayMs,
      },
      {
        onStateChanged: (state) => this.ui.setConnectionState(state),
        onMessage: (message) => this.handleProtocolMessage(message),
        onProtocolError: (message) => this.handleProtocolError(message),
        onClientError: (error) => {
          this.ui.showError(this.localizer.t('error.client', { detail: error.message }));
        },
        onDisconnected: () => {
          this.store.clear();
          this.ui.setAgentCount(0);
          this.ui.setProtocol(null);
        },
        onHelloAck: (version) => {
          this.ui.clearError();
          this.ui.setProtocol(version);
        },
        onFrameDecoded: (metrics) => {
          this.performanceMetrics.recordDecode(metrics.frameBytes, metrics.decodeTimeMs);
        },
      },
    );

    this.audio.onStateChanged((state) => this.ui.setAudioState(state));
    this.ui.onAudioUnlock(() => {
      void this.audio.unlock().catch((error: unknown) => {
        const detail = error instanceof Error ? error.message : String(error);
        this.ui.showError(this.localizer.t('error.client', { detail }));
      });
    });

    window.addEventListener('resize', this.handleResize);
  }

  public start(): void {
    this.connection.connect();
    this.animationFrame = window.requestAnimationFrame(this.animate);
  }

  public dispose(): void {
    window.cancelAnimationFrame(this.animationFrame);
    window.removeEventListener('resize', this.handleResize);
    this.connection.disconnect();
    this.audio.dispose();
    this.view.dispose();
  }

  private readonly handleResize = (): void => {
    this.view.resize();
  };

  private readonly animate = (now: number): void => {
    this.performanceMetrics.recordAnimationFrame(now);
    this.updateSubscription(now);
    this.view.render(this.store, now);
    this.audio.syncListenerFromCamera(this.view.camera);
    this.updateAudio(now);
    this.updatePerformanceUi(now);
    this.animationFrame = window.requestAnimationFrame(this.animate);
  };

  private updateSubscription(now: number): void {
    if (now - this.lastSubscriptionAt < this.config.subscriptionRefreshMs) {
      return;
    }
    this.lastSubscriptionAt = now;
    const area = this.view.getSubscriptionArea();
    if (this.lastSubscription !== null && rectanglesNearlyEqual(this.lastSubscription, area)) {
      return;
    }
    this.lastSubscription = area;
    this.connection.setSubscription(area);
  }

  private updateAudio(now: number): void {
    if (this.audioSyncPending || now - this.lastAudioSyncAt < 200) {
      return;
    }
    this.lastAudioSyncAt = now;
    this.audioSyncPending = true;
    const listener = this.view.getListenerPosition();
    void Promise.all([
      this.audio.syncSpatialVoices(listener),
      this.ambient.update(listener),
    ]).catch((error: unknown) => {
      const detail = error instanceof Error ? error.message : String(error);
      this.ui.showError(this.localizer.t('error.client', { detail }));
    }).finally(() => {
      this.audioSyncPending = false;
    });
  }

  private updatePerformanceUi(now: number): void {
    if (now - this.lastPerformanceUiAt < 500) {
      return;
    }
    this.lastPerformanceUiAt = now;
    this.ui.setPerformanceMetrics(this.performanceMetrics.snapshot());
  }

  private handleProtocolMessage(message: ProtocolMessage): void {
    switch (message.type) {
      case MessageType.AgentSpawn:
        this.applyAgentSpawn(message);
        return;
      case MessageType.AgentUpdate:
        this.applyAgentUpdate(message);
        return;
      case MessageType.AgentRemove:
        this.store.remove(message.agentId);
        this.audio.removeEntity(message.agentId);
        this.ui.setAgentCount(this.store.size);
        return;
      case MessageType.Hello:
      case MessageType.HelloAck:
      case MessageType.SubscribeArea:
      case MessageType.Error:
        return;
    }
  }

  private applyAgentSpawn(message: AgentStateMessage): void {
    this.store.spawn(message);
    this.audio.updateEntityPosition(message.agentId, { x: message.x, y: message.y });
    this.ui.setAgentCount(this.store.size);
  }

  private applyAgentUpdate(message: AgentStateMessage): void {
    if (!this.store.update(message)) {
      this.store.spawn(message);
    }
    this.audio.updateEntityPosition(message.agentId, { x: message.x, y: message.y });
    this.ui.setAgentCount(this.store.size);
  }

  private handleProtocolError(message: ProtocolErrorMessage): void {
    const parameters: Record<string, string> = {};
    for (const parameter of message.parameters) {
      parameters[parameter.key] = parameter.value;
    }
    parameters.code = String(message.code);
    const key = `error.protocol.${String(message.code)}`;
    const localized = this.localizer.t(key, parameters as LocaleParameters);
    this.ui.showError(localized === key
      ? this.localizer.t('error.protocol.unknown', { code: message.code })
      : localized);
  }
}

function rectanglesNearlyEqual(left: WorldRect, right: WorldRect): boolean {
  const epsilon = 0.5;
  return Math.abs(left.minX - right.minX) < epsilon &&
    Math.abs(left.minY - right.minY) < epsilon &&
    Math.abs(left.maxX - right.maxX) < epsilon &&
    Math.abs(left.maxY - right.maxY) < epsilon;
}
