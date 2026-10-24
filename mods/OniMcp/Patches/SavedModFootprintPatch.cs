using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using KMod;
using Label = KMod.Label;
using OniMcp.Tools;

namespace OniMcp
{
    // Optional observer: an unknown IL shape disables the read instead of changing native behavior.
    // Install separately so missing bindings cannot abort the mod's PatchAll.
    internal static class SavedModFootprintPatch
    {
        private static bool fileInstalled, bodyInstalled, callsiteVerified;
        private static readonly BindingFlags InstanceDeclared = BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private const Content ExpectedMask = Content.LayerableFiles | Content.Strings | Content.DLL |
            Content.Translation | Content.Animation;

        internal static void EnsureInstalled(Harmony harmony)
        {
            if (harmony == null) return;
            try
            {
                MethodInfo file = typeof(SaveLoader).GetMethod("Load", InstanceDeclared, null, new[] { typeof(string) }, null);
                MethodInfo body = typeof(SaveLoader).GetMethod("Load", InstanceDeclared, null, new[] { typeof(IReader) }, null);
                if (file == null || body == null || file.ReturnType != typeof(bool) || body.ReturnType != typeof(bool))
                { SavedModFootprint.BindingStatus = "signature_missing"; return; }
                if (!fileInstalled)
                {
                    harmony.Patch(file, Method(nameof(FilePrefix)), null, null, Method(nameof(FileFinalizer)));
                    fileInstalled = true;
                }
                if (!bodyInstalled)
                {
                    harmony.Patch(body, Method(nameof(BodyPrefix)), null, Method(nameof(Transpiler)), Method(nameof(BodyFinalizer)));
                    bodyInstalled = true;
                }
                UpdateBinding();
            }
            catch { SavedModFootprint.BindingStatus = "patch_failed"; }
        }

        private static HarmonyMethod Method(string name)
        {
            return new HarmonyMethod(typeof(SavedModFootprintPatch).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
        }

        private static void UpdateBinding()
        {
            SavedModFootprint.BindingStatus = fileInstalled && bodyInstalled && callsiteVerified
                ? "installed" : "callsite_unavailable";
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var original = new List<CodeInstruction>(instructions);
            callsiteVerified = false;
            try
            {
                Type root = typeof(SaveLoader).Assembly.GetType("Klei.SaveFileRoot", false);
                FieldInfo field = root?.GetField("active_mods", InstanceDeclared);
                MethodInfo matcher = typeof(Manager).GetMethod("MatchFootprint", InstanceDeclared, null,
                    new[] { typeof(List<Label>), typeof(Content) }, null);
                MethodInfo observer = typeof(SavedModFootprint).GetMethod(nameof(SavedModFootprint.Observe),
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(List<Label>) || matcher == null ||
                    matcher.ReturnType != typeof(bool) || observer == null) return original;
                int position = -1, calls = 0;
                for (int i = 0; i < original.Count; i++)
                {
                    var instruction = original[i];
                    if (Equals(instruction.operand, observer)) return original;
                    if (!Equals(instruction.operand, matcher)) continue;
                    calls++;
                    if (instruction.opcode != OpCodes.Callvirt || i < 2) continue;
                    var load = original[i - 2];
                    var mask = original[i - 1];
                    bool exactMask = (mask.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(mask.operand) == (int)ExpectedMask) ||
                        (mask.opcode == OpCodes.Ldc_I4 && Equals(mask.operand, (int)ExpectedMask));
                    if (load.opcode != OpCodes.Ldfld || !Equals(load.operand, field) || !exactMask) continue;
                    // Do not move labels or exception boundaries, or admit branches that skip the observation.
                    if (load.blocks.Count != 0 || mask.blocks.Count != 0 || instruction.blocks.Count != 0 ||
                        mask.labels.Count != 0 || instruction.labels.Count != 0) continue;
                    position = i - 2;
                }
                if (calls != 1 || position < 0) return original;
                var patched = new List<CodeInstruction>(original.Count + 2);
                for (int i = 0; i < original.Count; i++)
                {
                    patched.Add(original[i]);
                    if (i != position) continue;
                    patched.Add(new CodeInstruction(OpCodes.Dup));
                    patched.Add(new CodeInstruction(OpCodes.Call, observer));
                }
                callsiteVerified = true;
                return patched;
            }
            catch { return original; }
            finally { UpdateBinding(); }
        }

        private static void FilePrefix(SaveLoader __instance, string filename, out SavedModFootprint.LoadCall __state)
        {
            __state = null;
            try { __state = SavedModFootprint.BeginFile(__instance, filename); }
            catch { }
        }

        private static Exception FileFinalizer(SavedModFootprint.LoadCall __state, bool __result, Exception __exception)
        {
            try { SavedModFootprint.EndFile(__state, __result, __exception); }
            catch { }
            return __exception;
        }

        private static void BodyPrefix(SaveLoader __instance, out SavedModFootprint.BodyCall __state)
        {
            __state = null;
            try { __state = SavedModFootprint.BeginBody(__instance); }
            catch { }
        }

        private static Exception BodyFinalizer(SavedModFootprint.BodyCall __state, bool __result, Exception __exception)
        {
            try { SavedModFootprint.EndBody(__state, __result, __exception); }
            catch { }
            return __exception;
        }
    }
}
