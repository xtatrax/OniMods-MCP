using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class WorldAnalysisTools
    {
        private static Dictionary<string, object> CellPickupableSummary(int cell, int worldId)
        {
            var groups = new Dictionary<PickupableGroupKey, PickupableAggregate>();
            int count = 0;
            float totalMass = 0f;
            int stored = 0;

            foreach (var pickupable in Components.Pickupables.Items)
            {
                if (pickupable == null || pickupable.gameObject == null)
                    continue;
                if (!PickupableTouchesCell(pickupable, cell) || !MatchesWorld(pickupable.gameObject, worldId))
                    continue;

                count++;
                bool isStored = pickupable.storage != null
                    || (pickupable.KPrefabID != null && pickupable.KPrefabID.HasTag(GameTags.Stored));
                if (isStored)
                    stored++;

                string name = ToolUtil.CleanName(pickupable.GetProperName());
                bool nameAvailable = !string.IsNullOrWhiteSpace(name);
                if (string.IsNullOrWhiteSpace(name))
                    name = "Unknown";

                var primary = pickupable.PrimaryElement ?? pickupable.GetComponent<PrimaryElement>();
                float mass = primary != null ? SafeFloat(primary.Mass) : 0f;
                totalMass += mass;

                string prefabId = PrefabId(pickupable.gameObject);
                string element = primary != null ? primary.ElementID.ToString() : string.Empty;
                bool identityAvailable = nameAvailable && !string.IsNullOrWhiteSpace(prefabId) && primary != null;
                var key = new PickupableGroupKey(name, prefabId, element,
                    identityAvailable ? null : pickupable.gameObject);
                if (!groups.TryGetValue(key, out var aggregate))
                {
                    aggregate = new PickupableAggregate
                    {
                        Name = name,
                        PrefabId = prefabId,
                        Element = element,
                        FirstSeenOrdinal = count
                    };
                    groups[key] = aggregate;
                }

                aggregate.Count++;
                aggregate.MassKg += mass;
                if (isStored)
                    aggregate.Stored++;
            }

            return new Dictionary<string, object>
            {
                ["count"] = count,
                ["stored"] = stored,
                ["loose"] = count - stored,
                ["totalMassKg"] = Math.Round(totalMass, 3),
                ["groups"] = groups.Values
                    .OrderByDescending(item => item.MassKg)
                    .ThenBy(item => item.Name, StringComparer.Ordinal)
                    .ThenBy(item => item.PrefabId, StringComparer.Ordinal)
                    .ThenBy(item => item.Element, StringComparer.Ordinal)
                    .ThenBy(item => item.FirstSeenOrdinal)
                    .Take(16)
                    .Select(item => item.ToPayload())
                    .ToList(),
                ["truncated"] = Math.Max(0, groups.Count - 16)
            };
        }

        private static bool PickupableTouchesCell(Pickupable pickupable, int cell)
        {
            if (pickupable == null || pickupable.gameObject == null)
                return false;
            return pickupable.cachedCell == cell || Grid.PosToCell(pickupable.gameObject) == cell;
        }

        private static string PrefabId(GameObject go)
        {
            var prefab = go != null ? go.GetComponent<KPrefabID>() : null;
            return prefab != null && prefab.PrefabTag.IsValid ? prefab.PrefabTag.Name : string.Empty;
        }

        private sealed class PickupableGroupKey : IEquatable<PickupableGroupKey>
        {
            private readonly string name, prefabId, element;
            private readonly GameObject individual;

            internal PickupableGroupKey(string name, string prefabId, string element, GameObject individual)
            {
                this.name = name;
                this.prefabId = prefabId;
                this.element = element;
                this.individual = individual;
            }

            public bool Equals(PickupableGroupKey other)
            {
                return other != null
                    && StringComparer.Ordinal.Equals(name, other.name)
                    && StringComparer.Ordinal.Equals(prefabId, other.prefabId)
                    && StringComparer.Ordinal.Equals(element, other.element)
                    && object.ReferenceEquals(individual, other.individual);
            }

            public override bool Equals(object obj) => Equals(obj as PickupableGroupKey);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = name == null ? 0 : StringComparer.Ordinal.GetHashCode(name);
                    hash = hash * 397 ^ (prefabId == null ? 0 : StringComparer.Ordinal.GetHashCode(prefabId));
                    hash = hash * 397 ^ (element == null ? 0 : StringComparer.Ordinal.GetHashCode(element));
                    return hash * 397 ^ (object.ReferenceEquals(individual, null) ? 0
                        : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(individual));
                }
            }
        }

        private sealed class PickupableAggregate
        {
            public string Name;
            public string PrefabId;
            public string Element;
            public int Count;
            public int FirstSeenOrdinal;
            public int Stored;
            public float MassKg;

            public Dictionary<string, object> ToPayload()
            {
                return new Dictionary<string, object>
                {
                    ["name"] = Name,
                    ["prefabId"] = PrefabId,
                    ["element"] = Element,
                    ["count"] = Count,
                    ["stored"] = Stored,
                    ["loose"] = Count - Stored,
                    ["massKg"] = Math.Round(MassKg, 3)
                };
            }
        }
    }
}
