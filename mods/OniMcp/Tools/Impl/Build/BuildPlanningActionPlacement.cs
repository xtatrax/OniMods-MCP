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
        public static McpTool SearchBuildables()
        {
            return new McpTool
            {
                Name = "buildings_search_defs",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Description = "互換用の入口。building_control domain=planning action=search_defs を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備 ID または名前のキーワード", Required = false },
                    ["category"] = new McpToolParameter { Type = "string", Description = "建築メニューの分類・カテゴリのキーワード。例: oxygen、plumbing、rocketry。大文字・小文字を区別しない", Required = false },
                    ["includeUnavailable"] = new McpToolParameter { Type = "boolean", Description = "現在利用できない・未解除の設備定義を含めるか。既定は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定は 30、最大 100", Required = false }
                },
                Handler = args =>
                {
                    string query = args["query"]?.ToString();
                    string category = args["category"]?.ToString();
                    bool includeUnavailable = ToolUtil.GetBool(args, "includeUnavailable", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 30, 100));
                    var defs = Assets.BuildingDefs
                        .Where(def => def != null && (includeUnavailable || IsUnlockedAndAvailable(def)))
                        .Where(def => string.IsNullOrWhiteSpace(category) || MatchesCategory(def, category))
                        .Where(def => string.IsNullOrWhiteSpace(query) || Matches(def, query))
                        .OrderBy(def => def.PrefabID)
                        .Take(limit)
                        .Select(BuildingDefToDictionary)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["query"] = string.IsNullOrWhiteSpace(query) ? null : query,
                        ["category"] = string.IsNullOrWhiteSpace(category) ? null : category,
                        ["includeUnavailable"] = includeUnavailable,
                        ["returned"] = defs.Count,
                        ["buildings"] = defs
                    }, McpJsonUtil.Settings));
                }
            };
        }
        public static McpTool ParseBuildPlan()
        {
            return new McpTool
            {
                Name = "build_parse_plan",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "build_parse_sequence", "building_plan_parse", "blueprint_parse" },
                Tags = new List<string> { "buildings", "planning", "parse", "sequence", "blueprint", "建造", "规划", "解析" },
                Description = "互換用の入口。building_control domain=planning action=parse_plan を使用してください。自然言語・文字列を解析し、prefabId、material、anchor query などの建築引数に変換します。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["plan"] = new McpToolParameter { Type = "string", Description = "建築の文字列表現。例: 粉砂岩砖@氧气、Wire-小型冰箱、用铜矿造手动发电机在电池旁", Required = false },
                    ["blueprint"] = new McpToolParameter { Type = "string", Description = "plan の別名", Required = false },
                    ["sequence"] = new McpToolParameter { Type = "string", Description = "plan の別名。粉砂岩砖-梯子-电线@电池 のような順序付き文字列も指定可能。sequenceItems に各項目の分類を返す", Required = false },
                    ["text"] = new McpToolParameter { Type = "string", Description = "plan の別名", Required = false },
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "任意の既知の設備 prefabId。指定時は素材・アンカーのみを解析", Required = false },
                    ["material"] = new McpToolParameter { Type = "string", Description = "任意の既知の建築素材。指定時は文字列から素材を推測しない", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "任意の既知のアンカー検索語。指定時は文字列からアンカーを推測しない", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す候補数。既定は 5、最大 20", Required = false }
                },
                Handler = args =>
                {
                    try
                    {
                        var resolution = ResolveBuildPlan(args);
                        return CallToolResult.Text(JsonConvert.SerializeObject(resolution.ToDictionary(), McpJsonUtil.Settings));
                    }
                    catch (Exception ex)
                    {
                        return CallToolResult.Error("parse_plan failed: " + ex);
                    }
                }
            };
        }
        public static McpTool ListBuildMaterials()
        {
            return new McpTool
            {
                Name = "buildings_materials",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "building_materials", "build_materials" },
                Tags = new List<string> { "buildings", "materials", "inventory", "available", "建造", "材料" },
                Description = "互換用の入口。building_control domain=planning action=materials を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "設備の prefabId。例: Outhouse、Tile、Wire", Required = true },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は現在アクティブなワールド。関連ワールドの在庫も含む", Required = false },
                    ["includeUnavailable"] = new McpToolParameter { Type = "boolean", Description = "発見済みだが現在の在庫が 0 の候補も返すか。既定は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数の上限。既定は 50、最大 200", Required = false }
                },
                Handler = args =>
                {
                    var planResolution = ResolveBuildPlan(args);
                    if (args["prefabId"] == null && !string.IsNullOrWhiteSpace(planResolution.PrefabId))
                        args["prefabId"] = planResolution.PrefabId;
                    if (args["material"] == null && !string.IsNullOrWhiteSpace(planResolution.Material))
                        args["material"] = planResolution.Material;
                    if (args["query"] == null && args["target"] == null && args["search"] == null && !string.IsNullOrWhiteSpace(planResolution.AnchorQuery))
                        args["query"] = planResolution.AnchorQuery;

                    string prefabId = args["prefabId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(prefabId))
                        return CallToolResult.Error("prefabId is required");
                    string resolvedPrefabId;
                    string resolveError;
                    var def = ResolveBuildingDef(prefabId, out resolvedPrefabId, out resolveError);
                    if (def == null)
                        return CallToolResult.Error(resolveError);
                    prefabId = resolvedPrefabId;

                    int worldId = ToolUtil.ResolveWorldId(args);
                    bool includeUnavailable = ToolUtil.GetBool(args, "includeUnavailable", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 50, 200));
                    var materials = AvailableMaterials(def, worldId, includeUnavailable)
                        .Take(limit)
                        .Select(item => item.ToDictionary())
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["prefabId"] = def.PrefabID,
                        ["name"] = ToolUtil.CleanName(def.Name),
                        ["worldId"] = worldId,
                        ["materialCategories"] = def.MaterialCategory,
                        ["resolvedMaterialCategories"] = MaterialCategoryTags(def).Select(tag => tag.Name).ToList(),
                        ["defaultMaterials"] = DefaultBuildElements(def).Select(tag => tag.Name).ToList(),
                        ["autoMaterial"] = materials.Count > 0 ? materials[0]["tag"] : (object)"unavailable",
                        ["autoMaterialReason"] = materials.Count > 0 ? null : "no currently available material in this world/inventory",
                        ["returned"] = materials.Count,
                        ["materials"] = materials
                    }, McpJsonUtil.Settings));
                }
            };
        }
        public static McpTool PreviewBuild()
        {
            return new McpTool
            {
                Name = "build_preview",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "build_validate", "building_preview" },
                Tags = new List<string> { "buildings", "preview", "footprint", "placement", "建造", "预检" },
                Description = "互換用の入口。building_control domain=planning action=preview を使用してください",
                Parameters = BuildPlacementParameters(includeConfirm: false, includeArea: false),
                Handler = args =>
                {
                    string error;
                    int x;
                    int y;
                    if (!TryResolvePoint(args, out x, out y, out error))
                        return CallToolResult.Error(error);

                    var previewArgs = (JObject)args.DeepClone();
                    previewArgs["dryRun"] = true;
                    var result = TryPlanOne(args["prefabId"]?.ToString(), x, y, previewArgs);
                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }
        public static McpTool AutoConnectUtility()
        {
            return new McpTool
            {
                Name = "utility_auto_connect",
                Group = "buildings",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "build_auto_connect", "wire_auto_connect", "pipe_auto_connect", "logic_auto_connect" },
                Tags = new List<string> { "buildings", "utility", "wire", "pipe", "logic", "connect", "电线", "水管", "气管", "信号线" },
                Description = "互換用の入口。building_control domain=planning action=auto_connect を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["type"] = new McpToolParameter { Type = "string", Description = "インフラの種別: wire、liquid、gas、solid、logic。prefabId 省略時はこの値から既定の設備を選ぶ", Required = false, EnumValues = new List<string> { "wire", "liquid", "gas", "solid", "logic" } },
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "任意の設備 prefabId。既定では type に応じて Wire/LiquidConduit/GasConduit/SolidConduit/LogicWire を選択", Required = false },
                    ["fromX"] = new McpToolParameter { Type = "integer", Description = "始点 X", Required = false },
                    ["fromY"] = new McpToolParameter { Type = "integer", Description = "始点 Y", Required = false },
                    ["toX"] = new McpToolParameter { Type = "integer", Description = "終点 X", Required = false },
                    ["toY"] = new McpToolParameter { Type = "integer", Description = "終点 Y", Required = false },
                    ["fromQuery"] = new McpToolParameter { Type = "string", Description = "任意の始点の検索語。wire の自動接続では既存の電源・電線・設備を検索可能", Required = false },
                    ["toQuery"] = new McpToolParameter { Type = "string", Description = "任意の終点の検索語。wire の自動接続では電力を接続する設備を検索", Required = false },
                    ["maxAutoConnectRadius"] = new McpToolParameter { Type = "integer", Description = "wire の始点省略時に、対象の電力ポートを中心として既存の電線・出力ポートを探す半径。既定は 80、最大 200", Required = false },
                    ["points"] = new McpToolParameter { Type = "array", Description = "任意の折れ線経路の点配列。[[x,y],...] または [{x,y},...] に対応。指定時は優先して使用", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールドの ID。既定は現在アクティブなワールド", Required = false },
                    ["material"] = new McpToolParameter { Type = "string", Description = "建築素材の tag。auto/default は自動選択", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "建築の優先度 1..9。既定は 5", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "事前検証のみ。建築予定を作成しない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "変更の実行には true が必須。dryRun=true の場合は省略可能", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "処理する経路セル数の上限。既定は 200、最大 500", Required = false },
                    ["allowUtilityTerrainDig"] = new McpToolParameter { Type = "boolean", Description = "既定false。自然地形を採掘して配線・配管を敷く明示許可。confirmとは別。支持関係は未評価", Required = false },
                    ["autoDigObstructions"] = new McpToolParameter { Type = "boolean", Description = "既定true。自然地形の敷設には別途allowUtilityTerrainDig=trueも必須。falseの場合は採掘を許可しない", Required = false },
                    ["autoUprootObstructions"] = new McpToolParameter { Type = "boolean", Description = "既定は true。掘り起こせる植物に対して掘り起こしを自動で指示", Required = false },
                    ["maxAutoDigCells"] = new McpToolParameter { Type = "integer", Description = "採掘・掘り起こしを自動指定するセル数の上限。既定は 100、最大 500", Required = false },
                    ["nativePathPlacement"] = new McpToolParameter { Type = "boolean", Description = "Default true. Use ONI's native utility drag placement for the whole path before falling back to per-cell placement.", Required = false }
                },
                Handler = args =>
                {
                    bool dryRun = IsDryRun(args);
                    if (!dryRun && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required unless dryRun=true");

                    string prefabId = args["prefabId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(prefabId))
                        prefabId = DefaultUtilityPrefab(args["type"]?.ToString());
                    string resolvedPrefabId;
                    string resolveError;
                    var def = ResolveBuildingDef(prefabId, out resolvedPrefabId, out resolveError);
                    if (def == null)
                        return CallToolResult.Error(resolveError);
prefabId = resolvedPrefabId;
args["prefabId"] = prefabId;

string availabilityError = BuildAvailabilityError(def, args);
if (availabilityError != null)
return CallToolResult.Error(availabilityError);

if (!IsLinearUtilityPrefab(def.PrefabID))
return CallToolResult.Error("utility_auto_connect only supports linear utility prefabs such as Wire, LiquidConduit, GasConduit, SolidConduit and LogicWire");

                    int maxCells = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxCells") ?? 200, 500));
                    string error;
                    var path = ResolveUtilityPath(args, maxCells, out error);
                    if (error != null)
                        return CallToolResult.Error(error);

                    if (args["worldId"] == null)
                        args["worldId"] = ToolUtil.ResolveWorldId(args);
                    if (args["material"] == null)
                        args["material"] = "auto";
                    if (args["autoDigObstructions"] == null)
                        args["autoDigObstructions"] = true;
                    if (args["autoUprootObstructions"] == null)
                        args["autoUprootObstructions"] = true;
                    int worldId = ToolUtil.ResolveWorldId(args);
                    var terrainConflict = UtilityTerrainConflict(def,
                        path.Select(p => Grid.XYToCell(p.x, p.y)), worldId,
                        ToolUtil.GetBool(args, "allowUtilityTerrainDig", false),
                        System.Math.Max(1, System.Math.Min(ToolUtil.GetInt(args, "maxAutoDigCells") ?? 100, 500)),
                        ToolUtil.GetBool(args, "autoDigObstructions", true));
                    if (terrainConflict != null && !GetBool(terrainConflict, "allowed"))
                        return CallToolResult.Error(JsonConvert.SerializeObject(terrainConflict, McpJsonUtil.Settings));
                    var pathSafety = ValidateUtilityPathSafety(def, path, worldId);
                    if (!pathSafety.Valid)
                        return CallToolResult.Error(JsonConvert.SerializeObject(
                            UtilityPathConflictResult(def, path, pathSafety, "path_preflight"), McpJsonUtil.Settings));
if (!dryRun && ToolUtil.GetBool(args, "nativePathPlacement", true))
                    {
                        var nativePath = TryPlaceUtilityPathNative(def, path, args);
                        if (EqualsIgnoreCase(nativePath.TryGetValue("reasonCode", out object reasonCode) ? reasonCode?.ToString() : null, "utility_path_conflict"))
                            return CallToolResult.Error(JsonConvert.SerializeObject(nativePath, McpJsonUtil.Settings));
                        if (GetBool(nativePath, "success") && GetBool(nativePath, "complete"))
                            return CallToolResult.Text(JsonConvert.SerializeObject(nativePath, McpJsonUtil.Settings));
                        if (!GetBool(nativePath, "shouldFallback"))
                            return CallToolResult.Error(JsonConvert.SerializeObject(nativePath, McpJsonUtil.Settings));
                    }
                    var fallbackSafety = ValidateUtilityPathSafety(def, path, worldId);
                    if (!fallbackSafety.Valid)
                        return CallToolResult.Error(JsonConvert.SerializeObject(
                            UtilityPathConflictResult(def, path, fallbackSafety, "cell_fallback_pre_commit"), McpJsonUtil.Settings));
                    var results = new List<Dictionary<string, object>>();
                    var errors = new List<Dictionary<string, object>>();
                    var plannedSupportCells = new HashSet<int>();
                    var autoDigContext = AutoDigContext.FromArgs(args);
                    int planned = 0;
                    int reused = 0;
                    int valid = 0;
                    int autoMarked = 0;
                    int digNewlyQueued = 0, digAlreadyQueued = 0, wouldDig = 0;
                    foreach (var point in path)
                    {
                        var result = TryPlanOne(def.PrefabID, point.x, point.y, args, plannedSupportCells, autoDigContext);
                        bool ok = result.ContainsKey("planned") && (bool)result["planned"];
                        bool validPlacement = result.ContainsKey("valid") && (bool)result["valid"];
                    bool alreadyPresent = result.ContainsKey("alreadyPresent") && (bool)result["alreadyPresent"];
                    bool alreadyConnected = result.ContainsKey("alreadyConnected") && (bool)result["alreadyConnected"];
                        digNewlyQueued += GetAutoDigInt(result, "marked");
                        digAlreadyQueued += GetAutoDigInt(result, "alreadyMarked");
                        wouldDig += GetAutoDigInt(result, "wouldMark");
                        autoMarked += GetAutoDigInt(result, "marked") + GetAutoDigInt(result, "uprootMarked") + GetAutoDigInt(result, "alreadyMarked") + GetAutoDigInt(result, "alreadyUprootMarked");
                    if (ok || alreadyPresent || alreadyConnected || (dryRun && validPlacement) || IsAutoDigResult(result))
                    {
                        valid++;
                        if (ok)
                            planned++;
                        if (alreadyPresent || alreadyConnected)
                            reused++;
                    }
                        else
                        {
                            errors.Add(result);
                        }
                        results.Add(result);
                    }
                    bool placementConflict = errors.Any(item =>
                        EqualsIgnoreCase(item.TryGetValue("reasonCode", out object itemReason) ? itemReason?.ToString() : null, "utility_path_conflict")
                        || EqualsIgnoreCase(item.TryGetValue("reasonCode", out itemReason) ? itemReason?.ToString() : null, "placement_conflict"));
                    string networkError = null; bool networkConnected = dryRun || RefreshAndValidateUtilityPathNetwork(def, path, out networkError);
                    if (!networkConnected) errors.Add(new Dictionary<string, object> { ["reasonCode"] = "utility_network_incomplete", ["error"] = networkError });
                    int connectedCells = dryRun ? valid : CountUtilityPathCells(def, path, worldId);
                    bool complete = dryRun ? errors.Count == 0 && valid == path.Count : connectedCells >= path.Count && networkConnected;
                    var response = new Dictionary<string, object>
                    {
                        ["prefabId"] = def.PrefabID,
                        ["dryRun"] = dryRun,
                        ["committed"] = !dryRun && (planned > 0 || reused > 0 || autoMarked > 0),
                        ["placementMode"] = dryRun ? "dry_run_validation" : "cell_by_cell_fallback",
                        ["pathMode"] = "continuous_manhattan_path",
                ["pathCells"] = path.Count,
                ["planned"] = planned,
                ["reusedExisting"] = reused,
                ["valid"] = valid,
                ["autoMarkedObstructions"] = autoMarked,
                ["digNewlyQueued"] = digNewlyQueued,
                ["digAlreadyQueued"] = digAlreadyQueued,
                ["wouldDig"] = dryRun ? (object)wouldDig : null,
                ["terrainImpact"] = terrainConflict,
                ["failed"] = errors.Count,
                ["presentCells"] = connectedCells,
                ["connectedCells"] = dryRun ? (object)null : (IsPhysicalUtilityPathConnected(def, path) ? connectedCells : 0),
                ["networkConnected"] = dryRun || !IsCompletedUtilityPath(def, path) ? (object)null : IsPhysicalUtilityPathConnected(def, path),
                ["constructionComplete"] = !dryRun && IsCompletedUtilityPath(def, path),
                ["connectionPlanApplied"] = !dryRun && networkConnected,
                ["flowVerified"] = false,
                ["complete"] = complete,
                ["success"] = complete,
                ["autoDigLimitReached"] = autoDigContext.LimitReached,
                ["path"] = path.Select(p => new { x = p.x, y = p.y }).ToList(),
                ["segments"] = BuildPathSegments(path),
                ["errors"] = errors.Take(50).ToList(),
                ["results"] = results
            };
                    if (placementConflict)
                    {
                        response["reasonCode"] = "utility_path_conflict";
                        response["success"] = false;
                        return CallToolResult.Error(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));
                    }
                    if (!complete)
                    {
                        response["reasonCode"] = "utility_path_incomplete";
                        return CallToolResult.Error(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));
                    }
                    return CallToolResult.Text(JsonConvert.SerializeObject(response, McpJsonUtil.Settings));
        }
            };
        }
        public static McpTool FindPlacementCandidates()
        {
            return new McpTool
            {
                Name = "build_placement_candidates",
                Group = "buildings",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "placement_candidates", "footprint_candidates", "anchor_candidates", "build_anchor_candidates" },
                Tags = new List<string> { "buildings", "preview", "footprint", "placement", "anchor", "candidate", "layout", "建造", "候选", "空位", "支撑", "碰撞" },
                Description = "互換用の入口。building_control domain=planning action=placement_candidates を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "設備の prefabId。例: MedicalCot、Tile、Wire", Required = true },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定すると x1/y1/x2/y2/worldId を省略可能", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の X。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の始点・左下の Y。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の X。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の終点・右上の Y。省略時は現在のカメラの表示範囲付近", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "発見済みのセルだけを対象にするか。既定は true", Required = false },
                    ["allowUnsupported"] = new McpToolParameter { Type = "boolean", Description = "床・天井・回転支持設備に支えがない場合でも候補を返すか。既定は false", Required = false },
                    ["material"] = new McpToolParameter { Type = "string", Description = "任意の建築素材。省略または auto の場合は自動選択", Required = false },
                    ["facade"] = new McpToolParameter { Type = "string", Description = "任意の設備の外観・permit id", Required = false },
                    ["orientation"] = new McpToolParameter { Type = "string", Description = "任意の向き。既定は Neutral", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "建築の優先度 1..9。既定は 5", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す候補数の上限。既定は 8、最大 50", Required = false },
                    ["includeRejected"] = new McpToolParameter { Type = "boolean", Description = "配置可能な候補が十分にない場合、不適合の候補も追加するか。既定は false", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "走査セル数の上限。既定は 2500、厳格な上限は 2500", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    string prefabId = args["prefabId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(prefabId))
                        return CallToolResult.Error("prefabId is required");

                    string resolvedPrefabId;
                    string resolveError;
                    var def = ResolveBuildingDef(prefabId, out resolvedPrefabId, out resolveError);
                    if (def == null)
                        return CallToolResult.Error(resolveError);
                    prefabId = resolvedPrefabId;

                    int maxCells = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxCells") ?? 2500, 2500));
                    var rect = ToolUtil.GetRect(args);
                    int width = rect["x2"] - rect["x1"] + 1;
                    int height = rect["y2"] - rect["y1"] + 1;
                    int cells = width * height;
                    if (cells > maxCells)
                        return CallToolResult.Error($"Area too large: {width}x{height}={cells} cells, maxCells={maxCells}");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 8, 50));
                    bool visibleOnly = ToolUtil.GetBool(args, "visibleOnly", true);
                    bool allowUnsupported = ToolUtil.GetBool(args, "allowUnsupported", false);
                    bool includeRejected = ToolUtil.GetBool(args, "includeRejected", false);

                    var scanArgs = (JObject)args.DeepClone();
                    scanArgs["dryRun"] = true;
                    scanArgs["worldId"] = worldId;
                    scanArgs["allowUnsupported"] = allowUnsupported;

                    var validCandidates = new List<PlacementCandidate>();
                    var rejectedCandidates = new List<PlacementCandidate>();
                    int scannedAnchors = 0;
                    int validCount = 0;
                    int warningCount = 0;
                    int rejectedCount = 0;

                    for (int y = rect["y1"]; y <= rect["y2"]; y++)
                    {
                        for (int x = rect["x1"]; x <= rect["x2"]; x++)
                        {
                            scannedAnchors++;
                            var previewArgs = (JObject)scanArgs.DeepClone();
                            var preview = TryPlanOne(prefabId, x, y, previewArgs);
                            bool valid = GetDictionaryBool(preview, "valid");
                            bool warningOnly = valid && GetNestedBool(preview, "support", "warningOnly");
                            int score = ScorePlacementCandidate(preview, valid, warningOnly);

                            var candidate = new PlacementCandidate
                            {
                                Score = score,
                                Status = valid ? (warningOnly ? "warning" : "valid") : "invalid",
                                Preview = preview
                            };
                            AddAnchorInfo(candidate.Anchor, args, x, y);

                            if (valid)
                            {
                                validCount++;
                                if (warningOnly)
                                    warningCount++;
                                validCandidates.Add(candidate);
                            }
                            else
                            {
                                rejectedCount++;
                                rejectedCandidates.Add(candidate);
                            }
                        }
                    }

                    var selected = validCandidates
                        .OrderByDescending(item => item.Score)
                        .ThenBy(item => item.AnchorY)
                        .ThenBy(item => item.AnchorX)
                        .Take(limit)
                        .ToList();

                    if (selected.Count < limit && (includeRejected || validCandidates.Count == 0))
                    {
                        selected.AddRange(rejectedCandidates
                            .OrderByDescending(item => item.Score)
                            .ThenBy(item => item.AnchorY)
                            .ThenBy(item => item.AnchorX)
                            .Take(limit - selected.Count));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["v"] = 1,
                        ["prefabId"] = prefabId,
                        ["name"] = ToolUtil.CleanName(def.Name),
                        ["worldId"] = worldId,
                        ["rect"] = new[] { rect["x1"], rect["y1"], rect["x2"], rect["y2"] },
                        ["size"] = new[] { width, height },
                        ["scannedAnchors"] = scannedAnchors,
                        ["validCount"] = validCount,
                        ["warningCount"] = warningCount,
                        ["rejectedCount"] = rejectedCount,
                        ["limit"] = limit,
                        ["includeRejected"] = includeRejected,
                        ["candidates"] = selected.Select(item => item.ToDictionary()).ToList(),
                        ["bestCandidate"] = selected.Count > 0 ? (object)selected[0].ToDictionary() : null,
                        ["next"] = selected.Count == 0
                            ? "No viable anchor found. Use build_preview on a tighter rect or raise allowUnsupported/material settings."
                            : BuildingOperationGuidance.Placement
                    }, McpJsonUtil.Settings));
                }
            };
        }
    }
}
