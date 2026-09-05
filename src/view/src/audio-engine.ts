import manifestJson from '../audio/manifest.json' with { type: 'json' };
import { selectVoiceIds, type Point3D, type VoiceCandidate } from './audio-policy.ts';
import { EntityEmitterIndex } from './entity-emitter-index.ts';

export type AudioCategory = 'music' | 'ui' | 'ambient' | 'world' | 'voice';
export type AudioEngineState = 'locked' | 'running' | 'suspended' | 'unavailable';

interface AudioCueDefinition {
  readonly source: string;
  readonly category: AudioCategory;
  readonly spatial: boolean;
  readonly loop: boolean;
  readonly gain: number;
  readonly referenceDistance?: number;
  readonly maximumDistance?: number;
  readonly rolloffFactor?: number;
}

interface AudioManifest {
  readonly version: number;
  readonly defaults: {
    readonly referenceDistance: number;
    readonly maximumDistance: number;
    readonly rolloffFactor: number;
    readonly voiceBudgets: Readonly<Record<AudioCategory, number>>;
  };
  readonly cues: Readonly<Record<string, AudioCueDefinition>>;
}

export interface PlaySoundOptions { readonly gain?: number; readonly position?: Point3D; }
export interface ThreeCameraLike { readonly matrixWorld: { readonly elements: ArrayLike<number> }; }
export interface AudioListenerPose { readonly position: Point3D; readonly direction: Point3D; readonly up: Point3D; }
export interface AudioEmitterOptions { readonly position: Point3D; readonly priority?: number; readonly entityId?: bigint; }

interface VirtualEmitter { readonly id: string; readonly cueId: string; readonly priority: number; readonly entityId?: bigint; position: Point3D; }
interface ActiveEmitterVoice { readonly cueId: string; readonly source: AudioBufferSourceNode; readonly panner: PannerNode; }
interface AmbientVoice { readonly cueId: string; readonly source: AudioBufferSourceNode; readonly gain: GainNode; }

const manifest = manifestJson as AudioManifest;

export class AudioEngine {
  private context: AudioContext | null = null;
  private masterGain: GainNode | null = null;
  private readonly categoryGains = new Map<AudioCategory, GainNode>();
  private readonly categoryVolumes = new Map<AudioCategory, number>([['music', 1], ['ui', 1], ['ambient', 1], ['world', 1], ['voice', 1]]);
  private readonly buffers = new Map<string, Promise<AudioBuffer>>();
  private readonly emitters = new Map<string, VirtualEmitter>();
  private readonly emitterGenerations = new Map<string, number>();
  private readonly emitterIndex = new EntityEmitterIndex();
  private readonly activeEmitterVoices = new Map<string, ActiveEmitterVoice>();
  private readonly ambientVoices = new Map<string, AmbientVoice>();
  private readonly ambientGenerations = new Map<string, number>();
  private muted = false;
  private masterVolume = 1;
  private stateValue: AudioEngineState = typeof AudioContext === 'undefined' ? 'unavailable' : 'locked';
  private stateCallback: ((state: AudioEngineState) => void) | null = null;

  public get state(): AudioEngineState { return this.stateValue; }

  public onStateChanged(callback: (state: AudioEngineState) => void): void { this.stateCallback = callback; callback(this.stateValue); }

  public async unlock(): Promise<boolean> {
    if (this.stateValue === 'unavailable') return false;
    const context = this.ensureContext();
    if (context.state === 'suspended') await context.resume();
    this.refreshState();
    return context.state === 'running';
  }

  public async preload(cueIds: readonly string[]): Promise<void> { if (this.stateValue !== 'unavailable') await Promise.all(cueIds.map((cueId) => this.loadBuffer(cueId))); }

  public async play(cueId: string, options: PlaySoundOptions = {}): Promise<boolean> {
    const cue = getCue(cueId);
    const gainMultiplier = options.gain ?? 1;
    validateFinite(gainMultiplier, 'Sound gain');
    if (options.position !== undefined) validatePoint(options.position, 'Sound position');
    if (cue.spatial && options.position === undefined) throw new Error(`Spatial cue ${cueId} requires a position.`);
    if (this.stateValue !== 'running') return false;

    const context = this.ensureContext();
    const buffer = await this.loadBuffer(cueId);
    if (this.stateValue !== 'running') return false;

    const source = context.createBufferSource();
    source.buffer = buffer;
    source.loop = cue.loop;
    const gain = context.createGain();
    gain.gain.value = clamp01(cue.gain * gainMultiplier);
    source.connect(gain);
    if (cue.spatial) {
      const panner = this.createPanner(cue, options.position!);
      gain.connect(panner);
      panner.connect(this.getCategoryGain(cue.category));
    } else {
      gain.connect(this.getCategoryGain(cue.category));
    }
    source.start();
    return true;
  }

  public registerEmitter(id: string, cueId: string, options: AudioEmitterOptions): void {
    const cue = getCue(cueId);
    if (!cue.spatial || !cue.loop) throw new Error('Virtual emitters require a spatial looping cue.');
    validatePoint(options.position, `Emitter ${id} position`);
    const priority = options.priority ?? 0;
    validateFinite(priority, `Emitter ${id} priority`);

    this.bumpEmitterGeneration(id);
    const existing = this.emitters.get(id);
    if (existing !== undefined) {
      this.detachEmitterFromEntity(existing);
      this.stopEmitterVoice(id);
    }
    const emitter: VirtualEmitter = { id, cueId, priority, entityId: options.entityId, position: { ...options.position } };
    this.emitters.set(id, emitter);
    this.attachEmitterToEntity(emitter);
  }

  public hasEntityEmitters(entityId: bigint): boolean { return this.emitterIndex.has(entityId); }

  public updateEmitterPosition(id: string, position: Point3D): void {
    validatePoint(position, `Emitter ${id} position`);
    const emitter = this.emitters.get(id);
    if (emitter !== undefined) this.applyEmitterPosition(emitter, position);
  }

  public removeEmitter(id: string): void {
    const emitter = this.emitters.get(id);
    if (emitter === undefined) return;
    this.bumpEmitterGeneration(id);
    this.emitters.delete(id);
    this.detachEmitterFromEntity(emitter);
    this.stopEmitterVoice(id);
  }

  public updateEntityPosition(entityId: bigint, position: Point3D): number {
    validatePoint(position, 'Entity position');
    const emitterIds = this.emitterIndex.get(entityId);
    if (emitterIds === undefined) return 0;
    let updated = 0;
    for (const emitterId of emitterIds) {
      const emitter = this.emitters.get(emitterId);
      if (emitter === undefined) continue;
      this.applyEmitterPosition(emitter, position);
      updated += 1;
    }
    return updated;
  }

  public removeEntity(entityId: bigint): number {
    const emitterIds = this.emitterIndex.get(entityId);
    if (emitterIds === undefined) return 0;
    const ids = [...emitterIds];
    for (const emitterId of ids) this.removeEmitter(emitterId);
    return ids.length;
  }

  public async syncSpatialVoices(listener: Point3D): Promise<void> {
    validatePoint(listener, 'Audio listener');
    if (this.stateValue !== 'running') { this.stopAllEmitterVoices(); return; }
    const candidates: VoiceCandidate[] = [...this.emitters.values()].map((emitter) => ({ id: emitter.id, position: emitter.position, priority: emitter.priority }));
    const selected = selectVoiceIds(candidates, listener, manifest.defaults.voiceBudgets.world);
    for (const id of this.activeEmitterVoices.keys()) if (!selected.has(id)) this.stopEmitterVoice(id);
    await Promise.all([...selected].map((id) => this.ensureEmitterVoice(id)));
  }

  public syncListenerFromCamera(camera: ThreeCameraLike): void {
    if (this.stateValue !== 'running' || this.context === null) return;
    const pose = resolveAudioListenerPose(camera);
    if (pose === null) return;
    const { position, direction, up } = pose;
    const listener = this.context.listener;
    const time = this.context.currentTime;
    listener.positionX.setValueAtTime(position.x, time); listener.positionY.setValueAtTime(position.y, time); listener.positionZ.setValueAtTime(position.z, time);
    listener.forwardX.setValueAtTime(direction.x, time); listener.forwardY.setValueAtTime(direction.y, time); listener.forwardZ.setValueAtTime(direction.z, time);
    listener.upX.setValueAtTime(up.x, time); listener.upY.setValueAtTime(up.y, time); listener.upZ.setValueAtTime(up.z, time);
  }

  public async clearAmbientLayer(key: string, fadeSeconds = 1): Promise<void> {
    validateNonNegativeFinite(fadeSeconds, 'Ambient fade duration');
    this.bumpAmbientGeneration(key);
    this.stopAmbientVoice(key, fadeSeconds);
  }

  public async setAmbientLayer(key: string, cueId: string, gainValue: number, fadeSeconds = 1): Promise<void> {
    const cue = getCue(cueId);
    if (cue.category !== 'ambient' || cue.spatial || !cue.loop) throw new Error('Ambient layers require a non-spatial looping ambient cue.');
    const desiredGain = clamp01(gainValue);
    validateNonNegativeFinite(fadeSeconds, 'Ambient fade duration');
    const generation = this.bumpAmbientGeneration(key);
    if (this.stateValue !== 'running') return;

    let voice = this.ambientVoices.get(key);
    if (voice !== undefined && voice.cueId !== cueId) {
      this.stopAmbientVoice(key, fadeSeconds);
      voice = undefined;
    }
    if (voice === undefined && desiredGain > 0) voice = await this.startAmbientVoice(key, cueId, generation);
    if (this.ambientGenerations.get(key) !== generation || voice === undefined || voice.cueId !== cueId || this.context === null) return;

    const now = this.context.currentTime;
    voice.gain.gain.cancelScheduledValues(now);
    voice.gain.gain.setValueAtTime(voice.gain.gain.value, now);
    voice.gain.gain.linearRampToValueAtTime(desiredGain * cue.gain, now + fadeSeconds);
    if (desiredGain === 0) this.stopAmbientVoice(key, fadeSeconds);
  }

  public setMuted(muted: boolean): void { this.muted = muted; this.applyMasterGain(); }
  public setMasterVolume(volume: number): void { this.masterVolume = clamp01(volume); this.applyMasterGain(); }
  public setCategoryVolume(category: AudioCategory, volume: number): void { const normalizedVolume = clamp01(volume); this.categoryVolumes.set(category, normalizedVolume); const gain = this.categoryGains.get(category); if (gain !== undefined) gain.gain.value = normalizedVolume; }

  public dispose(): void {
    this.stopAllEmitterVoices();
    for (const key of [...this.ambientVoices.keys()]) this.stopAmbientVoice(key, 0);
    this.emitters.clear();
    this.emitterGenerations.clear();
    this.ambientGenerations.clear();
    this.emitterIndex.clear();
    const context = this.context;
    this.context = null;
    if (context !== null) void context.close();
    this.categoryGains.clear();
    this.masterGain = null;
    this.refreshState();
  }

  private bumpEmitterGeneration(id: string): number { const generation = (this.emitterGenerations.get(id) ?? 0) + 1; this.emitterGenerations.set(id, generation); return generation; }
  private bumpAmbientGeneration(key: string): number { const generation = (this.ambientGenerations.get(key) ?? 0) + 1; this.ambientGenerations.set(key, generation); return generation; }
  private attachEmitterToEntity(emitter: VirtualEmitter): void { if (emitter.entityId !== undefined) this.emitterIndex.add(emitter.entityId, emitter.id); }
  private detachEmitterFromEntity(emitter: VirtualEmitter): void { if (emitter.entityId !== undefined) this.emitterIndex.remove(emitter.entityId, emitter.id); }

  private applyEmitterPosition(emitter: VirtualEmitter, position: Point3D): void {
    emitter.position = { ...position };
    const active = this.activeEmitterVoices.get(emitter.id);
    if (active !== undefined) setPannerPosition(active.panner, position);
  }

  private ensureContext(): AudioContext {
    if (this.context !== null) return this.context;
    if (typeof AudioContext === 'undefined') { this.setState('unavailable'); throw new Error('Web Audio API is unavailable.'); }
    const context = new AudioContext();
    const master = context.createGain();
    master.connect(context.destination);
    this.context = context;
    this.masterGain = master;
    for (const category of ['music', 'ui', 'ambient', 'world', 'voice'] as const) {
      const gain = context.createGain(); gain.gain.value = this.categoryVolumes.get(category) ?? 1; gain.connect(master); this.categoryGains.set(category, gain);
    }
    context.addEventListener('statechange', () => this.refreshState());
    this.applyMasterGain(); this.refreshState(); return context;
  }

  private getCategoryGain(category: AudioCategory): GainNode { const gain = this.categoryGains.get(category); if (gain === undefined) throw new Error(`Audio category ${category} is not initialized.`); return gain; }

  private async loadBuffer(cueId: string): Promise<AudioBuffer> {
    const existing = this.buffers.get(cueId);
    if (existing !== undefined) return existing;
    const cue = getCue(cueId);
    const context = this.ensureContext();
    const promise = fetch(`/audio/${cue.source}`)
      .then((response) => { if (!response.ok) throw new Error(`Failed to load audio cue ${cueId}.`); return response.arrayBuffer(); })
      .then((data) => context.decodeAudioData(data));
    this.buffers.set(cueId, promise);
    try { return await promise; } catch (error) { this.buffers.delete(cueId); throw error; }
  }

  private createPanner(cue: AudioCueDefinition, position: Point3D): PannerNode {
    validatePoint(position, 'Panner position');
    const referenceDistance = cue.referenceDistance ?? manifest.defaults.referenceDistance;
    const maximumDistance = cue.maximumDistance ?? manifest.defaults.maximumDistance;
    const rolloffFactor = cue.rolloffFactor ?? manifest.defaults.rolloffFactor;
    validatePositiveFinite(referenceDistance, 'Audio reference distance'); validatePositiveFinite(maximumDistance, 'Audio maximum distance'); validateNonNegativeFinite(rolloffFactor, 'Audio rolloff factor');
    if (maximumDistance < referenceDistance) throw new RangeError('Audio maximum distance must be greater than or equal to reference distance.');
    const context = this.ensureContext();
    const panner = context.createPanner();
    panner.panningModel = 'HRTF'; panner.distanceModel = 'inverse'; panner.refDistance = referenceDistance; panner.maxDistance = maximumDistance; panner.rolloffFactor = rolloffFactor;
    setPannerPosition(panner, position);
    return panner;
  }

  private async ensureEmitterVoice(id: string): Promise<void> {
    if (this.activeEmitterVoices.has(id) || this.stateValue !== 'running') return;
    const emitter = this.emitters.get(id);
    if (emitter === undefined) return;
    const generation = this.emitterGenerations.get(id) ?? 0;
    const cueId = emitter.cueId;
    const cue = getCue(cueId);
    const buffer = await this.loadBuffer(cueId);
    const currentEmitter = this.emitters.get(id);
    if (this.activeEmitterVoices.has(id)
      || currentEmitter !== emitter
      || currentEmitter?.cueId !== cueId
      || this.emitterGenerations.get(id) !== generation
      || this.stateValue !== 'running') return;

    const context = this.ensureContext();
    const source = context.createBufferSource(); source.buffer = buffer; source.loop = true;
    const gain = context.createGain(); gain.gain.value = cue.gain;
    const panner = this.createPanner(cue, currentEmitter.position);
    source.connect(gain); gain.connect(panner); panner.connect(this.getCategoryGain(cue.category)); source.start();
    this.activeEmitterVoices.set(id, { cueId, source, panner });
  }

  private stopEmitterVoice(id: string): void {
    const active = this.activeEmitterVoices.get(id);
    if (active === undefined) return;
    this.activeEmitterVoices.delete(id);
    try { active.source.stop(); } catch { }
    active.source.disconnect(); active.panner.disconnect();
  }

  private stopAllEmitterVoices(): void { for (const id of [...this.activeEmitterVoices.keys()]) this.stopEmitterVoice(id); }

  private async startAmbientVoice(key: string, cueId: string, generation: number): Promise<AmbientVoice | undefined> {
    const buffer = await this.loadBuffer(cueId);
    if (this.ambientGenerations.get(key) !== generation || this.stateValue !== 'running' || this.context === null) return undefined;
    const existing = this.ambientVoices.get(key);
    if (existing !== undefined) return existing.cueId === cueId ? existing : undefined;
    const source = this.context.createBufferSource(); source.buffer = buffer; source.loop = true;
    const gain = this.context.createGain(); gain.gain.value = 0;
    source.connect(gain); gain.connect(this.getCategoryGain('ambient'));
    const voice: AmbientVoice = { cueId, source, gain };
    this.ambientVoices.set(key, voice); source.start(); return voice;
  }

  private stopAmbientVoice(key: string, fadeSeconds: number): void {
    const voice = this.ambientVoices.get(key);
    if (voice === undefined || this.context === null) return;
    this.ambientVoices.delete(key);
    const now = this.context.currentTime;
    const fade = Math.max(0, fadeSeconds);
    voice.gain.gain.cancelScheduledValues(now); voice.gain.gain.setValueAtTime(voice.gain.gain.value, now); voice.gain.gain.linearRampToValueAtTime(0, now + fade);
    window.setTimeout(() => { try { voice.source.stop(); } catch { } voice.source.disconnect(); voice.gain.disconnect(); }, fade * 1_000 + 50);
  }

  private applyMasterGain(): void { if (this.masterGain !== null) this.masterGain.gain.value = resolveMasterGain(this.muted, this.masterVolume); }
  private refreshState(): void { if (typeof AudioContext === 'undefined') { this.setState('unavailable'); return; } if (this.context === null) { this.setState('locked'); return; } this.setState(this.context.state === 'running' ? 'running' : 'suspended'); }
  private setState(state: AudioEngineState): void { if (this.stateValue === state) return; this.stateValue = state; this.stateCallback?.(state); }
}

function getCue(cueId: string): AudioCueDefinition { const cue = manifest.cues[cueId]; if (cue === undefined) throw new Error(`Unknown audio cue ID: ${cueId}.`); validateFinite(cue.gain, `Audio cue ${cueId} gain`); return cue; }
function setPannerPosition(panner: PannerNode, position: Point3D): void { const mapped = simulationToAudioPosition(position); const time = panner.context.currentTime; panner.positionX.setValueAtTime(mapped.x, time); panner.positionY.setValueAtTime(mapped.y, time); panner.positionZ.setValueAtTime(mapped.z, time); }
export function simulationToAudioPosition(position: Point3D): Point3D { validatePoint(position, 'Audio position'); return { x: position.x, y: position.z, z: position.y }; }
function normalize3(x: number, y: number, z: number): Point3D { const length = Math.hypot(x, y, z); if (length <= Number.EPSILON) return { x: 0, y: 0, z: -1 }; return { x: x / length, y: y / length, z: z / length }; }
export function resolveAudioListenerPose(camera: ThreeCameraLike): AudioListenerPose | null {
  const elements = camera.matrixWorld.elements;
  if (elements.length < 16) return null;
  const values = [Number(elements[4]), Number(elements[5]), Number(elements[6]), Number(elements[8]), Number(elements[9]), Number(elements[10]), Number(elements[12]), Number(elements[13]), Number(elements[14])];
  if (!values.every(Number.isFinite)) return null;
  return { position: { x: values[6]!, y: values[7]!, z: values[8]! }, direction: normalize3(-values[3]!, -values[4]!, -values[5]!), up: normalize3(values[0]!, values[1]!, values[2]!) };
}
export function resolveMasterGain(muted: boolean, volume: number): number { const normalizedVolume = clamp01(volume); return muted ? 0 : normalizedVolume; }
function validatePoint(point: Point3D, label: string): void { if (!Number.isFinite(point.x) || !Number.isFinite(point.y) || !Number.isFinite(point.z)) throw new RangeError(`${label} coordinates must be finite.`); }
function validatePositiveFinite(value: number, label: string): void { validateFinite(value, label); if (value <= 0) throw new RangeError(`${label} must be greater than zero.`); }
function validateNonNegativeFinite(value: number, label: string): void { validateFinite(value, label); if (value < 0) throw new RangeError(`${label} must be non-negative.`); }
function validateFinite(value: number, label: string): void { if (!Number.isFinite(value)) throw new RangeError(`${label} must be finite.`); }
function clamp01(value: number): number { validateFinite(value, 'Audio gain'); return Math.min(1, Math.max(0, value)); }
