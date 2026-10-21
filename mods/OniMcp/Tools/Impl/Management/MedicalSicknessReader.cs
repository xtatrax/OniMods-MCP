using System;
using System.Collections.Generic;
using System.Reflection;
using Klei.AI;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static class MedicalSicknessReader
    {
        private static readonly FieldInfo NameKeyField = BindNameKey();

        internal sealed class Observation
        {
            internal List<Dictionary<string, object>> Items;
            internal bool? HasSickness;
            internal int? TotalEntries;
            internal int? ObservedEntries;
            internal int? UnknownEntries;
            internal string Status = "unknown";
            internal string ReasonCode;
            internal readonly List<Dictionary<string, object>> UnavailableFields = new List<Dictionary<string, object>>();

            internal Dictionary<string, object> Metadata()
            {
                return new Dictionary<string, object>
                {
                    ["status"] = Status, ["source"] = "GameObject.GetSicknesses().ModifierList",
                    ["reasonCode"] = ReasonCode == null ? (object)JValue.CreateNull() : ReasonCode,
                    ["totalEntries"] = Number(TotalEntries), ["observedEntries"] = Number(ObservedEntries),
                    ["unknownEntries"] = Number(UnknownEntries), ["unavailableFields"] = UnavailableFields
                };
            }
        }

        internal static object JsonBoolean(bool? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();
        private static object Number(int? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();

        internal static Observation Read(GameObject go)
        {
            if (go == null) return Unknown("target_missing");
            Sicknesses sicknesses;
            try { sicknesses = go.GetSicknesses(); }
            catch { return Unknown("sicknesses_read_failed"); }
            if (sicknesses == null) return Unknown("sicknesses_missing");
            List<SicknessInstance> entries;
            int count;
            try
            {
                entries = sicknesses.ModifierList;
                if (entries == null) return Unknown("sickness_list_missing");
                count = entries.Count;
            }
            catch { return Unknown("sickness_list_read_failed"); }

            var observation = new Observation
            {
                Items = new List<Dictionary<string, object>>(), TotalEntries = count,
                ObservedEntries = 0, UnknownEntries = 0
            };
            for (int i = 0; i < count; i++)
            {
                SicknessInstance instance;
                try { instance = entries[i]; }
                catch
                {
                    UnknownEntry(observation, i, "entry_read_failed");
                    continue;
                }
                if (instance == null) { UnknownEntry(observation, i, "instance_missing"); continue; }
                Sickness definition;
                try { definition = instance.Sickness; }
                catch { UnknownEntry(observation, i, "definition_read_failed"); continue; }
                if (definition == null) { UnknownEntry(observation, i, "definition_missing"); continue; }

                observation.ObservedEntries++;
                var row = new Dictionary<string, object>
                {
                    ["id"] = JValue.CreateNull(), ["name"] = JValue.CreateNull(),
                    ["descriptiveSymptoms"] = JValue.CreateNull(), ["exposure"] = JValue.CreateNull()
                };
                string prefix = "sicknesses[" + i + "].";
                try { row["id"] = definition.id; }
                catch { Missing(observation, prefix + "id", "read_failed"); }
                ReadName(definition, row, observation, prefix);
                try { ReadCachedText(definition.DescriptiveSymptoms, row, "descriptiveSymptoms", observation, prefix); }
                catch { Missing(observation, prefix + "descriptiveSymptoms", "key_read_failed"); }
                try { row["exposure"] = instance.ExposureInfo.sourceInfo ?? (object)JValue.CreateNull(); }
                catch { Missing(observation, prefix + "exposure", "read_failed"); }
                observation.Items.Add(row);
            }
            observation.HasSickness = observation.ObservedEntries > 0 ? true : count == 0 ? false : (bool?)null;
            observation.Status = observation.UnavailableFields.Count == 0 ? "observed"
                : observation.ObservedEntries > 0 ? "partial" : "unknown";
            observation.ReasonCode = observation.UnavailableFields.Count == 0 ? null
                : observation.ObservedEntries > 0 ? "fields_unavailable" : "sickness_entries_unavailable";
            return observation;
        }

        private static Observation Unknown(string reason)
        {
            var observation = new Observation { ReasonCode = reason };
            Missing(observation, "sicknesses", reason);
            return observation;
        }

        private static void UnknownEntry(Observation observation, int index, string reason)
        {
            observation.Items.Add(null);
            observation.UnknownEntries++;
            Missing(observation, "sicknesses[" + index + "]", reason);
        }

        private static FieldInfo BindNameKey()
        {
            try
            {
                var field = typeof(Sickness).GetField("name", BindingFlags.Instance | BindingFlags.NonPublic);
                return field != null && !field.IsStatic && field.DeclaringType == typeof(Sickness)
                    && field.FieldType == typeof(StringKey) ? field : null;
            }
            catch { return null; }
        }

        private static void ReadName(Sickness definition, Dictionary<string, object> row,
            Observation observation, string prefix)
        {
            if (NameKeyField == null) { Missing(observation, prefix + "name", "name_key_binding_unavailable"); return; }
            try
            {
                var key = (StringKey)NameKeyField.GetValue(definition);
                ReadCachedText(key, row, "name", observation, prefix);
            }
            catch { Missing(observation, prefix + "name", "key_read_failed"); }
        }

        private static void ReadCachedText(StringKey key, Dictionary<string, object> row,
            string field, Observation observation, string prefix)
        {
            try
            {
                StringEntry entry;
                if (!Strings.TryGet(key, out entry)) Missing(observation, prefix + field, "localized_text_missing");
                else if (entry == null) Missing(observation, prefix + field, "string_entry_missing");
                else if (entry.String == null) Missing(observation, prefix + field, "localized_text_null");
                else row[field] = entry.String;
            }
            catch { Missing(observation, prefix + field, "localized_text_read_failed"); }
        }

        private static void Missing(Observation observation, string field, string reason)
        {
            observation.UnavailableFields.Add(new Dictionary<string, object> { ["field"] = field, ["reason"] = reason });
        }
    }
}
