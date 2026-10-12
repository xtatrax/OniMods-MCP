using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class MedicalTools
    {
        public static McpTool ListPatients()
        {
            return new McpTool
            {
                Name = "medical_patients_list",
                Group = "medical",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "sick_dupes_list", "injured_dupes_list", "patients_list" },
                Tags = new List<string> { "medical", "patients", "sickness", "health", "dupes" },
                Description = "互換用の入口。医療上の注意が必要な複製人間を一覧表示する。新しい呼び出しには colony_control domain=management kind=medical action=patients を使う。",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["includeHealthy"] = new McpToolParameter { Type = "boolean", Description = "健康な複製人間も含めるか。既定は false", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "複製人間の名前または病気 ID で絞り込む", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定では絞り込まない", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    int worldId = ToolUtil.GetInt(args, "worldId") ?? -1;
                    bool includeHealthy = ToolUtil.GetBool(args, "includeHealthy", false);
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var patients = Components.LiveMinionIdentities.Items
                        .Where(dupe => dupe != null && ToolUtil.GameObjectMatchesWorld(dupe.gameObject, worldId))
                        .Select(PatientInfo)
                        .Where(info => includeHealthy || (bool)info["needsMedicalAttention"])
                        .Where(info => PatientMatches(info, query))
                        .OrderByDescending(info => (bool)info["needsMedicalAttention"])
                        .ThenBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = patients.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["includeHealthy"] = includeHealthy,
                        ["patients"] = patients
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlMedical()
        {
            return new McpTool
            {
                Name = "medical_control",
                Group = "medical",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "medical_care_control", "clinic_control", "patient_control" },
                Tags = new List<string> { "medical", "patients", "clinic", "doctor", "threshold", "assign", "batch" },
                Description = "医療状態の読み取りと管理を統合。action=patients/clinics/doctor_stations/set_threshold/batch_set_threshold/assign_bed。",
                Parameters = RectParams(LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "patients、clinics、doctor_stations、set_threshold、batch_set_threshold、assign_bed。既定は patients", Required = false, EnumValues = new List<string> { "patients", "clinics", "doctor_stations", "set_threshold", "batch_set_threshold", "assign_bed" } },
                    ["includeHealthy"] = new McpToolParameter { Type = "boolean", Description = "action=patients で健康な複製人間も含めるか。既定は false", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "複製人間、病気、設備、prefab、医薬品などで絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す、または処理する最大件数。既定は 100、最大 500", Required = false },
                    ["thresholdPercent"] = new McpToolParameter { Type = "number", Description = "action=set_threshold/batch_set_threshold/assign_bed で設定できる治療しきい値の割合 0-100", Required = false },
                    ["dupeId"] = new McpToolParameter { Type = "integer", Description = "action=assign_bed で割り当てる複製人間の InstanceID", Required = false },
                    ["dupeName"] = new McpToolParameter { Type = "string", Description = "action=assign_bed で割り当てる複製人間の名前", Required = false },
                    ["bedAction"] = new McpToolParameter { Type = "string", Description = "action=assign_bed では assign または unassign。既定は assign", Required = false, EnumValues = new List<string> { "assign", "unassign" } },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "書き込み操作では true が必須", Required = false }
                })),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "patients").Trim().ToLowerInvariant();
                    if (action == "patients")
                        return ListPatients().Handler(args);
                    if (action == "clinics")
                        return ListClinics().Handler(args);
                    if (action == "doctor_stations")
                        return ListDoctorStations().Handler(args);
                    if (action == "set_threshold")
                        return SetClinicThreshold().Handler(args);
                    if (action == "batch_set_threshold")
                        return BatchSetClinicThreshold().Handler(args);
                    if (action == "assign_bed")
                        return AssignMedicalBed().Handler(MedicalBedAssignArgs(args));
                    return CallToolResult.Error("action must be patients, clinics, doctor_stations, set_threshold, batch_set_threshold, or assign_bed");
                }
            };
        }

        public static McpTool ListClinics()
        {
            return new McpTool
            {
                Name = "medical_clinics_list",
                Group = "medical",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "medical_beds_list", "clinics_list" },
                Tags = new List<string> { "medical", "clinic", "bed", "threshold", "care" },
                Description = "互換用の入口。医療用ベッド/診療所と、治療しきい値、割り当て対象、優先度を一覧表示する。新しい呼び出しには colony_control domain=management kind=medical action=clinics を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var clinics = Components.Clinics.Items
                        .Where(clinic => clinic != null && ToolUtil.GameObjectMatchesWorld(clinic.gameObject, worldId))
                        .Where(clinic => rect == null || CellInRect(Grid.PosToCell(clinic.gameObject), rect, worldId))
                        .OrderBy(clinic => TargetName(clinic.gameObject))
                        .Take(limit)
                        .Select(ClinicInfo)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = clinics.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["clinics"] = clinics
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool ControlClinic()
        {
            return new McpTool
            {
                Name = "medical_clinic_control",
                Group = "medical",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "medical_clinics_control", "medical_bed_control", "clinic_threshold_control" },
                Tags = new List<string> { "medical", "clinic", "bed", "threshold", "care", "batch" },
                Description = "互換用の入口。医療用ベッドや診療所の治療しきい値の読み取り/設定を統合。新しい呼び出しには colony_control domain=management kind=medical action=clinics/set_threshold/batch_set_threshold を使う。",
                Parameters = RectParams(LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list、set、batch。既定は list", Required = false, EnumValues = new List<string> { "list", "set", "batch" } },
                    ["thresholdPercent"] = new McpToolParameter { Type = "number", Description = "治療しきい値の割合 0-100。action=set/batch では必須", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=batch/list で医療用ベッド/診療所の名前または prefabId で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す、または処理する最大件数。既定は 100、最大 500", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set/batch でしきい値を変更する場合は true が必須", Required = false }
                })),
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "list").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListClinics().Handler(args);
                    if (action == "set")
                        return SetClinicThreshold().Handler(args);
                    if (action == "batch")
                        return BatchSetClinicThreshold().Handler(args);
                    return CallToolResult.Error("action must be list, set, or batch");
                }
            };
        }

        public static McpTool ListDoctorStations()
        {
            return new McpTool
            {
                Name = "doctor_stations_list",
                Group = "medical",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "medical_doctor_stations_list", "treatment_stations_list" },
                Tags = new List<string> { "medical", "doctor", "station", "medicine", "treatment" },
                Description = "互換用の入口。医務室/診療所、医薬品の在庫、治療可能な病気、現在治療可能な患者を一覧表示する。新しい呼び出しには colony_control domain=management kind=medical action=doctor_stations を使う。",
                Parameters = RectParams(new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string", Description = "設備名、prefabId、医薬品、病気 ID で絞り込む", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 100、最大 500", Required = false }
                }),
                Handler = args =>
                {
                    bool hasRect = HasRectInput(args);
                    var rect = hasRect ? ToolUtil.GetRect(args) : null;
                    int worldId = hasRect || ToolUtil.GetInt(args, "worldId").HasValue ? ToolUtil.ResolveWorldId(args) : -1;
                    string query = args["query"]?.ToString();
                    int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 100, 500));

                    var stations = Components.BuildingCompletes.Items
                        .Select(building => building?.GetComponent<DoctorStation>())
                        .Where(station => station != null && ToolUtil.GameObjectMatchesWorld(station.gameObject, worldId))
                        .Where(station => rect == null || CellInRect(Grid.PosToCell(station.gameObject), rect, worldId))
                        .Select(DoctorStationInfo)
                        .Where(info => DoctorStationMatches(info, query))
                        .OrderBy(info => info["name"].ToString())
                        .Take(limit)
                        .ToList();

                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["returned"] = stations.Count,
                        ["worldId"] = worldId >= 0 ? (object)worldId : null,
                        ["doctorStations"] = stations
                    }, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetClinicThreshold()
        {
            return new McpTool
            {
                Name = "medical_clinic_threshold_set",
                Group = "medical",
                Mode = "write",
                Risk = "medium",
                Hidden = true,
                Aliases = new List<string> { "medical_bed_threshold_set", "clinic_threshold_set" },
                Tags = new List<string> { "medical", "clinic", "bed", "threshold", "care" },
                Description = "互換用の入口。医療用ベッド/診療所の治療しきい値の割合を設定する。新しい呼び出しには colony_control domain=management kind=medical action=set_threshold を使う。",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["thresholdPercent"] = new McpToolParameter { Type = "number", Description = "治療しきい値の割合 0-100", Required = true },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "医療用ベッドのしきい値変更の確認。true が必須", Required = true }
                }),
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to change a medical clinic threshold");
                    var clinic = FindClinic(args);
                    if (clinic == null)
                        return CallToolResult.Error("Clinic target not found");
                    float? threshold = ToolUtil.GetFloat(args, "thresholdPercent");
                    if (!threshold.HasValue)
                        return CallToolResult.Error("thresholdPercent is required");

                    SetClinicThresholdValue(clinic, threshold.Value);
                    return CallToolResult.Text(JsonConvert.SerializeObject(ClinicInfo(clinic), McpJsonUtil.Settings));
                }
            };
        }

    }
}
