using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Server;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SnapshotTools
    {
        private static readonly SnapshotDeltaCache DeltaCache = new SnapshotDeltaCache();

        public static McpTool GetColonyStateSnapshot()
        {
            return new McpTool
            {
                Name = "colony_state_snapshot",
                Group = "colony",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "state_snapshot", "colony_snapshot" },
                Tags = new List<string> { "snapshot", "colony", "status", "diagnostics", "dupes", "food", "research", "performance", "快照", "状态" },
                Description = "互換用の入口。colony_control domain=snapshot action=get を優先してください。少ないトークンでコロニーの状態を一括取得し、通常の game_control domain=speed action=time + colony_status + colony_diagnostics + colony_alerts + resources_food + dupes_list + research_status の組み合わせを置き換えます。既定では全マップの大気を走査しません。酸素の統計が必要な場合は includeAtmosphere=true を明示してください。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["profile"] = new McpToolParameter { Type = "string", Description = "minimal/brief/standard/full。minimal はサイクル、一時停止状態、警告レベル、主要指標、1文の要約のみを返す", Required = false, EnumValues = new List<string> { "minimal", "brief", "standard", "full" } },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は現在アクティブなワールド。-1 で軽量に集計できるすべての項目を全体集計", Required = false },
                    ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "複製人間の要約を含めるか。既定は true", Required = false },
                    ["includeFood"] = new McpToolParameter { Type = "boolean", Description = "食料の要約を含めるか。既定は true", Required = false },
                    ["includeResearch"] = new McpToolParameter { Type = "boolean", Description = "研究の状態を含めるか。既定は true", Required = false },
                    ["includeBuildings"] = new McpToolParameter { Type = "boolean", Description = "主要設備の数を含めるか。既定は true", Required = false },
                    ["includeAlerts"] = new McpToolParameter { Type = "boolean", Description = "スナップショットの警告を含めるか。既定は true", Required = false },
                    ["includeAtmosphere"] = new McpToolParameter { Type = "boolean", Description = "可視の大気セルを走査するか。全マップの走査となる。既定は false", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "発見済みセル内の資源・食料だけを集計するか。既定は true。デバッグでは false を指定可能", Required = false },
                    ["dupeLimit"] = new McpToolParameter { Type = "integer", Description = "詳細を返す複製人間の人数上限。既定は brief=0 standard=12 full=50", Required = false },
                    ["foodLimit"] = new McpToolParameter { Type = "integer", Description = "返す食料の種類数の上限。既定は brief=0 standard=8 full=50", Required = false },
                    ["delta"] = new McpToolParameter { Type = "boolean", Description = "同じ legacy session または明示した deltaHandle と、同じ deltaKey の前回呼び出しからの変化だけを返す。初回または resetDelta=true では baseline を返す", Required = false },
                    ["deltaKey"] = new McpToolParameter { Type = "string", Description = "任意の delta キャッシュスロット。同じエージェントが観察ループごとに別の baseline を保持できる", Required = false },
                    ["deltaHandle"] = new McpToolParameter { Type = "string", Description = "MCP session のないステートレス呼び出しで delta baseline を引き継ぐための不透明なハンドル。初回の delta 応答で生成され、以後はそのまま渡す。legacy session の呼び出しではこの引数を無視する", Required = false },
                    ["resetDelta"] = new McpToolParameter { Type = "boolean", Description = "今回の delta baseline をクリアし、現在の結果を新しい baseline にする", Required = false },
                    ["watch"] = new McpToolParameter { Type = "array", Description = "監視指標の配列またはカンマ区切り文字列。例: stress,food_kcal,red_alert,alerts,oxygen。指定時は watch ブロックを返す", Required = false },
                    ["watchOnly"] = new McpToolParameter { Type = "boolean", Description = "watch 指標、警告レベル、要約だけを返す。繰り返しのポーリング向け", Required = false },
                    ["thresholds"] = new McpToolParameter { Type = "object", Description = "監視の閾値。例: {\"stress\":\">60\",\"food_kcal\":\"<20000\"}。threshold とも互換", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string profile = NormalizeProfile(args["profile"]?.ToString());
                    int worldId = ToolUtil.GetInt(args, "worldId") ?? (ClusterManager.Instance?.activeWorldId ?? 0);
                    bool includeDupes = ToolUtil.GetBool(args, "includeDupes", true);
                    bool includeFood = ToolUtil.GetBool(args, "includeFood", true);
                    bool includeResearch = ToolUtil.GetBool(args, "includeResearch", true);
                    bool includeBuildings = ToolUtil.GetBool(args, "includeBuildings", true);
                    bool includeAlerts = ToolUtil.GetBool(args, "includeAlerts", true);
                    bool includeAtmosphere = ToolUtil.GetBool(args, "includeAtmosphere", false);
                    bool visibleOnly = ToolUtil.GetBool(args, "visibleOnly", true);
                    int dupeLimit = Clamp(ToolUtil.GetInt(args, "dupeLimit") ?? DefaultDupeLimit(profile), 0, 100);
                    int foodLimit = Clamp(ToolUtil.GetInt(args, "foodLimit") ?? DefaultFoodLimit(profile), 0, 100);
                    bool minimal = profile == "minimal";
                    var watchKeys = ParseWatch(args["watch"]);
                    bool watchOnly = ToolUtil.GetBool(args, "watchOnly", false);
                    bool needAtmosphere = includeAtmosphere || WatchNeedsAtmosphere(watchKeys);

                    List<DupeSnapshot> dupes = (includeDupes || includeAlerts || minimal || watchOnly || watchKeys.Count > 0) ? BuildDupes(worldId) : new List<DupeSnapshot>();
                    FoodSnapshot food = (includeFood || includeAlerts || minimal || watchOnly || watchKeys.Count > 0) ? BuildFood(worldId, foodLimit, visibleOnly) : null;
                    BuildingSnapshot buildings = (includeBuildings || includeAlerts || minimal || watchOnly || watchKeys.Count > 0) ? BuildBuildings(worldId) : null;
                    Dictionary<string, object> atmosphere = needAtmosphere ? BuildAtmosphere(worldId) : null;
                    var redAlert = BuildRedAlert(worldId);
                    var alerts = includeAlerts || minimal || watchOnly || watchKeys.Count > 0
                        ? BuildAlerts(dupes, food, buildings, atmosphere, redAlert)
                        : new Dictionary<string, object> { ["count"] = 0, ["items"] = new List<Dictionary<string, object>>() };
                    var metrics = BuildMetrics(dupes, food, buildings, atmosphere, redAlert, alerts);
                    var time = BuildTime();
                    var colony = BuildColony();

                    Dictionary<string, object> snapshot = minimal
                        ? BuildMinimalSnapshot(profile, worldId, time, colony, metrics)
                        : new Dictionary<string, object>
                        {
                            ["v"] = 2,
                            ["profile"] = profile,
                            ["worldId"] = worldId,
                            ["visibleOnly"] = visibleOnly,
                            ["time"] = time,
                            ["colony"] = colony
                        };

                    if (!minimal && includeDupes)
                        snapshot["dupes"] = BuildDupeResult(dupes, dupeLimit, profile);
                    if (!minimal && includeFood && food != null)
                        snapshot["food"] = food.ToDictionary();
                    if (!minimal && includeBuildings && buildings != null)
                        snapshot["buildings"] = buildings.ToDictionary();
                    if (!minimal && includeResearch)
                        snapshot["research"] = BuildResearch();
                    if (!minimal && needAtmosphere)
                        snapshot["atmosphere"] = atmosphere;
                    if (!minimal && includeAlerts)
                        snapshot["alerts"] = alerts;

                    if (watchKeys.Count > 0)
                        snapshot["watch"] = BuildWatch(watchKeys, metrics, args["thresholds"] as JObject ?? args["threshold"] as JObject);
                    if (watchOnly)
                        snapshot = BuildWatchOnlySnapshot(profile, worldId, time, metrics, snapshot.ContainsKey("watch") ? snapshot["watch"] : BuildWatch(DefaultWatchKeys(), metrics, args["thresholds"] as JObject ?? args["threshold"] as JObject));

                    snapshot["cost"] = new Dictionary<string, object>
                    {
                        ["singleTool"] = true,
                        ["deltaAvailable"] = true,
                        ["watchOnly"] = watchOnly,
                        ["replaces"] = new[] { "game_control domain=speed action=time", "colony_control domain=read action=status", "colony_control domain=diagnostic action=diagnostics", "colony_control domain=diagnostic action=alerts", "read_control domain=resources action=food", "colony_control domain=read action=dupes", "colony_control domain=management kind=research action=status" },
                        ["fullGridScan"] = needAtmosphere
                    };
                    if (needAtmosphere)
                        ((Dictionary<string, object>)snapshot["cost"])["atmosphereNote"] = "Atmosphere is a full visible-grid aggregate and can mask local pockets; use world_area_snapshot on suspected rooms for local oxygen/temperature.";

                    if (ToolUtil.GetBool(args, "delta", false))
                    {
                        McpToolStateScope stateScope;
                        string stateScopeError;
                        if (!McpToolStateScope.TryResolve(McpHttpServer.CurrentSessionId, args["deltaHandle"]?.ToString(), out stateScope, out stateScopeError))
                            return CallToolResult.Error(stateScopeError);

                        string deltaKey = BuildDeltaKey(args, profile, worldId, watchKeys, watchOnly);
                        var delta = DeltaCache.Apply(stateScope.CacheScopeId, deltaKey, snapshot, ToolUtil.GetBool(args, "resetDelta", false));
                        if (stateScope.IsStateless)
                            delta["deltaHandle"] = stateScope.Handle;
                        return CallToolResult.Text(JsonConvert.SerializeObject(delta, McpJsonUtil.Settings));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(snapshot, McpJsonUtil.Settings));
                }
            };
        }
    }
}
