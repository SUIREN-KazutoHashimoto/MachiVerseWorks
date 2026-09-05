import { AmbientSystem } from './ambient-system.ts';
import { AudioEngine } from './audio-engine.ts';
import { ClientPerformanceMetrics } from './client-performance.ts';
import { loadClientConfig } from './config.ts';
import { MachiVerseConnection } from './connection.ts';
import { initializeLocalization, type LocaleParameters } from './localization.ts';
import { MultimodalTransitMessageType, type MultimodalTransitProtocolMessage, type MultimodalTransitSnapshotMessage } from './multimodal-transit.ts';
import { PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE, type PersistentRegionalEvolutionSnapshotMessage } from './persistent-regional-evolution-protocol.ts';
import { PopulationMessageType, type PopulationProtocolMessage, type PopulationStatisticsMessage, type PersonDebugMessage } from './population-protocol.ts';
import { MessageType, ProtocolErrorCode, type AgentStateMessage, type ProtocolErrorMessage, type ProtocolMessage, type WorldVolume } from './protocol.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE, type RegionalGenerationSnapshotMessage } from './regional-generation-protocol.ts';
import { RailwayInfrastructureLayer, RailwayMessageType, type RailwayProtocolMessage } from './railway-infrastructure.ts';
import { RailwayOperationsLayer, RailwayOperationsMessageType, type RailwayOperationsProtocolMessage, type RailwayOperationsSnapshotMessage } from './railway-operations.ts';
import { SettlementStructureRenderer } from './settlement-structure-renderer.ts';
import { isRetryableSubscriptionDetailCode } from './subscription-error-policy.ts';
import { ClientUi } from './ui.ts';
import { TrafficMessageType, type TrafficProtocolMessage } from './traffic-protocol.ts';
import { ViewNavigationController, createStaticNavigationTarget, getCameraFocusAtSimulationAltitude, type ViewNavigationTarget } from './view-navigation.ts';
import { WorldView } from './world-view.ts';
import { ECONOMY_SNAPSHOT_MESSAGE_TYPE, type EconomyProtocolMessage, type EconomySnapshotMessage } from './economy-protocol.ts';
import { LogisticsDebugOverlay } from './logistics-debug.ts';
import { LOGISTICS_SNAPSHOT_MESSAGE_TYPE, type LogisticsProtocolMessage, type LogisticsSnapshotMessage } from './logistics-protocol.ts';
import { PowerDebugOverlay } from './power-debug.ts';
import { POWER_SNAPSHOT_MESSAGE_TYPE, type PowerProtocolMessage, type PowerSnapshotMessage } from './power-protocol.ts';
import { WaterSewerDebugOverlay } from './water-sewer-debug.ts';
import { WATER_SEWER_SNAPSHOT_MESSAGE_TYPE, type WaterSewerProtocolMessage, type WaterSewerSnapshotMessage } from './water-sewer-protocol.ts';
import { GasDebugOverlay } from './gas-debug.ts';
import { GAS_SNAPSHOT_MESSAGE_TYPE, type GasProtocolMessage, type GasSnapshotMessage } from './gas-protocol.ts';
import { OpticalDebugOverlay } from './optical-debug.ts';
import { OPTICAL_SNAPSHOT_MESSAGE_TYPE, type OpticalProtocolMessage, type OpticalSnapshotMessage } from './optical-protocol.ts';
import { RadioDebugOverlay } from './radio-debug.ts';
import { RADIO_SNAPSHOT_MESSAGE_TYPE, SPECTRUM_SNAPSHOT_MESSAGE_TYPE, type RadioProtocolMessage, type RadioSnapshotMessage, type SpectrumSnapshotMessage } from './radio-protocol.ts';
import { ViewObservationState, type ReadonlyViewObservationState } from './view-observation-state.ts';
import { WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE, type WorldEnvironmentSnapshotMessage } from './world-environment-protocol.ts';

export class Application {
  private readonly localizer = initializeLocalization();
  private readonly config = loadClientConfig();
  private readonly observation = new ViewObservationState();
  private readonly audio = new AudioEngine();
  private readonly ambient = new AmbientSystem(this.audio);
  private readonly performanceMetrics = import.meta.env.DEV ? new ClientPerformanceMetrics() : null;
  private readonly view: WorldView;
  private readonly navigation: ViewNavigationController;
  private readonly regionalGeneration: SettlementStructureRenderer;
  private readonly railway: RailwayInfrastructureLayer;
  private readonly railwayOperations: RailwayOperationsLayer;
  private readonly ui: ClientUi;
  private readonly logisticsDebug: LogisticsDebugOverlay;
  private readonly powerDebug: PowerDebugOverlay;
  private readonly waterSewerDebug: WaterSewerDebugOverlay;
  private readonly gasDebug: GasDebugOverlay;
  private readonly opticalDebug: OpticalDebugOverlay;
  private readonly radioDebug: RadioDebugOverlay;
  private readonly connection: MachiVerseConnection;
  private animationFrame = 0;
  private lastSubscriptionAt = Number.NEGATIVE_INFINITY;
  private lastSubscription: WorldVolume | null = null;
  private lastAudioSyncAt = Number.NEGATIVE_INFINITY;
  private lastPerformanceUiAt = Number.NEGATIVE_INFINITY;
  private audioSyncPending = false;
  private started = false;
  private terrainCameraInitialized = false;
  private regionalCameraInitialized = false;
  private disposed = false;

  public constructor(host: HTMLElement) {
    const performanceMetrics = this.performanceMetrics;
    this.view = new WorldView(host);
    this.navigation = new ViewNavigationController(this.view.camera, this.view.renderer.domElement);
    this.regionalGeneration = new SettlementStructureRenderer(this.view.scene);
    this.railway = new RailwayInfrastructureLayer(this.view.scene);
    this.railwayOperations = new RailwayOperationsLayer(this.view.scene);
    this.ui = new ClientUi(host, this.localizer, performanceMetrics !== null);
    this.logisticsDebug = new LogisticsDebugOverlay(host, this.localizer);
    this.powerDebug = new PowerDebugOverlay(host, this.localizer);
    this.waterSewerDebug = new WaterSewerDebugOverlay(host);
    this.gasDebug = new GasDebugOverlay(host, this.localizer);
    this.opticalDebug = new OpticalDebugOverlay(host, this.localizer);
    this.radioDebug = new RadioDebugOverlay(host, this.localizer);
    this.connection = new MachiVerseConnection(
      this.config.serverUrl,
      { minimumDelayMs: this.config.reconnectMinimumDelayMs, maximumDelayMs: this.config.reconnectMaximumDelayMs },
      {
        onStateChanged: (state) => this.ui.setConnectionState(state),
        onMessage: (message) => this.handleProtocolMessage(message),
        onProtocolError: (message) => this.handleProtocolError(message),
        onClientError: (error) => this.ui.showError(this.localizer.t('error.client', { detail: error.message })),
        onDisconnected: () => {
          this.observation.resetConnectionState(); this.railway.clear(); this.railwayOperations.clear();
          this.ui.setAgentCount(0); this.ui.clearPopulation(); this.ui.clearRailwayOperations(); this.ui.clearMultimodalTransit(); this.ui.clearEconomy();
          this.logisticsDebug.clear(); this.powerDebug.clear(); this.waterSewerDebug.clear(); this.gasDebug.clear(); this.opticalDebug.clear(); this.radioDebug.clear(); this.ui.setProtocol(null);
        },
        onHelloAck: (version) => { this.ui.clearError(); this.ui.setProtocol(version); },
        ...(performanceMetrics === null ? {} : { onFrameDecoded: (metrics: { readonly frameBytes: number; readonly decodeTimeMs: number }) => performanceMetrics.recordDecode(metrics.frameBytes, metrics.decodeTimeMs) }),
      },
    );
    this.audio.onStateChanged((state) => this.ui.setAudioState(state));
    this.ui.onAudioUnlock(() => { void this.audio.unlock().catch((error: unknown) => { const detail = error instanceof Error ? error.message : String(error); this.ui.showError(this.localizer.t('error.client', { detail })); }); });
    this.ui.onInspectPerson((personId) => { this.connection.inspectPerson(personId); });
    this.ui.onClearPersonInspection(() => { this.connection.clearPersonInspection(); });
    window.addEventListener('resize', this.handleResize);
  }

  public get state(): ReadonlyViewObservationState { return this.observation; }

  public jumpTo(target: ViewNavigationTarget): boolean { return this.navigation.jump(target); }
  public focus(target: ViewNavigationTarget): boolean { return this.navigation.focus(target); }
  public follow(target: ViewNavigationTarget): boolean { return this.navigation.follow(target); }
  public clearFollow(): void { this.navigation.clearFollow(); }
  public focusEntity(entityId: bigint, preferredZoom?: number): boolean { return this.navigation.focusEntity(entityId, this.observation.entities, performance.now(), preferredZoom); }
  public followEntity(entityId: bigint, preferredZoom?: number): boolean { return this.navigation.followEntity(entityId, this.observation.entities, performance.now(), preferredZoom); }

  public start(): void {
    if (this.disposed) throw new Error('Application is disposed.');
    if (this.started) return;
    this.started = true;
    try {
      this.connection.connect();
      this.animationFrame = window.requestAnimationFrame(this.animate);
    } catch (error) {
      this.started = false;
      throw error;
    }
  }
  public dispose(): void {
    if (this.disposed) return;
    this.started = false; this.disposed = true; window.cancelAnimationFrame(this.animationFrame); window.removeEventListener('resize', this.handleResize); this.connection.disconnect(); this.navigation.dispose(); this.audio.dispose(); this.regionalGeneration.dispose(); this.railway.dispose(); this.railwayOperations.dispose(); this.logisticsDebug.dispose(); this.powerDebug.dispose(); this.waterSewerDebug.dispose(); this.gasDebug.dispose(); this.opticalDebug.dispose(); this.radioDebug.dispose(); this.view.dispose(); this.view.renderer.domElement.remove(); this.ui.dispose();
  }
  private readonly handleResize = (): void => { this.view.resize(); };

  private readonly animate = (now: number): void => {
    if (this.disposed || !this.started) return;
    const performanceMetrics = this.performanceMetrics; if (performanceMetrics !== null) performanceMetrics.recordAnimationFrame(now);
    this.navigation.update(now); this.updateSubscription(now); this.regionalGeneration.update(this.observation.regionalGeneration, this.observation.persistentRegionalEvolution); this.view.render(this.observation.entities, now, this.observation.pedestrians, this.observation.vehicles, this.observation.intersections, this.observation.roadNetwork, this.observation.worldEnvironment); this.audio.syncListenerFromCamera(this.view.camera); this.updateAudio(now); if (performanceMetrics !== null) this.updatePerformanceUi(now, performanceMetrics); this.animationFrame = window.requestAnimationFrame(this.animate);
  };

  private updateSubscription(now: number): void {
    if (now - this.lastSubscriptionAt < this.config.subscriptionRefreshMs) return; this.lastSubscriptionAt = now; const volume = this.view.getSubscriptionVolume(); if (this.lastSubscription !== null && volumesNearlyEqual(this.lastSubscription, volume)) return; this.connection.setSubscription(volume); this.lastSubscription = volume;
  }

  private updateAudio(now: number): void {
    if (this.audioSyncPending || now - this.lastAudioSyncAt < 200) return; this.lastAudioSyncAt = now; this.audioSyncPending = true; const listener = this.view.getListenerPosition();
    void Promise.all([this.audio.syncSpatialVoices(listener), this.ambient.update(listener)]).catch((error: unknown) => { if (this.disposed) return; const detail = error instanceof Error ? error.message : String(error); this.ui.showError(this.localizer.t('error.client', { detail })); }).finally(() => { this.audioSyncPending = false; });
  }

  private updatePerformanceUi(now: number, metrics: ClientPerformanceMetrics): void { if (now - this.lastPerformanceUiAt < 500) return; this.lastPerformanceUiAt = now; this.ui.setPerformanceMetrics(metrics.snapshot()); }

  private handleProtocolMessage(message: ProtocolMessage | TrafficProtocolMessage | PopulationProtocolMessage | RailwayProtocolMessage | RailwayOperationsProtocolMessage | MultimodalTransitProtocolMessage | EconomyProtocolMessage | LogisticsProtocolMessage | PowerProtocolMessage | WaterSewerProtocolMessage | GasProtocolMessage | OpticalProtocolMessage | RadioProtocolMessage | WorldEnvironmentSnapshotMessage | RegionalGenerationSnapshotMessage | PersistentRegionalEvolutionSnapshotMessage): void {
    switch (message.type) {
      case MessageType.AgentSpawn:
      case MessageType.AgentUpdate:
        this.observation.apply(message); this.updateEntityAudioPosition(message); this.ui.setAgentCount(this.observation.entities.size); return;
      case MessageType.AgentRemove:
        this.observation.apply(message); this.audio.removeEntity(message.agentId); this.ui.setAgentCount(this.observation.entities.size); return;
      case MessageType.PedestrianSpawn:
      case MessageType.PedestrianUpdate:
      case MessageType.PedestrianRemove:
        this.observation.apply(message); return;
      case MessageType.RoadNetworkSnapshot:
        this.observation.apply(message); return;
      case TrafficMessageType.VehicleSpawn:
      case TrafficMessageType.VehicleUpdate:
      case TrafficMessageType.VehicleRemove:
      case TrafficMessageType.IntersectionControlSnapshot:
        this.observation.apply(message); return;
      case WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE:
        this.observation.apply(message); this.initializeTerrainCamera(); return;
      case REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE:
        this.observation.apply(message); this.initializeRegionalCamera(message); return;
      case PERSISTENT_REGIONAL_EVOLUTION_SNAPSHOT_MESSAGE_TYPE:
        this.observation.apply(message); return;
      case PopulationMessageType.PopulationStatistics: this.applyPopulationStatistics(message); return;
      case PopulationMessageType.PersonDebug: this.applyPersonDebug(message); return;
      case RailwayMessageType.RailwayInfrastructureSnapshot: this.railway.apply(message); return;
      case RailwayOperationsMessageType.RailwayOperationsSnapshot: this.applyRailwayOperations(message); return;
      case MultimodalTransitMessageType.MultimodalTransitSnapshot: this.applyMultimodalTransit(message); return;
      case ECONOMY_SNAPSHOT_MESSAGE_TYPE: this.applyEconomy(message); return;
      case LOGISTICS_SNAPSHOT_MESSAGE_TYPE: this.applyLogistics(message); return;
      case POWER_SNAPSHOT_MESSAGE_TYPE: this.applyPower(message); return;
      case WATER_SEWER_SNAPSHOT_MESSAGE_TYPE: this.applyWaterSewer(message); return;
      case GAS_SNAPSHOT_MESSAGE_TYPE: this.applyGas(message); return;
      case OPTICAL_SNAPSHOT_MESSAGE_TYPE: this.applyOptical(message); return;
      case RADIO_SNAPSHOT_MESSAGE_TYPE: this.applyRadio(message); return;
      case SPECTRUM_SNAPSHOT_MESSAGE_TYPE: this.applySpectrum(message); return;
      case MessageType.Hello:
      case MessageType.HelloAck:
      case MessageType.SubscribeVolume:
      case MessageType.Error: return;
    }
  }

  private initializeTerrainCamera(): void {
    if (this.terrainCameraInitialized) return;
    const focus = getCameraFocusAtSimulationAltitude(this.view.camera, 0);
    if (focus === undefined) return;
    const elevation = this.observation.worldEnvironment.getNearestTerrainElevation(focus.x, focus.y);
    if (elevation === undefined || !this.navigation.rebaseFocusAltitude(0, elevation)) return;
    this.terrainCameraInitialized = true;
    this.lastSubscription = null;
    this.lastSubscriptionAt = Number.NEGATIVE_INFINITY;
  }

  private initializeRegionalCamera(message: RegionalGenerationSnapshotMessage): void {
    if (this.regionalCameraInitialized || message.settlements.length === 0) return;
    const settlement = [...message.settlements].sort((left, right) =>
      right.population - left.population
        || (left.settlementId < right.settlementId ? -1 : left.settlementId > right.settlementId ? 1 : 0))[0]!;
    if (!this.navigation.focus(createStaticNavigationTarget(
      'position',
      'initial-regional-settlement',
      { x: settlement.x, y: settlement.y, z: settlement.z },
      0.45,
    ))) return;
    this.regionalCameraInitialized = true;
    this.lastSubscription = null;
    this.lastSubscriptionAt = Number.NEGATIVE_INFINITY;
  }

  private applyPopulationStatistics(message: PopulationStatisticsMessage): void { this.ui.setPopulationStatistics(message); }
  private applyPersonDebug(message: PersonDebugMessage): void { this.ui.setPersonDebug(message); }
  private applyRailwayOperations(message: RailwayOperationsSnapshotMessage): void { this.railwayOperations.apply(message); this.ui.setRailwayOperations(message); }
  private applyMultimodalTransit(message: MultimodalTransitSnapshotMessage): void { this.ui.setMultimodalTransit(message); }
  private applyEconomy(message: EconomySnapshotMessage): void { this.ui.setEconomy(message); }
  private applyLogistics(message: LogisticsSnapshotMessage): void { this.logisticsDebug.apply(message); }
  private applyPower(message: PowerSnapshotMessage): void { this.powerDebug.apply(message); }
  private applyWaterSewer(message: WaterSewerSnapshotMessage): void { this.waterSewerDebug.apply(message); }
  private applyGas(message: GasSnapshotMessage): void { this.gasDebug.apply(message); }
  private applyOptical(message: OpticalSnapshotMessage): void { this.opticalDebug.apply(message); }
  private applyRadio(message: RadioSnapshotMessage): void { this.radioDebug.applyRadio(message); }
  private applySpectrum(message: SpectrumSnapshotMessage): void { this.radioDebug.applySpectrum(message); }
  private updateEntityAudioPosition(message: AgentStateMessage): void { if (this.audio.hasEntityEmitters(message.agentId)) this.audio.updateEntityPosition(message.agentId, { x: message.x, y: message.y, z: message.z }); }

  private handleProtocolError(message: ProtocolErrorMessage): void {
    const parameters: Record<string, string> = {}; for (const parameter of message.parameters) parameters[parameter.key] = parameter.value;
    if (message.code === ProtocolErrorCode.InvalidRequest && isRetryableSubscriptionDetailCode(parameters.detailCode) && this.view.zoomInForSubscriptionRetry()) { this.lastSubscription = null; this.lastSubscriptionAt = Number.NEGATIVE_INFINITY; this.ui.clearError(); return; }
    parameters.code = String(message.code); const key = `error.protocol.${String(message.code)}`; const localized = this.localizer.t(key, parameters as LocaleParameters); this.ui.showError(localized === key ? this.localizer.t('error.protocol.unknown', { code: message.code }) : localized);
  }
}

function volumesNearlyEqual(left: WorldVolume, right: WorldVolume): boolean {
  const epsilon = 0.5; return Math.abs(left.minX - right.minX) < epsilon && Math.abs(left.minY - right.minY) < epsilon && Math.abs(left.minZ - right.minZ) < epsilon && Math.abs(left.maxX - right.maxX) < epsilon && Math.abs(left.maxY - right.maxY) < epsilon && Math.abs(left.maxZ - right.maxZ) < epsilon;
}
