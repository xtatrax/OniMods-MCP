using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class StorageTools
    {
        private static readonly FieldInfo ExistingStorageDbField = BindStorageDbField();
        private static readonly string[] StoragePrimaryFields =
        {
            "massKg", "elementId", "temperatureK", "temperatureC", "diseaseIndex",
            "diseaseCount", "diseaseId", "diseaseName"
        };

        private static FieldInfo BindStorageDbField()
        {
            try
            {
                var field = typeof(Db).GetField("_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                return field != null && field.DeclaringType == typeof(Db) && field.FieldType == typeof(Db)
                    && field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        private static Dictionary<string, object> ReadStorageItemDetail(GameObject item)
        {
            var unavailable = new List<Dictionary<string, object>>();
            var data = new Dictionary<string, object>
            {
                ["id"] = null,
                ["name"] = ToolUtil.CleanName(item.GetProperName()),
                ["prefabId"] = item.name,
                ["propertiesSource"] = "PrimaryElement",
                ["primaryElementPresent"] = null,
                ["diseaseDefinitionSource"] = "Db.Diseases.resources",
                ["diseaseDefinitionStatus"] = "raw_unavailable",
                ["unavailableFields"] = unavailable
            };
            foreach (string field in StoragePrimaryFields) data[field] = null;
            KPrefabID prefab = null;
            bool prefabRead = false;
            try { prefab = item.GetComponent<KPrefabID>(); prefabRead = true; }
            catch { StorageFieldUnavailable(unavailable, "id", "prefab_id_read_failed"); }
            if (prefabRead && prefab == null) StorageFieldUnavailable(unavailable, "id", "prefab_id_missing");
            if (prefab != null)
            {
                try { data["id"] = prefab.InstanceID; }
                catch { StorageFieldUnavailable(unavailable, "id", "prefab_id_read_failed"); }
                try { data["prefabId"] = prefab.PrefabTag.Name ?? item.name; }
                catch { StorageFieldUnavailable(unavailable, "prefabId", "prefab_id_read_failed"); }
            }

            PrimaryElement primary;
            try
            {
                primary = item.GetComponent<PrimaryElement>();
                data["primaryElementPresent"] = primary != null;
            }
            catch
            {
                foreach (string field in StoragePrimaryFields)
                    StorageFieldUnavailable(unavailable, field, "primary_element_read_failed");
                return data;
            }
            if (primary == null)
            {
                foreach (string field in StoragePrimaryFields)
                    StorageFieldUnavailable(unavailable, field, "primary_element_missing");
                return data;
            }

            ReadStorageFloat(data, unavailable, "massKg", () => primary.Mass, 3);
            float? temperature = ReadStorageFloat(data, unavailable, "temperatureK", () => primary.Temperature);
            if (temperature.HasValue)
                data["temperatureC"] = Math.Round((double)temperature.Value - 273.15, 2);
            else StorageFieldUnavailable(unavailable, "temperatureC", "temperature_unavailable");
            try { data["elementId"] = primary.ElementID.ToString(); }
            catch { StorageFieldUnavailable(unavailable, "elementId", "read_failed"); }
            byte? diseaseIndex = null;
            try { diseaseIndex = primary.DiseaseIdx; data["diseaseIndex"] = diseaseIndex.Value; }
            catch { StorageFieldUnavailable(unavailable, "diseaseIndex", "read_failed"); }
            try { data["diseaseCount"] = primary.DiseaseCount; }
            catch { StorageFieldUnavailable(unavailable, "diseaseCount", "read_failed"); }
            ReadStorageDiseaseDefinition(data, unavailable, diseaseIndex);
            return data;
        }

        private static float? ReadStorageFloat(Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable, string field, Func<float> read, int? decimals = null)
        {
            try
            {
                float value = read();
                if (float.IsNaN(value) || float.IsInfinity(value))
                    StorageFieldUnavailable(unavailable, field, "nonfinite");
                else
                {
                    data[field] = decimals.HasValue ? (object)Math.Round(value, decimals.Value) : value;
                    return value;
                }
            }
            catch { StorageFieldUnavailable(unavailable, field, "read_failed"); }
            return null;
        }

        private static void ReadStorageDiseaseDefinition(Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable, byte? index)
        {
            if (!index.HasValue)
            {
                StorageDiseaseUnavailable(data, unavailable, "raw_unavailable", "disease_index_unavailable");
                return;
            }
            if (index.Value == byte.MaxValue)
            {
                data["diseaseDefinitionStatus"] = "native_none_index";
                return;
            }

            Klei.AI.Disease definition;
            try
            {
                // Db.Get can load and initialize the database; inspect only the existing cache.
                var db = ExistingStorageDbField?.GetValue(null) as Db;
                var definitions = db?.Diseases?.resources;
                if (definitions == null)
                {
                    StorageDiseaseUnavailable(data, unavailable, "lookup_failed", "database_unavailable");
                    return;
                }
                if (index.Value >= definitions.Count)
                {
                    StorageDiseaseUnavailable(data, unavailable, "unresolved_index", "index_out_of_range");
                    return;
                }
                definition = definitions[index.Value];
                if (definition == null)
                {
                    StorageDiseaseUnavailable(data, unavailable, "unresolved_index", "definition_missing");
                    return;
                }
            }
            catch
            {
                StorageDiseaseUnavailable(data, unavailable, "lookup_failed", "database_read_failed");
                return;
            }

            data["diseaseDefinitionStatus"] = "resolved";
            try
            {
                string id = definition.Id;
                if (string.IsNullOrEmpty(id)) StorageFieldUnavailable(unavailable, "diseaseId", "definition_id_missing");
                else data["diseaseId"] = id;
            }
            catch { StorageFieldUnavailable(unavailable, "diseaseId", "definition_id_read_failed"); }
            try
            {
                string name = definition.Name;
                if (string.IsNullOrEmpty(name)) StorageFieldUnavailable(unavailable, "diseaseName", "definition_name_missing");
                else data["diseaseName"] = ToolUtil.CleanName(name);
            }
            catch { StorageFieldUnavailable(unavailable, "diseaseName", "definition_name_read_failed"); }
            if (data["diseaseId"] == null || data["diseaseName"] == null)
                data["diseaseDefinitionStatus"] = "lookup_failed";
        }

        private static void StorageDiseaseUnavailable(Dictionary<string, object> data,
            List<Dictionary<string, object>> unavailable, string status, string reason)
        {
            data["diseaseDefinitionStatus"] = status;
            StorageFieldUnavailable(unavailable, "diseaseId", reason);
            StorageFieldUnavailable(unavailable, "diseaseName", reason);
        }

        private static void StorageFieldUnavailable(List<Dictionary<string, object>> unavailable,
            string field, string reason)
        {
            unavailable.Add(new Dictionary<string, object> { ["field"] = field, ["reason"] = reason });
        }
    }
}
