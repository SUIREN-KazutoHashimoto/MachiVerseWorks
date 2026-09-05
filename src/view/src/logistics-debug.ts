import { initializeLocalization, type Localizer } from './localization.ts';
import { type LogisticsSnapshotMessage } from './logistics-protocol.ts';

export class LogisticsDebugOverlay {
  private readonly element: HTMLPreElement;

  public constructor(host: HTMLElement, private readonly localizer: Localizer = initializeLocalization()) {
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
      this.localizer.t('logisticsDebug.summary', { tick: this.localizer.formatNumber(statistics.tickCount), cycle: this.localizer.formatNumber(statistics.logisticsCycle) }),
      this.localizer.t('logisticsDebug.inventory', { inventory: this.localizer.formatNumber(statistics.inventoryUnits), orders: this.localizer.formatNumber(statistics.openOrderCount), shipments: this.localizer.formatNumber(statistics.shipmentCount), delayed: this.localizer.formatNumber(statistics.delayedShipmentCount) }),
    ];
    for (const inventory of message.inventories.slice(0, 6)) {
      lines.push(this.localizer.t('logisticsDebug.inventoryDetail', { establishment: this.localizer.formatNumber(inventory.establishmentId), commodity: this.localizer.formatNumber(inventory.commodityId), quantity: this.localizer.formatNumber(inventory.quantity), capacity: this.localizer.formatNumber(inventory.capacity) }));
    }
    for (const shipment of message.shipments.slice(0, 6)) {
      lines.push(this.localizer.t('logisticsDebug.shipmentDetail', { shipment: this.localizer.formatNumber(shipment.shipmentId), state: this.localizer.t(`logisticsDebug.shipmentState.${String(shipment.state)}`), vehicle: shipment.vehicleId === 0n ? '-' : this.localizer.formatNumber(shipment.vehicleId), quantity: this.localizer.formatNumber(shipment.quantity), delay: this.localizer.formatNumber(shipment.delayTicks) }));
    }
    this.element.textContent = lines.join('\n');
  }

  public clear(): void { this.element.textContent = this.localizer.t('logisticsDebug.waiting'); }
  public dispose(): void { this.element.remove(); }
}
