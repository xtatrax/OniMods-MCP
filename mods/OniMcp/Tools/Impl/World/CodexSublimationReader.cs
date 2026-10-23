using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Observe cached definition fields; never configure UI, initialize caches or simulate emission.
    internal static class CodexSublimationReader
    {
        internal static Dictionary<string, object> Read(object widget, List<string> issues, ref int remainingNodes)
        {
            object transition;
            if (!CodexPanelReader.TryField(widget, "transitionType", out transition)
                || Convert.ToString(transition) != "SUBLIMATE") return null;
            // A fixed conservative reserve covers this small projection and all nested metadata.
            if (remainingNodes < 24) return Unavailable(null, "data_limit", issues);
            remainingNodes -= 24;
            object rawSource;
            if (!CodexPanelReader.TryField(widget, "sourceElement", out rawSource)
                || !CodexPanelReader.IsKnown(rawSource, "Element"))
                return Unavailable(null, "source_unavailable", issues);
            if (Game.Instance?.unlocks == null || CodexCache.entries == null)
                return Unavailable(null, "cache_unavailable", issues);
            var source = (Element)rawSource;
            Element direct;
            try { direct = ElementLoader.FindElementByHash(source.sublimateId); }
            catch { return Unavailable(null, "direct_lookup_unavailable", issues); }
            if (direct != null)
                return new Dictionary<string, object>
                {
                    ["required"] = false, ["status"] = "not_required", ["source"] = "Element.sublimateId"
                };
            var result = Result(true);
            Sublimates component;
            try
            {
                // Match the native hash without registering names through TagManager.Create.
                var prefab = Assets.TryGetPrefab(new Tag(source.id.ToString()));
                if (prefab == null) return Failure(result, "prefab_unavailable", issues);
                component = prefab.GetComponent<Sublimates>();
                if (component == null) return Failure(result, "component_unavailable", issues);
            }
            catch { return Failure(result, "prefab_lookup_unavailable", issues); }
            var info = component.info;
            bool finiteRate = Finite(info.sublimationRate), finitePower = Finite(info.massPower);
            result["values"] = new Dictionary<string, object>
            {
                ["sublimationRate"] = finiteRate ? (object)info.sublimationRate : null,
                ["massPower"] = finitePower ? (object)info.massPower : null
            };
            result["semantics"] = new Dictionary<string, object>
            {
                ["sublimationRate"] = "native_mass_dependent_rate_coefficient", ["massPower"] = "mass_exponent"
            };
            // available describes the complete finite pair, not the presence of the component.
            result["available"] = finiteRate && finitePower;
            if (!finiteRate || !finitePower) return Failure(result, "invalid_numeric", issues);
            Element target;
            try { target = ElementLoader.FindElementByHash(info.sublimatedElement); }
            catch { return Failure(result, "target_lookup_unavailable", issues); }
            result["targetResolved"] = target != null;
            if (target == null) return Failure(result, "target_unresolved", issues);
            Dictionary<string, object> metadata;
            bool filtered;
            try
            {
                if (!CodexReadTools.TryCategoryMemberMetadata(target.id.ToString(), out metadata, out filtered))
                    return Failure(result, filtered ? "target_filtered" : "target_metadata_unavailable", issues);
            }
            catch { return Failure(result, "target_metadata_unavailable", issues); }
            result["target"] = metadata;
            result["status"] = "resolved";
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static Dictionary<string, object> Result(bool? required) => new Dictionary<string, object>
        {
            ["required"] = required, ["available"] = false, ["status"] = "unavailable",
            ["source"] = required == true ? "Assets.TryGetPrefab/Sublimates.info" : null,
            ["values"] = null, ["semantics"] = null, ["targetResolved"] = null, ["target"] = null
        };
        private static Dictionary<string, object> Unavailable(bool? required, string status, List<string> issues)
            => Failure(Result(required), status, issues);
        private static Dictionary<string, object> Failure(Dictionary<string, object> result, string status, List<string> issues)
        {
            result["status"] = status;
            string issue = status == "data_limit" ? status : "prefab_sublimates_" + status;
            if (!issues.Contains(issue)) issues.Add(issue);
            return result;
        }
    }
}
