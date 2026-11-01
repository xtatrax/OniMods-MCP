using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static MaterialSelection SelectElements(BuildingDef def, string material, int worldId)
        {
            string requested = material?.Trim();
            bool auto = string.IsNullOrWhiteSpace(requested)
                || requested.Equals("auto", StringComparison.OrdinalIgnoreCase)
                || requested.Equals("default", StringComparison.OrdinalIgnoreCase);

            var available = AvailableMaterials(def, worldId, includeUnavailable: false).ToList();
            if (auto)
            {
                var defaults = DefaultBuildElements(def);
                if (IsFreeBuildContext())
                    return ValidatedMaterialSelection(defaults, "default_no_inventory_in_debug", requested, null, available);

                var selected = available.FirstOrDefault();
                if (selected != null)
                    return ValidatedMaterialSelection(new List<Tag> { selected.Tag }, "auto", requested, selected, available);

                return MaterialSelection.Invalid(
                    "No available build material in current world inventory",
                    requested,
                    available,
                    AvailableMaterials(def, worldId, includeUnavailable: true).Take(20).ToList());
            }

            var requestedCategory = MaterialCategoryTags(def).FirstOrDefault(category => EqualsIgnoreCase(category.Name, requested));
            if (requestedCategory.IsValid)
            {
                var categoryMatch = available
                    .Where(item => MaterialMatchesCategory(item.Tag, requestedCategory))
                    .OrderByDescending(item => item.AvailableKg)
                    .FirstOrDefault();
                if (categoryMatch != null)
                    return ValidatedMaterialSelection(new List<Tag> { categoryMatch.Tag }, "category:" + requestedCategory.Name, requested, categoryMatch, available);
            }

            var allCandidates = AvailableMaterials(def, worldId, includeUnavailable: true);
            var match = allCandidates
                .FirstOrDefault(item => EqualsIgnoreCase(item.Tag.Name, requested)
                    || EqualsIgnoreCase(item.Name, requested)
                    || Contains(item.Tag.Name, requested)
                    || Contains(item.Name, requested));
            var candidates = allCandidates.Take(20).ToList();
            if (match == null || !match.ValidForBuilding)
            {
                return MaterialSelection.Invalid(
                    $"Material '{requested}' is not valid for {def.PrefabID}",
                    requested,
                    available,
                    candidates);
            }

            if (match.AvailableKg <= 0f && !IsFreeBuildContext())
            {
                var unavailable = MaterialSelection.Invalid(
                    $"Material '{match.Tag.Name}' is valid for {def.PrefabID}, but none is currently available",
                    requested,
                    available,
                    candidates);
                unavailable.Selected = match;
                return unavailable;
            }

            return ValidatedMaterialSelection(new List<Tag> { match.Tag }, "explicit", requested, match, available);
        }

        private static MaterialSelection ValidatedMaterialSelection(
            List<Tag> elements,
            string mode,
            string requested,
            BuildMaterialInfo selected,
            List<BuildMaterialInfo> available)
        {
            if (elements == null || elements.Count == 0)
                return MaterialSelection.Invalid("No material selected.", requested, available, new List<BuildMaterialInfo>());

            Tag primaryTag = elements[0];
            if (!primaryTag.IsValid || ElementLoader.GetElement(elements[0]) == null)
                return MaterialSelection.Invalid(
                    $"Material '{primaryTag.Name}' is not a valid build material.",
                    requested,
                    available,
                    (available ?? new List<BuildMaterialInfo>()).Take(20).ToList());

            return MaterialSelection.Success(elements, mode, requested, selected, available);
        }

        private static List<BuildMaterialInfo> AvailableMaterials(BuildingDef def, int worldId, bool includeUnavailable)
        {
            var categories = MaterialCategoryTags(def).ToList();
            var candidates = CandidateMaterialTags(def, worldId)
                .Where(tag => tag.IsValid)
                .Distinct()
                .Select(tag =>
                {
                    var matches = categories.Where(category => MaterialMatchesCategory(tag, category)).ToList();
                    return new BuildMaterialInfo
                    {
                        Tag = tag,
                        Name = tag.ProperNameStripLink(),
                        AvailableKg = AvailableAmount(worldId, tag),
                        ValidForBuilding = categories.Count == 0 || matches.Count > 0,
                        Categories = matches
                    };
                })
                .Where(item => item.ValidForBuilding)
                .Where(item => includeUnavailable || item.AvailableKg > 0f || IsFreeBuildContext())
                .OrderByDescending(item => item.AvailableKg)
                .ThenBy(item => item.Tag.Name)
                .ToList();

            return candidates;
        }

        private static float RequiredMaterialKg(BuildingDef def)
        {
            // Mass contains native per-material-slot construction quantities.
            // Never compare one selected material with a sum of unrelated slots.
            return MaterialQuantityDiagnostics.SingleSlotRequirement(def?.Mass);
        }

        private static IEnumerable<Tag> CandidateMaterialTags(BuildingDef def, int worldId)
        {
            foreach (var tag in DefaultBuildElements(def))
                yield return tag;

            foreach (var category in MaterialCategoryTags(def))
            {
                if (DiscoveredResources.Instance != null)
                {
                    IEnumerable<Tag> discovered = null;
                    try
                    {
                        discovered = DiscoveredResources.Instance.GetDiscoveredResourcesFromTag(category);
                    }
                    catch
                    {
                        discovered = null;
                    }

                    if (discovered != null)
                    {
                        foreach (var tag in discovered)
                            yield return tag;
                    }
                }
            }

            foreach (var tag in InventoryMaterialTags(def, worldId))
                yield return tag;
        }

        private static IEnumerable<Tag> InventoryMaterialTags(BuildingDef def, int worldId)
        {
            var categories = MaterialCategoryTags(def).ToList();
            if (categories.Count == 0)
                yield break;

            foreach (var pickupable in Components.Pickupables.Items)
            {
                if (pickupable == null || pickupable.gameObject == null)
                    continue;

                int itemWorldId = PickupableWorldId(pickupable);
                if (worldId >= 0 && itemWorldId != worldId)
                    continue;

                var kpid = pickupable.KPrefabID ?? pickupable.GetComponent<KPrefabID>();
                var primary = pickupable.PrimaryElement ?? pickupable.GetComponent<PrimaryElement>();
                Tag prefabTag = kpid?.PrefabTag ?? Tag.Invalid;
                Tag elementTag = primary != null ? new Tag(primary.ElementID.ToString()) : Tag.Invalid;

                if (MaterialMatchesAnyCategory(prefabTag, categories))
                    yield return prefabTag;
                if (MaterialMatchesAnyCategory(elementTag, categories))
                    yield return elementTag;
                if (kpid != null && categories.Any(category => kpid.HasTag(category)))
                {
                    if (elementTag.IsValid)
                        yield return elementTag;
                    if (prefabTag.IsValid)
                        yield return prefabTag;
                }
            }
        }

        private static IEnumerable<Tag> MatchingMaterialCategories(BuildingDef def, Tag material)
        {
            var categories = MaterialCategoryTags(def).ToList();
            foreach (var category in categories)
            {
                if (MaterialMatchesCategory(material, category))
                    yield return category;
            }
        }

        private static IEnumerable<Tag> MaterialCategoryTags(BuildingDef def)
        {
            if (def.MaterialCategory == null)
                yield break;
            foreach (string categoryName in def.MaterialCategory)
            {
                foreach (var category in ParseMaterialCategoryExpression(categoryName))
                    yield return category;
            }
        }

        private static IEnumerable<Tag> ParseMaterialCategoryExpression(string categoryExpression)
        {
            if (string.IsNullOrWhiteSpace(categoryExpression))
                yield break;

            char[] separators = { '&', '|', ',', ';' };
            foreach (var part in categoryExpression.Split(separators, StringSplitOptions.RemoveEmptyEntries))
            {
                var category = new Tag(part.Trim());
                if (category.IsValid)
                    yield return category;
            }
        }

        private static List<Tag> DefaultBuildElements(BuildingDef def)
        {
            var defaults = def.DefaultElements() ?? new List<Tag>();
            var categories = MaterialCategoryTags(def).ToList();
            if (categories.Count == 0)
                return defaults.Where(tag => tag.IsValid).Distinct().ToList();

            return defaults
                .Where(tag => MaterialMatchesAnyCategory(tag, categories))
                .Distinct()
                .ToList();
        }

        private static bool MaterialMatchesAnyCategory(Tag material, List<Tag> categories)
        {
            return material.IsValid && categories.Any(category => MaterialMatchesCategory(material, category));
        }

        private static bool MaterialMatchesCategory(Tag material, Tag category)
        {
            if (!material.IsValid || !category.IsValid)
                return false;
            if (material == category)
                return true;

            var element = ElementLoader.GetElement(material);
            if (element != null && (element.GetMaterialCategoryTag() == category || element.HasTag(category)))
                return true;

            var prefab = Assets.GetPrefab(material);
            var kpid = prefab != null ? prefab.GetComponent<KPrefabID>() : null;
            return kpid != null && kpid.HasTag(category);
        }

        private static int PickupableWorldId(Pickupable pickupable)
        {
            int cell = pickupable.cachedCell;
            if (Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell))
                return Grid.WorldIdx[cell];
            return pickupable.GetMyWorldId();
        }

        private static float AvailableAmount(int worldId, Tag tag)
        {
            if (!tag.IsValid || ClusterManager.Instance == null)
                return 0f;
            var world = ClusterManager.Instance.GetWorld(worldId >= 0 ? worldId : ClusterManager.Instance.activeWorldId);
            if (world == null || world.worldInventory == null)
                return 0f;

            float amount = ToolUtil.SafeFloat(world.worldInventory.GetTotalAmount(tag, includeRelatedWorlds: true));
            foreach (var pickupable in Components.Pickupables.Items)
            {
                if (pickupable == null || pickupable.KPrefabID == null)
                    continue;
                if (pickupable.storage != null || pickupable.KPrefabID.HasTag(GameTags.Stored))
                    continue;
                if (!pickupable.KPrefabID.HasTag(tag))
                    continue;
                if (PickupableWorldId(pickupable) != world.id)
                    continue;

                if (pickupable.PrimaryElement != null)
                    amount += ToolUtil.SafeFloat(pickupable.PrimaryElement.Mass);
            }
            return amount;
        }

        private static bool IsFreeBuildContext()
        {
            if (WorldEditorTools.EnforceNormalMaterialRules)
                return false;
            return DebugHandler.InstantBuildMode || (Game.Instance != null && Game.Instance.SandboxModeActive);
        }

        private static FacadeSelection ResolveFacade(BuildingDef def, string facade)
        {
            if (string.IsNullOrWhiteSpace(facade))
                return FacadeSelection.Default();

            var facadeId = facade.Trim();
            if (facadeId.Equals("default", StringComparison.OrdinalIgnoreCase) || facadeId == "DEFAULT_FACADE")
                return FacadeSelection.Default();

            if (def.AvailableFacades == null || !def.AvailableFacades.Contains(facadeId))
                return FacadeSelection.Invalid(McpLocalization.Format("building_facade.not_available", null, facadeId, def.PrefabID));
            var observed = BuildingFacadeReader.Read(facadeId);
            if (!observed.Unlocked.HasValue)
                return FacadeSelection.Unknown(McpLocalization.Format("building_facade.unknown", null, facadeId, observed.ReasonCode ?? "unknown"), observed.ReasonCode);
            if (observed.Unlocked == false)
                return FacadeSelection.Invalid(McpLocalization.Format("building_facade.locked", null, facadeId));

            return FacadeSelection.Custom(facadeId);
        }
        private struct FacadeSelection
        {
            public readonly bool Valid;
            public readonly string TryPlaceId;
            public readonly string ResponseId;
            public readonly string Error;
            public readonly string AssessmentState;
            public readonly string ReasonCode;

            private FacadeSelection(bool valid, string tryPlaceId, string responseId, string error,
                string assessmentState = "known", string reasonCode = null)
            {
                Valid = valid;
                TryPlaceId = tryPlaceId;
                ResponseId = responseId;
                Error = error;
                AssessmentState = assessmentState;
                ReasonCode = reasonCode;
            }

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["valid"] = Valid,
                    ["status"] = AssessmentState == "unknown" ? "unknown" : Valid ? "selected" : "rejected",
                    ["assessmentState"] = AssessmentState,
                    ["reasonCode"] = ReasonCode == null ? (object)JValue.CreateNull() : ReasonCode
                };
            }

            public static FacadeSelection Unknown(string error, string reasonCode)
            {
                return new FacadeSelection(false, null, null, error, "unknown", reasonCode);
            }

            public static FacadeSelection Default()
            {
                return new FacadeSelection(true, null, "DEFAULT_FACADE", null);
            }

            public static FacadeSelection Custom(string facadeId)
            {
                return new FacadeSelection(true, facadeId, facadeId, null);
            }

            public static FacadeSelection Invalid(string error)
            {
                return new FacadeSelection(false, null, null, error);
            }
        }

        private sealed class BuildMaterialInfo
        {
            public Tag Tag;
            public string Name;
            public float AvailableKg;
            public bool ValidForBuilding;
            public List<Tag> Categories = new List<Tag>();

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["tag"] = Tag.Name,
                    ["name"] = Name,
                    ["availableKg"] = Math.Round(ToolUtil.SafeFloat(AvailableKg), 3),
                    ["validForBuilding"] = ValidForBuilding,
                    ["categories"] = Categories.Select(tag => tag.Name).OrderBy(name => name).ToList()
                };
            }
        }

        private sealed class MaterialSelection
        {
            public bool Valid;
            public string Mode;
            public string Requested;
            public List<Tag> Elements = new List<Tag>();
            public BuildMaterialInfo Selected;
            public List<BuildMaterialInfo> Available = new List<BuildMaterialInfo>();
            public List<BuildMaterialInfo> Candidates = new List<BuildMaterialInfo>();
            public float RequiredKg;
            public string Error;

            public static MaterialSelection Success(List<Tag> elements, string mode, string requested, BuildMaterialInfo selected, List<BuildMaterialInfo> available)
            {
                return new MaterialSelection
                {
                    Valid = true,
                    Mode = mode,
                    Requested = string.IsNullOrWhiteSpace(requested) ? "auto" : requested,
                    Elements = elements ?? new List<Tag>(),
                    Selected = selected,
                    Available = available ?? new List<BuildMaterialInfo>(),
                    RequiredKg = 0f
                };
            }

            public static MaterialSelection Invalid(string error, string requested, List<BuildMaterialInfo> available, List<BuildMaterialInfo> candidates)
            {
                return new MaterialSelection
                {
                    Valid = false,
                    Mode = "invalid",
                    Requested = string.IsNullOrWhiteSpace(requested) ? "auto" : requested,
                    Error = error,
                    Available = available ?? new List<BuildMaterialInfo>(),
                    Candidates = candidates ?? new List<BuildMaterialInfo>(),
                    RequiredKg = 0f
                };
            }

            public MaterialSelection CloneForPreview()
            {
                return new MaterialSelection
                {
                    Valid = Valid, Mode = Mode, Requested = Requested, Error = Error, RequiredKg = RequiredKg,
                    Elements = new List<Tag>(Elements),
                    Selected = CloneMaterialInfo(Selected),
                    Available = Available.Select(CloneMaterialInfo).ToList(),
                    Candidates = Candidates.Select(CloneMaterialInfo).ToList()
                };
            }

            private static BuildMaterialInfo CloneMaterialInfo(BuildMaterialInfo value)
            {
                return value == null ? null : new BuildMaterialInfo
                {
                    Tag = value.Tag, Name = value.Name, AvailableKg = value.AvailableKg,
                    ValidForBuilding = value.ValidForBuilding, Categories = new List<Tag>(value.Categories)
                };
            }

            public Dictionary<string, object> ToDictionary()
            {
                float selectedAvailableKg = Selected != null ? Selected.AvailableKg : float.NaN;
                var quantity = MaterialQuantityDiagnostics.Evaluate(RequiredKg, selectedAvailableKg, IsFreeBuildContext());
                bool hasRequiredKg = quantity.RequiredKg.HasValue;
                object satisfied = quantity.Satisfied;

                return new Dictionary<string, object>
                {
                    ["valid"] = Valid,
                    ["mode"] = Mode,
                    ["requested"] = Requested,
                    ["requirementKnown"] = hasRequiredKg,
                    ["requirementSource"] = hasRequiredKg ? "BuildingDef.Mass[0]" : null,
                    ["quantityScope"] = "single_material_slot; multi-slot requirements are not evaluated",
                    ["requiredKg"] = hasRequiredKg ? (object)Math.Round(quantity.RequiredKg.Value, 3) : null,
                    ["selectedAvailableKg"] = quantity.AvailableKg.HasValue ? (object)Math.Round(quantity.AvailableKg.Value, 3) : null,
                    ["satisfied"] = satisfied,
                    ["shortageKg"] = quantity.ShortageKg.HasValue ? (object)Math.Round(quantity.ShortageKg.Value, 3) : null,
                    ["quantityStatus"] = quantity.Status,
                    ["inventoryScope"] = "world inventory including related worlds and loose items; reservations and delivery reachability not evaluated",
                    ["selected"] = Selected != null ? Selected.ToDictionary() : null,
                    ["elements"] = Elements.Select(tag => tag.Name).ToList(),
                    ["availableMaterials"] = Available.Take(20).Select(item => item.ToDictionary()).ToList(),
                    ["candidateMaterials"] = Candidates.Take(20).Select(item => item.ToDictionary()).ToList(),
                    ["fallbackMaterial"] = Available.Count > 0 ? Available[0].Tag.Name : null,
                    ["next"] = Valid
                        ? quantity.Status == "insufficient"
                            ? "Material is compatible, but stock is insufficient. A blueprint can be queued; acquire more material before completion."
                            : quantity.Status == "unknown"
                                ? "Material is compatible; required quantity or stock is unknown. Do not assume sufficient material."
                                : quantity.Status == "not_required"
                                    ? "Material stock is not required in free-build mode."
                                    : "Material is compatible and inventory quantity is sufficient; delivery reachability is not evaluated."
                        : Available.Count > 0
                            ? "Retry with material=auto or material=" + Available[0].Tag.Name + " if exact material is not required."
                            : "No usable material is currently available; inspect inventory/material candidates before retrying.",
                    ["suggestion"] = Available.Count > 0 ? "Use material=auto or material=" + Available[0].Tag.Name : "No available material; inspect read_control domain=resources action=inventory/building_control domain=planning action=materials",
                    ["error"] = Error
                };
            }
        }
    }
}
