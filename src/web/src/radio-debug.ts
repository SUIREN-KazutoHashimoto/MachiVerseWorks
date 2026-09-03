import { initializeLocalization, type Localizer } from './localization.ts';
import { RadioAntennaPatternKind, RadioLinkState, type RadioSnapshotMessage, type SpectrumSnapshotMessage } from './radio-protocol.ts';

const SVG_NAMESPACE='http://www.w3.org/2000/svg';

export class RadioDebugOverlay {
  private readonly element:HTMLDivElement;
  private readonly summary:HTMLPreElement;
  private readonly svg:SVGSVGElement;
  private spectrum:SpectrumSnapshotMessage|null=null;

  public constructor(host:HTMLElement,private readonly localizer:Localizer=initializeLocalization()){
    this.element=document.createElement('div');this.element.dataset.radioDebug='true';
    Object.assign(this.element.style,{position:'absolute',right:'12px',bottom:'12px',zIndex:'21',width:'430px',maxWidth:'48vw',padding:'8px 10px',background:'rgba(0,0,0,0.72)',color:'#fff',font:'12px/1.4 monospace',pointerEvents:'none'});
    this.summary=document.createElement('pre');Object.assign(this.summary.style,{margin:'0 0 6px',whiteSpace:'pre-wrap'});
    this.svg=document.createElementNS(SVG_NAMESPACE,'svg');this.svg.setAttribute('viewBox','0 0 410 190');this.svg.setAttribute('width','410');this.svg.setAttribute('height','190');this.svg.setAttribute('aria-label',this.localizer.t('radioDebug.ariaLabel'));
    this.element.append(this.summary,this.svg);host.append(this.element);this.clear();
  }

  public applyRadio(message:RadioSnapshotMessage):void{
    const s=message.statistics;const spectrum=this.spectrum;
    const channels=[...new Set(message.emissions.map(x=>`${x.centerFrequencyMegahertz.toFixed(1)}MHz/${x.bandwidthMegahertz.toFixed(1)}MHz`))];
    this.summary.textContent=[
      this.localizer.t('radioDebug.summary',{tick:this.localizer.formatNumber(s.tickCount),sites:this.localizer.formatNumber(s.siteCount),transmitters:this.localizer.formatNumber(message.transmitters.length),receivers:this.localizer.formatNumber(message.receivers.length),emissions:this.localizer.formatNumber(message.emissions.length)}),
      this.localizer.t('radioDebug.links',{healthy:this.localizer.formatNumber(s.healthyLinkCount),interfered:this.localizer.formatNumber(s.interferedLinkCount),unreachable:this.localizer.formatNumber(s.unreachableLinkCount),peak:this.localizer.formatNumber(s.peakSpectrumUtilization*100),conflicts:this.localizer.formatNumber(s.conflictCount)}),
      this.localizer.t('radioDebug.channels',{channels:channels.slice(0,4).join(', ')||'-',more:channels.length>4?' ...':''}),
      spectrum===null?this.localizer.t('radioDebug.spectrumWaiting'):this.localizer.t('radioDebug.spectrum',{bands:this.localizer.formatNumber(spectrum.bands.length),blocks:this.localizer.formatNumber(spectrum.frequencyBlocks.length),conflicts:this.localizer.formatNumber(spectrum.conflicts.length)}),
    ].join('\n');
    this.render(message);
  }

  public applySpectrum(message:SpectrumSnapshotMessage):void{this.spectrum=message;}
  public clear():void{this.spectrum=null;this.summary.textContent=this.localizer.t('radioDebug.waiting');this.svg.replaceChildren();}
  public dispose():void{this.element.remove();}

  private render(message:RadioSnapshotMessage):void{
    this.svg.replaceChildren();if(message.sites.length===0)return;
    const xs=message.sites.map(x=>x.x),ys=message.sites.map(x=>x.y);const minX=Math.min(...xs),maxX=Math.max(...xs),minY=Math.min(...ys),maxY=Math.max(...ys);const width=Math.max(1,maxX-minX),height=Math.max(1,maxY-minY);
    const positions=new Map<bigint,readonly[number,number]>();for(const site of message.sites)positions.set(site.siteId,[25+((site.x-minX)/width)*360,20+((site.y-minY)/height)*150]);
    const maxRadius=Math.max(1,...message.serviceAreas.map(x=>x.radiusMeters));
    for(const area of message.serviceAreas){const p=positions.get(area.siteId);if(p===undefined)continue;const circle=document.createElementNS(SVG_NAMESPACE,'circle');circle.setAttribute('cx',p[0].toFixed(2));circle.setAttribute('cy',p[1].toFixed(2));circle.setAttribute('r',String(8+Math.min(45,(area.radiusMeters/maxRadius)*38)));circle.setAttribute('fill','none');circle.setAttribute('stroke','rgba(56,189,248,0.35)');circle.setAttribute('stroke-width','1');this.svg.append(circle);}
    for(const link of message.links){const from=positions.get(link.fromSiteId),to=positions.get(link.toSiteId);if(from===undefined||to===undefined)continue;const line=document.createElementNS(SVG_NAMESPACE,'line');line.setAttribute('x1',from[0].toFixed(2));line.setAttribute('y1',from[1].toFixed(2));line.setAttribute('x2',to[0].toFixed(2));line.setAttribute('y2',to[1].toFixed(2));line.setAttribute('stroke',link.state===RadioLinkState.Healthy?'#22c55e':link.state===RadioLinkState.Marginal?'#facc15':link.state===RadioLinkState.Interfered?'#f97316':'#ef4444');line.setAttribute('stroke-width',String(1.2+Math.min(3,link.utilization*3)));if(link.state===RadioLinkState.OutOfService||link.state===RadioLinkState.Unreachable)line.setAttribute('stroke-dasharray','5 4');this.svg.append(line);}
    for(const antenna of message.antennas){const p=positions.get(antenna.siteId);if(p===undefined||antenna.patternKind!==RadioAntennaPatternKind.Directional)continue;const line=document.createElementNS(SVG_NAMESPACE,'line');line.setAttribute('x1',p[0].toFixed(2));line.setAttribute('y1',p[1].toFixed(2));line.setAttribute('x2',(p[0]+antenna.orientationX*18).toFixed(2));line.setAttribute('y2',(p[1]+antenna.orientationY*18).toFixed(2));line.setAttribute('stroke','#c4b5fd');line.setAttribute('stroke-width','2');this.svg.append(line);}
    for(const site of message.sites){const p=positions.get(site.siteId);if(p===undefined)continue;const circle=document.createElementNS(SVG_NAMESPACE,'circle');circle.setAttribute('cx',p[0].toFixed(2));circle.setAttribute('cy',p[1].toFixed(2));circle.setAttribute('r','5');circle.setAttribute('fill',site.isInService?'#e0f2fe':'#fecaca');circle.setAttribute('stroke',site.isInService?'#0284c7':'#dc2626');circle.setAttribute('stroke-width','2');this.svg.append(circle);}
  }
}
