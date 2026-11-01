using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static void SetPriority(GameObject go, int priority)
        {
            var prioritizable = go.GetComponent<Prioritizable>();
            if (prioritizable == null)
                return;

            int clamped = Math.Max(1, Math.Min(priority, 9));
            prioritizable.SetMasterPriority(new PrioritySetting(PriorityScreen.PriorityClass.basic, clamped));
        }

        private static Orientation ParseOrientation(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Orientation.Neutral;

            Orientation orientation;
            return Enum.TryParse(value, true, out orientation) ? orientation : Orientation.Neutral;
        }

        private static bool Matches(BuildingDef def, string query)
        {
            string q = query.Trim();
            return Contains(def.PrefabID, q)
                || Contains(def.Name, q)
                || Contains(def.Desc, q)
                || BuildingCategories(def).Any(category => Contains(category, q))
                || def.SearchTerms.Any(term => Contains(term, q));
        }

        private static bool MatchesCategory(BuildingDef def, string category)
        {
            string q = category.Trim();
            return BuildingCategories(def).Any(value => Contains(value, q));
        }

        private static Dictionary<string, object> BuildingDefToDictionary(BuildingDef def)
        {
            return BuildingDefToDictionary(def, BuildingAvailabilityReader.Read(def));
        }

        private static Dictionary<string, object> BuildingDefToDictionary(BuildingDef def,
            BuildingAvailabilityReader.Observation availability)
        {
            availability = availability ?? BuildingAvailabilityReader.Read(def);
            int worldId = ClusterManager.Instance?.activeWorldId ?? -1;
            var availableMaterials = AvailableMaterials(def, worldId, includeUnavailable: false).ToList();
            var defaultElements = DefaultBuildElements(def);
            var availableTags = new HashSet<Tag>(availableMaterials.Select(m => m.Tag));
            var filteredDefaults = defaultElements
                .Where(tag => availableTags.Contains(tag))
                .ToList();
            if (filteredDefaults.Count == 0 && defaultElements.Count > 0)
                filteredDefaults = defaultElements;

            string recommendedMaterial = availableMaterials.Count > 0
                ? availableMaterials[0].Tag.Name
                : null;

            return new Dictionary<string, object>
            {
                ["prefabId"] = def.PrefabID,
                ["name"] = ToolUtil.CleanName(def.Name),
                ["width"] = def.WidthInCells,
                ["height"] = def.HeightInCells,
                ["buildLocationRule"] = def.BuildLocationRule.ToString(),
                ["placement"] = BuildDefPlacementToDictionary(def),
                ["categories"] = BuildingCategories(def),
                ["categoryScope"] = "native_definition_fields_and_material_categories",
                ["runtimeCategoryProperties"] = "not_evaluated",
                ["materialCategories"] = def.MaterialCategory,
                ["resolvedMaterialCategories"] = MaterialCategoryTags(def).Select(tag => tag.Name).ToList(),
                ["defaultMaterials"] = filteredDefaults.Select(tag => tag.Name).ToList(),
                ["availableMaterials"] = availableMaterials.Take(20).Select(item => item.ToDictionary()).ToList(),
                ["recommendedMaterial"] = recommendedMaterial,
                ["autoMaterial"] = AutoMaterialValue(def, worldId),
                ["autoMaterialReason"] = AutoMaterialReason(def, worldId, availability),
                ["facades"] = BuildingFacades(def),
                ["requiresPower"] = def.RequiresPowerInput,
                ["powerWatts"] = Math.Round(def.EnergyConsumptionWhenActive, 1),
                ["unlocked"] = BuildingAvailabilityReader.JsonBoolean(availability.Unlocked),
                ["availableNow"] = BuildingAvailabilityReader.JsonBoolean(availability.Value),
                ["availability"] = availability.ToDictionary()
            };
        }

private static bool IsUnlockedAndAvailable(BuildingDef def)
{
return BuildingAvailabilityReader.Read(def).Value == true;
}

private static string BuildAvailabilityError(BuildingDef def, JObject args = null)
{
    BuildingAvailabilityReader.Observation availability;
    return BuildAvailabilityError(def, args, out availability);
}

private static string BuildAvailabilityError(BuildingDef def, JObject args,
    out BuildingAvailabilityReader.Observation availability)
{
    // Capture through the same passive reader as discovery. Missing research
    // caches remain unknown; a preview must never initialize Db.Get().
    availability = BuildingAvailabilityReader.Read(def);
    return BuildAvailabilityErrorFromObservation(def, args, availability);
}

private static string BuildAvailabilityErrorFromObservation(BuildingDef def, JObject args,
    BuildingAvailabilityReader.Observation availability)
{
    if (def == null)
        return McpLocalization.Get("building_availability.error.definition_missing");
    bool researchAllowed = availability.Unlocked == true || CanBypassUtilityResearch(def, args);
    if (!researchAllowed && availability.Unlocked == false)
        return McpLocalization.Format("building_availability.error.research_locked", null, def.PrefabID);
    if (availability.DefinitionAvailable == false)
        return McpLocalization.Format("building_availability.error.definition_unavailable", null, def.PrefabID);
    if (!researchAllowed || availability.DefinitionAvailable != true)
        return McpLocalization.Format("building_availability.error.unknown", null,
            def.PrefabID, availability.ReasonCode ?? "unknown");
    return null;
}

private static bool CanBypassUtilityResearch(BuildingDef def, JObject args)
{
return def != null
&& args != null
&& ToolUtil.GetBool(args, "instantBuild", false)
&& ToolUtil.GetBool(args, "allowSandbox", false)
&& BuildingControlTools.IsVirtualFileEditContext
&& IsExactConnectionUtilityPrefab(def.PrefabID);
}

private static object AutoMaterialValue(BuildingDef def, int worldId)
        {
            var material = AvailableMaterials(def, worldId, includeUnavailable: false).FirstOrDefault();
            return material != null ? (object)material.Tag.Name : "unavailable";
        }

        private static object AutoMaterialReason(BuildingDef def, int worldId,
            BuildingAvailabilityReader.Observation availability)
        {
            if (availability.Unlocked == false)
                return "building_locked_by_research";
            if (availability.DefinitionAvailable == false)
                return "building_not_available_in_current_context";
            if (availability.Value != true)
                return "building_availability_unknown";
            var material = AvailableMaterials(def, worldId, includeUnavailable: false).FirstOrDefault();
            return material != null ? null : "no_currently_available_material";
        }

        private static List<string> BuildingCategories(BuildingDef def)
        {
            var categories = new List<string>();
            AddCategory(categories, ReadMemberString(def, "Category"));
            AddCategory(categories, ReadMemberString(def, "BuildMenuCategory"));
            AddCategory(categories, ReadMemberString(def, "MenuCategory"));
            AddCategory(categories, ReadMemberString(def, "PlanScreenCategory"));
            AddCategory(categories, ReadMemberString(def, "Subcategory"));
            AddCategory(categories, ReadMemberString(def, "BuildMenuSubcategory"));
            AddCategory(categories, ReadMemberString(def, "TechCategory"));

            if (def.MaterialCategory != null)
                foreach (var category in def.MaterialCategory)
                    AddCategory(categories, category);

            return categories.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList();
        }

        private static string ReadMemberString(BuildingDef def, string name)
        {
            if (def == null)
                return null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            var field = typeof(BuildingDef).GetField(name, flags);
            if (field == null || field.DeclaringType != typeof(BuildingDef)
                || field.IsStatic || (field.FieldType != typeof(string) && field.FieldType != typeof(Tag)))
                return null;
            try
            {
                object value = field.GetValue(def);
                if (field.FieldType == typeof(string))
                    return value as string;
                return value == null ? null : ((Tag)value).Name;
            }
            catch { return null; }
        }

        private static void AddCategory(List<string> categories, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                categories.Add(value.Trim());
        }

        private static List<Dictionary<string, object>> BuildingFacades(BuildingDef def)
        {
            var facades = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["id"] = "DEFAULT_FACADE",
                    ["name"] = McpLocalization.Get("building_facade.default_name"),
                    ["unlocked"] = true,
                    ["default"] = true
                }
            };
            if (def.AvailableFacades == null)
                return facades;
            foreach (var facadeId in def.AvailableFacades)
            {
                var observed = BuildingFacadeReader.Read(facadeId);
                if (observed.Unlocked == false)
                    continue;
                facades.Add(new Dictionary<string, object>
                {
                    ["id"] = facadeId,
                    ["name"] = observed.Name,
                    ["unlocked"] = BuildingAvailabilityReader.JsonBoolean(observed.Unlocked),
                    ["default"] = false,
                    ["availability"] = observed.ToDictionary()
                });
            }
            return facades;
        }

    }
}
