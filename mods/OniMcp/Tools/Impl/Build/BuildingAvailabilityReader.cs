using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    internal static class BuildingAvailabilityReader
    {
        private static readonly FieldInfo ExistingDb = BindExistingDb();

        internal sealed class Observation
        {
            internal bool? DefinitionAvailable;
            internal bool? Unlocked;
            internal bool? Value;
            internal string Status;
            internal string ReasonCode;
            internal string ResearchSource;

            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["status"] = Status,
                    ["reasonCode"] = ReasonCode != null ? (object)ReasonCode : JValue.CreateNull(),
                    ["scope"] = "definition_and_research",
                    ["researchSource"] = ResearchSource != null ? (object)ResearchSource : JValue.CreateNull()
                };
            }
        }

        private static FieldInfo BindExistingDb()
        {
            try
            {
                var field = typeof(Db).GetField("_Instance", BindingFlags.Static | BindingFlags.NonPublic);
                return field != null && field.DeclaringType == typeof(Db) && field.FieldType == typeof(Db)
                    && field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        internal static Observation Read(BuildingDef def)
        {
            var observation = new Observation();
            string definitionReason = null;
            string researchReason = null;
            if (def == null)
                definitionReason = researchReason = "definition_missing";
            else
            {
                try { observation.DefinitionAvailable = def.IsAvailable(); }
                catch { definitionReason = "definition_read_failed"; }
                ReadResearch(def, observation, out researchReason);
            }
            observation.Value = observation.DefinitionAvailable == false || observation.Unlocked == false
                ? (bool?)false : observation.DefinitionAvailable == true && observation.Unlocked == true
                    ? true : (bool?)null;
            observation.Status = observation.Value.HasValue
                ? observation.Value.Value ? "available" : "unavailable" : "unknown";
            observation.ReasonCode = observation.DefinitionAvailable == false ? "definition_unavailable"
                : observation.Unlocked == false ? "research_locked" : observation.Value == true ? null
                    : definitionReason ?? researchReason;
            return observation;
        }

        private static void ReadResearch(BuildingDef def, Observation observation, out string reason)
        {
            reason = null;
            try
            {
                if (DebugHandler.InstantBuildMode)
                {
                    observation.Unlocked = true;
                    observation.ResearchSource = "instant_build";
                    return;
                }
                if (Game.Instance != null && Game.Instance.SandboxModeActive)
                {
                    observation.Unlocked = true;
                    observation.ResearchSource = "sandbox";
                    return;
                }
            }
            catch { reason = "context_read_failed"; return; }
            if (string.IsNullOrEmpty(def.PrefabID)) { reason = "prefab_id_missing"; return; }
            if (ExistingDb == null) { reason = "database_binding_unavailable"; return; }
            Db db;
            try { db = ExistingDb.GetValue(null) as Db; }
            catch { reason = "database_cache_read_failed"; return; }
            if (db == null) { reason = "database_cache_missing"; return; }
            try
            {
                var techs = db.Techs;
                if (techs == null) { reason = "tech_catalog_missing"; return; }
                if (techs.resources == null) { reason = "tech_resources_missing"; return; }
                if (Research.Instance == null) { reason = "research_controller_missing"; return; }
                var techItems = db.TechItems;
                if (techItems == null) { reason = "tech_item_catalog_missing"; return; }
                if (techItems.resources == null) { reason = "tech_item_resources_missing"; return; }
                observation.Unlocked = techItems.IsTechItemComplete(def.PrefabID);
                observation.ResearchSource = "native_tech_items";
            }
            catch { reason = "research_read_failed"; }
        }

        internal static object JsonBoolean(bool? value)
        {
            return value.HasValue ? (object)value.Value : JValue.CreateNull();
        }

        internal static int SortRank(bool? value)
        {
            return value.HasValue ? (value.Value ? 2 : 1) : 0;
        }
    }
}
