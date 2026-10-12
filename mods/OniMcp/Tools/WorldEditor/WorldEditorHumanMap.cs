using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Browser-only snapshot. Every Unity/Grid access runs on MainThreadBridge.
        // Rows are yMin -> yMax; the canvas explicitly flips Y for display.
        public static object BuildHumanMapSnapshot()
        {
            if (Game.Instance == null || ClusterManager.Instance == null)
                throw new InvalidOperationException("Game not initialized");
            int worldId = ClusterManager.Instance.activeWorldId;
            var world = ClusterManager.Instance.GetWorld(worldId);
            if (world == null) throw new InvalidOperationException("Active world not available");
            int width = world.WorldSize.x, height = world.WorldSize.y;
            int xMin = world.WorldOffset.x, yMin = world.WorldOffset.y;
            int length = checked(width * height);
            var elements = new int[length];
            var temperatures = new float?[length];
            var masses = new float?[length];
            var visible = new int[length];
            var networks = new int[length];
            var palette = new List<object>();
            var indices = new Dictionary<SimHashes, int>();
            palette.Add(new { id = "Unavailable", name = McpLocalization.Get("humanMap.paletteUnavailable"), color = "#171d29", state = "unknown" });
            var layers = new[] { PowerLayers, LiquidLayers, GasLayers, LogicLayers, ConveyorLayers };
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int i = y * width + x, cell = Grid.XYToCell(xMin + x, yMin + y);
                if (!Grid.IsValidCell(cell) || !Grid.IsWorldValidCell(cell) || Grid.WorldIdx[cell] != worldId) continue;
                var elem = Grid.Element[cell];
                if (elem != null)
                {
                    if (!indices.TryGetValue(elem.id, out int index))
                    {
                        index = palette.Count; indices.Add(elem.id, index);
                        palette.Add(new { id = elem.id.ToString(), name = ToolUtil.CleanName(elem.name), color = HumanMapElementColor(elem), state = elem.IsSolid ? "solid" : elem.IsLiquid ? "liquid" : "gas" });
                    }
                    elements[i] = index;
                }
                float temp = Grid.Temperature[cell] - 273.15f, mass = Grid.Mass[cell];
                temperatures[i] = float.IsNaN(temp) || float.IsInfinity(temp) ? (float?)null : (float)Math.Round(temp, 1);
                masses[i] = float.IsNaN(mass) || float.IsInfinity(mass) ? (float?)null : (float)Math.Round(mass, 3);
                visible[i] = (byte)(Grid.IsVisible(cell) ? 1 : 0);
                for (int bit = 0; bit < layers.Length; bit++) if (HasLayer(cell, layers[bit])) networks[i] |= (byte)(1 << bit);
            }
            var buildings = new List<object>();
            foreach (var building in Components.BuildingCompletes.Items)
            {
                if (building == null || building.Def == null || !ToolUtil.GameObjectMatchesWorld(building.gameObject, worldId)) continue;
                // Share the geometry contract, including odd-width origins and rotated ports.
                buildings.Add(BuildingGeometryTools.Snapshot(building, building.Def, building.Orientation).ToDictionary(true));
            }
            var dupes = LiveDupes().Where(d => ToolUtil.GameObjectMatchesWorld(d.gameObject, worldId))
                .Select(d => { int c = Grid.PosToCell(d.gameObject); return new { name = ToolUtil.CleanName(d.GetProperName()), x = Grid.CellColumn(c), y = Grid.CellRow(c) }; }).ToArray();
            return new
            {
                readOnly = true, worldId, worldName = ToolUtil.CleanName(world.GetProperName()), saveName = GetActiveSaveName(),
                cycle = GameUtil.GetCurrentCycle(), cyclePercent = Math.Round((GameClock.Instance?.GetCurrentCycleAsPercentage() ?? 0) * 100, 1),
                paused = SpeedControlScreen.Instance != null && SpeedControlScreen.Instance.IsPaused,
                capturedAt = System.DateTime.UtcNow.ToString("o"), width, height, xMin, yMin,
                gameElementNames = new Dictionary<string, string>
                {
                    ["Oxygen"] = ToolUtil.CleanName(ElementLoader.FindElementByHash(SimHashes.Oxygen)?.name),
                    ["ContaminatedOxygen"] = ToolUtil.CleanName(ElementLoader.FindElementByHash(SimHashes.ContaminatedOxygen)?.name)
                },
                rowOrder = "y_ascending", palette, elements, temperatures, masses, visible, networks, buildings, dupes
            };
        }

        private static string HumanMapElementColor(Element elem)
        {
            string id = elem.id.ToString().ToLowerInvariant();
            // Specific variants must precede their base names.
            if (id.Contains("dirtyoxygen") || id.Contains("pollutedoxygen")) return "#a8ba55";
            if (id.Contains("saltwater") || id.Contains("brine")) return "#5a9ebc";
            if (id.Contains("vacuum")) return "#141b2a";
            return GetElementColor(elem);
        }
    }
}
