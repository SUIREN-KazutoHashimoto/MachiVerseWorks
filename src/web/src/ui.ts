import type { AudioEngineState } from './audio-engine.ts';
import type { ClientPerformanceSnapshot } from './client-performance.ts';
import type { ConnectionState } from './connection.ts';
import type { Localizer } from './localization.ts';
import { protocolVersionToString, type ProtocolVersion } from './protocol.ts';

export class ClientUi {
  private readonly connectionValue = document.createElement('span');
  private readonly protocolValue = document.createElement('span');
  private readonly agentsValue = document.createElement('span');
  private readonly audioValue = document.createElement('span');
  private readonly decodeValue = document.createElement('span');
  private readonly frameValue = document.createElement('span');
  private readonly errorValue = document.createElement('div');
  private readonly audioButton = document.createElement('button');

  public constructor(
    host: HTMLElement,
    private readonly localizer: Localizer,
  ) {
    const panel = document.createElement('section');
    panel.className = 'status-panel';
    panel.setAttribute('aria-live', 'polite');
    panel.append(
      this.createStatusRow('status.connection', this.connectionValue),
      this.createStatusRow('status.protocol', this.protocolValue),
      this.createStatusRow('status.agents', this.agentsValue),
      this.createStatusRow('status.audio', this.audioValue),
      this.createStatusRow('status.decode', this.decodeValue),
      this.createStatusRow('status.frame', this.frameValue),
    );

    this.audioButton.className = 'audio-unlock';
    this.audioButton.type = 'button';
    this.audioButton.textContent = localizer.t('audio.unlock');
    panel.append(this.audioButton);

    this.errorValue.className = 'client-error';
    this.errorValue.hidden = true;
    panel.append(this.errorValue);

    const hint = document.createElement('div');
    hint.className = 'camera-hint';
    hint.textContent = localizer.t('hint.camera');
    host.append(panel, hint);

    this.setConnectionState('disconnected');
    this.setProtocol(null);
    this.setAgentCount(0);
    this.setAudioState('locked');
    this.decodeValue.textContent = '—';
    this.frameValue.textContent = '—';
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
    this.agentsValue.textContent = new Intl.NumberFormat(this.localizer.locale).format(count);
  }

  public setAudioState(state: AudioEngineState): void {
    this.audioValue.textContent = this.localizer.t(`audio.${state}`);
    this.audioButton.hidden = state === 'running' || state === 'unavailable';
  }

  public setPerformanceMetrics(metrics: ClientPerformanceSnapshot): void {
    this.decodeValue.textContent = metrics.decodeSampleCount === 0
      ? '—'
      : this.formatTiming(metrics.decodeAverageMs, metrics.decodeMaximumMs);
    this.frameValue.textContent = metrics.frameSampleCount === 0
      ? '—'
      : this.formatTiming(metrics.frameAverageMs, metrics.frameMaximumMs);
  }

  public showError(message: string): void {
    this.errorValue.textContent = message;
    this.errorValue.hidden = false;
  }

  public clearError(): void {
    this.errorValue.textContent = '';
    this.errorValue.hidden = true;
  }

  private formatTiming(averageMs: number, maximumMs: number): string {
    return this.localizer.t('metrics.time', {
      average: averageMs.toFixed(3),
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
