using System;
using System.Collections.Generic;
using System.Reflection;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
{
        private static readonly MethodInfo ConsumerStopChoreMethod = typeof(ChoreConsumer).GetMethod("StopChore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly MethodInfo ChoreCancelWithStringMethod = typeof(Chore).GetMethod("Cancel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);

        private static readonly MethodInfo ChoreCancelNoArgsMethod = typeof(Chore).GetMethod("Cancel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        private static readonly MethodInfo BrainCancelChoreMethod = typeof(MinionBrain).GetMethod("CancelChore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);

        private static readonly MethodInfo BrainCancelFetchesMethod = typeof(MinionBrain).GetMethod("CancelFetches", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);

        private static readonly MethodInfo MoveMonitorCancelMethod = typeof(MoveToLocationMonitor.Instance).GetMethod("CancelChore", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        public static McpTool ControlDupes()
        {
            return new McpTool
            {
                Name = "dupes_control",
                Group = "dupes",
                Mode = "execute",
                Risk = "medium",
                Aliases = new List<string> { "duplicants_control", "dupe_control" },
                Tags = new List<string> { "dupes", "duplicants", "priority", "skills", "hats", "assignable", "commands", "side-screen" },
                Description = "Unified duplicant compatibility entrypoint. domain=info/priority/hat/command/side_screen/skill/assignable; use action plus name/dupeName/query/target/search/id to locate and execute targets. Coordinate input is not accepted here; use coordinate_control for exact coordinate operations. action is forwarded to the matching legacy control.",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["domain"] = new McpToolParameter { Type = "string", Description = "複製人間のサブシステム: info、priority、hat、command、side_screen、skill、assignable", Required = true, EnumValues = new List<string> { "info", "priority", "hat", "command", "side_screen", "skill", "assignable" } },
                    ["action"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.entry_action"), Required = true },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間、対象オブジェクト、または割り当て可能なオブジェクトの InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間または対象オブジェクトの名前", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID。一部の書き込み操作で使用", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。一部の書き込み操作で使用", Required = false },
                    ["targetId"] = new McpToolParameter { Type = "integer", Description = "対象オブジェクトの InstanceID", Required = false },
                    ["targetName"] = new McpToolParameter { Type = "string", Description = "対象オブジェクトの名前", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。読み取りの絞り込みや座標検索で使用", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "読み取り操作でのキーワードによる絞り込み", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す、または処理する最大件数", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "domain=priority の書き込み操作の優先度", Required = false },
                    ["skillId"] = new McpToolParameter { Type = "string", Description = "domain=skill action=learn のスキル ID", Required = false },
                    ["includeUi"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.include_ui"), Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.force"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.dry_run"), Required = false },
                    ["hatId"] = new McpToolParameter { Type = "string", Description = "domain=hat action=set の帽子 ID", Required = false },
                    ["newName"] = new McpToolParameter { Type = "string", Description = "domain=command action=rename の新しい名前", Required = false },
                    ["style"] = new McpToolParameter { Type = "string", Description = "domain=command action=auto_rename の命名形式", Required = false },
                    ["apply"] = new McpToolParameter { Type = "boolean", Description = "domain=command action=auto_rename を実際に適用するか", Required = false },
                    ["commandAction"] = new McpToolParameter { Type = "string", Description = "domain=command action=force_action の内部操作", Required = false },
                    ["slotId"] = new McpToolParameter { Type = "string", Description = "domain=assignable action=set_slot のスロット ID", Required = false },
                    ["itemId"] = new McpToolParameter { Type = "integer", Description = "domain=assignable action=set_slot のアイテムの InstanceID", Required = false },
                    ["includeDetails"] = new McpToolParameter { Type = "boolean", Description = "一部の info/side_screen の読み取りに詳細情報を含めるか", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "内部の書き込み、移動、強制操作、一括操作は、元の control の要件に従って確認する", Required = false }
                },
                Handler = args =>
                {
                    string domain = (args["domain"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    switch (domain)
                    {
                        case "info":
                        case "read":
                            return ControlDupeInfo().Handler(args);
                        case "priority":
                        case "priorities":
                            return ControlPersonalPriority().Handler(args);
                        case "hat":
                        case "hats":
                            return ControlHat().Handler(args);
                        case "command":
                        case "commands":
                            return ControlDupeCommands().Handler(args);
                        case "side_screen":
                        case "side":
                        case "sidescreen":
                            return ControlDupeSideScreens().Handler(args);
                        case "skill":
                        case "skills":
                            return ControlSkill().Handler(args);
                        case "assignable":
                        case "assignables":
                            return ControlAssignable().Handler(args);
                        default:
                            return CallToolResult.Error("domain must be info, priority, hat, command, side_screen, skill, or assignable");
                    }
                }
            };
        }
}
}
