using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class StorageTools
    {
        internal static bool HasStorageScope(JObject args) => args?.Property("storageScope") != null;

        internal static McpToolParameter StorageScopeParameter()
        {
            return new McpToolParameter
            {
                Type = "string", Required = false,
                EnumValues = new List<string> { "primary", "all_components" },
                Description = McpLocalization.Get("storage_components.scope_description")
            };
        }

        internal static CallToolResult StorageScopeDetailOnly()
        {
            return CallToolResult.Error(McpLocalization.Get("storage_components.detail_only"));
        }

        private static bool TryReadStorageScope(JObject args, out string scope)
        {
            scope = "primary";
            if (!HasStorageScope(args)) return true;
            var token = args["storageScope"];
            if (token == null || token.Type != JTokenType.String) return false;
            scope = token.Value<string>();
            return scope == "primary" || scope == "all_components";
        }

        private static Dictionary<string, object> ReadStorageDetail(StorageInfo target, string scope)
        {
            var unavailable = new List<Dictionary<string, object>>();
            var data = new Dictionary<string, object>
            {
                ["id"] = target.Id,
                ["name"] = target.Name,
                ["prefabId"] = target.PrefabId,
                ["position"] = new { x = target.X, y = target.Y },
                ["worldId"] = target.WorldId,
                ["acceptedTags"] = target.AcceptedTags,
                ["storageScope"] = scope,
                ["summaryScope"] = "primary",
                ["componentScope"] = "same_game_object",
                ["includesChildStorages"] = false,
                ["includesOtherIStorageImplementations"] = false,
                ["primarySelectionSource"] = "GameObject.GetComponent<Storage>",
                ["capacityKg"] = null,
                ["massStoredKg"] = null,
                ["unavailableFields"] = unavailable
            };
            bool partial = false;
            if (scope == "primary")
            {
                ReadStorageCore(target.Storage, data, unavailable, true);
                partial = (bool)data["partial"];
            }
            else
            {
                data["componentEnumerationSource"] = "GameObject.GetComponents<Storage>";
                data["componentEnumerationStatus"] = "unavailable";
                data["storageComponentCount"] = null;
                data["storageComponents"] = null;
                Storage[] components;
                string failure;
                if (!TryReadStorageComponents(target, out components, out failure))
                {
                    StorageFieldUnavailable(unavailable, "storageComponentCount", failure);
                    StorageFieldUnavailable(unavailable, "storageComponents", failure);
                    ReadStorageCore(target.Storage, data, unavailable, false);
                    partial = true;
                }
                else
                {
                    var rows = new List<Dictionary<string, object>>();
                    for (int index = 0; index < components.Length; index++)
                    {
                        var row = ReadStorageComponent(components[index], index,
                            ReferenceEquals(components[index], target.Storage));
                        rows.Add(row);
                        partial |= (bool)row["partial"];
                        if (!(bool)row["isPrimary"]) continue;
                        data["capacityKg"] = row["capacityKg"];
                        data["massStoredKg"] = row["massStoredKg"];
                        foreach (var missing in (List<Dictionary<string, object>>)row["unavailableFields"])
                        {
                            string field = (string)missing["field"];
                            if (field == "capacityKg" || field == "massStoredKg")
                                StorageFieldUnavailable(unavailable, field, (string)missing["reason"]);
                        }
                    }
                    data["storageComponentCount"] = components.Length;
                    data["storageComponents"] = rows;
                    data["componentEnumerationStatus"] = "observed";
                }
            }
            try
            {
                var locker = target.GameObject.GetComponent<StorageLocker>();
                if (locker != null)
                {
                    data["userMaxCapacityKg"] = null;
                    ReadStorageFloat(data, unavailable, "userMaxCapacityKg", () => locker.UserMaxCapacity, 3);
                }
            }
            catch
            {
                data["userMaxCapacityKg"] = null;
                StorageFieldUnavailable(unavailable, "userMaxCapacityKg", "read_failed");
            }
            data["partial"] = partial || unavailable.Count > 0;
            return data;
        }

        private static bool TryReadStorageComponents(StorageInfo target, out Storage[] components,
            out string failure)
        {
            components = null;
            failure = "component_enumeration_failed";
            try
            {
                var observed = target.GameObject.GetComponents<Storage>();
                if (observed == null) { failure = "component_enumeration_unavailable"; return false; }
                int primaryCount = 0;
                for (int index = 0; index < observed.Length; index++)
                {
                    if (observed[index] == null) { failure = "component_missing"; return false; }
                    if (ReferenceEquals(observed[index], target.Storage)) primaryCount++;
                    for (int prior = 0; prior < index; prior++)
                        if (ReferenceEquals(observed[index], observed[prior]))
                        { failure = "duplicate_component_reference"; return false; }
                }
                if (primaryCount != 1) { failure = "primary_component_unresolved"; return false; }
                components = observed;
                return true;
            }
            catch { return false; }
        }

        private static Dictionary<string, object> ReadStorageComponent(Storage storage, int index, bool primary)
        {
            var unavailable = new List<Dictionary<string, object>>();
            var data = new Dictionary<string, object>
            {
                ["componentIndex"] = index,
                ["isPrimary"] = primary,
                ["storageIdTag"] = null,
                ["storageIdStatus"] = "unavailable",
                ["storageIdKind"] = "native_storage_tag_not_instance_id",
                ["showInUI"] = null,
                ["storageFilters"] = null,
                ["storageFiltersStatus"] = "unavailable",
                ["unavailableFields"] = unavailable
            };
            try
            {
                Tag nativeTag = storage.storageID;
                if (nativeTag == Tag.Invalid) data["storageIdStatus"] = "native_unassigned";
                else
                {
                    string tag = nativeTag.Name;
                    if (string.IsNullOrEmpty(tag))
                    {
                        data["storageIdStatus"] = "unresolved";
                        StorageFieldUnavailable(unavailable, "storageIdTag", "tag_unavailable");
                    }
                    else
                    {
                        data["storageIdTag"] = tag;
                        data["storageIdStatus"] = "resolved";
                    }
                }
            }
            catch
            {
                data["storageIdStatus"] = "read_failed";
                StorageFieldUnavailable(unavailable, "storageIdTag", "read_failed");
            }
            try { data["showInUI"] = storage.showInUI; }
            catch { StorageFieldUnavailable(unavailable, "showInUI", "read_failed"); }
            try
            {
                var filters = storage.storageFilters;
                if (filters == null) data["storageFiltersStatus"] = "native_null";
                else
                {
                    var tags = new List<string>();
                    foreach (var tag in filters)
                    {
                        string name = tag.Name;
                        tags.Add(name);
                        if (string.IsNullOrEmpty(name))
                            StorageFieldUnavailable(unavailable, "storageFilters[" + (tags.Count - 1) + "]", "tag_unavailable");
                    }
                    data["storageFilters"] = tags;
                    data["storageFiltersStatus"] = "read";
                }
            }
            catch
            {
                data["storageFiltersStatus"] = "read_failed";
                StorageFieldUnavailable(unavailable, "storageFilters", "read_failed");
            }
            ReadStorageCore(storage, data, unavailable, true, true);
            return data;
        }

        private static void ReadStorageCore(Storage storage, Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable, bool includeItems, bool preserveMissingItems = false)
        {
            data["capacityKg"] = null;
            data["massStoredKg"] = null;
            if (includeItems) data["items"] = null;
            if (storage == null)
            {
                StorageFieldUnavailable(unavailable, "capacityKg", "storage_missing");
                StorageFieldUnavailable(unavailable, "massStoredKg", "storage_missing");
                if (includeItems) StorageFieldUnavailable(unavailable, "items", "storage_missing");
            }
            else
            {
                ReadStorageFloat(data, unavailable, "capacityKg", () => storage.capacityKg, 3);
                ReadStorageFloat(data, unavailable, "massStoredKg", () => storage.MassStored(), 3);
                if (includeItems) ReadStorageContents(storage, data, unavailable, preserveMissingItems);
            }
            bool partialItems = false;
            if (includeItems && data["items"] is List<Dictionary<string, object>> items)
                partialItems = items.Any(item => item == null ||
                    ((List<Dictionary<string, object>>)item["unavailableFields"]).Count > 0);
            data["partial"] = unavailable.Count > 0 || partialItems;
        }

        private static void ReadStorageContents(Storage storage, Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable, bool preserveMissingItems)
        {
            GameObject[] snapshot;
            try
            {
                var items = storage.items;
                if (items == null)
                {
                    StorageFieldUnavailable(unavailable, "items", "items_unavailable");
                    return;
                }
                snapshot = items.ToArray();
            }
            catch { StorageFieldUnavailable(unavailable, "items", "read_failed"); return; }
            var rows = new List<Dictionary<string, object>>();
            for (int index = 0; index < snapshot.Length; index++)
            {
                Dictionary<string, object> row = null;
                if (snapshot[index] == null)
                    StorageFieldUnavailable(unavailable, "items[" + index + "]", "item_missing");
                else
                {
                    try { row = ReadStorageItemDetail(snapshot[index]); }
                    catch { StorageFieldUnavailable(unavailable, "items[" + index + "]", "read_failed"); }
                }
                if (row != null || preserveMissingItems) rows.Add(row);
            }
            data["items"] = rows;
        }
    }
}
