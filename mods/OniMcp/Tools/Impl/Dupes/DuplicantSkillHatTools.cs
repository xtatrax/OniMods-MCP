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
                Name = "dupes_skills_list",
                Group = "dupes",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "dupes_skills", "skills_list" },
                Hidden = true,
                Description = "互換用の入口。dupes_control domain=skill action=list を使用すること。複製人間のスキルツリーのスキル、前提スキル、スキルグループ、士気の期待値、習得可否を一覧表示する",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "任意の複製人間の InstanceID。指定時はその複製人間の習得可否を付ける", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "任意の複製人間の名前。指定時はその複製人間の習得可否を付ける", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "スキルの ID/名前/グループのキーワード", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す件数。既定は 100、最大 300", Required = false }
                },
                Handler = args =>
                {
                    var dupe = ToolUtil.FindDupe(args);
                    var resume = dupe?.GetComponent<MinionResume>();
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 300));
                    var skills = Db.Get().Skills.resources
                        .Where(skill => skill != null && !skill.deprecated)
                        .Where(skill => string.IsNullOrWhiteSpace(query) || SkillMatches(skill, query))
                        .OrderBy(skill => skill.skillGroup)
                        .ThenBy(skill => skill.tier)
                        .ThenBy(skill => skill.Id)
                        .Take(limit)
                        .Select(skill => SkillToDictionary(skill, resume))
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["dupe"] = dupe == null ? null : DupeRef(dupe),
                        ["returned"] = skills.Count,
                        ["skills"] = skills
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool LearnSkill()
        {
            return new McpTool
            {
                Name = "dupes_learn_skill",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "learn_skill", "dupe_skill_learn", "skills_learn" },
                Hidden = true,
                Description = "互換用の入口。dupes_control domain=skill action=learn を使用すること。複製人間にスキルを1つ習得させる。既定ではスキルポイント、前提スキル、職業の制限に従う。force=true では GrantSkill による外部からの付与が可能",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["skillId"] = new McpToolParameter { Type = "string", Description = "スキル ID。例: Farming1、Mining1", Required = true },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "スキルポイント/前提条件を回避し、付与されたスキルとして記録する。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "複製人間のスキル変更の確認。true が必須", Required = true }
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

                    string skillId = args["skillId"]?.ToString();
                    var skill = string.IsNullOrWhiteSpace(skillId) ? null : Db.Get().Skills.TryGet(skillId.Trim());
                    if (skill == null || skill.deprecated)
                        return CallToolResult.Error("Skill not found");
                    if (resume.HasMasteredSkill(skill.Id))
                        return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["changed"] = false,
                            ["reason"] = "already_mastered",
                            ["dupe"] = DupeRef(dupe),
                            ["skill"] = SkillToDictionary(skill, resume)
                        }, McpJsonUtil.Settings));

                    bool force = ToolUtil.GetBool(args, "force", false);
                    var conditions = resume.GetSkillMasteryConditions(skill.Id);
                    if (!force && !resume.CanMasterSkill(conditions))
                        return CallToolResult.Error("Cannot master skill: " + string.Join(", ", conditions.Select(c => c.ToString()).ToArray()));

                    if (force)
                        resume.GrantSkill(skill.Id);
                    else
                        resume.MasterSkill(skill.Id);

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = true,
                        ["forceGranted"] = force,
                        ["dupe"] = DupeRef(dupe),
                        ["skill"] = SkillToDictionary(skill, resume),
                        ["availableSkillPoints"] = resume.AvailableSkillpoints
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlSkill()
        {
            return new McpTool
            {
                Name = "dupes_skill_control",
                Group = "dupes",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "dupe_skill_control", "skills_control" },
                Tags = new List<string> { "dupes", "skills", "management" },
                Description = "複製人間のスキル照会/習得の統合入口: action=list/learn。習得には confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "操作: list, learn", Required = true, EnumValues = new List<string> { "list", "learn" } },
                    ["id"] = new McpToolParameter { Type = "integer", Description = "複製人間の InstanceID", Required = false },
                    ["name"] = new McpToolParameter { Type = "string", Description = "複製人間の名前", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "list: スキルの ID/名前/グループのキーワード", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "list: 返す件数。既定は 100、最大 300", Required = false },
                    ["skillId"] = new McpToolParameter { Type = "string", Description = "learn: スキル ID。例: Farming1、Mining1", Required = false },
                    ["force"] = new McpToolParameter { Type = "boolean", Description = "learn: スキルポイント/前提条件を回避し、付与されたスキルとして記録する。既定は false", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "learn: 複製人間のスキル変更の確認。true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = args["action"]?.ToString()?.Trim().ToLowerInvariant();
                    switch (action)
                    {
                        case "list":
                            return ListSkills().Handler(args);
                        case "learn":
                            if (string.IsNullOrWhiteSpace(args["skillId"]?.ToString()))
                                return CallToolResult.Error("skillId is required for action=learn");
                            return LearnSkill().Handler(args);
                        default:
                            return CallToolResult.Error("action must be list or learn");
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

        private static Dictionary<string, object> SkillToDictionary(Database.Skill skill, MinionResume resume)
        {
            var result = new Dictionary<string, object>
            {
                ["id"] = skill.Id,
                ["name"] = ToolUtil.CleanName(skill.Name),
                ["description"] = ToolUtil.CleanName(skill.description),
                ["skillGroup"] = skill.skillGroup.ToString(),
                ["tier"] = skill.tier,
                ["moraleExpectation"] = skill.GetMoraleExpectation(),
                ["priorSkills"] = skill.priorSkills,
                ["hat"] = skill.hat
            };

            if (resume != null)
            {
                var conditions = resume.GetSkillMasteryConditions(skill.Id);
                result["mastered"] = resume.HasMasteredSkill(skill.Id);
                result["canMaster"] = resume.CanMasterSkill(conditions);
                result["conditions"] = conditions.Select(item => item.ToString()).ToList();
                result["granted"] = resume.HasBeenGrantedSkill(skill.Id);
            }

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
