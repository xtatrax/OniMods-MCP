using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static bool TryParseCellSnapshotPath(string relative, out int x, out int y)
        {
            x = 0;
            y = 0;
            const string prefix = "map/cell_";
            const string suffix = ".md";
            if (string.IsNullOrEmpty(relative)
                || !relative.StartsWith(prefix, StringComparison.Ordinal)
                || !relative.EndsWith(suffix, StringComparison.Ordinal))
                return false;

            string body = relative.Substring(prefix.Length, relative.Length - prefix.Length - suffix.Length);
            string[] parts = body.Split('_');
            return parts.Length == 2 && int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y);
        }

        private static string ReadCellSnapshotMarkdown(JObject args, int x, int y)
        {
            int cell = MapTextReadPolicy.Inside(x, y, Grid.WidthInCells, Grid.HeightInCells)
                ? Grid.XYToCell(x, y) : Grid.InvalidCell;
            var sb = new StringBuilder();
            sb.AppendLine("# Cell (" + x + "," + y + ") Cross-View Snapshot");
            sb.AppendLine();

            if (!IsReadableMapCell(cell))
            {
                sb.AppendLine("- 状態: 無効なセル");
                return sb.ToString();
            }

            AppendCellBaseSnapshot(sb, cell);
            AppendCellObjectSnapshot(sb, x, y, cell);
            AppendCellInfrastructureSnapshot(sb, cell);
            AppendCellPortSnapshot(sb, cell);
            AppendCellItemSnapshot(sb, cell, args);
            AppendCellPickupDetailSnapshot(sb, cell, args);
            AppendCellPickupSummary(sb, cell);
            AppendCellDecisionHints(sb, x, y, cell);
            AppendCellQuickOps(sb, x, y, cell);
            AppendCellNextReads(sb, x, y, cell);
            sb.AppendLine();
            sb.AppendLine("## Links");
            sb.AppendLine("- `/active/map/viewport.md`");
            sb.AppendLine("- `/active/infrastructure/power.md`");
            sb.AppendLine("- `/active/infrastructure/liquid_conduits.md`");
            sb.AppendLine("- `/active/infrastructure/gas_conduits.md`");
            sb.AppendLine("- `/active/infrastructure/logic.md`");
            sb.AppendLine("- `/active/infrastructure/solid_conveyor.md`");
            return sb.ToString();
        }

        private static void AppendCellQuickOps(StringBuilder sb, int x, int y, int cell)
        {
            sb.AppendLine();
            sb.AppendLine("## Quick Ops");
            sb.AppendLine("- execute: paste one command into `/active/ops/orders.md` under `## Edit Commands`; keep `dryRun=true` until the preview is clean.");
            sb.AppendLine("- 採掘: `挖 @(" + x + "," + y + "):7 dryRun=true`");
            sb.AppendLine("- モップ掛け: `擦 @(" + x + "," + y + "):6 dryRun=true`");
            sb.AppendLine("- 片付け: `扫 @(" + x + "," + y + "):6 dryRun=true`");
            sb.AppendLine("- キャンセル: `消 @(" + x + "," + y + ") dryRun=true`");
            GameObject building = CellBuildingObject(cell);
            if (building != null)
                sb.AppendLine("- 解体: `拆 建筑@(" + x + "," + y + "):7 dryRun=true`");
            if (BuildCritterCellMap().ContainsKey(cell))
            {
                sb.AppendLine("- 捕獲: `捕 小动物@(" + x + "," + y + "):7 dryRun=true`");
                sb.AppendLine("- 攻撃: `杀 小动物@(" + x + "," + y + "):7 dryRun=true`");
            }
        }

        private static void AppendCellBaseSnapshot(StringBuilder sb, int cell)
        {
            Element element = Grid.Element[cell];
            string elementName = element != null ? StripLinkTags(element.name) : "Unknown";
            string elementId = element != null ? element.id.ToString() : "Unknown";
            sb.AppendLine("## Base");
            sb.AppendLine("- Cell: " + cell);
            sb.AppendLine("- 元素: " + elementName + " (" + elementId + ")");
            sb.AppendLine("- 質量: " + Grid.Mass[cell].ToString("F2") + " kg");
            float tempC = Grid.Temperature[cell] - 273.15f;
            sb.AppendLine("- 温度: " + tempC.ToString("F1") + "°C"
                + (element != null && element.IsVacuum ? "（ゲーム内セル値。真空の熱危険判定には使用しない）" : ""));
            sb.AppendLine("- 温度の適合性: " + AmbientTemperaturePolicy.MapText(ToolUtil.GetElementState(element), tempC));
            sb.AppendLine("- 温度の適合性詳細: band=" + AmbientTemperaturePolicy.MapBand(ToolUtil.GetElementState(element), tempC)
                + " dupeComfortC=10~40 cautionC=-20~10/40~72 dangerC=<=-20/>=72");
            sb.AppendLine("- 環境判定: " + CellEnvironmentText(element, Grid.Mass[cell], tempC));
            sb.AppendLine("- 埋蔵物マーカー: " + ReadBuriedMarker(cell)
                + " (source=EntombedItemVisualizer.IsEntombedItem; visible solid cells only; contents unknown; read-only)");
        }

        private static string CellEnvironmentText(Element element, float massKg, float tempC)
        {
            string phase = ToolUtil.GetElementState(element);
            string breathable = IsBreathableElement(element, massKg) ? "yes" : "no";
            string pressure = CellPressureBand(element, massKg);
            string liquidRisk = element != null && element.IsLiquid && massKg > 1f ? "yes" : "no";
            return "phase=" + phase
                + " breathable=" + breathable
                + " pressure=" + pressure
                + " liquidRisk=" + liquidRisk
                + " tempBand=" + AmbientTemperaturePolicy.MapBand(ToolUtil.GetElementState(element), tempC);
        }

        private static bool IsBreathableElement(Element element, float massKg)
        {
            return element != null
                && massKg > 0.01f
                && (element.id == SimHashes.Oxygen || element.id == SimHashes.ContaminatedOxygen);
        }

        private static string CellPressureBand(Element element, float massKg)
        {
            if (element == null) return "unknown";
            if (element.IsVacuum) return "vacuum";
            if (element.IsLiquid)
                return massKg > 1f ? "liquid_blocking" : "liquid_trace";
            if (!element.IsGas)
                return "solid";
            if (massKg < 0.05f)
                return "near_vacuum";
            if (massKg < 0.2f)
                return "thin";
            if (massKg > 3f)
                return "overpressure";
            return "normal";
        }

        private static void AppendCellObjectSnapshot(StringBuilder sb, int x, int y, int cell)
        {
            sb.AppendLine();
            sb.AppendLine("## Objects");
            GameObject building = CellBuildingObject(cell);
            GameObject digPlacer = Grid.Objects[cell, (int)ObjectLayer.DigPlacer];
            GameObject minion = Grid.Objects[cell, (int)ObjectLayer.Minion];
            BuildCritterCellMap().TryGetValue(cell, out var critter);
            var cellBuildings = NativeBuildingCellIndex.At(cell);
            if (building != null && !cellBuildings.Contains(building)) cellBuildings = cellBuildings.Concat(new[] { building }).ToList();
            if (cellBuildings.Count == 0) sb.AppendLine("- 設備: なし");
            foreach (var go in cellBuildings) sb.AppendLine("- 設備: " + FormatCellGameObject(go));
            sb.AppendLine("- 採掘指示: " + FormatDigOrder(digPlacer));
            sb.AppendLine("- 複製人間/バイオニック複製人間: " + FormatCellDupe(minion));
            sb.AppendLine("- 動物: " + FormatCellGameObject(critter));

            if (building != null || minion != null || critter != null)
                sb.AppendLine("- マップ記号: " + FormatMapCellToken(
                    OverlayModes.None.ID,
                    ResolveMapSymbol(OverlayModes.None.ID, cell, "", "", CellPrefabId(building), CellProperName(building), building, minion, critter, 0f),
                    x, y, cell, building, minion, critter, CellPrefabId(building), CellProperName(building), null, out _));
        }

        private static string FormatDigOrder(GameObject digPlacer)
        {
            if (digPlacer == null)
                return "なし";
            var prioritizable = digPlacer.GetComponent<Prioritizable>();
            if (prioritizable == null)
                return "あり; priority=unknown";
            var setting = prioritizable.GetMasterPriority();
            return "あり; priority=" + setting.priority_value
                + "; class=" + setting.priority_class;
        }

        private static void AppendCellInfrastructureSnapshot(StringBuilder sb, int cell)
        {
            sb.AppendLine();
            sb.AppendLine("## Infrastructure");
            sb.AppendLine("- 電力: " + CellConnectionText(OverlayModes.Power.ID, cell, "wire", PowerLayers));
            sb.AppendLine("- 液体配管: " + CellConnectionText(OverlayModes.LiquidConduits.ID, cell, "liquid", LiquidLayers));
            sb.AppendLine("- 気体配管: " + CellConnectionText(OverlayModes.GasConduits.ID, cell, "gas", GasLayers));
            sb.AppendLine("- 信号: " + CellConnectionText(OverlayModes.Logic.ID, cell, "logic", LogicLayers));
            sb.AppendLine("- 輸送: " + CellConnectionText(OverlayModes.SolidConveyor.ID, cell, "rail", ConveyorLayers));

            GameObject building = CellBuildingObject(cell);
            string powerInfo = GetPowerInfo(building);
            if (!string.IsNullOrEmpty(powerInfo))
                sb.AppendLine("- 電力設備: " + powerInfo);
            AppendCellEndpointSnapshot(sb, "電力", OverlayModes.Power.ID, cell);
            AppendCellEndpointSnapshot(sb, "液体配管", OverlayModes.LiquidConduits.ID, cell);
            AppendCellEndpointSnapshot(sb, "気体配管", OverlayModes.GasConduits.ID, cell);
            AppendCellEndpointSnapshot(sb, "信号", OverlayModes.Logic.ID, cell);
            AppendCellEndpointSnapshot(sb, "輸送", OverlayModes.SolidConveyor.ID, cell);
            AppendCellInfrastructureDetailHint(sb, cell);
        }

        private static void AppendCellEndpointSnapshot(StringBuilder sb, string label, HashedString mode, int cell)
        {
            string endpoint = EndpointDetailLine(mode, cell);
            if (!string.IsNullOrEmpty(endpoint))
                sb.AppendLine("- ポート(" + label + "): " + TrimListBullet(endpoint));
        }

        private static string TrimListBullet(string text)
        {
            return text != null && text.StartsWith("- ", StringComparison.Ordinal)
                ? text.Substring(2)
                : text;
        }

        private static string CellConnectionText(HashedString mode, int cell, string layerName, ObjectLayer[] layers)
        {
            char glyph = mode == OverlayModes.Power.ID
                ? ResolvePowerConnectionSymbol(cell)
                : ResolveUtilityConnectionSymbol(cell, layers);
            bool connectionsKnown = TryGetUtilityConnections(cell, layers, out _);
            var dirs = ConnectionDirections(cell, layers, mode == OverlayModes.Power.ID);
            string text = "glyph=" + glyph
                + " dirs=" + (!connectionsKnown ? "?" : dirs.Count == 0 ? "." : string.Join("", dirs.Select(d => d.Dir).ToArray()))
                + " links=" + (connectionsKnown ? ConnectionLinkText(dirs) : "?")
                 + " open=" + (connectionsKnown ? OpenAdjacentConnectionText(cell, layers, dirs) : "?")
            + " to=" + (!connectionsKnown ? "?" : dirs.Count == 0 ? "." : string.Join(",", dirs.Select(d => CellCoord(d.Cell)).ToArray()));
            text += " " + UtilityConnectionReadState(cell, layers);
            if (mode == OverlayModes.Power.ID)
                text += " " + PowerCircuitText(cell);
            string bridge = BridgeText(cell, mode);
            if (!string.IsNullOrEmpty(bridge))
                text += " " + bridge;
            return "layer=" + layerName + " " + text;
        }

        private static void AppendCellItemSnapshot(StringBuilder sb, int cell, JObject args)
        {
            int limit = CellPickupItemLimit(args);
            var allItems = Components.Pickupables.Items
                .Where(item => PickupableTouchesCell(item, cell))
                .ToList();

            if (allItems.Count == 0)
                return;

            var items = allItems.Take(limit)
                .Select(item => "- " + StripLinkTags(item.GetProperName()) + " | "
                    + (item.PrimaryElement != null ? item.PrimaryElement.Mass.ToString("F2") + " kg" : "mass=?"))
                .ToList();

            sb.AppendLine();
            sb.AppendLine("## Items");
            sb.AppendLine("- total=" + allItems.Count
                + ", shown=" + items.Count
                + ", truncated=" + (items.Count < allItems.Count).ToString().ToLowerInvariant()
                + ", itemLimit=" + limit);
            foreach (string item in items)
                sb.AppendLine(item);
            if (items.Count < allItems.Count)
                sb.AppendLine("- ... +" + (allItems.Count - items.Count) + " more; reread with `itemLimit=" + allItems.Count + "` or `includeAllItems=true`.");
        }

        private static char InfrastructureGlyph(int cell, ObjectLayer[] layers)
        {
            return ResolveUtilityConnectionSymbol(cell, layers);
        }

        private static string FormatCellDupe(GameObject go)
        {
            if (go == null)
                return "なし";

            return GetDupeMapKind(go) + "@" + MapTokenPart(StripLinkTags(go.GetProperName()))
                + " | " + ObjectLocationText(go);
        }

        private static string FormatCellGameObject(GameObject go)
        {
            if (go == null)
                return "なし";

            string name = CellProperName(go);
            string id = CellPrefabId(go);
            string label = string.IsNullOrEmpty(id) ? name : name + " (ID=" + id + ")";
            return label + " | " + ObjectLocationText(go) + BuildingFootprintText(go);
        }

        private static string ObjectLocationText(GameObject go)
        {
            int objectCell = Grid.PosToCell(go);
            if (!Grid.IsValidCell(objectCell))
                return "判定点=invalid";

            var pos = go.transform.position;
            return "判定点=(" + Grid.CellColumn(objectCell) + "," + Grid.CellRow(objectCell) + ")"
                + ", Cell=" + objectCell
                + ", Pos=(" + pos.x.ToString("F2") + "," + pos.y.ToString("F2") + ")";
        }

        private static string BuildingFootprintText(GameObject go)
        {
            var building = go.GetComponent<Building>();
            BuildingDef def = building != null ? building.Def : null;
            if (def == null)
                return string.Empty;

            var shape = NativeBuildingCellIndex.Geometry(go);
            if (!shape.Available) return ", geometry=unknown, reason=" + shape.Error;
            var b = shape.Bounds;
            return ", footprint=" + shape.Size[0] + "x" + shape.Size[1]
                + ", bottomLeft=(" + b[0] + "," + b[1] + ")"
                + ", origin=(" + shape.Origin.X + "," + shape.Origin.Y + ")"
                + ", occupied=" + shape.Cells.Count + ", fillsBounds=" + shape.FillsBounds
                + ", orientation=" + shape.Orientation + ", rule=" + def.BuildLocationRule
                + ", source=native_PlacementOffsets";
        }

        private static string CellProperName(GameObject go)
        {
            return go == null ? string.Empty : StripLinkTags(go.GetProperName());
        }

        private static string CellPrefabId(GameObject go)
        {
            var prefab = go != null ? go.GetComponent<KPrefabID>() : null;
            return prefab != null && prefab.PrefabTag.IsValid ? prefab.PrefabTag.Name : string.Empty;
        }
    }
}
