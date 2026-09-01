import {
  SewerServiceState,
  UtilityFacilityKind,
  UtilityNetworkKind,
  UtilityOperatingState,
  WaterServiceState,
  type UtilityFacility,
  type WaterSewerSnapshotMessage,
} from './water-sewer-protocol.ts';

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

export class WaterSewerDebugOverlay {
  private readonly element: HTMLDivElement;
  private readonly summary: HTMLPreElement;
  private readonly svg: SVGSVGElement;

  public constructor(host: HTMLElement) {
    this.element = document.createElement('div');
    this.element.dataset.waterSewerDebug = 'true';
    Object.assign(this.element.style, {
      position: 'absolute',
      right: '12px',
      bottom: '12px',
      zIndex: '20',
      width: '380px',
      maxWidth: '44vw',
      padding: '8px 10px',
      background: 'rgba(0, 0, 0, 0.72)',
      color: '#fff',
      font: '12px/1.4 monospace',
      pointerEvents: 'none',
    });
    this.summary = document.createElement('pre');
    Object.assign(this.summary.style, { margin: '0 0 6px', whiteSpace: 'pre-wrap' });
    this.svg = document.createElementNS(SVG_NAMESPACE, 'svg');
    this.svg.setAttribute('viewBox', '0 0 360 160');
    this.svg.setAttribute('width', '360');
    this.svg.setAttribute('height', '160');
    this.svg.setAttribute('aria-label', 'Water and sewer network debug view');
    this.element.append(this.summary, this.svg);
    host.append(this.element);
    this.clear();
  }

  public apply(message: WaterSewerSnapshotMessage): void {
    const s = message.statistics;
    this.summary.textContent = [
      `Water/Sewer tick=${s.tickCount.toString()} water-unavailable=${String(s.waterUnavailableCount)} sewer-unavailable=${String(s.sewerUnavailableCount)} overflow=${String(s.sewerOverflowCount)}`,
      `water=${s.waterServedCubicMetersPerDay.toFixed(2)}/${s.waterDemandCubicMetersPerDay.toFixed(2)} m3/day capacity=${s.waterSupplyCapacityCubicMetersPerDay.toFixed(2)}`,
      `wastewater=${s.wastewaterProcessedCubicMetersPerDay.toFixed(2)}/${s.wastewaterGeneratedCubicMetersPerDay.toFixed(2)} overflow=${s.wastewaterOverflowCubicMetersPerDay.toFixed(2)} m3/day`,
    ].join('\n');
    this.renderNetwork(message);
  }

  public clear(): void {
    this.summary.textContent = 'Water/Sewer: waiting for snapshot';
    this.svg.replaceChildren();
  }

  public dispose(): void { this.element.remove(); }

  private renderNetwork(message: WaterSewerSnapshotMessage): void {
    this.svg.replaceChildren();
    if (message.nodes.length === 0) return;
    const xs = message.nodes.map((node) => node.x);
    const ys = message.nodes.map((node) => node.y);
    const minX = Math.min(...xs);
    const maxX = Math.max(...xs);
    const minY = Math.min(...ys);
    const maxY = Math.max(...ys);
    const width = Math.max(1, maxX - minX);
    const height = Math.max(1, maxY - minY);
    const positions = new Map<string, readonly [number, number]>();
    for (const node of message.nodes) {
      positions.set(
        nodeKey(node.networkKind, node.nodeId),
        [15 + ((node.x - minX) / width) * 330, 15 + ((node.y - minY) / height) * 130],
      );
    }

    for (const pipe of message.pipes) {
      const from = positions.get(nodeKey(pipe.networkKind, pipe.fromNodeId));
      const to = positions.get(nodeKey(pipe.networkKind, pipe.toNodeId));
      if (from === undefined || to === undefined) continue;
      const element = document.createElementNS(SVG_NAMESPACE, 'line');
      element.setAttribute('x1', from[0].toFixed(2));
      element.setAttribute('y1', from[1].toFixed(2));
      element.setAttribute('x2', to[0].toFixed(2));
      element.setAttribute('y2', to[1].toFixed(2));
      element.setAttribute('stroke', !pipe.isInService ? '#ef4444' : pipe.networkKind === UtilityNetworkKind.Water ? '#38bdf8' : '#a78bfa');
      element.setAttribute('stroke-width', pipe.isInService ? '2' : '3');
      if (!pipe.isInService) element.setAttribute('stroke-dasharray', '5 4');
      this.svg.append(element);
    }

    for (const node of message.nodes) {
      const point = positions.get(nodeKey(node.networkKind, node.nodeId));
      if (point === undefined) continue;
      const circle = document.createElementNS(SVG_NAMESPACE, 'circle');
      circle.setAttribute('cx', point[0].toFixed(2));
      circle.setAttribute('cy', point[1].toFixed(2));
      circle.setAttribute('r', '5');
      circle.setAttribute('fill', node.networkKind === UtilityNetworkKind.Water ? '#0ea5e9' : '#8b5cf6');
      circle.setAttribute('stroke', '#fff');
      circle.setAttribute('stroke-width', '1');
      this.svg.append(circle);
    }

    for (const facility of message.facilities) this.renderFacility(facility, positions);
    for (const service of message.servicePoints) {
      const point = positions.get(nodeKey(UtilityNetworkKind.Water, service.waterNodeId));
      if (point === undefined) continue;
      const ring = document.createElementNS(SVG_NAMESPACE, 'circle');
      ring.setAttribute('cx', point[0].toFixed(2));
      ring.setAttribute('cy', point[1].toFixed(2));
      ring.setAttribute('r', '9');
      ring.setAttribute('fill', 'none');
      ring.setAttribute('stroke-width', '2');
      ring.setAttribute('stroke', service.waterState === WaterServiceState.Unavailable
        || service.sewerState === SewerServiceState.Unavailable
        || service.sewerState === SewerServiceState.Overflow
        ? '#ef4444'
        : service.waterState === WaterServiceState.Constrained || service.sewerState === SewerServiceState.Constrained
          ? '#f59e0b'
          : '#22c55e');
      this.svg.append(ring);
    }
  }

  private renderFacility(facility: UtilityFacility, positions: Map<string, readonly [number, number]>): void {
    const network = facility.kind === UtilityFacilityKind.SewerPump || facility.kind === UtilityFacilityKind.SewageTreatmentPlant
      ? UtilityNetworkKind.Sewer
      : UtilityNetworkKind.Water;
    const point = positions.get(nodeKey(network, facility.nodeId));
    if (point === undefined) return;
    const rect = document.createElementNS(SVG_NAMESPACE, 'rect');
    rect.setAttribute('x', (point[0] - 4).toFixed(2));
    rect.setAttribute('y', (point[1] - 4).toFixed(2));
    rect.setAttribute('width', '8');
    rect.setAttribute('height', '8');
    rect.setAttribute('fill', facility.operatingState === UtilityOperatingState.Offline ? '#ef4444' : '#f8fafc');
    rect.setAttribute('stroke', '#111827');
    rect.setAttribute('stroke-width', '1');
    this.svg.append(rect);
  }
}

function nodeKey(network: UtilityNetworkKind, id: bigint): string { return `${String(network)}:${id.toString()}`; }
