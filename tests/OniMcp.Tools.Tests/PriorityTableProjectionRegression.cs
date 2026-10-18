using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class PriorityTableProjectionRegression
{
    private static int checks;

    internal static void Run()
    {
        ShuffledNativeMatrix();
        MissingValuesAndNativeAuthority();
        FirstSeenDuplicates();
        LegacyForms();
        DeterministicColumnLimit();
        Console.WriteLine("Priority table projection regression checks passed: " + checks);
    }

    private static void ShuffledNativeMatrix()
    {
        var rows = new List<JObject>();
        for (int dupe = 0; dupe < 4; dupe++)
        {
            var priorities = new JArray();
            for (int position = 0; position < 16; position++)
            {
                int group = (position * 5 + dupe * 3) % 16;
                priorities.Add(new JObject
                {
                    ["choreGroupId"] = "Group" + group.ToString("00"),
                    ["choreGroupName"] = "Localized " + group,
                    ["priority"] = (dupe + group) % 6,
                    ["disabled"] = group % 2 == 0,
                    ["defaultPriority"] = 3,
                    ["associatedSkillLevel"] = 100
                });
            }
            rows.Add(new JObject
            {
                ["dupe"] = new JObject { ["id"] = dupe + 1, ["name"] = "Dupe" + dupe },
                ["priorities"] = priorities
            });
        }
        string before = Snapshot(rows);
        var columns = PriorityTableProjection.CollectColumns(rows);
        Require(columns.Count == 16 && columns.Distinct().Count() == 16,
            "native matrix must contain all 16 unique chore groups");
        Require(columns.SequenceEqual(Enumerable.Range(0, 16).Select(position =>
            "Group" + ((position * 5) % 16).ToString("00"))), "native first-seen order must be stable");
        for (int dupe = 0; dupe < 4; dupe++)
            for (int group = 0; group < 16; group++)
                Require((int)PriorityTableProjection.ValueToken(rows[dupe], "Group" + group.ToString("00"))
                    == (dupe + group) % 6, "all 64 matrix values must match native IDs despite different array order");
        Require(before == Snapshot(rows), "projection must not mutate native input");
    }

    private static void MissingValuesAndNativeAuthority()
    {
        var row = JObject.Parse(@"{dupe:{name:'A'}, Dig:5, priorities:[
            {choreGroupId:'Dig',priority:0,disabled:true},
            {choreGroupId:'Build',priority:-1,disabled:false},
            {choreGroupId:'Cook',priority:null},
            {choreGroupId:'Research'},
            {choreGroupId:'Medicine',priority:5,disabled:true},
            {choreGroupId:'',priority:3},{priority:4},42,null]}");
        string before = row.ToString(Formatting.None);
        Require(PriorityTableProjection.CollectColumns(new[] { row }).SequenceEqual(new[]
            { "Dig", "Build", "Cook", "Research", "Medicine" }), "only explicit native group IDs become columns");
        Require((int)PriorityTableProjection.ValueToken(row, "Dig") == 0, "zero must survive and override legacy scalar");
        Require((int)PriorityTableProjection.ValueToken(row, "Build") == -1, "negative native unknown value must survive");
        Require(PriorityTableProjection.ValueToken(row, "Cook")?.Type == JTokenType.Null,
            "explicit null must remain null without a default priority");
        Require(PriorityTableProjection.ValueToken(row, "Research") == null, "missing priority must stay missing");
        Require(PriorityTableProjection.ValueToken(row, "Missing") == null, "missing group must stay missing");
        Require((int)PriorityTableProjection.ValueToken(row, "Medicine") == 5,
            "disabled flag must not rewrite observed priority");
        Require(before == row.ToString(Formatting.None), "missing-value projection must not mutate input");
        var empty = JObject.Parse("{priorities:[],Dig:5,legacy:{Build:4}}");
        Require(PriorityTableProjection.CollectColumns(new[] { empty }).Count == 0,
            "empty native array must not invent legacy columns");
        Require(PriorityTableProjection.ValueToken(empty, "Dig") == null,
            "empty native array must not fall back to a legacy value");
        Require(PriorityTableProjection.CollectColumns(new JObject[0]).Count == 0, "empty rows stay empty");
        Require(PriorityTableProjection.CollectColumns(null).Count == 0, "absent rows stay empty");
        Require(PriorityTableProjection.ValueToken(null, "Dig") == null, "absent row has no value");
    }

    private static void FirstSeenDuplicates()
    {
        var row = JObject.Parse(@"{priorities:[{choreGroupId:'Dig',priority:0},
            {choreGroupId:'Dig',priority:5},{choreGroupId:'Build',priority:null},
            {choreGroupId:'Build',priority:4}]}");
        var second = JObject.Parse("{priorities:[{choreGroupId:'Dig',priority:2},{choreGroupId:'Cook',priority:1}]}");
        Require(PriorityTableProjection.CollectColumns(new[] { row, second }).SequenceEqual(new[] { "Dig", "Build", "Cook" }),
            "duplicate group IDs must share a first-seen column");
        Require((int)PriorityTableProjection.ValueToken(row, "Dig") == 0, "first duplicate observation wins including zero");
        Require(PriorityTableProjection.ValueToken(row, "Build")?.Type == JTokenType.Null,
            "first duplicate null must not be replaced by a later normal value");
        Require((int)PriorityTableProjection.ValueToken(second, "Dig") == 2, "duplicate groups in other rows keep their own value");
    }

    private static void LegacyForms()
    {
        var flat = JObject.Parse("{name:'A',id:7,Dig:0,Build:4,unrelated:[1,2]}");
        var nested = JObject.Parse("{dupe:{id:8,name:'B'},priorities:{Dig:-1,Cook:5}}");
        var rows = new[] { flat, nested };
        string before = Snapshot(rows);
        Require(PriorityTableProjection.CollectColumns(rows).SequenceEqual(new[] { "Dig", "Build", "Cook" }),
            "legacy flat and dictionary columns must retain their order and union");
        Require((int)PriorityTableProjection.ValueToken(flat, "Dig") == 0, "legacy flat zero must survive");
        Require((int)PriorityTableProjection.ValueToken(nested, "Dig") == -1, "legacy dictionary negative value must survive");
        Require((int)PriorityTableProjection.ValueToken(nested, "Cook") == 5, "legacy dictionary value must survive");
        Require(PriorityTableProjection.ValueToken(flat, "Cook") == null, "missing legacy value remains missing");
        Require(before == Snapshot(rows), "legacy projection must not mutate input");
    }

    private static void DeterministicColumnLimit()
    {
        var priorities = new JArray(Enumerable.Range(0, 21).Select(group => new JObject
        {
            ["choreGroupId"] = "Group" + group.ToString("00"), ["priority"] = group % 6
        }));
        var rows = new[] { new JObject { ["priorities"] = priorities } };
        var expected = Enumerable.Range(0, 18).Select(group => "Group" + group.ToString("00")).ToArray();
        Require(PriorityTableProjection.CollectColumns(rows).SequenceEqual(expected), "existing 18-column cap must be deterministic");
        Require(PriorityTableProjection.CollectColumns(rows).SequenceEqual(expected), "repeated collection must preserve order");
        var legacy = new JObject();
        for (int group = 0; group < 21; group++)
            legacy["Group" + group.ToString("00")] = group % 6;
        Require(PriorityTableProjection.CollectColumns(new[] { legacy }).SequenceEqual(expected),
            "legacy 18-column cap remains unchanged");
    }

    private static string Snapshot(IEnumerable<JObject> rows)
    {
        return JsonConvert.SerializeObject(rows, Formatting.None);
    }

    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
