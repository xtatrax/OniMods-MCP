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

public struct Tag { public string Name; public Tag(string name) { Name = name; } }
namespace UnityEngine { public class Object { public string name; } }
public sealed class CodexSpacer : ICodexWidget { }
public sealed class CodexLargeSpacer : ICodexWidget { }
public sealed class CodexDividerLine : ICodexWidget { }
public sealed class CodexCollapsibleHeader : ICodexWidget
{
 private string label; private ContentContainer contents;
 public CodexCollapsibleHeader(string title, ContentContainer child) { label = title; contents = child; }
 public void Configure() { throw new Exception("Do not configure UI"); }
}
public sealed class ElementUsage { public Tag tag; public float amount; public bool continuous; public Func<string> customFormating; }
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
public sealed class Element
{
 public string id; public Tag tag; public string name {get;set;}
 public float lowTemp, highTemp, highTempTransitionOreMassConversion, lowTempTransitionOreMassConversion;
 public string lowTempTransitionTarget, highTempTransitionTarget, highTempTransitionOreID, lowTempTransitionOreID, sublimateId, convertId;
 public float sublimateRate, sublimateEfficiency, sublimateProbability, offGasPercentage;
}
public sealed class CodexTemperatureTransitionPanel : ICodexWidget { public Element sourceElement; public string transitionType; }
