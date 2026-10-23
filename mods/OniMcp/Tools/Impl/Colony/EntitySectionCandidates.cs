using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    // Crops and geysers need live registries beyond ordinary buildings and items.
    // Every Geyser object requires native visibility and Uncoverable exposure,
    // including objects reached through another live registry.
    internal static class EntitySectionCandidates
    {
        internal static IEnumerable<GameObject> LiveObjects()
        {
            return Components.BuildingCompletes.Items.Where(b => b != null).Select(b => b.gameObject)
                .Concat(Components.Pickupables.Items.Where(p => p != null).Select(p => p.gameObject))
                .Concat(Components.LiveMinionIdentities.Items.Where(d => d != null).Select(d => d.gameObject))
                .Concat(Components.HarvestDesignatables.Items.Where(p => p != null).Select(p => p.gameObject))
                .Concat(Components.Uprootables.Items.Where(p => p != null).Select(p => p.gameObject))
                .Concat(Components.Geysers.GetWorldsIds()
                    .SelectMany(worldId => Components.Geysers.GetItems(worldId))
                    .Where(IsVisibleGeyser).Select(g => g.gameObject))
                .Where(go => go != null)
                .Distinct()
                .Where(IsAllowedCandidate);
        }

        private static bool IsAllowedCandidate(GameObject go)
        {
            if (go.GetComponent<Geyser>() is Geyser geyser)
                return IsVisibleGeyser(geyser);
            return true;
        }

        private static bool IsVisibleGeyser(Geyser geyser)
        {
            if (geyser == null || geyser.gameObject == null)
                return false;
            int cell = Grid.PosToCell(geyser.gameObject);
            return Grid.IsValidCell(cell) && Grid.Visible[cell] > 0
                && (geyser.GetComponent<Uncoverable>()?.IsUncovered ?? false);
        }
    }
}
