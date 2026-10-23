using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
{
        public static McpTool ListSkills()
        {
            return new McpTool
            {
                Name = "dupes_skills_list", Group = "dupes", Mode = "read", Risk = "none",
                Aliases = new List<string> { "dupes_skills", "skills_list" }, Hidden = true,
                Description = McpLocalization.Get("skill_model.list_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("skill_model.id"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.name"), Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.query"), Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("skill_model.limit"), Required = false },
                    ["includeUi"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.include_ui"), Required = false }
                },
                Handler = ListSkillState
            };
        }

        private static CallToolResult ListSkillState(Newtonsoft.Json.Linq.JObject args)
        {
            var db = DuplicantSkillObservation.ReadDatabase(out string reason);
            if (db == null) return SkillUnavailable(reason, true);
            var resolved = DuplicantSkillObservation.ResolveTarget(args);
            if (resolved.Reason != null) return SkillUnavailable(resolved.Reason, true);
            var context = DuplicantSkillObservation.ReadContext(db, resolved.Target);
            if (!context.Available) return SkillUnavailable(context.Reason, true);
            string query = args["query"]?.ToString();
            int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 300));
            var ui = ToolUtil.GetBool(args, "includeUi", false) ? NativeSkillUi.Read(context.Target) : null;
            var skills = db.Skills.resources.Where(skill => skill != null && !skill.deprecated)
                .Select(skill => Tuple.Create(skill, context.Target == null ? (bool?)null : DuplicantSkillObservation.ReadMastered(context.Resume, skill.Id)))
                .Where(item => context.Target == null || DuplicantSkillObservation.ModelMatches(item.Item1, context) != false || item.Item2 != false)
                .Where(item => string.IsNullOrWhiteSpace(query) || SkillMatches(item.Item1, query))
                .OrderBy(item => item.Item1.skillGroup).ThenBy(item => item.Item1.tier).ThenBy(item => item.Item1.Id)
                .Take(limit).Select(item => SkillToDictionary(item.Item1, context, item.Item2, ui)).ToList();
            var result = new Dictionary<string, object>
            {
                ["dupe"] = context.Target == null ? null : DupeRef(context.Target),
                ["returned"] = skills.Count,
                ["skills"] = skills,
                ["scope"] = context.Target == null ? "definition_catalog" : "target_model_candidates_and_mastered_history",
                ["canMasterSemantics"] = "native_mastery_conditions",
                ["modelFilter"] = context.Target == null ? "none" : "exclude_known_model_mismatch_unmastered_before_query_limit"
            };
            if (context.Target != null)
            {
                result["targetModel"] = context.Model.Name;
                result["modelSource"] = "Personality.model";
            }
            if (ui != null) result["ui"] = ui.ToDictionary();
            return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
        }

        public static McpTool LearnSkill()
        {
            return new McpTool
            {
                Name = "dupes_learn_skill", Group = "dupes", Mode = "write", Risk = "medium",
                Aliases = new List<string> { "learn_skill", "dupe_skill_learn", "skills_learn" }, Hidden = true,
                Description = McpLocalization.Get("skill_model.learn_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("skill_model.id"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.name"), Required = false },
                    ["skillId"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.skill_id"), Required = true },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.force"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.dry_run"), Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.confirm"), Required = true }
                },
                Handler = LearnSkillThroughNativeUi
            };
        }

        private static CallToolResult LearnSkillThroughNativeUi(Newtonsoft.Json.Linq.JObject args)
        {
            bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
            if (!dryRun && !ToolUtil.GetBool(args, "confirm", false)) return SkillUnavailable("confirmation_required");
            var resolved = DuplicantSkillObservation.ResolveTarget(args);
            if (resolved.Target == null) return SkillUnavailable(resolved.Reason ?? "target_required");
            var db = DuplicantSkillObservation.ReadDatabase(out string reason);
            if (db == null) return SkillUnavailable(reason);
            var context = DuplicantSkillObservation.ReadContext(db, resolved.Target);
            if (!context.Available) return SkillUnavailable(context.Reason);
            string skillId = args["skillId"]?.ToString()?.Trim();
            var skill = db.Skills.resources.FirstOrDefault(item => item != null && item.Id == skillId && !item.deprecated);
            if (skill == null) return SkillUnavailable("skill_not_found");
            bool? before = DuplicantSkillObservation.ReadMastered(context.Resume, skill.Id);
            if (!before.HasValue) return SkillUnavailable("mastery_cache_unavailable");
            bool force = ToolUtil.GetBool(args, "force", false);
            if (dryRun)
            {
                var previewUi = NativeSkillUi.Read(context.Target);
                return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                {
                    ["ok"] = true, ["preview"] = true, ["changed"] = false,
                    ["dupe"] = DupeRef(context.Target), ["skill"] = SkillToDictionary(skill, context, before, previewUi),
                    ["ui"] = previewUi.ToDictionary(),
                    ["forceRequested"] = force, ["uiInvoked"] = false, ["nativeClickInvoked"] = false, ["grantInvoked"] = false,
                    ["executionPath"] = force ? "MinionResume.GrantSkill" : "ManagementMenu.OpenSkills->SkillWidget.OnPointerClick(Left)",
                    ["guidance"] = McpLocalization.Get("skill_model.preview_guidance")
                }, McpJsonUtil.Settings));
            }
            if (before == true) return SkillActionResult(context, skill, before, before, null, false, false, false, false);
            if (force)
            {
                string failure = null;
                try { context.Resume.GrantSkill(skill.Id); }
                catch { failure = "native_grant_failed"; }
                var after = DuplicantSkillObservation.ReadMastered(context.Resume, skill.Id);
                return SkillActionResult(context, skill, before, after, failure ?? (after == true ? null : "native_skill_not_mastered"), false, false, true, false);
            }
            if (NativeSkillUi.ReadInstantBuild() != false) return SkillUnavailable("instant_build_not_false");
            if (!NativeSkillUi.PointerAvailable()) return SkillUnavailable("event_system_unavailable");
            bool opened = NativeSkillUi.TryOpen(context.Target, out var ui, out reason, out bool openInvoked);
            if (!opened)
                return SkillActionResult(context, skill, before, DuplicantSkillObservation.ReadMastered(context.Resume, skill.Id), reason, openInvoked, false, false, openInvoked);
            bool clicked = NativeSkillUi.TryClick(ui, context.Target, skill.Id, out reason, out bool clickInvoked);
            bool? learned = DuplicantSkillObservation.ReadMastered(context.Resume, skill.Id);
            return SkillActionResult(context, skill, before, learned, clicked && learned == true ? null : reason ?? "native_skill_not_mastered", openInvoked, clickInvoked, false, !clicked || learned != true);
        }

        private static CallToolResult SkillActionResult(DuplicantSkillObservation.Context context, Database.Skill skill,
            bool? before, bool? after, string reason, bool uiInvoked, bool clickInvoked, bool grantInvoked, bool partial)
        {
            bool ok = reason == null;
            var ui = uiInvoked ? NativeSkillUi.Read(context.Target) : null;
            var result = new Dictionary<string, object>
            {
                ["ok"] = ok, ["partial"] = !ok && (partial || uiInvoked || clickInvoked || grantInvoked),
                ["changed"] = before.HasValue && after.HasValue ? (object)(before.Value != after.Value) : Newtonsoft.Json.Linq.JValue.CreateNull(),
                ["changeScope"] = "skill_mastery", ["reasonCode"] = (object)reason ?? Newtonsoft.Json.Linq.JValue.CreateNull(),
                ["dupe"] = DupeRef(context.Target), ["skill"] = SkillToDictionary(skill, context, after, ui),
                ["masteredBefore"] = DuplicantSkillObservation.JsonBoolean(before), ["masteredAfter"] = DuplicantSkillObservation.JsonBoolean(after),
                ["uiInvoked"] = uiInvoked, ["nativeClickInvoked"] = clickInvoked, ["grantInvoked"] = grantInvoked,
                ["forceGranted"] = grantInvoked && after == true,
                ["executionPath"] = grantInvoked ? "MinionResume.GrantSkill" : "ManagementMenu.OpenSkills->SkillWidget.OnPointerClick(Left)"
            };
            if (before == true && after == true && !uiInvoked && !clickInvoked && !grantInvoked) result["reason"] = "already_mastered";
            try { result["availableSkillPoints"] = context.Resume.AvailableSkillpoints; }
            catch { result["availableSkillPoints"] = Newtonsoft.Json.Linq.JValue.CreateNull(); }
            if (ui != null) result["ui"] = ui.ToDictionary();
            if (!ok) result["error"] = McpLocalization.Get("skill_model.action_unavailable");
            string text = JsonConvert.SerializeObject(result, McpJsonUtil.Settings);
            return ok ? CallToolResult.Text(text) : CallToolResult.Error(text);
        }

        private static CallToolResult SkillUnavailable(string reason, bool listing = false)
        {
            var result = new Dictionary<string, object>
            {
                ["ok"] = false, ["status"] = "unavailable", ["reasonCode"] = reason,
                ["changed"] = false, ["uiInvoked"] = false, ["nativeClickInvoked"] = false, ["grantInvoked"] = false,
                ["error"] = McpLocalization.Get("skill_model.action_unavailable")
            };
            if (listing)
            { result["returned"] = Newtonsoft.Json.Linq.JValue.CreateNull(); result["skills"] = Newtonsoft.Json.Linq.JValue.CreateNull(); }
            return CallToolResult.Error(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
        }

        private static CallToolResult OpenSkillScreen(Newtonsoft.Json.Linq.JObject args)
        {
            bool dryRun = ToolUtil.GetBool(args, "dryRun", false);
            if (!dryRun && !ToolUtil.GetBool(args, "confirm", false)) return SkillUnavailable("confirmation_required");
            var resolved = DuplicantSkillObservation.ResolveTarget(args);
            if (resolved.Target == null) return SkillUnavailable(resolved.Reason ?? "target_required");
            var db = DuplicantSkillObservation.ReadDatabase(out string reason);
            if (db == null) return SkillUnavailable(reason);
            var context = DuplicantSkillObservation.ReadContext(db, resolved.Target);
            if (!context.Available) return SkillUnavailable(context.Reason);
            var observed = NativeSkillUi.Read(context.Target);
            bool invoked = false;
            bool opened = dryRun || NativeSkillUi.TryOpen(context.Target, out observed, out reason, out invoked);
            var result = new Dictionary<string, object>
            {
                ["ok"] = opened, ["preview"] = dryRun, ["partial"] = !opened && invoked,
                ["changed"] = invoked ? (object)Newtonsoft.Json.Linq.JValue.CreateNull() : false,
                ["changeScope"] = "ui", ["dupe"] = DupeRef(context.Target), ["uiInvoked"] = invoked,
                ["reasonCode"] = (object)reason ?? Newtonsoft.Json.Linq.JValue.CreateNull(),
                ["executionPath"] = "ManagementMenu.OpenSkills", ["ui"] = observed.ToDictionary()
            };
            if (!opened) result["error"] = McpLocalization.Get("skill_model.action_unavailable");
            string text = JsonConvert.SerializeObject(result, McpJsonUtil.Settings);
            return opened ? CallToolResult.Text(text) : CallToolResult.Error(text);
        }

        public static McpTool ControlSkill()
        {
            return new McpTool
            {
                Name = "dupes_skill_control", Group = "dupes", Mode = "write", Risk = "medium",
                Aliases = new List<string> { "dupe_skill_control", "skills_control" },
                Tags = new List<string> { "dupes", "skills", "management" },
                Description = McpLocalization.Get("skill_model.control_description"),
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.action"), Required = true, EnumValues = new List<string> { "list", "learn", "open" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("skill_model.id"), Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.name"), Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.query"), Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = McpLocalization.Get("skill_model.limit"), Required = false },
                    ["skillId"] = new McpToolParameter { Type = "string", Description = McpLocalization.Get("skill_model.skill_id"), Required = false },
                    ["includeUi"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.include_ui"), Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.force"), Required = false },
                    ["dryRun"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.dry_run"), Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("skill_model.confirm"), Required = false }
                },
                Handler = args =>
                {
                    switch (args["action"]?.ToString()?.Trim().ToLowerInvariant())
                    {
                        case "list": return ListSkills().Handler(args);
                        case "learn": return LearnSkill().Handler(args);
                        case "open": return OpenSkillScreen(args);
                        default: return CallToolResult.Error(McpLocalization.Get("skill_model.invalid_action"));
                    }
                }
            };
        }

        public static McpTool ListHatOptions()
        {
            return new McpTool
            {
                Name = "dupes_hats_list",
                Hidden = true,
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "skills_hats_list", "dupe_hats_list" },
                Tags = new List<string> { "dupes", "skills", "hats", "cosmetic", "management" },
                Description = "互換用の入口。複製人間の帽子の候補を一覧表示する。新しい呼び出しには dupes_control domain=hat action=list を使う",
                Parameters = DupeLookupParams(),
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");
                    return CallToolResult.Text(JsonConvert.SerializeObject(HatInfo(dupe), McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlHat()
        {
            return new McpTool
            {
                Name = "dupes_hat_control",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "dupe_hat_control", "skills_hat_control", "hat_control" },
                Tags = new List<string> { "dupes", "skills", "hats", "cosmetic", "management" },
                Description = "複製人間の帽子の一覧表示と設定を統合。action=list/set。set では hat または clear=true を指定できる。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["hat"] = new McpToolParameter { Type = "string", Description = "set の対象の帽子の prefabId。空欄または clear=true で解除可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "set で対象の帽子を解除するか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "set 操作の確認。true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = args["action"]?.ToString()?.Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListHatOptions().Handler(args);
                        case "set":
                            return SetHat().Handler(args);
                        default:
                            return CallToolResult.Error("Unsupported action; use list or set");
                    }
                }
            };
        }

        public static McpTool SetHat()
        {
            return new McpTool
            {
                Name = "dupes_hat_set",
                Hidden = true,
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "set_hat", "skills_hat_set", "dupe_hat_set" },
                Tags = new List<string> { "dupes", "skills", "hats", "cosmetic", "management" },
                Description = "互換用の入口。複製人間の帽子の設定または解除を行う。新しい呼び出しには dupes_control domain=hat action=set を使う。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["hat"] = new McpToolParameter { Type = "string", Description = "帽子の prefabId。空欄または clear=true で解除可能", Required = false },
                    ["clear"] = new McpToolParameter { Type = "boolean", Description = "対象の帽子を解除するか。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "帽子変更の確認。true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required");

                    var dupe = ToolUtil.FindDupe(args);
                    if (dupe == null)
                        return CallToolResult.Error("Duplicant not found");

                    var resume = dupe.GetComponent<MinionResume>();
                    if (resume == null)
                        return CallToolResult.Error("Duplicant has no MinionResume");

                    var before = HatInfo(dupe);
                    bool clear = ToolUtil.GetBool(args, "clear", false);
                    string hat = args["hat"]?.ToString()?.Trim();
                    if (clear || string.IsNullOrWhiteSpace(hat))
                    {
                        resume.SetHats(resume.CurrentHat, null);
                        resume.ApplyTargetHat();
                    }
                    else
                    {
                        var options = HatOptions(dupe);
                        var selected = options.FirstOrDefault(option => string.Equals((string)option["hat"], hat, StringComparison.OrdinalIgnoreCase));
                        if (selected == null)
                            return CallToolResult.Error("Hat not found in available options");

                        resume.SetHats(resume.CurrentHat, hat);
                        resume.ApplyTargetHat();
                    }

                    var after = HatInfo(dupe);
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = JsonConvert.SerializeObject(before) != JsonConvert.SerializeObject(after),
                        ["dupe"] = DupeRef(dupe),
                        ["before"] = before,
                        ["after"] = after
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static bool SkillMatches(Database.Skill skill, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            return Contains(skill.Id, q)
                || Contains(skill.Name, q)
                || Contains(skill.skillGroup.ToString(), q)
                || Contains(skill.description, q);
        }

        private static Dictionary<string, object> SkillToDictionary(Database.Skill skill, DuplicantSkillObservation.Context context,
            bool? mastered, NativeSkillUi.Observation ui = null)
        {
            var result = new Dictionary<string, object>
            {
                ["id"] = skill.Id, ["name"] = ToolUtil.CleanName(skill.Name),
                ["description"] = ToolUtil.CleanName(skill.description), ["skillGroup"] = skill.skillGroup.ToString(),
                ["tier"] = skill.tier, ["moraleExpectation"] = skill.GetMoraleExpectation(),
                ["priorSkills"] = skill.priorSkills, ["hat"] = skill.hat,
                ["requiredDuplicantModel"] = (object)skill.requiredDuplicantModel ?? Newtonsoft.Json.Linq.JValue.CreateNull()
            };
            if (context.Target != null)
            {
                bool? modelMatch = DuplicantSkillObservation.ModelMatches(skill, context);
                var state = DuplicantSkillObservation.ReadState(skill, context, mastered);
                result["mastered"] = DuplicantSkillObservation.JsonBoolean(state.Mastered);
                result["canMaster"] = DuplicantSkillObservation.JsonBoolean(state.CanMaster);
                result["conditions"] = (object)state.Conditions ?? Newtonsoft.Json.Linq.JValue.CreateNull();
                result["granted"] = DuplicantSkillObservation.JsonBoolean(state.Granted);
                result["masteryStatus"] = state.Reason == null ? "observed" : "unavailable";
                if (state.Reason != null) result["masteryReasonCode"] = state.Reason;
                result["modelMatch"] = DuplicantSkillObservation.JsonBoolean(modelMatch);
                result["candidateScope"] = modelMatch == false
                    ? mastered == true ? "mastered_history_model_mismatch" : mastered == false ? "model_mismatch" : "mastery_unknown_model_mismatch"
                    : modelMatch == true ? mastered == true ? "mastered_history" : "model_match"
                        : mastered == true ? "mastered_history_model_unknown" : "model_unknown";
            }
            if (ui != null) result["ui"] = NativeSkillUi.ReadWidget(ui, skill.Id, out _);
            return result;
        }

        private static Dictionary<string, object> HatInfo(MinionIdentity dupe)
        {
            var resume = dupe.GetComponent<MinionResume>();
            return new Dictionary<string, object>
            {
                ["dupe"] = DupeRef(dupe),
                ["currentHat"] = resume?.CurrentHat,
                ["targetHat"] = resume?.TargetHat,
                ["options"] = HatOptions(dupe)
            };
        }

        private static List<Dictionary<string, object>> HatOptions(MinionIdentity dupe)
        {
            var resume = dupe.GetComponent<MinionResume>();
            if (resume == null)
                return new List<Dictionary<string, object>>();

            return resume.GetAllHats()
                .Select(info => new Dictionary<string, object>
                {
                    ["source"] = info.Source,
                    ["hat"] = info.Hat,
                    ["count"] = info.count,
                    ["owned"] = resume.OwnsHat(info.Hat)
                })
                .OrderBy(item => item["source"].ToString())
                .ThenBy(item => item["hat"].ToString())
                .ToList();
        }

        private static Dictionary<string, McpToolParameter> DupeLookupParams()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false }
            };
        }
}
}
