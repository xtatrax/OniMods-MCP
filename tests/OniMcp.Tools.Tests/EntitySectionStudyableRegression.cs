using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class EntitySectionStudyableRegression
{
    private static int checks;
    private const int FixtureId = 1764401;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException("Studyable section: " + message); }

    private static JObject ReadWork(int identity = FixtureId)
    {
        var result = EntitySectionTools.Read(new JObject { ["id"] = identity, ["worldId"] = 0, ["section"] = "work" });
        Check(!result.IsError, "actual full section reader accepts exact fixture ID");
        var body = JObject.Parse(result.Content.Single().Text);
        Check((int)body["matched"] == 1 && (int)body["returned"] == 1, "overlapping live registries resolve one exact object");
        return (JObject)body["items"].Single();
    }
    private static JObject Row(JObject item) { return (JObject)item["workables"].Single(); }
    private static void Raw(JObject row, float work, float remaining, float percent)
    {
        Check((double)row["workSeconds"] == work && (double)row["remainingSeconds"] == remaining
            && (double)row["percentComplete"] == percent
            && (string)row["numericState"]["workSeconds"] == "observed"
            && (string)row["numericState"]["remainingSeconds"] == "observed"
            && (string)row["numericState"]["percentComplete"] == "observed",
            "raw native work numbers and their observed states must remain unchanged");
    }

    internal static void Run()
    {
        var priorWorlds = Components.Geysers.Worlds.ToArray();
        var priorVisibility = Grid.Visible;
        var priorBuildings = Components.BuildingCompletes.Items.ToArray();
        var priorPickupables = Components.Pickupables.Items.ToArray();
        int forbiddenBefore = FreshnessBoundary.ForbiddenCalls;
        try
        {
            Grid.Visible = new byte[Math.Max(10000, priorVisibility == null ? 0 : priorVisibility.Length)];
            Grid.Visible[50] = 255;
            var go = new GameObject { Cell = 50, WorldId = 0, NativeName = "Native study fixture" };
            var id = new KPrefabID { InstanceID = FixtureId, PrefabTag = new Tag("GeyserStudyFixture"), gameObject = go };
            var study = new Studyable { WorkTimeValue = 3600, WorkTimeRemaining = 3600, PercentCompleteValue = 0, StudiedValue = false };
            var cover = new Uncoverable { IsUncovered = true };
            go.ExistingComponents[typeof(KPrefabID)] = id;
            go.ExistingComponents[typeof(Workable)] = study;
            var geyser = new Geyser { gameObject = go, Cover = cover };
            var building = new BuildingComplete { gameObject = go };
            var pickupable = new Pickupable { gameObject = go };
            go.ExistingComponents[typeof(Geyser)] = geyser;
            go.ExistingComponents[typeof(BuildingComplete)] = building;
            go.ExistingComponents[typeof(Pickupable)] = pickupable;
            Components.Geysers.Worlds.Clear();
            Components.Geysers.Worlds[0] = new List<Geyser> { geyser };
            Components.BuildingCompletes.Items.Add(building);
            Components.Pickupables.Items.Add(pickupable);

            var before = Row(ReadWork());
            Check(before.Property("studied") != null && before["studied"].Type == JTokenType.Boolean && !(bool)before["studied"],
                "native Studyable completion must be present as observed false, not omitted or inferred from progress");
            Check((string)before["studyStatus"] == "observed" && (string)before["studySource"] == "Studyable.Studied", "completion observation names its native source");
            Raw(before, 3600, 3600, 0);

            study.StudiedValue = true;
            var completed = Row(ReadWork());
            Check((bool)completed["studied"] && (string)completed["studyStatus"] == "observed", "native true remains completed even when progress was reset to zero");
            Raw(completed, 3600, 3600, 0);
            var changedOnlyCompletion = (JObject)completed.DeepClone(); changedOnlyCompletion["studied"] = false;
            Check(JToken.DeepEquals(before, changedOnlyCompletion), "changing native completion does not rewrite other work fields");

            study.StudiedReader = () => { throw new InvalidOperationException("native getter fault fixture"); };
            var unavailableItem = ReadWork(); var unavailable = Row(unavailableItem);
            Check(unavailable.Property("studied") != null && unavailable["studied"].Type == JTokenType.Null
                && (string)unavailable["studyStatus"] == "unavailable", "getter failure is explicit null, never false");
            Check(((JArray)unavailableItem["unavailableFields"]).Values<string>().Contains("Studyable.Studied"), "getter failure retains the field reason");
            Raw(unavailable, 3600, 3600, 0);
            study.StudiedReader = null; study.StudiedValue = false;
            Check(!(bool)Row(ReadWork())["studied"] && study.StudiedReads == 4, "reads observe current input once and do not cache stale completion");

            var ordinary = new Workable { WorkTimeValue = 3600, WorkTimeRemaining = 2700, PercentCompleteValue = 0.25f };
            var ordinaryGo = new GameObject { Cell = 51, WorldId = 0, NativeName = "Ordinary nongeyser fixture" };
            var ordinaryId = new KPrefabID { InstanceID = FixtureId + 1, PrefabTag = new Tag("OrdinaryWorkFixture"), gameObject = ordinaryGo };
            ordinaryGo.ExistingComponents[typeof(KPrefabID)] = ordinaryId;
            ordinaryGo.ExistingComponents[typeof(Workable)] = ordinary;
            Components.Pickupables.Items.Add(new Pickupable { gameObject = ordinaryGo });
            var plain = Row(ReadWork(FixtureId + 1));
            Check(plain.Property("studied") == null && plain.Property("studyStatus") == null && plain.Property("studySource") == null,
                "unrelated Workable rows receive no study metadata");
            Raw(plain, 3600, 2700, 0.25f);

            study.StudiedValue = true;
            int formattingBefore = FreshnessBoundary.FormattingCalls;
            cover.IsUncovered = false;
            foreach (string section in new[] { "status", "properties", "work" })
                Check(EntitySectionTools.Read(new JObject { ["id"] = FixtureId, ["section"] = section }).IsError,
                    "covered studied geyser in legacy registries must remain excluded: " + section);
            cover.IsUncovered = true; Grid.Visible[50] = 0;
            foreach (string section in new[] { "status", "properties", "work" })
            {
                Check(EntitySectionTools.Read(new JObject { ["id"] = FixtureId, ["section"] = section }).IsError,
                    "hidden studied geyser in legacy registries must remain excluded: " + section);
                var hiddenQuery = EntitySectionTools.Read(new JObject { ["query"] = "GeyserStudyFixture", ["worldId"] = 0, ["section"] = section });
                Check(!hiddenQuery.IsError, "hidden query returns an ordinary empty match result: " + section);
                var empty = JObject.Parse(hiddenQuery.Content.Single().Text);
                Check((int)empty["matched"] == 0 && (int)empty["returned"] == 0 && !empty["items"].Any(),
                    "hidden query does not reveal any geyser identity: " + section);
            }
            Check(study.StudiedReads == 4 && FreshnessBoundary.FormattingCalls == formattingBefore,
                "excluded registry overlaps evaluate neither completion nor native descriptor formatting");
            Check(study.WorkTimeValue == 3600 && study.WorkTimeRemaining == 3600 && study.PercentCompleteValue == 0
                && study.worker == null && study.StudiedValue && FreshnessBoundary.ForbiddenCalls == forbiddenBefore,
                "section reads preserve native inputs and use no factory or mutation API");
        }
        finally
        {
            Components.Geysers.Worlds.Clear();
            foreach (var world in priorWorlds) Components.Geysers.Worlds[world.Key] = world.Value;
            Grid.Visible = priorVisibility;
            Components.BuildingCompletes.Items.Clear(); Components.BuildingCompletes.Items.AddRange(priorBuildings);
            Components.Pickupables.Items.Clear(); Components.Pickupables.Items.AddRange(priorPickupables);
        }
        Console.WriteLine("Studyable section regression: " + checks + " checks passed (actual full reader; native inputs substituted)");
    }
}
