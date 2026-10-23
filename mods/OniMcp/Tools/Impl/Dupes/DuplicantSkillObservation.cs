using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    internal static class DuplicantSkillObservation
    {
        private static readonly FieldInfo ExistingDb = BindExistingDb();

        internal sealed class TargetResolution
        {
            internal bool Requested;
            internal MinionIdentity Target;
            internal string Reason;
        }

        internal sealed class Context
        {
            internal Db Database;
            internal MinionIdentity Target;
            internal MinionResume Resume;
            internal Tag Model;
            internal string Reason;
            internal bool Available => Reason == null;
        }

        internal sealed class State
        {
            internal bool? Mastered;
            internal bool? Granted;
            internal bool? CanMaster;
            internal List<string> Conditions;
            internal string Reason;
        }

        private static FieldInfo BindExistingDb()
        {
            try
            {
                var field = typeof(Db).GetField("_Instance", BindingFlags.NonPublic | BindingFlags.Static);
                return field != null && field.DeclaringType == typeof(Db) && field.FieldType == typeof(Db) && field.IsStatic ? field : null;
            }
            catch { return null; }
        }

        internal static Db ReadDatabase(out string reason)
        {
            reason = null;
            if (ExistingDb == null) { reason = "database_binding_unavailable"; return null; }
            try
            {
                var db = ExistingDb.GetValue(null) as Db;
                if (db == null) reason = "database_cache_missing";
                else if (db.Skills == null || db.Skills.resources == null) reason = "skill_catalog_missing";
                return reason == null ? db : null;
            }
            catch { reason = "database_cache_read_failed"; return null; }
        }

        internal static TargetResolution ResolveTarget(JObject args)
        {
            var result = new TargetResolution { Requested = args?["id"] != null || args?["name"] != null };
            if (!result.Requested) return result;
            try
            {
                var identities = Components.LiveMinionIdentities?.Items;
                if (identities == null) { result.Reason = "target_cache_missing"; return result; }
                string name = args?["name"]?.ToString();
                if (args?["name"] != null && string.IsNullOrWhiteSpace(name)) { result.Reason = "target_name_invalid"; return result; }
                if (args?["id"] != null)
                {
                    if (!int.TryParse(args["id"].ToString(), out int id)) { result.Reason = "target_id_invalid"; return result; }
                    var matches = identities.Where(item => item != null && item.GetComponent<KPrefabID>()?.InstanceID == id).ToList();
                    if (matches.Count != 1) { result.Reason = matches.Count == 0 ? "target_not_found" : "target_ambiguous"; return result; }
                    result.Target = matches[0];
                    if (name != null && !string.Equals(result.Target.GetProperName(), name, StringComparison.OrdinalIgnoreCase))
                    { result.Target = null; result.Reason = "target_identity_mismatch"; }
                    return result;
                }
                var named = identities.Where(item => item != null && string.Equals(item.GetProperName(), name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (named.Count != 1) result.Reason = named.Count == 0 ? "target_not_found" : "target_ambiguous";
                else result.Target = named[0];
            }
            catch { result.Target = null; result.Reason = "target_read_failed"; }
            return result;
        }

        internal static Context ReadContext(Db db, MinionIdentity target)
        {
            var result = new Context { Database = db, Target = target };
            if (target == null) return result;
            try
            {
                result.Resume = target.GetComponent<MinionResume>();
                if (result.Resume == null || result.Resume.MasteryBySkillID == null)
                { result.Reason = "resume_cache_missing"; return result; }
                if (db?.Personalities?.resources == null) { result.Reason = "personality_catalog_missing"; return result; }
                var personality = db.Personalities.resources.FirstOrDefault(item => item != null && item.Id == target.personalityResourceId);
                if (personality == null) { result.Reason = "personality_missing"; return result; }
                result.Model = personality.model;
                if (!result.Model.IsValid) result.Reason = "target_model_missing";
            }
            catch { result.Reason = "target_model_read_failed"; }
            return result;
        }

        internal static bool? ModelMatches(Database.Skill skill, Context context)
        {
            if (context?.Target == null || !context.Available || !context.Model.IsValid || skill?.requiredDuplicantModel == null)
                return null;
            return skill.requiredDuplicantModel == context.Model;
        }

        internal static bool? ReadMastered(MinionResume resume, string skillId)
        {
            if (resume == null || resume.MasteryBySkillID == null) return null;
            try { return resume.HasMasteredSkill(skillId); }
            catch { return null; }
        }

        internal static State ReadState(Database.Skill skill, Context context, bool? mastered)
        {
            var state = new State { Mastered = mastered };
            if (context?.Resume == null) { state.Reason = "resume_cache_missing"; return state; }
            try
            {
                state.Granted = context.Resume.HasBeenGrantedSkill(skill.Id);
                var conditions = context.Resume.GetSkillMasteryConditions(skill.Id);
                if (conditions == null) { state.Reason = "mastery_conditions_missing"; return state; }
                state.CanMaster = context.Resume.CanMasterSkill(conditions);
                state.Conditions = conditions.Select(item => item.ToString()).ToList();
            }
            catch { state.Reason = "mastery_conditions_read_failed"; }
            return state;
        }

        internal static object JsonBoolean(bool? value) => value.HasValue ? (object)value.Value : JValue.CreateNull();
    }
}
