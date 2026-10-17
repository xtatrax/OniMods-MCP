using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    // Crops and geysers need their own live registries; they are not necessarily
    // BuildingCompletes or Pickupables. Geysers follow the visible/uncovered gate
    // used by the GeoTuner list, so this addition does not reveal hidden vents.
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
                .Distinct();
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
