using System;
using System.Reflection;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    // This is the callback used by ONI's normal DisconnectTool. Keep scope and
    // consent checks in the caller; connection mutation belongs to the game.
    internal sealed class NativeUtilityDisconnect
    {
        internal const string ExecutionPath = "DisconnectTool.DisconnectCellsAction";
        private readonly object tool;
        private readonly MethodInfo action;

        private NativeUtilityDisconnect(object tool, MethodInfo action)
        {
            this.tool = tool;
            this.action = action;
        }

        internal static NativeUtilityDisconnect Prepare(object tool)
        {
            if (tool == null)
                throw new InvalidOperationException("Native disconnect tool is not initialized; no changes applied");
            var signature = new[] { typeof(int), typeof(GameObject), typeof(IHaveUtilityNetworkMgr), typeof(UtilityConnections) };
            var action = tool.GetType().GetMethod("DisconnectCellsAction",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, signature, null);
            if (action == null || action.ReturnType != typeof(void)
                || !action.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(signature))
                throw new InvalidOperationException("Native disconnect UI action is unavailable; refusing direct connection mutation");
            return new NativeUtilityDisconnect(tool, action);
        }

        internal void Execute(int cell, GameObject target, IHaveUtilityNetworkMgr utility, UtilityConnections removeConnections)
        {
            try
            {
                action.Invoke(tool, new object[] { cell, target, utility, removeConnections });
            }
            catch (TargetInvocationException ex)
            {
                throw new InvalidOperationException("Native disconnect UI action failed: "
                    + (ex.InnerException ?? ex).Message, ex.InnerException ?? ex);
            }
        }
    }
}
