using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Config;
using OniMcp.Server;
using OniMcp.Support;
using PeterHan.PLib.Options;

internal static class PlibOptionsStorageRegression
{
    private static readonly string[] FieldNames =
    {
        "Language", "Host", "Port", "AuthEnabled", "AuthToken",
        "GlobalAutoDisinfectDisabled", "RequirePrintingPodSelection",
        "ScreenshotCleanupEnabled", "ScreenshotRetentionMinutes", "ScreenshotMaxFiles"
    };
    private static readonly FieldInfo Current = typeof(OniMcpOptions).GetField(
        "_current", BindingFlags.Static | BindingFlags.NonPublic);
    private static int assertions;
    private static int nativeWriterCalls;
    private static int nativeCallbackCalls;

    internal static void Run()
    {
        assertions = nativeWriterCalls = nativeCallbackCalls = 0;
        string canonical = OniMcpPaths.ConfigPath;
        string legacy = Path.Combine(OniMcpPaths.ModPath, "legacy-mod", "PublicFork.OniMcpConfig.json");
        McpHttpServer.Instance = null;
        try
        {
            var configAttribute = typeof(OniMcpOptions).GetCustomAttribute<ConfigFileAttribute>();
            Check(configAttribute != null && configAttribute.ConfigFileName == "PublicFork.OniMcpConfig.json"
                && configAttribute.IndentOutput && !configAttribute.UseSharedConfigLocation,
                "ConfigFile's second argument denotes indentation, not a shared location.");
            // Localization's configured LanguageProvider reads Current. Refusal must not invoke it.
            object result = new object();
            ExpectFailure(() => Read(legacy, typeof(OniMcpOptions)));
            Check(Current.GetValue(null) == null && !File.Exists(canonical) && !Directory.Exists(Path.GetDirectoryName(legacy)),
                "Uninitialized dialog access loaded, constructed or saved fallback configuration.");
            foreach (Type type in new[] { null, typeof(object), typeof(DerivedOptions) })
            {
                object untouched = result;
                Check(PlibOptionsStorage.ReadPrefix(legacy, type, ref result), "A foreign read was intercepted.");
                Check(ReferenceEquals(result, untouched) && Current.GetValue(null) == null,
                    "A foreign read accessed canonical state or changed the result.");
            }
            foreach (object value in new object[] { null, new object(), new DerivedOptions() })
                Check(PlibOptionsStorage.WritePrefix(value, legacy, true), "A foreign write was intercepted.");
            Check(Current.GetValue(null) == null && !File.Exists(canonical), "Pass-through initialized options.");

            var active = MakeOptions();
            OniMcpOptions.Save(active);
            string original = File.ReadAllText(canonical);
            Directory.CreateDirectory(Path.GetDirectoryName(legacy));
            const string conflicting = "{\"Language\":\"en\",\"Host\":\"localhost\",\"Port\":8788,\"AuthToken\":\"legacy-token\"}";
            File.WriteAllText(legacy, conflicting);
            var first = Read(legacy, typeof(OniMcpOptions));
            Check(!ReferenceEquals(first, active), "Dialog and active settings share a mutable instance.");
            AssertFields(first, active, "Initial canonical snapshot");
            Check(first.SecurityMigrationVersion == active.SecurityMigrationVersion, "Migration metadata was lost.");
            Check(File.ReadAllText(canonical) == original && File.ReadAllText(legacy) == conflicting,
                "Opening the dialog rewrote configuration.");
            EditAll(first);
            AssertFields(OniMcpOptions.Current, active, "Cancelled dialog isolation");
            Check(File.ReadAllText(canonical) == original && File.ReadAllText(legacy) == conflicting,
                "Uncommitted dialog edits changed disk.");
            var second = Read(legacy, typeof(OniMcpOptions));
            AssertFields(second, active, "Reopened canonical snapshot");
            Check(!ReferenceEquals(second, first), "A new dialog reused cancelled state.");

            var server = new McpHttpServer();
            McpHttpServer.Instance = server;
            EditAll(second);
            NativeDialogWriteBoundary(second, legacy);
            Check(nativeWriterCalls == 0 && nativeCallbackCalls == 1 && server.RestartCalls == 1,
                "Owned save did not skip native IO and follow exactly one post-write callback.");
            AssertFields(OniMcpOptions.Current, second, "Saved active options");
            var saved = JObject.Parse(File.ReadAllText(canonical));
            foreach (string name in FieldNames)
                Check(JToken.DeepEquals(saved[name], JToken.FromObject(typeof(OniMcpOptions).GetProperty(name).GetValue(second))),
                    "Persisted field differs: " + name);
            Check(File.ReadAllText(legacy) == conflicting, "Owned save touched PLib's legacy file.");
            OniMcpOptions.Reload();
            AssertFields(OniMcpOptions.Current, second, "Reloaded all fields");
            Check(server.RestartCalls == 1, "Reload unexpectedly restarted the server.");

            // A native post-write callback is notification only: it must not re-save a stale dialog.
            var newer = Read(legacy, typeof(OniMcpOptions));
            newer.Port = 9345;
            newer.AuthToken = "newer-canonical-token";
            OniMcpOptions.Save(newer);
            string newerBytes = File.ReadAllText(canonical);
            second.OnOptionsChanged();
            Check(ReferenceEquals(OniMcpOptions.Current, newer) && File.ReadAllText(canonical) == newerBytes,
                "Post-write callback re-saved an obsolete dialog over newer canonical settings.");
            Check(server.RestartCalls == 2, "Post-write callback did not request one server restart.");

            var invalid = Read(legacy, typeof(OniMcpOptions));
            invalid.Host = "192.0.2.10";
            invalid.AuthEnabled = false;
            ExpectFailure(() => NativeDialogWriteBoundary(invalid, legacy));
            Check(ReferenceEquals(OniMcpOptions.Current, newer) && File.ReadAllText(canonical) == newerBytes,
                "Rejected security settings changed disk or active settings.");
            Check(nativeWriterCalls == 0 && nativeCallbackCalls == 1 && server.RestartCalls == 2,
                "Rejected write reached native IO or post-write restart.");
            Check(File.ReadAllText(legacy) == conflicting, "Rejected settings changed legacy bytes.");

            string blocked = Path.Combine(OniMcpPaths.ModPath, "blocked-replacement");
            Directory.CreateDirectory(blocked);
            var ioFailure = Read(legacy, typeof(OniMcpOptions));
            ioFailure.Port = 9456;
            OniMcpPaths.ConfigPath = blocked;
            ExpectFailure(() => NativeDialogWriteBoundary(ioFailure, legacy));
            Check(ReferenceEquals(OniMcpOptions.Current, newer) && File.ReadAllText(canonical) == newerBytes,
                "Failed atomic replacement changed prior disk or active state.");
            Check(nativeWriterCalls == 0 && nativeCallbackCalls == 1 && server.RestartCalls == 2,
                "Failed replacement reached native IO or restart.");
            Check(!Directory.GetFiles(OniMcpPaths.ModPath, "*.tmp").Any(), "Failed save left a temporary configuration.");
            OniMcpPaths.ConfigPath = canonical;

            // The pre-initialization snapshot gate also refuses an unexpected active subtype.
            Current.SetValue(null, new DerivedOptions());
            ExpectFailure(() => OniMcpOptions.CreateDialogSnapshot());
            Check(Current.GetValue(null).GetType() == typeof(DerivedOptions) && File.ReadAllText(canonical) == newerBytes,
                "Unexpected active type was replaced or persisted during refusal.");
            Console.WriteLine("PASS: " + assertions + " PLib canonical storage boundary assertions");
        }
        finally
        {
            OniMcpPaths.ConfigPath = canonical;
            McpHttpServer.Instance = null;
        }
    }

    private static OniMcpOptions Read(string legacyPath, Type type)
    {
        object result = null;
        Check(!PlibOptionsStorage.ReadPrefix(legacyPath, type, ref result), "Owned read fell through to native IO.");
        Check(result != null && result.GetType() == typeof(OniMcpOptions), "Owned read returned fallback or foreign state.");
        return (OniMcpOptions)result;
    }

    // Faithful native sequence is a boundary fixture, not an execution of PLib or its patches.
    private static void NativeDialogWriteBoundary(OniMcpOptions options, string legacyPath)
    {
        if (PlibOptionsStorage.WritePrefix(options, legacyPath, true))
        {
            nativeWriterCalls++;
            File.WriteAllText(legacyPath, JsonConvert.SerializeObject(options));
        }
        nativeCallbackCalls++;
        ((IOptions)options).OnOptionsChanged();
    }

    private static OniMcpOptions MakeOptions()
    {
        return new OniMcpOptions
        {
            Language = "ja", Host = "localhost", Port = 9123, AuthEnabled = true,
            AuthToken = "canonical-token", GlobalAutoDisinfectDisabled = true,
            RequirePrintingPodSelection = true,
            ScreenshotCleanupEnabled = false, ScreenshotRetentionMinutes = 73, ScreenshotMaxFiles = 17
        };
    }

    private static void EditAll(OniMcpOptions value)
    {
        value.Language = "en";
        value.Host = "127.0.0.1";
        value.PortInput = "9234";
        value.AuthEnabled = false;
        value.AuthToken = "replacement-token";
        value.GlobalAutoDisinfectDisabled = false;
        value.RequirePrintingPodSelection = false;
        value.ScreenshotCleanupEnabled = true;
        value.ScreenshotRetentionMinutesInput = "91";
        value.ScreenshotMaxFilesInput = "23";
    }

    private static void AssertFields(OniMcpOptions actual, OniMcpOptions expected, string context)
    {
        foreach (string name in FieldNames)
            Check(Equals(typeof(OniMcpOptions).GetProperty(name).GetValue(actual),
                typeof(OniMcpOptions).GetProperty(name).GetValue(expected)), context + ": " + name);
    }

    private static void ExpectFailure(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { assertions++; return; }
        catch (IOException) { assertions++; return; }
        catch (UnauthorizedAccessException) { assertions++; return; }
        throw new Exception("Expected refusal did not occur.");
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    private sealed class DerivedOptions : OniMcpOptions { }
}
