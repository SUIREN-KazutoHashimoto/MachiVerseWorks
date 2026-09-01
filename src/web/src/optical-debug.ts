import { OpticalQualityState, type OpticalSnapshotMessage } from './optical-protocol.ts';

const SVG_NAMESPACE = 'http://www.w3.org/2000/svg';

export class OpticalDebugOverlay {
  private readonly element: HTMLDivElement;
  private readonly summary: HTMLPreElement;
  private readonly svg: SVGSVGElement;

  public constructor(host: HTMLElement) {
    this.element = document.createElement('div');
    this.element.dataset.opticalDebug = 'true';
    Object.assign(this.element.style, { position: 'absolute', left: '12px', bottom: '12px', zIndex: '20', width: '390px', maxWidth: '46vw', padding: '8px 10px', background: 'rgba(0, 0, 0, 0.72)', color: '#fff', font: '12px/1.4 monospace', pointerEvents: 'none' });
    this.summary = document.createElement('pre');
    Object.assign(this.summary.style, { margin: '0 0 6px', whiteSpace: 'pre-wrap' });
    this.svg = document.createElementNS(SVG_NAMESPACE, 'svg');
    this.svg.setAttribute('viewBox', '0 0 370 170'); this.svg.setAttribute('width', '370'); this.svg.setAttribute('height', '170'); this.svg.setAttribute('aria-label', 'Optical communication network debug view');
    this.element.append(this.summary, this.svg); host.append(this.element); this.clear();
  }

  public apply(message: OpticalSnapshotMessage): void {
    const s = message.statistics;
    this.summary.textContent = [
      `Optical tick ${s.tickCount} | connected ${s.connectedDemandCount}/${s.demandCount} | unavailable ${s.unavailableDemandCount}`,
      `Traffic ${s.allocatedGigabitsPerSecond.toFixed(2)}/${s.demandGigabitsPerSecond.toFixed(2)} Gbps | backhaul ${s.backhaulCapacityGigabitsPerSecond.toFixed(2)} Gbps`,
      `Congested ${s.congestedDemandCount} | degraded ${s.degradedDemandCount} | peak fiber ${(s.peakFiberUtilization * 100).toFixed(1)}%`,
    ].join('\n');
    this.render(message);
  }

  public clear(): void { this.summary.textContent = 'Optical: waiting for snapshot'; this.svg.replaceChildren(); }
  public dispose(): void { this.element.remove(); }

  private render(message: OpticalSnapshotMessage): void {
    this.svg.replaceChildren(); if (message.nodes.length === 0) return;
    const xs=message.nodes.map(n=>n.x), ys=message.nodes.map(n=>n.y); const minX=Math.min(...xs),maxX=Math.max(...xs),minY=Math.min(...ys),maxY=Math.max(...ys); const w=Math.max(1,maxX-minX),h=Math.max(1,maxY-minY); const positions=new Map<bigint,readonly[number,number]>();
    for(const node of message.nodes) positions.set(node.nodeId,[15+((node.x-minX)/w)*340,15+((node.y-minY)/h)*140]);
    for(const cable of message.fiberCables){const from=positions.get(cable.fromNodeId),to=positions.get(cable.toNodeId);if(from===undefined||to===undefined)continue;const line=document.createElementNS(SVG_NAMESPACE,'line');line.setAttribute('x1',from[0].toFixed(2));line.setAttribute('y1',from[1].toFixed(2));line.setAttribute('x2',to[0].toFixed(2));line.setAttribute('y2',to[1].toFixed(2));line.setAttribute('stroke',!cable.isInService?'#ef4444':cable.isCongested?'#f59e0b':'#38bdf8');line.setAttribute('stroke-width',String(1.5+Math.min(4,cable.utilization*4)));if(!cable.isInService)line.setAttribute('stroke-dasharray','5 4');this.svg.append(line);}
    for(const node of message.nodes){const p=positions.get(node.nodeId);if(p===undefined)continue;const circle=document.createElementNS(SVG_NAMESPACE,'circle');circle.setAttribute('cx',p[0].toFixed(2));circle.setAttribute('cy',p[1].toFixed(2));circle.setAttribute('r','5');circle.setAttribute('fill','#e0f2fe');circle.setAttribute('stroke','#0369a1');circle.setAttribute('stroke-width','2');this.svg.append(circle);}
    for(const demand of message.demands){const p=positions.get(demand.nodeId);if(p===undefined)continue;const ring=document.createElementNS(SVG_NAMESPACE,'circle');ring.setAttribute('cx',p[0].toFixed(2));ring.setAttribute('cy',p[1].toFixed(2));ring.setAttribute('r','10');ring.setAttribute('fill','none');ring.setAttribute('stroke-width','2');ring.setAttribute('stroke',demand.qualityState===OpticalQualityState.Unavailable?'#ef4444':demand.qualityState===OpticalQualityState.Congested?'#f59e0b':demand.qualityState===OpticalQualityState.Degraded?'#facc15':'#22c55e');this.svg.append(ring);}
  }
}
