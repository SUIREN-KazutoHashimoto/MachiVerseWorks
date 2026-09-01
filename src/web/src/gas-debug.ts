import { Localizer, initializeLocalization } from './localization.ts';
import { GasFacilityKind, GasOperatingState, GasServiceState, type GasSnapshotMessage } from './gas-protocol.ts';

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

export class GasDebugOverlay {
  private readonly element: HTMLDivElement;
  private readonly summary: HTMLPreElement;
  private readonly svg: SVGSVGElement;

  public constructor(host: HTMLElement, private readonly localizer: Localizer = initializeLocalization()) {
    this.element = document.createElement('div');
    this.element.dataset.gasDebug = 'true';
    Object.assign(this.element.style, { position: 'absolute', right: '12px', top: '12px', zIndex: '20', width: '380px', maxWidth: '44vw', padding: '8px 10px', background: 'rgba(0, 0, 0, 0.72)', color: '#fff', font: '12px/1.4 monospace', pointerEvents: 'none' });
    this.summary = document.createElement('pre');
    Object.assign(this.summary.style, { margin: '0 0 6px', whiteSpace: 'pre-wrap' });
    this.svg = document.createElementNS(SVG_NAMESPACE, 'svg');
    this.svg.setAttribute('viewBox', '0 0 360 160'); this.svg.setAttribute('width', '360'); this.svg.setAttribute('height', '160'); this.svg.setAttribute('aria-label', this.localizer.t('gasDebug.ariaLabel'));
    this.element.append(this.summary, this.svg); host.append(this.element); this.clear();
  }

  public apply(message: GasSnapshotMessage): void {
    const s = message.statistics;
    this.summary.textContent = [
      this.localizer.t('gasDebug.summary', { tick: s.tickCount, unavailable: s.unavailableServicePointCount, piped: s.pipedServicePointCount, delivered: s.deliveredServicePointCount }),
      this.localizer.t('gasDebug.flow', { served: s.servedCubicMetersPerDay.toFixed(2), demand: s.demandCubicMetersPerDay.toFixed(2), capacity: s.supplyCapacityCubicMetersPerDay.toFixed(2) }),
      this.localizer.t('gasDebug.storage', { stored: s.storedCubicMeters.toFixed(2), unserved: s.unservedCubicMetersPerDay.toFixed(2) }),
    ].join('\n');
    this.renderNetwork(message);
  }
  public clear(): void { this.summary.textContent = this.localizer.t('gasDebug.waiting'); this.svg.replaceChildren(); }
  public dispose(): void { this.element.remove(); }

  private renderNetwork(message: GasSnapshotMessage): void {
    this.svg.replaceChildren(); if (message.nodes.length === 0) return;
    const xs = message.nodes.map((node) => node.x); const ys = message.nodes.map((node) => node.y);
    const minX = Math.min(...xs); const maxX = Math.max(...xs); const minY = Math.min(...ys); const maxY = Math.max(...ys);
    const width = Math.max(1, maxX - minX); const height = Math.max(1, maxY - minY); const positions = new Map<bigint, readonly [number, number]>();
    for (const node of message.nodes) positions.set(node.nodeId, [15 + ((node.x - minX) / width) * 330, 15 + ((node.y - minY) / height) * 130]);
    for (const pipeline of message.pipelines) {
      const from = positions.get(pipeline.fromNodeId); const to = positions.get(pipeline.toNodeId); if (from === undefined || to === undefined) continue;
      const line = document.createElementNS(SVG_NAMESPACE, 'line'); line.setAttribute('x1', from[0].toFixed(2)); line.setAttribute('y1', from[1].toFixed(2)); line.setAttribute('x2', to[0].toFixed(2)); line.setAttribute('y2', to[1].toFixed(2)); line.setAttribute('stroke', pipeline.isInService ? '#f59e0b' : '#ef4444'); line.setAttribute('stroke-width', pipeline.isInService ? '2' : '3'); if (!pipeline.isInService) line.setAttribute('stroke-dasharray', '5 4'); this.svg.append(line);
    }
    for (const node of message.nodes) {
      const point = positions.get(node.nodeId); if (point === undefined) continue;
      const circle = document.createElementNS(SVG_NAMESPACE, 'circle'); circle.setAttribute('cx', point[0].toFixed(2)); circle.setAttribute('cy', point[1].toFixed(2)); circle.setAttribute('r', '5'); circle.setAttribute('fill', '#f59e0b'); circle.setAttribute('stroke', '#fff'); circle.setAttribute('stroke-width', '1'); this.svg.append(circle);
    }
    for (const facility of message.facilities) {
      const point = positions.get(facility.nodeId); if (point === undefined) continue;
      const rect = document.createElementNS(SVG_NAMESPACE, 'rect'); rect.setAttribute('x', (point[0] - 4).toFixed(2)); rect.setAttribute('y', (point[1] - 4).toFixed(2)); rect.setAttribute('width', '8'); rect.setAttribute('height', '8'); rect.setAttribute('fill', facility.operatingState === GasOperatingState.Offline ? '#ef4444' : facility.kind === GasFacilityKind.Storage ? '#facc15' : '#f8fafc'); rect.setAttribute('stroke', '#111827'); rect.setAttribute('stroke-width', '1'); this.svg.append(rect);
    }
    for (const service of message.servicePoints) {
      if (service.nodeId === 0n) continue; const point = positions.get(service.nodeId); if (point === undefined) continue;
      const ring = document.createElementNS(SVG_NAMESPACE, 'circle'); ring.setAttribute('cx', point[0].toFixed(2)); ring.setAttribute('cy', point[1].toFixed(2)); ring.setAttribute('r', '9'); ring.setAttribute('fill', 'none'); ring.setAttribute('stroke-width', '2'); ring.setAttribute('stroke', service.serviceState === GasServiceState.Unavailable ? '#ef4444' : service.serviceState === GasServiceState.Constrained ? '#f59e0b' : '#22c55e'); this.svg.append(ring);
    }
  }
}
