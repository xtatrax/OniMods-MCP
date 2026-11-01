using System;
using System.Collections.Generic;
#pragma warning disable CS0649

// Deliberate native input boundaries for the actual BuildingAvailabilityReader.
// Eligibility/research orchestration is not reimplemented here. The reader sees
// supplied cached values; Db.Get initialization is forbidden in this fixture.
internal sealed partial class BuildingDef
{
    public bool FixtureNativeAvailable = true;
    public int FixtureAvailabilityReads;
    public Func<bool> FixtureAvailabilityPredicate;
    public bool IsAvailable()
    {
        FixtureAvailabilityReads++;
        return FixtureAvailabilityPredicate == null
            ? FixtureNativeAvailable : FixtureAvailabilityPredicate();
    }
}
internal sealed class Db
{
    private static Db _Instance;
    public FixtureTechCatalog Techs = new FixtureTechCatalog();
    public FixtureTechItems TechItems = new FixtureTechItems();
    public static int GetCalls;
    internal static void Seed(Db value) { _Instance = value; }
    internal static Db CachedForFixture { get { return _Instance; } }
    public static Db Get()
    {
        GetCalls++;
        throw new InvalidOperationException("Fixture forbids Db.Get initialization");
    }
}
internal class FixtureTechCatalog
{
    public List<FixtureTech> resources = new List<FixtureTech>();
}
internal sealed class FixtureTechItems
{
    public List<FixtureTechItem> resources = new List<FixtureTechItem>();
    public bool Complete = true;
    public int Reads;
    public string LastPrefabId;
    public bool IsTechItemComplete(string prefabId)
    {
        Reads++;
        LastPrefabId = prefabId;
        return Complete;
    }
}
internal sealed class Research { public static Research Instance; }

internal sealed class FixtureTech {public string Id;}
internal sealed class FixtureTechItem {public string Id,parentTechId;}
