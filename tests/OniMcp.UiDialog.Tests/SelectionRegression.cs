using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;
using OniMcp.Tools;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

internal static class SelectionRegression
{
    private const int Id = 73;
    private static int checks;
    private static MinionIdentity target;
    private static KSelectable selectable;
    private static SelectTool select;
    private static DetailsScreen details;
    private static SimpleInfoScreen simple;
    private static MinionVitalsPanel panel;
    private static MinionVitalsPanel.AmountLine line;
    private static ScrollRect scroll;
    private static EventSystem events;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static JObject Body(CallToolResult result)
    { Check(result != null && result.Content.Count == 1, "selection result is one structured block"); return JObject.Parse(result.Content[0].Text); }
    private static JObject Args(string action = "status")
    { return new JObject { ["domain"] = "ui", ["uiDomain"] = "selection", ["action"] = action, ["id"] = Id, ["confirm"] = true, ["task"] = "Observe the exact synthetic duplicant UI" }; }
    private static CallToolResult Call(JObject args) { return GameControlTools.ControlGame().Handler(args); }
    private static int Ref(UnityEngine.Object value) { return ReferenceEquals(value, null) ? 0 : value.GetInstanceID(); }
    private static string Snapshot()
    {
        return new JObject
        {
            ["selected"] = Ref(select.selected), ["details"] = Ref(details.target),
            ["simple"] = Ref(simple.FixtureSelected), ["vitals"] = Ref(panel.FixtureSelected),
            ["detailsActive"] = details.Active, ["detailsGoActive"] = details.gameObject.activeInHierarchy,
            ["detailsSpawned"] = details.isSpawned, ["detailsInitialized"] = details.FixtureInitialized,
            ["scroll"] = Ref(scroll), ["scrollContent"] = Ref(scroll?.content), ["scrollViewport"] = Ref(scroll?.viewport),
            ["events"] = Ref(EventSystem.current),
            ["label"] = line.locText.text, ["fixtureTooltip"] = line.toolTip == null ? null : line.toolTip.FixtureTooltip,
            ["pauseCount"] = SpeedControlScreen.Instance == null ? -1 : SpeedControlScreen.Instance.PauseCount,
            ["pauseCalls"] = SpeedControlScreen.Instance == null ? -1 : SpeedControlScreen.Instance.PauseCalls,
            ["unpauseCalls"] = SpeedControlScreen.Instance == null ? -1 : SpeedControlScreen.Instance.UnpauseCalls,
            ["frame"] = UnityEngine.Time.frameCount, ["time"] = GameClock.Instance.TimeValue
        }.ToString();
    }
    private static void NoActions(string before, string label)
    {
        Check(SelectionNativeCalls.Select == 0 && SelectionNativeCalls.Close == 0
            && SelectionNativeCalls.PointerEvents == 0 && SelectionNativeCalls.BaseScroll == 0 && SelectionNativeCalls.KScroll == 0,
            label + ": no selection, close, native wheel invocation or pointer event allocation");
        Check(SelectionNativeCalls.Refresh == 0 && SelectionNativeCalls.Format == 0 && SelectionNativeCalls.Focus == 0
            && SelectionNativeCalls.Activate == 0 && SelectionNativeCalls.DbGet == 0 && UnrelatedDispatchBoundary.Calls == 0,
            label + ": no refresh, formatter, database init, focus, tool activation or unrelated fallback");
        Check(before == Snapshot(), label + ": existing UI caches, native clock and pause remain untouched");
    }
    private static MinionIdentity Minion(int id)
    {
        var go = new GameObject();
        var value = new MinionIdentity { FixtureCell = 5, FixtureWorld = 0 };
        go.Add(value); go.Add(new KPrefabID { InstanceID = id }); go.Add(new KSelectable { FixtureWorld = 0 });
        return value;
    }
    private static void Accept(KSelectable value)
    {
        select.selected = value; details.target = value.gameObject;
        simple.FixtureTarget(value.gameObject); panel.FixtureTarget(value.gameObject);
        details.Active = true; details.gameObject.SetActive(true); simple.gameObject.SetActive(true);
        if (!KScreenManager.Instance.FixtureScreens().Contains(details)) KScreenManager.Instance.Add(details);
    }
    private static void NativeClose()
    {
        select.selected = null; details.target = null; simple.FixtureTarget(null); panel.FixtureTarget(null);
        details.Active = false; details.gameObject.SetActive(false); simple.gameObject.SetActive(false);
        KScreenManager.Instance.Remove(details);
    }
    private static void Setup(bool selected = true)
    {
        scroll = null; events = null; EventSystem.FixtureSetCurrent(null);
        PointerEventData.FixtureThrowOnConstruction = false;
        GameObject.FixtureParentEnumerations = 0; GameObject.FixtureOnParentEnumeration = null;
        Game.ThrowOnRead = false; Game.Instance = new Game(); App.IsExiting = App.isLoading = false;
        GameClock.Instance = new GameClock(); UnityEngine.Time.frameCount = 41; UnityEngine.Time.timeScale = 0;
        SpeedControlScreen.Instance = new SpeedControlScreen { PauseCount = 1 };
        Grid.CellCount = 16; Grid.Visible = Enumerable.Repeat(1, 16).ToArray(); Grid.WorldIdx = new int[16];
        Grid.Valid = Enumerable.Repeat(true, 16).ToArray(); Grid.WorldValid = Enumerable.Repeat(true, 16).ToArray();
        Grid.PositionReads = 0; Grid.FixtureOnPositionRead = null;
        Components.LiveMinionIdentities = new FixtureLiveMinions(); target = Minion(Id);
        Components.LiveMinionIdentities.Items.Add(target); selectable = target.GetComponent<KSelectable>();
        ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
        KScreenManager.Instance = new KScreenManager(); KScreenManager.Instance.DisableInput(false);
        ManagementMenu.Instance = new ManagementMenu();
        select = new SelectTool(); SelectTool.Instance = select;
        PlayerController.Instance = new PlayerController { ActiveTool = select };
        details = new DetailsScreen(); DetailsScreen.Instance = details;
        simple = new SimpleInfoScreen { Modal = false }; simple.transform.parent = details.transform; details.FixtureTab = simple;
        var panelGo = new GameObject(); panel = new MinionVitalsPanel(); panelGo.Add(panel);
        panel.transform.parent = simple.transform; simple.FixtureVitals(panel);
        var db = new Db(); db.Amounts.Breath = new Klei.AI.Amount { Id = "Breath" }; Db.FixtureSet(db);
        var lineGo = new GameObject(); lineGo.transform.parent = panel.transform;
        var label = new LocText { text = "<b>Native Breath</b>: 42%" }; var tooltip = new ToolTip { toolTip = "Native cached breath tooltip" };
        lineGo.Add(label); lineGo.Add(tooltip);
        line = new MinionVitalsPanel.AmountLine { amount = db.Amounts.Breath, go = lineGo, locText = label, toolTip = tooltip };
        panel.amountsLines.Add(line); select.FixtureNativeSelect = Accept; details.FixtureNativeClose = NativeClose;
        if (selected) Accept(selectable);
        else { details.Active = false; details.gameObject.SetActive(false); }
        SelectionNativeCalls.Reset(); UnrelatedDispatchBoundary.Calls = 0;
        McpLocalization.LanguageProvider = () => "en";
    }
    private static JObject Refused(JObject args, string reason, bool parseError = false)
    {
        string before = Snapshot(); var response = Call(args); var body = Body(response);
        Check(body["reasonCode"].Value<string>() == reason && !body["nativeInvoked"].Value<bool>()
            && !body["requested"].Value<bool>(), "exact refusal " + reason + ": " + body.ToString());
        if (parseError) Check(response.IsError, "invalid request is a structured MCP error");
        NoActions(before, reason); return body;
    }
    private static void ReadAndRoutes()
    {
        Setup(); string before = Snapshot();
        var routeProbe = Call(Args()); JObject routed = null;
        try { routed = JObject.Parse(routeProbe.Content[0].Text); } catch (Newtonsoft.Json.JsonException) { }
        Check(GameControlTools.ControlGame().Parameters["uiDomain"].EnumValues.Contains("selection")
            && UiControlTools.ControlUi().Parameters["domain"].EnumValues.Contains("selection")
            && routed != null && routed["source"]?.Value<string>() == "existing_native_duplicant_selection_ui"
            && routed["id"]?.Value<int>() == Id, "compiled_missing_selection_route");
        Check(GameControlTools.ControlGame().Parameters["id"].Type == "integer"
            && UiControlTools.ControlUi().Parameters["id"].Type == "integer", "actual aggregate schemas expose typed exact ID");
        var args = Args(); string input = args.ToString(); var result = Body(Call(args));
        Check(args.ToString() == input && result["available"].Value<bool>() && result["status"].Value<string>() == "observed", "public route reads exact native cache without mutating caller");
        Check(result["breath"]["label"].Value<string>() == line.locText.text
            && result["breath"]["storedTooltip"] == null
            && result["breath"]["source"].Value<string>() == "owned_MinionVitalsPanel.amountsLines.LocText.text"
            && !result["breath"]["pixelVisibilityVerified"].Value<bool>() && result["breath"]["value"] == null,
            "read returns only exact existing LocText, never tooltip or fabricated numeric/pixel proof");
        var direct = Args(); direct["domain"] = "selection"; direct.Remove("uiDomain");
        Check(JToken.DeepEquals(result, Body(UiControlTools.ControlUi().Handler(direct))), "both actual routing layers reach the same product read");
        NoActions(before, "readonly exact UI route");
        KScreenManager.Instance.Add(new KScreen { Modal = false });
        Check(Body(Call(Args()))["available"].Value<bool>(), "nonmodal screen is not invented as a blocking modal");
        var tooltipProperty = typeof(ToolTip).GetProperty("toolTip");
        Check(tooltipProperty != null && tooltipProperty.CanWrite && !tooltipProperty.CanRead,
            "fixture preserves actual write-only native ToolTip API");
        line.toolTip.toolTip = "Private fixture tooltip sentinel";
        before = Snapshot(); var textOnly = Body(Call(Args()));
        Check(textOnly["available"].Value<bool>() && textOnly["breath"]["label"].Value<string>() == line.locText.text
            && textOnly["breath"]["storedTooltip"] == null, "native tooltip input is never returned as existing text");
        NoActions(before, "readonly LocText with unrelated tooltip input");
        line.toolTip = null; panel.amountsLines[0] = line;
        before = Snapshot(); textOnly = Body(Call(Args()));
        Check(textOnly["available"].Value<bool>() && textOnly["breath"]["label"].Value<string>() == line.locText.text
            && textOnly["breath"]["storedTooltip"] == null, "owned LocText read does not require a ToolTip object");
        NoActions(before, "readonly LocText without a tooltip object");
    }
    private static void InitialHiddenDetails()
    {
        Setup(false); details.isSpawned = false;
        float initialTime = GameClock.Instance.TimeValue;
        string before = Snapshot(); var first = Body(Call(Args()));
        Check(!first["available"].Value<bool>() && !first["nativeInvoked"].Value<bool>() && first["breath"] == null,
            "initial hidden read exposes no normal breath value and invokes no UI");
        NoActions(before, "initial hidden initialized Details status");
        var previewArgs = Args("select"); previewArgs["dryRun"] = true;
        var preview = Body(Call(previewArgs));
        Check(preview["status"]?.Value<string>() == "preview" && preview["wouldInvoke"]?.Value<bool>() == true
            && preview["nativeInvoked"]?.Value<bool>() == false && !details.isSpawned,
            "compiled_initial_hidden_details_preflight_rejected");
        NoActions(before, "initial hidden initialized Details select preview");
        Check(first["reasonCode"]?.Value<string>() == "target_not_selected"
            && first["detailsScreenSpawned"]?.Value<bool>() == false
            && first["detailsScreenInitialized"]?.Value<bool>() == true
            && first["detailsScreenActive"]?.Value<bool>() == false,
            "initial status reports actual initialized but unspawned hidden Details before the target-not-selected return");
        var selected = Body(Call(Args("select")));
        Check(selected["nativeInvoked"].Value<bool>() && selected["requested"].Value<bool>()
            && selected["selectionAccepted"].Value<bool>() && selected["partial"].Value<bool>()
            && selected["status"].Value<string>() == "pending" && !selected["available"].Value<bool>()
            && selected["reasonCode"].Value<string>() == "details_not_displayed"
            && selected["detailsScreenSpawned"].Value<bool>() == false
            && selected["detailsScreenInitialized"].Value<bool>() && !details.isSpawned,
            "one normal native select can accept the initial target while native spawn is still pending");
        Check(SelectionNativeCalls.Select == 1 && SelectionNativeCalls.Close == 0
            && ReferenceEquals(select.LastArgument, selectable), "initial hidden select invokes the exact native target once");
        Check(SelectionNativeCalls.Refresh == 0 && SelectionNativeCalls.Format == 0 && SelectionNativeCalls.Focus == 0
            && SelectionNativeCalls.Activate == 0 && SelectionNativeCalls.DbGet == 0,
            "initial hidden selection never forces refresh, format, focus, tool activation or database initialization");
        Check(SpeedControlScreen.Instance.PauseCount == 1 && SpeedControlScreen.Instance.PauseCalls == 0
            && SpeedControlScreen.Instance.UnpauseCalls == 0 && UnityEngine.Time.frameCount == 41
            && GameClock.Instance.TimeValue == initialTime, "initial selection preserves native fixture clock and pause");
        SelectionNativeCalls.Reset(); before = Snapshot(); var pending = Body(Call(Args()));
        Check(pending["status"].Value<string>() == "pending" && !pending["available"].Value<bool>()
            && pending["reasonCode"].Value<string>() == "details_not_displayed" && pending["breath"] == null
            && !pending["detailsScreenSpawned"].Value<bool>() && pending["detailsScreenInitialized"].Value<bool>(),
            "read cannot report text as observed before native Details spawn");
        NoActions(before, "initial Details awaiting native spawn");
        foreach (bool dryRun in new[] { false, true })
        {
            var close = Args("close"); close["dryRun"] = dryRun;
            Refused(close, "details_not_displayed");
        }
        // Fixture event: native Start/OnSpawn completes later, outside the adapter.
        details.isSpawned = true; before = Snapshot(); var observed = Body(Call(Args()));
        Check(observed["available"].Value<bool>() && observed["status"].Value<string>() == "observed"
            && observed["detailsScreenSpawned"].Value<bool>() && observed["detailsScreenInitialized"].Value<bool>()
            && observed["breath"]["label"].Value<string>() == line.locText.text,
            "later status observes the existing native LocText after fixture spawn without reselecting");
        NoActions(before, "post-spawn passive Details status");
        Setup(); details.FixtureInitialized = false;
        Refused(Args(), "details_screen_not_initialized");
    }
    private static void Parsing()
    {
        foreach (JToken bad in new JToken[] { JValue.CreateNull(), "73", true, 73.5, 0, (long)int.MaxValue + 1, (long)int.MinValue - 1, new JArray(73), new JObject() })
        { Setup(); var args = Args("select"); args["id"] = bad; Refused(args, "invalid_id", true); }
        Setup(); var missing = Args("select"); missing.Remove("id"); Refused(missing, "invalid_id", true);
        foreach (string flag in new[] { "confirm", "dryRun" }) foreach (JToken bad in new JToken[] { JValue.CreateNull(), "true", 1, new JObject(), new JArray(true) })
        { Setup(); var args = Args("select"); args[flag] = bad; Refused(args, "invalid_boolean", true); }
        foreach (string selector in new[] { "name", "query", "target", "search", "x", "y", "x1", "y1", "x2", "y2", "worldId", "allWorlds", "areaId", "force" })
        { Setup(); var args = Args("select"); args[selector] = "foreign"; Refused(args, "unsupported_selector", true); }
        foreach (string action in new[] { "select", "close" })
        {
            Setup(); var args = Args(action); args.Remove("task"); Refused(args, "task_required", true);
            foreach (JToken bad in new JToken[] { JValue.CreateNull(), " ", true, 1 })
            { Setup(); args = Args(action); args["task"] = bad; Refused(args, "task_required", true); }
            Setup(); args = Args(action); args["confirm"] = false; Refused(args, "confirm_required", true);
            Setup(); args = Args(action); args.Remove("confirm"); Refused(args, "confirm_required", true);
            Setup(); args = Args(action); args["dryRun"] = true; args["confirm"] = false;
            string before = Snapshot(); var preview = Body(Call(args));
            Check(preview["status"].Value<string>() == "preview" && preview["wouldInvoke"].Value<bool>()
                && !preview["nativeInvoked"].Value<bool>() && !preview["requested"].Value<bool>(), "validated dryRun never pretends actual UI acceptance");
            NoActions(before, "dryRun " + action);
        }
        Setup(); Refused(Args("anything_else"), "unsupported_action", true);
        Setup(); target.GetComponent<KPrefabID>().InstanceID = int.MinValue; var signed = Args("select"); signed["id"] = int.MinValue;
        Check(Body(Call(signed))["selectionAccepted"].Value<bool>() && SelectionNativeCalls.Select == 1, "canonical signed Int32 is matched by actual KPrefabID, not coerced string/name");
    }
    private static void Guards()
    {
        var cases = new List<Tuple<string, Action, string>>
        {
            Tuple.Create("no_game", (Action)(() => Game.Instance = null), "game_unavailable"),
            Tuple.Create("loading", (Action)(() => Game.Instance.Loading = true), "game_unavailable"),
            Tuple.Create("app_loading", (Action)(() => App.isLoading = true), "game_unavailable"),
            Tuple.Create("exiting", (Action)(() => App.IsExiting = true), "game_unavailable"),
            Tuple.Create("missing_cache", (Action)(() => Components.LiveMinionIdentities.Items = null), "target_cache_unavailable"),
            Tuple.Create("wrong_id", (Action)(() => target.GetComponent<KPrefabID>().InstanceID = Id + 1), "target_not_found"),
            Tuple.Create("duplicate_id", (Action)(() => Components.LiveMinionIdentities.Items.Add(Minion(Id))), "target_ambiguous"),
            Tuple.Create("unspawned", (Action)(() => target.isSpawned = false), "target_not_selectable"),
            Tuple.Create("inactive_target", (Action)(() => target.gameObject.SetActive(false)), "target_not_selectable"),
            Tuple.Create("selectable_false", (Action)(() => selectable.IsSelectable = false), "target_not_selectable"),
            Tuple.Create("detached_selectable", (Action)(() => selectable.gameObject = new GameObject()), "target_not_selectable"),
            Tuple.Create("hidden", (Action)(() => Grid.Visible[5] = 0), "target_not_visible"),
            Tuple.Create("invalid_cell", (Action)(() => Grid.Valid[5] = false), "target_not_visible"),
            Tuple.Create("invalid_world_cell", (Action)(() => Grid.WorldValid[5] = false), "target_not_visible"),
            Tuple.Create("foreign_world", (Action)(() => target.FixtureWorld = 1), "target_wrong_world"),
            Tuple.Create("foreign_selectable", (Action)(() => selectable.FixtureWorld = 1), "target_wrong_world"),
            Tuple.Create("foreign_grid", (Action)(() => Grid.WorldIdx[5] = 1), "target_wrong_world"),
            Tuple.Create("missing_select", (Action)(() => SelectTool.Instance = null), "ui_unavailable"),
            Tuple.Create("unspawned_select", (Action)(() => select.isSpawned = false), "selection_tool_not_spawned"),
            Tuple.Create("inactive_select", (Action)(() => select.isActiveAndEnabled = false), "selection_tool_inactive"),
            Tuple.Create("missing_details", (Action)(() => DetailsScreen.Instance = null), "ui_unavailable"),
            Tuple.Create("missing_controller", (Action)(() => PlayerController.Instance = null), "ui_unavailable"),
            Tuple.Create("uninitialized_details", (Action)(() => details.FixtureInitialized = false), "details_screen_not_initialized"),
            Tuple.Create("missing_manager", (Action)(() => KScreenManager.Instance = null), "ui_unavailable"),
            Tuple.Create("unpaused", (Action)(() => SpeedControlScreen.Instance.PauseCount = 0), "ui_busy"),
            Tuple.Create("missing_pause", (Action)(() => SpeedControlScreen.Instance = null), "ui_busy"),
            Tuple.Create("tool_busy", (Action)(() => PlayerController.Instance.ActiveTool = new object()), "ui_busy"),
            Tuple.Create("dragging", (Action)(() => PlayerController.Instance.Dragging = true), "ui_busy"),
            Tuple.Create("input_disabled", (Action)(() => KScreenManager.Instance.DisableInput(true)), "ui_busy"),
            Tuple.Create("modal", (Action)(() => KScreenManager.Instance.Add(new KScreen())), "ui_busy"),
            Tuple.Create("management", (Action)(() => ManagementMenu.Instance.FixtureSetScreen(new ManagementMenu.ScreenData())), "ui_busy"),
            Tuple.Create("wrong_tab", (Action)(() => details.FixtureTab = new KScreen()), "simple_info_tab_unavailable"),
            Tuple.Create("detached_tab", (Action)(() => simple.transform.parent = new GameObject().transform), "simple_info_tab_unavailable")
        };
        foreach (var test in cases) foreach (bool dryRun in new[] { false, true })
        {
            Setup(); test.Item2(); var args = Args("select"); args["dryRun"] = dryRun;
            Refused(args, test.Item3);
        }
        foreach (var test in cases.Where(value => value.Item1 == "hidden" || value.Item1 == "foreign_world" || value.Item1 == "modal"))
        {
            Setup(); test.Item2(); var unreadable = Refused(Args(), test.Item3);
            Check(unreadable["breath"] == null, "readonly guard cannot expose a cached normal breath value: " + test.Item1);
        }
    }
    private static void CacheChain()
    {
        var cases = new List<Tuple<Action, string>>
        {
            Tuple.Create((Action)(() => select.selected = null), "target_not_selected"),
            Tuple.Create((Action)(() => details.target = new GameObject()), "details_target_pending"),
            Tuple.Create((Action)(() => details.isSpawned = false), "details_not_displayed"),
            Tuple.Create((Action)(() => details.Active = false), "details_not_displayed"),
            Tuple.Create((Action)(() => KScreenManager.Instance.Remove(details)), "details_not_displayed"),
            Tuple.Create((Action)(() => simple.FixtureTarget(new GameObject())), "simple_info_target_pending"),
            Tuple.Create((Action)(() => panel.FixtureTarget(new GameObject())), "vitals_target_pending"),
            Tuple.Create((Action)(() => simple.FixtureVitals(null)), "vitals_panel_unavailable"),
            Tuple.Create((Action)(() => panel.isActiveAndEnabled = false), "vitals_panel_inactive"),
            Tuple.Create((Action)(() => Db.FixtureSet(null)), "breath_definition_unavailable"),
            Tuple.Create((Action)(() => Db.FixtureExisting.Amounts.Breath = new Klei.AI.Amount { Id = "Breath" }), "breath_line_missing"),
            Tuple.Create((Action)(() => panel.amountsLines.Clear()), "breath_line_missing"),
            Tuple.Create((Action)(() => panel.amountsLines.Add(line)), "breath_line_ambiguous"),
            Tuple.Create((Action)(() => line.go.SetActive(false)), "breath_line_unavailable"),
            Tuple.Create((Action)(() => line.locText.isActiveAndEnabled = false), "breath_line_unavailable"),
            Tuple.Create((Action)(() => line.locText.text = string.Empty), "breath_text_pending")
        };
        foreach (var test in cases)
        { Setup(); test.Item1(); Refused(Args(), test.Item2); }
    }
    private static void InvokeAndPartial()
    {
        Setup(false); var result = Body(Call(Args("select")));
        Check(result["available"].Value<bool>() && result["selectionAccepted"].Value<bool>() && !result["partial"].Value<bool>()
            && SelectionNativeCalls.Select == 1 && SelectionNativeCalls.Close == 0 && ReferenceEquals(select.LastArgument, selectable), "one exact native Select is independently observed complete");
        Check(SelectionNativeCalls.Refresh == 0 && SelectionNativeCalls.Format == 0 && SelectionNativeCalls.Focus == 0, "selection does not manually refresh/format/focus");
        Setup(false); select.FixtureNativeSelect = value => { };
        result = Body(Call(Args("select")));
        Check(result["nativeInvoked"].Value<bool>() && result["partial"].Value<bool>() && !result["selectionAccepted"].Value<bool>()
            && result["reasonCode"].Value<string>() == "selection_not_accepted" && SelectionNativeCalls.Select == 1, "ignored native selection is partial, never retried");
        Setup(false); select.FixtureNativeSelect = value => { Accept(value); panel.FixtureTarget(new GameObject()); };
        result = Body(Call(Args("select")));
        Check(result["status"].Value<string>() == "pending" && result["partial"].Value<bool>() && result["selectionAccepted"].Value<bool>()
            && !result["available"].Value<bool>() && SelectionNativeCalls.Select == 1, "native acceptance remains separate from delayed vitals target binding");
        Setup(); result = Body(Call(Args("close")));
        Check(result["closed"].Value<bool>() && !result["partial"].Value<bool>() && SelectionNativeCalls.Close == 1
            && SelectionNativeCalls.Select == 0 && details.FixtureCloseButton.SignalCalls == 0, "close invokes only the owned real DeselectAndClose callback once");
        SelectionNativeCalls.Reset(); string closedState = Snapshot(); var closedRead = Body(Call(Args()));
        Check(closedRead["selectionEmpty"].Value<bool>() && closedRead["detailsTargetEmpty"].Value<bool>()
            && !closedRead["detailsScreenActive"].Value<bool>() && !closedRead["available"].Value<bool>()
            && closedRead["reasonCode"].Value<string>() == "target_not_selected", "later readonly status proves empty selection and closed Details before its early return");
        NoActions(closedState, "post-close readonly observation");
        Setup(); details.FixtureNativeClose = () => { };
        result = Body(Call(Args("close")));
        Check(!result["closed"].Value<bool>() && result["partial"].Value<bool>() && result["status"].Value<string>() == "pending"
            && SelectionNativeCalls.Close == 1 && SelectionNativeCalls.Select == 0, "uncompleted native close remains pending without fallback");
        foreach (string action in new[] { "select", "close" })
        {
            Setup();
            if (action == "select") select.FixtureNativeSelect = value => { Accept(value); throw new InvalidOperationException("native fixture failure"); };
            else details.FixtureNativeClose = () => { NativeClose(); throw new InvalidOperationException("native fixture failure"); };
            result = Body(Call(Args(action)));
            Check(result["nativeInvoked"].Value<bool>() && result["partial"].Value<bool>() && !result["available"].Value<bool>()
                && result["reasonCode"].Value<string>() == "native_invocation_failed", "native exception preserves attempted action and unknown completion");
            Check(SelectionNativeCalls.Select + SelectionNativeCalls.Close == 1 && SelectionNativeCalls.Refresh == 0, "exception has no second native action or refresh");
        }
    }
    private static void CloseGuards()
    {
        var cases = new List<Tuple<Action, string>>
        {
            Tuple.Create((Action)(() => details.isSpawned = false), "details_not_displayed"),
            Tuple.Create((Action)(() => details.FixtureInitialized = false), "details_screen_not_initialized"),
            Tuple.Create((Action)(() => details.Active = false), "details_not_displayed"),
            Tuple.Create((Action)(() => details.isActiveAndEnabled = false), "details_not_displayed"),
            Tuple.Create((Action)(() => KScreenManager.Instance.Remove(details)), "details_not_displayed"),
            Tuple.Create((Action)(() => details.target = new GameObject()), "close_target_mismatch"),
            Tuple.Create((Action)(() => simple.FixtureTarget(new GameObject())), "simple_info_target_pending"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.isSpawned = false), "close_button_unavailable"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.isInteractable = false), "close_button_unavailable"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.transform.parent = new GameObject().transform), "close_button_unavailable"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.ClearOnClick()), "close_callback_mismatch"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.onClick += () => { throw new Exception("foreign callback"); }), "close_callback_mismatch"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.onClick += details.DeselectAndClose), "close_callback_mismatch"),
            Tuple.Create((Action)(() => details.FixtureCloseButton.onBtnClick += key => { throw new Exception("foreign key callback"); }), "close_callback_mismatch"),
            Tuple.Create((Action)(() => { details.FixtureCloseButton.ClearOnClick(); var foreign = new DetailsScreen(); details.FixtureCloseButton.onClick += foreign.DeselectAndClose; }), "close_callback_mismatch")
        };
        foreach (var test in cases) foreach (bool dryRun in new[] { false, true })
        { Setup(); test.Item1(); var args = Args("close"); args["dryRun"] = dryRun; Refused(args, test.Item2); }
        Setup(); Grid.FixtureOnPositionRead = count =>
        {
            if (count != 2) return;
            var go = new GameObject(); var button = new KButton(); go.Add(button); button.transform.parent = details.transform;
            button.onClick += details.DeselectAndClose; details.FixtureReplaceButton(button);
        };
        Refused(Args("close"), "target_changed");
    }
    private static void StaleOwners()
    {
        foreach (string action in new[] { "select", "close" })
        {
            Setup(); Grid.FixtureOnPositionRead = count => { if (count == 1) Game.Instance = new Game(); };
            Refused(Args(action), "target_changed");
            foreach (string owner in new[] { "game", "controller", "manager", "cluster", "world", "cell", "select" })
            {
                Setup(); Action change = () =>
                {
                    if (owner == "game") Game.Instance = new Game();
                    else if (owner == "controller") PlayerController.Instance = new PlayerController { ActiveTool = select };
                    else if (owner == "manager") KScreenManager.Instance = new KScreenManager();
                    else if (owner == "cluster") ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
                    else if (owner == "world") { ClusterManager.Instance.activeWorldId = 1; }
                    else if (owner == "cell") target.FixtureCell = 6;
                    else { var replacement = new SelectTool(); SelectTool.Instance = replacement; PlayerController.Instance.ActiveTool = replacement; }
                };
                if (action == "select") select.FixtureNativeSelect = value => { Accept(value); change(); };
                else details.FixtureNativeClose = () => { NativeClose(); change(); };
                var result = Body(Call(Args(action)));
                Check(result["nativeInvoked"].Value<bool>() && result["partial"].Value<bool>() && !result["available"].Value<bool>()
                    && result["reasonCode"].Value<string>() == "postcondition_unavailable", "changed post-native owner is not fabricated as completed: " + action + "/" + owner);
                Check(SelectionNativeCalls.Select + SelectionNativeCalls.Close == 1 && SelectionNativeCalls.Focus == 0
                    && SelectionNativeCalls.Refresh == 0, "stale postcondition never retries or bypasses native UI");
            }
        }
    }
    private static void Localization()
    {
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            Setup(); McpLocalization.LanguageProvider = () => language; var args = Args("select"); args["confirm"] = false;
            var result = Body(Call(args));
            Check(result["reasonMessage"].Value<string>() == McpLocalization.GetForLanguage(language, "ui_selection.invalid_request")
                && !result["reasonMessage"].Value<string>().StartsWith("ui_selection.", StringComparison.Ordinal), "actual embedded selection message resolves " + language);
        }
        McpLocalization.LanguageProvider = () => "en";
    }
    private static GameObject RectObject()
    { var value = new GameObject(); value.FixtureUseRectTransform(); return value; }
    private static EventSystem NewEvents()
    { var go = new GameObject(); var value = new EventSystem(); go.Add(value); return value; }
    private static void SetupScroll(bool plain = false, bool unknown = false)
    {
        Setup(); var root = RectObject(); root.transform.parent = details.transform;
        if (unknown) scroll = new FixtureUnknownScrollRect();
        else scroll = plain ? new ScrollRect() : new KScrollRect();
        root.Add(scroll);
        var viewport = RectObject(); viewport.transform.parent = root.transform;
        var content = RectObject(); content.transform.parent = viewport.transform;
        scroll.viewport = (RectTransform)viewport.transform; scroll.content = (RectTransform)content.transform;
        simple.transform.parent = content.transform;
        events = NewEvents(); EventSystem.FixtureSetCurrent(events);
    }
    private static JObject Wheel(string action, bool plain = false)
    {
        var value = Body(Call(Args(action))); float expected = action == "scroll_down" ? -1f : 1f;
        Check(value["nativeInvoked"]?.Value<bool>() == true && value["requested"]?.Value<bool>() == true
            && value["status"]?.Value<string>() == "pending" && value["partial"]?.Value<bool>() == true
            && value["available"]?.Value<bool>() == false && value["reasonCode"]?.Value<string>() == "scroll_render_pending",
            "compiled_native_scroll_action_missing");
        Check(value["executionPath"].Value<string>() == "ScrollRect.OnScroll" && value["wheelDeltaY"].Value<float>() == expected
            && SelectionNativeCalls.PointerEvents == 1 && SelectionNativeCalls.Select == 0 && SelectionNativeCalls.Close == 0,
            "one native wheel request has exact direction, allocation and no selection/close fallback");
        Check(SelectionNativeCalls.BaseScroll == (plain ? 1 : 0) && SelectionNativeCalls.KScroll == (plain ? 0 : 1)
            && scroll.FixtureLastEvent.scrollDelta.x == 0 && scroll.FixtureLastEvent.scrollDelta.y == expected
            && ReferenceEquals(scroll.FixtureLastEvent.FixtureEvents, events),
            "native virtual dispatch reaches the exact base/override and live event owner once");
        Check(value["pixelVisibilityVerified"] == null && value["completed"] == null && value["breath"] == null
            && SelectionNativeCalls.Refresh == 0 && SelectionNativeCalls.Format == 0 && SelectionNativeCalls.Focus == 0
            && SelectionNativeCalls.Activate == 0 && SelectionNativeCalls.DbGet == 0,
            "pending wheel does not invent pixels, completion, refreshed text or alternate UI actions");
        return value;
    }
    private static void ScrollRegression()
    {
        foreach (bool plain in new[] { false, true }) foreach (string action in new[] { "scroll_down", "scroll_up" })
        {
            SetupScroll(plain); Wheel(action, plain);
            SelectionNativeCalls.Reset(); string before = Snapshot(); var read = Body(Call(Args()));
            Check(read["available"].Value<bool>() && !read["breath"]["pixelVisibilityVerified"].Value<bool>(),
                "later explicit status reads its old cache and still does not verify clipped pixels");
            NoActions(before, "passive status after native wheel");
            SetupScroll(plain); before = Snapshot(); var args = Args(action); args["dryRun"] = true; args["confirm"] = false;
            var preview = Body(Call(args));
            Check(preview["status"].Value<string>() == "preview" && preview["wouldInvoke"].Value<bool>()
                && preview["wheelDeltaY"].Value<float>() == (action == "scroll_down" ? -1 : 1), "wheel preview validates its exact signed intent");
            NoActions(before, "wheel preview " + action);
        }
        SetupScroll(); ((KScrollRect)scroll).scrollIsHorizontalOnly = true; Wheel("scroll_down");
        SetupScroll(); scroll.viewport = null; Wheel("scroll_up");
        foreach (string action in new[] { "scroll_down", "scroll_up" })
        {
            foreach (JToken bad in new JToken[] { JValue.CreateNull(), "73", 0, true, 73.5, (long)int.MaxValue + 1 })
            { SetupScroll(); var args = Args(action); args["id"] = bad; Refused(args, "invalid_id", true); }
            foreach (string selector in new[] { "query", "worldId", "x", "force" })
            { SetupScroll(); var args = Args(action); args[selector] = 1; Refused(args, "unsupported_selector", true); }
            foreach (string parameter in new[] { "task", "confirm" })
            { SetupScroll(); var args = Args(action); args.Remove(parameter); Refused(args, parameter + "_required", true); }
            SetupScroll(); var denied = Args(action); denied["task"] = " "; Refused(denied, "task_required", true);
            SetupScroll(); denied = Args(action); denied["confirm"] = false; Refused(denied, "confirm_required", true);
            foreach (string flag in new[] { "dryRun", "confirm" })
            { SetupScroll(); var args = Args(action); args[flag] = "true"; Refused(args, "invalid_boolean", true); }
        }
        var refusal = new List<Tuple<Action, string>>
        {
            Tuple.Create((Action)(() => select.selected = null), "target_not_selected"),
            Tuple.Create((Action)(() => details.isSpawned = false), "details_not_displayed"),
            Tuple.Create((Action)(() => details.target = new GameObject()), "details_target_pending"),
            Tuple.Create((Action)(() => Grid.Visible[5] = 0), "target_not_visible"),
            Tuple.Create((Action)(() => target.FixtureWorld = 1), "target_wrong_world"),
            Tuple.Create((Action)(() => PlayerController.Instance.ActiveTool = new object()), "ui_busy"),
            Tuple.Create((Action)(() => KScreenManager.Instance.Add(new KScreen())), "ui_busy"),
            Tuple.Create((Action)(() => simple.transform.parent = details.transform), "scroll_region_unavailable"),
            Tuple.Create((Action)(() => details.gameObject.Add(new ScrollRect())), "scroll_region_ambiguous"),
            Tuple.Create((Action)(() => scroll.vertical = false), "scroll_region_unavailable"),
            Tuple.Create((Action)(() => scroll.horizontal = true), "scroll_region_unavailable"),
            Tuple.Create((Action)(() => scroll.isActiveAndEnabled = false), "scroll_region_unavailable"),
            Tuple.Create((Action)(() => scroll.FixtureActive = false), "scroll_region_unavailable"),
            Tuple.Create((Action)(() => scroll.content = null), "scroll_owner_mismatch"),
            Tuple.Create((Action)(() => scroll.content = scroll.viewport), "scroll_owner_mismatch"),
            Tuple.Create((Action)(() => scroll.viewport = (RectTransform)RectObject().transform), "scroll_owner_mismatch"),
            Tuple.Create((Action)(() => ((KScrollRect)scroll).allowVerticalScrollWheel = false), "scroll_region_busy"),
            Tuple.Create((Action)(() => ((KScrollRect)scroll).isDragging = true), "scroll_region_busy"),
            Tuple.Create((Action)(() => EventSystem.FixtureSetCurrent(null)), "scroll_input_unavailable"),
            Tuple.Create((Action)(() => events.isActiveAndEnabled = false), "scroll_input_unavailable"),
            Tuple.Create((Action)(() => events.gameObject.SetActive(false)), "scroll_input_unavailable")
        };
        foreach (var test in refusal) foreach (bool dryRun in new[] { false, true })
        { SetupScroll(); test.Item1(); var args = Args("scroll_down"); args["dryRun"] = dryRun; Refused(args, test.Item2); }
        SetupScroll(unknown: true); Refused(Args("scroll_up"), "scroll_region_unavailable");
        SetupScroll(); GameObject.FixtureOnParentEnumeration = count => { if (count == 2) EventSystem.FixtureSetCurrent(NewEvents()); };
        var stale = Body(Call(Args("scroll_down")));
        Check(stale["reasonCode"].Value<string>() == "scroll_target_changed" && !stale["nativeInvoked"].Value<bool>()
            && SelectionNativeCalls.PointerEvents == 0 && SelectionNativeCalls.KScroll == 0, "pre-invocation event owner change is rejected before allocation");
        SetupScroll(); scroll.FixtureNativeScroll = data => EventSystem.FixtureSetCurrent(NewEvents());
        var partial = Body(Call(Args("scroll_up")));
        Check(partial["nativeInvoked"].Value<bool>() && partial["partial"].Value<bool>()
            && partial["reasonCode"].Value<string>() == "postcondition_unavailable" && SelectionNativeCalls.KScroll == 1
            && SelectionNativeCalls.PointerEvents == 1, "post-native owner change is partial with no repeat or fallback");
        SetupScroll(); scroll.FixtureNativeScroll = data => { throw new InvalidOperationException("fixture native wheel failure"); };
        partial = Body(Call(Args("scroll_up")));
        Check(partial["reasonCode"].Value<string>() == "native_invocation_failed" && partial["nativeInvoked"].Value<bool>()
            && partial["partial"].Value<bool>() && SelectionNativeCalls.KScroll == 1 && SelectionNativeCalls.PointerEvents == 1,
            "native wheel exception remains an attempted partial operation without retry");
        SetupScroll(); PointerEventData.FixtureThrowOnConstruction = true;
        var unavailable = Body(Call(Args("scroll_up")));
        Check(unavailable["reasonCode"].Value<string>() == "scroll_event_unavailable" && !unavailable["nativeInvoked"].Value<bool>()
            && SelectionNativeCalls.PointerEvents == 1 && SelectionNativeCalls.KScroll == 0, "pointer construction failure invokes no native wheel");
    }
    internal static void Run()
    {
        ReadAndRoutes(); InitialHiddenDetails(); Parsing(); Guards(); CacheChain(); InvokeAndPartial(); CloseGuards(); StaleOwners(); Localization(); ScrollRegression();
        Console.WriteLine("Duplicant selection UI: " + checks + " host checks passed (actual complete adapter/handler/routers; native UI boundary substituted)");
    }
}
