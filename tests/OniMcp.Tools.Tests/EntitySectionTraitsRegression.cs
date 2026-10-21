using System;
using System.Collections.Generic;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class EntitySectionTraitsRegression
{
    internal static void Run()
    {
        var plant = new Trait
        {
            Id = "WormPlantOriginal",
            Name = "<b>Plant</b>",
            description = "<color=green>Native description</color>",
            ThrowFromFormatters = true
        };
        plant.SelfModifiers.Add(new AttributeModifier { AttributeId = "MaxRadiationThreshold", Value = 12f });
        var dupe = new Trait { Id = "Handy", Name = "Handy", description = "Native dupe description" };
        dupe.SelfModifiers.Add(new AttributeModifier { AttributeId = "Machinery", Value = 3f });
        var errors = new List<string>();
        var rows = EntitySectionTraits.Read(new[] { plant, dupe }, errors);
        if (plant.NameCalls != 0 || plant.TooltipCalls != 0 || dupe.NameCalls != 0 || dupe.TooltipCalls != 0)
            throw new Exception("Trait reads must not invoke native display callbacks or formatters");
        if (rows.Count != 2 || !Equals(rows[0]["id"], plant.Id) || !Equals(rows[1]["id"], dupe.Id)
            || !Equals(rows[0]["name"], "Plant") || !Equals(rows[1]["name"], "Handy"))
            throw new Exception("Plant and duplicant trait identity/static names must be preserved");
        if (!Equals(rows[0]["description"], "Native description") || rows[0]["tooltip"] != null
            || !Equals(rows[0]["tooltipStatus"], "not_evaluated") || !errors.Contains("Traits.tooltip"))
            throw new Exception("Static descriptions and unavailable dynamic tooltips must remain distinct");
        var json = JArray.Parse(JsonConvert.SerializeObject(rows, McpJsonUtil.Settings));
        if ((string)json[0]["attributeModifiers"][0]["attributeId"] != "MaxRadiationThreshold"
            || (float)json[0]["attributeModifiers"][0]["value"] != 12f
            || (float)json[1]["attributeModifiers"][0]["value"] != 3f
            || json[0]["tooltip"].Type != JTokenType.Null)
            throw new Exception("Raw modifiers must not resolve missing attributes; null tooltips must serialize");

        var missing = new Trait { Id = "MissingText", Name = null, description = null, SelfModifiers = null };
        var unavailable = EntitySectionTraits.Read(new Trait[] { missing, null }, errors);
        if (unavailable.Count != 1 || unavailable[0]["name"] != null || unavailable[0]["description"] != null
            || unavailable[0]["attributeModifiers"] != null || !errors.Contains("Traits.null_entry")
            || !errors.Contains("Traits.attributeModifiers"))
            throw new Exception("Missing trait fields must remain unavailable without failing the entity read");
        if (EntitySectionTraits.Read(new Trait[0], new List<string>()).Count != 0)
            throw new Exception("An empty native trait list must remain empty");

        var modifiers = new Trait { Id = "RawModifiers", Name = "Raw", description = "Static" };
        modifiers.SelfModifiers.Add(new AttributeModifier { AttributeId = "Negative", Value = -3f, IsMultiplier = true });
        modifiers.SelfModifiers.Add(new AttributeModifier { AttributeId = "Zero", Value = 0f });
        modifiers.SelfModifiers.Add(new AttributeModifier { AttributeId = "NonFinite", Value = float.NaN });
        modifiers.SelfModifiers.Add(null);
        var modifierRows = EntitySectionTraits.Read(new[] { modifiers }, errors);
        var modifierJson = JArray.Parse(JsonConvert.SerializeObject(modifierRows, McpJsonUtil.Settings));
        if ((float)modifierJson[0]["attributeModifiers"][0]["value"] != -3f
            || !(bool)modifierJson[0]["attributeModifiers"][0]["isMultiplier"]
            || (float)modifierJson[0]["attributeModifiers"][1]["value"] != 0f
            || modifierJson[0]["attributeModifiers"][2]["value"].Type != JTokenType.Null
            || !errors.Contains("Traits.attributeModifiers.value")
            || !errors.Contains("Traits.attributeModifiers.null_entry"))
            throw new Exception("Raw negative/zero modifiers and unavailable non-finite values must remain distinct");
        if (EntitySectionTraits.Read(null, errors) != null || !errors.Contains("Traits.list"))
            throw new Exception("A missing native trait list must not be reported as empty");
        Console.WriteLine("Entity traits: static native fields, missing attributes, nulls and zero display getter calls passed");
    }
}

namespace Klei.AI
{
    internal sealed class Trait
    {
        internal string Id;
        internal string Name;
        internal string description;
        internal List<AttributeModifier> SelfModifiers = new List<AttributeModifier>();
        internal int NameCalls;
        internal int TooltipCalls;
        internal bool ThrowFromFormatters;

        internal string GetName()
        {
            NameCalls++;
            if (ThrowFromFormatters) throw new InvalidOperationException("Native name callback invoked");
            return Name;
        }

        internal string GetTooltip()
        {
            TooltipCalls++;
            if (ThrowFromFormatters) throw new NullReferenceException("Missing native attribute after logging");
            return description;
        }
    }

    internal sealed partial class AttributeModifier
    {
        internal string AttributeId { get; set; }
        internal float Value { get; set; }
        internal bool IsMultiplier { get; set; }
    }
}
