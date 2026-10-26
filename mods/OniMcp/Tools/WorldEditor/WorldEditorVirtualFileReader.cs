using System;
using System.IO;
using System.Text;
using System.Net;
using UnityEngine;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private const int VirtualMapLayerSize = 32;

        public static string ReadFileDirectly(string path)
        {
            if (path == "/active/map/viewport.md" || path == "/active/map/index.md")
                return ReadMapFileWithArgs(new JObject(), path);
            if (path.StartsWith("/active/map/layers/", StringComparison.Ordinal)
                && path.EndsWith(".md", StringComparison.Ordinal))
                return ReadMapLayerWithArgs(new JObject(), path);
            HashedString activeMode = OverlayScreen.Instance != null ? OverlayScreen.Instance.mode : OverlayModes.None.ID;
            string viewName = GetOverlayViewName(activeMode);

            if (path == "/active/screenshots/index.md")
                return ReadScreenshotsIndexMarkdown();

            if (path == "/active/map/viewport.html" || path == "/active/map/index.html")
            {
                try
                {
                    if (!TryGetCameraBounds(out int xMin, out int xMax, out int yMin, out int yMax))
                        return "<h1>Error</h1><p>Camera not initialized or main camera is not available.</p>";

                    int width = xMax - xMin + 1;

                    string cellLabel = McpLocalization.Get("map_html.tooltip.cell");
                    string elementLabel = McpLocalization.Get("map_html.tooltip.element");
                    string temperatureLabel = McpLocalization.Get("map_html.tooltip.temperature");
                    string buildingLabel = McpLocalization.Get("map_html.tooltip.building");

                    var sbCells = new StringBuilder();
                    for (int y = yMax; y >= yMin; y--)
                    {
                        for (int x = xMin; x <= xMax; x++)
                        {
                            int cell = Grid.XYToCell(x, y);
                            if (!IsReadableMapCell(cell))
                            {
                                sbCells.AppendFormat("<div class=\"cell\" data-state=\"unknown\" style=\"background:#171d29;\" title=\"{0}\"></div>",
                                    WebUtility.HtmlEncode(McpLocalization.Get("map_html.unreadable")));
                                continue;
                            }
                            string elemName = "Vacuum";
                            string color = "#1a202c";
                            float temp = 0f;
                            string bldName = "";
                            GameObject go = null;

                            if (Grid.IsValidCell(cell))
                            {
                                var elem = Grid.Element[cell];
                                temp = Grid.Temperature[cell];
                                if (elem != null)
                                    elemName = elem.id.ToString();

                                go = Grid.Objects[cell, (int)ObjectLayer.Building];
                                if (go != null)
                                {
                                    var cmp = go.GetComponent<BuildingComplete>();
                                    bldName = cmp != null ? cmp.name : go.name;
                                }

                                color = GetHtmlCellColor(cell, elem, go, activeMode, temp, bldName);
                            }

                            string tooltip = $"{cellLabel}: {cell} ({x}, {y})\n{elementLabel}: {elemName}\n{temperatureLabel}: {temp - 273.15f:F1}°C";
                            if (!string.IsNullOrEmpty(bldName))
                            {
                                tooltip += $"\n{buildingLabel}: {bldName}";
                            }

                            sbCells.AppendFormat("<div class=\"cell\" style=\"background:{0};\" title=\"{1}\"></div>", color, WebUtility.HtmlEncode(tooltip));
                        }
                    }

                    string legendHtml = "";
                    if (activeMode == OverlayModes.Temperature.ID)
                    {
                        legendHtml = @"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#90cdf4;""></div>絶対零度 (&lt;-260°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#00b5d8;""></div>寒冷 (-260°C ~ -18°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#4299e1;""></div>氷点下 (-18°C ~ 0°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#48bb78;""></div>穏やか (0°C ~ 20°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>温暖 (20°C ~ 35°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ed8936;""></div>高温 (35°C ~ 100°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#e57373;""></div>灼熱 (100°C ~ 1000°C)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#e53e3e;""></div>溶融 (&gt;1000°C)</div>";
                    }
                    else if (activeMode == OverlayModes.Power.ID)
                    {
                        legendHtml = @"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#dd6b20;""></div>電力設備 (Power Device)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>電線 (Wire)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>その他 (Others)</div>";
                    }
                    else if (activeMode == OverlayModes.Oxygen.ID)
                    {
                        legendHtml = @"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#48bb78;""></div>呼吸しやすい (Breathable, &gt;=600g)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>呼吸可能 (Passable, 100g~600g)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ed8936;""></div>呼吸困難 (Difficult, &lt;100g)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#e53e3e;""></div>呼吸不可 (Unbreathable)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>固体ブロック (Solid Block)</div>";
                    }
                    else if (activeMode == OverlayModes.LiquidConduits.ID)
                    {
                        legendHtml = @"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#3182ce;""></div>液体配管 (Liquid Pipe)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>その他 (Others)</div>";
                    }
                    else if (activeMode == OverlayModes.GasConduits.ID)
                    {
                        legendHtml = @"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#38a169;""></div>気体配管 (Gas Pipe)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>その他 (Others)</div>";
                    }
                    else if (activeMode == OverlayModes.Light.ID)
                    {
                        legendHtml = @"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#fffbeb;""></div>日焼けするほどの強い光 (Sunburn, &gt;=72500 Lux)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#fef08a;""></div>作業に適した明るさ (Bright, 1000~72500 Lux)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#fde047;""></div>栽培向けの通常の明るさ (Normal, 200~1000 Lux)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ca8a04;""></div>微光 (Dim, &lt;200 Lux)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#111827;""></div>暗闇 (Dark, 0 Lux)</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>固体ブロック (Solid Block)</div>";
                    }
                    else
                    {
                        legendHtml = $@"
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#1a202c;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.vacuum_space"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.building"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#8c5b30;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.dirt"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#a88060;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.sandstone"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#3182ce;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.water"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#4a3728;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.polluted_water"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#38a169;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.algae"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#718096;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.granite_rock"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#e6fffa;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.oxygen_gas"))}</div>
                            <div class=""legend-item""><div class=""legend-color"" style=""background:#feb2b2;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.carbon_dioxide"))}</div>";
                    }

                    return GetMapHtml(
                        WebUtility.HtmlEncode(McpLocalization.Format("map_html.viewport_title", null, viewName, xMin, xMax, yMin, yMax)),
                        "/active/map/",
                        WebUtility.HtmlEncode(McpLocalization.Get("map_html.viewport_back")),
                        WebUtility.HtmlEncode(McpLocalization.Get("map_html.viewport_description")),
                        width,
                        sbCells.ToString(),
                        legendHtml
                    );
                }
                catch (Exception ex)
                {
                    return $"<h1>Error rendering viewport map</h1><p>{ex.Message}</p>";
                }
            }

            if (path.StartsWith("/active/", StringComparison.Ordinal)
                && TryParseCellSnapshotPath(SaveRelativePath(path), out int cellX, out int cellY))
                return ReadCellSnapshotMarkdown(new JObject(), cellX, cellY);

            if (path.StartsWith("/active/", StringComparison.Ordinal)
                && TryParseZoomPath(SaveRelativePath(path), out int zoomX1, out int zoomY1, out int zoomX2, out int zoomY2))
            {
                ValidateMapCellBudget(new JObject(), zoomX1, zoomX2, zoomY1, zoomY2);
                return ReadZoomMarkdown(zoomX1, zoomY1, zoomX2, zoomY2, DefaultZoomViews());
            }

            if (path.StartsWith("/active/map/layers/layer_", StringComparison.Ordinal) && path.EndsWith(".html", StringComparison.Ordinal))
            {
                try
                {
                    string filename = Path.GetFileNameWithoutExtension(path);
                    string parts = filename.Substring("layer_".Length);
                    string[] split = parts.Split('_');
                    if (split.Length == 2
                        && int.TryParse(split[0], out int relYMin)
                        && int.TryParse(split[1], out int relYMax))
                    {
                        int worldId = ClusterManager.Instance?.activeWorldId ?? 0;
                        var world = ClusterManager.Instance?.GetWorld(worldId);
                        if (world != null)
                        {
                            int height = world.WorldSize.y;
                            if (relYMin < 0
                                || relYMax < relYMin
                                || relYMax >= height
                                || relYMin % VirtualMapLayerSize != 0)
                            {
                                return "<h1>" + WebUtility.HtmlEncode(McpLocalization.Get("map_html.invalid_layer.title"))
                                    + "</h1><p>" + WebUtility.HtmlEncode(McpLocalization.Get("map_html.invalid_layer.description")) + "</p>";
                            }

                            int expectedRelYMax = Math.Min(relYMin + VirtualMapLayerSize - 1, height - 1);
                            if (relYMax != expectedRelYMax)
                            {
                                return "<h1>" + WebUtility.HtmlEncode(McpLocalization.Get("map_html.invalid_layer.title"))
                                    + "</h1><p>" + WebUtility.HtmlEncode(McpLocalization.Get("map_html.invalid_layer.description")) + "</p>";
                            }

                            int xMin = world.WorldOffset.x;
                            int xMax = world.WorldOffset.x + world.WorldSize.x - 1;
                            int yMin = world.WorldOffset.y + relYMin;
                            int yMax = world.WorldOffset.y + relYMax;
                            int width = world.WorldSize.x;

                            string cellLabel = McpLocalization.Get("map_html.tooltip.cell");
                            string elementLabel = McpLocalization.Get("map_html.tooltip.element");
                            string temperatureLabel = McpLocalization.Get("map_html.tooltip.temperature");
                            string buildingLabel = McpLocalization.Get("map_html.tooltip.building");

                            var sbCells = new StringBuilder();
                            for (int y = yMax; y >= yMin; y--)
                            {
                                for (int x = xMin; x <= xMax; x++)
                                {
                                    int cell = Grid.XYToCell(x, y);
                                    if (!IsReadableMapCell(cell))
                                    {
                                        sbCells.AppendFormat("<div class=\"cell\" data-state=\"unknown\" style=\"background:#171d29;\" title=\"{0}\"></div>",
                                            WebUtility.HtmlEncode(McpLocalization.Get("map_html.unreadable")));
                                        continue;
                                    }
                                    string elemName = "Vacuum";
                                    string color = "#1a202c";
                                    float temp = 0f;
                                    string bldName = "";
                                    GameObject go = null;

                                    if (Grid.IsValidCell(cell))
                                    {
                                        var elem = Grid.Element[cell];
                                        temp = Grid.Temperature[cell];
                                        if (elem != null)
                                            elemName = elem.id.ToString();

                                        go = Grid.Objects[cell, (int)ObjectLayer.Building];
                                        if (go != null)
                                        {
                                            var cmp = go.GetComponent<BuildingComplete>();
                                            bldName = cmp != null ? cmp.name : go.name;
                                        }

                                        color = GetHtmlCellColor(cell, elem, go, activeMode, temp, bldName);
                                    }

                                    string tooltip = $"{cellLabel}: {cell} ({x}, {y})\n{elementLabel}: {elemName}\n{temperatureLabel}: {temp - 273.15f:F1}°C";
                                    if (!string.IsNullOrEmpty(bldName))
                                    {
                                        tooltip += $"\n{buildingLabel}: {bldName}";
                                    }

                                    sbCells.AppendFormat("<div class=\"cell\" style=\"background:{0};\" title=\"{1}\"></div>", color, WebUtility.HtmlEncode(tooltip));
                                }
                            }

                            string legendHtml = "";
                            if (activeMode == OverlayModes.Temperature.ID)
                            {
                                legendHtml = @"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#90cdf4;""></div>絶対零度 (&lt;-260°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#00b5d8;""></div>寒冷 (-260°C ~ -18°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#4299e1;""></div>氷点下 (-18°C ~ 0°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#48bb78;""></div>穏やか (0°C ~ 20°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>温暖 (20°C ~ 35°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ed8936;""></div>高温 (35°C ~ 100°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#e57373;""></div>灼熱 (100°C ~ 1000°C)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#e53e3e;""></div>溶融 (&gt;1000°C)</div>";
                            }
                            else if (activeMode == OverlayModes.Power.ID)
                            {
                                legendHtml = @"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#dd6b20;""></div>電力設備 (Power Device)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>電線 (Wire)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>その他 (Others)</div>";
                            }
                            else if (activeMode == OverlayModes.Oxygen.ID)
                            {
                                legendHtml = @"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#48bb78;""></div>呼吸しやすい (Breathable, &gt;=600g)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>呼吸可能 (Passable, 100g~600g)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ed8936;""></div>呼吸困難 (Difficult, &lt;100g)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#e53e3e;""></div>呼吸不可 (Unbreathable)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>固体ブロック (Solid Block)</div>";
                            }
                            else if (activeMode == OverlayModes.LiquidConduits.ID)
                            {
                                legendHtml = @"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#3182ce;""></div>液体配管 (Liquid Pipe)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>その他 (Others)</div>";
                            }
                            else if (activeMode == OverlayModes.GasConduits.ID)
                            {
                                legendHtml = @"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#38a169;""></div>気体配管 (Gas Pipe)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>その他 (Others)</div>";
                            }
                            else if (activeMode == OverlayModes.Light.ID)
                            {
                                legendHtml = @"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#fffbeb;""></div>日焼けするほどの強い光 (Sunburn, &gt;=72500 Lux)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#fef08a;""></div>作業に適した明るさ (Bright, 1000~72500 Lux)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#fde047;""></div>栽培向けの通常の明るさ (Normal, 200~1000 Lux)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ca8a04;""></div>微光 (Dim, &lt;200 Lux)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#111827;""></div>暗闇 (Dark, 0 Lux)</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#2d3748;""></div>固体ブロック (Solid Block)</div>";
                            }
                            else
                            {
                                legendHtml = $@"
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#1a202c;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.vacuum_space"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#ecc94b;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.building"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#8c5b30;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.dirt"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#a88060;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.sandstone"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#3182ce;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.water"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#4a3728;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.polluted_water"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#38a169;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.algae"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#718096;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.granite_rock"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#e6fffa;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.oxygen_gas"))}</div>
                                    <div class=""legend-item""><div class=""legend-color"" style=""background:#feb2b2;""></div>{WebUtility.HtmlEncode(McpLocalization.Get("map_html.legend.carbon_dioxide"))}</div>";
                            }

                            return GetMapHtml(
                                WebUtility.HtmlEncode(McpLocalization.Format("map_html.layer_title", null, viewName, relYMin, relYMax)),
                                "/active/map/layers/",
                                WebUtility.HtmlEncode(McpLocalization.Get("map_html.layer_back")),
                                WebUtility.HtmlEncode(McpLocalization.Get("map_html.layer_description")),
                                width,
                                sbCells.ToString(),
                                legendHtml
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    return $"<h1>Error rendering layer</h1><p>{ex.Message}</p>";
                }
            }
            return "<h1>File not found</h1>";
        }

    }
}
