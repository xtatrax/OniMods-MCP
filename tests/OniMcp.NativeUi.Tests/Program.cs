using System;
using OniMcp.Tools;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static GameObject target;
    private static ToggleFixture toggle;
    private static PlayerControlledToggleSideScreen screen;
    private static DetailsScreen details;

    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--toilet-read-only") { ToiletReadRegression.Run(); return; }
        if (args.Length == 1 && args[0] == "--toilet-legacy-missing-only") { ToiletReadRegression.LegacyMissingOnly(); return; }
        if (args.Length == 1 && args[0] == "--completion-no-direct-write") { BlueprintParentRegression.VerifyNoDirectFallbackOnly(); return; }
        if (args.Length == 1 && args[0] == "--placement-guidance-only") { PlacementGuidanceRegression.Run(); return; }
        if (args.Length == 1 && args[0] == "--liquid-foreground-only") { LiquidBridgeForegroundRegression.Run(); return; }
        if (args.Length == 1 && args[0] == "--liquid-foreground-positive-only") { LiquidBridgeForegroundRegression.PositiveOnly(); return; }
        if (args.Length == 1 && args[0] == "--tile-only") { TileReplacementRegression.Run(); return; }
        if (args.Length == 1 && args[0] == "--preview-id-only") { PreviewIdRegression.Run(); return; }
        if (args.Length == 1 && args[0] == "--background-wall-only") { BackgroundWallRegression.Run(); return; }
        ToiletReadRegression.Run();
        BackgroundWallRegression.Run();
        PreviewIdRegression.Run();
        TestTargetGuards();
        TestUiReadiness();
        TestNativeButton();
        TestFirstUseInitialization();
        TestFailureObservation();
        BlueprintCompletionRegression.Run();
        TileReplacementRegression.Run();
        LiquidBridgeForegroundRegression.Run();
        PlacementGuidanceRegression.Run();
        Console.WriteLine("Native UI adapter: " + checks + " host checks passed (Unity/game boundary substituted)");
    }

    private static void Setup()
    {
        target = new GameObject { Cell = Grid.XYToCell(2, 3) };
        var selectable = new KSelectable(); target.Add(selectable);
        toggle = new ToggleFixture { Selectable = selectable }; target.Add(toggle);
        target.Add(new KPrefabID { InstanceID = 42 });
        for (int i = 0; i < Grid.Visible.Length; i++) { Grid.Visible[i] = 1; Grid.WorldIdx[i] = 0; }
        screen = new PlayerControlledToggleSideScreen();
        details = new DetailsScreen { Screens = new[] { screen } };
        details.OnRefresh = go => { details.target = go; screen.target = go.GetComponent<IPlayerControlledToggle>(); };
        DetailsScreen.Instance = details;
        SelectTool.Instance = new SelectTool();
        ClusterManager.Instance = new ClusterManager { activeWorldId = 0 };
    }

    private static string Guard(int? id = 42, int? x = 2, int? y = 3, int? world = 0, string query = null)
    { return NativePlayerToggleUi.ValidateTarget(target, id, x, y, world, query, (go, q) => q == "match"); }

    private static void TestTargetGuards()
    {
        Setup();
        Check(NativePlayerToggleUi.ValidateSelectors(null, null, null) != null, "query-only or absent selector rejected");
        Check(NativePlayerToggleUi.ValidateSelectors(42, 2, null) != null, "partial coordinates rejected even with id");
        Check(NativePlayerToggleUi.ValidateSelectors(42, -1, 3) != null, "negative coordinates rejected before XY aliasing");
        Check(NativePlayerToggleUi.ValidateSelectors(null, 8, 0) != null, "outside column rejected before XY aliasing");
        Check(NativePlayerToggleUi.ValidateSelectors(null, 0, 8) != null, "outside row rejected");
        Check(Guard() == null, "matching id coordinates and world accepted");
        Check(Guard(42, null, null) == null && Guard(null) == null, "explicit id or cell accepted");
        Check(Guard(99) != null, "cell cannot override explicit id");
        Check(Guard(42, 3, 3) != null, "id cannot override explicit cell");
        Check(Guard(world: 1) != null, "wrong world rejected");
        Check(Guard(query: "other") != null && Guard(query: "match") == null, "additional query remains binding");
        Grid.Visible[target.Cell] = 0;
        Check(Guard() != null, "hidden target rejected");
        Grid.Visible[target.Cell] = 1; Grid.WorldIdx[target.Cell] = -1;
        Check(Guard() != null, "invalid world cell rejected");
        target.Cell = -1;
        Check(Guard() != null, "invalid cell rejected");
        Check(NativePlayerToggleUi.ValidateTarget(null, 42, null, null, null, null, null) != null, "missing resolved target rejected");
    }

    private static void TestUiReadiness()
    {
        Setup(); DetailsScreen.Instance = null;
        RejectClick("missing native details UI");
        Setup(); SelectTool.Instance = null;
        RejectClick("missing native selection tool");
        Setup(); toggle.Selectable = null;
        RejectClick("missing native selectable");
        Setup(); toggle.Selectable = new KSelectable { gameObject = new GameObject() };
        RejectClick("wrong selectable target");
        Setup(); toggle.Selectable.IsSelectable = false;
        RejectClick("native unselectable target");
        Setup(); toggle.Selectable.WorldId = 1;
        RejectClick("non-active world target");
        Check(SelectTool.Instance.Selects == 0 && details.Refreshes == 0,
            "wrong world preflight must not change selection or details");
        Setup(); ClusterManager.Instance = null;
        RejectClick("missing native world context");
        Setup(); SelectTool.Instance.AcceptSelection = false;
        RejectClick("native selection rejected target");
        Check(details.Refreshes == 0, "rejected native selection must not be bypassed by details refresh");
        Setup(); details.Screens = new PlayerControlledToggleSideScreen[0];
        RejectClick("missing initialized screen");
        Setup(); details.OnRefresh = go => details.target = go;
        RejectClick("screen bound to a different toggle");
        Setup(); details.Screens = new[] { screen, screen };
        RejectClick("ambiguous initialized screens");
        Setup(); screen.Valid = false;
        RejectClick("native target validity rejected");
        Setup(); screen.isActiveAndEnabled = false;
        RejectClick("inactive native screen");
        Setup(); screen.toggleButton = null;
        RejectClick("missing native button");
        Setup(); screen.toggleButton.isActiveAndEnabled = false;
        RejectClick("inactive native button");
        Setup(); screen.toggleButton.isInteractable = false;
        RejectClick("disabled native button");
        Setup(); details.OnRefresh = go => { throw new InvalidOperationException("native refresh failed"); };
        RejectClick("native refresh failure");
        Setup(); details.OnRefresh = go => { screen.target = toggle; };
        RejectClick("native details target not accepted");
    }

    private static void TestNativeButton()
    {
        Setup(); int attempted = 0, nativeCallbacks = 0;
        screen.toggleButton.OnClick = () => nativeCallbacks++;
        bool changedUi = NativePlayerToggleUi.Click(target, toggle, () => attempted++);
        Check(changedUi && details.Refreshes == 1 && SelectTool.Instance.Selects == 1,
            "native selection and details refresh own UI target binding");
        Check(SelectTool.Instance.selected == toggle.Selectable && details.target == target,
            "exact selectable and details targets established");
        Check(attempted == 1 && nativeCallbacks == 1 && screen.toggleButton.Clicks == 1,
            "actual native button is invoked exactly once");
        Check(!NativePlayerToggleUi.Click(target, toggle, null), "same selection does not claim UI retargeting");
        Setup(); DetailsScreen.Instance = null;
        bool clicked = NativeBuildingToggle.SetPlayerToggle(true, false, true, true,
            () => NativePlayerToggleUi.Click(target, toggle, null));
        Check(!clicked && screen.toggleButton.Clicks == 0, "idempotent no-op needs no UI or click");
    }

    private static void TestFirstUseInitialization()
    {
        Setup(); screen.isSpawned = false;
        Check(screen.isActiveAndEnabled && screen.toggleButton.isActiveAndEnabled,
            "first-use fixture is active before Unity OnSpawn");
        int attempted = 0;
        bool initializing = false;
        try { NativePlayerToggleUi.Click(target, toggle, () => attempted++); }
        catch (NativePlayerToggleUi.InitializationPendingException) { initializing = true; }
        Check(initializing && attempted == 0 && screen.toggleButton.Clicks == 0,
            "unspawned active screen returns initializing before button attempt");
        Check(details.target == target && SelectTool.Instance.selected == toggle.Selectable,
            "initializing response can still follow accepted native UI selection");
        screen.isSpawned = true;
        int acceptedCallbacks = 0;
        screen.toggleButton.OnClick = () => acceptedCallbacks++;
        NativePlayerToggleUi.Click(target, toggle, () => attempted++);
        Check(attempted == 1 && acceptedCallbacks == 1 && screen.toggleButton.Clicks == 1,
            "normal Unity initialization permits a later native callback");

        Setup(); screen.toggleButton.isSpawned = false;
        initializing = false;
        try { NativePlayerToggleUi.Click(target, toggle, null); }
        catch (NativePlayerToggleUi.InitializationPendingException) { initializing = true; }
        Check(initializing && screen.toggleButton.Clicks == 0, "unspawned active button is not ready");

        Setup(); screen.toggleButton.OnClick = null;
        bool on = true, pending = false;
        NativePlayerToggleUi.Click(target, toggle, null);
        bool rejected = false;
        try { NativeBuildingToggle.RequirePlayerToggleAccepted(false, true, on, pending); }
        catch (InvalidOperationException) { rejected = true; }
        var observed = NativeBuildingToggle.ReadPlayerToggleObservation(false, () => on, () => pending);
        Check(rejected && screen.toggleButton.Clicks == 1 && !(bool)observed["completed"]
            && (bool)observed["on"] && !(bool)observed["toggleRequested"],
            "missing callback cannot be accepted solely because SignalClick returned");
    }

    private static void TestFailureObservation()
    {
        Setup(); bool on = false, pending = false; int attempted = 0;
        var expected = new InvalidOperationException("native callback failed after acceptance");
        screen.toggleButton.OnClick = () => { on = true; throw expected; };
        Exception observed = null;
        try { NativePlayerToggleUi.Click(target, toggle, () => attempted++); }
        catch (Exception ex) { observed = ex; }
        Check(ReferenceEquals(observed, expected) && attempted == 1 && screen.toggleButton.Clicks == 1,
            "exception after native mutation is preserved with attempted invocation");
        var state = NativeBuildingToggle.ReadPlayerToggleObservation(true, () => on, () => pending);
        Check((bool)state["on"] && !(bool)state["toggleRequested"] && (bool)state["completed"],
            "post-exception observation reports actual accepted completion");
        state = NativeBuildingToggle.ReadPlayerToggleObservation(true, () => false, () => true);
        Check(!(bool)state["on"] && (bool)state["requestedOn"] && !(bool)state["completed"],
            "queued request is observed but not falsely completed");
        state = NativeBuildingToggle.ReadPlayerToggleObservation(true,
            () => { throw new Exception("native read unavailable"); }, () => true);
        Check(state["on"] == null && (bool)state["toggleRequested"] && state["completed"] == null
            && state["requestedOn"] == null, "unreadable native state remains unknown with partial evidence retained");
        state = NativeBuildingToggle.ReadPlayerToggleObservation(true, () => false,
            () => { throw new Exception("native queue read unavailable"); });
        Check(!(bool)state["on"] && state["toggleRequested"] == null && state["completed"] == null,
            "unreadable queue does not fabricate completion");
    }

    private static void RejectClick(string reason)
    {
        bool rejected = false; int attempted = 0;
        try { NativePlayerToggleUi.Click(target, toggle, () => attempted++); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && attempted == 0 && (screen.toggleButton == null || screen.toggleButton.Clicks == 0),
            reason + " must fail before native button invocation");
    }

    private static void Check(bool ok, string reason)
    {
        checks++;
        if (!ok) throw new Exception(reason);
    }
}
