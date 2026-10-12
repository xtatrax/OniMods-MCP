using System;
using System.Collections.Generic;
using System.Linq;

namespace OniMcp.Tools
{
    // The two profiles project the same observations; compact never invents a rectangle.
    internal sealed class BuildingGeometryCell
    {
        internal int X { get; set; }
        internal int Y { get; set; }
        internal int? Cell { get; set; }
        internal bool Valid { get; set; } = true;

        internal object ToDictionary(bool compact)
        {
            if (compact) return new[] { X, Y };
            return new Dictionary<string, object>
            {
                ["x"] = X, ["y"] = Y, ["cell"] = Cell, ["valid"] = Valid
            };
        }
    }

    internal sealed class BuildingGeometryPort
    {
        internal string Kind { get; set; }
        internal string Direction { get; set; }
        internal string Id { get; set; }
        internal BuildingGeometryCell Position { get; set; }
        internal int[] Offset { get; set; }
        internal string Source { get; set; }

        internal object ToDictionary(bool compact)
        {
            var result = new Dictionary<string, object>
            {
                ["kind"] = Kind, ["direction"] = Direction, ["id"] = Id,
                ["position"] = Position?.ToDictionary(compact)
            };
            if (!compact)
            {
                result["offset"] = Offset;
                result["source"] = Source;
            }
            return result;
        }
    }

    internal sealed class BuildingGeometrySnapshot
    {
        internal int? Id { get; set; }
        internal string PrefabId { get; set; }
        internal string Name { get; set; }
        internal string Scope { get; set; }
        internal int? WorldId { get; set; }
        internal string CoordinateSpace { get; set; }
        internal string Orientation { get; set; }
        internal string BuildLocationRule { get; set; }
        internal string ObjectLayer { get; set; }
        internal string TileLayer { get; set; }
        internal string PermittedRotations { get; set; }
        internal int[] DefinitionSize { get; set; }
        internal BuildingGeometryCell Origin { get; set; }
        internal string Source { get; set; }
        internal List<BuildingGeometryCell> Cells { get; set; }
        internal List<int[]> DefinitionOffsets { get; set; }
        internal readonly List<BuildingGeometryPort> Ports = new List<BuildingGeometryPort>();
        internal readonly List<string> Errors = new List<string>();
        internal bool PortsComplete { get; set; } = true;

        internal Dictionary<string, object> ToDictionary(bool compact)
        {
            var validCells = Cells?.Where(c => c.Valid).ToList();
            bool available = Cells != null && Cells.Count > 0 && validCells.Count == Cells.Count;
            int[] bounds = available ? new[]
            {
                validCells.Min(c => c.X), validCells.Min(c => c.Y),
                validCells.Max(c => c.X), validCells.Max(c => c.Y)
            } : null;
            int[] size = bounds == null ? null : new[] { bounds[2] - bounds[0] + 1, bounds[3] - bounds[1] + 1 };
            bool? fillsBounds = size == null ? (bool?)null
                : validCells.Select(c => Tuple.Create(c.X, c.Y)).Distinct().Count() == size[0] * size[1];
            var result = new Dictionary<string, object>
            {
                ["id"] = Id, ["prefabId"] = PrefabId, ["name"] = Name, ["scope"] = Scope,
                ["worldId"] = WorldId, ["coordinateSpace"] = CoordinateSpace,
                ["orientation"] = Orientation, ["permittedRotations"] = PermittedRotations,
                ["origin"] = Origin?.ToDictionary(compact),
                ["bottomLeft"] = bounds == null ? null : new[] { bounds[0], bounds[1] },
                ["bounds"] = bounds, ["size"] = size,
                ["occupiedCount"] = available ? (object)Cells.Count : null,
                ["fillsBounds"] = fillsBounds, ["available"] = available,
                ["source"] = Source, ["portsComplete"] = PortsComplete,
                ["ports"] = Ports.Select(p => p.ToDictionary(compact)).ToList(),
                ["errors"] = Errors.Distinct().ToArray()
            };
            if (!compact)
            {
                result["occupiedCells"] = Cells?.Select(c => c.ToDictionary(false)).ToList();
                result["definitionOffsets"] = DefinitionOffsets;
                result["orientedOffsets"] = Cells?.Where(c => c.Valid).Select(c => new[] { c.X - (Origin?.X ?? 0), c.Y - (Origin?.Y ?? 0) }).ToList();
                result["definitionSize"] = DefinitionSize;
                result["buildLocationRule"] = BuildLocationRule;
                result["objectLayer"] = ObjectLayer;
                result["tileLayer"] = TileLayer;
                result["coordinateContract"] = "origin is the native building reference cell; bottomLeft is the minimum occupied x/y; bounds is [xMin,yMin,xMax,yMax] inclusive. Bounds do not imply every cell is occupied.";
            }
            return result;
        }
    }
}
