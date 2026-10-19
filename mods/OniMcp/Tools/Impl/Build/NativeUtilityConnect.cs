using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using OniMcp.Support;
using UnityEngine;

namespace OniMcp.Tools
{
    // Drive the same two-cell gesture as the normal build UI. Never repair
    // connection masks or rebuild networks when the gesture fails.
    internal sealed class NativeUtilityConnect
    {
        internal const string ExecutionPath = "BaseUtilityBuildTool.Activate/OnLeftClickDown/OnDragTool/OnLeftClickUp";
        private readonly BaseUtilityBuildTool tool;
        private readonly MethodInfo drag;
        private readonly FieldInfo path, cell, valid;
        internal bool? UiRetargeted { get; private set; } = false;
        private bool activationAttempted;
        internal bool CommitAttempted { get; private set; }

        private NativeUtilityConnect(BaseUtilityBuildTool tool, MethodInfo drag,
            FieldInfo path, FieldInfo cell, FieldInfo valid)
        {
            this.tool = tool; this.drag = drag; this.path = path;
            this.cell = cell; this.valid = valid;
        }

        internal static NativeUtilityConnect Prepare(BaseUtilityBuildTool tool)
        {
            if (tool == null || PlayerController.Instance == null || SelectTool.Instance == null)
                throw new InvalidOperationException(McpLocalization.Get("connect_existing.ui_unavailable"));
            if (PlayerController.Instance.IsDragging() || PlayerController.Instance.ActiveTool is BaseUtilityBuildTool)
                throw new InvalidOperationException(McpLocalization.Get("connect_existing.ui_dragging"));
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(BaseUtilityBuildTool);
            var drag = type.GetMethod("OnDragTool", flags, null, new[] { typeof(int), typeof(int) }, null);
            var path = type.GetField("path", flags);
            var node = type.GetNestedType("PathNode", BindingFlags.NonPublic);
            var cell = node?.GetField("cell");
            var valid = node?.GetField("valid");
            if (drag == null || drag.ReturnType != typeof(void)
                || drag.GetParameters()[0].ParameterType != typeof(int)
                || drag.GetParameters()[1].ParameterType != typeof(int) || path == null
                || node == null || path.FieldType != typeof(List<>).MakeGenericType(node)
                || cell?.FieldType != typeof(int) || valid?.FieldType != typeof(bool))
                throw new InvalidOperationException(McpLocalization.Get("connect_existing.ui_unavailable"));
            return new NativeUtilityConnect(tool, drag, path, cell, valid);
        }

        internal void Execute(BuildingDef def, Tag material, int first, int second, System.Action revalidate)
        {
            Exception failure = null;
            try
            {
                activationAttempted = true;
                UiRetargeted = null;
                tool.Activate(def, new List<Tag> { material });
                UiRetargeted = true;
                tool.OnLeftClickDown(Grid.CellToPosCBC(first, def.SceneLayer));
                drag.Invoke(tool, new object[] { second, 1 });
                var nodes = path.GetValue(tool) as IList;
                if (nodes == null || nodes.Count != 2
                    || (int)cell.GetValue(nodes[0]) != first || (int)cell.GetValue(nodes[1]) != second
                    || !(bool)valid.GetValue(nodes[0]) || !(bool)valid.GetValue(nodes[1]))
                    throw new InvalidOperationException(McpLocalization.Get("connect_existing.native_path_rejected"));
                // Activation/click hooks may have changed a target. Do not release
                // the mouse onto a missing pipe or a replacement reservation.
                revalidate();
                CommitAttempted = true;
                tool.OnLeftClickUp(Grid.CellToPosCBC(second, def.SceneLayer));
            }
            catch (TargetInvocationException ex) { failure = ex.InnerException ?? ex; }
            catch (Exception ex) { failure = ex; }
            finally
            {
                // Leaving the native tool cancels its drag/coroutine and previews;
                // an unsuccessful gesture must not remain armed for a later click.
                if (activationAttempted)
                {
                    try { PlayerController.Instance.ActivateTool(SelectTool.Instance); }
                    catch (Exception ex) { failure = failure == null ? ex : new AggregateException(failure, ex); }
                }
            }
            if (failure != null) throw new InvalidOperationException(failure.Message, failure);
        }
    }
}
