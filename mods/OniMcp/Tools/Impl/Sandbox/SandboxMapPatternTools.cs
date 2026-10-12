using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class SandboxTools
    {
        public static McpTool ReplaceMapPattern()
        {
            return new McpTool
            {
                Name = "sandbox_map_designate",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "map_designate", "sandbox_search_designate", "sandbox_map_replace", "map_replace", "sandbox_search_replace", "debug_map_replace" },
                Tags = new List<string> { "sandbox", "map", "designate", "search", "text-map" },
                Description = "search/designate のテキストマップ断片に基づいてマップを編集する。範囲内または現在のカメラ付近で token 行列を検索し、一致する範囲の位置を自動特定して元素 token を指定する。既定値 dryRun=true。実際の変更には confirm=true に加え、サンドボックスモードが有効であること、または force=true が必要。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["search"] = new McpToolParameter { Type = "string", Description = "検索するテキストマップ断片。行は改行で、セルの token は空白またはカンマで区切る。* または any のワイルドカード、vac/oxy/po2/co2/hyd/gas/liq/sol/tile、および元素 ID に対応する。", Required = true },
                    ["designate"] = new McpToolParameter { Type = "string", Description = "指定用断片。サイズは search と同じである必要がある。token は元素 ID または vac/oxy/po2/co2/hyd/gas/liq/water/steam/rock を指定できる。_、same、keep は元のセルを保持する。tile/bld/dup などのオブジェクト token は直接指定できない。", Required = false },
                    ["replace"] = new McpToolParameter { Type = "string", Description = "旧パラメーターとの互換用。designate を使用すること。意味は指定用断片と同じ。", Required = false },
                    ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の検索範囲ハンドル。範囲パラメーターを省略した場合、既定では現在のカメラ付近を使用する", Required = false },
                    ["x1"] = new McpToolParameter { Type = "integer", Description = "検索範囲の始点または左下 X。省略可能", Required = false },
                    ["y1"] = new McpToolParameter { Type = "integer", Description = "検索範囲の始点または左下 Y。省略可能", Required = false },
                    ["x2"] = new McpToolParameter { Type = "integer", Description = "検索範囲の終点または右上 X。省略可能", Required = false },
                    ["y2"] = new McpToolParameter { Type = "integer", Description = "検索範囲の終点または右上 Y。省略可能", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールド ID。既定値は現在のアクティブワールド、または areaId に紐づくワールド", Required = false },
                    ["visibleOnly"] = new McpToolParameter { Type = "boolean", Description = "検索時に未発見セルを unk とみなすか。既定値 false", Required = false },
                    ["matchMode"] = new McpToolParameter { Type = "string", Description = "一致処理: unique は一致が一意であることを要求し、first は最初の一致を選択し、all はすべてを置換する。既定値 unique", Required = false, EnumValues = new List<string> { "unique", "first", "all" } },
                    ["matchIndex"] = new McpToolParameter { Type = "integer", Description = "複数の一致がある場合に選択するインデックス。0 始まり。matchMode=unique/first より優先する", Required = false },
                    ["maxCells"] = new McpToolParameter { Type = "integer", Description = "検索範囲の最大セル数の既定値 1600、絶対上限 2500。実際の置換には引き続きサンドボックスの max 1000 制限が適用される", Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "置換後のセルごとの質量 kg。省略時は元素の状態に応じた既定値を使用する。気体 1、液体 1000、固体 1840、真空 0", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "置換後の温度 K。省略時は元素の既定温度を使用する", Required = false },
                    ["disease"] = new McpToolParameter { Type = "string", Description = "病原菌 ID。既定値はなし", Required = false },
                    ["diseaseCount"] = new McpToolParameter { Type = "integer", Description = "セルごとの病原菌数。既定値 0", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "一致と変更のプレビューのみ行い、マップを変更しない。既定値 true", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "サンドボックスモード以外での実行を許可する。既定値 false。dryRun の場合は不要", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。dryRun=false の場合は true が必要", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    bool dryRun = ToolUtil.GetBool(args, "dryRun", true);
                    if (!dryRun && !ValidateSandbox(args, out string error))
                        return CallToolResult.Error(error);

                    var search = ParseTokenGrid(args["search"]?.ToString());
                    string designateText = args["designate"]?.ToString();
                    if (string.IsNullOrWhiteSpace(designateText))
                        designateText = args["replace"]?.ToString();
                    if (string.IsNullOrWhiteSpace(designateText))
                        return CallToolResult.Error("designate is required");

                    var designate = ParseTokenGrid(designateText);
                    if (search.Error != null)
                        return CallToolResult.Error("Invalid search pattern: " + search.Error);
                    if (designate.Error != null)
                        return CallToolResult.Error("Invalid designate pattern: " + designate.Error);
                    if (search.Width != designate.Width || search.Height != designate.Height)
                        return CallToolResult.Error($"search and designate sizes differ: search={search.Width}x{search.Height}, designate={designate.Width}x{designate.Height}");

                    int maxCells = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "maxCells") ?? 1600, 2500));
                    var rect = WorldEditor.ResolveRectOrCamera(args, maxCells);
                    int scanCells = RectCellCount(rect);
                    if (scanCells > maxCells)
                        return CallToolResult.Error($"Search area has {scanCells} cells; maxCells={maxCells}. Use areaId or a smaller x1/y1/x2/y2 rectangle.");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    bool visibleOnly = ToolUtil.GetBool(args, "visibleOnly", false);
                    var matches = FindPatternMatches(search, rect, worldId, visibleOnly);
                    if (matches.Count == 0)
                    {
                        return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["dryRun"] = true,
                            ["matched"] = 0,
                            ["worldId"] = worldId,
                            ["rect"] = rect,
                            ["searchSize"] = new[] { search.Width, search.Height },
                            ["hint"] = "No matching token pattern found. Use world_text_map format=json/profile=standard to inspect the search area."
                        }, McpJsonUtil.Settings));
                    }

                    var selected = SelectMatches(matches, args);
                    if (selected.Error != null)
                    {
                        return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["dryRun"] = true,
                            ["matched"] = matches.Count,
                            ["error"] = selected.Error,
                            ["matches"] = MatchPreviews(matches, 20)
                        }, McpJsonUtil.Settings));
                    }

                    var changes = BuildReplacementChanges(selected.Matches, designate, worldId, args);
                    if (changes.Error != null)
                        return CallToolResult.Error(changes.Error);
                    if (changes.Items.Count > MaxSandboxCells)
                        return CallToolResult.Error($"Refusing to designate {changes.Items.Count} cells; max={MaxSandboxCells}");

                    if (!dryRun)
                    {
                        foreach (var change in changes.Items)
                            SimMessages.ReplaceElement(change.Cell, change.Element.id, CellEventLogger.Instance.SandBoxTool, change.MassKg, change.TemperatureK, change.DiseaseIdx, change.DiseaseCount);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["dryRun"] = dryRun,
                        ["matched"] = matches.Count,
                        ["selectedMatches"] = selected.Matches.Count,
                        ["changed"] = dryRun ? 0 : changes.Items.Count,
                        ["wouldChange"] = changes.Items.Count,
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["patternSize"] = new[] { search.Width, search.Height },
                        ["matches"] = MatchPreviews(selected.Matches, 20),
                        ["changes"] = ChangePreviews(changes.Items, 80)
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SampleCell()
        {
            return new McpTool
            {
                Name = "sandbox_sample_cell",
                Group = "sandbox",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "sandbox_copy_element", "debug_sample_cell" },
                Tags = new List<string> { "sandbox", "sample", "cell", "element" },
                Description = "互換用エントリー。指定セルの元素、質量、温度、病原菌を参照し、サンドボックスのブラシや塗りつぶしにそのまま使用できるパラメーターを返す。新規の呼び出しでは game_control domain=sandbox kind=read action=sample_cell を使用すること。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = true },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = true },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールド ID。既定値は現在のアクティブワールド", Required = false }
                },
                Handler = args =>
                {
                    if (Game.Instance == null)
                        return CallToolResult.Error("Game not initialized");

                    int? x = ToolUtil.GetInt(args, "x");
                    int? y = ToolUtil.GetInt(args, "y");
                    if (!x.HasValue || !y.HasValue)
                        return CallToolResult.Error("x and y are required");

                    int cell = Grid.XYToCell(x.Value, y.Value);
                    if (!Grid.IsValidCell(cell))
                        return CallToolResult.Error("Invalid target cell");
                    int worldId = ToolUtil.ResolveWorldId(args);
                    if (!ToolUtil.CellMatchesWorld(cell, worldId))
                        return CallToolResult.Error($"Target cell is not in worldId={worldId}");

                    return CallToolResult.Text(JsonConvert.SerializeObject(CellSample(cell), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool PaintElement()
        {
            return new McpTool
            {
                Name = "sandbox_paint_element",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Hidden = true,
                Aliases = new List<string> { "sandbox_brush", "debug_paint_element" },
                Description = "互換用エントリー。サンドボックスのブラシ。新規の呼び出しでは game_control domain=sandbox kind=area action=paint を使用すること。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["element"] = new McpToolParameter { Type = "string", Description = "元素 ID。例: Oxygen、Water、IgneousRock、Vacuum", Required = true },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "セルごとの質量 kg。Vacuum は自動的に 0。既定値 1", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "温度 K。既定値は元素の既定温度", Required = false },
                    ["disease"] = new McpToolParameter { Type = "string", Description = "病原菌 ID。既定値はなし", Required = false },
                    ["diseaseCount"] = new McpToolParameter { Type = "integer", Description = "セルごとの病原菌数。既定値 0", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "サンドボックスモード以外での実行を許可する。既定値 false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ValidateSandbox(args, out string error))
                        return CallToolResult.Error(error);
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var element = ResolveElement(args["element"]?.ToString());
                    if (element == null)
                        return CallToolResult.Error("Unknown element");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > MaxSandboxCells)
                        return CallToolResult.Error($"Refusing to paint {cells} cells; max={MaxSandboxCells}");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    byte diseaseIdx = byte.MaxValue;
                    var disease = Db.Get().Diseases.TryGet(args["disease"]?.ToString());
                    if (disease != null)
                        diseaseIdx = Db.Get().Diseases.GetIndex(disease.id);
                    int diseaseCount = Math.Max(0, ToolUtil.GetInt(args, "diseaseCount") ?? 0);
                    float mass = element.IsVacuum ? 0f : Math.Max(0f, ToolUtil.GetFloat(args, "massKg") ?? 1f);
                    float temp = ToolUtil.GetFloat(args, "temperatureK") ?? element.defaultValues.temperature;
                    int changed = 0;

                    foreach (int cell in RectCells(rect))
                    {
                        if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                            continue;
                        SimMessages.ReplaceElement(cell, element.id, CellEventLogger.Instance.SandBoxTool, mass, temp, diseaseIdx, diseaseCount);
                        changed++;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = changed,
                        ["element"] = element.id.ToString(),
                        ["massKg"] = mass,
                        ["temperatureK"] = temp,
                        ["worldId"] = worldId,
                        ["rect"] = rect
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool DestroyArea()
        {
            return new McpTool
            {
                Name = "sandbox_destroy_area",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Hidden = true,
                Aliases = new List<string> { "sandbox_clear", "debug_destroy_area" },
                Description = "互換用エントリー。サンドボックスの削除。新規の呼び出しでは game_control domain=sandbox kind=area action=destroy を使用すること。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "サンドボックスモード以外での実行を許可する。既定値 false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ValidateSandbox(args, out string error))
                        return CallToolResult.Error(error);
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (cells > MaxSandboxCells)
                        return CallToolResult.Error($"Refusing to destroy {cells} cells; max={MaxSandboxCells}");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    int destroyed = 0;
                    foreach (int cell in RectCells(rect))
                    {
                        if (!Grid.IsValidCell(cell) || !ToolUtil.CellMatchesWorld(cell, worldId))
                            continue;
                        GameUtil.DestroyCell(cell, CellEventLogger.Instance.SandBoxTool);
                        destroyed++;
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["destroyed"] = destroyed,
                        ["worldId"] = worldId,
                        ["rect"] = rect
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SpawnEntity()
        {
            return new McpTool
            {
                Name = "sandbox_spawn_entity",
                Group = "sandbox",
                Mode = "execute",
                Risk = "dangerous",
                Hidden = true,
                Aliases = new List<string> { "debug_spawn_entity", "sandbox_spawn" },
                Description = "互換用エントリー。サンドボックスでエンティティまたは完成済みの設備を生成する。新規の呼び出しでは game_control domain=sandbox kind=entity action=spawn_entity を使用すること。既定ではサンドボックスモードが有効である必要がある。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["prefabId"] = new McpToolParameter { Type = "string", Description = "Prefab ID。例: Hatch、BasicPlantBar、Tile、OxygenDiffuser", Required = true },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X", Required = true },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y", Required = true },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象ワールド ID。既定値は現在のアクティブワールド", Required = false },
                    ["massKg"] = new McpToolParameter { Type = "number", Description = "ElementChunk を生成する場合の質量 kg。既定値 100", Required = false },
                    ["temperatureK"] = new McpToolParameter { Type = "number", Description = "ElementChunk を生成する場合の温度 K。既定値は元素の既定温度", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "サンドボックスモード以外での実行を許可する。既定値 false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ValidateSandbox(args, out string error))
                        return CallToolResult.Error(error);
                    string prefabId = args["prefabId"]?.ToString();
                    if (string.IsNullOrWhiteSpace(prefabId))
                        return CallToolResult.Error("prefabId is required");
                    int? x = ToolUtil.GetInt(args, "x");
                    int? y = ToolUtil.GetInt(args, "y");
                    if (!x.HasValue || !y.HasValue)
                        return CallToolResult.Error("x and y are required");
                    int cell = Grid.XYToCell(x.Value, y.Value);
                    if (!Grid.IsValidCell(cell))
                        return CallToolResult.Error("Invalid target cell");
                    int worldId = ToolUtil.ResolveWorldId(args);
                    if (!ToolUtil.CellMatchesWorld(cell, worldId))
                        return CallToolResult.Error($"Target cell is not in worldId={worldId}");

                    var prefab = Assets.GetPrefab(prefabId.Trim());
                    if (prefab == null)
                        return CallToolResult.Error("Prefab not found");

                    GameObject spawned;
                    var building = prefab.GetComponent<Building>();
                    if (building != null)
                    {
                        var def = building.Def;
                        spawned = def.Build(cell, Orientation.Neutral, null, def.DefaultElements(), ToolUtil.GetFloat(args, "temperatureK") ?? 298.15f);
                    }
                    else
                    {
                        var controller = prefab.GetComponent<KBatchedAnimController>();
                        Grid.SceneLayer layer = controller == null ? Grid.SceneLayer.Creatures : controller.sceneLayer;
                        spawned = GameUtil.KInstantiate(prefab, Grid.CellToPosCBC(cell, layer), layer);
                        var chunk = spawned.GetComponent<ElementChunk>();
                        if (chunk != null)
                        {
                            var primary = spawned.GetComponent<PrimaryElement>();
                            primary.Mass = Math.Max(0f, ToolUtil.GetFloat(args, "massKg") ?? 100f);
                            primary.Temperature = ToolUtil.GetFloat(args, "temperatureK") ?? primary.Element.defaultValues.temperature;
                        }
                        spawned.SetActive(true);
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["spawned"] = spawned != null,
                        ["prefabId"] = prefabId,
                        ["id"] = spawned?.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                        ["x"] = x.Value,
                        ["y"] = y.Value,
                        ["worldId"] = worldId
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ListStoryTraits()
        {
            return new McpTool
            {
                Name = "sandbox_story_traits_list",
                Group = "sandbox",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "sandbox_story_trait_templates_list", "story_trait_stamp_templates_list" },
                Tags = new List<string> { "sandbox", "story", "trait", "stamp", "template" },
                Description = "互換用エントリー。サンドボックスの Story Trait Tool で配置できるストーリー特性のテンプレートと、現在既に存在するかどうかを一覧表示する。新規の呼び出しでは game_control domain=sandbox kind=read action=list_story_traits を使用すること。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "storyId、trait 名、テンプレート ID で検索する", Required = false }
                },
                Handler = args =>
                {
                    string query = args["query"]?.ToString();
                    var stories = Db.Get().Stories.resources
                        .Where(story => story != null && !string.IsNullOrWhiteSpace(story.sandboxStampTemplateId))
                        .Where(story => StoryMatches(story, query))
                        .OrderBy(story => story.Id)
                        .Select(StoryInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = stories.Count,
                        ["stories"] = stories
                    }, McpJsonUtil.Settings));
                }
            };
        }

    }
}
