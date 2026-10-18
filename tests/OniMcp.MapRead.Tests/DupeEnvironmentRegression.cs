using System.Globalization;
using System.Text;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static void RunDupeEnvironmentRegressions()
        {
            int assertions = 0;
            void Check(bool value, string label) { assertions++; if (!value) throw new InvalidOperationException(label); }
            int Count(string value, string needle) => value.Split(new[] { needle }, StringSplitOptions.None).Length - 1;
            string Read(int x1 = 1, int x2 = 8, int y1 = 1, int y2 = 8)
            {
                var result = new StringBuilder();
                AppendDupeEnvironmentObservations(result, x1, x2, y1, y2);
                return result.ToString();
            }
            GameObject Dupe(string name, int? id)
            {
                var go = new GameObject { name = name };
                if (id.HasValue) go.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = id.Value };
                return go;
            }
            var water = new Element { id = SimHashes.Water, name = "Water", IsLiquid = true };
            var polluted = new Element { id = SimHashes.PollutedWater, name = "Polluted water", IsLiquid = true };
            var oxygen = new Element { id = SimHashes.Oxygen, name = "Oxygen", IsGas = true };
            void Put(GameObject go, int x, int y, Element element, float mass, bool visible = true, int world = 0)
            {
                int cell = Grid.XYToCell(x, y);
                Grid.Objects[cell, (int)ObjectLayer.Minion] = go;
                Grid.Element[cell] = element; Grid.Mass[cell] = mass;
                Grid.Visible[cell] = visible; Grid.WorldIdx[cell] = world;
            }
            void Reset()
            {
                Grid.Element = new Element[16384]; Grid.Mass = new float[16384]; Grid.Temperature = new float[16384];
                Grid.Objects = new GameObject[16384, 2]; Grid.Solid = new bool[16384]; Grid.Foundation = new bool[16384];
                Grid.Visible = Enumerable.Repeat(true, 16384).ToArray(); Grid.WorldIdx = new int[16384];
                Grid.WorldValid = Enumerable.Repeat(true, 16384).ToArray();
                ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
                McpLocalization.LanguageProvider = () => "en"; McpLocalization.GameLanguageProvider = () => "en";
            }
            var savedElements = Grid.Element; var savedMass = Grid.Mass; var savedTemperature = Grid.Temperature;
            var savedObjects = Grid.Objects; var savedSolid = Grid.Solid; var savedFoundation = Grid.Foundation;
            var savedVisible = Grid.Visible; var savedWorlds = Grid.WorldIdx; var savedWorldValid = Grid.WorldValid;
            var savedCluster = ClusterManager.Instance;
            var savedLanguage = McpLocalization.LanguageProvider; var savedGameLanguage = McpLocalization.GameLanguageProvider;
            var savedCulture = CultureInfo.CurrentCulture;
            int initialMoves = CameraController.Instance.Moves, initialChanges = OverlayScreen.Instance.Changes;
            var initialOverlay = OverlayScreen.Instance.mode;
            try
            {
                Reset();
                var normal = Dupe("Ada", 101);
                Put(normal, 4, 5, water, 12.34f); Put(normal, 4, 6, water, 45.67f);
                string text = Read();
                Check(Count(text, "Ada [id=101]") == 1 && Count(text, "[id=") == 1, "two native occupied cells have one subject");
                Check(text.Contains("Water @(4,5), 12.3kg | Water @(4,6), 45.7kg"), "both cell coordinates and individual masses retained");
                Check(!text.Contains("58.0kg"), "cell mass never summed into subject mass");
                Check(normal.ComponentReads == 1 && normal.NameReads == 1, "native identity metadata read once per subject");
                Check(text.StartsWith("- Environment observation: ") && text.EndsWith("." + Environment.NewLine), "existing summary placement and line form preserved");
                Check(!text.Contains("warning", StringComparison.OrdinalIgnoreCase) && !text.Contains("safe", StringComparison.OrdinalIgnoreCase), "observation adds no safety judgement");

                Reset();
                var same1 = Dupe("Same", 201); var same2 = Dupe("Same", 202);
                Put(same1, 2, 2, water, 11); Put(same1, 2, 3, water, 12);
                Put(same2, 3, 2, water, 21); Put(same2, 3, 3, water, 22);
                text = Read();
                Check(Count(text, "Same [id=") == 2 && text.Contains("id=201") && text.Contains("id=202"), "same-name distinct native objects are not merged");
                Check(text.Contains("Water @(2,2), 11.0kg | Water @(2,3), 12.0kg") && text.Contains("Water @(3,2), 21.0kg | Water @(3,3), 22.0kg"), "each same-name object retains its own contacts");
                Check(same1.ComponentReads == 1 && same2.ComponentReads == 1, "same names do not cause repeated or skipped identity reads");

                Reset();
                var missing1 = Dupe("Missing", null); var missing2 = Dupe("Missing", null);
                Put(missing1, 2, 2, water, 10); Put(missing1, 2, 3, water, 11);
                Put(missing2, 3, 2, water, 20); Put(missing2, 3, 3, water, 21);
                text = Read();
                Check(Count(text, "Missing [id=null]") == 2, "missing metadata IDs never merge distinct native objects");
                Check(text.Contains("@(2,3), 11.0kg") && text.Contains("@(3,3), 21.0kg"), "missing IDs retain both complete contact sets");
                Check(missing1.ComponentReads == 1 && missing2.ComponentReads == 1, "missing ID acquisition is once per native subject");
                Check(!text.Contains("unityInstanceId"), "canonical ID output does not introduce redundant runtime identifier");

                Reset();
                var colliding1 = Dupe("First", 333); var colliding2 = Dupe("Second", 333);
                Put(colliding1, 2, 2, water, 10); Put(colliding2, 3, 2, water, 20);
                text = Read();
                Check(Count(text, "[id=333]") == 2 && text.Contains("First") && text.Contains("Second"), "ID presentation never becomes grouping key");

                Reset();
                var bionic = Dupe("Chip", 404);
                Put(bionic, 4, 5, water, 10.1f); Put(bionic, 4, 6, polluted, 20.2f);
                text = Read();
                Check(Count(text, "Chip [id=404]") == 1, "second two-cell native object uses same grouping contract");
                Check(text.Contains("Water @(4,5), 10.1kg | Polluted water @(4,6), 20.2kg"), "different liquids at upper and lower occupied cells retained");
                Check(!text.Contains("30.3kg"), "different cell masses are not combined");
                text = Read(4, 4, 5, 5);
                Check(text.Contains("Chip [id=404]") && text.Contains("@(4,5)") && !text.Contains("@(4,6)") && !text.Contains("Polluted water"), "lower-only rectangle has no upper evidence");
                text = Read(4, 4, 6, 6);
                Check(text.Contains("Chip [id=404]") && text.Contains("@(4,6)") && !text.Contains("@(4,5)") && !text.Contains("Water @"), "upper-only rectangle has no lower evidence");
                Check(!Read(1, 3, 1, 8).Contains("Chip"), "out-of-rectangle entity is not scanned through anchor or transform");

                Reset();
                var later = Dupe("Alpha", 1); var earlier = Dupe("Zulu", 99);
                Put(later, 2, 3, water, 1); Put(later, 2, 4, polluted, 2);
                Put(earlier, 3, 2, water, 3); Put(earlier, 3, 3, polluted, 4);
                text = Read();
                Check(text.IndexOf("Zulu", StringComparison.Ordinal) < text.IndexOf("Alpha", StringComparison.Ordinal), "subject order follows first native cell visit, not names or IDs");
                Check(text.Contains("Water @(3,2), 3.0kg | Polluted water @(3,3), 4.0kg"), "contact order preserves existing y-then-x traversal");
                Check(Read() == text && Read() == text, "repeated paused observations have deterministic ordering");

                Reset();
                var scoped = Dupe("Scoped", 505); var hidden = Dupe("Hidden", 606); var foreign = Dupe("Foreign", 707);
                Put(scoped, 4, 5, water, 10); Put(scoped, 4, 6, polluted, 20, false);
                Put(hidden, 2, 2, water, 30, false); Put(foreign, 3, 2, water, 40, true, 1);
                text = Read();
                Check(Count(text, "Scoped [id=505]") == 1 && text.Contains("@(4,5)"), "readable partial subject remains observed");
                Check(!text.Contains("@(4,6)") && !text.Contains("Polluted water"), "hidden contact on observed entity never leaks");
                Check(!text.Contains("Hidden") && !text.Contains("@(2,2)") && !text.Contains("Foreign") && !text.Contains("@(3,2)"), "hidden and foreign-world entities excluded");
                Check(hidden.NameReads == 0 && hidden.ComponentReads == 0 && foreign.NameReads == 0 && foreign.ComponentReads == 0, "scope gate precedes all excluded native identity metadata reads");
                Check(scoped.NameReads == 1 && scoped.ComponentReads == 1, "known entity metadata once after visible contacts only");
                Grid.WorldValid[Grid.XYToCell(4,5)] = false;
                Check(Read() == McpLocalization.Get("dupe_environment.empty") + Environment.NewLine, "invalid-world contact is not read");
                Check(scoped.NameReads == 1 && scoped.ComponentReads == 1, "invalid world does not reacquire subject metadata");
                Grid.WorldValid[Grid.XYToCell(4,5)] = true; ClusterManager.Instance.activeWorldId = -1;
                Check(Read() == McpLocalization.Get("dupe_environment.empty") + Environment.NewLine, "no active world excludes subjects");
                ClusterManager.Instance = null;
                Check(Read() == McpLocalization.Get("dupe_environment.empty") + Environment.NewLine, "missing cluster excludes subjects");

                Reset();
                var dry = Dupe("Dry", 808);
                Put(dry, 2, 2, oxygen, 2); Put(dry, 2, 3, water, 0.01f);
                Put(dry, 3, 2, water, 0); Put(dry, 3, 3, null, 50);
                Put(null, 4, 2, water, 20);
                text = Read();
                Check(text == "- Environment observation: no duplicant is standing in liquid in this view." + Environment.NewLine, "empty observation semantics preserved for gas, threshold, zero, missing element and missing minion");
                Check(dry.NameReads == 0 && dry.ComponentReads == 0, "non-liquid subjects never have metadata read");
                Put(dry, 5, 2, water, 0.0101f);
                Check(Read().Contains("Dry [id=808]") && Read().Contains("@(5,2), 0.0kg"), "strict liquid threshold stays greater than 0.01 and retains existing one-decimal rendering");

                Reset();
                var zeroId = Dupe("Zero", 0); Put(zeroId, 2, 2, water, 7);
                Check(Read().Contains("Zero [id=0]"), "actual native zero ID is preserved");
                var negativeId = Dupe("Negative", -1); Put(negativeId, 3, 2, water, 8);
                Check(Read().Contains("Negative [id=-1]"), "native metadata is not recast through sentinel assumptions");

                Reset();
                var localized = Dupe("ゲーム名", 909); Put(localized, 2, 2, water, 12.34f); Put(localized, 2, 3, polluted, 23.45f);
                foreach (string language in new[] { "en", "ja", "zh-Hans" })
                {
                    McpLocalization.LanguageProvider = () => language;
                    text = Read();
                    Check(Count(text, "ゲーム名 [id=909]") == 1, "native game name preserved independently of MCP language " + language);
                    Check(text.Contains("Water @(2,2), 12.3kg | Polluted water @(2,3), 23.5kg"), "localized prose keeps individual cell values " + language);
                    Check(!text.Contains("dupe_environment."), "runtime keys resolve " + language);
                    Check(text.StartsWith(McpLocalization.Get("dupe_environment.summary").Split("{0}")[0]), "selected language controls summary prose " + language);
                }
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                Check(Read().Contains("12.3kg") && !Read().Contains("12,3kg"), "localized cell numeric formatter is invariant");

                Reset();
                var stable = Dupe("Stable", 1001); Put(stable, 2, 2, water, 10); Put(stable, 2, 3, polluted, 20);
                var elementsBefore = Grid.Element.ToArray(); var massBefore = Grid.Mass.ToArray(); var tempBefore = Grid.Temperature.ToArray();
                var objectsBefore = (GameObject[,])Grid.Objects.Clone(); var visibleBefore = Grid.Visible.ToArray();
                var worldsBefore = Grid.WorldIdx.ToArray(); var validBefore = Grid.WorldValid.ToArray();
                var solidBefore = Grid.Solid.ToArray(); var foundationBefore = Grid.Foundation.ToArray();
                var componentsBefore = stable.Components.ToArray();
                int worldBefore = ClusterManager.Instance.activeWorldId;
                Read();
                Check(Grid.Element.SequenceEqual(elementsBefore) && Grid.Mass.SequenceEqual(massBefore) && Grid.Temperature.SequenceEqual(tempBefore), "observation leaves native material arrays unchanged");
                Check(Grid.Visible.SequenceEqual(visibleBefore) && Grid.WorldIdx.SequenceEqual(worldsBefore) && Grid.WorldValid.SequenceEqual(validBefore), "observation leaves visibility and world arrays unchanged");
                Check(Grid.Solid.SequenceEqual(solidBefore) && Grid.Foundation.SequenceEqual(foundationBefore), "observation leaves terrain arrays unchanged");
                Check(Grid.Objects.Cast<GameObject>().SequenceEqual(objectsBefore.Cast<GameObject>()), "observation leaves native occupation array unchanged");
                Check(stable.Components.SequenceEqual(componentsBefore) && stable.name == "Stable", "observation leaves native identity metadata unchanged");
                Check(ClusterManager.Instance.activeWorldId == worldBefore && CameraController.Instance.Moves == initialMoves
                    && OverlayScreen.Instance.Changes == initialChanges && OverlayScreen.Instance.mode == initialOverlay, "observation does not move camera, select world, or change overlay");
                Console.WriteLine("Duplicant environment: " + assertions + " assertions passed");
            }
            finally
            {
                Grid.Element = savedElements; Grid.Mass = savedMass; Grid.Temperature = savedTemperature; Grid.Objects = savedObjects;
                Grid.Solid = savedSolid; Grid.Foundation = savedFoundation; Grid.Visible = savedVisible; Grid.WorldIdx = savedWorlds;
                Grid.WorldValid = savedWorldValid; ClusterManager.Instance = savedCluster;
                McpLocalization.LanguageProvider = savedLanguage; McpLocalization.GameLanguageProvider = savedGameLanguage;
                CultureInfo.CurrentCulture = savedCulture;
            }
        }

        // Only the environment method is exercised. Fail if an unrelated spatial
        // summary path is accidentally invoked through these support boundaries.
        private static GameObject CellBuildingObject(int cell) => throw new NotSupportedException();
        private static void AppendBuriedMarkerSummary(StringBuilder sb, int x1, int x2, int y1, int y2) => throw new NotSupportedException();
        // Plain native names are used here; markup removal remains outside this regression.
        private static string StripLinkTags(string text) => text ?? string.Empty;
    }
}
