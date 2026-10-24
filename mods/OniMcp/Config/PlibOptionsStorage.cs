using System;
using System.Reflection;
using HarmonyLib;
using PeterHan.PLib.Options;

namespace OniMcp.Config
{
    // PLib's normal dialog uses its owner's POptions copy. Adapt only this exact options
    // type; other mods retain their original paths, serialization and callbacks.
    internal static class PlibOptionsStorage
    {
        private static bool installed;

        internal static bool EnsureInstalled(Harmony harmony)
        {
            if (installed) return true;
            if (harmony == null) return false;
            try
            {
                const BindingFlags flags = BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                MethodInfo read = typeof(POptions).GetMethod("ReadSettings", flags, null,
                    new[] { typeof(string), typeof(Type) }, null);
                MethodInfo write = typeof(POptions).GetMethod("WriteSettings", flags, null,
                    new[] { typeof(object), typeof(string), typeof(bool) }, null);
                if (read == null || write == null || read.IsGenericMethod || write.IsGenericMethod ||
                    read.ReturnType != typeof(object) || write.ReturnType != typeof(void)) return false;
                harmony.Patch(read, new HarmonyMethod(typeof(PlibOptionsStorage), nameof(ReadPrefix)));
                harmony.Patch(write, new HarmonyMethod(typeof(PlibOptionsStorage), nameof(WritePrefix)));
                installed = HasPrefix(read, nameof(ReadPrefix)) && HasPrefix(write, nameof(WritePrefix));
            }
            catch { installed = false; }
            return installed;
        }

        private static bool HasPrefix(MethodInfo target, string name)
        {
            MethodInfo expected = typeof(PlibOptionsStorage).GetMethod(name,
                BindingFlags.Static | BindingFlags.NonPublic);
            var patches = Harmony.GetPatchInfo(target);
            if (patches == null) return false;
            foreach (var patch in patches.Prefixes)
                if (patch.PatchMethod == expected) return true;
            return false;
        }

        internal static bool ReadPrefix(string path, Type optionsType, ref object __result)
        {
            if (optionsType != typeof(OniMcpOptions)) return true;
            // Do not let PLib convert a missing snapshot into newly constructed defaults.
            __result = OniMcpOptions.CreateDialogSnapshot();
            return false;
        }

        internal static bool WritePrefix(object settings, string path, bool indent)
        {
            if (settings == null || settings.GetType() != typeof(OniMcpOptions)) return true;
            // Save validates and replaces atomically. Propagate failure before PLib invokes
            // its post-write callback; never also write a second PLib configuration file.
            OniMcpOptions.Save((OniMcpOptions)settings);
            return false;
        }
    }
}
