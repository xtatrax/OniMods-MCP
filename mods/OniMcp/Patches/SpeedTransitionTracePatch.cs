using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using OniMcp.Tools;

namespace OniMcp
{
    // Install separately: missing optional observer bindings must not abort PatchAll.
    internal static class SpeedTransitionTracePatch
    {
        private static readonly HashSet<MethodBase> Installed = new HashSet<MethodBase>();

        internal static void EnsureInstalled(Harmony harmony)
        {
            if (harmony == null) return;
            foreach (string name in SpeedTransitionTrace.Methods)
            {
                try
                {
                    Type[] parameters = name == "Pause" ? new[] { typeof(bool), typeof(bool) }
                        : name == "Unpause" ? new[] { typeof(bool) }
                        : name == "SetSpeed" ? new[] { typeof(int) } : Type.EmptyTypes;
                    MethodInfo method = typeof(SpeedControlScreen).GetMethod(name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                        null, parameters, null);
                    if (method == null || method.IsStatic || method.ReturnType != typeof(void))
                    {
                        SpeedTransitionTrace.SetCoverage(name, "signature_missing");
                        continue;
                    }
                    if (!Installed.Contains(method))
                    {
                        harmony.Patch(method,
                            new HarmonyMethod(typeof(SpeedTransitionTracePatch).GetMethod(nameof(Prefix), BindingFlags.Static | BindingFlags.NonPublic)),
                            null, null,
                            new HarmonyMethod(typeof(SpeedTransitionTracePatch).GetMethod(nameof(Finalizer), BindingFlags.Static | BindingFlags.NonPublic)));
                        Installed.Add(method);
                    }
                    SpeedTransitionTrace.SetCoverage(name, "installed");
                }
                catch
                {
                    SpeedTransitionTrace.SetCoverage(name, "patch_failed");
                }
            }
        }

        private static void Prefix(SpeedControlScreen __instance, MethodBase __originalMethod, out object __state)
        {
            __state = null;
            try { __state = SpeedTransitionTrace.Begin(__instance, __originalMethod); }
            catch { }
        }

        private static Exception Finalizer(SpeedControlScreen __instance, object __state, Exception __exception)
        {
            try { SpeedTransitionTrace.End(__state as SpeedTransitionTrace.Call, __instance, __exception); }
            catch { }
            return __exception;
        }
    }
}
