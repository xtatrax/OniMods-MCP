using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    // Use the same live registries as farming reads. Planted crops are not
    // BuildingCompletes or Pickupables, even though their seeds are pickupable.
    internal static class EntitySectionCandidates
    {
        internal static IEnumerable<GameObject> LiveObjects()
        {
            return Components.BuildingCompletes.Items.Where(b => b != null).Select(b => b.gameObject)
                .Concat(Components.Pickupables.Items.Where(p => p != null).Select(p => p.gameObject))
                .Concat(Components.LiveMinionIdentities.Items.Where(d => d != null).Select(d => d.gameObject))
                .Concat(Components.HarvestDesignatables.Items.Where(p => p != null).Select(p => p.gameObject))
                .Concat(Components.Uprootables.Items.Where(p => p != null).Select(p => p.gameObject))
                .Where(go => go != null)
                .Distinct();
        }
    }
}
