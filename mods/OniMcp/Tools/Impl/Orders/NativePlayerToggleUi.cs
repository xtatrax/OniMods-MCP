using System;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    internal static class NativePlayerToggleUi
    {
        internal sealed class InitializationPendingException : InvalidOperationException
        {
            internal InitializationPendingException()
                : base("Native player-toggle UI is still initializing; read config state and retry after a normal Unity frame") { }
        }

        internal static string ValidateSelectors(int? id, int? x, int? y)
        {
            if (x.HasValue != y.HasValue)
                return "Both x and y are required when selecting a native toggle by coordinates";
            if (!id.HasValue && !x.HasValue)
                return "Select a native toggle with explicit id or x/y; read config state to identify an unambiguous target";
            if (x.HasValue && (x.Value < 0 || x.Value >= Grid.WidthInCells
                || y.Value < 0 || y.Value >= Grid.HeightInCells))
                return "Native toggle coordinates are outside the grid";
            return null;
        }

        internal static string ValidateTarget(GameObject target, int? id, int? x, int? y,
            int? worldId, string query, Func<GameObject, string, bool> matchesQuery)
        {
            string selectorError = ValidateSelectors(id, x, y);
            if (selectorError != null) return selectorError;
            if (target == null) return "Target not found";
            var targetId = target.GetComponent<KPrefabID>();
            int cell = Grid.PosToCell(target);
            if (id.HasValue && (targetId == null || targetId.InstanceID != id.Value))
                return "Resolved target does not match the explicit id; read config state before retrying";
            if (!Grid.IsValidCell(cell) || !Grid.IsWorldValidCell(cell) || Grid.Visible[cell] <= 0)
                return "Native toggle target must occupy a valid visible cell";
            if (x.HasValue && cell != Grid.XYToCell(x.Value, y.Value))
                return "Resolved target does not match the explicit coordinates";
            if (worldId.HasValue && !ToolUtil.GameObjectMatchesWorld(target, worldId.Value))
                return "Resolved target does not match the requested world";
            if (!string.IsNullOrEmpty(query) && (matchesQuery == null || !matchesQuery(target, query)))
                return "Resolved target does not match the requested query";
            return null;
        }

        internal static bool Click(GameObject target, IPlayerControlledToggle toggle, System.Action beforeButtonClick,
            System.Action beforeUiSelection = null)
        {
            var details = DetailsScreen.Instance;
            if (details == null || SelectTool.Instance == null)
                throw new InvalidOperationException("Native selection/details screen is not ready");
            var selectable = toggle.GetSelectable();
            if (selectable == null || selectable.gameObject != target)
                throw new InvalidOperationException("Native toggle has no matching selectable target");

            if (!selectable.IsSelectable)
                throw new InvalidOperationException("Native toggle target is not currently selectable");
            if (ClusterManager.Instance == null || selectable.GetMyWorldId() != ClusterManager.Instance.activeWorldId)
                throw new InvalidOperationException("Native toggle target must be in the active world; select that world before retrying");

            bool retargeted = details.target != target || SelectTool.Instance.selected != selectable;
            beforeUiSelection?.Invoke();
            SelectTool.Instance.Select(selectable);
            if (SelectTool.Instance.selected != selectable)
                throw new InvalidOperationException("Native selection did not accept the requested target");
            // Refresh owns validation and UI instantiation. Unity's later Start/OnSpawn
            // binds ClickToggle; an active component alone is not a ready native button.
            // Never spawn components, register handlers or alter ToggleRequested here.
            details.Refresh(target);
            if (details.target != target)
                throw new InvalidOperationException("Native details screen did not accept the requested target");
            var screens = details.GetComponentsInChildren<PlayerControlledToggleSideScreen>(true)
                .Where(screen => screen != null && ReferenceEquals(screen.target, toggle)
                    && screen.IsValidForTarget(target))
                .ToList();
            if (screens.Count != 1)
                throw new InvalidOperationException("Initialized native player-toggle screen unavailable or ambiguous");
            var screenControl = screens[0];
            if (!screenControl.isSpawned || (screenControl.toggleButton != null && !screenControl.toggleButton.isSpawned))
                throw new InitializationPendingException();
            if (!screenControl.isActiveAndEnabled || screenControl.toggleButton == null
                || !screenControl.toggleButton.isActiveAndEnabled || !screenControl.toggleButton.isInteractable)
                throw new InvalidOperationException("Native player-toggle button is not currently interactable");

            beforeButtonClick?.Invoke();
            screenControl.toggleButton.SignalClick(KKeyCode.Mouse0);
            return retargeted;
        }
    }
}
