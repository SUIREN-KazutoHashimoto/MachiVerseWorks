import { ShipmentState, type LogisticsSnapshotMessage } from './logistics-protocol.ts';

export class LogisticsDebugOverlay {
  private readonly element: HTMLPreElement;

  public constructor(host: HTMLElement) {
    this.element = document.createElement('pre');
    this.element.dataset.logisticsDebug = 'true';
    Object.assign(this.element.style, {
      position: 'absolute',
      right: '12px',
      top: '12px',
      zIndex: '20',
      maxWidth: '420px',
      maxHeight: '40vh',
      overflow: 'auto',
      margin: '0',
      padding: '8px 10px',
      background: 'rgba(0, 0, 0, 0.72)',
      color: '#fff',
      font: '12px/1.45 monospace',
      pointerEvents: 'none',
      whiteSpace: 'pre-wrap',
    });
    host.append(this.element);
    this.clear();
  }

  public apply(message: LogisticsSnapshotMessage): void {
    const statistics = message.statistics;
    const lines = [
      `Logistics tick=${statistics.tickCount.toString()} cycle=${statistics.logisticsCycle.toString()}`,
      `inventory=${statistics.inventoryUnits.toFixed(1)} orders=${String(statistics.openOrderCount)} shipments=${String(statistics.shipmentCount)} delayed=${String(statistics.delayedShipmentCount)}`,
    ];
    for (const inventory of message.inventories.slice(0, 6)) {
      lines.push(`INV est=${inventory.establishmentId.toString()} commodity=${inventory.commodityId.toString()} ${inventory.quantity.toFixed(1)}/${inventory.capacity.toFixed(1)}`);
    }
    for (const shipment of message.shipments.slice(0, 6)) {
      lines.push(`SHP ${shipment.shipmentId.toString()} ${ShipmentState[shipment.state]} vehicle=${shipment.vehicleId === 0n ? '-' : shipment.vehicleId.toString()} qty=${shipment.quantity.toFixed(1)} delay=${shipment.delayTicks.toString()}`);
    }
    this.element.textContent = lines.join('\n');
  }

  public clear(): void { this.element.textContent = 'Logistics: waiting for snapshot'; }
  public dispose(): void { this.element.remove(); }
}
