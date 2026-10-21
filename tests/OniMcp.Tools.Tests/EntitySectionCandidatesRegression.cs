using System;
using System.Collections.Generic;
using System.Linq;
using OniMcp.Tools;
using UnityEngine;

internal static class EntitySectionCandidatesRegression
{
    internal static void Run()
    {
        var building = new GameObject();
        var item = new GameObject();
        var dupe = new GameObject();
        var crop = new GameObject();
        var decorPlant = new GameObject();
        var geyser = new GameObject { Cell = 50 };
        var coveredGeyser = new GameObject { Cell = 51 };
        var hiddenGeyser = new GameObject { Cell = 52 };
        var priorVisibility = Grid.Visible;
        try
        {
            Grid.Visible = new byte[10000];
            Grid.Visible[50] = Grid.Visible[51] = 255;
            Components.Geysers.Worlds[0] = new List<Geyser>
            {
                new Geyser { gameObject = geyser, Cover = new Uncoverable { IsUncovered = true } },
                new Geyser { gameObject = coveredGeyser, Cover = new Uncoverable() },
                null, new Geyser()
            };
            Components.Geysers.Worlds[1] = new List<Geyser>
            {
                new Geyser { gameObject = geyser, Cover = new Uncoverable { IsUncovered = true } },
                new Geyser { gameObject = hiddenGeyser, Cover = new Uncoverable { IsUncovered = true } }
            };
            Components.BuildingCompletes.Items.Clear();
            Components.BuildingCompletes.Items.Add(new BuildingComplete { gameObject = building });
            Components.Pickupables.Items.Add(new Pickupable { gameObject = item });
            Components.LiveMinionIdentities.Items.Add(new MinionIdentity { gameObject = dupe });
            Components.HarvestDesignatables.Items.Add(new HarvestDesignatable { gameObject = crop });
            Components.Uprootables.Items.Add(new Uprootable { gameObject = crop });
            Components.Uprootables.Items.Add(new Uprootable { gameObject = decorPlant });
            Components.HarvestDesignatables.Items.Add(null);
            Components.Uprootables.Items.Add(new Uprootable());

            Components.LiveMinionIdentities.Items.Add(null);
            if (!SweepTargetPolicy.IsLiveDuplicant(dupe)
                || SweepTargetPolicy.IsLiveDuplicant(item)
                || SweepTargetPolicy.IsLiveDuplicant(null))
                throw new Exception("Sweep must reject live dupes while retaining loose items");
            Components.LiveMinionIdentities.Items.Clear();
            if (SweepTargetPolicy.IsLiveDuplicant(dupe))
                throw new Exception("Sweep live registry must not retain a removed duplicant");
            Components.LiveMinionIdentities.Items.Add(new MinionIdentity { gameObject = dupe });

            var found = EntitySectionCandidates.LiveObjects().ToArray();
            if (found.Length != 6 || !found.Contains(crop) || !found.Contains(decorPlant))
                throw new Exception("Planted crops/decor must resolve once without null candidates");
            if (!found.Contains(building) || !found.Contains(item) || !found.Contains(dupe))
                throw new Exception("Existing live entity categories must remain available");

            if (!found.Contains(geyser) || found.Contains(coveredGeyser) || found.Contains(hiddenGeyser))
                throw new Exception("Visible uncovered geysers must resolve once; hidden/covered vents must not be revealed");
            Components.Geysers.Worlds.Clear();
            if (EntitySectionCandidates.LiveObjects().Contains(geyser))
                throw new Exception("Removed geyser must not remain in a stale candidate cache");

            // Uprooting removes the planted object from both live registries.
            Components.HarvestDesignatables.Items.Clear();
            Components.Uprootables.Items.Clear();
            if (EntitySectionCandidates.LiveObjects().Contains(crop))
                throw new Exception("Removed crop must not remain in a stale candidate cache");
        }
        finally
        {
            Components.Geysers.Worlds.Clear();
            Grid.Visible = priorVisibility;
            Components.BuildingCompletes.Items.Clear();
            Components.Pickupables.Items.Clear();
            Components.LiveMinionIdentities.Items.Clear();
            Components.HarvestDesignatables.Items.Clear();
            Components.Uprootables.Items.Clear();
        }
        Console.WriteLine("Entity candidates: crops, decor, visible geysers, hidden vent exclusion, deduplication and removal passed");
    }
}
internal class Pickupable { internal GameObject gameObject; }
internal partial class MinionIdentity { internal GameObject gameObject; }
internal class HarvestDesignatable { internal GameObject gameObject; }
internal class Uprootable { internal GameObject gameObject; }
internal static partial class Components
{
    internal static class Pickupables { internal static readonly List<Pickupable> Items = new List<Pickupable>(); }
    internal static class LiveMinionIdentities { internal static readonly List<MinionIdentity> Items = new List<MinionIdentity>(); }
    internal static class HarvestDesignatables { internal static readonly List<HarvestDesignatable> Items = new List<HarvestDesignatable>(); }
    internal static class Uprootables { internal static readonly List<Uprootable> Items = new List<Uprootable>(); }
}

internal class Uncoverable { internal bool IsUncovered; }
internal class Geyser
{
    internal GameObject gameObject;
    internal Uncoverable Cover;
    internal T GetComponent<T>() where T : class => Cover as T;
}
internal static partial class Components
{
    internal static class Geysers
    {
        internal static readonly Dictionary<int, List<Geyser>> Worlds = new Dictionary<int, List<Geyser>>();
        internal static IEnumerable<int> GetWorldsIds() => Worlds.Keys;
        internal static IEnumerable<Geyser> GetItems(int worldId) => Worlds[worldId];
    }
}
