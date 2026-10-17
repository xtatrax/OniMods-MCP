using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static SupportValidation ValidateSupport(BuildingDef def, int x, int y, bool allowUnsupported,
            HashSet<int> plannedSupportCells, Orientation orientation = Orientation.Neutral)
        {
            string rule = def?.BuildLocationRule.ToString() ?? "unknown";
            var result = SupportValidation.Success(rule, null);
            if (def == null || !ConstructionSupportGeometry.IsKnownRule(rule))
            {
                result.Observation["status"] = rule == "Anywhere" || rule == "Tile" ? "not_required" : "not_evaluated";
                result.Observation["currentValid"] = null;
                result.Observation["nativeFoundationValid"] = null;
                return result;
            }
            var shape = NativeBuildingFootprint.FromLowerLeft(def, x, y, orientation);
            int origin = shape.Origin?.Cell ?? Grid.InvalidCell;
            int worldId = Grid.IsWorldValidCell(origin) ? Grid.WorldIdx[origin] : -1;
            var cells = ConstructionSupportGeometry.Offsets(rule, def.WidthInCells, def.HeightInCells, orientation)
                .Select(o => ConstructionSupportInspection.Offset(origin, o)).Distinct().ToList();
            var samples = new List<Dictionary<string, object>>();
            bool unknown = !shape.Available || cells.Count == 0;
            bool pending = false, allPlanned = true, scheduledRemoval = false;
            foreach (int cell in cells)
            {
                bool observable = ConstructionSupportInspection.Observable(cell, worldId);
                unknown |= !observable;
                var item = Grid.IsValidCell(cell) ? ConstructionSupportInspection.Cell(cell) : new Dictionary<string, object> { ["cell"] = null };
                bool solid = observable && Grid.Solid[cell];
                bool blueprint = observable && !solid && HasSupportBlueprint(cell);
                bool planned = observable && !solid && plannedSupportCells != null && plannedSupportCells.Contains(cell);
                bool dig = observable && Grid.Objects[cell, (int)ObjectLayer.DigPlacer] != null;
                bool removal = dig;
                if (observable)
                {
                    var go = Grid.Objects[cell, (int)ObjectLayer.Building];
                    removal |= go?.GetComponent<Deconstructable>()?.IsMarkedForDeconstruction() == true;
                }
                scheduledRemoval |= removal;
                pending |= blueprint || planned;
                allPlanned &= solid || blueprint || planned;
                item["state"] = !observable ? "unknown" : solid ? Grid.Foundation[cell] ? "constructed_support" : "natural_solid"
                    : blueprint ? "support_blueprint" : planned ? "planned_in_request" : "missing";
                item["scheduledRemoval"] = observable ? (object)removal : null;
                samples.Add(item);
                if (!observable || (!solid && !blueprint && !planned)) result.MissingSupportCells.Add(item);
            }
            bool? native = unknown ? (bool?)null : BuildingDef.CheckFoundation(origin, orientation,
                def.BuildLocationRule, def.WidthInCells, def.HeightInCells);
            // Planned tiles are a sequencing dependency, not a successful current foundation check.
            bool pendingBase = ConstructionSupportGeometry.IsBaseRule(rule) && !unknown && allPlanned && pending;
            result.Valid = native == true || pendingBase || (!unknown && allowUnsupported && ConstructionSupportGeometry.IsBaseRule(rule));
            result.WarningOnly = result.Valid && (native != true || scheduledRemoval);
            result.Error = native == true ? null : pendingBase ? "Complete support tiles before this building; current native foundation check failed."
                : "Unsupported " + rule + " building: inspect native support cells before placing " + def.PrefabID;
            result.Observation["status"] = unknown ? "unknown" : native == true ? scheduledRemoval ? "support_scheduled_for_removal" : "supported_now"
                : pendingBase ? "awaiting_support_construction" : "missing_support";
            result.Observation["currentValid"] = native;
            result.Observation["nativeFoundationValid"] = native;
            result.Observation["supportCells"] = samples;
            result.Observation["requiresSupportCompletion"] = pendingBase && native != true;
            result.Observation["orientation"] = orientation.ToString();
            if (!result.Valid) result.Observation["reasonCode"] = "unsupported";
            return result;
        }
    }
}
