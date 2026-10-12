using System.Collections.Generic;
using System.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class RocketCrewCargoTools
    {
        public static McpTool ListCrewRequests()
        {
            return new McpTool
            {
                Name = "rocket_crew_requests_list",
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "summon_crew_list", "rocket_passenger_requests_list" },
                Tags = new List<string> { "rocket", "crew", "passenger", "summon", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=crew_request action=list を優先して使う。SummonCrewSideScreen の乗組員の召集・解放状態、搭乗人数、操縦士の状態を一覧表示する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["rocketId"] = new McpToolParameter { Type = "integer", Description = "任意のロケットの InstanceID", Required = false },
                    ["rocketName"] = new McpToolParameter { Type = "string", Description = "任意のロケット名", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "ロケット名、モジュール名、状態で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    var craftFilter = FindRocket(args);
                    string query = args["query"]?.ToString();
                    int limit = ToolUtil.ClampLimit(args, 100, 500);

                    var rows = EnumeratePassengerModules(craftFilter)
                        .Select(item => CrewRequestInfo(item.craft, item.passenger))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["rocketName"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["crewRequests"] = rows
                    });
                }
            };
        }

        public static McpTool SetCrewRequest()
        {
            return new McpTool
            {
                Name = "rocket_crew_request_set",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "summon_crew_set", "rocket_passenger_request_set" },
                Tags = new List<string> { "rocket", "crew", "passenger", "summon", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=crew_request action=set を優先して使う。SummonCrewSideScreen のボタンの状態を設定する。request=乗組員を召集して搭乗させる、release=乗組員を解放して自由に行動させる",
                Parameters = PassengerLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["state"] = new McpToolParameter { Type = "string", Description = "request または release", Required = true, EnumValues = new List<string> { "request", "release" } },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "乗組員の搭乗要求の変更を確認するため、true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");
                    var found = FindPassengerModule(args);
                    if (found.passenger == null)
                        return CallToolResult.Error("Target passenger rocket module not found");
                    string state = (args["state"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    var before = CrewRequestInfo(found.craft, found.passenger);
                    if (state == "request")
                        found.passenger.RequestCrewBoard(PassengerRocketModule.RequestCrewState.Request);
                    else if (state == "release")
                        found.passenger.RequestCrewBoard(PassengerRocketModule.RequestCrewState.Release);
                    else
                        return CallToolResult.Error("state must be request or release");

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(found.passenger.gameObject),
                        ["before"] = before,
                        ["crewRequest"] = CrewRequestInfo(found.craft, found.passenger)
                    });
                }
            };
        }

        public static McpTool ControlCrewRequest()
        {
            return new McpTool
            {
                Name = "rocket_crew_request_control",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "summon_crew_control", "rocket_passenger_request_control" },
                Tags = new List<string> { "rocket", "crew", "passenger", "summon", "side-screen" },
                Description = "ロケットの乗組員の召集・解放状態の読み取りと設定を統合する。action=list/set。set は confirm=true が必要。",
                Parameters = PassengerLookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set", Required = true },
                    ["state"] = new McpToolParameter { Type = "string", Description = "action=set では request または release", Required = false, EnumValues = new List<string> { "request", "release" } },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list でロケット名、モジュール名、状態により絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set では true が必須", Required = false }
                }),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListCrewRequests().Handler(args);
                    if (action == "set")
                        return SetCrewRequest().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool ListAssignmentGroups()
        {
            return new McpTool
            {
                Name = "assignment_groups_list",
                Group = "rockets",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "rocket_assignment_groups_list", "crew_assignment_groups_list" },
                Tags = new List<string> { "rocket", "crew", "assignment", "group", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=assignment_group action=list を優先して使う。AssignmentGroupControllerSideScreen の割り当てグループと、各複製人間の加入状態、同じワールドにいるか、操縦士の資格を一覧表示する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["groupId"] = new McpToolParameter { Type = "string", Description = "任意の AssignmentGroupID との完全一致", Required = false },
                    ["controllerId"] = new McpToolParameter { Type = "integer", Description = "任意の割り当てグループのコントローラーオブジェクトの InstanceID", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "オブジェクト名、prefabId、groupId、複製人間の名前で絞り込む", Required = false },
                    ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "各複製人間のグループへの加入状態を返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string query = args["query"]?.ToString();
                    bool includeDupes = ToolUtil.GetBool(args, "includeDupes", true);
                    int limit = ToolUtil.ClampLimit(args, 100, 500);
                    var target = FindAssignmentGroupController(args);

                    var rows = EnumerateAssignmentGroups()
                        .Where(group => target == null || group == target)
                        .Select(group => AssignmentGroupInfo(group, includeDupes))
                        .Where(info => MatchesQuery(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["returned"] = rows.Count,
                        ["assignmentGroups"] = rows
                    });
                }
            };
        }

        public static McpTool SetAssignmentGroupMember()
        {
            return new McpTool
            {
                Name = "assignment_group_member_set",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "rocket_assignment_group_member_set", "crew_assignment_group_member_set" },
                Tags = new List<string> { "rocket", "crew", "assignment", "group", "side-screen" },
                Description = "互換用の入口。building_control domain=rocket rocketDomain=assignment_group action=set を優先して使う。AssignmentGroupControllerSideScreen で、1人の複製人間を指定グループに加入させるかを設定する。その複製人間の加入スイッチをクリックする操作と同等",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["groupId"] = new McpToolParameter { Type = "string", Description = "AssignmentGroupID。controllerId が空の場合に使用する", Required = false },
                    ["controllerId"] = new McpToolParameter { Type = "integer", Description = "割り当てグループのコントローラーオブジェクトの InstanceID。groupId が空の場合に使用する", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "複製人間または MinionAssignablesProxy の InstanceID。dupeName が空の場合に使用する", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "複製人間の名前。dupeId が空の場合に使用する", Required = false },
                    ["isMember"] = new McpToolParameter { Type = "boolean", Description = "true で割り当てグループへ追加し、false でグループから外す", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "割り当てグループのメンバー変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var group = FindAssignmentGroupController(args);
                    if (group == null)
                        return CallToolResult.Error("Assignment group controller not found; use building_control domain=rocket rocketDomain=assignment_group action=list first");

                    var proxy = FindAssignableProxy(args);
                    if (proxy == null)
                        return CallToolResult.Error("Duplicant assignable proxy not found");

                    bool isMember = ToolUtil.GetBool(args, "isMember", false);
                    var before = AssignmentGroupInfo(group, includeDupes: true);
                    group.SetMember(proxy, isMember);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(group.gameObject),
                        ["dupe"] = AssignmentGroupDupeInfo(group, proxy, AssignmentGroupWorld(group)),
                        ["before"] = before,
                        ["after"] = AssignmentGroupInfo(group, includeDupes: true)
                    });
                }
            };
        }

        public static McpTool ControlAssignmentGroup()
        {
            return new McpTool
            {
                Name = "assignment_group_control",
                Group = "rockets",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "rocket_assignment_group_control", "crew_assignment_group_control" },
                Tags = new List<string> { "rocket", "crew", "assignment", "group", "side-screen" },
                Description = "ロケット・割り当てグループのメンバーの読み取りと設定を統合する。action=list/set。set は confirm=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list、set", Required = true },
                    ["groupId"] = new McpToolParameter { Type = "string", Description = "AssignmentGroupID。controllerId が空の場合に使用する", Required = false },
                    ["controllerId"] = new McpToolParameter { Type = "integer", Description = "割り当てグループのコントローラーオブジェクトの InstanceID。groupId が空の場合に使用する", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "action=set の複製人間または MinionAssignablesProxy の InstanceID。dupeName が空の場合に使用する", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "action=set の複製人間の名前。dupeId が空の場合に使用する", Required = false },
                    ["isMember"] = new McpToolParameter { Type = "boolean", Description = "action=set で true を指定すると割り当てグループへ追加し、false を指定すると外す", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list でオブジェクト名、prefabId、groupId、複製人間の名前により絞り込む", Required = false },
                    ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "action=list で各複製人間のグループへの加入状態を返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list で返す件数の上限。既定値は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set では true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListAssignmentGroups().Handler(args);
                    if (action == "set")
                        return SetAssignmentGroupMember().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

    }
}
