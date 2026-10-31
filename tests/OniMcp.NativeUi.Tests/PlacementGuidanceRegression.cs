using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class PlacementGuidanceRegression
{
    private static int checks;
    private static readonly string[] ProseKeys = {
        "building_guidance.anchor_source", "building_guidance.orientation",
        "building_guidance.coordinate_contract", "building_guidance.orientation_unavailable"
    };

    internal static void Run()
    {
        var preferred = McpLocalization.LanguageProvider;
        var gameLanguage = McpLocalization.GameLanguageProvider;
        var originalSelect = SelectTool.Instance;
        var originalDetails = DetailsScreen.Instance;
        int? selects = originalSelect == null ? (int?)null : originalSelect.Selects;
        int? refreshes = originalDetails == null ? (int?)null : originalDetails.Refreshes;
        try
        {
            McpLocalization.LanguageProvider = () => "en";
            McpLocalization.GameLanguageProvider = () => "en";
            // This goes through the complete source-linked production model.
            // The exact current prechange source compiles, then returns generic Neutral.
            var first = Project(BuildPlanningTools.PlacementGuidanceForTest(Orientation.R270, true));
            Check((string)first["operationGuide"]?["preview"]?["arguments"]?["orientation"] == "R270",
                "placement_operation_guidance_contract: actual caller retains R270");
            Check((string)first["operationGuide"]?["coordinateContext"]?["anchorSource"] == "placement",
                "placement_anchor_provenance_contract: resolved coordinates are placement context, not request proof");
            var selectedProse = new List<string[]>();
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                string chosen = language;
                McpLocalization.LanguageProvider = () => chosen;
                string[] prose = ProseKeys.Select(key => McpLocalization.Get(key)).ToArray();
                Check(prose.All(value => !string.IsNullOrWhiteSpace(value))
                    && !prose.Where((value, index) => value == ProseKeys[index]).Any(),
                    "Guidance prose resolves in the selected runtime language.");
                selectedProse.Add(prose);
                foreach (Orientation orientation in new[] { Orientation.Neutral, Orientation.R90, Orientation.R180,
                    Orientation.R270, Orientation.FlipH, Orientation.FlipV })
                {
                    var output = Project(BuildPlanningTools.PlacementGuidanceForTest(orientation, true));
                    var guide = (JObject)output["operationGuide"];
                    var args = (JObject)guide["preview"]["arguments"];
                    var context = (JObject)guide["coordinateContext"];
                    Check((string)output["orientation"] == orientation.ToString()
                        && (string)args["orientation"] == orientation.ToString(),
                        "The production caller preserves every supplied orientation.");
                    Check((string)guide["orientationContext"]["source"] == "placement"
                        && (bool)guide["orientationContext"]["fromPlacement"]
                        && (bool)guide["orientationContext"]["known"]
                        && (string)guide["orientationContext"]["value"] == orientation.ToString(),
                        "Reported orientation is separated from a default template.");
                    Check((string)context["anchorRole"] == "lowerLeftCell"
                        && context["anchor"].Values<int>().SequenceEqual(new[] { 2, 2 })
                        && (string)context["anchorSource"] == "placement",
                        "Map anchors preserve resolved placement coordinates without claiming request provenance.");
                    Check((string)context["nativeOriginRole"] == "nativeReferenceCell"
                        && context["nativeOrigin"].Values<int>().SequenceEqual(new[] { 2, 3 })
                        && (int)context["nativeOriginCell"] == Grid.XYToCell(2, 3)
                        && (bool)context["nativeOriginAvailable"] && (int)context["worldId"] == 0,
                        "Native reference data is copied separately without re-rotating it.");
                    Check((string)guide["anchorSource"] == prose[0] && (string)guide["orientation"] == prose[1]
                        && (string)guide["coordinateContract"] == prose[2],
                        "Actual model output uses the selected localized explanations.");
                    VerifySafeTemplate(guide);
                }
                var missingGeometry = Project(BuildPlanningTools.PlacementGuidanceForTest(Orientation.R270, false));
                var missingGuide = missingGeometry["operationGuide"];
                Check(!(bool)missingGuide["coordinateContext"]["geometryAvailable"]
                    && (string)missingGuide["coordinateContext"]["anchorSource"] == "placement"
                    && missingGuide["coordinateContext"]["nativeOrigin"].Type == JTokenType.Null
                    && missingGuide["coordinateContext"]["nativeOriginCell"].Type == JTokenType.Null
                    && !(bool)missingGuide["coordinateContext"]["nativeOriginAvailable"],
                    "Unavailable geometry never copies the caller's legacy origin fallback as observed context.");
                Check(missingGeometry["origin"].Values<int>().SequenceEqual(new[] { 2, 3 })
                    && (string)missingGuide["preview"]["arguments"]["orientation"] == "R270",
                    "Legacy outer fields remain unchanged and a known orientation remains known.");
                foreach (Orientation unknown in new[] { Orientation.NumRotations, (Orientation)999 })
                {
                    var guide = Project(BuildPlanningTools.PlacementGuidanceForTest(unknown, false))["operationGuide"];
                    Check(guide["preview"].Type == JTokenType.Null
                        && !(bool)guide["orientationContext"]["known"]
                        && !(bool)guide["orientationContext"]["previewTemplateAvailable"]
                        && guide["orientationContext"]["value"].Type == JTokenType.Null
                        && (string)guide["orientationContext"]["source"] == "unknown",
                        "Invalid and sentinel orientations must not become a Neutral preview.");
                    Check((string)guide["orientationUnavailableReason"] == prose[3],
                        "An unavailable orientation has a localized reason.");
                }
                var absent = Project(BuildingOperationGuidance.MapPlacement("FixtureBridge", null,
                    2, 2, 0, null, null, null, false));
                Check(absent["preview"].Type == JTokenType.Null
                    && (string)absent["coordinateContext"]["anchorSource"] == "placement"
                    && (string)absent["orientationContext"]["source"] == "unknown",
                    "Missing orientation in a placement context is not a default definition template.");
                var generic = Project(BuildingOperationGuidance.MapPlacement("FixtureBridge"));
                Check((string)generic["preview"]["arguments"]["orientation"] == "Neutral"
                    && (string)generic["orientationContext"]["source"] == "default_template"
                    && !(bool)generic["orientationContext"]["fromPlacement"]
                    && !(bool)generic["orientationContext"]["known"]
                    && (string)generic["coordinateContext"]["anchorSource"] == "unavailable"
                    && generic["coordinateContext"]["anchor"].Type == JTokenType.Null
                    && generic["coordinateContext"]["nativeOrigin"].Type == JTokenType.Null,
                    "One-argument callers retain an explicitly generic Neutral template.");
                VerifySafeTemplate(generic);
                var zero = Project(BuildingOperationGuidance.MapPlacement("FixtureBridge", Orientation.Neutral,
                    0, 0, 0, 0, 0, 0, true));
                Check(zero["coordinateContext"]["anchor"].Values<int>().SequenceEqual(new[] { 0, 0 })
                    && (string)zero["coordinateContext"]["anchorSource"] == "placement"
                    && zero["coordinateContext"]["nativeOrigin"].Values<int>().SequenceEqual(new[] { 0, 0 })
                    && (int)zero["coordinateContext"]["nativeOriginCell"] == 0
                    && (int)zero["coordinateContext"]["worldId"] == 0,
                    "Known zero coordinates/cell/world remain zero, not unknown.");
                var partial = Project(BuildingOperationGuidance.MapPlacement("FixtureBridge", Orientation.R90,
                    2, null, -1, null, null, null, false));
                Check(partial["coordinateContext"]["anchor"].Type == JTokenType.Null
                    && partial["coordinateContext"]["worldId"].Type == JTokenType.Null
                    && (string)partial["coordinateContext"]["anchorSource"] == "unavailable",
                    "Partial anchors and unknown world remain unavailable.");
            }
            for (int index = 0; index < ProseKeys.Length; index++)
                Check(selectedProse.Select(values => values[index]).Distinct().Count() == 3,
                    "Each migrated explanation has three independently selected runtime translations.");
            Check(ReferenceEquals(SelectTool.Instance, originalSelect) && ReferenceEquals(DetailsScreen.Instance, originalDetails)
                && (SelectTool.Instance == null ? (int?)null : SelectTool.Instance.Selects) == selects
                && (DetailsScreen.Instance == null ? (int?)null : DetailsScreen.Instance.Refreshes) == refreshes,
                "Projection must not select or refresh a native UI target.");
        }
        finally
        {
            McpLocalization.LanguageProvider = preferred;
            McpLocalization.GameLanguageProvider = gameLanguage;
        }
        Console.WriteLine("Placement operation guidance: " + checks + " host checks passed (native geometry supplied as fixture facts)");
    }

    private static JObject Project(object value)
    {
        return JObject.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
    }

    private static void VerifySafeTemplate(JToken guide)
    {
        var args = guide["preview"]["arguments"];
        Check((string)guide["preview"]["tool"] == "world_editor" && (string)args["command"] == "edit"
            && (string)args["path"] == "/active/map/viewport.md" && (string)args["content"] == "<filled patchTemplate>"
            && (bool)args["dryRun"] && !(bool)args["confirm"], "Preview remains the nonexecuting virtual map template.");
        Check(!(bool)guide["directBuildAreaAllowed"] && (bool)guide["templateRequiresMapRead"] && !(bool)guide["templateComplete"],
            "Observed coordinates do not make this a filled executable patch.");
        Check(new[] { "x", "y", "x1", "y1", "x2", "y2", "anchorX", "anchorY", "origin", "nativeOrigin" }
            .All(key => args[key] == null), "Coordinates belong only to read-only context, not direct edit arguments.");
        Check((bool)guide["executeChanges"]["confirm"] && !(bool)guide["executeChanges"]["dryRun"],
            "Execution still requires a distinct confirmed request after preview.");
    }

    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        internal static Dictionary<string, object> PlacementGuidanceForTest(Orientation orientation, bool geometryAvailable)
        {
            // Facts are injected; this fixture deliberately performs no ONI
            // placement, rotation, geometry calculation, selection or action.
            return new PlacementDetails
            {
                PrefabId = "FixtureBridge", AnchorX = 2, AnchorY = 2, WorldId = 0,
                Orientation = orientation, Width = 1, Height = 3,
                OriginX = 2, OriginY = 3, OriginCell = Grid.XYToCell(2, 3),
                GeometryAvailable = geometryAvailable, GeometryError = geometryAvailable ? null : "fixture_geometry_missing",
                FillsBounds = true, PlacementPoint = Grid.CellToPosCBC(Grid.XYToCell(2, 3), 0),
                Footprint = Enumerable.Range(0, 3).Select(offset => new FootprintCell {
                    X = 2, Y = 2 + offset, Cell = Grid.XYToCell(2, 2 + offset), WorldId = 0,
                    Valid = geometryAvailable, Visible = geometryAvailable, InWorld = geometryAvailable
                }).ToList()
            }.ToDictionary();
        }
    }
}
