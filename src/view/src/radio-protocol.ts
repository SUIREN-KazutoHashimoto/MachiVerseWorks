import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, PROTOCOL_MAX_PAYLOAD_LENGTH, ProtocolDecodeFailure, type ProtocolVersion } from './protocol.ts';

export const RADIO_SNAPSHOT_MESSAGE_TYPE = 790;
export const SPECTRUM_SNAPSHOT_MESSAGE_TYPE = 791;
const RADIO_FIXED_LENGTH = 66;
const SITE_LENGTH = 50;
const ANTENNA_LENGTH = 90;
const TRANSMITTER_LENGTH = 34;
const RECEIVER_LENGTH = 50;
const EMISSION_LENGTH = 58;
const LINK_LENGTH = 82;
const SERVICE_AREA_LENGTH = 32;
const SPECTRUM_FIXED_LENGTH = 14;
const FREQUENCY_BLOCK_LENGTH = 32;
const utf8Decoder = new TextDecoder('utf-8', { fatal: true });

export enum RadioSiteKind { Macro = 0, Micro = 1, SmallCell = 2, PointToPoint = 3, Gateway = 4 }
export enum RadioLinkState { Healthy = 0, Marginal = 1, Interfered = 2, Unreachable = 3, OutOfService = 4 }
export enum RadioAntennaPatternKind { Omnidirectional = 0, Directional = 1 }

export interface RadioStatistics { readonly siteCount:number; readonly bandCount:number; readonly frequencyBlockCount:number; readonly linkCount:number; readonly serviceAreaCount:number; readonly conflictCount:number; readonly healthyLinkCount:number; readonly interferedLinkCount:number; readonly unreachableLinkCount:number; readonly peakSpectrumUtilization:number; readonly tickCount:bigint; }
export interface RadioSite { readonly siteId:bigint; readonly kind:RadioSiteKind; readonly x:number; readonly y:number; readonly z:number; readonly antennaGainDb:number; readonly antennaHeightMeters:number; readonly isInService:boolean; }
export interface RadioAntenna { readonly antennaId:bigint; readonly siteId:bigint; readonly offsetX:number; readonly offsetY:number; readonly offsetZ:number; readonly orientationX:number; readonly orientationY:number; readonly orientationZ:number; readonly gainDb:number; readonly patternKind:RadioAntennaPatternKind; readonly beamwidthDegrees:number; readonly frontToBackRatioDb:number; readonly isInService:boolean; }
export interface RadioTransmitter { readonly transmitterId:bigint; readonly siteId:bigint; readonly antennaId:bigint; readonly maximumTransmitPowerDbm:number; readonly isInService:boolean; readonly isOperational:boolean; }
export interface RadioReceiver { readonly receiverId:bigint; readonly siteId:bigint; readonly antennaId:bigint; readonly minimumFrequencyMegahertz:number; readonly maximumFrequencyMegahertz:number; readonly sensitivityDbm:number; readonly isInService:boolean; readonly isOperational:boolean; }
export interface RadioEmission { readonly emissionId:bigint; readonly transmitterId:bigint; readonly channelId:bigint; readonly centerFrequencyMegahertz:number; readonly bandwidthMegahertz:number; readonly transmitPowerDbm:number; readonly utilization:number; readonly isInService:boolean; readonly isOperational:boolean; }
export interface RadioLink { readonly linkId:bigint; readonly fromSiteId:bigint; readonly toSiteId:bigint; readonly frequencyBlockId:bigint; readonly distanceMeters:number; readonly pathLossDb:number; readonly receivedPowerDbm:number; readonly interferenceDbm:number; readonly sinrDb:number; readonly utilization:number; readonly state:RadioLinkState; readonly isInService:boolean; }
export interface RadioServiceArea { readonly siteId:bigint; readonly frequencyBlockId:bigint; readonly radiusMeters:number; readonly minimumSinrDb:number; }
export interface RadioSnapshotMessage { readonly type:typeof RADIO_SNAPSHOT_MESSAGE_TYPE; readonly statistics:RadioStatistics; readonly sites:readonly RadioSite[]; readonly antennas:readonly RadioAntenna[]; readonly transmitters:readonly RadioTransmitter[]; readonly receivers:readonly RadioReceiver[]; readonly emissions:readonly RadioEmission[]; readonly links:readonly RadioLink[]; readonly serviceAreas:readonly RadioServiceArea[]; }
export interface SpectrumBand { readonly bandId:bigint; readonly name:string; readonly minimumFrequencyMegahertz:number; readonly maximumFrequencyMegahertz:number; }
export interface FrequencyBlock { readonly frequencyBlockId:bigint; readonly bandId:bigint; readonly centerFrequencyMegahertz:number; readonly bandwidthMegahertz:number; }
export interface SpectrumConflict { readonly firstBlockId:bigint; readonly secondBlockId:bigint; readonly firstSiteId:bigint; readonly secondSiteId:bigint; readonly reason:string; }
export interface SpectrumSnapshotMessage { readonly type:typeof SPECTRUM_SNAPSHOT_MESSAGE_TYPE; readonly tickCount:bigint; readonly bands:readonly SpectrumBand[]; readonly frequencyBlocks:readonly FrequencyBlock[]; readonly conflicts:readonly SpectrumConflict[]; }
export type RadioProtocolMessage = RadioSnapshotMessage | SpectrumSnapshotMessage;
export interface RadioProtocolEnvelope { readonly version:ProtocolVersion; readonly message:RadioProtocolMessage; }

export function isRadioFrame(frame:ArrayBuffer):boolean {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false;
  const view = new DataView(frame);
  if (view.getUint32(0,true) !== PROTOCOL_MAGIC) return false;
  const type = view.getUint16(8,true);
  return type === RADIO_SNAPSHOT_MESSAGE_TYPE || type === SPECTRUM_SNAPSHOT_MESSAGE_TYPE;
}

export function decodeRadioFrame(frame:ArrayBuffer):RadioProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Radio frame is shorter than the protocol header.');
  const view = new DataView(frame);
  if (view.getUint32(0,true) !== PROTOCOL_MAGIC || view.getUint16(10,true) !== 0) throw new ProtocolDecodeFailure('Invalid Radio frame header.');
  const version:ProtocolVersion = Object.freeze({major:view.getUint16(4,true),minor:view.getUint16(6,true)});
  if (version.major !== 2 || version.minor < 16) throw new ProtocolDecodeFailure('Radio snapshots require Protocol 2.16 or newer.');
  const payloadLength = view.getUint32(12,true);
  if (payloadLength > PROTOCOL_MAX_PAYLOAD_LENGTH || PROTOCOL_HEADER_SIZE + payloadLength !== frame.byteLength) throw new ProtocolDecodeFailure('Radio frame length is invalid.');
  const type = view.getUint16(8,true);
  if (type === RADIO_SNAPSHOT_MESSAGE_TYPE) return {version,message:decodeRadioSnapshot(view,payloadLength)};
  if (type === SPECTRUM_SNAPSHOT_MESSAGE_TYPE) return {version,message:decodeSpectrumSnapshot(view,payloadLength)};
  throw new ProtocolDecodeFailure('Unknown Radio message type.');
}

function decodeRadioSnapshot(view:DataView,payloadLength:number):RadioSnapshotMessage {
  if (payloadLength < RADIO_FIXED_LENGTH) throw new ProtocolDecodeFailure('Radio snapshot payload is too short.');
  const o=PROTOCOL_HEADER_SIZE;
  const siteCount=view.getUint16(o+52,true), antennaCount=view.getUint16(o+54,true), transmitterCount=view.getUint16(o+56,true), receiverCount=view.getUint16(o+58,true), emissionCount=view.getUint16(o+60,true), linkCount=view.getUint16(o+62,true), areaCount=view.getUint16(o+64,true);
  const expected=RADIO_FIXED_LENGTH+siteCount*SITE_LENGTH+antennaCount*ANTENNA_LENGTH+transmitterCount*TRANSMITTER_LENGTH+receiverCount*RECEIVER_LENGTH+emissionCount*EMISSION_LENGTH+linkCount*LINK_LENGTH+areaCount*SERVICE_AREA_LENGTH;
  if(payloadLength!==expected) throw new ProtocolDecodeFailure('Radio snapshot counts do not match payload length.');
  const statistics:RadioStatistics={siteCount:view.getUint32(o,true),bandCount:view.getUint32(o+4,true),frequencyBlockCount:view.getUint32(o+8,true),linkCount:view.getUint32(o+12,true),serviceAreaCount:view.getUint32(o+16,true),conflictCount:view.getUint32(o+20,true),healthyLinkCount:view.getUint32(o+24,true),interferedLinkCount:view.getUint32(o+28,true),unreachableLinkCount:view.getUint32(o+32,true),peakSpectrumUtilization:view.getFloat64(o+36,true),tickCount:view.getBigUint64(o+44,true)};
  let c=o+RADIO_FIXED_LENGTH;
  const sites:RadioSite[]=[]; for(let i=0;i<siteCount;i++){sites.push({siteId:view.getBigUint64(c,true),kind:view.getUint8(c+8) as RadioSiteKind,x:view.getFloat64(c+9,true),y:view.getFloat64(c+17,true),z:view.getFloat64(c+25,true),antennaGainDb:view.getFloat64(c+33,true),antennaHeightMeters:view.getFloat64(c+41,true),isInService:readBool(view,c+49)});c+=SITE_LENGTH;}
  const antennas:RadioAntenna[]=[]; for(let i=0;i<antennaCount;i++){antennas.push({antennaId:view.getBigUint64(c,true),siteId:view.getBigUint64(c+8,true),offsetX:view.getFloat64(c+16,true),offsetY:view.getFloat64(c+24,true),offsetZ:view.getFloat64(c+32,true),orientationX:view.getFloat64(c+40,true),orientationY:view.getFloat64(c+48,true),orientationZ:view.getFloat64(c+56,true),gainDb:view.getFloat64(c+64,true),patternKind:view.getUint8(c+72) as RadioAntennaPatternKind,beamwidthDegrees:view.getFloat64(c+73,true),frontToBackRatioDb:view.getFloat64(c+81,true),isInService:readBool(view,c+89)});c+=ANTENNA_LENGTH;}
  const transmitters:RadioTransmitter[]=[]; for(let i=0;i<transmitterCount;i++){transmitters.push({transmitterId:view.getBigUint64(c,true),siteId:view.getBigUint64(c+8,true),antennaId:view.getBigUint64(c+16,true),maximumTransmitPowerDbm:view.getFloat64(c+24,true),isInService:readBool(view,c+32),isOperational:readBool(view,c+33)});c+=TRANSMITTER_LENGTH;}
  const receivers:RadioReceiver[]=[]; for(let i=0;i<receiverCount;i++){receivers.push({receiverId:view.getBigUint64(c,true),siteId:view.getBigUint64(c+8,true),antennaId:view.getBigUint64(c+16,true),minimumFrequencyMegahertz:view.getFloat64(c+24,true),maximumFrequencyMegahertz:view.getFloat64(c+32,true),sensitivityDbm:view.getFloat64(c+40,true),isInService:readBool(view,c+48),isOperational:readBool(view,c+49)});c+=RECEIVER_LENGTH;}
  const emissions:RadioEmission[]=[]; for(let i=0;i<emissionCount;i++){emissions.push({emissionId:view.getBigUint64(c,true),transmitterId:view.getBigUint64(c+8,true),channelId:view.getBigUint64(c+16,true),centerFrequencyMegahertz:view.getFloat64(c+24,true),bandwidthMegahertz:view.getFloat64(c+32,true),transmitPowerDbm:view.getFloat64(c+40,true),utilization:view.getFloat64(c+48,true),isInService:readBool(view,c+56),isOperational:readBool(view,c+57)});c+=EMISSION_LENGTH;}
  const links:RadioLink[]=[]; for(let i=0;i<linkCount;i++){links.push({linkId:view.getBigUint64(c,true),fromSiteId:view.getBigUint64(c+8,true),toSiteId:view.getBigUint64(c+16,true),frequencyBlockId:view.getBigUint64(c+24,true),distanceMeters:view.getFloat64(c+32,true),pathLossDb:view.getFloat64(c+40,true),receivedPowerDbm:view.getFloat64(c+48,true),interferenceDbm:view.getFloat64(c+56,true),sinrDb:view.getFloat64(c+64,true),utilization:view.getFloat64(c+72,true),state:view.getUint8(c+80) as RadioLinkState,isInService:readBool(view,c+81)});c+=LINK_LENGTH;}
  const serviceAreas:RadioServiceArea[]=[]; for(let i=0;i<areaCount;i++){serviceAreas.push({siteId:view.getBigUint64(c,true),frequencyBlockId:view.getBigUint64(c+8,true),radiusMeters:view.getFloat64(c+16,true),minimumSinrDb:view.getFloat64(c+24,true)});c+=SERVICE_AREA_LENGTH;}
  validateRadioSnapshot(statistics,sites,antennas,transmitters,receivers,emissions,links,serviceAreas);
  return {type:RADIO_SNAPSHOT_MESSAGE_TYPE,statistics,sites,antennas,transmitters,receivers,emissions,links,serviceAreas};
}

function decodeSpectrumSnapshot(view:DataView,payloadLength:number):SpectrumSnapshotMessage {
  if(payloadLength<SPECTRUM_FIXED_LENGTH)throw new ProtocolDecodeFailure('Spectrum snapshot payload is too short.');
  const o=PROTOCOL_HEADER_SIZE, end=o+payloadLength; const tickCount=view.getBigUint64(o,true); const bandCount=view.getUint16(o+8,true),blockCount=view.getUint16(o+10,true),conflictCount=view.getUint16(o+12,true); let c=o+SPECTRUM_FIXED_LENGTH;
  const bands:SpectrumBand[]=[]; for(let i=0;i<bandCount;i++){ensureBytes(c,26,end);const bandId=view.getBigUint64(c,true),minimumFrequencyMegahertz=view.getFloat64(c+8,true),maximumFrequencyMegahertz=view.getFloat64(c+16,true),length=view.getUint16(c+24,true);ensureBytes(c+26,length,end);const name=decodeText(view,c+26,length);bands.push({bandId,name,minimumFrequencyMegahertz,maximumFrequencyMegahertz});c+=26+length;}
  const frequencyBlocks:FrequencyBlock[]=[]; for(let i=0;i<blockCount;i++){ensureBytes(c,FREQUENCY_BLOCK_LENGTH,end);frequencyBlocks.push({frequencyBlockId:view.getBigUint64(c,true),bandId:view.getBigUint64(c+8,true),centerFrequencyMegahertz:view.getFloat64(c+16,true),bandwidthMegahertz:view.getFloat64(c+24,true)});c+=FREQUENCY_BLOCK_LENGTH;}
  const conflicts:SpectrumConflict[]=[]; for(let i=0;i<conflictCount;i++){ensureBytes(c,34,end);const firstBlockId=view.getBigUint64(c,true),secondBlockId=view.getBigUint64(c+8,true),firstSiteId=view.getBigUint64(c+16,true),secondSiteId=view.getBigUint64(c+24,true),length=view.getUint16(c+32,true);ensureBytes(c+34,length,end);const reason=decodeText(view,c+34,length);conflicts.push({firstBlockId,secondBlockId,firstSiteId,secondSiteId,reason});c+=34+length;}
  if(c!==end)throw new ProtocolDecodeFailure('Spectrum snapshot contains trailing data.');
  const bandIds=new Set(bands.map(x=>x.bandId)); const blockIds=new Set(frequencyBlocks.map(x=>x.frequencyBlockId));
  if(bandIds.has(0n)||bandIds.size!==bands.length||bands.some(x=>x.name.trim().length===0||!positive(x.minimumFrequencyMegahertz)||!positive(x.maximumFrequencyMegahertz)||x.maximumFrequencyMegahertz<=x.minimumFrequencyMegahertz)
    ||blockIds.has(0n)||blockIds.size!==frequencyBlocks.length||frequencyBlocks.some(x=>!bandIds.has(x.bandId)||!positive(x.centerFrequencyMegahertz)||!positive(x.bandwidthMegahertz))
    ||conflicts.some(x=>!blockIds.has(x.firstBlockId)||!blockIds.has(x.secondBlockId)||x.firstSiteId===0n||x.secondSiteId===0n||x.reason.trim().length===0))throw new ProtocolDecodeFailure('Spectrum snapshot contains invalid values or references.');
  return {type:SPECTRUM_SNAPSHOT_MESSAGE_TYPE,tickCount,bands,frequencyBlocks,conflicts};
}

function validateRadioSnapshot(statistics:RadioStatistics,sites:readonly RadioSite[],antennas:readonly RadioAntenna[],transmitters:readonly RadioTransmitter[],receivers:readonly RadioReceiver[],emissions:readonly RadioEmission[],links:readonly RadioLink[],areas:readonly RadioServiceArea[]):void {
  if(!nonNegative(statistics.peakSpectrumUtilization)||statistics.peakSpectrumUtilization>1+1e-9)throw new ProtocolDecodeFailure('Radio statistics are invalid.');
  const siteIds=new Set(sites.map(x=>x.siteId)); const antennaIds=new Set(antennas.map(x=>x.antennaId)); const transmitterIds=new Set(transmitters.map(x=>x.transmitterId)); const receiverIds=new Set(receivers.map(x=>x.receiverId)); const linkIds=new Set(links.map(x=>x.linkId));
  if(siteIds.has(0n)||siteIds.size!==sites.length||sites.some(x=>!enumRange(x.kind,0,4)||!finite(x.x)||!finite(x.y)||!finite(x.z)||!finite(x.antennaGainDb)||!nonNegative(x.antennaHeightMeters)))throw new ProtocolDecodeFailure('Radio sites contain invalid values.');
  if(antennaIds.has(0n)||antennaIds.size!==antennas.length||antennas.some(x=>!siteIds.has(x.siteId)||!finite(x.offsetX)||!finite(x.offsetY)||!finite(x.offsetZ)||!finite(x.orientationX)||!finite(x.orientationY)||!finite(x.orientationZ)||!finite(x.gainDb)||!enumRange(x.patternKind,0,1)||!positive(x.beamwidthDegrees)||x.beamwidthDegrees>360||!nonNegative(x.frontToBackRatioDb)))throw new ProtocolDecodeFailure('Radio antennas contain invalid values.');
  if(transmitterIds.has(0n)||transmitterIds.size!==transmitters.length||transmitters.some(x=>!siteIds.has(x.siteId)||!antennaIds.has(x.antennaId)||!finite(x.maximumTransmitPowerDbm)))throw new ProtocolDecodeFailure('Radio transmitters contain invalid values.');
  if(receiverIds.has(0n)||receiverIds.size!==receivers.length||receivers.some(x=>!siteIds.has(x.siteId)||!antennaIds.has(x.antennaId)||!positive(x.minimumFrequencyMegahertz)||!positive(x.maximumFrequencyMegahertz)||x.maximumFrequencyMegahertz<=x.minimumFrequencyMegahertz||!finite(x.sensitivityDbm)||x.sensitivityDbm>=0))throw new ProtocolDecodeFailure('Radio receivers contain invalid values.');
  if(emissions.some(x=>x.emissionId===0n||!transmitterIds.has(x.transmitterId)||x.channelId===0n||!positive(x.centerFrequencyMegahertz)||!positive(x.bandwidthMegahertz)||!finite(x.transmitPowerDbm)||!nonNegative(x.utilization)||x.utilization>1+1e-9))throw new ProtocolDecodeFailure('Radio emissions contain invalid values.');
  if(linkIds.has(0n)||linkIds.size!==links.length||links.some(x=>x.fromSiteId===0n||x.toSiteId===0n||x.fromSiteId===x.toSiteId||!siteIds.has(x.fromSiteId)||!siteIds.has(x.toSiteId)||x.frequencyBlockId===0n||!nonNegative(x.distanceMeters)||!finite(x.pathLossDb)||!finite(x.receivedPowerDbm)||!finite(x.interferenceDbm)||!finite(x.sinrDb)||!nonNegative(x.utilization)||x.utilization>1+1e-9||!enumRange(x.state,0,4)))throw new ProtocolDecodeFailure('Radio links contain invalid values.');
  if(areas.some(x=>!siteIds.has(x.siteId)||x.frequencyBlockId===0n||!nonNegative(x.radiusMeters)||!finite(x.minimumSinrDb)))throw new ProtocolDecodeFailure('Radio service areas contain invalid values.');
}
function readBool(view:DataView,offset:number):boolean{const value=view.getUint8(offset);if(value>1)throw new ProtocolDecodeFailure('Radio boolean flag is invalid.');return value!==0;}
function ensureBytes(offset:number,length:number,end:number):void{if(offset<0||length<0||offset>end-length)throw new ProtocolDecodeFailure('Radio variable payload is truncated.');}
function decodeText(view:DataView,offset:number,length:number):string{try{return utf8Decoder.decode(new Uint8Array(view.buffer,view.byteOffset+offset,length));}catch{return '';}}
function finite(x:number):boolean{return Number.isFinite(x);} function positive(x:number):boolean{return Number.isFinite(x)&&x>0;} function nonNegative(x:number):boolean{return Number.isFinite(x)&&x>=0;} function enumRange(x:number,min:number,max:number):boolean{return Number.isInteger(x)&&x>=min&&x<=max;}
