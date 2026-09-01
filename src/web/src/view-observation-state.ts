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

/** Single writable ingress for observation messages used by the View. */
export class ViewObservationState implements ReadonlyViewObservationState {
  public readonly entities = new EntityStore();
  public readonly pedestrians = new PedestrianStore();
  public readonly vehicles = new VehicleStore();
  public readonly intersections = new IntersectionControlStore();
  public readonly roadNetwork = new RoadNetworkStore();

  public apply(message: ProtocolMessage | TrafficProtocolMessage, receivedAt = performance.now()): boolean {
    switch (message.type) {
      case MessageType.AgentSpawn:
        this.entities.spawn(message, receivedAt);
        return true;
      case MessageType.AgentUpdate:
        if (!this.entities.update(message, receivedAt)) this.entities.spawn(message, receivedAt);
        return true;
      case MessageType.AgentRemove:
        this.entities.remove(message.agentId);
        return true;
      case MessageType.PedestrianSpawn:
        this.pedestrians.spawn(message, receivedAt);
        return true;
      case MessageType.PedestrianUpdate:
        if (!this.pedestrians.update(message, receivedAt)) this.pedestrians.spawn(message, receivedAt);
        return true;
      case MessageType.PedestrianRemove:
        this.pedestrians.remove(message.pedestrianId);
        return true;
      case MessageType.RoadNetworkSnapshot:
        this.roadNetwork.replace(message);
        return true;
      case TrafficMessageType.VehicleSpawn:
        this.vehicles.spawn(message, receivedAt);
        return true;
      case TrafficMessageType.VehicleUpdate:
        if (!this.vehicles.update(message, receivedAt)) this.vehicles.spawn(message, receivedAt);
        return true;
      case TrafficMessageType.VehicleRemove:
        this.vehicles.remove(message.vehicleId);
        return true;
      case TrafficMessageType.IntersectionControlSnapshot:
        this.intersections.apply(message, receivedAt);
        return true;
      default:
        return false;
    }
  }

  /** Clears connection-local authoritative observations and all visual interpolation history. */
  public resetConnectionState(): void {
    this.entities.clear();
    this.pedestrians.clear();
    this.vehicles.clear();
    this.intersections.clear();
    this.roadNetwork.clear();
  }
}
