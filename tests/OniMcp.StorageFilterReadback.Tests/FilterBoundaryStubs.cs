using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Explicit native filter boundaries. No Unity lifecycle or actual game DLL runs.
public sealed class Game { public static Game Instance = new Game(); }
public static class GameTags { public static readonly Tag Void = new Tag("Void"); }
public sealed class BuildingDef { public string PrefabID; }
public sealed class Building { public BuildingDef Def; }
public enum FilterElementState { Any }
public sealed class Filterable
{
    private Tag selected;
    public int Reads, Writes, OptionReads;
    public FilterElementState filterElementState;
    public readonly Dictionary<string, List<Tag>> Options = new Dictionary<string, List<Tag>>();
    public Tag SelectedTag { get { Reads++; return selected; } set { Writes++; selected = value; } }
    public Dictionary<string, List<Tag>> GetTagOptions() { OptionReads++; return Options; }
    public void FixtureSet(string value) { selected = new Tag(value); }
    public string FixtureTag() { return selected.Name; }
}
public sealed class FlatTagFilterable
{
    public GameObject Owner;
    public readonly List<Tag> selectedTags = new List<Tag>();
    public readonly List<Tag> tagOptions = new List<Tag>();
    public bool currentlyUserAssignable = true;
    public int SelectCalls;
    public string GetHeaderText() { return "Fixture flat filter"; }
    public T GetComponent<T>() where T : class { return Owner.GetComponent<T>(); }
    public void SelectTag(Tag tag, bool selected)
    {
        SelectCalls++;
        selectedTags.RemoveAll(value => value == tag);
        if (selected) selectedTags.Add(tag);
    }
    public void FixtureSet(params string[] values)
    {
        selectedTags.Clear();
        selectedTags.AddRange(values.Select(value => new Tag(value)));
    }
}
