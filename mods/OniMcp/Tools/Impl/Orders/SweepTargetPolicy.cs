using UnityEngine;

namespace OniMcp.Tools
{
    internal static class SweepTargetPolicy
    {
        // Living duplicants also have Pickupable/Clearable. Those components alone
        // do not make them debris. Keep deceased-body handling unchanged.
        internal static bool IsLiveDuplicant(GameObject candidate)
        {
            if (candidate == null)
                return false;
            foreach (var identity in Components.LiveMinionIdentities.Items)
                if (identity != null && identity.gameObject == candidate)
                    return true;
            return false;
        }
    }
}
