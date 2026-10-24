using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
public sealed class Game { public static Game Instance; public Unlocks unlocks; public static bool IsCorrectDlcActiveForCurrentSave(IHasDlcRestrictions value) => value.ValidDlc; }
public sealed class Unlocks { public HashSet<string> unlocked = new HashSet<string>(); public IEnumerable<string> GetAllUnlockedIds() => unlocked; public bool IsUnlocked(string id) => unlocked.Contains(id); }
public interface IHasDlcRestrictions { bool ValidDlc { get; } }
public static class DlcManager { public static bool IsCorrectDlcActiveForCurrentSave(IHasDlcRestrictions value) => value.ValidDlc; }
public static class Strings { public static string Get(string key) => key == "STRINGS.TITLE" ? "日本語見出し" : key; }
public static class CodexCache
{
 public static Dictionary<string,string> LockEntries = new Dictionary<string,string>();
 public static string GetEntryForLock(string id) => LockEntries.TryGetValue(id,out var value) ? value : null;
 public static Dictionary<string,CodexEntry> entries;
 public static Dictionary<string,SubEntry> subEntries;
 public static string FormatLinkID(string id) => id.ToUpper().Replace("_", "");
 public static void CodexCacheInit() { throw new Exception("MCP read must not initialize Codex"); }
}
public sealed class CodexEntry : IHasDlcRestrictions
{
 public string id, parentId, category, name, title, subtitle;
 public bool disabled;
 public bool ValidDlc { get; set; } = true;
 public int ContentReads;
 public List<ContentContainer> StoredContent;
 public List<ContentContainer> contentContainers { get { ContentReads++; return StoredContent; } }
}
public sealed class SubEntry : IHasDlcRestrictions
{
 public string id, parentEntryID, name, title, subtitle, lockID;
 public bool disabled;
 public bool ValidDlc { get; set; } = true;
 public List<ContentContainer> contentContainers;
}
public sealed class ContentContainer : IHasDlcRestrictions
{
 public string lockID;
 public bool ValidDlc { get; set; } = true;
 public List<ICodexWidget> content;
}
public interface ICodexWidget { }
public sealed class CodexText : ICodexWidget { public string text; }
public sealed class CodexTextWithTooltip : ICodexWidget { public string text,tooltip; }
public sealed class CodexLabelWithIcon : ICodexWidget { public CodexText label; }
public sealed class CodexIndentedLabelWithIcon : ICodexWidget { public CodexText label; }
public sealed class DangerousWidget : ICodexWidget { public string Text { get { throw new Exception("Unknown getters must not be invoked"); } } }
namespace OniMcp.Tools
{
 internal static class ToolUtil
 {
  internal static bool GetBool(JObject a,string k,bool d) => a[k]?.Value<bool>() ?? d;
  internal static int? GetInt(JObject a,string k) => a[k]?.Value<int?>();
 }
}

public struct Tag { public string Name; public bool IsValid => !string.IsNullOrEmpty(Name); public Tag(string name) { Name = name; } }
namespace UnityEngine
{
 public class Object { public string name; }
 public sealed class GameObject : Object
 {
  public KPrefabID Identity;
  public Sublimates Sublimation;
  public bool ThrowSublimationRead;
  public static int ProperNameCalls, SublimationReads;
  public T GetComponent<T>() where T : class
  {
   if (typeof(T)==typeof(Sublimates)) { SublimationReads++; if(ThrowSublimationRead)throw new Exception("component read unavailable"); }
   return (Identity as T) ?? (Sublimation as T);
  }
  public string GetProperName() { ProperNameCalls++; throw new Exception("Do not invoke prefab naming callbacks"); }
 }
}
public sealed class KPrefabID { public Tag PrefabTag; }
public static class Assets
{
 public static Dictionary<string,List<UnityEngine.GameObject>> Members = new Dictionary<string,List<UnityEngine.GameObject>>();
 public static List<string> ObservedTags = new List<string>();
 public static bool Throw;
 public static Dictionary<string,UnityEngine.GameObject> Prefabs = new Dictionary<string,UnityEngine.GameObject>();
 public static List<string> PrefabRequests = new List<string>();
 public static bool ThrowPrefabLookup;
 public static int WarningLookupCalls;
 public static UnityEngine.GameObject TryGetPrefab(Tag tag)
 {
  PrefabRequests.Add(tag.Name);
  if(ThrowPrefabLookup)throw new Exception("prefab cache unavailable");
  return Prefabs.TryGetValue(tag.Name,out var value) ? value : null;
 }
 public static UnityEngine.GameObject GetPrefab(Tag tag) { WarningLookupCalls++; throw new Exception("Read must use the passive lookup"); }
 public static List<UnityEngine.GameObject> GetPrefabsWithTag(Tag tag)
 {
  ObservedTags.Add(tag.Name);
  if (Throw) throw new Exception("native cached lookup unavailable");
  if (!Members.TryGetValue(tag.Name,out var result)) return new List<UnityEngine.GameObject>();
  return result == null ? null : new List<UnityEngine.GameObject>(result);
 }
}

public sealed class CodexSpacer : ICodexWidget { }
public sealed class CodexLargeSpacer : ICodexWidget { }
public sealed class CodexDividerLine : ICodexWidget { }
public class CodexCollapsibleHeader : ICodexWidget
{
 private string label; private ContentContainer contents;
 public CodexCollapsibleHeader(string title, ContentContainer child) { label = title; contents = child; }
 public void Configure() { throw new Exception("Do not configure UI"); }
}
public sealed class CodexElementCategoryList : CodexCollapsibleHeader
{
 public Tag categoryTag {get;set;}
 public CodexElementCategoryList(Tag category) : base("Elements", null) {categoryTag=category;}
}
public sealed class ElementUsage { public Tag tag; public float amount; public bool continuous; public Func<Tag,float,bool,string> customFormating; }
public sealed class CodexConversionPanel : ICodexWidget
{
 public string title; public ElementUsage[] ins, outs; public UnityEngine.Object Converter; public IconSettings aidIcon;
 public sealed class IconSettings { public string spriteName, tooltip; }
 public void Configure() { throw new Exception("Do not configure UI"); }
}
public sealed class ComplexRecipe
{
 public string id, recipeCategoryID, customName, description, requiredTech;
 public RecipeElement[] ingredients, results; public float time; public int consumedHEP, producedHEP;
 public List<Tag> fabricators; public Func<string> runTimeDescription;
 public sealed class RecipeElement
 {
  public Tag material; public Tag[] possibleMaterials; public float[] possibleMaterialAmounts;
  public float amount {get;set;} public string temperatureOperation, facadeID;
  public bool storeElement, inheritElement, doNotConsume;
 }
}
public sealed class CodexRecipePanel : ICodexWidget
{
 public ComplexRecipe complexRecipe; public object recipe; public bool useFabricatorForTitle; public string linkID {get;set;}
}
public enum SimHashes { None, Oxygen, TestSource, DirectTarget, FallbackTarget, PrivateTarget, Missing }
public sealed class Element
{
 public SimHashes id, sublimateId; public Tag tag; public string name {get;set;}
 public float lowTemp, highTemp, highTempTransitionOreMassConversion, lowTempTransitionOreMassConversion;
 public string lowTempTransitionTarget, highTempTransitionTarget, highTempTransitionOreID, lowTempTransitionOreID, convertId;
 public float sublimateRate, sublimateEfficiency, sublimateProbability, offGasPercentage;
}
public sealed class CodexTemperatureTransitionPanel : ICodexWidget
{
 public Element sourceElement; public string transitionType;
 public static int ConfigureCalls;
 public void Configure() { ConfigureCalls++; throw new Exception("Read must not configure native UI"); }
}
public static class ElementLoader
{
 public static Dictionary<SimHashes,Element> Elements = new Dictionary<SimHashes,Element>();
 public static List<SimHashes> Requests = new List<SimHashes>();
 public static HashSet<SimHashes> ThrowHashes = new HashSet<SimHashes>();
 public static int LoadCalls;
 public static Element FindElementByHash(SimHashes hash)
 {
  Requests.Add(hash);
  if(Elements==null || ThrowHashes.Contains(hash))throw new Exception("element table unavailable");
  return Elements.TryGetValue(hash,out var value) ? value : null;
 }
 public static void Load() { LoadCalls++; throw new Exception("Read must not load elements"); }
}
public static class TagManager
{
 public static int CreateCalls;
 public static Tag Create(string name) { CreateCalls++; throw new Exception("Read must not register tags"); }
}
public static class GameTagExtensions { public static Tag CreateTag(this SimHashes id) => TagManager.Create(id.ToString()); }
public sealed class Sublimates
{
 public struct Info
 {
  public float sublimationRate, minSublimationAmount, maxDestinationMass, massPower;
  public SimHashes sublimatedElement;
  public Info(float rate,float minimum,float maximum,float power,SimHashes target)
  { sublimationRate=rate;minSublimationAmount=minimum;maxDestinationMass=maximum;massPower=power;sublimatedElement=target; }
 }
 public Info info;
 public static int SimCalls;
 public void Sim200ms(float dt) { SimCalls++; throw new Exception("Read must not simulate emission"); }
}

public static class GameUtil
{
 public static int PreyCalls, DirectPlantCalls, StoragePlantCalls;
 public static string GetFormattedPreyConsumptionValuePerCycle(Tag tag,float value,bool perCycle=true)
 { PreyCalls++;throw new Exception("Read must not execute native prey formatter"); }
 public static string GetFormattedDirectPlantConsumptionValuePerCycle(Tag tag,float value,bool perCycle=true)
 { DirectPlantCalls++;throw new Exception("Read must not execute native direct plant formatter"); }
 public static string GetFormattedPlantStorageConsumptionValuePerCycle(Tag tag,float value,bool perCycle=true)
 { StoragePlantCalls++;throw new Exception("Read must not execute native storage plant formatter"); }
}
public static class UnrelatedPreyFormatters
{
 public static int Calls;
 public static string GetFormattedPreyConsumptionValuePerCycle(Tag tag,float value,bool perCycle=true)
 { Calls++;throw new Exception("Read must not execute same name foreign formatter"); }
}
public sealed class BoundPreyFormatter
{
 public int Calls;
 public string GetFormattedPreyConsumptionValuePerCycle(Tag tag,float value,bool perCycle=true)
 { Calls++;throw new Exception("Read must not execute instance formatter"); }
}
