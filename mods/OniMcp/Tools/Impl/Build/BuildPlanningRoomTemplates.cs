using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        public static McpTool RoomTemplatePlan()
        {
            return new McpTool
            {
                Name = "build_room_template",
                Group = "buildings",
                Mode = "execute",
                Risk = "dangerous",
                Hidden = true,
                Description = "Compatibility entrypoint: use building_control domain=planning action=room_template. kind=starter/toilet_lab is the one-call starter setup: dig interiors and build room shells, doors, outhouse, wash basin, and research station. execute=true confirm=true runs the full plan.",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["kind"] = new McpToolParameter { Type = "string", Description = "Template kind: toilet/restroom/lab/research/starter/toilet_lab.", Required = false },
                    ["template"] = new McpToolParameter { Type = "string", Description = "Alias for kind.", Required = false },
                    ["plan"] = new McpToolParameter { Type = "string", Description = "Natural template phrase, e.g. 完整厕所, 实验室, 厕所加实验室, 厕所实验室, 卫生间和研究站.", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "Preferred room candidate area handle.", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "Search anchor when areaId/x/y are omitted, e.g. printing pod or oxygen pocket.", Required = false },
                    ["target"] = new McpToolParameter { Type = "string", Description = "Alias for query.", Required = false },
                    ["search"] = new McpToolParameter { Type = "string", Description = "Alias for query.", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "Room lower-left X. Prefer areaId when available.", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "Room lower-left Y. Prefer areaId when available.", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "Room candidate rect lower X.", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "Room candidate rect lower Y.", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "Room candidate rect upper X.", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "Room candidate rect upper Y.", Required = false },
                    ["width"] = new McpToolParameter { Type = "integer", Description = "Single room width. Default 8; starter defaults to two rooms.", Required = false },
                    ["height"] = new McpToolParameter { Type = "integer", Description = "Room height. Default 4; minimum 4.", Required = false },
                    ["material"] = new McpToolParameter { Type = "string", Description = "Build material. Default auto.", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "Dig/build priority 1..9. Default 7.", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "Mark generated work top priority when supported.", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "Target world id. Defaults active world or area world.", Required = false },
                    ["execute"] = new McpToolParameter { Type = "boolean", Description = "When true, execute generated dig/build calls in this one tool call.", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "Required with execute=true unless dryRun=true.", Required = false },
                    ["autoLayout"] = new McpToolParameter { Type = "boolean", Description = "When true and no area/x/query is supplied, select the best layout candidate automatically. Starter templates auto-layout by default.", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "Preview without writing orders/blueprints.", Required = false }
                },
                Handler = args => PreviewValidationObservation.Rejected(HandleRoomTemplate(args), "room_template",
                    args != null && ToolUtil.GetBool(args, "execute", false) && !IsDryRun(args))
            };
        }

        private static CallToolResult HandleRoomTemplate(JObject args)
        {
            args = args ?? new JObject();
            string kind = ResolveRoomTemplateKind(args);
            if (string.IsNullOrEmpty(kind))
                return CallToolResult.Error("kind/template/plan must mention toilet/restroom/lab/research/starter/toilet_lab.");

            string anchorError;
            RoomTemplateAnchor anchor = ResolveRoomTemplateAnchor(args, kind, out anchorError);
            if (anchor == null)
                return CallToolResult.Error(anchorError);

            string material = string.IsNullOrWhiteSpace(args["material"]?.ToString()) ? "auto" : args["material"].ToString();
            int priority = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "priority") ?? 7, 9));
            bool topPriority = ToolUtil.GetBool(args, "topPriority", false);
            bool execute = ToolUtil.GetBool(args, "execute", false);
            bool dryRun = IsDryRun(args);
            if (execute && !dryRun && !ToolUtil.GetBool(args, "confirm", false))
                return CallToolResult.Error("confirm=true required with execute=true dryRun=false.");

            List<RoomTemplateCall> calls = BuildRoomTemplateCalls(kind, anchor, material, priority, topPriority, execute, dryRun);
            var response = new JObject
            {
                ["ok"] = true,
                ["template"] = kind,
                ["anchor"] = new JObject { ["x"] = anchor.X, ["y"] = anchor.Y },
                ["areaId"] = anchor.AreaId,
                ["size"] = new JObject { ["width"] = anchor.Width, ["height"] = anchor.Height },
                ["rooms"] = BuildRoomTemplateRoomSummary(kind, anchor),
                ["priorityAction"] = new JObject { ["priority"] = priority, ["topPriority"] = topPriority },
                ["executionPlan"] = BuildRoomTemplateExecutionPlan(kind, anchor, priority),
                ["verificationPlan"] = BuildRoomTemplateVerificationPlan(kind, anchor, priority),
                ["tokenHint"] = "For one-call starter setup call kind=starter execute=true confirm=true; if no anchor is supplied the tool auto-selects a layout. Read rooms, results[].summary/diagnostic, then nextActions.",
                ["nextActions"] = BuildRoomTemplateNextActions(kind, anchor, priority),
                ["calls"] = new JArray(calls.Select(c => c.Call))
            };

            if (execute)
            {
                var results = ExecuteRoomTemplateCalls(calls);
                int succeeded = results.Count(row => row["ok"]?.Value<bool>() == true);
                bool failed = results.Any(row => row["ok"]?.Value<bool>() == false);
                bool childPartial = results.Any(row => row["partial"]?.Value<bool>() == true);
                int unknown = results.Count(row => row["resultState"]?.Value<string>() == "unknown");
                response["results"] = results;
                response["requestedCalls"] = calls.Count;
                response["attemptedCalls"] = results.Count;
                response["succeededCalls"] = succeeded;
                response["failedCalls"] = results.Count - succeeded - unknown;
                response["unknownCalls"] = unknown;
                response["remainingCalls"] = calls.Count - results.Count;
                response["dispatchComplete"] = !failed && !childPartial && results.Count == calls.Count;
                response["ok"] = response["dispatchComplete"].DeepClone();
                response["dispatchInterrupted"] = !response["dispatchComplete"].Value<bool>();
                response["partial"] = childPartial || unknown > 0 || failed && succeeded > 0;
                if (unknown > 0)
                {
                    response["assessmentState"] = "unknown";
                    response["reasonCode"] = "child_result_unobserved";
                    response["changed"] = JValue.CreateNull();
                    response["possiblePartial"] = !dryRun;
                }
            }
            else
                response["next"] = "Re-run with execute=true confirm=true, or send calls through server_control batch.";
            bool ok = response["ok"].Value<bool>();
            var result = ok ? CallToolResult.Text(JsonConvert.SerializeObject(response, McpJsonUtil.Settings))
                : CallToolResult.Error(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));
            string scope = !execute ? "template_generation" : dryRun ? "composite_preflight" : "composite_execution_observation";
            bool childCoverageComplete = !execute || ((JArray)response["results"]).All(row =>
                (row["validation"] as JObject)?["coverage"]?.Value<string>() == "complete");
            return new PreviewValidationObservation("room_template", scope, ok && childCoverageComplete ? "complete" : "partial",
                execute && !dryRun, false, "ok", "partial", "dispatchComplete", "requestedCalls", "attemptedCalls",
                "succeededCalls", "failedCalls", "unknownCalls", "remainingCalls", "assessmentState", "reasonCode", "dispatchInterrupted", "changed", "possiblePartial").Attach(result);
        }

        private static RoomTemplateAnchor ResolveRoomTemplateAnchor(JObject args, string kind, out string error)
        {
            error = null;
            int worldId = ToolUtil.ResolveWorldId(args);
            int x;
            int y;

            if (TryGetInt(args, "x", out x) && TryGetInt(args, "y", out y))
                return BuildRoomTemplateAnchor(args, kind, x, y, worldId, null);

            if (HasRoomRect(args))
            {
                Dictionary<string, int> rect = WorldEditor.ResolveRect(args);
                return BuildRoomTemplateAnchor(args, kind, rect["x1"], rect["y1"], worldId, rect);
            }

            if (ToolUtil.TryResolveSearchCell(args, out x, out y, out error))
                return BuildRoomTemplateAnchor(args, kind, x, y, worldId, null);

            RoomTemplateAnchor autoAnchor = TryAutoRoomTemplateAnchor(args, kind, worldId, out error);
            if (autoAnchor != null)
                return autoAnchor;

            error = "room_template needs areaId, x/y, x1/y1/x2/y2, query/target/search anchor, or kind=starter auto layout. For one-call starter setup use kind=starter execute=true confirm=true.";
            return null;
        }

        private static RoomTemplateAnchor BuildRoomTemplateAnchor(JObject args, string kind, int x, int y, int worldId, Dictionary<string, int> rect)
        {
            int inferredWidth = rect == null ? DefaultRoomTemplateWidth(kind) : rect["x2"] - rect["x1"] + 1;
            int inferredHeight = rect == null ? 4 : rect["y2"] - rect["y1"] + 1;
            return new RoomTemplateAnchor
            {
                X = x,
                Y = y,
                Width = Math.Max(DefaultRoomTemplateWidth(kind), ToolUtil.GetInt(args, "width") ?? inferredWidth),
                Height = Math.Max(4, ToolUtil.GetInt(args, "height") ?? inferredHeight),
                WorldId = worldId,
                AreaId = args["areaId"]?.ToString()
            };
        }

        private static bool HasRoomRect(JObject args)
        {
            if (!string.IsNullOrWhiteSpace(args["areaId"]?.ToString()))
                return true;
            return args["x1"] != null && args["y1"] != null;
        }

        private static List<RoomTemplateCall> BuildRoomTemplateCalls(string kind, RoomTemplateAnchor anchor, string material, int priority, bool topPriority, bool execute, bool dryRun)
        {
            if (kind == "starter")
                return BuildStarterTemplateCalls(anchor, material, priority, topPriority, execute, dryRun);

            return BuildSingleRoomCalls(kind, anchor, material, priority, topPriority, execute, dryRun);
        }

        private static List<RoomTemplateCall> BuildStarterTemplateCalls(RoomTemplateAnchor anchor, string material, int priority, bool topPriority, bool execute, bool dryRun)
        {
            int roomWidth = Math.Max(7, (anchor.Width - 1) / 2);
            var toilet = anchor.With(anchor.X, anchor.Y, roomWidth, anchor.Height);
            var lab = anchor.With(anchor.X + roomWidth + 1, anchor.Y, roomWidth, anchor.Height);
            var calls = new List<RoomTemplateCall>();
            calls.AddRange(BuildSingleRoomCalls("toilet", toilet, material, priority, topPriority, execute, dryRun, true, false, true));
            calls.AddRange(BuildSingleRoomCalls("lab", lab, material, priority, topPriority, execute, dryRun, false, true));
 calls.Add(BuildCall("Tile", material, VerticalWallAnchors(anchor.X + roomWidth, anchor.Y, anchor.Height), priority, topPriority, anchor.WorldId, execute, dryRun));
            return calls;
        }

        private static List<RoomTemplateCall> BuildSingleRoomCalls(string kind, RoomTemplateAnchor anchor, string material, int priority, bool topPriority, bool execute, bool dryRun, bool doorOnLeft = false, bool omitLeftWall = false, bool omitRightWall = false)
        {
            int doorX = doorOnLeft ? anchor.X : anchor.X + anchor.Width - 1;
            var calls = new List<RoomTemplateCall>
            {
                OrdersCall("dig", anchor.X + 1, anchor.Y + 1, anchor.X + anchor.Width - 2, anchor.Y + anchor.Height - 2, priority, topPriority, anchor.WorldId, execute, dryRun),
                BuildCall("Tile", material, RoomShellAnchors(anchor, doorOnLeft, omitLeftWall, omitRightWall), priority, topPriority, anchor.WorldId, execute, dryRun),
                BuildCall("Door", material, OneAnchor(doorX, anchor.Y + 1), priority, topPriority, anchor.WorldId, execute, dryRun)
            };

            if (kind == "toilet")
            {
                calls.Add(BuildCall("Outhouse", material, OneAnchor(anchor.X + 2, anchor.Y + 1), priority, topPriority, anchor.WorldId, execute, dryRun));
                calls.Add(BuildCall("WashBasin", material, OneAnchor(anchor.X + Math.Max(4, anchor.Width - 3), anchor.Y + 1), priority, topPriority, anchor.WorldId, execute, dryRun));
            }
            else
            {
                calls.Add(BuildCall("ResearchCenter", material, OneAnchor(anchor.X + 2, anchor.Y + 1), priority, topPriority, anchor.WorldId, execute, dryRun));
            }

            return calls;
        }

        private static RoomTemplateCall OrdersCall(string action, int x1, int y1, int x2, int y2, int priority, bool topPriority, int worldId, bool execute, bool dryRun)
        {
            var args = new JObject
            {
                ["domain"] = "area",
                ["action"] = action,
                ["x1"] = x1,
                ["y1"] = y1,
                ["x2"] = x2,
                ["y2"] = y2,
                ["priority"] = priority,
                ["topPriority"] = topPriority,
                ["worldId"] = worldId,
                ["dryRun"] = dryRun
            };
            if (execute && !dryRun)
                args["confirm"] = true;
            return new RoomTemplateCall("orders_control", args);
        }

        private static RoomTemplateCall BuildCall(string prefabId, string material, JArray anchors, int priority, bool topPriority, int worldId, bool execute, bool dryRun)
        {
            var args = new JObject
            {
                ["domain"] = "planning",
                ["action"] = "build_area",
                ["prefabId"] = prefabId,
                ["material"] = material,
                ["anchors"] = anchors,
                ["priority"] = priority,
                ["topPriority"] = topPriority,
                ["worldId"] = worldId,
                ["dryRun"] = dryRun,
                ["allowPartial"] = true,
                ["autoDig"] = true,
                ["maxCommitAnchors"] = 32
            };
            if (execute && !dryRun)
                args["confirm"] = true;
            return new RoomTemplateCall("building_control", args);
        }

        private static JArray RoomShellAnchors(RoomTemplateAnchor anchor, bool doorOnLeft = false, bool omitLeftWall = false, bool omitRightWall = false)
        {
            var anchors = new JArray();
            int left = anchor.X;
            int right = anchor.X + anchor.Width - 1;
            int bottom = anchor.Y;
            int top = anchor.Y + anchor.Height - 1;

            for (int x = left; x <= right; x++)
            {
                anchors.Add(Anchor(x, bottom));
                anchors.Add(Anchor(x, top));
            }

            for (int y = bottom + 1; y < top; y++)
            {
                if (!omitLeftWall && (!doorOnLeft || (y != bottom + 1 && y != bottom + 2)))
                    anchors.Add(Anchor(left, y));
                if (!omitRightWall && (doorOnLeft || (y != bottom + 1 && y != bottom + 2)))
                    anchors.Add(Anchor(right, y));
            }

            return anchors;
        }

 private static JArray OneAnchor(int x, int y)
 {
 return new JArray(Anchor(x, y));
 }

 private static JArray VerticalWallAnchors(int x, int y, int height)
 {
 var anchors = new JArray();
 for (int offset = 0; offset < height; offset++)
 anchors.Add(Anchor(x, y + offset));
 return anchors;
 }

 private static JObject Anchor(int x, int y)
        {
            return new JObject { ["x"] = x, ["y"] = y };
        }

        private static JArray BuildRoomTemplateRoomSummary(string kind, RoomTemplateAnchor anchor)
        {
            if (kind != "starter")
                return new JArray(RoomSummary(kind, anchor));

            int roomWidth = Math.Max(7, (anchor.Width - 1) / 2);
            return new JArray
            {
                RoomSummary("toilet", anchor.With(anchor.X, anchor.Y, roomWidth, anchor.Height)),
                RoomSummary("lab", anchor.With(anchor.X + roomWidth + 1, anchor.Y, roomWidth, anchor.Height))
            };
        }

        private static JObject RoomSummary(string kind, RoomTemplateAnchor anchor)
        {
            var core = kind == "toilet"
                ? new JArray("Outhouse", "WashBasin")
                : new JArray("ResearchCenter");
            var coreCells = kind == "toilet"
                ? new JArray(
                    CoreCell("Outhouse", anchor.X + 2, anchor.Y + 1),
                    CoreCell("WashBasin", anchor.X + Math.Max(4, anchor.Width - 3), anchor.Y + 1))
                : new JArray(CoreCell("ResearchCenter", anchor.X + 2, anchor.Y + 1));
            return new JObject
            {
                ["kind"] = kind,
                ["rect"] = new JObject
                {
                    ["x1"] = anchor.X,
                    ["y1"] = anchor.Y,
                    ["x2"] = anchor.X + anchor.Width - 1,
                    ["y2"] = anchor.Y + anchor.Height - 1
                },
                ["interiorDig"] = new JObject
                {
                    ["x1"] = anchor.X + 1,
                    ["y1"] = anchor.Y + 1,
                    ["x2"] = anchor.X + anchor.Width - 2,
                    ["y2"] = anchor.Y + anchor.Height - 2
                },
                ["coreBuildings"] = core,
                ["coreCells"] = coreCells
            };
        }

        private static JObject CoreCell(string prefabId, int x, int y)
        {
            return new JObject
            {
                ["prefabId"] = prefabId,
                ["x"] = x,
                ["y"] = y,
                ["cellPath"] = "/active/map/cell_" + x + "_" + y + ".md"
            };
        }

        private static JArray ExecuteRoomTemplateCalls(List<RoomTemplateCall> calls)
        {
            var results = new JArray();
            foreach (RoomTemplateCall call in calls)
            {
                CallToolResult result;
                try { result = OniToolRegistry.CallTool(call.Tool, call.Args); }
                catch (Exception ex) { result = CallToolResult.Error(ex.Message); }
                var resultContent = result?.Content?.FirstOrDefault(content => content != null && content.Type == "text"
                    && !content.IsOwnedPrintingPodPolicy
                    && (content.Meta == null || !content.Meta.ContainsKey(ToolContent.ContentKindMetadataKey)
                        || content.Meta[ToolContent.ContentKindMetadataKey] as string == ToolContent.ToolResultKind));
                string text = resultContent?.Text ?? string.Empty;
                JObject payload = null;
                try { payload = JObject.Parse(text); } catch { }
                bool failed = result == null || result.IsError;
                foreach (string key in new[] { "ok", "success", "valid", "actionable" })
                    if (payload?[key]?.Type == JTokenType.Boolean && !payload[key].Value<bool>()) failed = true;
                foreach (string key in new[] { "failed", "hardFailed" })
                    if (RoomTemplateCount(payload, key) > 0) failed = true;
                bool partial = payload?["partial"]?.Type == JTokenType.Boolean && payload["partial"].Value<bool>()
                    || RoomTemplateCount(payload, "deferred") > 0 || RoomTemplateCount(payload, "remainingCells") > 0
                    || RoomTemplateCount(payload, "pendingBuildAfterDig") > 0
                    || payload?["remainingAnchors"] is JArray remainingAnchors && remainingAnchors.Count > 0
                    || payload?["allRequestedSatisfied"]?.Type == JTokenType.Boolean && !payload["allRequestedSatisfied"].Value<bool>()
                    || payload?["throttled"]?.Type == JTokenType.Boolean && payload["throttled"].Value<bool>();
                bool unknown = !failed && !partial && !RoomTemplateSuccessObserved(call, payload);
                var row = new JObject
                {
                    ["tool"] = call.Tool,
                    ["action"] = call.Args["action"]?.ToString(),
                    ["ok"] = !failed && !partial && !unknown,
                    ["resultState"] = unknown ? "unknown" : failed ? "refused" : partial ? "partial" : "accepted",
                    ["partial"] = partial,
                    ["summary"] = SummarizeRoomTemplateResult(text),
                    ["diagnostic"] = DiagnoseRoomTemplateResult(text, failed || partial),
                    ["error"] = failed || partial || unknown ? TrimRoomTemplateText(text, 1200) : null
                };
                if (payload?["validation"] != null) row["validation"] = payload["validation"].DeepClone();
                else
                {
                    var childValidation = resultContent?.Meta != null
                        && resultContent.Meta.ContainsKey(PreviewValidationObservation.MetadataKey)
                        ? resultContent.Meta[PreviewValidationObservation.MetadataKey] as JToken : null;
                    if (childValidation != null) row["validation"] = childValidation.DeepClone();
                }
                results.Add(row);
                if (failed || partial || unknown) break;
            }
            return results;
        }

        private static bool RoomTemplateSuccessObserved(RoomTemplateCall call, JObject payload)
        {
            if (payload == null) return false;
            foreach (string key in new[] { "ok", "success", "valid", "actionable", "allRequestedSatisfied" })
                if (payload[key]?.Type == JTokenType.Boolean && payload[key].Value<bool>()) return true;
            // Dig has a count-based acknowledgement; zero is a valid observed result.
            return call.Tool == "orders_control" && call.Args["domain"]?.Value<string>() == "area"
                && call.Args["action"]?.Value<string>() == "dig"
                && payload["dryRun"]?.Type == JTokenType.Boolean
                && payload["marked"]?.Type == JTokenType.Integer && payload["marked"].Value<long>() >= 0
                && payload["worldId"]?.Type == JTokenType.Integer
                && payload["rect"] is JObject && payload["execution"] is JObject;
        }

        private static int RoomTemplateCount(JObject payload, string key)
        {
            JToken value = payload?[key];
            return value?.Type == JTokenType.Integer && value.Value<long>() > 0
                ? (int)Math.Min(value.Value<long>(), int.MaxValue) : 0;
        }

        private static string ResolveRoomTemplateKind(JObject args)
        {
            string text = ((args["kind"] ?? args["template"] ?? args["plan"])?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            bool wantsToilet = text.Contains("toilet") || text.Contains("restroom") || text.Contains("latrine") || text.Contains("厕所") || text.Contains("卫生间") || text.Contains("洗手");
            bool wantsLab = text.Contains("lab") || text.Contains("research") || text.Contains("实验") || text.Contains("研究");
            if (text.Contains("starter") || text.Contains("toilet_lab") || text.Contains("toilet+lab") || text.Contains("toilet lab") || text.Contains("厕所加实验室") || text.Contains("厕所和实验室") || text.Contains("厕所实验室") || (wantsToilet && wantsLab))
                return "starter";
            if (wantsToilet)
                return "toilet";
            if (wantsLab)
                return "lab";
            return string.Empty;
        }

        private static int DefaultRoomTemplateWidth(string kind)
        {
            return kind == "starter" ? 15 : 8;
        }

        private static bool TryGetInt(JObject args, string key, out int value)
        {
            value = 0;
            return args[key] != null && int.TryParse(args[key].ToString(), out value);
        }

        private static string SummarizeRoomTemplateResult(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "ok";

            try
            {
                var obj = JObject.Parse(text);
                string[] keys = { "planned", "marked", "executedCells", "remainingCells", "failed", "prefabId", "dryRun" };
                var parts = keys.Where(k => obj[k] != null).Select(k => k + "=" + obj[k]).ToList();
                return parts.Count == 0 ? "ok" : string.Join(", ", parts.ToArray());
            }
            catch
            {
                return TrimRoomTemplateText(text, 400);
            }
        }

        private static string TrimRoomTemplateText(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text ?? string.Empty;
            return text.Substring(0, max) + "...";
        }

        private sealed class RoomTemplateAnchor
        {
            public int X;
            public int Y;
            public int Width;
            public int Height;
            public int WorldId;
            public string AreaId;

            public RoomTemplateAnchor With(int x, int y, int width, int height)
            {
                return new RoomTemplateAnchor { X = x, Y = y, Width = width, Height = height, WorldId = WorldId, AreaId = AreaId };
            }
        }

        private sealed class RoomTemplateCall
        {
            public readonly string Tool;
            public readonly JObject Args;

            public JObject Call => new JObject { ["tool"] = Tool, ["arguments"] = (JObject)Args.DeepClone() };

            public RoomTemplateCall(string tool, JObject args)
            {
                Tool = tool;
                Args = args;
            }
        }
    }
}
