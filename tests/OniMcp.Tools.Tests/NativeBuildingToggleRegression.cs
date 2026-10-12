using System;
using OniMcp.Tools;

internal static class NativeBuildingToggleRegression
{
    public static void Run()
    {
        foreach (bool initial in new[] { false, true })
        {
            var button = new Button(initial);
            var result = NativeBuildingToggle.Set(button, !initial, false);
            Check(button.IsEnabled == initial && button.Pending && button.Clicks == 1, "must queue without immediate state change");
            Check(!(bool)result["completed"], "queued is not completed");
            NativeBuildingToggle.Set(button, !initial, false);
            Check(button.Pending && button.Clicks == 1, "retry must keep existing request");
            NativeBuildingToggle.Set(button, initial, false);
            Check(!button.Pending && button.Clicks == 2 && button.IsEnabled == initial, "opposite request cancels through menu");
            NativeBuildingToggle.Set(button, !initial, false);
            button.CompleteWork();
            result = NativeBuildingToggle.Set(button, !initial, false);
            Check((bool)result["completed"] && button.Clicks == 3, "completed state is idempotent");
        }
        var guarded = new Button(true);
        bool rejected = false;
        try { NativeBuildingToggle.Set(guarded, false, true); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected && guarded.Clicks == 0, "InstantBuild must not bypass dupe work on normal route");
        rejected = false;
        try { NativeBuildingToggle.Set(new object(), false, false); }
        catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "missing API fails closed");
        foreach (bool current in new[] { false, true })
        foreach (bool queued in new[] { false, true })
        foreach (bool desired in new[] { false, true })
        foreach (bool paused in new[] { false, true })
        {
            bool state = current, pending = queued; int toggles = 0;
            NativeBuildingToggle.SetPlayerToggle(current, queued, desired, paused,
                v => pending = v, () => { state = !state; toggles++; });
            Check(paused ? state == current && pending == (current != desired) && toggles == 0
                : state == desired && !pending && toggles == (current == desired ? 0 : 1),
                "dupe-free native switch must preserve pause request semantics");
        }
        Console.WriteLine("Native building toggle regressions passed");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Queue
    {
        public bool Pending;
        public bool IsToggleQueued(int index) { if (index != 3) throw new Exception("wrong handler"); return Pending; }
    }
    private sealed class Button
    {
        private readonly Queue Toggleable = new Queue();
        private readonly int ToggleIdx = 3;
        private bool enabled;
        public int Clicks;
        public bool IsEnabled { get { return enabled; } set { throw new Exception("direct setter must not be used"); } }
        public bool Pending { get { return Toggleable.IsToggleQueued(ToggleIdx); } }
        public Button(bool value) { enabled = value; }
        private void OnMenuToggle() { Clicks++; Toggleable.Pending = !Toggleable.Pending; }
        public void CompleteWork() { if (Pending) { enabled = !enabled; Toggleable.Pending = false; } }
    }
}
