using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Tools;
internal static class Program {
 static int checks;
 static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
 static JObject Data(CallToolResult r)=>JObject.Parse(r.Content.First(c=>c.Meta==null||!c.Meta.ContainsKey(ToolContent.ContentKindMetadataKey)||c.Meta[ToolContent.ContentKindMetadataKey] as string==ToolContent.ToolResultKind).Text);
 static PreviewValidationObservation Observation()=>new PreviewValidationObservation("preview","placement_preflight","partial",false,false,"valid","support.nativeFoundationValid","missing","large");
 static void HelperTests(){
  var opaque=new OpaqueMetadata();
  var result=CallToolResult.Error("{\"valid\":false,\"support\":{\"nativeFoundationValid\":null},\"large\":[1,2,3],\"validationScope\":\"existing_object_reuse\"}");
  var original=result.Content[0];original.Meta["opaque"]=opaque;
  var notification=ToolContent.CreateText("notification",ToolContent.NotificationKind);
  var policy=ToolContent.CreateText("policy",ToolContent.PrintingPodPolicyKind);policy.IsOwnedPrintingPodPolicy=true;
  var alias=ToolContent.CreateText("alias",ToolContent.LegacyAliasWarningKind);
  result.Content=new List<ToolContent>{notification,policy,original,alias};
  string before=original.Text;var copy=Observation().Attach(result);
  Check(!object.ReferenceEquals(copy,result)&&copy.IsError&&copy.Content.Count==4,"clone result preserves error/content count");
  for(int i=0;i<4;i++)Check(!object.ReferenceEquals(copy.Content[i],result.Content[i])&&!object.ReferenceEquals(copy.Content[i].Meta,result.Content[i].Meta),"clone each content and metadata dictionary");
  Check(original.Text==before&&!original.Meta.ContainsKey(PreviewValidationObservation.MetadataKey),"scope annotation must not mutate pooled source content/meta");
  Check(copy.Content[0].Text==notification.Text&&copy.Content[1].Text==policy.Text&&copy.Content[3].Text==alias.Text&&copy.Content[1].IsOwnedPrintingPodPolicy,"policy/notification/alias order, text and ownership remain");
  Check(copy.Content[0].Meta[ToolContent.ContentKindMetadataKey] as string==ToolContent.NotificationKind&&!copy.Content[0].Meta.ContainsKey(PreviewValidationObservation.MetadataKey),"notification classification stays unannotated");
  Check(object.ReferenceEquals(copy.Content[2].Meta["opaque"],opaque)&&opaque.Reads==0,"opaque metadata is preserved without evaluating getters");
  var payload=JObject.Parse(copy.Content[2].Text);var validation=payload["validation"];
  Check((string)payload["validationScope"]=="existing_object_reuse"&&payload["support"]["nativeFoundationValid"].Type==JTokenType.Null,"existing native scope and unknown support facts remain");
  Check(!(bool)validation["reportedFacts"]["valid"]&&validation["reportedFacts"]["support.nativeFoundationValid"].Type==JTokenType.Null,"only existing false/null facts are copied honestly");
  Check(validation["reportedFacts"]["missing"]==null&&validation["reportedFacts"]["large"]==null,"missing/large facts are not manufactured or duplicated");
  Check((string)validation["coverage"]=="partial"&&!(bool)validation["gameplayMutationRequested"]&&!(bool)validation["uiInteractionRequested"],"explicit scope intent retained");
  var fallback=PreviewValidationObservation.Rejected(copy,"dispatcher_requested",true,true);
  Check(JToken.DeepEquals(Data(fallback)["validation"],validation),"dispatcher fallback never overwrites the leaf scope");
  var textError=CallToolResult.Error("exact plain error \n with details");var error=PreviewValidationObservation.Rejected(textError,"candidate_scan");
  Check(error.Content[0].Text==textError.Content[0].Text&&error.IsError,"plain errors stay byte-for-byte text");
  var errorMeta=(JObject)error.Content[0].Meta[PreviewValidationObservation.MetadataKey];
  Check((string)errorMeta["validationScope"]=="request_rejected"&&(string)errorMeta["coverage"]=="not_evaluated"&&!errorMeta["reportedFacts"].HasValues,"early refusals do not claim performed checks");
  Check(!textError.Content[0].Meta.ContainsKey(PreviewValidationObservation.MetadataKey),"plain error source metadata untouched");
  var dictionary=new Dictionary<string,object>{{"valid",true},{"support",new Dictionary<string,object>{{"nativeFoundationValid",null}}}};
  var projected=Observation().AttachPayload(dictionary);Check(!dictionary.ContainsKey("validation")&&!object.ReferenceEquals(dictionary,projected),"dictionary decoration copies parent payload");
  var dictValidation=(JObject)projected["validation"];Check((bool)dictValidation["reportedFacts"]["valid"]&&dictValidation["reportedFacts"]["support.nativeFoundationValid"].Type==JTokenType.Null,"dictionary facts preserve true/null");
  Check(Observation().Attach(null)==null&&Observation().Attach(new CallToolResult{Content=null}).Content==null,"null boundaries remain null");
  var early=BuildPlanningTools.HostValidationFallback(args=>CallToolResult.Error("exact factory refusal"),new JObject());
  Check((string)((JObject)early.Content[0].Meta[PreviewValidationObservation.MetadataKey])["validationScope"]=="request_rejected","factory fallback marks a real early transport refusal without inventing validation");
  var noProof=BuildPlanningTools.HostValidationFallback(args=>CallToolResult.Text("{\"ok\":true}"),new JObject());
  Check((string)Data(noProof)["validation"]["validationScope"]=="requested_scope_only"&&(string)Data(noProof)["validation"]["coverage"]=="not_evaluated","factory fallback never relabels unobserved success as a rejection or completed validation");
  var leafFallback=BuildPlanningTools.HostValidationFallback(args=>copy,new JObject());
  Check(JToken.DeepEquals(Data(leafFallback)["validation"],validation),"actual factory fallback preserves the authoritative leaf scope");
  var image=new CallToolResult{Content=new List<ToolContent>{new ToolContent{Type="image",Text="image"},policy}};var cloned=Observation().Attach(image);
  Check(cloned.Content[0].Meta==null&&!cloned.Content[1].Meta.ContainsKey(PreviewValidationObservation.MetadataKey),"non-result/image/policy content is not converted into validation output");
 }
 static JObject Args(bool execute=true,bool dryRun=true)=>new JObject{{"kind","toilet"},{"x",10},{"y",10},{"execute",execute},{"dryRun",dryRun},{"confirm",execute&&!dryRun}};
 static CallToolResult Child(JObject payload)=>new PreviewValidationObservation("build_area","batch_placement_preflight","complete",false,false,"ok","partial").Attach(CallToolResult.Text(payload.ToString()));
 static void Enqueue(params CallToolResult[] results){OniToolRegistry.Reset();foreach(var result in results){var captured=result;OniToolRegistry.Results.Enqueue(()=>captured);}}
 static void TemplateTests(){
  // Semantic failure is transport-successful. Old full source keeps parent ok=true.
  var refused=CallToolResult.Text("{\"ok\":false,\"failed\":1,\"partial\":false,\"error\":\"native refusal\"}");Enqueue(refused);
  var result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());var data=Data(result);
  Check(result.IsError&&!(bool)data["ok"],"template parent must fail when a transport-successful child reports failure");
  Check(OniToolRegistry.Arguments.Count==1&&(int)data["attemptedCalls"]==1&&(int)data["failedCalls"]==1&&(int)data["remainingCalls"]==4,"stop at first semantic failure and count child calls, not cells");
  Check(!(bool)data["partial"]&&!(bool)data["dispatchComplete"],"first clean refusal is incomplete without claiming prior partial changes");
  Check((string)data["results"][0]["error"]==refused.Content[0].Text,"original child error text is preserved in the existing error contract");

  Enqueue();result=BuildPlanningTools.RoomTemplatePlan().Handler(Args(false,false));data=Data(result);
  Check(!result.IsError&&OniToolRegistry.Arguments.Count==0&&data["calls"].Count()==5,"generation performs zero child calls and retains generated plan");
  Check((string)data["validation"]["validationScope"]=="template_generation"&&(string)data["validation"]["coverage"]=="complete"&&!(bool)data["validation"]["gameplayMutationRequested"],"generation completeness is not placement validation");

  var accepted=Child(new JObject{{"ok",true},{"failed",0},{"partial",false}});Enqueue(accepted,accepted,accepted,accepted,accepted);
  result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
  Check(!result.IsError&&(bool)data["ok"]&&(bool)data["dispatchComplete"]&&(int)data["succeededCalls"]==5&&(int)data["remainingCalls"]==0,"all accepted child previews complete the composite dispatch");
  Check(OniToolRegistry.Arguments.All(a=>(bool)a["dryRun"]&&a["confirm"]==null),"dry-run composite only forwards child previews");
  Check((string)data["validation"]["validationScope"]=="composite_preflight"&&(string)data["validation"]["coverage"]=="complete","composite preserves explicit completed child scopes");
  Check(data["results"].All(r=>r["validation"]!=null)&&!accepted.Content[0].Text.Contains("composite_preflight"),"child observations retained without overwriting pooled child scope");

  Enqueue(CallToolResult.Text("{\"ok\":true}"),CallToolResult.Text("{\"ok\":true}"),CallToolResult.Error("original child error"));
  result=BuildPlanningTools.RoomTemplatePlan().Handler(Args(true,false));data=Data(result);
  Check(result.IsError&&!(bool)data["ok"]&&(bool)data["partial"]&&(int)data["attemptedCalls"]==3&&(int)data["succeededCalls"]==2&&(int)data["remainingCalls"]==2,"earlier accepted dispatches plus later failure remain partial");
  Check((bool)data["validation"]["gameplayMutationRequested"]&&(string)data["validation"]["coverage"]=="partial","execution intent is distinct from native completion proof");

  foreach(var payload in new[]{new JObject{{"partial",true}},new JObject{{"deferred",2}},new JObject{{"remainingCells",1}},new JObject{{"throttled",true}},new JObject{{"allRequestedSatisfied",false}},new JObject{{"pendingBuildAfterDig",1}},new JObject{{"remainingAnchors",new JArray(new JObject{{"x",1},{"y",2}})}},new JObject{{"success",false}},new JObject{{"valid",false}},new JObject{{"actionable",false}},new JObject{{"hardFailed",1}}}){
   Enqueue(CallToolResult.Text(payload.ToString()));result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
   Check(result.IsError&&!(bool)data["ok"]&&OniToolRegistry.Arguments.Count==1,"semantic failure/partial stops composite: "+payload);
  }
  foreach(string unknown in new[]{"{}","","plain callback text","{\"unrelated\":1}"}){
   Enqueue(CallToolResult.Text(unknown));result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
   Check(result.IsError&&!(bool)data["ok"]&&!(bool)data["dispatchComplete"]&&(int)data["unknownCalls"]==1&&(int)data["failedCalls"]==0&&(int)data["remainingCalls"]==4,"unobserved child acknowledgement is unknown, not success or proven native failure");
   Check((string)data["results"][0]["resultState"]=="unknown"&&(string)data["assessmentState"]=="unknown"&&OniToolRegistry.Arguments.Count==1,"unknown child stops composite without invoking later actions");
  }
  foreach(bool dry in new[]{false,true}){
   Enqueue(CallToolResult.Text("{}"));result=BuildPlanningTools.RoomTemplatePlan().Handler(Args(true,dry));data=Data(result);
   Check(result.IsError&&(int)data["attemptedCalls"]==1&&(int)data["unknownCalls"]==1&&(int)data["succeededCalls"]==0&&(bool)data["partial"]&&(bool)data["dispatchInterrupted"],"first unknown child is interrupted and potentially incomplete even without earlier accepted children");
   Check(data["changed"].Type==JTokenType.Null&&(bool)data["possiblePartial"]==!dry&&OniToolRegistry.Arguments.Count==1,"unknown mutating outcome remains unknown and is never retried; pure preflight has no requested gameplay mutation");
  }
  var zeroDig=CallToolResult.Text("{\"dryRun\":true,\"marked\":0,\"worldId\":0,\"rect\":{},\"execution\":{}}");
  Enqueue(zeroDig,accepted,accepted,accepted,accepted);result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
  Check(!result.IsError&&(bool)data["dispatchComplete"]&&(int)data["succeededCalls"]==5&&(int)data["unknownCalls"]==0,"native-shaped zero-dig acknowledgement is legitimate, not shortage or unknown");
  Enqueue((CallToolResult)null);result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
  Check(result.IsError&&!(bool)data["ok"]&&(int)data["attemptedCalls"]==1,"null child result is unavailable, not success");
  OniToolRegistry.Reset();OniToolRegistry.Results.Enqueue(()=>throw new InvalidOperationException("exact callback error"));
  result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
  Check(result.IsError&&((string)data["results"][0]["error"]).Contains("exact callback error"),"thrown child error retains cause and aggregate refusal");

  var notified=CallToolResult.Text("{\"ok\":false}");notified.Content.Insert(0,ToolContent.CreateText("notification text",ToolContent.NotificationKind));
  var owned=ToolContent.CreateText("owned policy",ToolContent.PrintingPodPolicyKind);owned.IsOwnedPrintingPodPolicy=true;notified.Content.Insert(1,owned);
  Enqueue(notified);result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
  Check(result.IsError&&!(bool)data["results"][0]["ok"]&&((string)data["results"][0]["error"])=="{\"ok\":false}","notification/policy text must not replace actual child result selection");
  Check(notified.Content[1].IsOwnedPrintingPodPolicy&&notified.Content[0].Text=="notification text","composite does not mutate child ownership/notification");
  var unknownChild=CallToolResult.Text("{}");var unrelatedNotification=ToolContent.CreateText("notice",ToolContent.NotificationKind);
  unrelatedNotification.Meta[PreviewValidationObservation.MetadataKey]=new JObject{{"coverage","complete"}};unknownChild.Content.Insert(0,unrelatedNotification);
  Enqueue(unknownChild,unknownChild,unknownChild,unknownChild,unknownChild);result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());data=Data(result);
  Check(result.IsError&&(int)data["unknownCalls"]==1&&(string)data["validation"]["coverage"]=="partial"&&data["results"].All(row=>row["validation"]==null),"notification metadata cannot stand in for actual child validation or acceptance evidence");
  var opaqueChild=new OpaqueMetadata();var opaqueResult=CallToolResult.Text("{\"ok\":false}");opaqueResult.Content[0].Meta["opaque"]=opaqueChild;opaqueResult.Content[0].Meta[PreviewValidationObservation.MetadataKey]=opaqueChild;
  Enqueue(opaqueResult);result=BuildPlanningTools.RoomTemplatePlan().Handler(Args());
  Check(result.IsError&&opaqueChild.Reads==0,"template scope observation must not evaluate arbitrary metadata getters");


  Enqueue(accepted,accepted,accepted,accepted,accepted);result=BuildPlanningTools.RoomTemplatePlan().Handler(Args(true,false));data=Data(result);
  Check(!result.IsError&&OniToolRegistry.Arguments.All(a=>!(bool)a["dryRun"]&&(bool)a["confirm"]),"confirmed composite forwards original execution intent");
  Check((string)data["validation"]["validationScope"]=="composite_execution_observation"&&(bool)data["validation"]["gameplayMutationRequested"],"executed composite does not masquerade as passive preview");

  Enqueue(accepted,accepted,accepted,accepted,accepted);var aliasArgs=Args();aliasArgs["dryRun"]=false;aliasArgs["validateOnly"]=true;aliasArgs["confirm"]=false;
  result=BuildPlanningTools.RoomTemplatePlan().Handler(aliasArgs);data=Data(result);
  Check(!result.IsError&&OniToolRegistry.Arguments.All(a=>(bool)a["dryRun"]&&a["confirm"]==null),"template effective validateOnly alias must forward previews only");
  Check((string)data["validation"]["validationScope"]=="composite_preflight"&&!(bool)data["validation"]["gameplayMutationRequested"],"template alias scope reports actual passive composite intent");

  Enqueue();result=BuildPlanningTools.RoomTemplatePlan().Handler(new JObject{{"kind","invalid"}});
  Check(result.IsError&&(string)((JObject)result.Content[0].Meta[PreviewValidationObservation.MetadataKey])["coverage"]=="not_evaluated"&&OniToolRegistry.Arguments.Count==0,"invalid template has only requested scope, no child validation");
 }
 static void UnknownCanary(){Enqueue(CallToolResult.Text("{}"));var result=BuildPlanningTools.RoomTemplatePlan().Handler(Args(true,false));var data=Data(result);Check(result.IsError&&!(bool)data["ok"],"unknown child acknowledgement must not become composite success");}
 static void Main(string[] args){if(args.Length>0&&args[0]=="--unknown-canary-only"){UnknownCanary();Console.WriteLine("PASS unknown template child canary");return;}HelperTests();TemplateTests();Console.WriteLine("PASS "+checks+" preview observation/template actual-source checks");}
}
internal sealed class OpaqueMetadata{public int Reads;public int Value{get{Reads++;throw new Exception("metadata getter must not execute");}}}
