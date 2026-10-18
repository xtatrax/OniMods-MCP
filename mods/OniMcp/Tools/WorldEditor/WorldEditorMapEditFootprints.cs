using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private sealed class MapEditAnchorRequest
        {
            internal int X;
            internal int Y;
            internal List<MapEditCell> RequestedCells;
        }

        private static bool TryBuildAnchorsForPrefabFootprints(
            JObject parentArgs, string prefabId, IEnumerable<MapEditCell> cells,
            out JArray anchors, out string error)
        {
            return TryBuildAnchorsForPrefabFootprints(parentArgs, prefabId, cells, out anchors, out _, out error);
        }

        private static bool TryBuildAnchorsForPrefabFootprints(
            JObject parentArgs, string prefabId, IEnumerable<MapEditCell> cells,
            out JArray anchors, out List<MapEditAnchorRequest> requests, out string error)
        {
            anchors = new JArray();
            requests = new List<MapEditAnchorRequest>();
            error = null;
            var def = Assets.GetBuildingDef(prefabId);
            if (def == null)
            {
                foreach (var cell in cells)
                    AddMapAnchorRequest(anchors, requests, cell.X, cell.Y, new List<MapEditCell> { cell });
                return true;
            }

            string orientation = NormalizeMapEditOrientation(parentArgs);
            var shape = NativeBuildingFootprint.Definition(def, (Orientation)Enum.Parse(typeof(Orientation), orientation));
            if (!shape.Available)
            {
                error = "Native footprint unavailable for " + prefabId + ": " + shape.Error;
                return false;
            }
            int width = shape.Size[0], height = shape.Size[1];
            var occupied = new HashSet<string>(shape.Cells.Select(c => (c.X - shape.Bounds[0]) + "," + (c.Y - shape.Bounds[1])));

            if (width == 1 && height == 1)
            {
                foreach (var cell in cells)
                    AddMapAnchorRequest(anchors, requests, cell.X, cell.Y, new List<MapEditCell> { cell });
                return true;
            }

            // Disconnected masks cannot be distinguished from separate anchor markers.
            // Never turn one native building into several blueprints by accident.
            if (cells.Count() > 1 && ConnectedCellComponents(shape.Cells
                .Select(c => new MapEditCell { X = c.X, Y = c.Y })).Skip(1).Any())
            {
                error = "Disconnected native footprint for " + prefabId
                    + ": use one lower-left anchor marker per edit; a multi-cell mask is ambiguous.";
                return false;
            }

            foreach (var component in ConnectedCellComponents(cells))
            {
                if (!TryAddFootprintAnchor(prefabId, orientation, width, height, occupied, component, anchors, requests, out error))
                    return false;
            }

            return true;
        }

        private static bool TryAddFootprintAnchor(
            string prefabId,
            string orientation,
            int width,
            int height,
            HashSet<string> occupied,
            List<MapEditCell> component,
            JArray anchors,
            List<MapEditAnchorRequest> requests,
            out string error)
        {
            error = null;
            int minX = component.Min(c => c.X);
            int maxX = component.Max(c => c.X);
            int minY = component.Min(c => c.Y);
            int maxY = component.Max(c => c.Y);
            int actualWidth = maxX - minX + 1;
            int actualHeight = maxY - minY + 1;

            // Agent-friendly shorthand: a single changed cell is the lower-left anchor.
            // Explicit masks must match the native occupied cells, including holes and rotation.
            if (component.Count == 1 && actualWidth == 1 && actualHeight == 1)
            {
                AddMapAnchorRequest(anchors, requests, minX, minY, component);
                return true;
            }

            var present = new HashSet<string>(component.Select(c => (c.X - minX) + "," + (c.Y - minY)));
            if (actualWidth != width || actualHeight != height || !present.SetEquals(occupied))
            {
                error = $"Build token for {prefabId} covers {actualWidth}x{actualHeight}/{component.Count} cells, but native footprint is {width}x{height}/{occupied.Count} cells for orientation {orientation}. Use the exact occupied mask or only the lower-left anchor cell. Refusing ambiguous multi-cell edit.";
                return false;
            }

            AddMapAnchorRequest(anchors, requests, minX, minY, component);
            return true;
        }

        private static void AddMapAnchorRequest(JArray anchors, List<MapEditAnchorRequest> requests,
            int x, int y, List<MapEditCell> cells)
        {
            anchors.Add(new JObject { ["x"] = x, ["y"] = y });
            requests.Add(new MapEditAnchorRequest { X = x, Y = y, RequestedCells = cells });
        }

        private static IEnumerable<List<MapEditCell>> ConnectedCellComponents(IEnumerable<MapEditCell> cells)
        {
            var byKey = cells
                .GroupBy(c => c.X + "," + c.Y)
                .ToDictionary(g => g.Key, g => g.First());
            var remaining = new HashSet<string>(byKey.Keys);

            while (remaining.Count > 0)
            {
                string start = remaining.First();
                remaining.Remove(start);
                var component = new List<MapEditCell>();
                var queue = new Queue<string>();
                queue.Enqueue(start);

                while (queue.Count > 0)
                {
                    string key = queue.Dequeue();
                    var cell = byKey[key];
                    component.Add(cell);

                    foreach (var next in NeighborKeys(cell.X, cell.Y))
                    {
                        if (!remaining.Remove(next))
                            continue;
                        queue.Enqueue(next);
                    }
                }

                yield return component;
            }
        }

        private static IEnumerable<string> NeighborKeys(int x, int y)
        {
            yield return (x + 1) + "," + y;
            yield return (x - 1) + "," + y;
            yield return x + "," + (y + 1);
            yield return x + "," + (y - 1);
        }

        private static string NormalizeMapEditOrientation(JObject args)
        {
            string value = args?["orientation"]?.ToString();
            if (string.IsNullOrWhiteSpace(value))
                value = args?["rotation"]?.ToString();

            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "90":
                case "r90":
                case "right":
                case "clockwise":
                case "cw":
                    return "R90";
                case "fliph":
                    return "FlipH";
                case "flipv":
                    return "FlipV";
                case "180":
                case "r180":
                    return "R180";
                case "270":
                case "-90":
                case "r270":
                case "left":
                case "counterclockwise":
                case "ccw":
                    return "R270";
                default:
                    return "Neutral";
            }
        }
    }
}
