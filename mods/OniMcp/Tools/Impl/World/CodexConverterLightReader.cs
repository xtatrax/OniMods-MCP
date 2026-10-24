using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Read nominal values only from the selected panel's exact cached native prefab.
    internal static class CodexConverterLightReader
    {
        internal static Dictionary<string, object> Read(object widget, List<string> issues, ref int remainingNodes)
        {
            if (!CodexPanelReader.IsKnown(widget, "CodexConversionPanel")) return null;
            // Reserve the entire small result before observing components or the prefab cache.
            if (remainingNodes < 20)
            {
                if (!issues.Contains("data_limit")) issues.Add("data_limit");
                return null;
            }
            remainingNodes -= 20;
            if (Game.Instance?.unlocks == null || CodexCache.entries == null)
                return Unavailable("cache_unavailable");
            UnityEngine.GameObject converter;
            try
            {
                object value;
                if (!CodexPanelReader.TryField(widget, "Converter", out value)
                    || (converter = value as UnityEngine.GameObject) == null)
                    return Unavailable("converter_unavailable");
            }
            catch { return Unavailable("converter_unavailable"); }
            KPrefabID identity;
            try { identity = converter.GetComponent<KPrefabID>(); }
            catch { return Unavailable("identity_unavailable"); }
            if (identity == null || identity.GetType() != typeof(KPrefabID) || !identity.PrefabTag.IsValid)
                return Unavailable("identity_unavailable");
            UnityEngine.GameObject cached;
            try { cached = Assets.TryGetPrefab(identity.PrefabTag); }
            catch { return Unavailable("prefab_lookup_unavailable"); }
            if (cached == null) return Unavailable("cached_prefab_unavailable");
            if (!object.ReferenceEquals(cached, converter)) return Unavailable("cache_identity_mismatch");
            Light2D light;
            try { light = converter.GetComponent<Light2D>(); }
            catch { return Unavailable("component_unavailable"); }
            if (light == null) return Unavailable("not_present");
            if (light.GetType() != typeof(Light2D)) return Unavailable("component_type_unavailable");
            object lux = null, range = null;
            string luxStatus = "read_unavailable", rangeStatus = "read_unavailable";
            try { lux = light.Lux; luxStatus = "observed"; }
            catch { }
            try
            {
                float value = light.Range;
                if (float.IsNaN(value) || float.IsInfinity(value)) rangeStatus = "invalid_numeric";
                else { range = value; rangeStatus = "observed"; }
            }
            catch { }
            bool luxAvailable = luxStatus == "observed", rangeAvailable = rangeStatus == "observed";
            return new Dictionary<string, object>
            {
                ["available"] = luxAvailable && rangeAvailable,
                ["status"] = luxAvailable && rangeAvailable ? "observed" : luxAvailable || rangeAvailable ? "partial" : "unavailable",
                ["source"] = "CodexConversionPanel.Converter/Assets.TryGetPrefab/Light2D.Lux/Range",
                ["semantics"] = "nominal_cached_prefab_light",
                ["lux"] = new Dictionary<string, object>
                {
                    ["available"] = luxAvailable, ["status"] = luxStatus, ["value"] = lux, ["unit"] = "lux"
                },
                ["range"] = new Dictionary<string, object>
                {
                    ["available"] = rangeAvailable, ["status"] = rangeStatus, ["value"] = range,
                    ["unit"] = "cells", ["semantics"] = "radius"
                }
            };
        }

        private static Dictionary<string, object> Unavailable(string status) => new Dictionary<string, object>
        {
            ["available"] = false, ["status"] = status
        };
    }
}
