using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
{
        public static McpTool SweepArea()
        {
            return new McpTool
            {
                Name = "orders_sweep_area",
                Group = "orders",
                Mode = "execute",
                Risk = "medium",
                Hidden = true,
                Description = "互換用の入口。orders_control domain=area action=sweep を優先して使う。矩形領域内の地面にある固体のアイテム・破片を、収納先へ運ぶ掃除の対象に指定する。水、汚染水、その他すべての液体は扱わない。",
                Aliases = new List<string> { "sweep_area", "clear_debris_area", "solid_debris_sweep_area" },
                Tags = new List<string> { "orders", "sweep", "clear", "debris", "solid", "pickupable", "storage", "not-liquid", "固体", "碎片", "散落物", "清扫到仓库" },
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "掃除作業の優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "赤色の最優先にするか。既定値は false", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "走査して、指定・スキップされる対象を返すだけで、実際の掃除指示は出さない。既定値は false。dryRun では confirm は不要", Required = false },
                    ["includeStored"] = new McpToolParameter { Type = "boolean", Description = "収納済みのオブジェクトを診断に含めるか。既定値は false。通常は収納済みのものを掃除対象にしない", Required = false },
                    ["detail"] = new McpToolParameter { Type = "boolean", Description = "対象ごとの座標サンプルを返すか。既定値は false。通常は先に概要フィールドを確認する", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "個別診断で返す件数の上限。既定値は 120、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "領域が 100 セルを超える場合は true が必須", Required = false },
                    ["previewToken"] = new McpToolParameter { Type = "string", Description = "dryRun が返すプレビュートークン。指定すると、重複する引数を省略できる", Required = false }
                }),
                Handler = args =>
                {
                    bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
                    string previewToken = args["previewToken"]?.ToString();
                    if (!dryRun && !string.IsNullOrEmpty(previewToken))
                    {
                        var cachedArgs = PreviewTokenRegistry.Get(previewToken);
                        if (cachedArgs == null)
                            return CallToolResult.Error("Preview token expired or invalid; run dryRun=true first");
                        args = cachedArgs;
                        args["confirm"] = true;
                        dryRun = false;
                    }

                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");

                    var rect = ToolUtil.GetRect(args);
                    int cells = RectCellCount(rect);
                    if (!dryRun && cells > 100 && !ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required when sweeping more than 100 cells");

                    int worldId = ToolUtil.ResolveWorldId(args);
                    bool includeStored = ToolUtil.GetBool(args, "includeStored", false);
                    bool detail = ToolUtil.GetBool(args, "detail", false);
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 120, 500));
                    int marked = 0;
                    double kgTotal = 0;
                    int scanned = 0;
                    int inRect = 0;
                    int skippedStored = 0;
                    int skippedEquipped = 0;
                    int skippedNoCell = 0;
                    int skippedNoClearable = 0;
                    int skippedLiveDuplicants = 0;
                    var liquidScan = ScanLiquidCells(rect, worldId, 12);
                    var targets = new List<Dictionary<string, object>>();
                    var targetCells = new List<int>();
                    var executionSkipped = new Dictionary<string, int>();

                    foreach (var pickupable in Components.Pickupables.Items)
                    {
                        if (pickupable == null || pickupable.gameObject == null)
                            continue;

                        scanned++;
                        int cell = SweepCell(pickupable);
                        if (!Grid.IsValidCell(cell))
                        {
                            skippedNoCell++;
                            IncrementSkip(executionSkipped, "invalid_cell");
                            AddSweepTarget(targets, detail, limit, pickupable, cell, "skipped_invalid_cell");
                            continue;
                        }
                        if (!CellInRect(cell, rect, worldId))
                            continue;

                        inRect++;
                        if (SweepTargetPolicy.IsLiveDuplicant(pickupable.gameObject))
                        {
                            skippedLiveDuplicants++;
                            IncrementSkip(executionSkipped, "live_duplicant");
                            AddSweepTarget(targets, detail, limit, pickupable, cell, "skipped_live_duplicant");
                            continue;
                        }
                        var kpid = pickupable.KPrefabID ?? pickupable.GetComponent<KPrefabID>();
                        if (kpid != null && kpid.HasTag(GameTags.Equipped))
                        {
                            skippedEquipped++;
                            IncrementSkip(executionSkipped, "equipped");
                            AddSweepTarget(targets, detail, limit, pickupable, cell, "skipped_equipped");
                            continue;
                        }
                        if (!includeStored && kpid != null && (kpid.HasTag(GameTags.Stored) || pickupable.storage != null))
                        {
                            skippedStored++;
                            IncrementSkip(executionSkipped, "stored");
                            AddSweepTarget(targets, detail, limit, pickupable, cell, "skipped_stored");
                            continue;
                        }

                        var clearable = pickupable.GetComponent<Clearable>();
                        if (clearable == null)
                        {
                            skippedNoClearable++;
                            IncrementSkip(executionSkipped, "no_clearable");
                            AddSweepTarget(targets, detail, limit, pickupable, cell, "skipped_no_clearable");
                            continue;
                        }

                        var primary = pickupable.PrimaryElement ?? pickupable.GetComponent<PrimaryElement>();
                        if (primary != null)
                            kgTotal += ToolUtil.SafeFloat(primary.Mass);

                        if (!dryRun)
                        {
                            clearable.MarkForClear();
                            ApplyPriority(pickupable.gameObject, args);
                        }
                        marked++;
                        targetCells.Add(cell);
                        AddSweepTarget(targets, detail, limit, pickupable, cell, dryRun ? "would_mark" : "marked");
                    }

                    var responseDict = new Dictionary<string, object>
                    {
                        ["dryRun"] = dryRun,
                        ["scannedPickupables"] = scanned,
                        ["inRect"] = inRect,
                        ["marked"] = marked,
                        ["skipped"] = new Dictionary<string, object>
                        {
                            ["invalidCell"] = skippedNoCell,
                            ["stored"] = skippedStored,
                            ["equipped"] = skippedEquipped,
                            ["liveDuplicants"] = skippedLiveDuplicants,
                            ["noClearable"] = skippedNoClearable
                        },
                        ["execution"] = CellExecutionMetadata("sweep", worldId, targetCells, executionSkipped, detail, limit),
                        ["worldId"] = worldId,
                        ["rect"] = rect,
                        ["preview"] = new Dictionary<string, object>
                        {
                            ["targets"] = targets.Where(item => item["status"]?.ToString() == "would_mark" || item["status"]?.ToString() == "marked").ToList(),
                            ["kgTotal"] = Math.Round(kgTotal, 3),
                            ["risks"] = SweepPreviewRisks(liquidScan)
                        },
                        ["targets"] = targets,
                        ["truncatedTargets"] = detail ? Math.Max(0, inRect - targets.Count) : 0,
                        ["liquidCellsInRect"] = liquidScan["count"],
                        ["mopHint"] = (int)liquidScan["count"] > 0 ? "This area contains liquid cells. Sweep only handles solid pickupables/debris; use orders_control domain=area action=mop for water/liquid on floor." : null,
                        ["liquidSamples"] = liquidScan["samples"],
                        ["note"] = marked == 0
                            ? "No sweep errands were marked. Check targets/skipped; sweep only handles solid pickupables/debris, not water/liquid. For floor liquids use orders_control domain=area action=mop. Sweep also requires reachable storage accepting the item before dupes will haul it."
                            : "Sweep marks solid debris only; it never mops water/liquid. Dupes still need reachable storage accepting the item."
                    };
                    if (dryRun)
                        responseDict["previewToken"] = PreviewTokenRegistry.Register(args);
                    return CallToolResult.Text(JsonConvert.SerializeObject(responseDict, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool AreaAction()
        {
            return new McpTool
            {
                Name = "orders_area_action",
                Group = "orders",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "orders_area", "area_order", "area_designate" },
                Tags = new List<string> { "orders", "area", "dig", "sweep", "mop", "disinfect", "cancel", "harvest", "挖", "扫", "清扫", "打扫", "拾取", "擦", "毒", "消", "收" },
                Description = "領域単位の作業指示の統合エントリーポイント。action=dig/挖/挖掘、sweep/扫/清扫/打扫/拾取/搬运/clean、mop/擦/擦拭/拖地、disinfect/毒/消毒/杀菌、cancel/消/取消、harvest/收/收获/采摘。各作業指示の引数を再利用する。危険な操作や広範囲への操作では、引き続き confirm=true が必要。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                        ["action"] = new McpToolParameter { Type = "string", Description = "領域単位の作業指示: dig/挖/挖掘、sweep/扫/清扫/打扫/拾取/搬运/clean、mop/擦/擦拭/拖地、disinfect/毒/消毒/杀菌、cancel/消/取消、harvest/收/收获/采摘", Required = true, EnumValues = new List<string> { "dig", "sweep", "mop", "disinfect", "cancel", "harvest", "挖", "挖掘", "开挖", "扫", "清", "清扫", "清理", "打扫", "捡", "捡起", "拾取", "搬运", "收拾", "clean", "擦", "擦拭", "擦水", "拖", "拖地", "毒", "消毒", "杀菌", "灭菌", "消", "取消", "取消任务", "取消命令", "收", "收获", "收割", "采收", "采摘" } },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "対応するコマンドで使用する作業優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "対応するコマンドで、赤色の最優先にするか。既定値は false", Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "dig/sweep/harvest に対応。プレビューだけを行い、実行しない", Required = false },
                    ["previewToken"] = new McpToolParameter { Type = "string", Description = "dig/sweep/harvest の dryRun が返すプレビュートークン", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "harvest に対応: mark、when_ready、cancel", Required = false, EnumValues = new List<string> { "mark", "when_ready", "cancel" } },
                    ["readyOnly"] = new McpToolParameter { Type = "boolean", Description = "harvest に対応。mark モードで現在収穫可能な対象だけを処理するか。既定値は true", Required = false },
                    ["includeStored"] = new McpToolParameter { Type = "boolean", Description = "sweep に対応。収納済みのオブジェクトを含めるか。既定値は false", Required = false },
                    ["includeAttack"] = new McpToolParameter { Type = "boolean", Description = "cancel に対応。攻撃指定を取り消すか。既定値は true", Required = false },
                    ["includeCapture"] = new McpToolParameter { Type = "boolean", Description = "cancel に対応。捕獲指定を取り消すか。既定値は true", Required = false },
                    ["detail"] = new McpToolParameter { Type = "boolean", Description = "dig/sweep に対応。対象ごとの座標サンプルを返すか。既定値は false。概要の読み取りを優先する", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "dig/sweep に対応。個別診断で返す件数の上限", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作や広範囲への操作の確認。dig の実行では true が必須", Required = false }
                }),
                Handler = args => RunAreaAction(args)
            };
        }

        public static McpTool DesignationControl()
        {
            return new McpTool
            {
                Name = "designation_control",
                Group = "orders",
                Mode = "execute",
                Risk = "dangerous",
                Aliases = new List<string> { "orders_designation_control", "map_designation_control" },
                Tags = new List<string> { "orders", "designation", "deconstruct", "attack", "capture", "conduit", "manual-delivery", "拆", "杀", "攻击", "捕", "捕捉", "抓捕", "捕获" },
                Description = "指定・取り消し操作の統合エントリーポイント。action=deconstruct/拆/拆除/拆毁、attack/杀/攻击/进攻、capture/捕/捕捉/抓捕/捕获/wrangle、empty_conduits/清空管道、cut_conduits/剪断、manual_delivery/手动送货。処理の振り分けだけを統合し、各旧ツールの confirm と安全上の制約を維持する。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter
                    {
                        Type = "string",
                            Description = "指定する操作: deconstruct/拆/拆除/拆毁、attack/杀/攻击/进攻、capture/捕/捕捉/抓捕/捕获/wrangle、empty_conduits/清空管道、cut_conduits/剪断、manual_delivery/手动送货",
                        Required = true,
                            EnumValues = new List<string> { "deconstruct", "attack", "capture", "empty_conduits", "cut_conduits", "manual_delivery", "拆", "拆除", "拆毁", "拆掉", "杀", "攻击", "打", "进攻", "捕", "捕捉", "抓捕", "抓", "捕获", "wrangle", "清管", "清空管道", "剪", "剪断", "剪管", "送", "送货", "手动送货" }
                    },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "対象のオブジェクトの InstanceID。deconstruct/attack/capture/manual_delivery に使用できる", Required = false },
                    ["x"] = new McpToolParameter { Type = "integer", Description = "対象のセルの X。deconstruct/attack/cut_conduits/manual_delivery に使用できる", Required = false },
                    ["y"] = new McpToolParameter { Type = "integer", Description = "対象のセルの Y。deconstruct/attack/cut_conduits/manual_delivery に使用できる", Required = false },
                    ["direction"] = new McpToolParameter { Type = "string", Description = "cut_conduits single-cell edge: up/down/left/right", Required = false },
                    ["type"] = new McpToolParameter { Type = "string", Description = "empty_conduits/cut_conduits の配管・配線の種類", Required = false },
                    ["priority"] = new McpToolParameter { Type = "integer", Description = "対応する操作で使用する作業優先度 1-9。既定値は 5", Required = false },
                    ["topPriority"] = new McpToolParameter { Type = "boolean", Description = "対応する操作で、赤色の最優先にするか", Required = false },
                    ["mode"] = new McpToolParameter { Type = "string", Description = "attack/capture は mark、cancel に対応する。capture は release にも対応する", Required = false, EnumValues = new List<string> { "mark", "cancel", "release" } },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "capture に対応。指定・取り消し・解放される対象をプレビューするだけで、実際には変更しない。dryRun では confirm は不要", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "attack に対応。友好的・協力的な陣営の対象への攻撃指定を許可する", Required = false },
                    ["attackAreaConfirm"] = new McpToolParameter { Type = "string", Description = "attack の領域 mark の追加確認。attack area と正確に入力する必要がある", Required = false },
                    ["paused"] = new McpToolParameter { Type = "boolean", Description = "manual_delivery に対応。true は手動補充を一時停止し、false は再開する", Required = false },
                    ["capacityKg"] = new McpToolParameter { Type = "number", Description = "manual_delivery に対応。設定する貯蔵量の上限（kg）", Required = false },
                    ["refillMassKg"] = new McpToolParameter { Type = "number", Description = "manual_delivery に対応。補充のしきい値（kg）", Required = false },
                    ["minimumMassKg"] = new McpToolParameter { Type = "number", Description = "manual_delivery に対応。1回の運搬の最小質量（kg）", Required = false },
                    ["requestNow"] = new McpToolParameter { Type = "boolean", Description = "manual_delivery に対応。直ちに運搬を1回要求する", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "危険な操作の確認。deconstruct/attack/cut_conduits、または広範囲の capture/empty_conduits では、元の規則に従って指定する", Required = false }
                }),
                Handler = args =>
                {
                    string action = NormalizeDesignationAction(args["action"]?.ToString());
                    switch (action)
                    {
                        case "deconstruct":
                        case "deconstruct_building":
                            return DeconstructBuilding().Handler(args);
                        case "attack":
                            return Attack().Handler(WithRoutedMode(args));
                        case "capture":
                        case "wrangle":
                            return CaptureCritters().Handler(WithRoutedMode(args));
                        case "empty_conduits":
                        case "empty_pipe":
                            return EmptyConduits().Handler(args);
                        case "cut_conduits":
                        case "cut":
                            return CutConduits().Handler(args);
                        case "manual_delivery":
                        case "delivery":
                            return ConfigureManualDelivery().Handler(args);
                        default:
                    return CallToolResult.Error("action must be deconstruct/拆, attack/杀, capture/捕, empty_conduits, cut_conduits, or manual_delivery");
                    }
                }
            };
        }

        private static CallToolResult RunAreaAction(JObject args)
        {
            string action = NormalizeAreaAction(args["action"]?.ToString());
            switch (action)
            {
                case "dig":
                    return DigArea().Handler(args);
                case "sweep":
                    return SweepArea().Handler(args);
                case "mop":
                    return MopArea().Handler(args);
                case "disinfect":
                    return DisinfectArea().Handler(args);
                case "cancel":
                    return CancelArea().Handler(args);
                case "harvest":
                    return HarvestArea().Handler(args);
                default:
                    return CallToolResult.Error("action must be dig/挖, sweep/扫, mop/擦, disinfect/毒, cancel/消, or harvest/收");
            }
        }

        private static string NormalizeAreaAction(string action)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "挖":
                case "挖掘":
                case "开挖":
                    return "dig";
                case "扫":
                case "清":
                case "清扫":
                case "清理":
                case "打扫":
                case "捡":
                case "捡起":
                case "拾取":
                case "搬运":
                case "收拾":
                case "pickup":
                case "pick_up":
                case "clear":
                case "clean":
                    return "sweep";
                case "擦":
                case "擦拭":
                case "擦水":
                case "拖":
                case "拖地":
                case "wipe":
                    return "mop";
                case "毒":
                case "消毒":
                case "杀菌":
                case "灭菌":
                case "sanitize":
                    return "disinfect";
                case "收":
                case "收获":
                case "收割":
                case "采收":
                    return "harvest";
                case "消":
                case "取消":
                case "取消任务":
                case "取消命令":
                    return "cancel";
            default:
                return (action ?? string.Empty).Trim().ToLowerInvariant();
            }
        }

        private static string NormalizeDesignationAction(string action)
        {
            switch ((action ?? string.Empty).Trim().ToLowerInvariant())
            {
            case "拆":
            case "拆除":
            case "拆毁":
            case "拆掉":
                return "deconstruct";
            case "杀":
            case "攻击":
            case "打":
            case "进攻":
                return "attack";
                case "捕":
                case "捕捉":
                case "抓捕":
                case "抓":
                case "捕获":
                    return "capture";
            case "清管":
            case "清空管道":
                return "empty_conduits";
            case "剪":
            case "剪断":
            case "剪管":
                return "cut_conduits";
            case "送":
            case "送货":
            case "手动送货":
                return "manual_delivery";
            default:
                return (action ?? string.Empty).Trim().ToLowerInvariant();
            }
        }

        private static int RectCellCount(Dictionary<string, int> rect)
        {
            return (rect["x2"] - rect["x1"] + 1) * (rect["y2"] - rect["y1"] + 1);
        }

        private static bool HasRectInput(Newtonsoft.Json.Linq.JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                || (args["x1"] != null && args["y1"] != null && args["x2"] != null && args["y2"] != null);
        }
}
}
