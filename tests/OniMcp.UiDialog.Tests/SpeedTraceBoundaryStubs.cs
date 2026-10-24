using System;
using System.Collections.Generic;
using System.Reflection;

// Observer-only native boundaries: no real game assembly or native implementation.
// The installed game defines a global DateTime; keep the qualification regression visible.
internal sealed class DateTime { }
namespace UnityEngine
{
    internal static class Time
    {
        public static int frameCount = 1;
        public static float realtimeSinceStartup = 1;
        public static float timeScale = 0;
    }
    internal static class Mathf
    {
        public static int RoundToInt(float value) { return (int)Math.Round(value); }
    }
}
internal sealed class Game : UnityEngine.Object
{
    private static Game instance;
    public static bool ThrowOnRead;
    public bool Loading;
    public bool IsLoading() { return Loading; }
    public static Game Instance
    {
        get { if (ThrowOnRead) throw new InvalidOperationException("fixture game unavailable"); return instance; }
        set { instance = value; }
    }
}
internal sealed class GameClock
{
    public static GameClock Instance;
    public float TimeValue = 123.4567f;
    public int Reads;
    public bool ThrowOnRead;
    public float GetCurrentCycleAsPercentage() { return 0; }
    public float GetTime() { Reads++; if (ThrowOnRead) throw new InvalidOperationException("fixture clock unavailable"); return TimeValue; }
}
internal static class GameUtil { public static int GetCurrentCycle() { return 0; } }
namespace OniMcp.Server
{
    internal static class GameContextLifecycle
    {
        internal static int Generation = 1;
        internal static int CaptureGeneration() { return Generation; }
    }
    internal static class McpHttpServer
    {
        [ThreadStatic] private static string session;
        internal static bool ThrowOnSessionRead;
        internal static string CurrentSessionId
        {
            get { if (ThrowOnSessionRead) throw new InvalidOperationException("fixture context unavailable"); return session; }
            set { session = value; }
        }
    }
}
namespace HarmonyLib
{
    internal sealed class HarmonyMethod
    {
        public readonly MethodInfo method;
        public HarmonyMethod(MethodInfo value) { method = value; }
    }
    internal sealed class Harmony
    {
        internal sealed class Registration
        {
            internal MethodBase Original;
            internal HarmonyMethod Prefix, Postfix, Transpiler, Finalizer;
        }
        internal static readonly List<Registration> Registrations = new List<Registration>();
        internal static string ThrowOnMethod;
        public Harmony(string id) { }
        public void Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null,
            HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
        {
            if (original.Name == ThrowOnMethod) throw new InvalidOperationException("fixture observer patch unavailable");
            Registrations.Add(new Registration { Original = original, Prefix = prefix, Postfix = postfix, Transpiler = transpiler, Finalizer = finalizer });
        }
    }
}
