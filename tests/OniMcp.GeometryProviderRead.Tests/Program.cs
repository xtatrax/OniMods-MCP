using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;
internal static class Program {
 static int checks;
 static void Check(bool value,string name){checks++;if(!value)throw new Exception(name);}
 static ConduitPortInfo Info(ConduitType type=ConduitType.Gas)=>new(){conduitType=type,offset=type==ConduitType.Gas?new CellOffset(1,0):type==ConduitType.Liquid?new CellOffset(0,1):new CellOffset(-1,0)};
 static object[] Known()=>new object[]{new ConduitSecondaryInput{portInfo=Info()},new ConduitPreferentialFlow{portInfo=Info()},new JetSuitLocker{portInfo=Info()},new ConduitSecondaryOutput{portInfo=Info()},new ConduitOverflow{portInfo=Info()},new ElementFilter{portInfo=Info()},new RailGun{gasPortInfo=Info(),liquidPortInfo=Info(ConduitType.Liquid),solidPortInfo=Info(ConduitType.Solid)},new WarpConduitSender{gasPortInfo=Info(),liquidPortInfo=Info(ConduitType.Liquid),solidPortInfo=Info(ConduitType.Solid)},new WarpConduitReceiver{gasPortInfo=Info(),liquidPortInfo=Info(ConduitType.Liquid),solidPortInfo=Info(ConduitType.Solid)}};
 static (int Has,int Offset) Calls(object p)=>p switch{SecondaryInputBoundary x=>(x.HasCalls,x.OffsetCalls),SecondaryOutputBoundary x=>(x.HasCalls,x.OffsetCalls),MultiInputBoundary x=>(x.HasCalls,x.OffsetCalls),WarpConduitReceiver x=>(x.HasCalls,x.OffsetCalls),_=>(0,0)};
 static BuildingGeometrySnapshot Snapshot(object[] providers,out BuildingDef def,Orientation orientation=Orientation.Neutral,bool instance=false){
  var go=new GameObject();foreach(var provider in providers)go.Add(provider);
  def=new BuildingDef{BuildingComplete=go,RequiresPowerInput=true,PowerInputOffset=new CellOffset(0,0)};
  Building building=null;if(instance){building=new BuildingComplete{Def=def,Orientation=orientation};go.Add(building);}
  return BuildingGeometryTools.Snapshot(building,def,orientation);
 }
 static void MarkUnavailable(object p,int slot=0){
  if(p is SecondaryInputBoundary input)input.portInfo=null;
  else if(p is SecondaryOutputBoundary output)output.portInfo=null;
  else if(p is MultiInputBoundary multi){if(slot==0)multi.gasPortInfo=null;else if(slot==1)multi.liquidPortInfo=null;else multi.solidPortInfo=null;}
  else if(p is WarpConduitReceiver receiver){if(slot==0)receiver.gasPortInfo=null;else if(slot==1)receiver.liquidPortInfo=null;else receiver.solidPortInfo=null;}
 }
 static void Main(){
  var unknownInput=new UnknownInput();var unknownOutput=new UnknownOutput();
  var snapshot=Snapshot(new object[]{unknownInput,unknownOutput},out var def);
  Check(unknownInput.Calls==0 && unknownOutput.Calls==0,"geometry read must reject unknown secondary providers BEFORE interface callbacks");
  Check(!snapshot.PortsComplete && snapshot.Errors.Contains("secondary_input_provider_unsupported") && snapshot.Errors.Contains("secondary_output_provider_unsupported"),"unknown inputs/outputs explicitly mark ports incomplete");
  Check(snapshot.Ports.Count==1 && snapshot.Ports[0].Kind=="power","unknown secondary providers preserve observed primary ports");

  var derivedInput=new DerivedInput{portInfo=Info()};var derivedOutput=new DerivedOutput{portInfo=Info()};
  snapshot=Snapshot(new object[]{derivedInput,derivedOutput},out def);
  Check(derivedInput.Calls==0 && derivedOutput.Calls==0 && Calls(derivedInput)==(0,0) && Calls(derivedOutput)==(0,0),"native subclasses cannot run reimplemented callbacks");
  Check(!snapshot.PortsComplete && snapshot.Errors.Count==2,"native subclasses are explicit unsupported observations");

  foreach(bool instance in new[]{false,true})foreach(var orientation in new[]{Orientation.Neutral,Orientation.R90})foreach(var provider in Known()){
   snapshot=Snapshot(new[]{provider},out def,orientation,instance);
   string label=provider.GetType().Name+"/"+orientation+"/"+instance;
   int count=provider is MultiInputBoundary||provider is WarpConduitReceiver?3:1;
   Check(snapshot.PortsComplete && snapshot.Errors.Count==0,label+": known ready native provider complete");
   var ports=snapshot.Ports.Where(p=>p.Direction.StartsWith("secondary_")).ToArray();
   Check(ports.Length==count && Calls(provider)==(3,count),label+": native Has/Get called only for supported conduit types");
   Check(ports.All(p=>p.Id==provider.GetType().Name && p.Source.StartsWith("ISecondary")),label+": native source identity retained");
   foreach(var port in ports){var raw=new CellOffset(port.Offset[0],port.Offset[1]);var rotated=Rotatable.GetRotatedCellOffset(raw,orientation);int x=(instance?2:0)+rotated.x,y=(instance?2:0)+rotated.y;
    Check(port.Position.X==x && port.Position.Y==y,label+": forward native offset and requested rotation");
   }
   foreach(bool compact in new[]{true,false})Check((bool)snapshot.ToDictionary(compact)["portsComplete"],label+": projected port completeness retained");
  }
  foreach(var provider in Known()){
   MarkUnavailable(provider);snapshot=Snapshot(new[]{provider},out def);
   Check(Calls(provider)==(0,0),provider.GetType().Name+": missing cached port fields refuse before callbacks");
   Check(!snapshot.PortsComplete && snapshot.Errors.Single().EndsWith("provider_unavailable") && snapshot.Ports.Count==1,provider.GetType().Name+": cold native ports are incomplete and primary ports remain");
  }
  foreach(int slot in new[]{0,1,2})foreach(var provider in Known().Where(p=>p is MultiInputBoundary||p is WarpConduitReceiver)){
   MarkUnavailable(provider,slot);snapshot=Snapshot(new[]{provider},out def);
   Check(Calls(provider)==(0,0) && !snapshot.PortsComplete && snapshot.Ports.Count==1,"all cached multiport fields must be ready before any callback: "+provider.GetType().Name+"/"+slot);
  }
  var known=new ConduitSecondaryOutput{portInfo=Info()};unknownInput=new UnknownInput();
  snapshot=Snapshot(new object[]{unknownInput,known},out def);
  Check(!snapshot.PortsComplete && unknownInput.Calls==0 && Calls(known)==(3,1),"unknown provider does not hide independent known-native ports");
  Check(snapshot.Ports.Count==2,"mixed unknown/known observation retains primary and known secondary port");
  foreach(bool compact in new[]{true,false}){
   Assets.Defs[def.PrefabID]=def;
   var result=BuildingGeometryTools.Read(new JObject{{"action","definition"},{"prefabId",def.PrefabID},{"compact",compact}});
   Check(!result.IsError,"full geometry API returns an honest partial observation");var item=JObject.Parse(result.Value)["items"][0];
   Check(!(bool)item["portsComplete"] && item["errors"].Values<string>().Contains("secondary_input_provider_unsupported"),"full geometry API preserves unsupported status in both profiles");
  }
  Console.WriteLine($"PASS {checks} geometry actual-reader provider-boundary checks");
 }
}
public sealed class UnknownInput:Component,ISecondaryInput{public int Calls;public bool HasSecondaryConduitType(ConduitType t){Calls++;throw new Exception("unknown input");}public CellOffset GetSecondaryConduitOffset(ConduitType t){Calls++;throw new Exception("unknown input offset");}}
public sealed class UnknownOutput:Component,ISecondaryOutput{public int Calls;public bool HasSecondaryConduitType(ConduitType t){Calls++;throw new Exception("unknown output");}public CellOffset GetSecondaryConduitOffset(ConduitType t){Calls++;throw new Exception("unknown output offset");}}
public sealed class DerivedInput:ConduitSecondaryInput,ISecondaryInput{public int Calls;bool ISecondaryInput.HasSecondaryConduitType(ConduitType t){Calls++;throw new Exception("derived input");}CellOffset ISecondaryInput.GetSecondaryConduitOffset(ConduitType t){Calls++;throw new Exception("derived input offset");}}
public sealed class DerivedOutput:ConduitSecondaryOutput,ISecondaryOutput{public int Calls;bool ISecondaryOutput.HasSecondaryConduitType(ConduitType t){Calls++;throw new Exception("derived output");}CellOffset ISecondaryOutput.GetSecondaryConduitOffset(ConduitType t){Calls++;throw new Exception("derived output offset");}}
