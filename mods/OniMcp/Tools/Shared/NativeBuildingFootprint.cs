using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    // Geometry only: no UI, ports, placement or game progression. Bounds are never filled in.
    internal sealed class NativeBuildingFootprint
    {
        internal const string NativeSource = "BuildingDef.RunOnArea + Rotatable.GetRotatedCellOffset";
        internal BuildingGeometryCell Origin;
        internal Orientation Orientation;
        internal List<BuildingGeometryCell> Cells;
        internal string Error;
        internal bool Available => Error == null && Cells != null && Cells.Count > 0 && Cells.All(c => c.Valid);
        internal int[] Bounds => Cells == null || Cells.Count == 0 ? null : new[]
            { Cells.Min(c => c.X), Cells.Min(c => c.Y), Cells.Max(c => c.X), Cells.Max(c => c.Y) };
        internal int[] Size => Bounds == null ? null : new[] { Bounds[2] - Bounds[0] + 1, Bounds[3] - Bounds[1] + 1 };
        internal bool? FillsBounds => Bounds == null ? (bool?)null : Cells.Select(c => Tuple.Create(c.X,c.Y)).Distinct().Count() == Size[0] * Size[1];
        internal bool Contains(int cell) => Available && Cells.Any(c => c.Cell == cell);

        internal static NativeBuildingFootprint Definition(BuildingDef def, Orientation orientation)
        {
            var result = new NativeBuildingFootprint { Orientation = orientation, Origin = new BuildingGeometryCell { X=0,Y=0 } };
            if (!Enum.IsDefined(typeof(Orientation), orientation) || orientation == Orientation.NumRotations)
            { result.Error = "orientation_invalid"; return result; }
            if (def?.PlacementOffsets == null || def.PlacementOffsets.Length == 0)
            { result.Error = "placement_offsets_missing"; return result; }
            try
            {
                result.Cells = def.PlacementOffsets.Select(o => Rotatable.GetRotatedCellOffset(o,orientation))
                    .Select(o => new BuildingGeometryCell { X=o.x,Y=o.y }).ToList();
            }
            catch { result.Error="geometry_read_failed"; }
            return result;
        }

        internal static NativeBuildingFootprint AtOrigin(BuildingDef def, int x, int y, Orientation orientation)
        {
            var result=Definition(def,orientation);
            result.Origin=AbsoluteCell(x,y);
            if(result.Cells==null)return result;
            var offsets=result.Cells;
            result.Cells=offsets.Select(o => AbsoluteCell(x+o.X,y+o.Y)).ToList();
            if(!result.Origin.Valid) { result.Error="origin_invalid"; return result; }
            try
            {
                var native=new List<int>();
                def.RunOnArea(result.Origin.Cell.Value,orientation,c=>native.Add(c));
                // Check XY as well: Grid's linear offset can wrap around the edge of a row.
                if(native.Count!=result.Cells.Count || native.Where((c,i)=>result.Cells[i].Valid && result.Cells[i].Cell!=c).Any())
                    result.Error="native_footprint_mismatch";
                else if(result.Cells.Any(c=>!c.Valid))result.Error="occupied_cell_invalid";
            }
            catch { result.Error="geometry_read_failed"; }
            return result;
        }

        internal static NativeBuildingFootprint FromLowerLeft(BuildingDef def, int x, int y, Orientation orientation)
        {
            var shape=Definition(def,orientation);
            if(!shape.Available)return shape;
            return AtOrigin(def,x-shape.Bounds[0],y-shape.Bounds[1],orientation);
        }

        internal static NativeBuildingFootprint Instance(GameObject go, BuildingDef def=null)
        {
            var building=go==null?null:go.GetComponent<Building>();
            if(building==null) return new NativeBuildingFootprint { Error="building_component_missing" };
            def=def??building.Def;
            int cell=Grid.PosToCell(go);
            if(!Grid.IsValidCell(cell)) return new NativeBuildingFootprint { Error="origin_invalid" };
            return AtOrigin(def,Grid.CellColumn(cell),Grid.CellRow(cell),building.Orientation);
        }

        internal static BuildingGeometryCell AbsoluteCell(int x,int y)
        {
            bool inside=x>=0 && x<Grid.WidthInCells && y>=0 && y<Grid.HeightInCells;
            int cell=inside?Grid.XYToCell(x,y):Grid.InvalidCell;
            return new BuildingGeometryCell { X=x,Y=y,Cell=cell,Valid=inside && Grid.IsValidCell(cell) };
        }

        internal Dictionary<string,object> Summary(bool includeCells=true)
        {
            var b=Available?Bounds:null;
            var result=new Dictionary<string,object>
            {
                ["geometryAvailable"]=Available,["geometryError"]=Error,["geometrySource"]=NativeSource,
                ["origin"]=Origin?.ToDictionary(false),["orientation"]=Orientation.ToString(),
                ["bottomLeft"]=b==null?null:new[]{b[0],b[1]},["bounds"]=b,["size"]=Available?Size:null,
                ["occupiedCount"]=Available?(object)Cells.Count:null,["fillsBounds"]=Available?FillsBounds:null
            };
            if(includeCells)result["occupiedCells"]=Cells?.Select(c=>c.ToDictionary(false)).ToList();
            return result;
        }
    }

    // Shared by point reads and human maps. Rebuild after frame changes or native placement.
    internal static class NativeBuildingCellIndex
    {
        private static int frame=-1, completeCount=-1;
        private static Game game;
        private static Dictionary<int,List<GameObject>> cells;
        private static Dictionary<GameObject,NativeBuildingFootprint> footprints;
        internal static void Invalidate() { frame=-1;cells=null;footprints=null; }
        private static void Ensure()
        {
            int count=Components.BuildingCompletes.Items.Count;
            if(cells!=null && frame==Time.frameCount && game==Game.Instance && completeCount==count)return;
            frame=Time.frameCount;game=Game.Instance;completeCount=count;
            cells=new Dictionary<int,List<GameObject>>();footprints=new Dictionary<GameObject,NativeBuildingFootprint>();
            foreach(var complete in Components.BuildingCompletes.Items)
                if(complete!=null)Add(complete.gameObject);
            foreach(var constructable in UnityEngine.Object.FindObjectsByType<Constructable>(FindObjectsSortMode.None))
                if(constructable!=null)Add(constructable.gameObject);
        }
        private static void Add(GameObject go)
        {
            if(go==null || footprints.ContainsKey(go))return;
            var shape=NativeBuildingFootprint.Instance(go);footprints[go]=shape;
            if(!shape.Available)return;
            foreach(var cell in shape.Cells)
            {
                List<GameObject> list;
                if(!cells.TryGetValue(cell.Cell.Value,out list))cells[cell.Cell.Value]=list=new List<GameObject>();
                if(!list.Contains(go))list.Add(go);
            }
        }
        internal static List<GameObject> At(int cell)
        {
            Ensure();List<GameObject> list;
            return cells.TryGetValue(cell,out list)?list:new List<GameObject>();
        }
        internal static NativeBuildingFootprint Geometry(GameObject go)
        {
            Ensure();NativeBuildingFootprint shape;
            if(footprints.TryGetValue(go,out shape))return shape;
            return NativeBuildingFootprint.Instance(go);
        }
    }
}
