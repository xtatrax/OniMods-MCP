using System.Collections.Generic;
using OniMcp.Core;

namespace OniMcp.Tools
{
    public static partial class StoryFacilityTools
    {
        public static McpTool ListRemoteWorkTerminals()
        {
            return new McpTool
            {
                Name = "remote_work_terminals_list",
                Hidden = true,
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "remote_work_terminal_docks_list" },
                Tags = new List<string> { "remote-work", "dock", "side-screen", "building" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=story_facility kind=remote_work_terminal action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "端末、dock、prefabId、ワールドで絞り込む", Required = false },
                    ["includeDocks"] = new McpToolParameter { Type = "boolean", Description = "同じワールドの選択可能な dock を返すか。既定値は true", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListTargets(args, go => go.GetComponent<RemoteWorkTerminal>() != null, go => RemoteWorkTerminalInfo(go.GetComponent<RemoteWorkTerminal>(), ToolUtil.GetBool(args, "includeDocks", true)), "terminals")
            };
        }

        public static McpTool SetRemoteWorkDock()
        {
            return new McpTool
            {
                Name = "remote_work_terminal_dock_set",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "remote_work_dock_set" },
                Tags = new List<string> { "remote-work", "dock", "side-screen", "building" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=story_facility kind=remote_work_terminal action=set_dock を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["dockId"] = new McpToolParameter { Type = "integer", Description = "対象の RemoteWorkerDock の InstanceID", Required = false },
                    ["dockName"] = new McpToolParameter { Type = "string", Description = "対象の dock 名", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "選択を解除するか。既定値は false", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args, target => target.GetComponent<RemoteWorkTerminal>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target RemoteWorkTerminal not found");
                    var terminal = go.GetComponent<RemoteWorkTerminal>();
                    var before = RemoteWorkTerminalInfo(terminal, includeDocks: true);
                    if (ToolUtil.GetBool(args, "clear", false))
                    {
                        terminal.FutureDock = null;
                    }
                    else
                    {
                        var dock = FindDockForTerminal(terminal, args);
                        if (dock == null)
                            return CallToolResult.Error("dockId or dockName must match a RemoteWorkerDock in the terminal world");
                        terminal.FutureDock = dock;
                    }

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["before"] = before,
                        ["terminal"] = RemoteWorkTerminalInfo(terminal, includeDocks: true)
                    });
                }
            };
        }

        public static McpTool ControlRemoteWorkTerminal()
        {
            return new McpTool
            {
                Name = "remote_work_terminal_control",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "remote_work_dock_control" },
                Tags = new List<string> { "remote-work", "dock", "side-screen", "building" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=story_facility kind=remote_work_terminal を使うこと。action=list で選択可能な dock を照会し、action=set_dock で対象の dock を設定または解除する",
                Parameters = RemoteWorkTerminalControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListRemoteWorkTerminals().Handler(args);
                    if (action == "set_dock" || action == "set")
                        return SetRemoteWorkDock().Handler(args);
                    return CallToolResult.Error("action must be list or set_dock");
                }
            };
        }

        public static McpTool ListGeneticAnalysisStations()
        {
            return new McpTool
            {
                Name = "genetic_analysis_stations_list",
                Hidden = true,
                Group = "farming",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "botanical_analyzers_list", "seed_analysis_stations_list" },
                Tags = new List<string> { "farming", "seed", "mutation", "analysis", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=story_facility kind=genetic_analysis_station action=list を使用すること",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備、種子、植物、状態で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定値は 100、最大 500", Required = false }
                }),
                Handler = args => ListTargets(args, go => go.GetSMI<GeneticAnalysisStation.StatesInstance>() != null, go => GeneticStationInfo(go.GetSMI<GeneticAnalysisStation.StatesInstance>()), "stations")
            };
        }

        public static McpTool SetGeneticAnalysisSeed()
        {
            return new McpTool
            {
                Name = "genetic_analysis_seed_set",
                Hidden = true,
                Group = "farming",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "botanical_analyzer_seed_set", "seed_analysis_allowed_set" },
                Tags = new List<string> { "farming", "seed", "mutation", "analysis", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=story_facility kind=genetic_analysis_station action=set_seed を使用すること",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["seedId"] = new McpToolParameter { Type = "string", Description = "種子の tag/prefab id。例: BasicFabricPlantSeed。speciesId とのどちらかを指定する", Required = false },
                    ["speciesId"] = new McpToolParameter { Type = "string", Description = "植物の species の prefab id。対応する seedId に解決される", Required = false },
                    ["allowed"] = new McpToolParameter { Type = "boolean", Description = "true=分析用の搬入を許可、false=禁止", Required = true }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args, target => target.GetSMI<GeneticAnalysisStation.StatesInstance>() != null);
                    if (go == null)
                        return CallToolResult.Error("Target GeneticAnalysisStation not found");
                    var station = go.GetSMI<GeneticAnalysisStation.StatesInstance>();
                    Tag seed = ResolveSeedTag(args);
                    if (!seed.IsValid)
                        return CallToolResult.Error("seedId or speciesId must resolve to a valid discovered seed option");
                    bool allowed = ToolUtil.GetBool(args, "allowed", true);
                    var before = GeneticStationInfo(station);
                    station.SetSeedForbidden(seed, !allowed);

                    return JsonResult(new Dictionary<string, object>
                    {
                        ["target"] = TargetInfo(go),
                        ["seedId"] = seed.Name,
                        ["allowed"] = allowed,
                        ["before"] = before,
                        ["station"] = GeneticStationInfo(station)
                    });
                }
            };
        }

        public static McpTool ControlGeneticAnalysisStation()
        {
            return new McpTool
            {
                Name = "genetic_analysis_station_control",
                Hidden = true,
                Group = "farming",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "botanical_analyzer_control", "seed_analysis_station_control" },
                Tags = new List<string> { "farming", "seed", "mutation", "analysis", "side-screen" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。building_control domain=story_facility kind=genetic_analysis_station を使うこと。action=list で分析可能な種子を照会し、action=set_seed で許可・禁止を設定する",
                Parameters = GeneticAnalysisStationControlParams(),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListGeneticAnalysisStations().Handler(args);
                    if (action == "set_seed" || action == "set")
                        return SetGeneticAnalysisSeed().Handler(args);
                    return CallToolResult.Error("action must be list or set_seed");
                }
            };
        }

    }
}
