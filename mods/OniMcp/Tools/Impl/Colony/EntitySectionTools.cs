using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static class EntitySectionTools
    {
        internal static string Plain(string s) => s == null ? null : EntitySectionText.Plain(s);
        internal static CallToolResult Read(JObject args)
        {
            string section = (args["section"]?.ToString() ?? "status").Trim().ToLowerInvariant();
            if (!new[] { "status", "properties", "personality", "work", "freshness" }.Contains(section))
                return CallToolResult.Error(McpLocalization.Get("food_freshness.error.section"));
            bool includeModifiers = false;
            if (section == "freshness" && args.TryGetValue("includeFreshnessModifiers", out JToken modifiers))
            {
                if (modifiers.Type != JTokenType.Boolean)
                    return CallToolResult.Error(McpLocalization.Get("food_freshness.error.modifiers_boolean"));
                includeModifiers = modifiers.Value<bool>();
            }
            int? id = ToolUtil.GetInt(args, "id");
            string query = args["query"]?.ToString();
            if (!id.HasValue && string.IsNullOrWhiteSpace(query)) return CallToolResult.Error("id or query is required; use an instance ID from world search");
            int? world = ToolUtil.GetInt(args, "worldId");
            int limit = Math.Max(1, Math.Min(20, ToolUtil.GetInt(args, "limit") ?? 1));
            var objects = EntitySectionCandidates.LiveObjects().Select(go => go.GetComponent<KPrefabID>())
                .Where(k => k != null && k.InstanceID > 0)
                .Where(k => !id.HasValue || k.InstanceID == id.Value)
                .Where(k => !world.HasValue || ToolUtil.GameObjectMatchesWorld(k.gameObject, world.Value))
                .Where(k => string.IsNullOrWhiteSpace(query) || k.PrefabTag.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    || Plain(k.gameObject.GetProperName()).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(k => k.InstanceID).ToList();
            if (id.HasValue && objects.Count == 0) return CallToolResult.Error("Live instance not found: " + id);
            var items = objects.Take(limit).Select(k => Snapshot(k.gameObject, k, section, includeModifiers)).ToList();
            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["scope"] = "live_instance", ["section"] = section, ["matched"] = objects.Count,
                ["returned"] = items.Count, ["truncated"] = objects.Count > items.Count,
                ["items"] = items, ["observedAtUtc"] = System.DateTime.UtcNow.ToString("o")
            }, McpJsonUtil.Settings));
        }

        private static Dictionary<string, object> Snapshot(GameObject go, KPrefabID k, string section, bool includeModifiers)
        {
            int cell = Grid.PosToCell(go);
            var errors = new List<string>();
            var result = new Dictionary<string, object>
            {
                ["id"] = k.InstanceID, ["prefabId"] = k.PrefabTag.Name, ["name"] = Plain(go.GetProperName()),
                ["x"] = Grid.IsValidCell(cell) ? (object)Grid.CellColumn(cell) : null,
                ["y"] = Grid.IsValidCell(cell) ? (object)Grid.CellRow(cell) : null,
                ["worldId"] = go.GetMyWorldId()
            };
            if (section == "freshness")
            {
                result["freshness"] = EntitySectionFoodFreshness.Read(go, k, includeModifiers);
                result["unavailableFields"] = errors;
                return result;
            }
            var dupe = go.GetComponent<MinionIdentity>();
            if (section == "status")
            {
                var statuses = new List<object>();
                try
                {
                    var group = go.GetComponent<KSelectable>()?.GetStatusItemGroup();
                    result["statusesAvailable"] = group != null;
                    if (group == null) errors.Add("status_group_missing");
                    if (group != null)
                        foreach (var entry in group)
                            if (entry.item != null) statuses.Add(new Dictionary<string, object>
                            {
                                ["id"] = entry.item.Id, ["name"] = Plain(entry.item.GetName(entry.data)),
                                ["tooltip"] = Plain(entry.item.GetTooltip(entry.data))
                            });
                }
                catch { errors.Add("status_items_read_failed"); }
                result["statuses"] = statuses;
                var operational = go.GetComponent<Operational>();
                if (operational != null) result["operational"] = new { functional = operational.IsFunctional, operational = operational.IsOperational, active = operational.IsActive };
                var battery = go.GetComponent<Battery>();
                if (battery != null) result["battery"] = new { joules = battery.JoulesAvailable, capacityJoules = battery.Capacity };
                var def = go.GetComponent<Building>()?.Def;
                if (def != null) { result["description"] = Plain(def.Desc); result["effect"] = Plain(def.Effect); }
                try
                {
                    var descriptors = GameUtil.GetAllDescriptors(go, false);
                    result["requirements"] = Descriptors(GameUtil.GetRequirementDescriptors(descriptors), errors);
                    result["effects"] = Descriptors(GameUtil.GetEffectDescriptors(descriptors), errors);
                }
                catch { errors.Add("descriptors_read_failed"); }
            }
            else if (section == "properties")
            {
                var primary = go.GetComponent<PrimaryElement>();
                if (primary != null)
                {
                    result["massKg"] = primary.Mass; result["temperatureK"] = primary.Temperature;
                    result["temperatureC"] = Math.Round(primary.Temperature - 273.15, 2);
                    result["elementId"] = primary.ElementID.ToString(); result["diseaseIndex"] = primary.DiseaseIdx; result["diseaseCount"] = primary.DiseaseCount;
                    result["material"] = Fields(primary.Element, new[] { "specificHeatCapacity", "thermalConductivity", "highTemp", "radiationAbsorptionFactor" }, errors, "Element");
                }
                else errors.Add("PrimaryElement_missing");
                var building = go.GetComponent<BuildingComplete>();
                if (building != null) result["ageCycles"] = Math.Round((GameClock.Instance.GetTime() - building.creationTime) / 600d, 3);
                var operational = go.GetComponent<Operational>();
                if (operational != null) result["uptime"] = new { currentCycle = operational.GetCurrentCycleUptime(), lastCycle = operational.GetLastCycleUptime(), lastFiveCycles = operational.GetUptimeOverCycles(5) };
                var traits = go.GetComponent<Klei.AI.Traits>();
                if (traits != null) result["traits"] = EntitySectionTraits.Read(traits.TraitList, errors);
            }
            else if (section == "personality")
            {
                result["supported"] = dupe != null;
                if (dupe != null) result["personality"] = new { resourceId = dupe.personalityResourceId.ToString(), gender = dupe.gender, model = dupe.model.Name, arrivalTimeSeconds = dupe.arrivalTime };
                else result["reason"] = "not_a_duplicant";
            }
            else
            {
                if (dupe != null) result["currentChore"] = DuplicantTools.ReadSectionCurrentChore(dupe);
                var works = go.GetComponents<Workable>();
                result["workables"] = works.Select(w => WorkInfo(w, errors)).ToList();
                result["hasWorkable"] = works.Length > 0;
            }
            result["unavailableFields"] = errors.Distinct().ToList();
            return result;
        }

        private static Dictionary<string, object> WorkInfo(Workable workable, List<string> errors)
        {
            var result = new Dictionary<string, object>
            {
                ["component"] = workable.GetType().Name,
                ["inProgress"] = workable.worker != null,
                ["workerId"] = workable.worker?.GetComponent<KPrefabID>()?.InstanceID
            };
            var state = new Dictionary<string, object>();
            AddWorkNumber(result, state, "workSeconds", workable.GetWorkTime());
            AddWorkNumber(result, state, "remainingSeconds", workable.WorkTimeRemaining);
            AddWorkNumber(result, state, "percentComplete", workable.GetPercentComplete());
            result["numericState"] = state;
            if (workable is Studyable studyable)
            {
                result["studied"] = JValue.CreateNull();
                result["studyStatus"] = "unavailable";
                result["studySource"] = "Studyable.Studied";
                try
                {
                    result["studied"] = studyable.Studied;
                    result["studyStatus"] = "observed";
                }
                catch { errors.Add("Studyable.Studied"); }
            }
            return result;
        }
        private static void AddWorkNumber(Dictionary<string, object> result, Dictionary<string, object> state, string name, double value)
        {
            object observed = EntitySectionText.NonnegativeOrNull(value);
            result[name] = observed;
            state[name] = observed != null ? "observed" : double.IsPositiveInfinity(value) ? "indefinite" : "undefined";
        }

        private static List<Dictionary<string, object>> Descriptors(System.Collections.Generic.List<Descriptor> descriptors, List<string> errors)
        {
            return descriptors.Select(d => Fields(d, new[] { "text", "tooltipText", "type", "indent" }, errors, "Descriptor")).ToList();
        }
        private static Dictionary<string, object> Fields(object target, string[] names, List<string> errors, string prefix)
        {
            var r = new Dictionary<string, object>();
            foreach (string name in names)
            {
                try
                {
                    if (target == null) throw new InvalidOperationException();
                    var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public);
                    if (f == null) throw new MissingFieldException();
                    var v = f.GetValue(target); r[name] = v is string ? Plain((string)v) : v != null && v.GetType().IsEnum ? v.ToString() : v;
                }
                catch { r[name] = null; errors.Add(prefix + "." + name); }
            }
            return r;
        }
    }
}
