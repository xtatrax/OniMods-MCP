using OniMcp.Support;
using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    // Public routes only. Coordinates remain map-derived, never standalone build calls.
    internal static class BuildingOperationGuidance
    {
        internal const string Placement = "Read /active/map/viewport.md with world_editor command=read format=edit compact=false view=default. Replace an empty lower-left anchor token with PrefabId:5 in one SEARCH/REPLACE block; for a multi-cell building, leave room for its whole footprint or replace the complete footprint. Preview with world_editor command=edit dryRun=true confirm=false using the same compact/view settings, then execute a new edit with dryRun=false confirm=true and re-read the map. Direct building_control planning build_area calls are forbidden.";

        internal static Dictionary<string, object> MapPlacement(string prefabId, Orientation? orientation = null,
            int? anchorX = null, int? anchorY = null, int? worldId = null,
            int? originX = null, int? originY = null, int? originCell = null, bool? geometryAvailable = null)
        {
            bool contextProvided = anchorX.HasValue || anchorY.HasValue || worldId.HasValue
                || originX.HasValue || originY.HasValue || originCell.HasValue || geometryAvailable.HasValue;
            bool genericTemplate = !contextProvided && !orientation.HasValue;
            bool orientationKnown = orientation.HasValue && Enum.IsDefined(typeof(Orientation), orientation.Value)
                && orientation.Value != Orientation.NumRotations;
            bool previewAvailable = genericTemplate || orientationKnown;
            string selectedOrientation = genericTemplate ? Orientation.Neutral.ToString()
                : orientationKnown ? orientation.Value.ToString() : null;
            bool originAvailable = geometryAvailable == true && originX.HasValue && originY.HasValue;
            return new Dictionary<string, object>
            {
                ["route"] = "virtual_map_patch",
                ["directBuildAreaAllowed"] = false,
                ["anchorSource"] = McpLocalization.Get("building_guidance.anchor_source"),
                ["buildToken"] = prefabId + ":5",
                ["materialToken"] = "Append # plus the material glyph from the current map legend; do not assume a material ID is a glyph.",
                ["read"] = new Dictionary<string, object>
                {
                    ["tool"] = "world_editor",
                    ["arguments"] = new Dictionary<string, object> { ["command"] = "read", ["path"] = "/active/map/viewport.md", ["format"] = "edit", ["compact"] = false, ["view"] = "default" }
                },
                ["patchTemplate"] = "<<<<<<< SEARCH\n<exact freshly read map row(s)>\n=======\n<same row(s), replacing only chosen empty token(s) with " + prefabId + ":5>\n>>>>>>> REPLACE",
                ["templateRequiresMapRead"] = true,
                ["templateComplete"] = false,
                ["coordinateContext"] = new Dictionary<string, object>
                {
                    ["anchorRole"] = "lowerLeftCell",
                    ["anchorSource"] = anchorX.HasValue && anchorY.HasValue ? "placement" : "unavailable",
                    ["anchor"] = anchorX.HasValue && anchorY.HasValue ? new[] { anchorX.Value, anchorY.Value } : null,
                    ["nativeOriginRole"] = "nativeReferenceCell",
                    ["nativeOrigin"] = originAvailable ? new[] { originX.Value, originY.Value } : null,
                    ["nativeOriginCell"] = geometryAvailable == true ? originCell : null,
                    ["nativeOriginAvailable"] = originAvailable,
                    ["worldId"] = worldId.HasValue && worldId.Value >= 0 ? worldId : null,
                    ["geometryAvailable"] = geometryAvailable
                },
                ["coordinateContract"] = McpLocalization.Get("building_guidance.coordinate_contract"),
                ["orientationContext"] = new Dictionary<string, object>
                {
                    ["source"] = genericTemplate ? "default_template" : orientationKnown ? "placement" : "unknown",
                    ["value"] = selectedOrientation, ["known"] = orientationKnown,
                    ["previewTemplateAvailable"] = previewAvailable,
                    ["fromPlacement"] = orientationKnown && contextProvided
                },
                ["preview"] = previewAvailable ? new Dictionary<string, object>
                {
                    ["tool"] = "world_editor",
                    ["arguments"] = new Dictionary<string, object> { ["command"] = "edit", ["path"] = "/active/map/viewport.md", ["content"] = "<filled patchTemplate>", ["compact"] = false, ["view"] = "default", ["orientation"] = selectedOrientation, ["dryRun"] = true, ["confirm"] = false }
                } : null,
                ["orientationUnavailableReason"] = previewAvailable ? null
                    : McpLocalization.Get("building_guidance.orientation_unavailable"),
                ["executeChanges"] = new Dictionary<string, object> { ["dryRun"] = false, ["confirm"] = true },
                ["orientation"] = McpLocalization.Get("building_guidance.orientation"),
                ["verify"] = "Re-read /active/map/viewport.md to verify a queued blueprint; geometry inspect reads completed buildings only. After construction completes, use building_control domain=geometry action=inspect."
            };
        }

        internal static Dictionary<string, object> ConfigurationRead(string capability, bool stateIncluded,
            string prefabId, int worldId, int targetId)
        {
            string surface = capability == "activation_range" ? "activation"
                : capability == "direction" || capability == "few_option" || capability == "broadcast_receiver" || capability == "radbolt_direction" ? "option" : null;
            string filterKind = capability == "filterable" ? "single"
                : capability == "tree_filter" ? "tree" : capability == "flat_filter" ? "flat" : null;
            var args = new Dictionary<string, object>
            {
                ["domain"] = filterKind != null ? "filter" : surface == null ? "config" : "side_surface", ["action"] = "list",
                ["query"] = prefabId, ["worldId"] = worldId, ["limit"] = 500
            };
            if (filterKind != null) args["kind"] = filterKind;
            if (surface != null) args["surface"] = surface;
            if (surface == "option") args["kind"] = capability;
            return new Dictionary<string, object>
            {
                ["capability"] = capability, ["stateIncluded"] = stateIncluded,
                ["expectedTargetId"] = targetId,
                ["read"] = surface == null && filterKind == null && !stateIncluded ? null : new Dictionary<string, object> { ["tool"] = "building_control", ["arguments"] = args },
                ["note"] = surface != null || filterKind != null ? "Follow read and match expectedTargetId in returned targets; capability alone is not the current setting."
                    : stateIncluded ? "Current state is included in this config record."
                    : "Capability detected, but current state is not included by config list; no detailed reader is promised by this record."
            };
        }
    }
}
