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
 }
}
