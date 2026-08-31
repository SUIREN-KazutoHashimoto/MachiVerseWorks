import type { AudioEngineState } from './audio-engine.ts';
import type { ClientPerformanceSnapshot } from './client-performance.ts';
import type { ConnectionState } from './connection.ts';
import type { Localizer } from './localization.ts';
import { ActivityKind, PersonTravelState, type PersonDebugMessage, type PopulationStatisticsMessage } from './population-protocol.ts';
import { TransitMode, TransitVehicleKind, TransitVehicleState, type MultimodalTransitSnapshotMessage } from './multimodal-transit.ts';
import { protocolVersionToString, type ProtocolVersion } from './protocol.ts';
import { RailwayServiceState, type RailwayOperationsSnapshotMessage } from './railway-operations.ts';

export class AgentCountFormatter {
  private readonly formatter: Intl.NumberFormat;
  private lastCount: number | null = null;

  public constructor(locale: string) { this.formatter = new Intl.NumberFormat(locale); }

  public formatIfChanged(count: number): string | null {
    if (this.lastCount === count) return null;
    this.lastCount = count;
    return this.formatter.format(count);
  }
}

export class ClientUi {
  private readonly connectionValue = document.createElement('span');
  private readonly protocolValue = document.createElement('span');
  private readonly agentsValue = document.createElement('span');
  private readonly populationValue = document.createElement('span');
  private readonly trainsValue = document.createElement('span');
  private readonly railwayDebugValue = document.createElement('div');
  private readonly transitDebugValue = document.createElement('div');
  private readonly audioValue = document.createElement('span');
  private readonly decodeValue: HTMLSpanElement | null;
  private readonly frameValue: HTMLSpanElement | null;
  private readonly errorValue = document.createElement('div');
  private readonly audioButton = document.createElement('button');
  private readonly personIdInput = document.createElement('input');
  private readonly inspectPersonButton = document.createElement('button');
  private readonly clearPersonButton = document.createElement('button');
  private readonly personDebugValue = document.createElement('div');
  private readonly agentCountFormatter: AgentCountFormatter;
  private readonly ownedRoots: HTMLElement[] = [];
  private readonly eventCleanup: Array<() => void> = [];
  private disposed = false;

  public constructor(host: HTMLElement, private readonly localizer: Localizer, showPerformanceOverlay = false) {
    this.agentCountFormatter = new AgentCountFormatter(localizer.locale);
    const panel = document.createElement('section');
    panel.className = 'status-panel';
    panel.setAttribute('aria-live', 'polite');
    panel.append(
      this.createStatusRow('status.connection', this.connectionValue),
      this.createStatusRow('status.protocol', this.protocolValue),
      this.createStatusRow('status.agents', this.agentsValue),
      this.createStatusRow('status.population', this.populationValue),
      this.createStatusRow('status.trains', this.trainsValue),
      this.createStatusRow('status.audio', this.audioValue),
    );

    this.audioButton.className = 'audio-unlock';
    this.audioButton.type = 'button';
    this.audioButton.textContent = localizer.t('audio.unlock');
    panel.append(this.audioButton);

    const inspector = document.createElement('div');
    inspector.className = 'person-debug';
    const inspectorTitle = document.createElement('strong');
    inspectorTitle.textContent = localizer.t('personDebug.title');
    const controls = document.createElement('div');
    controls.className = 'person-debug-controls';
    this.personIdInput.type = 'number';
    this.personIdInput.min = '1';
    this.personIdInput.step = '1';
    this.personIdInput.inputMode = 'numeric';
    this.personIdInput.placeholder = localizer.t('personDebug.personId');
    this.inspectPersonButton.type = 'button';
    this.inspectPersonButton.textContent = localizer.t('personDebug.inspect');
    this.clearPersonButton.type = 'button';
    this.clearPersonButton.textContent = localizer.t('personDebug.clear');
    controls.append(this.personIdInput, this.inspectPersonButton, this.clearPersonButton);
    this.personDebugValue.className = 'person-debug-value';
    this.personDebugValue.textContent = localizer.t('personDebug.none');
    inspector.append(inspectorTitle, controls, this.personDebugValue);
    panel.append(inspector);

    const railwayDebug = document.createElement('div');
    railwayDebug.className = 'railway-debug';
    const railwayDebugTitle = document.createElement('strong');
    railwayDebugTitle.textContent = localizer.t('railwayDebug.title');
    this.railwayDebugValue.className = 'railway-debug-value';
    railwayDebug.append(railwayDebugTitle, this.railwayDebugValue);
    panel.append(railwayDebug);

    const transitDebug = document.createElement('div');
    transitDebug.className = 'transit-debug';
    const transitDebugTitle = document.createElement('strong');
    transitDebugTitle.textContent = localizer.t('transitDebug.title');
    this.transitDebugValue.className = 'transit-debug-value';
    transitDebug.append(transitDebugTitle, this.transitDebugValue);
    panel.append(transitDebug);

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
      performancePanel.append(title, this.createStatusRow('status.decode', this.decodeValue), this.createStatusRow('status.frame', this.frameValue));
      host.append(performancePanel);
      this.ownedRoots.push(performancePanel);
    } else {
      this.decodeValue = null;
      this.frameValue = null;
    }

    const hint = document.createElement('div');
    hint.className = 'camera-hint';
    hint.textContent = localizer.t('hint.camera');
    host.append(panel, hint);
    this.ownedRoots.push(panel, hint);

    this.setConnectionState('disconnected');
    this.setProtocol(null);
    this.setAgentCount(0);
    this.clearPopulation();
    this.clearRailwayOperations();
    this.clearMultimodalTransit();
    this.setAudioState('locked');
    if (this.decodeValue !== null) this.decodeValue.textContent = '—';
    if (this.frameValue !== null) this.frameValue.textContent = '—';
  }

  public onAudioUnlock(handler: () => void): void {
    this.audioButton.addEventListener('click', handler);
    this.eventCleanup.push(() => this.audioButton.removeEventListener('click', handler));
  }

  public onInspectPerson(handler: (personId: bigint) => void): void {
    const inspect = (): void => {
      try {
        const value = this.personIdInput.value.trim();
        if (!/^\d+$/.test(value)) return;
        const personId = BigInt(value);
        if (personId > 0n) handler(personId);
      } catch {
      }
    };
    const keydown = (event: KeyboardEvent): void => { if (event.key === 'Enter') inspect(); };
    this.inspectPersonButton.addEventListener('click', inspect);
    this.personIdInput.addEventListener('keydown', keydown);
    this.eventCleanup.push(
      () => this.inspectPersonButton.removeEventListener('click', inspect),
      () => this.personIdInput.removeEventListener('keydown', keydown),
    );
  }

  public onClearPersonInspection(handler: () => void): void {
    const clear = (): void => {
      this.personIdInput.value = '';
      this.personDebugValue.textContent = this.localizer.t('personDebug.none');
      handler();
    };
    this.clearPersonButton.addEventListener('click', clear);
    this.eventCleanup.push(() => this.clearPersonButton.removeEventListener('click', clear));
  }

  public dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    for (const cleanup of this.eventCleanup.splice(0)) cleanup();
    for (const root of this.ownedRoots.splice(0)) root.remove();
  }

  public setConnectionState(state: ConnectionState): void { this.connectionValue.textContent = this.localizer.t(`connection.${state}`); this.connectionValue.dataset.state = state; }
  public setProtocol(version: ProtocolVersion | null): void { this.protocolValue.textContent = version === null ? '—' : protocolVersionToString(version); }
  public setAgentCount(count: number): void { const formatted = this.agentCountFormatter.formatIfChanged(count); if (formatted !== null) this.agentsValue.textContent = formatted; }

  public setPopulationStatistics(message: PopulationStatisticsMessage): void {
    this.populationValue.textContent = this.localizer.t('population.summary', { households: message.householdCount, persons: message.personCount, walking: message.walkingCount, driving: message.drivingCount });
  }

  public clearPopulation(): void { this.populationValue.textContent = '—'; this.personDebugValue.textContent = this.localizer.t('personDebug.none'); }

  public setRailwayOperations(message: RailwayOperationsSnapshotMessage): void {
    this.trainsValue.textContent = String(message.trains.length);
    const delayed = message.services.filter((service) => service.delayTicks > 0n).length;
    const completed = message.services.filter((service) => service.state === RailwayServiceState.Completed).length;
    const timetableById = new Map(message.timetables.map((timetable) => [timetable.id, timetable] as const));
    const arrivals: string[] = [];
    for (const service of message.services) {
      if (service.state === RailwayServiceState.Completed) continue;
      const timetable = timetableById.get(service.timetableId);
      const stop = timetable?.stops[service.nextStopIndex];
      if (stop === undefined) continue;
      arrivals.push(`S${stop.stationId.toString()}@${(stop.plannedArrivalTick + service.delayTicks).toString()}`);
    }
    this.railwayDebugValue.textContent = this.localizer.t('railwayDebug.summary', { delayed, completed, arrivals: arrivals.length === 0 ? '—' : arrivals.join(', ') });
  }

  public clearRailwayOperations(): void { this.trainsValue.textContent = '0'; this.railwayDebugValue.textContent = this.localizer.t('railwayDebug.none'); }

  public setMultimodalTransit(message: MultimodalTransitSnapshotMessage): void {
    const busLines = message.lines.filter((line) => line.mode === TransitMode.Bus).length;
    const railwayLines = message.lines.filter((line) => line.mode === TransitMode.Railway).length;
    const buses = message.vehicles.filter((vehicle) => vehicle.kind === TransitVehicleKind.Bus && vehicle.state !== TransitVehicleState.Completed).length;
    const taxis = message.vehicles.filter((vehicle) => vehicle.kind === TransitVehicleKind.Taxi && vehicle.state !== TransitVehicleState.Completed).length;
    const routes = message.patterns.map((pattern) => `L${pattern.lineId.toString()}:${pattern.stops.map((stop) => stop.stopId.toString()).join('>')}`).join(', ');
    const vehicles = message.vehicles.slice(0, 6).map((vehicle) => `V${vehicle.id.toString()}@(${vehicle.x.toFixed(1)},${vehicle.y.toFixed(1)})`).join(', ');
    const arrivals = message.arrivalEstimates.slice(0, 6).map((arrival) => `S${arrival.stopId.toString()}@${arrival.estimatedArrivalTick.toString()}`).join(', ');
    this.transitDebugValue.textContent = this.localizer.t('transitDebug.summary', { routes: routes || '—', stops: message.stops.length, busLines, railwayLines, buses, taxis, vehicles: vehicles || '—', arrivals: arrivals || '—' });
  }

  public clearMultimodalTransit(): void { this.transitDebugValue.textContent = this.localizer.t('transitDebug.none'); }

  public setPersonDebug(message: PersonDebugMessage): void {
    const residence = formatEndpoint(message.residenceBuildingId, message.residencePoiId);
    const current = formatEndpoint(message.currentBuildingId, message.currentPoiId);
    const destination = formatEndpoint(message.destinationBuildingId, message.destinationPoiId);
    this.personDebugValue.textContent = this.localizer.t('personDebug.summary', { id: message.personId, household: message.householdId, residence, current, destination, activity: activityLabel(this.localizer, message.currentActivity), travel: travelStateLabel(this.localizer, message.travelState) });
  }

  public setAudioState(state: AudioEngineState): void { this.audioValue.textContent = this.localizer.t(`audio.${state}`); this.audioButton.hidden = state === 'running' || state === 'unavailable'; }
  public setPerformanceMetrics(metrics: ClientPerformanceSnapshot): void { if (this.decodeValue !== null) this.decodeValue.textContent = metrics.decodeSampleCount === 0 ? '—' : this.formatTiming(metrics.decodeAverageMs, metrics.decodeP95Ms, metrics.decodeMaximumMs); if (this.frameValue !== null) this.frameValue.textContent = metrics.frameSampleCount === 0 ? '—' : this.formatTiming(metrics.frameAverageMs, metrics.frameP95Ms, metrics.frameMaximumMs); }
  public showError(message: string): void { this.errorValue.textContent = message; this.errorValue.hidden = false; }
  public clearError(): void { this.errorValue.textContent = ''; this.errorValue.hidden = true; }

  private formatTiming(averageMs: number, p95Ms: number, maximumMs: number): string { return this.localizer.t('metrics.time', { average: averageMs.toFixed(3), p95: p95Ms.toFixed(3), maximum: maximumMs.toFixed(3) }); }
  private createStatusRow(labelKey: string, value: HTMLElement): HTMLElement { const row = document.createElement('div'); row.className = 'status-row'; const label = document.createElement('span'); label.className = 'status-label'; label.textContent = this.localizer.t(labelKey); value.className = 'status-value'; row.append(label, value); return row; }
}

function formatEndpoint(buildingId: bigint | null, poiId: bigint | null): string { if (buildingId !== null) return `Building ${buildingId.toString()}`; if (poiId !== null) return `POI ${poiId.toString()}`; return '—'; }
function activityLabel(localizer: Localizer, activity: ActivityKind): string { const key = ActivityKind[activity]?.toLowerCase() ?? 'unknown'; return localizer.t(`activity.${key}`); }
function travelStateLabel(localizer: Localizer, state: PersonTravelState): string { const key = PersonTravelState[state]?.toLowerCase() ?? 'unknown'; return localizer.t(`personTravel.${key}`); }
