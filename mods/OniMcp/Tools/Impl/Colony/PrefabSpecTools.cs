using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    // Definition metadata only: never serialize live instances or arbitrary Unity graphs.
    internal static class PrefabSpecTools
    {
        private static readonly string[] BuildingBrief = { "WidthInCells", "HeightInCells", "RequiresPowerInput", "EnergyConsumptionWhenActive", "Overheatable", "OverheatTemperature", "Mass", "MaterialCategory" };
        private static readonly string[] BuildingFull = { "ConstructionTime", "HitPoints", "BaseMeltingPoint", "ThermalConductivity", "SelfHeatKilowattsWhenActive", "ExhaustKilowattsWhenActive", "BaseDecor", "BaseDecorRadius", "BaseNoisePollution", "BaseNoisePollutionRadius", "BuildLocationRule", "ObjectLayer", "PermittedRotations", "Floodable", "Entombable", "BaseTimeUntilRepair" };
        private static readonly Dictionary<string, string[]> Components = new Dictionary<string, string[]>
        {
            ["Edible"] = new[] { "FoodID" },
            ["PrimaryElement"] = new[] { "Mass", "Temperature", "ElementID" },
            ["Storage"] = new[] { "capacityKg" },
            ["Battery"] = new[] { "capacity", "joulesLostPerSecond" },
            ["Generator"] = new[] { "wattageRating" },
            ["InfoDescription"] = new[] { "description", "effect" },
            ["ElementConsumer"] = new[] { "elementToConsume", "consumptionRate", "capacityKG", "consumptionRadius" },
            ["ElementEmitter"] = new[] { "emitRate", "maxPressure" },
            ["ConduitConsumer"] = new[] { "conduitType", "consumptionRate", "capacityKG", "alwaysConsume" },
            ["ConduitDispenser"] = new[] { "conduitType", "invertElementFilter" },
            ["ElementConverter"] = new[] { "consumedElements", "outputElements" },
            ["BuildingElementEmitter"] = new[] { "emitRate", "maxPressure" }
        };

        internal static CallToolResult Read(JObject args, string kind)
        {
            if (Game.Instance == null) return CallToolResult.Error("Game not initialized");
            string detail = (args["specDetail"]?.ToString() ?? "brief").Trim().ToLowerInvariant();
            if (detail != "brief" && detail != "full") return CallToolResult.Error("specDetail must be brief or full");
            bool full = detail == "full";
            string exact = args["prefabId"]?.ToString();
            string query = args["query"]?.ToString();
            if (string.IsNullOrWhiteSpace(exact) && string.IsNullOrWhiteSpace(query))
                return CallToolResult.Error("specs requires prefabId or query; use catalogs to discover IDs");
            int limit = Math.Max(1, Math.Min(50, ToolUtil.GetInt(args, "limit") ?? (full ? 1 : 10)));
            var prefabs = Assets.Prefabs.Where(p => p != null)
                .Select(p => new { Go = p.gameObject, Id = p.PrefabTag.Name })
                .Where(p => string.IsNullOrWhiteSpace(exact) ? Match(p.Id, query) || Match(ToolUtil.CleanName(p.Go.GetProperName()), query) : p.Id == exact)
                .Where(p => kind == "buildings" ? Assets.GetBuildingDef(p.Id) != null : Assets.GetBuildingDef(p.Id) == null)
                .GroupBy(p => p.Id).Select(g => g.First()).OrderBy(p => p.Id).ToList();
            if (prefabs.Count == 0 && !string.IsNullOrWhiteSpace(exact)) return CallToolResult.Error("Definition not found in " + kind + ": " + exact);
            var items = prefabs.Take(limit).Select(p => Snapshot(p.Go, p.Id, full)).ToList();
            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["scope"] = "prefab_definition", ["specDetail"] = detail, ["matched"] = prefabs.Count,
                ["coverage"] = "supported definition fields only; no live inventory/state; omitted components are not zero",
                ["units"] = "native: temperature K, power W, heat kW, mass kg, time s; food calories explicitly kcal per unit",
                ["returned"] = items.Count, ["truncated"] = prefabs.Count > items.Count, ["items"] = items
            }, McpJsonUtil.Settings));
        }

        private static bool Match(string value, string query) => !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static Dictionary<string, object> Snapshot(GameObject go, string id, bool full)
        {
            var def = Assets.GetBuildingDef(id);
            var missing = new List<string>();
            var result = new Dictionary<string, object>
            {
                ["prefabId"] = id, ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["kind"] = def != null ? "building" : "item_or_entity",
                ["fullPath"] = def != null ? "/active/buildings/specs.oni" : "/active/resources/specs.oni"
            };
            if (def != null)
            {
                result["specs"] = Members(def, BuildingBrief.Concat(full ? BuildingFull : new string[0]), missing, "BuildingDef");
                if (full)
                {
                    result["description"] = EntitySectionTools.Plain(def.Desc);
                    result["geometry"] = BuildingGeometryTools.Snapshot(null, def, Orientation.Neutral).ToDictionary(false);
                }
            }
            var primary = go.GetComponent<PrimaryElement>();
            if (primary != null && def == null)
            {
                try
                {
                    if (primary.Element != null)
                        result["material"] = Members(primary.Element, new[] { "id", "state", "specificHeatCapacity", "thermalConductivity" }.Concat(full ? new[] { "molarMass", "hardness", "highTemp", "lowTemp", "highTempTransitionTarget", "lowTempTransitionTarget" } : new string[0]), missing, "Element");
                    else missing.Add("PrimaryElement.Element");
                }
                catch { missing.Add("PrimaryElement.Element"); }
            }
            var componentSpecs = new Dictionary<string, object>();
            foreach (var component in go.GetComponents<Component>())
            {
                if (component == null) continue;
                string type = component.GetType().Name;
                string[] fields;
                var supportedType = component.GetType();
                while (!Components.ContainsKey(supportedType.Name) && supportedType.BaseType != null) supportedType = supportedType.BaseType;
                if (!Components.TryGetValue(supportedType.Name, out fields)) continue;
                if (!full && supportedType.Name != "Edible" && supportedType.Name != "Storage" && supportedType.Name != "Battery" && supportedType.Name != "Generator") continue;
                var data = Members(component, fields, missing, type);
                var edible = component as Edible;
                if (edible != null)
                {
                    try
                    {
                        data["quality"] = edible.GetQuality(); data["morale"] = edible.GetMorale();
                        var food = EdiblesManager.GetAllFoodTypes().FirstOrDefault(f => f != null && f.Id == edible.FoodID);
                        if (food != null) { data["caloriesKcalPerUnit"] = Math.Round(food.CaloriesPerUnit / 1000d, 1); data["canRot"] = food.CanRot; }
                        else missing.Add("Edible.food_definition");
                    }
                    catch { missing.Add("Edible.quality_or_morale"); }
                }
                if (componentSpecs.ContainsKey(type))
                {
                    var list = componentSpecs[type] as List<object>;
                    if (list == null) componentSpecs[type] = list = new List<object> { componentSpecs[type] };
                    list.Add(data);
                }
                else componentSpecs[type] = data;
            }
            result["components"] = componentSpecs;
            if (full) result["componentTypes"] = go.GetComponents<Component>().Where(c => c != null).Select(c => c.GetType().FullName).Distinct().OrderBy(x => x).ToList();
            result["unavailableFields"] = missing;
            return result;
        }

        private static Dictionary<string, object> Members(object target, IEnumerable<string> names, List<string> missing, string prefix)
        {
            var values = new Dictionary<string, object>();
            foreach (string name in names)
            {
                try
                {
                    var type = target.GetType();
                    var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public);
                    var property = type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
                    if (field == null && (property == null || property.GetIndexParameters().Length != 0)) { missing.Add(prefix + "." + name); continue; }
                    var raw = field != null ? field.GetValue(target) : property.GetValue(target, null);
                    values[name] = Scalar(raw, 0);
                    if (raw != null && values[name] == null) missing.Add(prefix + "." + name + ":unsupported_value");
                }
                catch { values[name] = null; missing.Add(prefix + "." + name); }
            }
            return values;
        }

        private static object Scalar(object value, int depth)
        {
            if (value == null) return null;
            if (value is double && (double.IsNaN((double)value) || double.IsInfinity((double)value))) return null;
            if (value is float && (float.IsNaN((float)value) || float.IsInfinity((float)value))) return null;
            if (value is string || value is bool || value is int || value is long || value is double || value is float || value is byte || value is uint) return value;
            if (value is Vector3) { var v = (Vector3)value; return new[] { v.x, v.y, v.z }; }
            if (value is Vector2) { var v = (Vector2)value; return new[] { v.x, v.y }; }
            if (value is Tag) return ((Tag)value).Name;
            if (value.GetType().IsEnum) return value.ToString();
            if (value is Array && depth < 2) return ((Array)value).Cast<object>().Select(v => Scalar(v, depth + 1)).ToList();
            if (value.GetType().IsValueType && depth < 2)
                return value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => !f.Name.Equals("accumulator", StringComparison.OrdinalIgnoreCase)).ToDictionary(f => f.Name, f => Scalar(f.GetValue(value), depth + 1));
            return null;
        }
    }
}
