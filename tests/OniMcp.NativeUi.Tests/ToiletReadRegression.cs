using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;

internal static class ToiletReadRegression
{
    private static int checks;
    private static readonly string[] SmiFields =
    {
        "flushesRemaining", "isSoiled", "isCloggedWithGunk", "hasCleanChore", "canCleanEarly"
    };

    private static void Check(bool value, string name)
    {
        checks++;
        if (!value) throw new InvalidOperationException("toilet_passive_read_contract: " + name);
    }
    private static Toilet Fixture(int used = 3, int max = 15, bool ready = true)
    {
        var toilet = new Toilet(used, max);
        var target = new GameObject { Id = 73, name = "Fixture outhouse" };
        target.Add(toilet);
        if (ready) toilet.Cache = new Toilet.StatesInstance(toilet);
        return toilet;
    }
    private static JObject Wire(System.Collections.Generic.Dictionary<string, object> value)
    {
        return JObject.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
    }
    private static JObject Info(Toilet toilet) { return Wire(MaintenanceActionTools.ReadToiletInfoForTest(toilet)); }
    private static JObject ActionRow(Toilet toilet) { return Wire(MaintenanceActionTools.ReadToiletActionForTest(toilet)); }
    private static bool Reason(JObject info, string field, string reason)
    {
        return ((JArray)info["unavailableFields"]).Any(row => (string)row["field"] == field && (string)row["reasonCode"] == reason);
    }
    private static void Unknown(JObject info, string reason)
    {
        foreach (string field in SmiFields)
            Check(info.Property(field) != null && info[field].Type == JTokenType.Null && Reason(info, field, reason), "explicit unknown field/reason " + field + reason);
        Check((string)info["stateMachineAvailability"] == "unavailable" && (string)info["stateMachineReason"] == reason,
            "cache availability reason " + reason);
        Check(info.Property("currentState") != null && info["currentState"].Type == JTokenType.Null, "unknown state is explicit null");
    }
    private static void Passive(Toilet toilet, StateMachine.Instance cache, int expectedRaw, int used, int max)
    {
        Check(ReferenceEquals(toilet.Cache, cache) && toilet.RawReads == expectedRaw, "same cache and bounded raw reads");
        Check(toilet.LazyReads == 0 && toilet.FactoryCalls == 0 && toilet.SetterCalls == 0, "no lazy factory or setter");
        Check(toilet.SerializedFlushesForTest == used && toilet.SerializedMaxForTest == max, "serialized master values unchanged");
        var smi = cache as Toilet.StatesInstance;
        if (smi != null) Check(smi.StartCalls == 0 && smi.GoToCalls == 0 && smi.RefreshCalls == 0, "no native start/transition/refresh");
    }

    internal static void LegacyMissingOnly()
    {
        var toilet = Fixture(4, 15, false);
        toilet.PoisonLazyGetter = false;
        var before = toilet.Cache;
        var info = Info(toilet);
        Check(toilet.LazyReads == 0 && toilet.FactoryCalls == 0 && ReferenceEquals(toilet.Cache, before),
            "missing cache read must not invoke lazy getter or construct SMI");
        Unknown(info, "cached_smi_missing");
        Check((int)info["flushesUsed"] == 4 && (int)info["maxFlushes"] == 15, "missing cache retains master data");
        Passive(toilet, before, 1, 4, 15);
        Console.WriteLine("PASS: toilet missing-cache passive read assertions=" + checks);
    }

    private static void MissingAndRepeat()
    {
        var toilet = Fixture(4, 15, false);
        var first = Info(toilet);
        Unknown(first, "cached_smi_missing");
        Check((int)first["flushesUsed"] == 4 && (int)first["maxFlushes"] == 15, "missing cache retains serialized values");
        Check(JToken.DeepEquals(first, Info(toilet)), "unknown cache repeat is stable");
        Passive(toilet, null, 2, 4, 15);
        var row = ActionRow(toilet);
        Check(!(bool)row["canExecute"] && row["state"]["canCleanEarly"].Type == JTokenType.Null,
            "action false guard does not replace unknown native predicate");
        Passive(toilet, null, 3, 4, 15);
        var missing = Info(null);
        Unknown(missing, "toilet_missing");
        Check(missing["flushesUsed"].Type == JTokenType.Null && missing["maxFlushes"].Type == JTokenType.Null,
            "missing master values remain unknown");
    }

    private static void CacheGuards()
    {
        var wrongType = Fixture();
        var foreign = new ForeignToiletState(); wrongType.Cache = foreign;
        Unknown(Info(wrongType), "cached_smi_type_mismatch");
        Check(foreign.CurrentStateReads == 0, "foreign instance current-state reader not invoked");
        Passive(wrongType, foreign, 1, 3, 15);

        var inheritedState = Fixture();
        var inherited = new InheritedToiletState(inheritedState); inheritedState.Cache = inherited;
        Unknown(Info(inheritedState), "cached_smi_type_mismatch");
        Check(inherited.CurrentStateReads == 0, "inherited SMI rejected before virtual state read");
        Passive(inheritedState, inherited, 1, 3, 15);

        var wrongOwner = Fixture();
        var other = Fixture(8);
        var wrong = new Toilet.StatesInstance(other); wrongOwner.Cache = wrong;
        Unknown(Info(wrongOwner), "cached_smi_owner_mismatch");
        Check(wrong.CurrentStateReads == 0 && wrong.RemainingReads == 0, "wrong owner does not reach state/value readers");
        Passive(wrongOwner, wrong, 1, 3, 15);
        Check(other.RawReads == 0 && other.MasterUsedReads == 0, "foreign master not read");

        var unstarted = Fixture();
        var unstartedSmi = (Toilet.StatesInstance)unstarted.Cache; unstartedSmi.CurrentState = null;
        Unknown(Info(unstarted), "cached_smi_unstarted");
        Check(unstartedSmi.CurrentStateReads == 1 && unstartedSmi.RemainingReads == 0 && unstartedSmi.SoiledReads == 0,
            "unstarted reader does not inspect derived state values");
        Passive(unstarted, unstartedSmi, 1, 3, 15);

        var inheritedToilet = new InheritedToilet(6, 15); new GameObject().Add(inheritedToilet);
        inheritedToilet.Cache = new Toilet.StatesInstance(inheritedToilet);
        Unknown(Info(inheritedToilet), "toilet_type_unsupported");
        Check(inheritedToilet.OverrideCalls == 0, "inherited Toilet rejected before virtual GetSMI");
        Passive(inheritedToilet, inheritedToilet.Cache, 0, 6, 15);
    }

    private static void CacheExceptions()
    {
        foreach (string fault in new[] { "raw", "owner", "state" })
        {
            var toilet = Fixture(); var smi = (Toilet.StatesInstance)toilet.Cache;
            string reason;
            if (fault == "raw") { toilet.ThrowRaw = true; reason = "cached_smi_read_failed"; }
            else if (fault == "owner") { smi.ThrowOwner = true; reason = "cached_smi_owner_read_failed"; }
            else { smi.ThrowState = true; reason = "cached_smi_state_read_failed"; }
            var info = Info(toilet); Unknown(info, reason);
            Check((int)info["flushesUsed"] == 3 && (int)info["maxFlushes"] == 15, "cache fault preserves master data " + fault);
            Check(smi.RemainingReads == 0 && smi.SoiledReads == 0 && smi.CloggedReads == 0 && smi.ChoreReads == 0,
                "cache fault does not reach derived getters " + fault);
            Passive(toilet, smi, 1, 3, 15);
        }
    }

    private static void ObservedValuesAndPredicate()
    {
        foreach (bool full in new[] { false, true }) foreach (int used in new[] { 0, 1 }) foreach (bool chore in new[] { false, true })
        {
            var toilet = Fixture(used); var smi = (Toilet.StatesInstance)toilet.Cache;
            smi.CurrentState = full ? smi.sm.full : smi.sm.other;
            smi.CleanChoreForTest = chore ? new Chore() : null;
            var stateBefore = smi.CurrentState; var choreBefore = smi.CleanChoreForTest;
            var row = ActionRow(toilet); var info = (JObject)row["state"];
            bool expected = !full && used > 0 && !chore;
            Check((bool)row["canExecute"] == expected && (bool)info["canCleanEarly"] == expected,
                "native full/soiled/chore predicate and guard share one observation");
            Check((int)info["flushesUsed"] == used && (int)info["maxFlushes"] == 15 && (int)info["flushesRemaining"] == 15 - used,
                "native count values preserved");
            Check((bool)info["isSoiled"] == (used > 0) && !(bool)info["isCloggedWithGunk"] && (bool)info["hasCleanChore"] == chore,
                "observed false is a boolean and not missing");
            Check((string)info["stateMachineAvailability"] == "observed" && info.Property("stateMachineReason") != null
                && info["stateMachineReason"].Type == JTokenType.Null && !((JArray)info["unavailableFields"]).Any(), "known availability and explicit null reason");
            Check((string)info["currentState"] == stateBefore.name && (string)info["currentStateSource"] == "Toilet.GetSMI().GetCurrentState().name",
                "small native state ID and source retained");
            Check((int)info["id"] == 73 && (string)info["prefabId"] == "Outhouse" && (bool)info["fixtureMetadata"], "legacy target metadata retained");
            Check((string)row["actionKey"] == "clean_toilet" && (string)row["category"] == "maintenance" && (string)row["componentType"] == "Toilet",
                "protocol action identity retained");
            Check(smi.CurrentStateReads == 1 && smi.RemainingReads == 1 && smi.SoiledReads == 1 && smi.CloggedReads == 1 && smi.ChoreReads == 1,
                "action and state reuse one cached snapshot");
            Check(ReferenceEquals(smi.CurrentState, stateBefore) && ReferenceEquals(smi.CleanChoreForTest, choreBefore), "native state and chore references unchanged");
            Passive(toilet, smi, 1, used, 15);
        }
        var cloggedToilet = Fixture(2); var cloggedSmi = (Toilet.StatesInstance)cloggedToilet.Cache; cloggedSmi.Clogged = true;
        var cloggedInfo = Info(cloggedToilet);
        Check((bool)cloggedInfo["isCloggedWithGunk"] && (bool)cloggedInfo["canCleanEarly"], "observed clogged flag is retained independently of native early-clean predicate");
        Passive(cloggedToilet, cloggedSmi, 1, 2, 15);
        foreach (int used in new[] { 0, 15, 17 })
        {
            int max = used == 0 ? 0 : 15;
            var toilet = Fixture(used, max); var smi = (Toilet.StatesInstance)toilet.Cache;
            smi.CurrentState = smi.sm.full;
            var info = Info(toilet);
            Check(info["flushesRemaining"].Type == JTokenType.Integer && (int)info["flushesRemaining"] == max - used,
                "real zero and negative remaining counts are not clamped or defaulted");
            Check(info["isSoiled"].Type == JTokenType.Boolean && info["hasCleanChore"].Type == JTokenType.Boolean
                && info["isCloggedWithGunk"].Type == JTokenType.Boolean && info["canCleanEarly"].Type == JTokenType.Boolean,
                "known false values survive the actual JSON settings");
            Passive(toilet, smi, 1, used, max);
        }
    }

    private static void PartialGetterFaults()
    {
        foreach (string fault in new[] { "remaining", "soiled", "clogged", "chore", "full_definition" })
        {
            var toilet = Fixture(5); var smi = (Toilet.StatesInstance)toilet.Cache;
            string field;
            if (fault == "remaining") { smi.ThrowRemaining = true; field = "flushesRemaining"; }
            else if (fault == "soiled") { smi.ThrowSoiled = true; field = "isSoiled"; }
            else if (fault == "clogged") { smi.ThrowClogged = true; field = "isCloggedWithGunk"; }
            else if (fault == "chore") { smi.ThrowChore = true; field = "hasCleanChore"; }
            else { smi.sm.full = null; field = "canCleanEarly"; }
            var row = ActionRow(toilet); var info = (JObject)row["state"];
            string reason = fault == "full_definition" ? "native_state_definition_missing" : "native_read_failed";
            Check(info.Property(field) != null && info[field].Type == JTokenType.Null && Reason(info, field, reason), "individual getter fault is explicit unknown " + fault);
            Check((int)info["flushesUsed"] == 5 && (int)info["maxFlushes"] == 15 && (string)info["stateMachineAvailability"] == "observed",
                "individual getter fault retains master and ready-cache status");
            if (fault != "remaining") Check((int)info["flushesRemaining"] == 10, "unrelated remaining value retained " + fault);
            if (fault != "soiled") Check((bool)info["isSoiled"], "unrelated soiled value retained " + fault);
            if (fault != "clogged") Check(!(bool)info["isCloggedWithGunk"], "unrelated clogged value retained " + fault);
            if (fault != "chore") Check(!(bool)info["hasCleanChore"], "unrelated chore value retained " + fault);
            bool conditionKnown = fault == "remaining" || fault == "clogged";
            Check(conditionKnown ? (bool)info["canCleanEarly"] && (bool)row["canExecute"]
                : info["canCleanEarly"].Type == JTokenType.Null && !(bool)row["canExecute"], "only predicate dependencies control guard availability " + fault);
            Passive(toilet, smi, 1, 5, 15);
        }
        foreach (bool usedFault in new[] { true, false })
        {
            var toilet = Fixture(5); var smi = (Toilet.StatesInstance)toilet.Cache;
            toilet.ThrowUsed = usedFault; toilet.ThrowMax = !usedFault;
            var info = Info(toilet); string missing = usedFault ? "flushesUsed" : "maxFlushes";
            Check(info[missing].Type == JTokenType.Null && Reason(info, missing, "master_read_failed"), "master read fault is not substituted with zero");
            Check(usedFault ? (int)info["maxFlushes"] == 15 : (int)info["flushesUsed"] == 5, "independent master value retained");
            Check(!(bool)info["isCloggedWithGunk"] && !(bool)info["hasCleanChore"], "unrelated ready-cache values retained on master fault");
            Passive(toilet, smi, 1, 5, 15);
        }
        var noDefinition = Fixture(); var noDefinitionSmi = (Toilet.StatesInstance)noDefinition.Cache; noDefinitionSmi.sm = null;
        var missingDefinition = Info(noDefinition);
        Check(missingDefinition["isCloggedWithGunk"].Type == JTokenType.Null && Reason(missingDefinition, "isCloggedWithGunk", "native_read_failed"),
            "missing parameter context remains unknown");
        Check(missingDefinition["canCleanEarly"].Type == JTokenType.Null && Reason(missingDefinition, "canCleanEarly", "native_state_definition_missing"),
            "missing definition is not created for predicate");
        Check((int)missingDefinition["flushesRemaining"] == 12 && (bool)missingDefinition["isSoiled"], "missing definition retains independent native observations");
        Passive(noDefinition, noDefinitionSmi, 1, 3, 15);
        var unnamed = Fixture(); var unnamedSmi = (Toilet.StatesInstance)unnamed.Cache; unnamedSmi.CurrentState.name = null;
        var unnamedState = Info(unnamed);
        Check(unnamedState["currentState"].Type == JTokenType.Null && Reason(unnamedState, "currentState", "native_state_name_missing"),
            "missing native state name does not stringify or serialize SMI");
        Check((int)unnamedState["flushesRemaining"] == 12 && (bool)unnamedState["canCleanEarly"], "optional state ID does not gate native predicate");
        Passive(unnamed, unnamedSmi, 1, 3, 15);
    }

    private static void RepeatAndNativeTitle()
    {
        var provider = McpLocalization.LanguageProvider;
        string title = STRINGS.UI.USERMENUACTIONS.CLEANTOILET.NAME;
        try
        {
            STRINGS.UI.USERMENUACTIONS.CLEANTOILET.NAME = "本体の清掃名";
            var toilet = Fixture(7); var smi = (Toilet.StatesInstance)toilet.Cache;
            JObject first = null;
            foreach (string language in new[] { "en", "ja-JP", "zh-cn" })
            {
                McpLocalization.LanguageProvider = () => language;
                var row = ActionRow(toilet);
                Check((string)row["title"] == "本体の清掃名", "native game-localized action name does not follow independent MCP prose language");
                if (first == null) first = row;
                else Check(JToken.DeepEquals(first, row), "repeat values remain stable across MCP language changes");
            }
            Check(smi.CurrentStateReads == 3 && smi.RemainingReads == 3 && smi.SoiledReads == 3 && smi.CloggedReads == 3 && smi.ChoreReads == 3,
                "repeat performs bounded passive reads without hidden refresh");
            Passive(toilet, smi, 3, 7, 15);
        }
        finally
        {
            McpLocalization.LanguageProvider = provider;
            STRINGS.UI.USERMENUACTIONS.CLEANTOILET.NAME = title;
        }
    }

    private sealed class QuerySerializationProbe
    {
        public int GetterReads;
        public string Value
        {
            get { GetterReads++; return "stable managed probe"; }
        }
    }

    private static void NonToiletQueryMissIsSingleSerialization()
    {
        var foreign = new System.Collections.Generic.Dictionary<string, object>
        {
            ["actionKey"] = "other_action", ["title"] = "Other native title"
        };
        foreach (object rows in new object[]
        {
            new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>> { foreign },
            new[] { foreign },
            "unsupported rows",
            new JArray(new JObject { ["actionKey"] = "clean_toilet", ["title"] = "Native title" }),
            new[] { new System.Collections.Generic.Dictionary<string, object> { ["actionKey"] = "clean_toilet", ["title"] = 17 } },
            new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>> { null, foreign },
        })
        {
            var probe = new QuerySerializationProbe();
            var target = new System.Collections.Generic.Dictionary<string, object> { ["actions"] = rows, ["probe"] = probe };
            Check(!MaintenanceActionTools.MatchesToiletQueryForTest(target, "Request early toilet cleaning"), "noneligible row miss remains false");
            Check(probe.GetterReads == 1, "non-Toilet or unsupported row miss performs only original JSON serialization, not projection");
            Check(ReferenceEquals(target["actions"], rows), "cheap guard preserves source collection identity");
        }
        var noRowsProbe = new QuerySerializationProbe();
        var noRows = new System.Collections.Generic.Dictionary<string, object> { ["probe"] = noRowsProbe };
        Check(!MaintenanceActionTools.MatchesToiletQueryForTest(noRows, "Request early toilet cleaning") && noRowsProbe.GetterReads == 1,
            "missing actions skips additional projection");
        var emptyProbe = new QuerySerializationProbe();
        var emptyTarget = new System.Collections.Generic.Dictionary<string, object> { ["probe"] = emptyProbe };
        Check(MaintenanceActionTools.MatchesToiletQueryForTest(emptyTarget, " ") && emptyProbe.GetterReads == 0,
            "empty query keeps original pre-serialization short circuit");
    }

    private static void QueryCompatibility()
    {
        var toilet = Fixture(4); var smi = (Toilet.StatesInstance)toilet.Cache;
        var row = MaintenanceActionTools.ReadToiletActionForTest(toilet);
        var foreignRow = new System.Collections.Generic.Dictionary<string, object>
        {
            ["actionKey"] = "other_action", ["title"] = "Other native title", ["sentinel"] = 12
        };
        var actions = new System.Collections.Generic.List<System.Collections.Generic.Dictionary<string, object>> { row, foreignRow };
        var target = new System.Collections.Generic.Dictionary<string, object>
        {
            ["name"] = "Target metadata", ["actions"] = actions, ["unchanged"] = true
        };
        string before = JsonConvert.SerializeObject(target);
        string nativeTitle = (string)row["title"];
        foreach (string query in new[] { null, "", "  ", nativeTitle, "clean_toilet", "Other native title", "Target metadata", "\"title\":\"" + nativeTitle + "\"" })
            Check(MaintenanceActionTools.MatchesToiletQueryForTest(target, query), "normal metadata/native title query preserved " + query);
        foreach (string query in new[] { "Request early toilet cleaning", " request EARLY toilet CLEANING ", "early toilet", "\"title\":\"Request early toilet cleaning\"" })
            Check(MaintenanceActionTools.MatchesToiletQueryForTest(target, query), "legacy fixed title query alias preserved " + query);
        Check(!MaintenanceActionTools.MatchesToiletQueryForTest(target, "not present in either projection"), "unmatched query remains false");
        var arrayTarget = new System.Collections.Generic.Dictionary<string, object> { ["actions"] = new[] { row } };
        Check(MaintenanceActionTools.MatchesToiletQueryForTest(arrayTarget, "Request early toilet cleaning")
            && (string)row["title"] == nativeTitle, "eligible array rows retain legacy alias without changing native title");
        Check(JsonConvert.SerializeObject(target) == before && ReferenceEquals(target["actions"], actions)
            && (string)row["title"] == nativeTitle && actions[0] == row && actions[1] == foreignRow,
            "query projection does not mutate source dictionary, row order or native title");
        Check((string)foreignRow["title"] == "Other native title" && (int)foreignRow["sentinel"] == 12,
            "other action row is unchanged by toilet alias");
        Check(!target.ContainsKey("searchAliases") && !row.ContainsKey("searchAliases"), "compatibility alias is not added to response fields");
        Check(smi.CurrentStateReads == 1 && smi.RemainingReads == 1 && smi.SoiledReads == 1 && smi.CloggedReads == 1 && smi.ChoreReads == 1,
            "query matching never rereads native snapshot");
        Passive(toilet, smi, 1, 4, 15);
        var onlyForeign = new System.Collections.Generic.Dictionary<string, object>
        {
            ["actions"] = new[] { foreignRow }
        };
        Check(!MaintenanceActionTools.MatchesToiletQueryForTest(onlyForeign, "Request early toilet cleaning"), "legacy title alias is limited to canonical toilet rows");
        Check(MaintenanceActionTools.MatchesToiletQueryForTest(onlyForeign, "Other native title"), "other actions retain normal matching");
        var missingTitle = new System.Collections.Generic.Dictionary<string, object>
        {
            ["actions"] = new[] { new System.Collections.Generic.Dictionary<string, object> { ["actionKey"] = "clean_toilet" } }
        };
        Check(!MaintenanceActionTools.MatchesToiletQueryForTest(missingTitle, "Request early toilet cleaning"), "alias does not create a missing title");
        Check(MaintenanceActionTools.MatchesToiletQueryForTest(null, "null") && MaintenanceActionTools.MatchesToiletQueryForTest(null, " ")
            && !MaintenanceActionTools.MatchesToiletQueryForTest(null, "missing"), "null metadata retains existing serialized and empty query behavior");
    }

    internal static void Run()
    {
        checks = 0;
        var detailsBefore = DetailsScreen.Instance; var selectionBefore = SelectTool.Instance; var cluster = ClusterManager.Instance;
        var selectedTarget = new GameObject { Id = 91 };
        var selected = new KSelectable(); selectedTarget.Add(selected);
        var details = new DetailsScreen { target = selectedTarget };
        var selection = new SelectTool { selected = selected };
        DetailsScreen.Instance = details; SelectTool.Instance = selection;
        try
        {
            LegacyMissingOnly();
            MissingAndRepeat();
            CacheGuards();
            CacheExceptions();
            ObservedValuesAndPredicate();
            PartialGetterFaults();
            RepeatAndNativeTitle();
            QueryCompatibility();
            NonToiletQueryMissIsSingleSerialization();
            Check(ReferenceEquals(details, DetailsScreen.Instance) && ReferenceEquals(selection, SelectTool.Instance) && ReferenceEquals(cluster, ClusterManager.Instance),
                "read does not construct or replace native UI/selection owners");
            Check(details.Refreshes == 0 && selection.Selects == 0 && ReferenceEquals(details.target, selectedTarget) && ReferenceEquals(selection.selected, selected),
                "read does not select, refresh or change existing UI ownership");
            Console.WriteLine("PASS: actual toilet passive read assertions=" + checks + " (native boundary substituted)");
        }
        finally
        {
            DetailsScreen.Instance = detailsBefore; SelectTool.Instance = selectionBefore;
        }
    }
}
