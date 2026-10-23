using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
        KnownMismatchIsExcludedBeforeLimit();
        BothModelsAndMasteredHistory();
        UnknownInputsDoNotBecomeEligible();
        CatalogAndStrictTargets();
        NativeMasteryAndUiAreSeparate();
        DryRunsAndNativeActions();
        NativeUiIdentityGuards();
        NativeActionFailureAndOpenPreviews();
        NativeInputAndModalGuards();
        Console.WriteLine("Production-linked skill eligibility checks passed: " + checks);
    }

    private static void KnownMismatchIsExcludedBeforeLimit()
    {
        var f = Fixture("Minion");
        Add(f, "A_foreign", "BionicMinion", "A");
        Add(f, "B_ordinary", "Minion", "B");
        Add(f, "A_deprecated", "Minion", "A"); f.Database.Skills.resources.Last().deprecated = true;
        f.Database.Skills.resources.Insert(0, null);
        var body = List(f, new JObject { ["limit"] = 1 });
        Check((int)body["returned"] == 1 && (string)body["skills"][0]["id"] == "B_ordinary",
            "Known incompatible candidates must be excluded before the native list limit; legacy producer exposes the wrong model.");
        Check((string)body["canMasterSemantics"] == "native_mastery_conditions", "canMaster declares its native condition meaning");
        Check((bool)body["skills"][0]["modelMatch"] && (bool)body["skills"][0]["canMaster"], "model and native mastery are separate observed facts");
        Check(Db.GetCalls == 0 && f.Menu.OpenCalls == 0 && f.Menu.IsOpenReads == 0, "default read uses existing caches without Db.Get or UI access");
        NoMutations(f, "list");
        body = List(f, new JObject { ["query"] = "A_foreign", ["limit"] = 1 });
        Check((int)body["returned"] == 0 && !body["skills"].Any(), "query cannot reintroduce an excluded model");
        body = List(f, new JObject { ["query"] = "ordinary", ["limit"] = 0 });
        Check((int)body["returned"] == 1, "existing minimum limit and query matching remain effective");
    }

    private static void BothModelsAndMasteredHistory()
    {
        foreach (string model in new[] { "Minion", "BionicMinion" })
        {
            var f = Fixture(model);
            string other = model == "Minion" ? "BionicMinion" : "Minion";
            Add(f, "A_current", model, "Group");
            Add(f, "B_other", other, "Group");
            Add(f, "C_history", other, "Group");
            f.Resume.Mastered.Add("C_history");
            f.Resume.MasteryBySkillID["C_history"] = true;
            var body = List(f);
            Check(Ids(body).SequenceEqual(new[] { "A_current", "C_history" }), "both models filter the other model but retain mastered history");
            var history = Find(body, "C_history");
            Check((bool)history["mastered"] && !(bool)history["modelMatch"], "history preserves mastered and observed mismatch");
            Check((string)history["candidateScope"] == "mastered_history_model_mismatch", "historical mismatch is not presented as a new model candidate");
            NoMutations(f, "both-model read");
        }
        var alias = Fixture("minion"); Add(alias, "Alias", "Minion", "Group");
        Check((bool)Find(List(alias), "Alias")["modelMatch"], "model comparison respects the supplied native Tag hash identity");
        var custom = Fixture("ThirdPartyModel");
        Add(custom, "External", "ThirdPartyModel", "ExternalGroup");
        Check((bool)Find(List(custom), "External")["modelMatch"], "valid native model identifiers are not limited to a mod-owned whitelist");
    }

    private static void UnknownInputsDoNotBecomeEligible()
    {
        var f = Fixture("Minion"); Add(f, "UnknownRequiredModel", null, "Group");
        var row = Find(List(f), "UnknownRequiredModel");
        Check(row.Property("modelMatch") != null && row["modelMatch"].Type == JTokenType.Null, "missing required model is explicitly null in actual JSON");
        Check((string)row["candidateScope"] == "model_unknown", "missing required model is not assumed compatible");
        f.Resume.Conditions["UnknownRequiredModel"] = null;
        row = Find(List(f), "UnknownRequiredModel");
        Check(row.Property("canMaster") != null && row["canMaster"].Type == JTokenType.Null, "missing native mastery conditions is JSON null, not true");
        f.Resume.ThrowConditions = true;
        row = Find(List(f), "UnknownRequiredModel");
        Check(row["canMaster"].Type == JTokenType.Null, "native mastery getter failure remains unknown");
        NoMutations(f, "unknown skill read");

        f = Fixture("Minion"); Add(f, "ForeignWithUnknownMastery", "BionicMinion", "Group"); f.Resume.ThrowMastered = true;
        row = Find(List(f), "ForeignWithUnknownMastery");
        Check(row["mastered"].Type == JTokenType.Null && (string)row["candidateScope"] == "mastery_unknown_model_mismatch",
            "unknown mastery cannot be treated as false to discard possibly historical native data");

        foreach (string missing in new[] { "resume", "mastery", "personality", "model", "database", "catalog" })
        {
            f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group");
            if (missing == "resume") f.Dupe.Resume = null;
            if (missing == "mastery") f.Resume.MasteryBySkillID = null;
            if (missing == "personality") f.Database.Personalities.resources.Clear();
            if (missing == "model") f.Database.Personalities.resources[0].model = null;
            if (missing == "database") Db.SetFixture(null);
            if (missing == "catalog") f.Database.Skills.resources = null;
            var result = DuplicantTools.ListSkills().Handler(new JObject { ["id"] = f.Dupe.Id });
            var body = Body(result);
            Check(result.IsError && body["skills"].Type == JTokenType.Null && body["returned"].Type == JTokenType.Null,
                missing + " must fail unavailable rather than return a successful empty/global list");
            Check(Db.GetCalls == 0, "unavailable native cache cannot initialize Db");
            NoMutations(f, missing);
        }
    }

    private static void CatalogAndStrictTargets()
    {
        var f = Fixture("Minion");
        Add(f, "A_bionic", "BionicMinion", "A"); Add(f, "B_normal", "Minion", "B");
        var catalog = Body(DuplicantTools.ListSkills().Handler(new JObject()));
        Check(!catalog["skills"].Any(s => s["mastered"] != null && s["mastered"].Type != JTokenType.Null), "catalog does not invent a target mastery opinion");
        Check(Ids(catalog).SequenceEqual(new[] { "A_bionic", "B_normal" }), "unscoped catalog retains all native model definitions");
        Check((string)Find(catalog, "A_bionic")["requiredDuplicantModel"] == "BionicMinion", "catalog returns raw required model");
        foreach (var args in new[] {
            new JObject { ["id"] = 999, ["name"] = f.Dupe.Name },
            new JObject { ["id"] = f.Dupe.Id, ["name"] = "Other" },
            new JObject { ["id"] = "not-an-id", ["name"] = f.Dupe.Name },
            new JObject { ["name"] = "" }, new JObject { ["name"] = "NoSuchDupe" } })
        {
            var result = DuplicantTools.ListSkills().Handler(args);
            Check(result.IsError && Body(result)["skills"].Type == JTokenType.Null, "explicit unresolved or inconsistent target cannot fall back to catalog/name");
        }
        Components.LiveMinionIdentities.Items.Add(new MinionIdentity { Id = 22, Name = f.Dupe.Name });
        Check(DuplicantTools.ListSkills().Handler(new JObject { ["name"] = f.Dupe.Name }).IsError, "duplicate names are ambiguous");
        NoMutations(f, "strict target reads");
    }

    private static void NativeMasteryAndUiAreSeparate()
    {
        var f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group");
        var widget = Widget(f, "Ordinary");
        widget.gameObject.activeSelf = false;
        var row = Find(List(f, new JObject { ["includeUi"] = true }), "Ordinary");
        Check((bool)row["canMaster"] && !(bool)row["ui"]["visible"], "native conditions true is distinct from cached UI visibility false");
        Check(f.Menu.OpenCalls == 0 && widget.ClickCalls == 0, "includeUi observes an existing screen without opening or clicking");
        f.Screen.CurrentlySelectedMinion = new MinionIdentity { Id = 99, Name = "Other" };
        var body = List(f, new JObject { ["includeUi"] = true });
        Check((string)body["ui"]["reasonCode"] == "ui_target_mismatch" && Find(body, "Ordinary")["ui"]["visible"].Type == JTokenType.Null,
            "another selected target produces unknown UI fields rather than a false result for this target");
        NoMutations(f, "cached UI reads");
    }

    private static void DryRunsAndNativeActions()
    {
        foreach (bool force in new[] { false, true })
        {
            var f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group"); var widget = Widget(f, "Ordinary");
            var result = DuplicantTools.LearnSkill().Handler(new JObject { ["id"] = f.Dupe.Id, ["skillId"] = "Ordinary", ["confirm"] = true, ["dryRun"] = true, ["force"] = force });
            Check(!result.IsError && !(bool)Body(result)["changed"], "dryRun is a nonchanging response, including force");
            Check(Body(result)["ui"] != null && Body(result)["skill"]["ui"] != null && f.Menu.IsOpenReads == 1,
                "dryRun observes one shared cached UI context without opening or refreshing it");
            NoMutations(f, "dryRun");
            Check(f.Menu.OpenCalls == 0 && widget.ClickCalls == 0 && !f.Resume.Mastered.Any(), "dryRun cannot open, click, grant or master");
        }
        var normal = Fixture("Minion"); Add(normal, "Ordinary", "Minion", "Group"); var w = Widget(normal, "Ordinary");
        w.OnClick = () => { normal.Resume.Mastered.Add("Ordinary"); normal.Resume.MasteryBySkillID["Ordinary"] = true; };
        var learned = Learn(normal, "Ordinary");
        Check(!learned.IsError && (bool)Body(learned)["changed"], "ordinary result is determined from post-click mastery observation");
        Check(normal.Menu.OpenCalls == 1 && w.ClickCalls == 1 && normal.Resume.DirectMasterCalls == 0 && normal.Resume.GrantCalls == 0,
            "ordinary learning invokes one native UI route with no direct mutation fallback");
        Check(w.LastEvent != null && w.LastEvent.System == UnityEngine.EventSystems.EventSystem.current && w.LastEvent.button == UnityEngine.EventSystems.PointerEventData.InputButton.Left,
            "native click receives the current event system and a nonnull Left event");
        var unchanged = Fixture("Minion"); Add(unchanged, "Ordinary", "Minion", "Group"); var idle = Widget(unchanged, "Ordinary");
        unchanged.Resume.NativeOpinions[unchanged.Resume.Conditions["Ordinary"]] = false;
        var resultIdle = Learn(unchanged, "Ordinary");
        Check(!(bool)Body(resultIdle)["changed"] && idle.ClickCalls == 1 && unchanged.Resume.DirectMasterCalls == 0,
            "accepted click without mastery change is not claimed as completed and is not retried directly");
        var forced = Fixture("Minion"); Add(forced, "Ordinary", "Minion", "Group");
        var granted = DuplicantTools.LearnSkill().Handler(new JObject { ["id"] = forced.Dupe.Id, ["skillId"] = "Ordinary", ["confirm"] = true, ["force"] = true });
        Check(!granted.IsError && forced.Resume.GrantCalls == 1 && forced.Resume.DirectMasterCalls == 0 && forced.Menu.OpenCalls == 0,
            "explicit legacy force grant remains distinct from normal UI learning");
    }

    private static void NativeUiIdentityGuards()
    {
        foreach (string broken in new[] { "model", "menu", "event", "event_inactive", "instant", "instant_unknown", "screen_closed", "screen_inactive", "target", "widget_missing", "widget_inactive", "parent", "widget_id", "after_open_instant" })
        {
            var f = Fixture("Minion"); Add(f, "Ordinary", broken == "model" ? "BionicMinion" : "Minion", "Group"); var widget = Widget(f, "Ordinary");
            if (broken == "model") widget.gameObject.activeSelf = false;
            if (broken == "menu") ManagementMenu.Instance = null;
            if (broken == "event") UnityEngine.EventSystems.EventSystem.current = null;
            if (broken == "event_inactive") UnityEngine.EventSystems.EventSystem.current.isActiveAndEnabled = false;
            if (broken == "instant") DebugHandler.InstantValue = true;
            if (broken == "instant_unknown") DebugHandler.ThrowInstant = true;
            if (broken == "screen_closed") f.Menu.Open = false;
            if (broken == "screen_inactive") f.Screen.gameObject.activeInHierarchy = false;
            if (broken == "target") f.Screen.CurrentlySelectedMinion = new MinionIdentity { Id = 33, Name = "Other" };
            if (broken == "widget_missing") f.Screen.SetWidgetsMissing();
            if (broken == "widget_inactive") widget.gameObject.activeSelf = false;
            if (broken == "parent") widget.gameObject.transform.Parent = new UnityEngine.Transform();
            if (broken == "widget_id") widget.skillID = "DifferentSkill";
            if (broken == "after_open_instant") f.Menu.OnOpen = _ => DebugHandler.InstantValue = true;
            var result = Learn(f, "Ordinary");
            Check(result.IsError, broken + " must refuse ordinary native click");
            Check(widget.ClickCalls == 0 && f.Resume.DirectMasterCalls == 0 && f.Resume.GrantCalls == 0 && !f.Resume.Mastered.Any(),
                broken + " cannot click or fall back to direct native mutation");
        }
    }

    private static void NativeActionFailureAndOpenPreviews()
    {
        var f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group"); var w = Widget(f, "Ordinary");
        var args = new JObject { ["action"] = "open", ["id"] = f.Dupe.Id, ["dryRun"] = true };
        var preview = DuplicantTools.ControlSkill().Handler(args);
        Check(!preview.IsError && !(bool)Body(preview)["uiInvoked"] && f.Menu.OpenCalls == 0, "open preview does not invoke the native menu");
        args["dryRun"] = false; args["confirm"] = true;
        var opened = DuplicantTools.ControlSkill().Handler(args);
        Check(!opened.IsError && f.Menu.OpenCalls == 1 && w.ClickCalls == 0, "explicit open delegates once and never clicks a skill");
        NoMutations(f, "open");

        f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group"); w = Widget(f, "Ordinary");
        f.Menu.OnOpen = _ => { throw new InvalidOperationException("native open failed fixture"); };
        var failed = Learn(f, "Ordinary"); var body = Body(failed);
        Check(failed.IsError && (bool)body["partial"] && (bool)body["uiInvoked"] && !(bool)body["nativeClickInvoked"],
            "a throwing native menu records its invocation/partial UI side effects without claiming mastery change");
        Check(w.ClickCalls == 0 && f.Resume.DirectMasterCalls == 0, "native open error cannot fall back directly");
        f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group"); w = Widget(f, "Ordinary");
        w.OnClick = () => { throw new InvalidOperationException("native click failed fixture"); };
        failed = Learn(f, "Ordinary"); body = Body(failed);
        Check(failed.IsError && (bool)body["partial"] && (bool)body["nativeClickInvoked"] && w.ClickCalls == 1,
            "a throwing native click is reported as attempted, not retried or completed");
        Check(f.Resume.DirectMasterCalls == 0 && f.Resume.GrantCalls == 0, "native click error cannot use a direct fallback");
    }

    private static void NativeInputAndModalGuards()
    {
        foreach (string broken in new[] { "input_disabled", "manager_missing", "stack_missing", "own_screen_absent", "active_modal", "modal_state_unknown", "busy_after_open" })
        {
            var f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group"); var widget = Widget(f, "Ordinary");
            if (broken == "input_disabled") KScreenManager.SetFixtureInputDisabled(true);
            if (broken == "manager_missing") KScreenManager.Instance = null;
            if (broken == "stack_missing") KScreenManager.Instance.SetStackMissing();
            if (broken == "own_screen_absent") KScreenManager.Instance = new KScreenManager();
            if (broken == "active_modal") KScreenManager.Instance.FixtureStack().Add(new KScreen { Active = true, Modal = true });
            if (broken == "modal_state_unknown") KScreenManager.Instance.FixtureStack().Add(new KScreen { ThrowActive = true, Modal = true });
            if (broken == "busy_after_open") f.Menu.OnOpen = _ => KScreenManager.SetFixtureInputDisabled(true);
            var result = Learn(f, "Ordinary");
            Check(result.IsError && widget.ClickCalls == 0 && f.Resume.DirectMasterCalls == 0 && f.Resume.GrantCalls == 0,
                broken + " native input state blocks click without direct fallback");
        }
        foreach (string control in new[] { "modal_below_own", "inactive_modal_above", "nonmodal_above", "proxy_target" })
        {
            var f = Fixture("Minion"); Add(f, "Ordinary", "Minion", "Group"); var widget = Widget(f, "Ordinary");
            if (control == "modal_below_own") KScreenManager.Instance = new KScreenManager(new KScreen { Active = true, Modal = true }, f.Screen);
            if (control == "inactive_modal_above") KScreenManager.Instance.FixtureStack().Add(new KScreen { Active = false, Modal = true });
            if (control == "nonmodal_above") KScreenManager.Instance.FixtureStack().Add(new KScreen { Active = true, Modal = false });
            if (control == "proxy_target") f.Screen.CurrentlySelectedMinion = new MinionAssignablesProxy { Target = f.Dupe.gameObject };
            Learn(f, "Ordinary");
            Check(widget.ClickCalls == 1 && f.Resume.DirectMasterCalls == 0, control + " does not falsely block the native owned skill target");
        }
    }

    private sealed class State
    {
        internal Db Database;
        internal MinionIdentity Dupe;
        internal MinionResume Resume;
        internal SkillsScreen Screen;
        internal ManagementMenu Menu;
    }
    private static State Fixture(string model)
    {
        var f = new State { Database = new Db(), Resume = new MinionResume(), Screen = new SkillsScreen() };
        f.Dupe = new MinionIdentity { Id = 17, Name = "Fixture", personalityResourceId = "PersonalityFixture", Resume = f.Resume };
        f.Dupe.gameObject.Components[typeof(MinionIdentity)] = f.Dupe;
        f.Database.Personalities.resources.Add(new Personality { Id = f.Dupe.personalityResourceId, model = model });
        Components.LiveMinionIdentities = new MinionCollection(); Components.LiveMinionIdentities.Items.Add(f.Dupe);
        Db.SetFixture(f.Database); Db.GetCalls = 0;
        f.Screen.CurrentlySelectedMinion = f.Dupe;
        f.Menu = new ManagementMenu(f.Screen); ManagementMenu.Instance = f.Menu;
        UnityEngine.EventSystems.EventSystem.current = new UnityEngine.EventSystems.EventSystem();
        DebugHandler.InstantValue = false; DebugHandler.ThrowInstant = false; DebugHandler.InstantReads = 0;
        KScreenManager.SetFixtureInputDisabled(false); KScreenManager.Instance = new KScreenManager(f.Screen);
        return f;
    }
    private static void Add(State f, string id, string model, string group)
    {
        f.Database.Skills.resources.Add(new Database.Skill { Id = id, Name = id, description = "fixture", skillGroup = group, requiredDuplicantModel = model });
        var conditions = new MinionResume.SkillMasteryConditions[0];
        f.Resume.Conditions[id] = conditions; f.Resume.NativeOpinions[conditions] = true;
    }
    private static SkillWidget Widget(State f, string id) { var w = new SkillWidget(f.Screen, id); f.Screen.Add(id, w); return w; }
    private static JObject List(State f, JObject extra = null)
    {
        var args = extra == null ? new JObject() : (JObject)extra.DeepClone(); args["id"] = f.Dupe.Id;
        var result = DuplicantTools.ListSkills().Handler(args); Check(!result.IsError, "available skill fixture returns a successful list"); return Body(result);
    }
    private static CallToolResult Learn(State f, string id) { return DuplicantTools.LearnSkill().Handler(new JObject { ["id"] = f.Dupe.Id, ["skillId"] = id, ["confirm"] = true }); }
    private static JObject Body(CallToolResult result) { return JObject.Parse(result.Content.Single().Text); }
    private static string[] Ids(JObject body) { return body["skills"].Select(s => (string)s["id"]).ToArray(); }
    private static JObject Find(JObject body, string id) { return body["skills"].OfType<JObject>().Single(s => (string)s["id"] == id); }
    private static void NoMutations(State f, string context) { Check(f.Resume.DirectMasterCalls == 0 && f.Resume.GrantCalls == 0, context + " preserves native skill state"); }
    private static void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
}
