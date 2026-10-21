using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class FoodFreshnessRegression
{
    private static int checks;
    private static int dbReadsBefore;
    private sealed class Fixture
    {
        internal GameObject Go;
        internal KPrefabID Id;
        internal Edible Edible;
        internal PrimaryElement Primary;
        internal Rottable.Instance Smi;
        internal Amount Definition;
        internal AmountInstance Amount;
        internal AttributeInstance Attribute;
    }
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new InvalidOperationException("Food freshness: " + message); }
    private static void Near(JToken actual, double expected, string message)
    { Check(actual != null && actual.Type != JTokenType.Null && Math.Abs((double)actual - expected) < 0.0001, message); }
    private static void Null(JToken actual, string message)
    { Check(actual != null && actual.Type == JTokenType.Null, message + " explicit null"); }
    private static bool Missing(JObject body, string field, string reason)
    { return ((JArray)body["unavailableFields"]).Any(x => (string)x["field"] == field && (string)x["reason"] == reason); }
    private static void Reset()
    {
        Components.BuildingCompletes.Items.Clear(); Components.Pickupables.Items.Clear();
        Components.LiveMinionIdentities.Items.Clear(); Components.HarvestDesignatables.Items.Clear();
        Components.Uprootables.Items.Clear(); Components.Geysers.Worlds.Clear();
        Db.Current = new Db { Amounts = new Database.Amounts { Rot = new Amount() } };
        SpeedControlScreen.Instance = new SpeedControlScreen(); FreshnessBoundary.ResetCounters();
    }
    private static Fixture Food(int id = 10, int world = 0, string prefab = "FixtureFood")
    {
        var go = new GameObject { Cell = Grid.XYToCell(10, 10), WorldId = world, NativeName = "<b>Fresh food</b>" };
        var f = new Fixture { Go = go, Definition = Db.Current.Amounts.Rot, Attribute = new AttributeInstance(), Smi = new Rottable.Instance(), Primary = new PrimaryElement { TemperatureValue = 300.15f }, Edible = new Edible { ExistingFood = new EdiblesManager.FoodInfo { CanRot = true } } };
        f.Id = new KPrefabID { InstanceID = id, PrefabTag = new Tag(prefab), gameObject = go };
        f.Id.Tags.Add(GameTags.RotModifierTags.Fresh);
        f.Amount = new AmountInstance { amount = f.Definition, deltaAttribute = f.Attribute };
        go.ExistingComponents[typeof(KPrefabID)] = f.Id; go.ExistingComponents[typeof(PrimaryElement)] = f.Primary;
        go.ExistingComponents[typeof(Edible)] = f.Edible; go.ExistingSmi = f.Smi;
        go.BoundAmountDefinition = f.Definition; go.BoundAmount = f.Amount;
        Components.Pickupables.Items.Add(new Pickupable { gameObject = go }); return f;
    }
    private static JObject Direct(Fixture f, bool modifiers = false)
    {
        FreshnessBoundary.InRead = true;
        try { return JObject.Parse(JsonConvert.SerializeObject(EntitySectionFoodFreshness.Read(f.Go, f.Id, modifiers), McpJsonUtil.Settings)); }
        finally { FreshnessBoundary.InRead = false; }
    }
    private static CallToolResult Route(JObject args)
    {
        FreshnessBoundary.InRead = true;
        try { return EntitySectionTools.Read(args); }
        finally { FreshnessBoundary.InRead = false; }
    }
    private static JObject Routed(Fixture f, bool? modifiers = null)
    {
        var args = new JObject { ["section"] = "freshness", ["id"] = f.Id.InstanceID };
        if (modifiers.HasValue) args["includeFreshnessModifiers"] = modifiers.Value;
        var result = Route(args); Check(!result.IsError, "real section routing accepts freshness");
        var body = JObject.Parse(result.Content[0].Text);
        Check((string)body["scope"] == "live_instance" && (int)body["items"][0]["id"] == f.Id.InstanceID, "real routing retains exact live identity");
        return (JObject)body["items"][0]["freshness"];
    }
    private static void NoMutation(string label)
    {
        Check(Db.Reads == dbReadsBefore, label + " cache-only database read");
        Check(FreshnessBoundary.ForbiddenCalls == 0, label + " no constructor/write/factory/formatter");
    }
    private static void EstimateNull(JObject b, string reason)
    {
        Null(b["remainingEstimate"]?["simulationSeconds"], reason);
        Check((string)b["remainingEstimate"]?["reasonCode"] == reason, reason + " machine reason");
        Check((string)b["remainingEstimate"]?["status"] == "not_estimated", reason + " estimate status");
    }
    private static void NormalAndModifiers()
    {
        Reset(); var f = Food();
        var a = new AttributeModifier { AttributeId = "NativeRotRate", Value = -2 };
        var ui = new AttributeModifier { AttributeId = "NativeRotRate", Value = 20, UIOnly = true };
        f.Attribute.ExistingModifiers.AddRange(new[] { a, ui, a, new AttributeModifier { AttributeId = "Zero", Value = -0f, IsMultiplier = true } });
        var b = Routed(f);
        Check((bool)b["canRot"] && (bool)b["rottableRunning"] && (bool)b["simulationPaused"], "native applicability and independent pause states");
        Near(b["nativeFraction"], 0.75, "native SMI fraction preserved rather than amount/max");
        Near(b["rotAmount"]["value"], 30, "native amount value"); Near(b["rotAmount"]["minimum"], 0, "native amount minimum"); Near(b["rotAmount"]["maximum"], 100, "native amount maximum");
        Near(b["rotAmount"]["actualDeltaPerSimulationSecond"], -2, "actual rate excludes independent UI display rate");
        Check(f.Attribute.DisplayReads == 0 && (string)b["rotAmount"]["nativeUnits"] == "Flat", "native units with no display getter");
        Near(b["temperatureK"], 300.15, "physical object Kelvin"); Near(b["temperatureC"], 27, "derived Celsius"); Near(b["remainingEstimate"]["simulationSeconds"], 15, "derived simulation seconds under current conditions");
        Check((string)b["remainingEstimate"]["source"] == "derived_from_current_attribute_rate" && (string)b["remainingEstimate"]["assumption"] == "current_conditions_unchanged" && (string)b["remainingEstimate"]["timeBasis"] == "simulation_seconds", "estimate provenance is explicit");
        Check((string)b["units"]["nativeFraction"] == "fraction" && (string)b["units"]["actualDeltaPerSimulationSecond"] == "raw_amount_per_simulation_second", "native units are not percent/cycle");
        Check(b["modifiers"] == null && f.Attribute.ModifierReads == 0, "brief/default response never reads modifier list");
        Check((string)b["status"] == "observed" && ((JArray)b["unavailableFields"]).Count == 0, "normal observation is complete");
        Check(((JArray)b["stateTags"]).Values<string>().SequenceEqual(new[] { "Fresh" }), "native state tags observed without synthesized state");
        var details = Routed(f, true); var rows = (JArray)details["modifiers"];
        Check(rows.Count == 4 && (int)details["modifierCount"] == 4 && (int)details["returnedModifiers"] == 4 && !(bool)details["modifiersTruncated"], "explicit modifiers have counts and no truncation");
        Check((int)rows[1]["index"] == 1 && (bool)rows[1]["uiOnly"] && (string)rows[0]["attributeId"] == (string)rows[2]["attributeId"], "UIOnly, duplicate identity and native order preserved");
        Near(rows[0]["value"], -2, "raw negative modifier"); Near(rows[1]["value"], 20, "display-only modifier still reported raw"); Near(rows[2]["value"], -2, "duplicate raw modifier"); Near(rows[3]["value"], 0, "real zero modifier preserved");
        Check((bool)rows[3]["isMultiplier"], "raw multiplier flag");
        var before = f.Attribute.ExistingModifiers.ToArray(); var tags = f.Id.Tags.ToArray(); var dict = f.Go.ExistingComponents.ToArray();
        var repeated = Direct(f, true); Check(JToken.DeepEquals(details, repeated), "repeat observation has stable data");
        Check(ReferenceEquals(f.Go.BoundAmount, f.Amount) && ReferenceEquals(f.Go.ExistingSmi, f.Smi) && f.Amount.RawValue == 30 && !f.Amount.PausedValue && f.Smi.RunningValue, "repeat read leaves amount/SMI untouched");
        Check(f.Attribute.ExistingModifiers.SequenceEqual(before) && f.Id.Tags.SetEquals(tags) && f.Go.ExistingComponents.SequenceEqual(dict), "repeat read leaves modifier references, tags and components untouched");
        Check(f.Attribute.DisplayReads == 0 && FreshnessBoundary.FormattingCalls == 0, "no native UI formatter evaluation"); NoMutation("normal/repeated");

        Reset(); f = Food();
        for (int i = 0; i < 21; i++) f.Attribute.ExistingModifiers.Add(new AttributeModifier { AttributeId = "Index" + i, Value = i - 10, UIOnly = i % 2 == 0 });
        b = Direct(f, true); rows = (JArray)b["modifiers"];
        Check(rows.Count == 16 && (int)b["modifierLimit"] == 16 && (int)b["modifierCount"] == 21 && (int)b["returnedModifiers"] == 16 && (bool)b["modifiersTruncated"], "fixed16 cap reports full count");
        Check(rows.Select(x => (int)x["index"]).SequenceEqual(Enumerable.Range(0, 16)), "truncation preserves prefix order"); NoMutation("bounded modifiers");
        Reset(); f = Food(); f.Attribute.ExistingModifiers.Add(null); f.Attribute.ExistingModifiers.Add(new AttributeModifier { AttributeId = null, Value = float.NaN });
        b = Direct(f, true); Null(b["modifiers"][0], "null modifier row retained"); Null(b["modifiers"][1]["value"], "nonfinite raw modifier");
        Check(Missing(b, "modifiers[0]", "modifier_missing") && Missing(b, "modifiers[1].value", "nonfinite"), "modifier partial reasons match rows"); NoMutation("partial modifiers");
        Reset(); f = Food(); f.Attribute.ModifierReader = () => { throw new Exception("modifier cache unavailable"); };
        b = Direct(f, true); Null(b["modifiers"], "failed modifier list"); Null(b["modifierCount"], "failed modifier count not zero"); Near(b["rotAmount"]["actualDeltaPerSimulationSecond"], -2, "modifier detail failure preserves actual rate"); NoMutation("modifier fault");
    }
    private static void ApplicabilityAndMissing()
    {
        Reset(); var f = Food(); f.Go.ExistingComponents.Remove(typeof(Edible)); var b = Direct(f, true);
        Check((string)b["status"] == "not_applicable" && (string)b["reasonCode"] == "not_food", "nonfood is not applicable"); Null(b["canRot"], "nonfood applicability"); Null(b["nativeFraction"], "nonfood freshness not full"); Check(f.Go.SmiLookupReads == 0 && f.Go.AmountLookupReads == 0, "nonfood does not query rot caches"); EstimateNull(b, "not_food");
        Reset(); f = Food(); f.Edible.ExistingFood.CanRot = false; b = Direct(f, true);
        Check((bool)b["canRot"] == false && (string)b["reasonCode"] == "food_does_not_rot", "known nonrottable distinct from missing"); Null(b["nativeFraction"], "nonrottable freshness"); Check(f.Go.SmiLookupReads == 0 && f.Go.AmountLookupReads == 0, "nonrottable avoids native rot lookup"); EstimateNull(b, "not_rottable");
        Reset(); f = Food(); f.Edible.ExistingFood = null; b = Direct(f); Null(b["canRot"], "missing food definition"); Check(Missing(b, "canRot", "food_definition_missing"), "missing food reason"); Near(b["nativeFraction"], 0.75, "other native success retained"); EstimateNull(b, "rottability_unknown");
        foreach (string missing in new[] { "db", "amounts", "definition", "instance", "identity" })
        {
            Reset(); f = Food();
            if (missing == "db") Db.Current = null;
            else if (missing == "amounts") Db.Current.Amounts = null;
            else if (missing == "definition") Db.Current.Amounts.Rot = null;
            else if (missing == "instance") f.Go.BoundAmount = null;
            else Db.Current.Amounts.Rot = new Amount();
            b = Direct(f); Null(b["rotAmount"]["value"], missing + " native amount"); Near(b["nativeFraction"], 0.75, missing + " independent SMI fraction"); EstimateNull(b, "amount_unavailable"); NoMutation(missing + " cache");
        }
        Reset(); f = Food(); f.Go.ExistingSmi = null; b = Direct(f); Null(b["nativeFraction"], "SMI missing"); Null(b["rottableRunning"], "SMI running unknown"); Near(b["rotAmount"]["value"], 30, "SMI missing keeps amount success"); EstimateNull(b, "rottable_running_unknown");
        Reset(); f = Food(); f.Go.ExistingComponents.Remove(typeof(PrimaryElement)); b = Direct(f); Null(b["temperatureK"], "primary missing"); Null(b["temperatureC"], "primary Celsius missing"); Near(b["rotAmount"]["value"], 30, "primary missing keeps amount");
        Reset(); f = Food(); SpeedControlScreen.Instance = null; b = Direct(f); Null(b["simulationPaused"], "speed controller missing"); Near(b["rotAmount"]["value"], 30, "speed missing keeps amount");
        NoMutation("applicability/missing");
    }
    private static void EstimateAndFaults()
    {
        Reset(); var f = Food(); f.Amount.RawValue = 0; f.Smi.FractionValue = 0; var b = Direct(f); Near(b["nativeFraction"], 0, "true native freshness zero"); Near(b["rotAmount"]["value"], 0, "true amount zero"); Near(b["remainingEstimate"]["simulationSeconds"], 0, "zero crossing estimate without claiming rot event");
        foreach (string state in new[] { "zero_rate", "positive_rate", "amount_paused", "not_running", "running_unknown", "range", "fraction_range" })
        {
            Reset(); f = Food(); string reason;
            if (state == "zero_rate") { f.Attribute.ActualValue = 0; reason = "not_decreasing_now"; }
            else if (state == "positive_rate") { f.Attribute.ActualValue = 2; reason = "increasing_now"; }
            else if (state == "amount_paused") { f.Amount.PausedValue = true; reason = "amount_paused"; }
            else if (state == "not_running") { f.Smi.RunningValue = false; reason = "rottable_not_running"; }
            else if (state == "running_unknown") { f.Smi.RunningReader = () => { throw new Exception(); }; reason = "rottable_running_unknown"; }
            else if (state == "range") { f.Amount.MinValue = 1; reason = "amount_range_unusable"; }
            else { f.Smi.FractionValue = 1.2f; reason = "freshness_fraction_out_of_range"; }
            b = Direct(f); EstimateNull(b, reason); Near(b["rotAmount"]["value"], 30, state + " retains raw amount");
            if (state == "fraction_range") Near(b["nativeFraction"], 1.2, "out-of-range raw fraction not clamped");
            NoMutation(state);
        }
        Reset(); f = Food(); SpeedControlScreen.Instance.PausedValue = false; b = Direct(f); Near(b["remainingEstimate"]["simulationSeconds"], 15, "simulation running keeps native simulation-second estimate"); Check(!(bool)b["simulationPaused"] && !(bool)b["rotAmount"]["paused"], "simulation/amount pause separate");
        foreach (string field in new[] { "fraction", "value", "min", "max", "delta", "temperature" })
        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Reset(); f = Food(); string path;
            if (field == "fraction") { f.Smi.FractionValue = invalid; path = "nativeFraction"; }
            else if (field == "value") { f.Amount.RawValue = invalid; path = "rotAmount.value"; }
            else if (field == "min") { f.Amount.MinValue = invalid; path = "rotAmount.minimum"; }
            else if (field == "max") { f.Amount.MaxValue = invalid; path = "rotAmount.maximum"; }
            else if (field == "delta") { f.Attribute.ActualValue = invalid; path = "rotAmount.actualDeltaPerSimulationSecond"; }
            else { f.Primary.TemperatureValue = invalid; path = "temperatureK"; }
            b = Direct(f); Null(b.SelectToken(path), field + " nonfinite"); Check(Missing(b, path, "nonfinite"), field + " nonfinite reason"); NoMutation(field + " nonfinite");
        }
        foreach (string field in new[] { "food", "smi", "amount", "value", "min", "max", "delta", "paused", "temperature", "speed", "tags" })
        {
            Reset(); f = Food();
            if (field == "food") f.Edible.FoodReader = () => { throw new Exception(); };
            else if (field == "smi") f.Go.SmiReader = () => { throw new Exception(); };
            else if (field == "amount") f.Go.AmountReader = () => { throw new Exception(); };
            else if (field == "value") f.Amount.ValueReader = () => { throw new Exception(); };
            else if (field == "min") f.Amount.MinReader = () => { throw new Exception(); };
            else if (field == "max") f.Amount.MaxReader = () => { throw new Exception(); };
            else if (field == "delta") f.Amount.DeltaReader = () => { throw new Exception(); };
            else if (field == "paused") f.Amount.PausedReader = () => { throw new Exception(); };
            else if (field == "temperature") f.Primary.TemperatureReader = () => { throw new Exception(); };
            else if (field == "speed") SpeedControlScreen.Instance.PausedReader = () => { throw new Exception(); };
            else f.Id.TagReader = _ => { throw new Exception(); };
            b = Direct(f); Check((string)b["status"] == "partial" && ((JArray)b["unavailableFields"]).Count > 0, field + " fault does not fail whole response");
            if (field != "fraction" && field != "smi") Near(b["nativeFraction"], 0.75, field + " fault keeps independent fraction"); NoMutation(field + " fault");
        }
    }
    private static void Routing()
    {
        Reset(); var first = Food(10, 0); var second = Food(20, 0); var other = Food(30, 1); var rejected = Food(40, 0, "OtherFood");
        var result = Route(new JObject { ["section"] = "freshness", ["query"] = "FixtureFood", ["worldId"] = 0, ["limit"] = 1 });
        Check(!result.IsError, "query routing succeeds"); var body = JObject.Parse(result.Content[0].Text);
        Check((int)body["matched"] == 2 && (int)body["returned"] == 1 && (bool)body["truncated"], "native target selection and limit before snapshot");
        Check((int)body["items"][0]["id"] == 10 && first.Go.SmiLookupReads == 1 && second.Go.SmiLookupReads == 0 && other.Go.SmiLookupReads == 0 && rejected.Go.SmiLookupReads == 0, "only selected live target native cache is read");
        result = Route(new JObject { ["section"] = "freshness", ["id"] = 999, ["query"] = "FixtureFood" });
        Check(result.IsError && second.Go.SmiLookupReads == 0, "stale ID never falls back to same-prefab food");
        foreach (JToken invalid in new JToken[] { JValue.CreateNull(), new JValue("true"), new JValue(1) })
        {
            result = Route(new JObject { ["section"] = "freshness", ["id"] = first.Id.InstanceID, ["includeFreshnessModifiers"] = invalid });
            Check(result.IsError, "freshness modifiers require an explicit boolean");
        }
        foreach (string section in new[] { "status", "properties", "personality", "work" })
        {
            int smi = first.Go.SmiLookupReads, amount = first.Go.AmountLookupReads, edible = first.Go.EdibleLookupReads;
            result = Route(new JObject { ["section"] = section, ["id"] = first.Id.InstanceID, ["includeFreshnessModifiers"] = "unrelated ignored" });
            Check(!result.IsError, section + " existing route retained");
            body = JObject.Parse(result.Content[0].Text);
            Check(body["items"][0]["freshness"] == null && first.Go.SmiLookupReads == smi && first.Go.AmountLookupReads == amount && first.Go.EdibleLookupReads == edible, section + " has zero new freshness native reads");
        }
        NoMutation("routing");
    }
    internal static void Run()
    {
        var priorDb = Db.Current; var priorSpeed = SpeedControlScreen.Instance; dbReadsBefore = Db.Reads;
        FreshnessBoundary.GuardNativeFactory = true;
        try { NormalAndModifiers(); ApplicabilityAndMissing(); EstimateAndFaults(); Routing(); }
        finally { Reset(); Db.Current = priorDb; SpeedControlScreen.Instance = priorSpeed; FreshnessBoundary.GuardNativeFactory = false; FreshnessBoundary.InRead = false; }
        Console.WriteLine("Food freshness regression: " + checks + " checks passed (full reader and section routing; native inputs substituted)");
    }
}
