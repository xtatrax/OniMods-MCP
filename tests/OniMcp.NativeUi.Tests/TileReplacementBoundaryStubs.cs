using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
#pragma warning disable CS0649

// This fixture models native UI callbacks; it never creates a queue by default.
internal sealed partial class BuildingDef
{
    public bool Replaceable=true;
    public bool IsFoundation=true;
    public ObjectLayer TileLayer=ObjectLayer.FoundationTile;
    public ObjectLayer ReplacementLayer=ObjectLayer.ReplacementTile;
    public List<ObjectLayer> EquivalentReplacementLayers=new List<ObjectLayer>{ObjectLayer.ReplacementLadder};
    public int WidthInCells=1,HeightInCells=1;
    public CellOffset[] PlacementOffsets=new[]{new CellOffset()};
    public float[] Mass=new[]{100f};
    public Recipe CraftRecipe=new Recipe();
    public bool NativeCanReplace=true;
    public GameObject GetReplacementCandidate(int cell){return Grid.Objects[cell,(int)TileLayer];}
    public bool CanReplace(GameObject go){return NativeCanReplace&&go!=null;}
    public bool IsReplacementLayerOccupied(int cell)
    {return Grid.Objects[cell,(int)ReplacementLayer]!=null||EquivalentReplacementLayers.Any(layer=>Grid.Objects[cell,(int)layer]!=null);}
    public int TryPlaceCalls;
    public GameObject TryPlace(GameObject source,Vector3 position,Orientation orientation,IList<Tag> elements,string facade)
    {TryPlaceCalls++;throw new InvalidOperationException("fixture direct TryPlace forbidden for a replacement");}
}
internal sealed partial class Constructable {public bool IsReplacementTile;}
internal sealed class Prioritizable : Component
{
    public PrioritySetting Observed=new PrioritySetting(PriorityScreen.PriorityClass.basic,5);
    public PrioritySetting GetMasterPriority(){return Observed;}
    public void SetMasterPriority(PrioritySetting priority){throw new InvalidOperationException("fixture direct object priority mutation forbidden");}
}
internal struct PrioritySetting
{
    public PriorityScreen.PriorityClass priority_class;
    public int priority_value;
    public PrioritySetting(PriorityScreen.PriorityClass kind,int value){priority_class=kind;priority_value=value;}
    public static bool operator ==(PrioritySetting left,PrioritySetting right){return left.priority_class==right.priority_class&&left.priority_value==right.priority_value;}
    public static bool operator !=(PrioritySetting left,PrioritySetting right){return !(left==right);}
    public override bool Equals(object value){return value is PrioritySetting&&this==(PrioritySetting)value;}
    public override int GetHashCode(){return ((int)priority_class)*31+priority_value;}
}
internal class PriorityScreen : Component
{
    public enum PriorityClass {idle=-1,basic,high,personalNeeds,topPriority,compulsory}
    public bool isSpawned;
    private PrioritySetting lastSelectedPriority=new PrioritySetting(PriorityClass.basic,5);
    protected List<PriorityButton> buttons_basic=Enumerable.Range(1,9).Select(value=>new PriorityButton()).ToList();
    protected KToggle button_toggleHigh=new KToggle();
    protected PriorityButton button_emergency=new PriorityButton();
    public int SetCalls;
    public System.Action<PrioritySetting> OnSet;
    public PrioritySetting GetLastSelectedPriority(){return lastSelectedPriority;}
    public void SetScreenPriority(PrioritySetting priority,bool play_sound=false)
    {SetCalls++;lastSelectedPriority=priority;OnSet?.Invoke(priority);}
    public void FixtureBreakCache(string part)
    {
        if(part=="list")buttons_basic=null;
        else if(part=="count")buttons_basic.RemoveAt(0);
        else if(part=="button")buttons_basic[4]=null;
        else if(part=="toggle")buttons_basic[4].toggle=null;
        else if(part=="tooltip")buttons_basic[4].tooltip=null;
        else if(part=="high")button_toggleHigh=null;
        else if(part=="emergency")button_emergency=null;
        else if(part=="emergency_toggle")button_emergency.toggle=null;
        else if(part=="priority")lastSelectedPriority=new PrioritySetting(PriorityClass.basic,-1);
        else throw new ArgumentOutOfRangeException("part");
    }
}
internal sealed class PriorityButton : Component
{public KToggle toggle=new KToggle();public ToolTip tooltip=new ToolTip();}
internal sealed class ToolTip : Component { }
internal sealed class Recipe {public Tag Result;}
internal sealed class MaterialSelector : Component
{
    public bool isSpawned;
    public int selectorIndex;
    public Tag CurrentSelectedElement;
    public readonly Dictionary<Tag,KToggle> ElementToggles=new Dictionary<Tag,KToggle>();
}
internal sealed class KToggle : Component
{
    public bool isActiveAndEnabled=true,FixtureInteractable=true;
    private event System.Action onClick;
    public void FixtureSetCallback(bool available){onClick=available?(System.Action)(()=>{}):null;}
    public bool IsInteractable(){return FixtureInteractable;}
    public void FixtureClick(){onClick?.Invoke();}
}
internal sealed class MaterialSelectionPanel : Component
{
    public bool isSpawned;
    public PriorityScreen FixturePriority;
    public PriorityScreen PriorityScreen {get{return FixturePriority;}}
    // Actual native field names/types: three selectors always exist, only recipe slots are active.
    private List<MaterialSelector> materialSelectors=new List<MaterialSelector>();
    private Recipe activeRecipe;
    public int MaterialSelectCalls,RememberedSelections;
    public void SetFixtureSelectors(List<MaterialSelector> value,Recipe recipe){materialSelectors=value;activeRecipe=recipe;}
    public IList<Tag> GetSelectedElementAsList {get{return materialSelectors.Where(value=>value.gameObject.activeSelf).Select(value=>value.CurrentSelectedElement).ToList();}}
    public Tag CurrentSelectedElement {get{return materialSelectors.Count==0?new Tag():materialSelectors[0].CurrentSelectedElement;}}
    public System.Action<Tag> OnSelect;
    public void ForceSelectPrimaryTag(Tag tag)
    {
        MaterialSelectCalls++;
        if(!materialSelectors[0].ElementToggles.ContainsKey(tag))throw new KeyNotFoundException("fixture missing native material toggle");
        if(materialSelectors[0].CurrentSelectedElement!=tag)
        {
            if(activeRecipe!=null)RememberedSelections++;
            materialSelectors[0].CurrentSelectedElement=tag;
            OnSelect?.Invoke(tag);
        }
    }
}
internal sealed class ProductInfoScreen : Component
{
    public bool isSpawned;
    public BuildingDef currentDef;
    public MaterialSelectionPanel materialSelectionPanel;
}
internal sealed partial class PlanScreen
{
    public bool isSpawned;
    private ProductInfoScreen fixtureProduct;
    public ProductInfoScreen ProductInfoScreen {get{return fixtureProduct;}}
    public string LastSelectedBuildingFacade;
    public BuildingDef FixtureSelectedDefinition;
    public int CopyCalls;
    public System.Action<BuildingDef,string> OnCopy;
    public void SetFixtureProduct(ProductInfoScreen value){fixtureProduct=value;}
    public void CopyBuildingOrder(BuildingDef definition,string facade)
    {
        CopyCalls++;
        if(OnCopy!=null){OnCopy(definition,facade);return;}
        if(fixtureProduct==null)throw new InvalidOperationException("fixture native panel not initialized");
        fixtureProduct.currentDef=definition;
        FixtureSelectedDefinition=definition;
        BuildTool.Instance.Activate(definition,new List<Tag>(fixtureProduct.materialSelectionPanel.GetSelectedElementAsList),facade);
    }
    public PrioritySetting GetBuildingPriority(){return ProductInfoScreen.materialSelectionPanel.PriorityScreen.GetLastSelectedPriority();}
}
internal sealed class BuildMenu
{
    public static BuildMenu Instance;
    public bool isSpawned;
    private ProductInfoScreen productInfoScreen;
    public void SetFixtureProduct(ProductInfoScreen value){productInfoScreen=value;}
    public ProductInfoScreen FixtureProduct {get{return productInfoScreen;}}
    public PrioritySetting GetBuildingPriority(){return productInfoScreen.materialSelectionPanel.PriorityScreen.GetLastSelectedPriority();}
}
internal sealed partial class Game {public bool SandboxModeActive;}
internal sealed class SandboxToolParameterMenu
{
    internal sealed class Settings {public bool InstantBuild;}
    public static SandboxToolParameterMenu instance;
    public Settings settings;
}
internal sealed class Deconstructable : Component
{
    public bool Marked;
    public bool IsMarkedForDeconstruction(){return Marked;}
}
namespace OniMcp.Tools
{
    internal static partial class ToolUtil
    {
        internal static int ResolveWorldId(JObject args){return args?["worldId"]?.Value<int?>()??ClusterManager.Instance.activeWorldId;}
        internal static float SafeFloat(float value){return value;}
    }
}

internal static partial class Grid
{
    public static bool IsValidBuildingCell(int cell){return IsValidCell(cell);}
    public static int PosToCell(Vector3 position){return XYToCell((int)position.x,(int)position.y);}
}

internal sealed class Reconstructable : Component {public bool ReconstructRequested;}
