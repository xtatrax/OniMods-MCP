using System;
using System.Collections.Generic;
using UnityEngine;

namespace OniMcp.Tools
{
    // Project loaded native tag-cache links to readable cached Codex metadata only.
    // Never configure a panel, initialize assets, or derive names from a prefab callback.
    internal static class CodexCategoryMemberReader
    {
        internal static Dictionary<string, object> Read(object widget, List<string> issues, ref int remainingNodes)
        {
            var result = new Dictionary<string, object>
            {
                ["source"] = "Assets.GetPrefabsWithTag/CodexCache.FormatLinkID/entries/subEntries",
                ["scope"] = "normalized_cached_codex_links_with_readable_metadata",
                ["identitySemantics"] = "codex_link_projection_not_raw_prefab_identity",
                ["postCopyMetadataWalkBounded"] = true,
                ["nativeCacheCopyBounded"] = false,
                ["pageRegeneratesNativeCacheCopy"] = true,
                ["available"] = false,
                ["complete"] = false,
                ["matched"] = null,
                ["returned"] = null,
                ["filteredMembers"] = null,
                ["unresolvedMembers"] = null,
                ["truncated"] = false,
                ["members"] = null
            };
            object rawTag;
            if (!CodexPanelReader.IsKnown(widget, "CodexElementCategoryList")
                || !CodexPanelReader.TryField(widget, "<categoryTag>k__BackingField", out rawTag)
                || !(rawTag is Tag category) || !category.IsValid
                || Game.Instance?.unlocks == null || CodexCache.entries == null)
            {
                issues.Add("category_members_unavailable");
                return result;
            }
            List<GameObject> prefabs;
            try { prefabs = Assets.GetPrefabsWithTag(category); }
            catch
            {
                issues.Add("category_members_unavailable");
                return result;
            }
            if (prefabs == null)
            {
                issues.Add("category_members_unavailable");
                return result;
            }
            var members = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int filtered = 0, unresolved = 0;
            bool truncated = false;
            foreach (var prefab in prefabs)
            {
                if (remainingNodes <= 0) { truncated = true; break; }
                remainingNodes--;
                try
                {
                    var identity = prefab == null ? null : prefab.GetComponent<KPrefabID>();
                    if (identity == null || !identity.PrefabTag.IsValid) { unresolved++; continue; }
                    Dictionary<string, object> metadata;
                    bool excluded;
                    string prefabId = identity.PrefabTag.Name;
                    if (!CodexReadTools.TryCategoryMemberMetadata(prefabId, out metadata, out excluded))
                    {
                        if (excluded) filtered++;
                        else unresolved++;
                        continue;
                    }
                    string entryId = (string)metadata["entryId"];
                    if (!seen.Add(entryId)) continue;
                    // The native call above has already copied its complete cache list.
                    // Bound only this post-copy walk and projected metadata; page regenerates it.
                    int cost = metadata.Count + 2;
                    if (remainingNodes < cost) { truncated = true; break; }
                    remainingNodes -= cost;
                    members.Add(metadata);
                }
                catch { unresolved++; }
            }
            if (truncated && !issues.Contains("data_limit")) issues.Add("data_limit");
            if (filtered != 0) issues.Add("category_members_filtered");
            if (unresolved != 0) issues.Add("category_members_unresolved");
            result["available"] = true;
            result["complete"] = !truncated && filtered == 0 && unresolved == 0;
            result["matched"] = truncated || unresolved != 0 ? null : (object)members.Count;
            result["returned"] = members.Count;
            result["filteredMembers"] = filtered;
            result["unresolvedMembers"] = unresolved;
            result["truncated"] = truncated;
            result["members"] = members;
            return result;
        }
    }
}
