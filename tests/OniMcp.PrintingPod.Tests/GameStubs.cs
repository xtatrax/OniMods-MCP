using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;

public interface ITelepadDeliverable{}
public interface ITelepadDeliverableContainer{}
public class FakeDupe:ITelepadDeliverable{}
public class FakeDupeContainer:UnityEngine.Object,ITelepadDeliverableContainer{}
public class CarePackageInfo:ITelepadDeliverable{public string id,facadeID;public float quantity;public Func<bool> requirement;}
public class CarePackageContainer:UnityEngine.Object,ITelepadDeliverableContainer{public static List<ITelepadDeliverableContainer> containers=new List<ITelepadDeliverableContainer>();public CarePackageInfo Info;}
public class Immigration{public static Immigration Instance;public bool ImmigrantsAvailable;public int EndCalls;public void EndImmigration(){EndCalls++;ImmigrantsAvailable=false;}public float GetTimeRemaining()=>ImmigrantsAvailable?0:1800;}
public class Telepad{public GameObject gameObject=new GameObject();public int Delivered;public void OnAcceptDelivery(ITelepadDeliverable d){Delivered++;Immigration.Instance.EndImmigration();}}
public class CharacterSelectionController{
 public List<ITelepadDeliverable> selectedDeliverables=new List<ITelepadDeliverable>();public List<ITelepadDeliverableContainer> containers=new List<ITelepadDeliverableContainer>();
 public void RemoveDeliverable(ITelepadDeliverable d)=>selectedDeliverables.Remove(d);public void AddDeliverable(ITelepadDeliverable d)=>selectedDeliverables.Add(d);public bool IsSelected(ITelepadDeliverable d)=>selectedDeliverables.Contains(d);
}
public class ImmigrantScreen:CharacterSelectionController{public static ImmigrantScreen instance;public Telepad Telepad;public GameObject gameObject=new GameObject();public int ProceedCalls; public int RejectCalls; private void OnRejectionConfirmed(){RejectCalls++;Immigration.Instance.EndImmigration();gameObject.activeSelf=false;containers.Clear();CarePackageContainer.containers.Clear();}protected void OnProceed(){ProceedCalls++;Telepad.OnAcceptDelivery(selectedDeliverables[0]);gameObject.activeSelf=false;containers.Clear();CarePackageContainer.containers.Clear();}}
public class ClusterManager{public static ClusterManager Instance=new ClusterManager();public int activeWorldId;}
public static class Components{public static class Telepads{public static List<Telepad> Items=new List<Telepad>();}}
public static class Grid{public static int PosToCell(Telepad x)=>-1;public static bool IsValidCell(int cell)=>false;public static int CellColumn(int x)=>0;public static int CellRow(int x)=>0;}
public static class Assets{public static GameObject GetPrefab(string id)=>null;}
namespace UnityEngine{public class Object{
 public bool Destroyed;
 public static bool operator ==(Object left,Object right){
  bool leftNull=ReferenceEquals(left,null)||(!ReferenceEquals(left,null)&&left.Destroyed);
  bool rightNull=ReferenceEquals(right,null)||(!ReferenceEquals(right,null)&&right.Destroyed);
  return leftNull||rightNull?leftNull&&rightNull:ReferenceEquals(left,right);
 }
 public static bool operator !=(Object left,Object right)=>!(left==right);
 public override bool Equals(object other)=>ReferenceEquals(this,other);
 public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
}
public class GameObject{public bool activeSelf=true;public T GetComponent<T>() where T:class=>null;public int GetMyWorldId()=>0;public string GetProperName()=>"seed";}}
namespace OniMcp.Support{public static class McpLocalization{public static string Get(string key,string fallback=null)=>fallback??key;}}
namespace OniMcp.Config{public class OniMcpOptions{public static OniMcpOptions Current=new OniMcpOptions();public bool RequirePrintingPodSelection;}}
namespace OniMcp.Tools{
 public static class ToolUtil{public static int? GetInt(JObject a,string k)=>a[k]?.Value<int>();public static bool GetBool(JObject a,string k,bool f=false)=>a[k]?.Value<bool>()??f;public static float SafeFloat(float v)=>v;public static string CleanName(string v)=>v;}
 public static partial class FacilitySideScreenTools{
  public static Telepad Target=new Telepad();public static CallToolResult Reject(JObject a)=>RejectPrintingRewards(a,Target,new Dictionary<string,object>());public static CallToolResult Claim(JObject a)=>ClaimPrintingReward(a,Target,new Dictionary<string,object>());public static Dictionary<string,object> Status()=>PrintingRewardStatus(Target);
  static GameObject FindBuildingTarget(JObject a,Func<GameObject,bool> p)=>null;static bool HasRectInput(JObject a)=>false;
  static Dictionary<string,object> TelepadInfo(Telepad t,bool includeVictory)=>new Dictionary<string,object>();
  static bool MatchesQuery(Dictionary<string,object> a,string query)=>a.Any(p=>p.Value?.ToString()==query);
  static CallToolResult JsonResult(object x)=>CallToolResult.Text(Newtonsoft.Json.JsonConvert.SerializeObject(x));
 }
}
