import { PROTOCOL_HEADER_SIZE, PROTOCOL_MAGIC, PROTOCOL_MAX_PAYLOAD_LENGTH, ProtocolDecodeFailure, type ProtocolVersion } from './protocol.ts';

export const OPTICAL_SNAPSHOT_MESSAGE_TYPE = 780;
const FIXED_PAYLOAD_LENGTH = 86;
const NODE_LENGTH = 33;
const CABLE_LENGTH = 50;
const EQUIPMENT_LENGTH = 45;
const BACKHAUL_LENGTH = 42;
const DEMAND_LENGTH = 74;
const NUMERIC_TOLERANCE = 1e-9;

export enum OpticalNodeKind { BackboneGateway = 0, CentralOffice = 1, Distribution = 2, Access = 3, Endpoint = 4, DataCenter = 5 }
export enum OpticalEquipmentKind { Olt = 0, Onu = 1, Splitter = 2, Switch = 3, Router = 4 }
export enum OpticalDemandKind { Building = 0, Office = 1, DataCenter = 2, RadioBackhaul = 3 }
export enum OpticalQualityState { Healthy = 0, Congested = 1, Degraded = 2, Unavailable = 3 }

export interface OpticalStatistics { readonly nodeCount:number; readonly fiberCableCount:number; readonly equipmentCount:number; readonly backhaulCount:number; readonly demandCount:number; readonly connectedDemandCount:number; readonly congestedDemandCount:number; readonly degradedDemandCount:number; readonly unavailableDemandCount:number; readonly backhaulCapacityGigabitsPerSecond:number; readonly demandGigabitsPerSecond:number; readonly allocatedGigabitsPerSecond:number; readonly peakFiberUtilization:number; readonly tickCount:bigint; }
export interface OpticalNode { readonly nodeId:bigint; readonly kind:OpticalNodeKind; readonly x:number; readonly y:number; readonly z:number; }
export interface FiberCable { readonly cableId:bigint; readonly fromNodeId:bigint; readonly toNodeId:bigint; readonly capacityGigabitsPerSecond:number; readonly loadGigabitsPerSecond:number; readonly utilization:number; readonly isInService:boolean; readonly isCongested:boolean; }
export interface OpticalEquipment { readonly equipmentId:bigint; readonly nodeId:bigint; readonly kind:OpticalEquipmentKind; readonly buildingId:bigint; readonly establishmentId:bigint; readonly capacityGigabitsPerSecond:number; readonly requiresPower:boolean; readonly isInService:boolean; readonly isPowered:boolean; readonly isOperational:boolean; }
export interface OpticalBackhaul { readonly backhaulId:bigint; readonly nodeId:bigint; readonly capacityGigabitsPerSecond:number; readonly allocatedGigabitsPerSecond:number; readonly utilization:number; readonly isInService:boolean; readonly isOperational:boolean; }
export interface OpticalDemand { readonly demandId:bigint; readonly nodeId:bigint; readonly kind:OpticalDemandKind; readonly buildingId:bigint; readonly establishmentId:bigint; readonly baseDemandGigabitsPerSecond:number; readonly demandGigabitsPerSecond:number; readonly allocatedGigabitsPerSecond:number; readonly qualityState:OpticalQualityState; readonly backhaulId:bigint; readonly estimatedLatencyMilliseconds:number; }
export interface OpticalSnapshotMessage { readonly type:typeof OPTICAL_SNAPSHOT_MESSAGE_TYPE; readonly statistics:OpticalStatistics; readonly nodes:readonly OpticalNode[]; readonly fiberCables:readonly FiberCable[]; readonly equipment:readonly OpticalEquipment[]; readonly backhauls:readonly OpticalBackhaul[]; readonly demands:readonly OpticalDemand[]; }
export interface OpticalProtocolEnvelope { readonly version:ProtocolVersion; readonly message:OpticalSnapshotMessage; }
export type OpticalProtocolMessage = OpticalSnapshotMessage;

export function isOpticalFrame(frame:ArrayBuffer):boolean { if (frame.byteLength < PROTOCOL_HEADER_SIZE) return false; const v=new DataView(frame); return v.getUint32(0,true)===PROTOCOL_MAGIC && v.getUint16(8,true)===OPTICAL_SNAPSHOT_MESSAGE_TYPE; }

export function decodeOpticalFrame(frame:ArrayBuffer):OpticalProtocolEnvelope {
  if (frame.byteLength < PROTOCOL_HEADER_SIZE) throw new ProtocolDecodeFailure('Optical frame is shorter than the protocol header.');
  const v=new DataView(frame); if(v.getUint32(0,true)!==PROTOCOL_MAGIC) throw new ProtocolDecodeFailure('Optical frame magic is invalid.');
  const version:ProtocolVersion=Object.freeze({major:v.getUint16(4,true),minor:v.getUint16(6,true)}); if(version.major!==2||version.minor<15) throw new ProtocolDecodeFailure('Optical snapshots require Protocol 2.15 or newer.');
  if(v.getUint16(8,true)!==OPTICAL_SNAPSHOT_MESSAGE_TYPE||v.getUint16(10,true)!==0) throw new ProtocolDecodeFailure('Invalid Optical frame header.');
  const payloadLength=v.getUint32(12,true); if(payloadLength>PROTOCOL_MAX_PAYLOAD_LENGTH||PROTOCOL_HEADER_SIZE+payloadLength!==frame.byteLength||payloadLength<FIXED_PAYLOAD_LENGTH) throw new ProtocolDecodeFailure('Optical frame length is invalid.');
  const o=PROTOCOL_HEADER_SIZE; const nc=v.getUint16(o+76,true), cc=v.getUint16(o+78,true), ec=v.getUint16(o+80,true), bc=v.getUint16(o+82,true), dc=v.getUint16(o+84,true);
  const expected=FIXED_PAYLOAD_LENGTH+nc*NODE_LENGTH+cc*CABLE_LENGTH+ec*EQUIPMENT_LENGTH+bc*BACKHAUL_LENGTH+dc*DEMAND_LENGTH; if(payloadLength!==expected) throw new ProtocolDecodeFailure('Optical payload counts do not match its length.');
  const statistics:OpticalStatistics={nodeCount:v.getUint32(o,true),fiberCableCount:v.getUint32(o+4,true),equipmentCount:v.getUint32(o+8,true),backhaulCount:v.getUint32(o+12,true),demandCount:v.getUint32(o+16,true),connectedDemandCount:v.getUint32(o+20,true),congestedDemandCount:v.getUint32(o+24,true),degradedDemandCount:v.getUint32(o+28,true),unavailableDemandCount:v.getUint32(o+32,true),backhaulCapacityGigabitsPerSecond:v.getFloat64(o+36,true),demandGigabitsPerSecond:v.getFloat64(o+44,true),allocatedGigabitsPerSecond:v.getFloat64(o+52,true),peakFiberUtilization:v.getFloat64(o+60,true),tickCount:v.getBigUint64(o+68,true)};
  if (![statistics.backhaulCapacityGigabitsPerSecond, statistics.demandGigabitsPerSecond, statistics.allocatedGigabitsPerSecond, statistics.peakFiberUtilization].every(nonNegative)) throw new ProtocolDecodeFailure('Optical statistics contain invalid values.');

  let c=o+FIXED_PAYLOAD_LENGTH;
  const nodes:OpticalNode[]=[];
  for(let i=0;i<nc;i++){
    const node={nodeId:v.getBigUint64(c,true),kind:v.getUint8(c+8) as OpticalNodeKind,x:v.getFloat64(c+9,true),y:v.getFloat64(c+17,true),z:v.getFloat64(c+25,true)};
    if(node.nodeId===0n || !enumRange(node.kind, OpticalNodeKind.BackboneGateway, OpticalNodeKind.DataCenter) || !finite3(node.x,node.y,node.z)) throw new ProtocolDecodeFailure('Optical node entry is invalid.');
    nodes.push(node); c+=NODE_LENGTH;
  }

  const fiberCables:FiberCable[]=[];
  for(let i=0;i<cc;i++){
    const a=v.getUint8(c+48),b=v.getUint8(c+49); if(a>1||b>1)throw new ProtocolDecodeFailure('Invalid Optical cable flags.');
    const cable={cableId:v.getBigUint64(c,true),fromNodeId:v.getBigUint64(c+8,true),toNodeId:v.getBigUint64(c+16,true),capacityGigabitsPerSecond:v.getFloat64(c+24,true),loadGigabitsPerSecond:v.getFloat64(c+32,true),utilization:v.getFloat64(c+40,true),isInService:a!==0,isCongested:b!==0};
    if(cable.cableId===0n || cable.fromNodeId===0n || cable.toNodeId===0n || cable.fromNodeId===cable.toNodeId || !positive(cable.capacityGigabitsPerSecond) || !nonNegative(cable.loadGigabitsPerSecond) || !boundedUtilization(cable.utilization)) throw new ProtocolDecodeFailure('Optical cable entry is invalid.');
    fiberCables.push(cable); c+=CABLE_LENGTH;
  }

  const equipment:OpticalEquipment[]=[];
  for(let i=0;i<ec;i++){
    const item={equipmentId:v.getBigUint64(c,true),nodeId:v.getBigUint64(c+8,true),kind:v.getUint8(c+16) as OpticalEquipmentKind,buildingId:v.getBigUint64(c+17,true),establishmentId:v.getBigUint64(c+25,true),capacityGigabitsPerSecond:v.getFloat64(c+33,true),requiresPower:v.getUint8(c+41)!==0,isInService:v.getUint8(c+42)!==0,isPowered:v.getUint8(c+43)!==0,isOperational:v.getUint8(c+44)!==0};
    if(item.equipmentId===0n || item.nodeId===0n || !enumRange(item.kind, OpticalEquipmentKind.Olt, OpticalEquipmentKind.Router) || !positive(item.capacityGigabitsPerSecond)) throw new ProtocolDecodeFailure('Optical equipment entry is invalid.');
    equipment.push(item); c+=EQUIPMENT_LENGTH;
  }

  const backhauls:OpticalBackhaul[]=[];
  for(let i=0;i<bc;i++){
    const item={backhaulId:v.getBigUint64(c,true),nodeId:v.getBigUint64(c+8,true),capacityGigabitsPerSecond:v.getFloat64(c+16,true),allocatedGigabitsPerSecond:v.getFloat64(c+24,true),utilization:v.getFloat64(c+32,true),isInService:v.getUint8(c+40)!==0,isOperational:v.getUint8(c+41)!==0};
    if(item.backhaulId===0n || item.nodeId===0n || !positive(item.capacityGigabitsPerSecond) || !nonNegative(item.allocatedGigabitsPerSecond) || !boundedUtilization(item.utilization)) throw new ProtocolDecodeFailure('Optical backhaul entry is invalid.');
    backhauls.push(item); c+=BACKHAUL_LENGTH;
  }

  const demands:OpticalDemand[]=[];
  for(let i=0;i<dc;i++){
    const item={demandId:v.getBigUint64(c,true),nodeId:v.getBigUint64(c+8,true),kind:v.getUint8(c+16) as OpticalDemandKind,buildingId:v.getBigUint64(c+17,true),establishmentId:v.getBigUint64(c+25,true),baseDemandGigabitsPerSecond:v.getFloat64(c+33,true),demandGigabitsPerSecond:v.getFloat64(c+41,true),allocatedGigabitsPerSecond:v.getFloat64(c+49,true),qualityState:v.getUint8(c+57) as OpticalQualityState,backhaulId:v.getBigUint64(c+58,true),estimatedLatencyMilliseconds:v.getFloat64(c+66,true)};
    if(item.demandId===0n || item.nodeId===0n || !enumRange(item.kind, OpticalDemandKind.Building, OpticalDemandKind.RadioBackhaul) || !enumRange(item.qualityState, OpticalQualityState.Healthy, OpticalQualityState.Unavailable) || !positive(item.baseDemandGigabitsPerSecond) || !nonNegative(item.demandGigabitsPerSecond) || !nonNegative(item.allocatedGigabitsPerSecond) || !nonNegative(item.estimatedLatencyMilliseconds)) throw new ProtocolDecodeFailure('Optical demand entry is invalid.');
    demands.push(item); c+=DEMAND_LENGTH;
  }
  return {version,message:{type:OPTICAL_SNAPSHOT_MESSAGE_TYPE,statistics,nodes,fiberCables,equipment,backhauls,demands}};
}
function positive(x:number):boolean{return Number.isFinite(x)&&x>0;}
function nonNegative(x:number):boolean{return Number.isFinite(x)&&x>=0;}
function finite3(x:number,y:number,z:number):boolean{return Number.isFinite(x)&&Number.isFinite(y)&&Number.isFinite(z);}
function enumRange(value:number,min:number,max:number):boolean{return Number.isInteger(value)&&value>=min&&value<=max;}
function boundedUtilization(x:number):boolean{return nonNegative(x)&&x<=1+NUMERIC_TOLERANCE;}
