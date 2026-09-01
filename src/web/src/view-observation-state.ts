import { EntityStore, type ReadonlyEntityStore } from './entity-store.ts';
import { PedestrianStore, type ReadonlyPedestrianStore } from './pedestrian-store.ts';
import { MessageType, type ProtocolMessage } from './protocol.ts';
import { RoadNetworkStore, type ReadonlyRoadNetworkStore } from './road-network-store.ts';
import { TrafficMessageType, type TrafficProtocolMessage } from './traffic-protocol.ts';
import { IntersectionControlStore, type ReadonlyIntersectionControlStore, type ReadonlyVehicleStore, VehicleStore } from './traffic-store.ts';

export interface ReadonlyViewObservationState {
  readonly entities: ReadonlyEntityStore;
  readonly pedestrians: ReadonlyPedestrianStore;
  readonly vehicles: ReadonlyVehicleStore;
  readonly intersections: ReadonlyIntersectionControlStore;
  readonly roadNetwork: ReadonlyRoadNetworkStore;
}

/**
 * Single writable ingress for observation messages used by the View.
 * Consumers receive only ReadonlyViewObservationState and therefore cannot
 * mutate simulation or observation state through the rendering boundary.
 */
export class ViewObservationState implements ReadonlyViewObservationState {
  private readonly entityStore = new EntityStore();
  private readonly pedestrianStore = new PedestrianStore();
  private readonly vehicleStore = new VehicleStore();
  private readonly intersectionStore = new IntersectionControlStore();
  private readonly roadStore = new RoadNetworkStore();

  public readonly entities: ReadonlyEntityStore = this.entityStore;
  public readonly pedestrians: ReadonlyPedestrianStore = this.pedestrianStore;
  public readonly vehicles: ReadonlyVehicleStore = this.vehicleStore;
  public readonly intersections: ReadonlyIntersectionControlStore = this.intersectionStore;
  public readonly roadNetwork: ReadonlyRoadNetworkStore = this.roadStore;

  public apply(message: ProtocolMessage | TrafficProtocolMessage, receivedAt = performance.now()): boolean {
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
        this.roadStore.replace(message);
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
      default:
        return false;
    }
  }

  /** Clears all state tied to the current observation connection, including interpolation history. */
  public resetConnectionState(): void {
    this.entityStore.clear();
    this.pedestrianStore.clear();
    this.vehicleStore.clear();
    this.intersectionStore.clear();
    this.roadStore.clear();
  }
}
