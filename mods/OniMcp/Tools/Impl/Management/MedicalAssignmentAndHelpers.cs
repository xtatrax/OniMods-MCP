using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class MedicalTools
    {
        public static McpTool BatchSetClinicThreshold()
        {
            return new McpTool
            {
                Name = "medical_clinics_threshold_batch_set",
                Group = "medical",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "medical_beds_threshold_batch_set", "clinics_threshold_batch_set" },
                Tags = new List<string> { "medical", "clinic", "bed", "threshold", "batch" },
                Description = "互換用の入口。区域内の医療用ベッド/診療所の治療しきい値の割合を一括設定する。新しい呼び出しには colony_control domain=management kind=medical action=batch_set_threshold を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["thresholdPercent"] = new McpToolParameter { Type = "number", Description = "治療しきい値の割合 0-100", Required = true },
                    ["query"] = new McpToolParameter { Type = "string", Description = "医療用ベッド/診療所の名前または prefabId で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "処理する最大件数。既定は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "医療用ベッドのしきい値の一括変更の確認。true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!HasRectInput(args))
                        return CallToolResult.Error("areaId or x1/y1/x2/y2 are required");
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to batch change medical clinic thresholds");
                    float? threshold = ToolUtil.GetFloat(args, "thresholdPercent");
                    if (!threshold.HasValue)
                        return CallToolResult.Error("thresholdPercent is required");

                    var rect = ToolUtil.GetRect(args);
                    int worldId = ToolUtil.ResolveWorldId(args);
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));
                    var changed = new List<Dictionary<string, object>>();

                    foreach (var clinic in Components.Clinics.Items
                                 .Where(clinic => clinic != null && ToolUtil.GameObjectMatchesWorld(clinic.gameObject, worldId))
                                 .Where(clinic => CellInRect(Grid.PosToCell(clinic.gameObject), rect, worldId))
                                 .Where(clinic => ClinicMatches(clinic, query))
                                 .OrderBy(clinic => TargetName(clinic.gameObject))
                                 .Take(limit))
                    {
                        SetClinicThresholdValue(clinic, threshold.Value);
                        changed.Add(ClinicInfo(clinic));
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["changed"] = changed.Count,
                        ["thresholdPercent"] = threshold.Value,
                        ["worldId"] = worldId,
                        ["clinics"] = changed
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool AssignMedicalBed()
        {
            return new McpTool
            {
                Name = "medical_bed_assign",
                Group = "medical",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "clinic_assign_patient", "medical_clinic_assign", "patient_bed_assign" },
                Tags = new List<string> { "medical", "clinic", "bed", "assign", "patient", "care" },
                Description = "互換用の入口。医療用ベッド/診療所の患者を割り当て、または解除する。治療しきい値も同時に設定できる。新しい呼び出しには colony_control domain=management kind=medical action=assign_bed を使う。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "割り当てる複製人間の InstanceID。action=assign では dupeName とどちらか一方を指定", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "割り当てる複製人間の名前。action=assign では dupeId とどちらか一方を指定", Required = false },
                    ["action"] = new McpToolParameter { Type = "string", Description = "assign または unassign。既定は assign", Required = false, EnumValues = new List<string> { "assign", "unassign" } },
                    ["thresholdPercent"] = new McpToolParameter { Type = "number", Description = "任意。治療しきい値の割合 0-100 を同時に設定する", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "医療用ベッドの割り当て変更の確認。true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to change a medical bed assignment");
                    var clinic = FindClinic(args);
                    if (clinic == null)
                        return CallToolResult.Error("Clinic target not found");
                    var assignable = clinic.GetComponent<Assignable>();
                    if (assignable == null)
                        return CallToolResult.Error("Clinic target is not assignable");

                    float? threshold = ToolUtil.GetFloat(args, "thresholdPercent");
                    if (threshold.HasValue)
                        SetClinicThresholdValue(clinic, threshold.Value);

                    string action = (args["action"]?.ToString() ?? "assign").Trim().ToLowerInvariant();
                    if (action == "unassign")
                    {
                        assignable.Unassign();
                    }
                    else if (action == "assign")
                    {
                        var dupeArgs = new JObject();
                        if (args["dupeId"] != null)
                            dupeArgs["id"] = args["dupeId"];
                        if (args["dupeName"] != null)
                            dupeArgs["name"] = args["dupeName"];
                        var dupe = ToolUtil.FindDupe(dupeArgs);
                        if (dupe == null)
                            return CallToolResult.Error("Duplicant not found");
                        assignable.Assign(dupe);
                    }
                    else
                    {
                        return CallToolResult.Error("action must be assign or unassign");
                    }

                    return CallToolResult.Text(JsonConvert.SerializeObject(ClinicInfo(clinic), McpJsonUtil.Settings));
                }
            };
        }

        private static JObject MedicalBedAssignArgs(JObject args)
        {
            var dispatched = args == null ? new JObject() : new JObject(args);
            string bedAction = dispatched["bedAction"]?.ToString();
            dispatched["action"] = string.IsNullOrWhiteSpace(bedAction) ? "assign" : bedAction.Trim().ToLowerInvariant();
            return dispatched;
        }

        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["id"] = new McpToolParameter { Type = "integer", Description = "対象オブジェクトの InstanceID", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "対象セルの X", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "対象セルの Y", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。座標で検索する場合の既定は現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> extra)
        {
            var parameters = new Dictionary<string, McpToolParameter>
            {
                ["areaId"] = new McpToolParameter { Type = "string", Description = "任意の区域ハンドル。指定時は x1/y1/x2/y2 を省略可能", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "区域の左下/始点 X。areaId を使う場合は省略可能", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "区域の左下/始点 Y。areaId を使う場合は省略可能", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "区域の右上/終点 X。areaId を使う場合は省略可能", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "区域の右上/終点 Y。areaId を使う場合は省略可能", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "対象のワールド ID。既定は areaId に紐付くワールド、または現在アクティブなワールド", Required = false }
            };
            foreach (var item in extra)
                parameters[item.Key] = item.Value;
            return parameters;
        }

        private static Clinic FindClinic(JObject args)
        {
            int? id = ToolUtil.GetInt(args, "id");
            int? x = ToolUtil.GetInt(args, "x");
            int? y = ToolUtil.GetInt(args, "y");
            int? cell = x.HasValue && y.HasValue ? Grid.XYToCell(x.Value, y.Value) : (int?)null;
            int worldId = cell.HasValue ? ToolUtil.ResolveWorldId(args) : (ToolUtil.GetInt(args, "worldId") ?? -1);
            foreach (var clinic in Components.Clinics.Items)
            {
                var go = clinic?.gameObject;
                if (go == null || !ToolUtil.GameObjectMatchesWorld(go, worldId))
                    continue;
                var kpid = go.GetComponent<KPrefabID>();
                if (id.HasValue && kpid != null && kpid.InstanceID == id.Value)
                    return clinic;
                if (cell.HasValue && Grid.PosToCell(go) == cell.Value)
                    return clinic;
            }
            return null;
        }

        private static void SetClinicThresholdValue(Clinic clinic, float thresholdPercent)
        {
            var slider = (ISliderControl)clinic;
            float value = Mathf.Clamp(thresholdPercent, slider.GetSliderMin(0), slider.GetSliderMax(0));
            slider.SetSliderValue(value, 0);
        }

        private static bool ClinicMatches(Clinic clinic, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            var go = clinic.gameObject;
            var kpid = go.GetComponent<KPrefabID>();
            string q = query.Trim();
            return Contains(TargetName(go), q) || Contains(kpid?.PrefabTag.Name, q);
        }

        private static Dictionary<string, object> ClinicInfo(Clinic clinic)
        {
            var go = clinic.gameObject;
            int cell = Grid.PosToCell(go);
            var kpid = go.GetComponent<KPrefabID>();
            var assignable = go.GetComponent<Assignable>();
            var slider = (ISliderControl)clinic;
            var prioritizable = go.GetComponent<Prioritizable>();
            var priority = prioritizable == null ? default(PrioritySetting) : prioritizable.GetMasterPriority();
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["name"] = TargetName(go),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1,
                ["thresholdPercent"] = slider.GetSliderValue(0),
                ["thresholdTooltip"] = slider.GetSliderTooltip(0),
                ["medicalAttentionMinimum"] = Math.Round(clinic.MedicalAttentionMinimum, 3),
                ["assigned"] = assignable?.assignee != null && !assignable.assignee.IsNull(),
                ["assignee"] = assignable?.assignee == null || assignable.assignee.IsNull() ? null : assignable.assignee.GetProperName(),
                ["priority"] = prioritizable == null ? (object)null : new Dictionary<string, object>
                {
                    ["class"] = priority.priority_class.ToString(),
                    ["value"] = priority.priority_value
                }
            };
        }

        private static Dictionary<string, object> PatientInfo(MinionIdentity dupe)
        {
            var health = dupe.GetComponent<Health>();
            var sicknesses = dupe.gameObject.GetSicknesses();
            var sicknessItems = SicknessList(sicknesses);
            var medicalBed = FindAssignedMedicalBed(dupe);
            var amounts = dupe.GetComponent<Amounts>();
            var stress = amounts?.Get(Db.Get().Amounts.Stress);
            var radiation = Game.IsDlcActiveForCurrentSave("EXPANSION1_ID") ? amounts?.Get(Db.Get().Amounts.RadiationBalance) : null;
            bool injured = health != null && health.hitPoints < health.maxHitPoints;
            bool sick = sicknessItems.Count > 0;
            return new Dictionary<string, object>
            {
                ["id"] = dupe.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                ["name"] = dupe.GetProperName(),
                ["worldId"] = dupe.GetMyWorldId(),
                ["needsMedicalAttention"] = injured || sick || (radiation != null && radiation.value > 0f),
                ["health"] = health == null ? null : new Dictionary<string, object>
                {
                    ["hitPoints"] = Math.Round(health.hitPoints, 2),
                    ["maxHitPoints"] = Math.Round(health.maxHitPoints, 2),
                    ["percent"] = Math.Round(health.hitPoints / Math.Max(1f, health.maxHitPoints) * 100f, 1)
                },
                ["sicknesses"] = sicknessItems,
                ["stress"] = stress == null ? (object)null : Math.Round(stress.value, 2),
                ["radiationBalance"] = radiation == null ? (object)null : Math.Round(radiation.value, 2),
                ["assignedMedicalBed"] = medicalBed == null ? null : AssignableSummary(medicalBed)
            };
        }

        private static List<Dictionary<string, object>> SicknessList(Sicknesses sicknesses)
        {
            var result = new List<Dictionary<string, object>>();
            if (sicknesses == null)
                return result;

            foreach (SicknessInstance instance in sicknesses.ModifierList)
            {
                if (instance == null)
                    continue;
                result.Add(new Dictionary<string, object>
                {
                    ["id"] = instance.Sickness.id,
                    ["name"] = instance.Sickness.Name,
                    ["descriptiveSymptoms"] = instance.Sickness.DescriptiveSymptoms.ToString(),
                    ["exposure"] = instance.ExposureInfo.sourceInfo
                });
            }
            return result;
        }

        private static Dictionary<string, object> DoctorStationInfo(DoctorStation station)
        {
            var go = station.gameObject;
            int cell = Grid.PosToCell(go);
            var kpid = go.GetComponent<KPrefabID>();
            var operational = go.GetComponent<Operational>();
            var medicine = new List<Dictionary<string, object>>();
            var cures = new HashSet<string>();
            foreach (var item in station.storage.items)
            {
                if (item == null)
                    continue;
                var pill = item.GetComponent<MedicinalPill>();
                if (pill == null)
                    continue;
                var itemId = item.GetComponent<KPrefabID>();
                var cured = pill.info.curedSicknesses ?? new List<string>();
                foreach (string sickness in cured)
                    cures.Add(sickness);
                medicine.Add(new Dictionary<string, object>
                {
                    ["prefabId"] = itemId?.PrefabTag.Name ?? item.name,
                    ["name"] = ToolUtil.CleanName(item.GetProperName()),
                    ["cures"] = cured.OrderBy(id => id).ToList()
                });
            }

            var treatablePatients = Components.LiveMinionIdentities.Items
                .Where(dupe => dupe != null && ToolUtil.GameObjectMatchesWorld(dupe.gameObject, Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1))
                .Where(dupe => station.IsTreatmentAvailable(dupe.gameObject))
                .Select(dupe => new Dictionary<string, object>
                {
                    ["id"] = dupe.GetComponent<KPrefabID>()?.InstanceID ?? -1,
                    ["name"] = dupe.GetProperName(),
                    ["doctorAvailable"] = station.IsDoctorAvailable(dupe.gameObject)
                })
                .ToList();

            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["name"] = TargetName(go),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["worldId"] = Grid.IsValidCell(cell) && Grid.IsWorldValidCell(cell) ? Grid.WorldIdx[cell] : -1,
                ["isOperational"] = operational?.IsOperational ?? false,
                ["medicine"] = medicine,
                ["cures"] = cures.OrderBy(id => id).ToList(),
                ["treatablePatients"] = treatablePatients
            };
        }

        private static Assignable FindAssignedMedicalBed(MinionIdentity dupe)
        {
            foreach (var assignable in Components.AssignableItems.Items)
            {
                if (assignable == null || !string.Equals(assignable.slotID, Db.Get().AssignableSlots.MedicalBed.Id, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (assignable.assignee != null && !assignable.assignee.IsNull() && string.Equals(assignable.assignee.GetProperName(), dupe.GetProperName(), StringComparison.OrdinalIgnoreCase))
                    return assignable;
            }
            return null;
        }

        private static Dictionary<string, object> AssignableSummary(Assignable assignable)
        {
            var go = assignable.gameObject;
            int cell = Grid.PosToCell(go);
            var kpid = go.GetComponent<KPrefabID>();
            return new Dictionary<string, object>
            {
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["name"] = TargetName(go),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1
            };
        }

        private static bool PatientMatches(Dictionary<string, object> patient, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            if (patient["name"].ToString().IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            var sicknesses = patient["sicknesses"] as List<Dictionary<string, object>>;
            return sicknesses != null && sicknesses.Any(item => Contains(item["id"]?.ToString(), q) || Contains(item["name"]?.ToString(), q));
        }

        private static bool DoctorStationMatches(Dictionary<string, object> station, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return true;
            string q = query.Trim();
            if (Contains(station["name"]?.ToString(), q) || Contains(station["prefabId"]?.ToString(), q))
                return true;
            var cures = station["cures"] as List<string>;
            if (cures != null && cures.Any(cure => Contains(cure, q)))
                return true;
            var medicine = station["medicine"] as List<Dictionary<string, object>>;
            return medicine != null && medicine.Any(item => Contains(item["prefabId"]?.ToString(), q) || Contains(item["name"]?.ToString(), q));
        }

        private static bool HasRectInput(JObject args)
        {
            return !string.IsNullOrWhiteSpace(args["areaId"]?.ToString())
                || (args["x1"] != null && args["y1"] != null && args["x2"] != null && args["y2"] != null);
        }

        private static bool CellInRect(int cell, Dictionary<string, int> rect, int worldId)
        {
            if (!Grid.IsValidCell(cell)) return false;
            if (!ToolUtil.CellMatchesWorld(cell, worldId)) return false;
            int x = Grid.CellColumn(cell);
            int y = Grid.CellRow(cell);
            return x >= rect["x1"] && x <= rect["x2"] && y >= rect["y1"] && y <= rect["y2"];
        }

        private static string TargetName(GameObject go)
        {
            return ToolUtil.CleanName(go.GetProperName());
        }

        private static bool Contains(string value, string query)
        {
            return !string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
