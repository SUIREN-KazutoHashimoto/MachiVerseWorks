import { EntityStore, type ReadonlyEntityStore } from './entity-store.ts';
import { PedestrianStore, type ReadonlyPedestrianStore } from './pedestrian-store.ts';
import { MessageType, type ProtocolMessage } from './protocol.ts';
import { RegionalGenerationStore, type ReadonlyRegionalGenerationStore } from './regional-generation-store.ts';
import { REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE, type RegionalGenerationSnapshotMessage } from './regional-generation-protocol.ts';
import { RoadNetworkStore, type ReadonlyRoadNetworkStore } from './road-network-store.ts';
import { TrafficMessageType, type TrafficProtocolMessage } from './traffic-protocol.ts';
import { IntersectionControlStore, type ReadonlyIntersectionControlStore, type ReadonlyVehicleStore, VehicleStore } from './traffic-store.ts';
import { WorldEnvironmentStore, type ReadonlyWorldEnvironmentStore } from './world-environment-store.ts';
import { WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE, type WorldEnvironmentSnapshotMessage } from './world-environment-protocol.ts';

export interface ReadonlyViewObservationState {
  readonly entities: ReadonlyEntityStore;
  readonly pedestrians: ReadonlyPedestrianStore;
  readonly vehicles: ReadonlyVehicleStore;
  readonly intersections: ReadonlyIntersectionControlStore;
  readonly roadNetwork: ReadonlyRoadNetworkStore;
  readonly worldEnvironment: ReadonlyWorldEnvironmentStore;
  readonly regionalGeneration: ReadonlyRegionalGenerationStore;
}

/** Single writable ingress for observation messages used by the View. */
export class ViewObservationState implements ReadonlyViewObservationState {
  private readonly entityStore = new EntityStore();
  private readonly pedestrianStore = new PedestrianStore();
  private readonly vehicleStore = new VehicleStore();
  private readonly intersectionStore = new IntersectionControlStore();
  private readonly roadNetworkStore = new RoadNetworkStore();
  private readonly worldEnvironmentStore = new WorldEnvironmentStore();
  private readonly regionalGenerationStore = new RegionalGenerationStore();

  public get entities(): ReadonlyEntityStore { return this.entityStore; }
  public get pedestrians(): ReadonlyPedestrianStore { return this.pedestrianStore; }
  public get vehicles(): ReadonlyVehicleStore { return this.vehicleStore; }
  public get intersections(): ReadonlyIntersectionControlStore { return this.intersectionStore; }
  public get roadNetwork(): ReadonlyRoadNetworkStore { return this.roadNetworkStore; }
  public get worldEnvironment(): ReadonlyWorldEnvironmentStore { return this.worldEnvironmentStore; }
  public get regionalGeneration(): ReadonlyRegionalGenerationStore { return this.regionalGenerationStore; }

  public apply(message: ProtocolMessage | TrafficProtocolMessage | WorldEnvironmentSnapshotMessage | RegionalGenerationSnapshotMessage, receivedAt = performance.now()): boolean {
    switch (message.type) {
      case MessageType.AgentSpawn:
        this.entityStore.spawn(message, receivedAt);
        return true;
      case MessageType.AgentUpdate:
        if (!this.entityStore.update(message, receivedAt)) this.entityStore.spawn(message, receivedAt);
        return true;
      case MessageType.AgentRemove:
        this.entityStore.remove(message.agentId);
        return true;
      case MessageType.PedestrianSpawn:
        this.pedestrianStore.spawn(message, receivedAt);
        return true;
      case MessageType.PedestrianUpdate:
        if (!this.pedestrianStore.update(message, receivedAt)) this.pedestrianStore.spawn(message, receivedAt);
        return true;
      case MessageType.PedestrianRemove:
        this.pedestrianStore.remove(message.pedestrianId);
        return true;
      case MessageType.RoadNetworkSnapshot:
        this.roadNetworkStore.replace(message);
        return true;
      case TrafficMessageType.VehicleSpawn:
        this.vehicleStore.spawn(message, receivedAt);
        return true;
      case TrafficMessageType.VehicleUpdate:
        if (!this.vehicleStore.update(message, receivedAt)) this.vehicleStore.spawn(message, receivedAt);
        return true;
      case TrafficMessageType.VehicleRemove:
        this.vehicleStore.remove(message.vehicleId);
        return true;
      case TrafficMessageType.IntersectionControlSnapshot:
        this.intersectionStore.apply(message, receivedAt);
        return true;
      case WORLD_ENVIRONMENT_SNAPSHOT_MESSAGE_TYPE:
        this.worldEnvironmentStore.replace(message);
        return true;
      case REGIONAL_GENERATION_SNAPSHOT_MESSAGE_TYPE:
        this.regionalGenerationStore.replace(message);
        return true;
      default:
        return false;
    }
  }

  /** Clears connection-local authoritative observations and all visual interpolation history. */
  public resetConnectionState(): void {
    this.entityStore.clear();
    this.pedestrianStore.clear();
    this.vehicleStore.clear();
    this.intersectionStore.clear();
    this.roadNetworkStore.clear();
    this.worldEnvironmentStore.clear();
    this.regionalGenerationStore.clear();
  }
}
