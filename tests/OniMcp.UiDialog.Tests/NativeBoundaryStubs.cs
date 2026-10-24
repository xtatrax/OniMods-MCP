using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Substitute only Unity, native UI and unrelated dispatch boundaries. Native
// dialog identification, tokens, guards and MCP routing remain product source.
namespace UnityEngine
{
    internal class Object
    {
        public bool Destroyed;
        private static int nextInstanceId;
        private readonly int instanceId = ++nextInstanceId;
        public int GetInstanceID() { return instanceId; }
        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || (!ReferenceEquals(a, null) && a.Destroyed);
            bool bn = ReferenceEquals(b, null) || (!ReferenceEquals(b, null) && b.Destroyed);
            return an && bn || !an && !bn && ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object obj) { return ReferenceEquals(this, obj); }
        public override int GetHashCode() { return RuntimeHelpers.GetHashCode(this); }
    }
    internal class Component : Object
    {
        public GameObject gameObject;
        public bool isActiveAndEnabled = true;
        public bool isSpawned = true;
        public Transform transform { get { return gameObject.transform; } }
        public T[] GetComponentsInParent<T>(bool includeInactive) where T : class
        { return gameObject.GetComponentsInParent<T>(includeInactive); }
    }
    internal sealed class GameObject : Object
    {
        private readonly List<object> components = new List<object>();
        public readonly List<GameObject> Children = new List<GameObject>();
        public bool activeInHierarchy = true;
        public bool activeSelf = true;
        public string name = "fixture";
        private Transform fixtureTransform;
        public Transform transform { get { return fixtureTransform; } }
        public static int FixtureParentEnumerations;
        public static Action<int> FixtureOnParentEnumeration;
        public GameObject() { fixtureTransform = new Transform(this); }
        public void FixtureUseRectTransform() { fixtureTransform = new RectTransform(this); }
        public void Add(object component)
        {
            components.Add(component);
            var item = component as Component;
            if (item != null) item.gameObject = this;
        }
        public T GetComponent<T>() where T : class { return components.OfType<T>().FirstOrDefault(); }
        public T[] GetComponentsInParent<T>(bool includeInactive) where T : class
        {
            var result = new List<T>();
            for (var node = transform; node != null; node = node.parent)
                if (includeInactive || node.gameObject.activeInHierarchy)
                    result.AddRange(node.gameObject.components.OfType<T>());
            FixtureOnParentEnumeration?.Invoke(++FixtureParentEnumerations);
            return result.ToArray();
        }
        public T GetComponentInChildren<T>() where T : class
        { return GetComponent<T>() ?? Children.Select(c => c.GetComponentInChildren<T>()).FirstOrDefault(c => c != null); }
        public void SetActive(bool value) { activeSelf = value; activeInHierarchy = value; }
    }
    internal class Transform : Object
    {
        public readonly GameObject gameObject;
        public Transform parent;
        public Transform(GameObject go) { gameObject = go; }
        public bool IsChildOf(Transform other)
        { for (var node = this; node != null; node = node.parent) if (ReferenceEquals(node, other)) return true; return false; }
    }
    internal sealed class Sprite : Object { }
}
internal sealed class LocText : UnityEngine.Component { public string text; public string key = string.Empty; }
internal enum KKeyCode { Mouse0, Mouse1 }
internal class KScreen : UnityEngine.Component
{
    public bool Active = true;
    public bool Modal = true;
    public int DeactivateCalls;
    public KScreen() { var go = new UnityEngine.GameObject(); go.Add(this); }
    public bool IsActive() { return Active; }
    public bool IsScreenActive() { return Active && isActiveAndEnabled && gameObject.activeInHierarchy; }
    public virtual bool IsModal() { return Modal; }
    public virtual void Deactivate()
    {
        DeactivateCalls++; Active = false; gameObject.SetActive(false);
        KScreenManager.Instance?.Remove(this);
        SpeedControlScreen.Instance?.Unpause(false);
    }
}
internal sealed class KScreenManager
{
    public static KScreenManager Instance;
    private static bool inputDisabled;
    private List<KScreen> screenStack = new List<KScreen>();
    public int Refreshes;
    public void Add(KScreen screen) { screenStack.Add(screen); }
    public void Remove(KScreen screen) { screenStack.Remove(screen); }
    public void RefreshStack() { Refreshes++; }
    public void DisableInput(bool value) { inputDisabled = value; }
    public KScreen[] FixtureScreens() { return screenStack.ToArray(); }
    public bool FixtureInputDisabled() { return inputDisabled; }
}
internal sealed class KButton : UnityEngine.Component
{
    public bool isInteractable = true;
    public int SignalCalls;
    public event System.Action onClick;
    public event Action<KKeyCode> onBtnClick;
    public void ClearOnClick() { onClick = null; onBtnClick = null; }
    public void SignalClick(KKeyCode key)
    {
        SignalCalls++;
        if (isInteractable)
        {
            if (key != KKeyCode.Mouse1) onClick?.Invoke();
            onBtnClick?.Invoke(key);
        }
    }
}
internal sealed class ConfirmDialogScreen : KScreen
{
    private System.Action confirmAction;
    private System.Action cancelAction;
    private System.Action configurableAction;
    private UnityEngine.GameObject confirmButton;
    private UnityEngine.GameObject cancelButton;
    private UnityEngine.GameObject configurableButton;
    private LocText titleText;
    private LocText popupMessage;
    public bool deactivateOnConfirmAction = true;
    public System.Action onDeactivateCB;
    public int NativeOkCalls;
    public bool IgnoreClose;
    // Fault injection inside the native method boundary; this is not an extra
    // button delegate that would bypass the product's callback-shape guard.
    public System.Action FixtureAfterNativeOk;
    public ConfirmDialogScreen()
    {
        titleText = new LocText(); popupMessage = new LocText();
        confirmButton = Button("OK"); cancelButton = Button("Cancel"); configurableButton = Button("Other");
        cancelButton.SetActive(false); configurableButton.SetActive(false);
    }
    private static UnityEngine.GameObject Button(string label)
    {
        var go = new UnityEngine.GameObject(); go.Add(new KButton()); go.Add(new LocText { text = label }); return go;
    }
    public void PopupConfirmDialog(string text, System.Action on_confirm, System.Action on_cancel,
        string configurable_text = null, System.Action on_configurable_clicked = null,
        string title_text = null, string confirm_text = null, string cancel_text = null, UnityEngine.Sprite image_sprite = null)
    {
        confirmAction = on_confirm; cancelAction = on_cancel; configurableAction = on_configurable_clicked;
        titleText.text = title_text; popupMessage.text = text;
        confirmButton.GetComponentInChildren<LocText>().text = confirm_text ?? "OK";
        confirmButton.GetComponent<KButton>().onClick += OnSelect_OK;
        cancelButton.SetActive(on_cancel != null); configurableButton.SetActive(on_configurable_clicked != null);
    }
    public void OnSelect_OK()
    {
        NativeOkCalls++;
        if (deactivateOnConfirmAction && !IgnoreClose) { Deactivate(); onDeactivateCB?.Invoke(); }
        confirmAction?.Invoke();
        FixtureAfterNativeOk?.Invoke();
    }
    public KButton FixtureButton { get { return confirmButton.GetComponent<KButton>(); } }
    public UnityEngine.GameObject FixtureConfirmObject { get { return confirmButton; } }
    public UnityEngine.GameObject FixtureCancelObject { get { return cancelButton; } }
    public UnityEngine.GameObject FixtureConfigObject { get { return configurableButton; } }
    public string FixtureTitle { get { return titleText.text; } set { titleText.text = value; } }
    public string FixtureMessage { get { return popupMessage.text; } set { popupMessage.text = value; } }
    public void FixtureSetAction(System.Action action) { confirmAction = action; }
    public bool FixtureHasOtherActions() { return cancelAction != null || configurableAction != null; }
}
internal sealed class SpeedControlScreen : UnityEngine.Object
{
    public static SpeedControlScreen Instance;
    public int PauseCount = 1;
    public int PauseCalls;
    public int UnpauseCalls;
    public int SetSpeedCalls, RidiculousCalls, ChangedCalls, SpeedReads;
    public int InternalSpeed;
    public bool ThrowOnSpeedRead;
    public int GetSpeed() { SpeedReads++; if (ThrowOnSpeedRead) throw new InvalidOperationException("fixture native speed read failed"); return InternalSpeed; }
    public void SetSpeed(int value) { SetSpeedCalls++; InternalSpeed = value; }
    public void ToggleRidiculousSpeed() { RidiculousCalls++; }
    private void OnChanged() { ChangedCalls++; }
    public bool ThrowOnPause;
    public bool ThrowOnPauseRead;
    public bool IsPaused { get { if (ThrowOnPauseRead) throw new InvalidOperationException("native pause read failed"); return PauseCount > 0; } }
    public void Pause(bool playSound = true, bool isCrashed = false)
    { PauseCalls++; if (ThrowOnPause) throw new InvalidOperationException("native pause failed"); PauseCount++; }
    public void Unpause(bool playSound = true) { UnpauseCalls++; PauseCount = Math.Max(0, PauseCount - 1); }
}
internal sealed class Global
{
    public static Global Instance;
    public KMod.Manager modManager = new KMod.Manager();
    public UnityEngine.GameObject globalCanvas = new UnityEngine.GameObject();
}
namespace KMod
{
    internal sealed partial class Manager
    {
        public void NotifyDialog(string title, string message_format, UnityEngine.GameObject parent) { }
    }
}
namespace STRINGS
{
    internal static class UI
    {
        internal static class FRONTEND
        {
            internal static class MOD_DIALOGS
            {
                internal static class SAVE_GAME_MODS_DIFFER
                {
                    public const string TITLE = "Native mod mismatch fixture";
                    public const string MESSAGE = "Native save mod difference: {0}";
                }
            }
        }
    }
}
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    internal sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch() { }
        public HarmonyPatch(Type type, string name) { }
        public HarmonyPatch(Type type, string name, Type[] arguments) { }
    }
    [AttributeUsage(AttributeTargets.Method)] internal sealed class HarmonyPrefix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] internal sealed class HarmonyPostfix : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] internal sealed class HarmonyFinalizer : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] internal sealed class HarmonyPrepare : Attribute { }
    [AttributeUsage(AttributeTargets.Method)] internal sealed class HarmonyTargetMethod : Attribute { }
    internal static class AccessTools
    {
        public static FieldInfo Field(Type type, string name)
        { return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
        public static MethodInfo Method(Type type, string name, Type[] arguments = null)
        { return arguments == null ? type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            : type.GetMethod(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, arguments, null); }
    }
}
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        // Match the shared ClampLimit boundary; production helper behavior is tested separately.
        internal static int ClampLimit(JObject args, int defaultValue, int max)
        {
            int value;
            if (args["limit"] != null && int.TryParse(args["limit"].ToString(), out value))
                return Math.Max(1, Math.Min(value, max));
            return defaultValue;
        }
        internal static bool GetBool(JObject args, string key, bool fallback = false)
        { bool value; return args != null && args[key] != null && bool.TryParse(args[key].ToString(), out value) ? value : fallback; }
    }
}

namespace OniMcp.Tools
{
    public sealed class McpTool
    {
        public string Name, Group, Mode, Risk, Description;
        public bool Hidden;
        public List<string> Aliases, Tags;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class McpToolParameter
    {
        public string Type, Description;
        public bool Required;
        public List<string> EnumValues;
    }
    internal static class UnrelatedDispatchBoundary
    {
        internal static int Calls;
        internal static McpTool Tool() { return new McpTool { Handler = args => { Calls++; return CallToolResult.Error("Unrelated fixture dispatch"); } }; }
    }
    internal static class UiTools { public static McpTool ControlUiAction() { return UnrelatedDispatchBoundary.Tool(); } }
    internal static class UiHintTools { public static McpTool ControlUiFeedback() { return UnrelatedDispatchBoundary.Tool(); } }
    internal static class GameLaunchTools { public static McpTool ControlGameLaunch() { return UnrelatedDispatchBoundary.Tool(); } }
    internal static class SandboxTools { public static McpTool ControlSandbox() { return UnrelatedDispatchBoundary.Tool(); } }
    internal static class SandboxDryRunRoutingPolicy
    {
        internal static bool TryRejectUnsupported(string kind, string action, bool dryRun, out string error)
        { error = null; return false; }
    }
    public static partial class GameControlTools
    {
        private static void UnpauseAll(SpeedControlScreen screen) { throw new InvalidOperationException("Unrelated speed dispatch boundary"); }
        public static McpTool ControlGameState() { return UnrelatedDispatchBoundary.Tool(); }
        public static McpTool ControlGameSave() { return UnrelatedDispatchBoundary.Tool(); }
        public static McpTool ControlDlcActivation() { return UnrelatedDispatchBoundary.Tool(); }
    }
}
