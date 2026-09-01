import {
  GeneratorOperatingState,
  PowerNodeKind,
  PowerSupplyState,
  type PowerSnapshotMessage,
} from './power-protocol.ts';

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

export class PowerDebugOverlay {
  private readonly element: HTMLDivElement;
  private readonly summary: HTMLPreElement;
  private readonly svg: SVGSVGElement;

  public constructor(host: HTMLElement) {
    this.element = document.createElement('div');
    this.element.dataset.powerDebug = 'true';
    Object.assign(this.element.style, {
      position: 'absolute',
      left: '12px',
      bottom: '12px',
      zIndex: '20',
      width: '360px',
      maxWidth: '42vw',
      padding: '8px 10px',
      background: 'rgba(0, 0, 0, 0.72)',
      color: '#fff',
      font: '12px/1.4 monospace',
      pointerEvents: 'none',
    });
    this.summary = document.createElement('pre');
    Object.assign(this.summary.style, { margin: '0 0 6px', whiteSpace: 'pre-wrap' });
    this.svg = document.createElementNS(SVG_NAMESPACE, 'svg');
    this.svg.setAttribute('viewBox', '0 0 340 150');
    this.svg.setAttribute('width', '340');
    this.svg.setAttribute('height', '150');
    this.svg.setAttribute('aria-label', 'Power network debug view');
    this.element.append(this.summary, this.svg);
    host.append(this.element);
    this.clear();
  }

  public apply(message: PowerSnapshotMessage): void {
    const statistics = message.statistics;
    this.summary.textContent = [
      `Power tick=${statistics.tickCount.toString()} outages=${String(statistics.outageLoadCount)}`,
      `generation=${statistics.generationOutputMegawatts.toFixed(2)}/${statistics.generationCapacityMegawatts.toFixed(2)} MW`,
      `demand=${statistics.demandMegawatts.toFixed(2)} served=${statistics.servedMegawatts.toFixed(2)} unserved=${statistics.unservedMegawatts.toFixed(2)} MW`,
    ].join('\n');
    this.renderNetwork(message);
  }

  public clear(): void {
    this.summary.textContent = 'Power: waiting for snapshot';
    this.svg.replaceChildren();
  }

  public dispose(): void {
    this.element.remove();
  }

  private renderNetwork(message: PowerSnapshotMessage): void {
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
    const position = new Map<bigint, readonly [number, number]>();
    for (const node of message.nodes) {
      position.set(node.nodeId, [15 + ((node.x - minX) / width) * 310, 15 + ((node.y - minY) / height) * 120]);
    }

    for (const line of message.lines) {
      const from = position.get(line.fromNodeId);
      const to = position.get(line.toNodeId);
      if (from === undefined || to === undefined) continue;
      const element = document.createElementNS(SVG_NAMESPACE, 'line');
      element.setAttribute('x1', from[0].toFixed(2));
      element.setAttribute('y1', from[1].toFixed(2));
      element.setAttribute('x2', to[0].toFixed(2));
      element.setAttribute('y2', to[1].toFixed(2));
      element.setAttribute('stroke', line.isInService ? '#9ca3af' : '#ef4444');
      element.setAttribute('stroke-width', line.isInService ? '2' : '3');
      if (!line.isInService) element.setAttribute('stroke-dasharray', '5 4');
      this.svg.append(element);
    }

    const generatorByNode = new Map(message.generators.map((generator) => [generator.nodeId, generator]));
    const loadByNode = new Map(message.loads.map((load) => [load.nodeId, load]));
    for (const node of message.nodes) {
      const point = position.get(node.nodeId);
      if (point === undefined) continue;
      const generator = generatorByNode.get(node.nodeId);
      const load = loadByNode.get(node.nodeId);
      const circle = document.createElementNS(SVG_NAMESPACE, 'circle');
      circle.setAttribute('cx', point[0].toFixed(2));
      circle.setAttribute('cy', point[1].toFixed(2));
      circle.setAttribute('r', node.kind === PowerNodeKind.Substation ? '5' : '6');
      circle.setAttribute('fill', this.nodeColor(node.kind, generator?.operatingState, load?.supplyState));
      circle.setAttribute('stroke', '#fff');
      circle.setAttribute('stroke-width', '1');
      this.svg.append(circle);
    }
  }

  private nodeColor(
    kind: PowerNodeKind,
    generatorState: GeneratorOperatingState | undefined,
    supplyState: PowerSupplyState | undefined,
  ): string {
    if (generatorState === GeneratorOperatingState.Offline) return '#ef4444';
    if (supplyState === PowerSupplyState.Outage) return '#ef4444';
    if (supplyState === PowerSupplyState.Constrained) return '#f59e0b';
    if (kind === PowerNodeKind.GeneratorBus) return '#22c55e';
    if (kind === PowerNodeKind.Substation) return '#60a5fa';
    if (kind === PowerNodeKind.Load) return '#a78bfa';
    return '#d1d5db';
  }
}
