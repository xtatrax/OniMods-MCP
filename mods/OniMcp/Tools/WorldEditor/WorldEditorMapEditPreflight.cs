using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static bool TryCompileMapEdit(JObject args, string search, string replacement, out List<MapEditCell> changes, out string error)
        {
            changes = null;
            if (!TryReadVirtualFileTextForMapEdit(args, args["sourcePath"]?.ToString(), search, out string current, out string readError))
            {
                error = "Cannot read current map before applying map edit: " + readError;
                return false;
            }
            if (!TryParseMapEditChangesFromPatchCoordinates(current, search, replacement, out changes, out error))
                changes = ParseMapEditChanges(current, search, replacement, out error);
            if (changes == null)
                return false;
            if (changes.Count == 0)
            {
                error = "Map edit changed no grid cells; the replacement already matches current state.";
                return false;
            }
            return ValidateCompiledMapChanges(args, changes, out error);
        }

        private static bool ValidateCompiledMapChanges(JObject args, List<MapEditCell> changes, out string error)
        {
            if (!ValidateMapEditInput(args, changes, out error)) return false;
            foreach (var group in changes.GroupBy(ChangeKind))
            {
                if (group.Key == "wire")
                {
                    error = "Connection glyph edits are refused because auto_connect may touch cells outside the source snapshot. Use an explicit infrastructure plan or /active/ops/build.md auto_connect command.";
                    return false;
                }
                if (group.Key == "unsupported")
                {
                    error = UnsupportedMapEdit(group).Content?.FirstOrDefault()?.Text ?? "unsupported map edit";
                    return false;
                }
                if (group.Key != "build")
                    continue;

                foreach (var tokenGroup in group.GroupBy(cell => cell.ToToken))
                {
                    if (!ParseBuildToken(tokenGroup.Key, out char symbol, out int? priority, out _)
                        || !priority.HasValue)
                    {
                        error = "Build token `" + tokenGroup.Key + "` must include a valid :priority.";
                        return false;
                    }
                    if (!TryResolveBuildPrefabFromToken(tokenGroup.Key, symbol, out string prefabId))
                    {
                        error = "Cannot map building token `" + tokenGroup.Key + "` to a buildable prefab.";
                        return false;
                    }
                    if (!TryBuildAnchorsForPrefabFootprints(args, prefabId, tokenGroup, out JArray _, out error))
                        return false;
                }
            }
            return true;
        }

        private static bool ValidateExplicitMapChangesAgainstSource(JObject args, string path, List<MapEditCell> changes, out string error)
        {
            if (!TryReadVirtualFileText(args, path, out string current, out string readError))
            {
                error = "Cannot read current map before applying explicit edit: " + readError;
                return false;
            }
            var rows = ParseMapRows(current, out int[] hundreds, out int[] tens, out int[] ones, out error);
            if (rows == null || !TryBuildAxisCoordinates(hundreds, tens, ones, out int[] coordinates, out error))
                return false;
            var offsets = coordinates.Select((x, index) => new { x, index }).ToDictionary(item => item.x, item => item.index);

            for (int i = changes.Count - 1; i >= 0; i--)
            {
                MapEditCell change = changes[i];
                if (!rows.TryGetValue(change.Y, out string[] row)
                    || !offsets.TryGetValue(change.X, out int offset)
                    || offset >= row.Length)
                {
                    error = "Explicit edit cell (" + change.X + "," + change.Y + ") is outside the source viewport/layer.";
                    return false;
                }
                string actual = row[offset];
                if (!string.IsNullOrWhiteSpace(change.FromToken) && !SearchTokenMatches(actual, change.FromToken))
                {
                    error = "Stale explicit edit at (" + change.X + "," + change.Y + "): expected `" + change.FromToken + "`, current `" + actual + "`.";
                    return false;
                }
                change.FromToken = actual;
                if (MapTokensEquivalent(actual, change.ToToken))
                    changes.RemoveAt(i);
            }
            if (changes.Count == 0)
            {
                error = "Explicit map edit contains no differences from current state.";
                return false;
            }
            return true;
        }

        // Inspection only: never call placement, auto-connect or order handlers here.
        private static JObject MapEditCellEvidence(MapEditCell change)
        {
            string kind = ChangeKind(change);
            var result = new JObject
            {
                ["x"] = change.X, ["y"] = change.Y,
                ["kind"] = kind,
                ["fromToken"] = change.FromToken, ["toToken"] = change.ToToken
            };
            if (kind != "build") return result;

            if (!ParseBuildToken(change.ToToken, out char symbol, out int? priority, out _)
                || !TryResolveBuildPrefabFromToken(change.ToToken, symbol, out string prefabId))
                return result;
            var def = Assets.GetBuildingDef(prefabId);
            result["requestedPrefabId"] = prefabId;
            result["definitionObjectLayer"] = def == null ? null : def.ObjectLayer.ToString();
            int cell = Grid.XYToCell(change.X, change.Y);
            if (!Grid.IsValidCell(cell)) return result;
            result["solidTerrain"] = Grid.Solid[cell];
            var layers = new JArray();
            for (int layer = 0; layer < (int)ObjectLayer.NumLayers; layer++)
            {
                var go = Grid.Objects[cell, layer];
                if (go == null) continue;
                var identity = go.GetComponent<KPrefabID>();
                layers.Add(new JObject
                {
                    ["layer"] = ((ObjectLayer)layer).ToString(),
                    ["prefabId"] = identity == null ? null : identity.PrefabTag.Name,
                    ["instanceId"] = identity == null ? JValue.CreateNull() : new JValue(identity.InstanceID),
                    ["isDefinitionObjectLayer"] = def != null && layer == (int)def.ObjectLayer
                });
            }
            result["observedGridLayers"] = layers;
            result["duplicantsAtCell"] = new JArray(Components.LiveMinionIdentities.Items
                .Where(d => d != null && Grid.PosToCell(d.gameObject) == cell)
                .Select(d => d.GetComponent<KPrefabID>())
                .Where(k => k != null).Select(k => k.InstanceID));
            return result;
        }

        private static CallToolResult PreflightMapEdit(JObject args, string search, string replacement)
        {
            return EditorStampChildCalls(PreviewValidationObservation.Rejected(
                PreflightMapEditWithObservation(args, search, replacement), "map_edit", EditorGameplayRequested(args)), 0);
        }

        private static CallToolResult PreflightMapEditWithObservation(JObject args, string search, string replacement)
        {
            if (!TryCompileMapEdit(args, search, replacement, out List<MapEditCell> changes, out string error))
                return CallToolResult.Error(error);
            var payload = new JObject
            {
                ["ok"] = true,
                ["phase"] = "preflight",
                ["sourcePath"] = args["sourcePath"]?.ToString(),
                ["validationScope"] = "syntax_and_footprint_shape",
                ["placementValidated"] = false,
                ["note"] = "Layer observations describe current objects, not permission to replace them. Placement, delivery, support, auto-dig and network effects still require execution-path validation.",
                ["changedCells"] = changes.Count,
                ["cellDetails"] = new JArray(changes.Take(64).Select(MapEditCellEvidence)),
                ["cellDetailsTruncated"] = changes.Count > 64,
                ["kinds"] = new JArray(changes.GroupBy(ChangeKind).Select(group => new JObject
                {
                    ["kind"] = group.Key,
                    ["cells"] = group.Count()
                }))
            };
            return EditorObservePayload(payload, "map_edit", "syntax_and_footprint_shape",
                "partial", 0, EditorGameplayRequested(args));
        }
    }
}
