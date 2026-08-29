import type { AudioEngineState } from './audio-engine.ts';
import type { ClientPerformanceSnapshot } from './client-performance.ts';
import type { ConnectionState } from './connection.ts';
import type { Localizer } from './localization.ts';
import { protocolVersionToString, type ProtocolVersion } from './protocol.ts';

export class AgentCountFormatter {
  private readonly formatter: Intl.NumberFormat;
  private lastCount: number | null = null;

  public constructor(locale: string) {
    this.formatter = new Intl.NumberFormat(locale);
  }

  public formatIfChanged(count: number): string | null {
    if (this.lastCount === count) {
      return null;
    }
    this.lastCount = count;
    return this.formatter.format(count);
  }
}

export class ClientUi {
  private readonly connectionValue = document.createElement('span');
  private readonly protocolValue = document.createElement('span');
  private readonly agentsValue = document.createElement('span');
  private readonly audioValue = document.createElement('span');
  private readonly decodeValue: HTMLSpanElement | null;
  private readonly frameValue: HTMLSpanElement | null;
  private readonly errorValue = document.createElement('div');
  private readonly audioButton = document.createElement('button');
  private readonly agentCountFormatter: AgentCountFormatter;

  public constructor(
    host: HTMLElement,
    private readonly localizer: Localizer,
    showPerformanceOverlay = false,
  ) {
    this.agentCountFormatter = new AgentCountFormatter(localizer.locale);
    const panel = document.createElement('section');
    panel.className = 'status-panel';
    panel.setAttribute('aria-live', 'polite');
    panel.append(
      this.createStatusRow('status.connection', this.connectionValue),
      this.createStatusRow('status.protocol', this.protocolValue),
      this.createStatusRow('status.agents', this.agentsValue),
      this.createStatusRow('status.audio', this.audioValue),
    );

    this.audioButton.className = 'audio-unlock';
    this.audioButton.type = 'button';
    this.audioButton.textContent = localizer.t('audio.unlock');
    panel.append(this.audioButton);

    this.errorValue.className = 'client-error';
    this.errorValue.hidden = true;
    panel.append(this.errorValue);

    if (showPerformanceOverlay) {
      const performancePanel = document.createElement('section');
      performancePanel.className = 'performance-overlay';
      performancePanel.setAttribute('aria-label', localizer.t('performance.title'));
      const title = document.createElement('strong');
      title.className = 'performance-title';
      title.textContent = localizer.t('performance.title');
      this.decodeValue = document.createElement('span');
      this.frameValue = document.createElement('span');
      performancePanel.append(
        title,
        this.createStatusRow('status.decode', this.decodeValue),
        this.createStatusRow('status.frame', this.frameValue),
      );
      host.append(performancePanel);
    } else {
      this.decodeValue = null;
      this.frameValue = null;
    }

    const hint = document.createElement('div');
    hint.className = 'camera-hint';
    hint.textContent = localizer.t('hint.camera');
    host.append(panel, hint);

    this.setConnectionState('disconnected');
    this.setProtocol(null);
    this.setAgentCount(0);
    this.setAudioState('locked');
    if (this.decodeValue !== null) {
      this.decodeValue.textContent = '—';
    }
    if (this.frameValue !== null) {
      this.frameValue.textContent = '—';
    }
  }

  public onAudioUnlock(handler: () => void): void {
    this.audioButton.addEventListener('click', handler);
  }

  public setConnectionState(state: ConnectionState): void {
    this.connectionValue.textContent = this.localizer.t(`connection.${state}`);
    this.connectionValue.dataset.state = state;
  }

  public setProtocol(version: ProtocolVersion | null): void {
    this.protocolValue.textContent = version === null ? '—' : protocolVersionToString(version);
  }

  public setAgentCount(count: number): void {
    const formatted = this.agentCountFormatter.formatIfChanged(count);
    if (formatted !== null) {
      this.agentsValue.textContent = formatted;
    }
  }

  public setAudioState(state: AudioEngineState): void {
    this.audioValue.textContent = this.localizer.t(`audio.${state}`);
    this.audioButton.hidden = state === 'running' || state === 'unavailable';
  }

  public setPerformanceMetrics(metrics: ClientPerformanceSnapshot): void {
    if (this.decodeValue !== null) {
      this.decodeValue.textContent = metrics.decodeSampleCount === 0
        ? '—'
        : this.formatTiming(metrics.decodeAverageMs, metrics.decodeP95Ms, metrics.decodeMaximumMs);
    }
    if (this.frameValue !== null) {
      this.frameValue.textContent = metrics.frameSampleCount === 0
        ? '—'
        : this.formatTiming(metrics.frameAverageMs, metrics.frameP95Ms, metrics.frameMaximumMs);
    }
  }

  public showError(message: string): void {
    this.errorValue.textContent = message;
    this.errorValue.hidden = false;
  }

  public clearError(): void {
    this.errorValue.textContent = '';
    this.errorValue.hidden = true;
  }

  private formatTiming(averageMs: number, p95Ms: number, maximumMs: number): string {
    return this.localizer.t('metrics.time', {
      average: averageMs.toFixed(3),
      p95: p95Ms.toFixed(3),
      maximum: maximumMs.toFixed(3),
    });
  }

  private createStatusRow(labelKey: string, value: HTMLElement): HTMLElement {
    const row = document.createElement('div');
    row.className = 'status-row';
    const label = document.createElement('span');
    label.className = 'status-label';
    label.textContent = this.localizer.t(labelKey);
    value.className = 'status-value';
    row.append(label, value);
    return row;
  }
}
