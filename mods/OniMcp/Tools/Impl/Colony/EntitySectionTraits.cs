using System.Collections.Generic;
using Klei.AI;

namespace OniMcp.Tools
{
    internal static class EntitySectionTraits
    {
        internal static List<Dictionary<string, object>> Read(IEnumerable<Trait> traits, List<string> errors)
        {
            if (traits == null)
            {
                errors.Add("Traits.list");
                return null;
            }

            var rows = new List<Dictionary<string, object>>();
            foreach (var trait in traits)
            {
                if (trait == null)
                {
                    errors.Add("Traits.null_entry");
                    continue;
                }

                // GetName/GetTooltip may invoke callbacks. Even the default tooltip
                // resolves modifier IDs through ResourceSet.Get, which logs a game
                // error before returning null for an absent attribute. Catching its
                // later exception cannot undo that side effect; read native fields.
                rows.Add(new Dictionary<string, object>
                {
                    ["id"] = trait.Id,
                    ["name"] = EntitySectionText.Plain(trait.Name),
                    ["description"] = EntitySectionText.Plain(trait.description),
                    ["textSource"] = "native_static_fields",
                    ["tooltip"] = null,
                    ["tooltipStatus"] = "not_evaluated",
                    ["attributeModifiers"] = ReadModifiers(trait.SelfModifiers, errors)
                });
                errors.Add("Traits.tooltip");
                if (trait.Name == null) errors.Add("Traits.name");
                if (trait.description == null) errors.Add("Traits.description");
            }
            return rows;
        }

        private static List<Dictionary<string, object>> ReadModifiers(IEnumerable<AttributeModifier> modifiers, List<string> errors)
        {
            if (modifiers == null)
            {
                errors.Add("Traits.attributeModifiers");
                return null;
            }
            var rows = new List<Dictionary<string, object>>();
            foreach (var modifier in modifiers)
            {
                if (modifier == null)
                {
                    errors.Add("Traits.attributeModifiers.null_entry");
                    continue;
                }
                float value = modifier.Value;
                bool finite = !float.IsNaN(value) && !float.IsInfinity(value);
                rows.Add(new Dictionary<string, object>
                {
                    ["attributeId"] = modifier.AttributeId,
                    ["value"] = finite ? (object)value : null,
                    ["valueStatus"] = finite ? "native_raw" : "unavailable",
                    ["isMultiplier"] = modifier.IsMultiplier
                });
                if (!finite) errors.Add("Traits.attributeModifiers.value");
            }
            return rows;
        }
    }
}
