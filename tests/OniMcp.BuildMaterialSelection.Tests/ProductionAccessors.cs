using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        // The production private selection, requirement reader and projection run unchanged.
        // This is the same requirement assignment as TryPlanOne, not a planner test.
        internal static Dictionary<string, object> HostSelect(BuildingDef def, string material, int worldId)
        {
            var selected = SelectElements(def, material, worldId);
            selected.RequiredKg = RequiredMaterialKg(def);
            return selected.ToDictionary();
        }

        // Unchanged generic collaborators from BuildPlanningErrors.cs:79-82,175-178.
        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static bool EqualsIgnoreCase(string value, string query)
        {
            return string.Equals(value, query, StringComparison.OrdinalIgnoreCase);
        }
    }

    internal static partial class ToolUtil
    {
        // Boundary copy of ToolUtil.cs:306-309. It does not select or enumerate materials.
        public static float SafeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    public static partial class WorldEditorTools
    {
        internal static bool EnforceNormalMaterialRules;
    }
}
