using System.Collections.Generic;
namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private sealed class SupportValidation
        {
            public bool Valid;
            public bool WarningOnly;
            public string Rule;
            public Dictionary<string, object> Observation = new Dictionary<string, object>();
            public List<Dictionary<string, object>> MissingSupportCells = new List<Dictionary<string, object>>();
            public string Error;

            public static SupportValidation Success(string rule, List<Dictionary<string, object>> missing)
            {
                return new SupportValidation
                {
                    Valid = true,
                    WarningOnly = false,
                    Rule = rule,
                    MissingSupportCells = missing ?? new List<Dictionary<string, object>>()
                };
            }

            public static SupportValidation Warning(string rule, List<Dictionary<string, object>> missing, string error)
            {
                return new SupportValidation
                {
                    Valid = true,
                    WarningOnly = true,
                    Rule = rule,
                    MissingSupportCells = missing ?? new List<Dictionary<string, object>>(),
                    Error = error
                };
            }

            public static SupportValidation Invalid(string rule, List<Dictionary<string, object>> missing, string error)
            {
                return new SupportValidation
                {
                    Valid = false,
                    WarningOnly = false,
                    Rule = rule,
                    MissingSupportCells = missing ?? new List<Dictionary<string, object>>(),
                    Error = error
                };
            }

            public Dictionary<string, object> ToDictionary()
            {
                var result = new Dictionary<string, object>
                {
                    ["valid"] = Valid,
                    ["warningOnly"] = WarningOnly,
                    ["buildLocationRule"] = Rule,
                    ["missingSupportCells"] = MissingSupportCells,
                    ["error"] = Error
                };
                foreach (var item in Observation) result[item.Key] = item.Value;
                return result;
            }
        }

    }
}
