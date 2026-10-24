using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
using UnityEngine;
class Program {
 static void Require(bool b,string text){if(!b)throw new Exception(text);}
 static void Reset(bool available=true){Immigration.Instance=new Immigration{ImmigrantsAvailable=available};ImmigrantScreen.instance=new ImmigrantScreen{Telepad=FacilitySideScreenTools.Target};CarePackageContainer.containers.Clear();var c=new CarePackageContainer{Info=new CarePackageInfo{id="seed",quantity=1}};CarePackageContainer.containers.Add(c);ImmigrantScreen.instance.containers.Add(c);FacilitySideScreenTools.Target.Delivered=0;}
 static JObject Body(CallToolResult r)=>JObject.Parse(r.Content[0].Text);
 static void Main(){
 CandidateObservationRegression();
 ClaimCandidateSourceRegression();
 Reset(false);var r=FacilitySideScreenTools.Claim(new JObject{["confirm"]=true});Require(r.IsError&&FacilitySideScreenTools.Target.Delivered==0,"Unavailable stale offers must never be delivered");Require((int)JObject.FromObject(FacilitySideScreenTools.Status())["rewardCount"]==0,"Unavailable stale offers must be hidden");
 Reset();var prior=new FakeDupe();ImmigrantScreen.instance.AddDeliverable(prior);r=FacilitySideScreenTools.Claim(new JObject{["itemId"]="seed",["confirm"]=true});var body=Body(r);Require(!r.IsError&&FacilitySideScreenTools.Target.Delivered==1,"Care package must be delivered once");Require(Immigration.Instance.EndCalls==1,"End immigration must occur once through native delivery");Require((bool)body["screenCleanupConfirmed"]&&(int)body["remainingScreenContainers"]==0,"Native confirmation must clear controller candidates");Require(ImmigrantScreen.instance.ProceedCalls==1&&ImmigrantScreen.instance.selectedDeliverables.All(x=>x is CarePackageInfo),"Existing duplicant selections must be replaced by the requested care package");Require((bool)body["screenClosed"],"Native screen must close");r=FacilitySideScreenTools.Claim(new JObject{["confirm"]=true});Require(r.IsError&&FacilitySideScreenTools.Target.Delivered==1,"Repeated claim must be rejected");
 Reset();r=FacilitySideScreenTools.Claim(new JObject{["dryRun"]=true});Require(!r.IsError&&FacilitySideScreenTools.Target.Delivered==0&&ImmigrantScreen.instance.ProceedCalls==0,"Preview must not deliver or confirm");
 Reset();r=FacilitySideScreenTools.Claim(new JObject{["rewardIndex"]=999,["confirm"]=true});Require(r.IsError&&FacilitySideScreenTools.Target.Delivered==0,"Invalid candidate must not deliver");
 Reset();r=FacilitySideScreenTools.Claim(new JObject());Require(r.IsError&&FacilitySideScreenTools.Target.Delivered==0,"Confirmation must be required");
 Reset();ImmigrantScreen.instance.Telepad=new Telepad();r=FacilitySideScreenTools.Claim(new JObject{["confirm"]=true});Require(r.IsError&&FacilitySideScreenTools.Target.Delivered==0,"Wrong pod screen must be rejected");
 Reset();ImmigrantScreen.instance=null;r=FacilitySideScreenTools.Claim(new JObject{["confirm"]=true});Require(r.IsError&&FacilitySideScreenTools.Target.Delivered==0,"Missing native screen must fail safely");
 Reset();OniMcp.Config.OniMcpOptions.Current.RequirePrintingPodSelection=false;Require(!FacilitySideScreenTools.Status().ContainsKey("selectionPolicy"),"OFF reward queries must not include policy guidance");
 OniMcp.Config.OniMcpOptions.Current.RequirePrintingPodSelection=true;Require(FacilitySideScreenTools.Status().ContainsKey("selectionPolicy"),"ON reward queries must include policy");
 Reset();ImmigrantScreen.instance.AddDeliverable(new FakeDupe());r=FacilitySideScreenTools.Reject(new JObject{["confirm"]=true});body=Body(r);Require(!r.IsError&&(bool)body["rejectedAll"]&&(bool)body["screenCleanupConfirmed"]&&(bool)body["screenClosed"],"Native rejection must resolve, clean up, and close");Require(FacilitySideScreenTools.Target.Delivered==0&&ImmigrantScreen.instance.ProceedCalls==0&&ImmigrantScreen.instance.RejectCalls==1&&Immigration.Instance.EndCalls==1,"Reject all must not deliver any selected dupe or item, and must end once");
 r=FacilitySideScreenTools.Reject(new JObject{["confirm"]=true});Require(r.IsError&&Immigration.Instance.EndCalls==1,"Repeated rejection must not reset cooldown");
 Reset();r=FacilitySideScreenTools.Reject(new JObject{["dryRun"]=true});Require(!r.IsError&&Immigration.Instance.ImmigrantsAvailable&&ImmigrantScreen.instance.RejectCalls==0&&ImmigrantScreen.instance.containers.Count==1,"Rejection preview must not mutate candidates or availability");
 Reset();r=FacilitySideScreenTools.Reject(new JObject());Require(r.IsError&&ImmigrantScreen.instance.RejectCalls==0,"Rejection requires confirmation");
 Reset();ImmigrantScreen.instance.Telepad=new Telepad();r=FacilitySideScreenTools.Reject(new JObject{["confirm"]=true});Require(r.IsError&&Immigration.Instance.ImmigrantsAvailable,"Wrong screen must not reject");
 Reset();ImmigrantScreen.instance.gameObject.activeSelf=false;r=FacilitySideScreenTools.Reject(new JObject{["confirm"]=true});Require(r.IsError&&Immigration.Instance.ImmigrantsAvailable,"Closed screen must not reject");
 Reset();ImmigrantScreen.instance=null;r=FacilitySideScreenTools.Reject(new JObject{["confirm"]=true});Require(r.IsError&&Immigration.Instance.ImmigrantsAvailable,"Missing screen must not reject");
 Console.WriteLine("PASS: native reject-all, no delivery, duplicate rejection, preview, confirmation, screen guards, OFF policy omission");
 Console.WriteLine("PASS: Printing Pod native confirmation, cleanup, dupe exclusion, single delivery, stale-offer guard, preview, confirmation, invalid candidate and screen validation");
 AchievementReadRegression.Run();
 }
 static JObject Observe(string state, bool? available, int? total, int? care, string support) {
  var screen=ImmigrantScreen.instance;
  var list=screen?.containers;
  var entries=list?.ToArray();
  var selected=screen?.selectedDeliverables.ToArray();
  bool? active=screen?.gameObject.activeSelf;
  int delivered=FacilitySideScreenTools.Target.Delivered;
  int? ends=Immigration.Instance?.EndCalls;
  int? proceeds=screen?.ProceedCalls;
  int? rejects=screen?.RejectCalls;
  var result=FacilitySideScreenTools.Status();
  Require(result.ContainsKey("candidateObservation"),"Status must distinguish an unobserved candidate cache from known zero");
  var body=JObject.FromObject(result);
  var observation=body["candidateObservation"];
  Require((string)observation["status"]==state,"Unexpected native candidate observation state");
  Require((bool?)body["available"]==available,"Unknown readiness must not collapse to false");
  Require((int?)observation["totalCount"]==total&&(int?)observation["carePackageCount"]==care
   &&(int?)body["rewardCount"]==care,"Missing counts must remain distinct from known zero");
  Require((string)body["claimSupport"]==support,"Claim support must reflect whether the count was observed");
  Require(!string.IsNullOrWhiteSpace((string)observation["reason"])
   &&!string.IsNullOrWhiteSpace((string)observation["source"]),"Observation must identify its native evidence boundary");
  Require(ReferenceEquals(screen,ImmigrantScreen.instance)&&FacilitySideScreenTools.Target.Delivered==delivered
   &&Immigration.Instance?.EndCalls==ends,"Status must not replace the screen, deliver or end immigration");
  if(screen!=null){
   Require(ReferenceEquals(list,screen.containers)&&screen.gameObject.activeSelf==active
    &&screen.ProceedCalls==proceeds&&screen.RejectCalls==rejects,"Status must not open, close, rebuild, confirm or reject the screen");
   Require((list==null&&entries==null)||(list!=null&&list.SequenceEqual(entries)),"Status mutated native candidate entries");
   Require(screen.selectedDeliverables.SequenceEqual(selected),"Status changed native selections");
  }
  return body;
 }
 static void CandidateObservationRegression() {
  bool previousPolicy=OniMcp.Config.OniMcpOptions.Current.RequirePrintingPodSelection;
  try {
   OniMcp.Config.OniMcpOptions.Current.RequirePrintingPodSelection=false;
   Reset();Immigration.Instance=null;
   Observe("unknown",null,null,null,"unknown");
   Reset();ImmigrantScreen.instance=null;
   Observe("not_materialized",true,null,null,"unknown");
   Reset();ImmigrantScreen.instance.Telepad=new Telepad();
   Observe("unknown",true,null,null,"unknown");
   Reset();ImmigrantScreen.instance.containers=null;
   Observe("not_materialized",true,null,null,"unknown");
   Reset();ImmigrantScreen.instance.containers.Clear();
   Observe("not_materialized",true,null,null,"unknown");
   // A nonempty dupe-only cache proves known zero care packages. The unrelated
   // static care-package registry still contains a seed and must not be read.
   Reset();ImmigrantScreen.instance.containers.Clear();
   ImmigrantScreen.instance.containers.Add(new FakeDupeContainer());
   var zero=Observe("materialized",true,1,0,"none_currently_safe");
   Require(!zero["rewards"].Any(),"A target screen with no care packages must not inherit global cached items");
   Reset();ImmigrantScreen.instance.containers.Add(null);
   Observe("unknown",true,null,null,"unknown");
   Reset();ImmigrantScreen.instance.containers.Add(new CarePackageContainer{Destroyed=true,Info=new CarePackageInfo{id="destroyed"}});
   Observe("unknown",true,null,null,"unknown");
   Reset();ImmigrantScreen.instance.containers.Clear();
   ImmigrantScreen.instance.containers.Add(new CarePackageContainer());
   var pending=Observe("generation_pending",true,null,null,"unknown");
   Require(!pending["rewards"].Any(),"Unfinished generation must not claim a complete candidate count");
   Reset();ImmigrantScreen.instance.containers.Add(new CarePackageContainer());
   Require(!Observe("generation_pending",true,null,null,"unknown")["rewards"].Any(),
    "A partially generated cache must not publish a partial list as complete");
   Reset();int requirements=0;
   ((CarePackageContainer)ImmigrantScreen.instance.containers[0]).Info.requirement=()=>{requirements++;return false;};
   ImmigrantScreen.instance.containers.Add(new CarePackageContainer{Info=new CarePackageInfo{
    id="second",quantity=2,requirement=()=>{requirements++;throw new Exception("read evaluated eligibility");}}});
   ImmigrantScreen.instance.gameObject.activeSelf=false;
   ImmigrantScreen.instance.AddDeliverable(new FakeDupe());
   var known=Observe("materialized",true,2,2,"care_package_only");
   Require(requirements==0&&known["rewards"].Count()==2,"Passive status must list existing offers without evaluating requirements");
   foreach(var reward in known["rewards"])
    Require(reward["requirementMet"].Type==JTokenType.Null&&reward["claimable"].Type==JTokenType.Null
     &&(string)reward["claimabilityStatus"]=="not_evaluated","Read status must not manufacture native eligibility");
   Require((string)known["rewards"][0]["id"]=="seed"&&(string)known["rewards"][1]["id"]=="second",
    "The observed native screen order must remain intact");
   Immigration.Instance.ImmigrantsAvailable=false;
   ImmigrantScreen.instance.containers.Add(null);
   var noReady=Observe("no_ready_offers",false,0,0,"none_currently_safe");
   Require(requirements==0&&!noReady["rewards"].Any(),"No-ready status must ignore retained candidate caches and callbacks");
   Console.WriteLine("PASS: production candidate observation distinguishes unknown, unmaterialized, pending, known zero and known offers without callbacks or UI changes");
  } finally {OniMcp.Config.OniMcpOptions.Current.RequirePrintingPodSelection=previousPolicy;}
 }

 static void ClaimCandidateSourceRegression() {
  Reset();
  int staleRequirements=0;
  int currentRequirements=0;
  var stale=new CarePackageInfo{id="global-stale",quantity=99,requirement=()=>{staleRequirements++;return true;}};
  CarePackageContainer.containers.Clear();
  CarePackageContainer.containers.Add(new CarePackageContainer{Info=stale});
  var first=new CarePackageInfo{id="controller-first",quantity=3,requirement=()=>{currentRequirements++;return true;}};
  var second=new CarePackageInfo{id="controller-second",quantity=4};
  ImmigrantScreen.instance.containers.Clear();
  ImmigrantScreen.instance.containers.Add(new CarePackageContainer{Info=first});
  ImmigrantScreen.instance.containers.Add(new FakeDupeContainer());
  ImmigrantScreen.instance.containers.Add(new CarePackageContainer{Info=second});
  var previousSelection=new FakeDupe();
  ImmigrantScreen.instance.AddDeliverable(previousSelection);
  var status=Observe("materialized",true,3,2,"care_package_only");
  Require((string)status["rewards"][0]["id"]==first.id&&staleRequirements==0&&currentRequirements==0,
   "Status must report the current target controller order without evaluating eligibility");
  var preview=FacilitySideScreenTools.Claim(new JObject{["rewardIndex"]=0,["dryRun"]=true});
  Require(!preview.IsError&&(string)Body(preview)["selectedReward"]["id"]==first.id,
   "Claim preview index must match the current target status index, not an unrelated static cache");
  Require(FacilitySideScreenTools.Target.Delivered==0&&Immigration.Instance.EndCalls==0
   &&ImmigrantScreen.instance.ProceedCalls==0&&ImmigrantScreen.instance.selectedDeliverables.Single()==previousSelection,
   "Claim preview must retain the screen selection and avoid native confirmation");
  Require(staleRequirements==0&&currentRequirements>0,
   "Execution preview may evaluate only its current selected reward; passive status must not evaluate stale entries");
  var result=FacilitySideScreenTools.Claim(new JObject{["rewardIndex"]=0,["confirm"]=true});
  Require(!result.IsError&&(string)Body(result)["selectedReward"]["id"]==first.id
   &&FacilitySideScreenTools.Target.Delivered==1&&Immigration.Instance.EndCalls==1
   &&ImmigrantScreen.instance.ProceedCalls==1,"Claim must confirm the status-selected current reward exactly once");
  Require(ImmigrantScreen.instance.selectedDeliverables.Count==1
   &&ReferenceEquals(ImmigrantScreen.instance.selectedDeliverables[0],first)&&staleRequirements==0,
   "The native confirmation must receive the same current Info object instead of a stale global choice");
  Console.WriteLine("PASS: status, claim preview and native confirmation share the target controller candidate order while ignoring stale global cache");
 }

}
