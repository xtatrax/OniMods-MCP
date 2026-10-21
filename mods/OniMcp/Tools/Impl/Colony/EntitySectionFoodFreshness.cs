using System;
using System.Collections.Generic;
using System.Reflection;
using Klei.AI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static class EntitySectionFoodFreshness
    {
        internal const int ModifierLimit = 16;
        private static readonly FieldInfo ExistingDbField = BindExistingDb();

        internal static Dictionary<string, object> Read(GameObject go, KPrefabID prefab, bool includeModifiers)
        {
            var unavailable = new List<Dictionary<string, object>>();
            var amountData = new Dictionary<string, object>
            {
                ["value"] = JValue.CreateNull(), ["minimum"] = JValue.CreateNull(),
                ["maximum"] = JValue.CreateNull(), ["actualDeltaPerSimulationSecond"] = JValue.CreateNull(),
                ["paused"] = JValue.CreateNull(), ["nativeUnits"] = JValue.CreateNull()
            };
            var data = new Dictionary<string, object>
            {
                ["canRot"] = JValue.CreateNull(), ["nativeFraction"] = JValue.CreateNull(),
                ["rottableRunning"] = JValue.CreateNull(),
                ["rotAmount"] = amountData, ["temperatureK"] = JValue.CreateNull(),
                ["temperatureC"] = JValue.CreateNull(), ["simulationPaused"] = JValue.CreateNull(),
                ["stateTags"] = JValue.CreateNull(), ["unavailableFields"] = unavailable,
                ["sources"] = new Dictionary<string, object>
                {
                    ["canRot"] = "Edible.FoodInfo.CanRot", ["nativeFraction"] = "Rottable.Instance.RotConstitutionPercentage",
                    ["rottableRunning"] = "Rottable.Instance.IsRunning()",
                    ["rotAmount"] = "Db.Amounts.Rot.Lookup(GameObject); AmountInstance.value/GetMin/GetMax/GetDelta/paused",
                    ["temperature"] = "PrimaryElement.Temperature", ["stateTags"] = "KPrefabID.HasTag",
                    ["simulationPaused"] = "SpeedControlScreen.IsPaused"
                },
                ["units"] = new Dictionary<string, object>
                {
                    ["nativeFraction"] = "fraction", ["rotAmount"] = "raw_amount",
                    ["actualDeltaPerSimulationSecond"] = "raw_amount_per_simulation_second",
                    ["temperatureK"] = "kelvin", ["temperatureC"] = "celsius"
                }
            };
            ReadTemperature(go, data, unavailable);
            try
            {
                var speed = SpeedControlScreen.Instance;
                if (speed == null) Missing(unavailable, "simulationPaused", "speed_controller_missing");
                else data["simulationPaused"] = speed.IsPaused;
            }
            catch { Missing(unavailable, "simulationPaused", "read_failed"); }

            Edible edible = null;
            bool edibleRead = false;
            try { edible = go.GetComponent<Edible>(); edibleRead = true; }
            catch { Missing(unavailable, "canRot", "edible_read_failed"); }
            bool? canRot = null;
            if (edible != null)
            {
                try
                {
                    var food = edible.FoodInfo;
                    if (food == null) Missing(unavailable, "canRot", "food_definition_missing");
                    else { canRot = food.CanRot; data["canRot"] = canRot.Value; }
                }
                catch { Missing(unavailable, "canRot", "food_definition_read_failed"); }
            }
            else if (edibleRead)
            {
                data["status"] = "not_applicable"; data["reasonCode"] = "not_food";
                data["remainingEstimate"] = Estimate(null, null, null, null, null, null, null, null, "not_food");
                if (includeModifiers) UnavailableModifiers(data, "not_applicable");
                return data;
            }
            if (canRot == false)
            {
                data["status"] = "not_applicable"; data["reasonCode"] = "food_does_not_rot";
                data["remainingEstimate"] = Estimate(canRot, null, null, null, null, null, null, null);
                if (includeModifiers) UnavailableModifiers(data, "not_applicable");
                return data;
            }

            float? fraction = null;
            bool? running = null;
            try
            {
                var rottable = go.GetSMI<Rottable.Instance>();
                if (rottable == null)
                {
                    Missing(unavailable, "nativeFraction", "rottable_instance_missing");
                    Missing(unavailable, "rottableRunning", "rottable_instance_missing");
                }
                else
                {
                    try { running = rottable.IsRunning(); data["rottableRunning"] = running.Value; }
                    catch { Missing(unavailable, "rottableRunning", "read_failed"); }
                    fraction = ReadNumber(data, unavailable, "nativeFraction", () => rottable.RotConstitutionPercentage);
                }
            }
            catch
            {
                Missing(unavailable, "nativeFraction", "rottable_instance_read_failed");
                Missing(unavailable, "rottableRunning", "rottable_instance_read_failed");
            }
            if (fraction.HasValue && (fraction.Value < 0f || fraction.Value > 1f))
                Missing(unavailable, "nativeFraction", "out_of_range");
            ReadStateTags(prefab, data, unavailable);

            var amount = ExistingRotAmount(go, unavailable);
            float? value = null, minimum = null, maximum = null, delta = null;
            bool? paused = null;
            if (amount != null)
            {
                value = ReadNumber(amountData, unavailable, "value", () => amount.value, "rotAmount.");
                minimum = ReadNumber(amountData, unavailable, "minimum", amount.GetMin, "rotAmount.");
                maximum = ReadNumber(amountData, unavailable, "maximum", amount.GetMax, "rotAmount.");
                delta = ReadNumber(amountData, unavailable, "actualDeltaPerSimulationSecond", amount.GetDelta, "rotAmount.");
                try { paused = amount.paused; amountData["paused"] = paused.Value; }
                catch { Missing(unavailable, "rotAmount.paused", "read_failed"); }
                try { amountData["nativeUnits"] = amount.amount.units.ToString(); }
                catch { Missing(unavailable, "rotAmount.nativeUnits", "read_failed"); }
                if (minimum.HasValue && maximum.HasValue && value.HasValue
                    && (minimum.Value > maximum.Value || value.Value < minimum.Value || value.Value > maximum.Value))
                    Missing(unavailable, "rotAmount.value", "out_of_range");
            }
            data["remainingEstimate"] = Estimate(canRot, running, fraction, value, minimum, maximum, delta, paused);
            if (includeModifiers)
            {
                if (amount == null) UnavailableModifiers(data, "amount_unavailable");
                else ReadModifiers(amount, data, unavailable);
            }
            data["status"] = unavailable.Count == 0 ? "observed" : "partial";
            data["reasonCode"] = unavailable.Count == 0 ? (object)JValue.CreateNull() : "fields_unavailable";
            return data;
        }

        private static FieldInfo BindExistingDb()
        {
            try
            {
                var field = typeof(Db).GetField("_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                return field != null && field.IsStatic && field.DeclaringType == typeof(Db)
                    && field.FieldType == typeof(Db) ? field : null;
            }
            catch { return null; }
        }

        private static AmountInstance ExistingRotAmount(GameObject go, List<Dictionary<string, object>> unavailable)
        {
            string reason = null;
            try
            {
                if (ExistingDbField == null) reason = "database_binding_unavailable";
                else
                {
                    var db = ExistingDbField.GetValue(null) as Db;
                    if (db == null) reason = "database_cache_missing";
                    else if (db.Amounts == null) reason = "amount_definitions_missing";
                    else if (db.Amounts.Rot == null) reason = "rot_definition_missing";
                    else
                    {
                        var amount = db.Amounts.Rot.Lookup(go);
                        if (amount != null) return amount;
                        reason = "rot_amount_missing";
                    }
                }
            }
            catch { reason = "rot_amount_read_failed"; }
            Missing(unavailable, "rotAmount", reason);
            return null;
        }

        private static void ReadTemperature(GameObject go, Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable)
        {
            try
            {
                var primary = go.GetComponent<PrimaryElement>();
                if (primary == null) Missing(unavailable, "temperatureK", "primary_element_missing");
                else
                {
                    float? temperature = ReadNumber(data, unavailable, "temperatureK", () => primary.Temperature);
                    if (temperature.HasValue) { data["temperatureC"] = Math.Round((double)temperature.Value - 273.15, 2); return; }
                }
            }
            catch { Missing(unavailable, "temperatureK", "primary_element_read_failed"); }
            Missing(unavailable, "temperatureC", "temperature_unavailable");
        }

        private static void ReadStateTags(KPrefabID prefab, Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable)
        {
            try
            {
                if (prefab == null) { Missing(unavailable, "stateTags", "prefab_id_missing"); return; }
                var tags = new[] { GameTags.RotModifierTags.Fresh, GameTags.RotModifierTags.Stale,
                    GameTags.RotModifierTags.Refrigerated, GameTags.RotModifierTags.DeepFrozen,
                    GameTags.Preserved, GameTags.Entombed };
                var observed = new List<string>();
                foreach (var tag in tags) if (prefab.HasTag(tag)) observed.Add(tag.Name);
                data["stateTags"] = observed;
            }
            catch { Missing(unavailable, "stateTags", "read_failed"); }
        }

        private static float? ReadNumber(Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable, string field, Func<float> read, string prefix = "")
        {
            try
            {
                float value = read();
                if (float.IsNaN(value) || float.IsInfinity(value)) Missing(unavailable, prefix + field, "nonfinite");
                else { data[field] = value; return value; }
            }
            catch { Missing(unavailable, prefix + field, "read_failed"); }
            return null;
        }

        private static Dictionary<string, object> Estimate(bool? canRot, bool? running, float? fraction, float? value,
            float? minimum, float? maximum, float? delta, bool? paused, string forcedReason = null)
        {
            string reason = forcedReason;
            double? seconds = null;
            if (reason == null)
            {
                if (canRot == false) reason = "not_rottable";
                else if (canRot != true) reason = "rottability_unknown";
                else if (running != true) reason = running == false ? "rottable_not_running" : "rottable_running_unknown";
                else if (!fraction.HasValue) reason = "freshness_fraction_unavailable";
                else if (fraction.Value < 0f || fraction.Value > 1f) reason = "freshness_fraction_out_of_range";
                else if (!value.HasValue || !minimum.HasValue || !maximum.HasValue || !delta.HasValue || !paused.HasValue) reason = "amount_unavailable";
                else if (minimum.Value != 0f || maximum.Value <= 0f || value.Value < minimum.Value || value.Value > maximum.Value) reason = "amount_range_unusable";
                else if (paused.Value) reason = "amount_paused";
                else if (delta.Value == 0f) reason = "not_decreasing_now";
                else if (delta.Value > 0f) reason = "increasing_now";
                else
                {
                    double estimate = (double)value.Value / -(double)delta.Value;
                    if (double.IsNaN(estimate) || double.IsInfinity(estimate)) reason = "estimate_nonfinite";
                    else seconds = estimate;
                }
            }
            return new Dictionary<string, object>
            {
                ["simulationSeconds"] = seconds.HasValue ? (object)seconds.Value : JValue.CreateNull(),
                ["status"] = seconds.HasValue ? "estimated" : "not_estimated",
                ["reasonCode"] = reason == null ? (object)JValue.CreateNull() : reason,
                ["source"] = "derived_from_current_attribute_rate", ["assumption"] = "current_conditions_unchanged",
                ["timeBasis"] = "simulation_seconds"
            };
        }

        private static void ReadModifiers(AmountInstance amount, Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable)
        {
            try
            {
                var attribute = amount.deltaAttribute;
                if (attribute == null) { UnavailableModifiers(data, "delta_attribute_missing"); Missing(unavailable, "modifiers", "delta_attribute_missing"); return; }
                int count = attribute.Modifiers.Count;
                if (count < 0) { UnavailableModifiers(data, "modifier_count_invalid"); Missing(unavailable, "modifiers", "count_invalid"); return; }
                var rows = new List<object>();
                for (int i = 0; i < Math.Min(count, ModifierLimit); i++)
                {
                    try
                    {
                        var modifier = attribute.Modifiers[i];
                        if (modifier == null) { rows.Add(JValue.CreateNull()); Missing(unavailable, "modifiers[" + i + "]", "modifier_missing"); continue; }
                        var row = new Dictionary<string, object> { ["index"] = i, ["attributeId"] = JValue.CreateNull(),
                            ["value"] = JValue.CreateNull(), ["isMultiplier"] = JValue.CreateNull(), ["uiOnly"] = JValue.CreateNull() };
                        try
                        {
                            string id = modifier.AttributeId; row["attributeId"] = id ?? (object)JValue.CreateNull();
                            if (id == null) Missing(unavailable, "modifiers[" + i + "].attributeId", "attribute_id_missing");
                        }
                        catch { Missing(unavailable, "modifiers[" + i + "].attributeId", "read_failed"); }
                        ReadNumber(row, unavailable, "value", () => modifier.Value, "modifiers[" + i + "].");
                        try { row["isMultiplier"] = modifier.IsMultiplier; }
                        catch { Missing(unavailable, "modifiers[" + i + "].isMultiplier", "read_failed"); }
                        try { row["uiOnly"] = modifier.UIOnly; }
                        catch { Missing(unavailable, "modifiers[" + i + "].uiOnly", "read_failed"); }
                        rows.Add(row);
                    }
                    catch { rows.Add(JValue.CreateNull()); Missing(unavailable, "modifiers[" + i + "]", "read_failed"); }
                }
                data["modifiers"] = rows; data["modifierCount"] = count; data["returnedModifiers"] = rows.Count;
                data["modifierLimit"] = ModifierLimit; data["modifiersTruncated"] = count > rows.Count;
                data["modifiersStatus"] = "read";
            }
            catch { UnavailableModifiers(data, "modifiers_read_failed"); Missing(unavailable, "modifiers", "read_failed"); }
        }

        private static void UnavailableModifiers(Dictionary<string, object> data, string reason)
        {
            data["modifiers"] = JValue.CreateNull(); data["modifierCount"] = JValue.CreateNull();
            data["returnedModifiers"] = JValue.CreateNull(); data["modifierLimit"] = ModifierLimit;
            data["modifiersTruncated"] = JValue.CreateNull(); data["modifiersStatus"] = reason;
        }

        private static void Missing(List<Dictionary<string, object>> unavailable, string field, string reason)
        {
            unavailable.Add(new Dictionary<string, object> { ["field"] = field, ["reason"] = reason });
        }
    }
}
