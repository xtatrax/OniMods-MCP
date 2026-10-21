using Newtonsoft.Json.Linq;
using UnityEngine;
using OniMcp.Tools;

WorldAnalysisTools.RunPickupableGroupingRegressions();
WorldAnalysisTools.RunUtilitySummaryRegressions();
WorldEditorTools.RunReadRegressions();
WorldEditorTools.RunMapContractRegressions();
WorldAnalysisTools.RunTextMapFormattingRegressions();
WorldEditorTools.RunDupeEnvironmentRegressions();

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static void RunReadRegressions()
        {
            int assertions = 0;
            void Check(bool ok, string message) { assertions++; if (!ok) throw new InvalidOperationException(message); }
            void Reject(Action call, string message)
            {
                int renders = Rendered.Count, moves = CameraController.Instance?.Moves ?? 0, changes = OverlayScreen.Instance?.Changes ?? 0;
                bool rejected = false;
                try { call(); } catch (ArgumentException) { rejected = true; }
                Check(rejected, message);
                Check(Rendered.Count == renders && (CameraController.Instance?.Moves ?? 0) == moves
                    && (OverlayScreen.Instance?.Changes ?? 0) == changes, message + " before rendering or mutation");
            }
            JObject Rect() => JObject.Parse("{x1:12,y1:22,x2:14,y2:24}");
            const string viewport = "/active/map/viewport.md";
            Camera.main = null;
            var rect = Rect();
            ReadMapFileWithArgs(rect, viewport);
            Check(Rendered.Last() == (12, 14, 22, 24, OverlayModes.None.ID, true), "exact bounds work without camera");
            Check(CameraController.Instance.Moves == 0 && OverlayScreen.Instance.Changes == 0, "default read has no view mutation");
            OverlayScreen.Instance.mode = OverlayModes.Oxygen.ID;
            ReadMapFileWithArgs(Rect(), viewport);
            Check(Rendered.Last().mode == OverlayModes.Oxygen.ID, "no requested view uses current overlay");
            rect = Rect(); rect["format"] = "edit";
            ReadMapFileWithArgs(rect, viewport);
            Check(Rendered.Last().mode == OverlayModes.Oxygen.ID && !Rendered.Last().compact, "format cannot change view semantics");
            rect = Rect(); rect["view"] = "power";
            ReadMapFileWithArgs(rect, viewport);
            Check(Rendered.Last().mode == OverlayModes.Power.ID && OverlayScreen.Instance.mode == OverlayModes.Oxygen.ID, "explicit view rendered without live overlay change");
            rect["syncView"] = true;
            ReadMapFileWithArgs(rect, viewport); ReadMapFileWithArgs(rect, viewport);
            Check(OverlayScreen.Instance.Changes == 1 && CameraController.Instance.Moves == 2, "repeat same overlay never toggles off");
            Reject(() => ReadMapFileWithArgs(new JObject(), viewport), "missing camera requires bounds");
            foreach (string invalid in new[]
            {
                "{x1:12}", "{x1:\"bad\"}", "{x1:null}", "{x1:1.5,y1:2,x2:3,y2:4}", "{x1:2147483648,y1:2,x2:3,y2:4}", "{x1:-1,y1:0,x2:1,y2:1}", "{x1:14,y1:22,x2:12,y2:24}",
                "{x1:0,y1:0,x2:128,y2:1}", "{x1:0,y1:0,x2:30,y2:30}",
                "{x1:1,y1:1,x2:2,y2:2,maxCells:0}", "{x1:1,y1:1,x2:2,y2:2,maxCells:\"bad\"}", "{x1:1,y1:1,x2:2,y2:2,maxCells:2501}",
                "{x1:1,y1:1,x2:2,y2:2,view:'unknown'}"
            }) Reject(() => { var a = JObject.Parse(invalid); a["syncView"] = true; ReadMapFileWithArgs(a, viewport); }, "invalid request " + invalid);
            rect = JObject.Parse("{x1:0,y1:0,x2:30,y2:30,maxCells:961}");
            ReadMapFileWithArgs(rect, viewport);
            Check(Rendered.Last().xMax == 30, "explicit larger budget accepted");
            OverlayScreen.Instance.mode = new HashedString("unsupported");
            Reject(() => ReadMapFileWithArgs(Rect(), viewport), "unsupported active overlay never substitutes normal map");
            OverlayScreen.Instance.mode = OverlayModes.None.ID;
            rect = Rect(); rect["view"] = "temperature";
            ReadMapLayerWithArgs(rect, "/active/map/layers/layer_0_31.md");
            Check(Rendered.Last() == (12, 14, 22, 24, OverlayModes.Temperature.ID, true), "layer uses requested view and world-offset coordinates");
            foreach (string path in new[] {"layer_1_32.md", "layer_0_32.md", "layer_64_95.md", "bad.md"})
                Reject(() => ReadMapLayerWithArgs(Rect(), path), "invalid layer " + path);
            Reject(() => ReadMapLayerWithArgs(JObject.Parse("{x1:0,y1:0,x2:2,y2:2}"), "layer_0_31.md"), "out of layer bounds");
            Reject(() => ReadMapLayerWithArgs(new JObject(), "layer_0_31.md"), "wide layer obeys budget");
            rect = new JObject { ["maxCells"] = 2048 };
            ReadMapLayerWithArgs(rect, "layer_0_31.md");
            Check(Rendered.Last() == (10, 73, 20, 51, OverlayModes.None.ID, true), "complete named layer respects world offset");
            int before = Rendered.Count;
            Zoom(Rect()); Check(Rendered.Count == before + 1, "single default zoom view");
            foreach (JToken badViews in new JToken[] { new JArray(), new JArray("power", ""), true, "unknown" })
                Reject(() => { var a=Rect(); a["views"]=badViews; a["syncView"]=true; Zoom(a); }, "invalid zoom views");
            foreach (string partialKey in new[] { "x1", "y1", "x2", "y2", "left", "top" })
            {
                int renderedBefore = Rendered.Count, movesBefore = CameraController.Instance.Moves;
                var partial = new JObject { [partialKey] = 12, ["path"] = "/active/map/zoom_12_22_14_24.md", ["syncView"] = true };
                var result = Zoom(partial);
                Check(result.IsError && result.Value.Contains("partial range"), "partial explicit bounds never replaced by path " + partialKey);
                Check(Rendered.Count == renderedBefore && CameraController.Instance.Moves == movesBefore, "partial bounds reject before rendering/sync");
            }
            Zoom(new JObject { ["path"] = "/active/map/zoom_12_22_14_24.md" });
            Check(Rendered.Last() == (12, 14, 22, 24, OverlayModes.None.ID, true), "path bounds work when no explicit range exists");
            foreach (JToken radius in new JToken[] { 0, -1, "bad", 1.5, JValue.CreateNull(), int.MaxValue })
                Reject(() => ReadMapFileWithArgs(new JObject { ["x"] = 20, ["y"] = 20, ["radius"] = radius, ["syncView"] = true }, viewport), "invalid focus radius " + radius);
            Reject(() => ReadMapFileWithArgs(new JObject { ["x"] = int.MaxValue, ["y"] = 20, ["radius"] = int.MaxValue }, viewport), "focus coordinate/radius sum cannot overflow into valid bounds");
            ReadMapFileWithArgs(new JObject { ["x"] = 20, ["y"] = 20, ["radius"] = 1 }, viewport);
            Check(Rendered.Last() == (19, 21, 19, 21, OverlayModes.None.ID, true), "valid radius is used without expansion");
            Camera.main = new Camera();
            ReadMapFileWithArgs(new JObject(), viewport); Check(Rendered.Last().xMin == 18, "initial camera read");
            Camera.main.transform.position = new Vector3(45, 45, 0);
            ReadMapFileWithArgs(new JObject(), viewport); Check(Rendered.Last().xMin == 43, "camera movement never reads stale synchronized cache");
            for (int mask=0;mask<16;mask++) { Check(MapTextReadPolicy.ConnectionGlyph(true,mask)=="*→←─↓┌┐┬↑└┘┴│├┤┼"[mask], "direction mask"); Check(MapTextReadPolicy.ConnectionGlyph(false,mask)=='?', "unknown mask"); }
            for (int x=-1;x<=4;x++) for (int y=-1;y<=3;y++) Check(MapTextReadPolicy.Inside(x,y,4,3)==(x>=0&&x<4&&y>=0&&y<3), "XY boundary");
            foreach(bool valid in new[]{false,true}) foreach(bool visible in new[]{false,true}) foreach(int cw in new[]{-1,0,1}) foreach(int aw in new[]{-1,0,1})
                Check(MapTextReadPolicy.CanRead(valid,visible,cw,aw)==(valid&&visible&&aw>=0&&cw==aw), "world visibility policy");
            Check(MapTextReadPolicy.BoundsError(999,0,1000,1,1200,10)!=null, "three-digit map bounds fail rather than wrap");
            var go=new GameObject(); var manager=new TestManager {Mask=UtilityConnections.Left|UtilityConnections.Right};
            var graph=new KAnimGraphTileVisualizer {ConnectionManager=manager}; go.Components[typeof(KAnimGraphTileVisualizer)]=graph; Grid.Objects[10,0]=go;
            string state=UtilityConnectionReadState(10,new[]{ObjectLayer.Wire});
            Check(state.Contains("connectionState=planned")&&state.Contains("plannedDirs=LR")&&state.Contains("physicalConnection=not_built")&&!manager.RequestedPhysical, "plans separately labeled");
            go.Components[typeof(BuildingComplete)]=new BuildingComplete();
            Check(UtilityConnectionReadState(10,new[]{ObjectLayer.Wire}).Contains("connectionSource=unknown"), "completed object without native provider stays unknown");
            go.Components[typeof(IHaveUtilityNetworkMgr)] = new TestProvider { Manager = manager };
            Check(TryGetUtilityConnections(10,new[]{ObjectLayer.Wire},out var physical) && manager.RequestedPhysical
                && physical == manager.Mask, "completed native reads use physical bits");
            Check(UtilityConnectionReadState(10,new[]{ObjectLayer.Wire}).Contains("connectionSource=native_physical"), "completed native source reported only when observed");
            go.Components.Remove(typeof(BuildingComplete));
            Check(!TryGetUtilityConnections(10,new[]{ObjectLayer.Wire},out _), "blueprint never reports planned bits as physical");
            Grid.Visible[10]=false; Check(UtilityConnectionReadState(10,new[]{ObjectLayer.Wire})=="connectionState=unknown", "hidden plan never exposed");
            Grid.Visible[10]=true;
            MapEditCell Change(string token, int x=10, int y=10) => new() {X=x,Y=y,FromToken=".",ToToken=token};
            foreach (string name in new[] { "挖", "挖掘", "开挖", "dig", "拆", "拆除", "拆建筑", "拆除建筑", "deconstruct", "擦", "擦拭", "擦水", "拖", "拖地", "mop", "wipe", "杀", "攻", "攻击", "击杀", "attack", "收", "收获", "收割", "采收", "harvest", "消", "取消", "取消任务", "取消命令", "cancel", "毒", "消毒", "杀菌", "灭菌", "disinfect", "sanitize", "扫", "清", "清扫", "清理", "打扫", "收拾", "捡", "捡起", "拾取", "搬运", "扫地", "sweep", "pickup", "pick_up", "clear", "clean", "捕", "捕捉", "抓捕", "capture", "wrangle" })
            {
                Check(ValidateMapEditInput(new JObject(),new List<MapEditCell>{Change(name+":7")},out _), "valid order alias "+name);
                foreach (string suffix in new[] { ":10", ":0", ":bad", ":", ":5#TestElement", ":5#TestElement,OtherElement" })
                    Check(!ValidateMapEditInput(new JObject(),new List<MapEditCell>{Change("dig:5"),Change(name+suffix)},out _), "invalid later order rejected before grouping "+name+suffix);
            }
            foreach (JToken priority in new JToken[] {0,10,"5",1.5,true,JValue.CreateNull()})
                Check(!ValidateMapEditInput(new JObject{["priority"]=priority},new List<MapEditCell>{Change("dig")},out _), "invalid parent priority");
            Check(!ValidateMapEditInput(new JObject(),new List<MapEditCell>{Change("dig:5"),Change("Tile:5#TestElement,OtherElement")},out string materialError)
                && materialError.Contains("Multi-slot"), "mixed edit rejects unsupported recipe before any order group");
            Check(!ValidateMapEditInput(new JObject(),new List<MapEditCell>{Change("dig:5",128,0)},out _), "out of grid X cannot alias another row");
            Check(!ValidateMapEditInput(new JObject(),new List<MapEditCell>{new(){X=10,Y=10,FromToken="?",ToToken="dig:5"}},out _), "unknown source is not editable");
            Console.WriteLine("PASS: " + assertions + " production map read, bounds, camera, and plan assertions");
        }
    }
}
