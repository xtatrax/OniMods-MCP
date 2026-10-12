using System;
using System.Collections.Generic;
using System.Reflection;

namespace OniMcp.Tools
{
    // Use the native menu handler: it owns queuedToggle, priority refs and Toggle chores.
    // Never fall back to assigning IsEnabled if the game's private API changes.
    internal static class NativeBuildingToggle
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static Dictionary<string, object> Read(object button)
        {
            var type = button.GetType();
            var enabled = type.GetProperty("IsEnabled", Members);
            var toggleField = type.GetField("Toggleable", Members);
            var indexField = type.GetField("ToggleIdx", Members);
            if (enabled == null || toggleField == null || indexField == null)
                throw new InvalidOperationException("Native building toggle metadata unavailable; refusing direct state mutation");
            var toggle = toggleField.GetValue(button);
            int index = (int)indexField.GetValue(button);
            var queuedMethod = toggle?.GetType().GetMethod("IsToggleQueued", Members, null, new[] { typeof(int) }, null);
            if (queuedMethod == null || index < 0)
                throw new InvalidOperationException("Native building toggle queue unavailable");
            bool current = (bool)enabled.GetValue(button, null);
            bool queued = (bool)queuedMethod.Invoke(toggle, new object[] { index });
            return new Dictionary<string, object>
            {
                ["enabled"] = current, ["toggleQueued"] = queued,
                ["requestedEnabled"] = queued ? !current : current,
                ["requiresDuplicantWork"] = true,
                ["executionPath"] = "BuildingEnabledButton.OnMenuToggle"
            };
        }

        internal static Dictionary<string, object> Set(object button, bool desired, bool instantBuild)
        {
            if (instantBuild)
                throw new InvalidOperationException("Normal building toggle refuses InstantBuildMode; use the explicit sandbox debug route");
            var before = Read(button);
            bool changed = (bool)before["requestedEnabled"] != desired;
            if (changed)
            {
                var menu = button.GetType().GetMethod("OnMenuToggle", Members, null, Type.EmptyTypes, null);
                if (menu == null)
                    throw new InvalidOperationException("Native menu toggle unavailable; refusing direct state mutation");
                menu.Invoke(button, null);
            }
            var result = Read(button);
            result["beforeEnabled"] = before["enabled"];
            result["beforeToggleQueued"] = before["toggleQueued"];
            result["changed"] = changed;
            result["requestedEnabled"] = desired;
            result["completed"] = (bool)result["enabled"] == desired && !(bool)result["toggleQueued"];
            return result;
        }

        internal static void SetPlayerToggle(bool current, bool queued, bool desired, bool paused,
            System.Action<bool> setRequested, System.Action toggleByPlayer)
        {
            if (paused)
            {
                // Native ClickToggle defers these dupe-free switches to their Sim33ms.
                bool pending = current != desired;
                if (queued != pending) setRequested(pending);
                return;
            }
            if (queued) setRequested(false);
            if (current != desired) toggleByPlayer();
        }
    }
}
