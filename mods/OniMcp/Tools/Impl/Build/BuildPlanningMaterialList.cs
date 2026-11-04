using System.Collections.Generic;
using System.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Dictionary<string, object> BuildMaterialListing(BuildingDef def, int worldId,
            bool includeUnavailable, int limit)
        {
            var all = AvailableMaterials(def, worldId, includeUnavailable: true);
            bool freeBuild = IsFreeBuildContext();
            var auto = all.FirstOrDefault(item => item.AvailableKg > 0f || freeBuild);
            var unknown = all.FirstOrDefault(item => item.Inventory?.State == "unknown");
            var materials = all.Where(item => includeUnavailable || item.AvailableKg > 0f || freeBuild)
                .Take(limit).Select(item => item.ToDictionary()).ToList();
            return new Dictionary<string, object>
            {
                ["prefabId"] = def.PrefabID, ["name"] = ToolUtil.CleanName(def.Name), ["worldId"] = worldId,
                ["materialCategories"] = def.MaterialCategory,
                ["resolvedMaterialCategories"] = MaterialCategoryTags(def).Select(tag => tag.Name).ToList(),
                ["defaultMaterials"] = DefaultBuildElements(def).Select(tag => tag.Name).ToList(),
                ["autoMaterial"] = auto != null ? auto.Tag.Name : "unavailable",
                ["autoMaterialReason"] = auto != null ? null : McpLocalization.Get(unknown != null
                    ? "build_materials.inventory_unknown" : "build_materials.none_available"),
                ["inventoryState"] = unknown == null ? "known" : all.Any(item => item.Inventory?.State == "known") ? "partial" : "unknown",
                ["inventoryReasonCode"] = unknown?.Inventory?.ReasonCode,
                ["inventoryScope"] = McpLocalization.Get("build_materials.inventory_scope"),
                ["inventorySource"] = "WorldInventory.GetAmount(tag,true)",
                ["transportableKg"] = null, ["transportStatus"] = "not_evaluated",
                ["returned"] = materials.Count, ["materials"] = materials
            };
        }
    }
}
