using HarmonyLib;
using KMod;
using OniMcp.Config;
using OniMcp.Server;
using OniMcp.Support;
using OniMcp.Tools;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using System;
using System.Reflection;
using UnityEngine;

namespace OniMcp
{
    /// <summary>
    /// MOD のエントリーポイント。ゲームの読み込み時に自動でインスタンス化される。
    /// </summary>
    public class ModInfo : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            var modAssembly = assembly ?? typeof(ModInfo).Assembly;

            OniMcpPaths.Initialize(path, modAssembly);
            OniMcpOptions.Reload();
            McpLocalization.LanguageProvider = () => OniMcpOptions.Current.Language;
            McpLocalization.GameLanguageProvider = () => McpGameLanguageAdapter.Language;
            McpGameLanguageAdapter.Refresh();
            PUtil.InitLibrary();
            Localization.RegisterForTranslation(typeof(STRINGS));

            try
            {
                var options = new POptions();
                options.RegisterOptions(this, typeof(OniMcpOptions));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OniMcp] POptions registration skipped: {e}");
            }

            // The native base entry point performs PatchAll(assembly); install once after setup.
            base.OnLoad(harmony);
            SpeedTransitionTracePatch.EnsureInstalled(harmony);
            SavedModFootprintPatch.EnsureInstalled(harmony);
            InputSafetyPatchVerifier.EnsureInstalled(harmony, "OnLoad");
            AutoDisinfectPolicy.EnsureInstalled(harmony, "OnLoad");
            OniMcpLog.Debug($"[OniMcp] Loaded assembly {OniMcpVersion.Display} from {modAssembly.Location}");

            // ツールレジストリを初期化する
            OniToolRegistry.Initialize();

            // MCP サーバーとメインスレッドのブリッジを早期に起動し、メインメニューから接続できるようにする
            var bridgeObj = new GameObject("OniMcp_MainThreadBridge");
            bridgeObj.AddComponent<MainThreadBridge>();
            GameRestartCoordinator.EnsureCreated();
            var serverObj = new GameObject("OniMcp_HttpServer");
            serverObj.AddComponent<McpHttpServer>();

            OniMcpLog.Debug("[OniMcp] Mod loaded. MCP Server is starting...");
        }
    }

    // Read the game's locale on the main thread; HTTP workers only read the cached code.
    [HarmonyPatch(typeof(Db), "Initialize")]
    internal static class McpGameLanguageAdapter
    {
        private static volatile string language;
        internal static string Language => language;

        [HarmonyPostfix]
        internal static void Refresh()
        {
            try
            {
                var method = typeof(global::Localization).GetMethod("GetLocale", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                object locale = method == null ? null : method.Invoke(null, null);
                if (locale == null)
                {
                    language = null;
                    return;
                }
                var type = locale.GetType();
                var property = type.GetProperty("Code", BindingFlags.Public | BindingFlags.Instance);
                var field = type.GetField("Code", BindingFlags.Public | BindingFlags.Instance);
                language = (property != null ? property.GetValue(locale, null) : field?.GetValue(locale)) as string;
            }
            catch (Exception)
            {
                language = null;
            }
        }
    }

    // Global.Awake can load mods before it initializes Localization, while Db.Initialize
    // runs later. Refresh here too so auto language is correct at the main menu.
    [HarmonyPatch(typeof(global::Localization), "Initialize", new Type[0])]
    internal static class McpGameLanguageInitializationPatch
    {
        [HarmonyPostfix]
        internal static void Postfix()
        {
            McpGameLanguageAdapter.Refresh();
        }
    }

    /// <summary>
    /// データベースの初期化後に、範囲選択の編集ツールのランタイムインスタンスを作成する
    /// Db.Initialize() は ONI MOD の標準的なゲーム内エントリーポイント
    /// </summary>
}
