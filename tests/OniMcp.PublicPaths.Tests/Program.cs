using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OniMcp.Support;
using UnityEngine;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "OniMcp.PublicPaths.Tests-" + Guid.NewGuid().ToString("N"));
        string persistent = Path.Combine(root, "persistent");
        string mod = Path.Combine(root, "mods", "Local", "OniMcpPublic");
        Directory.CreateDirectory(persistent);
        Directory.CreateDirectory(mod);
        var originals = new Dictionary<string, string>();
        try
        {
            // Old developer/upstream files and another mod coexist at the real resolver boundaries.
            foreach (string directory in new[] { persistent, mod })
                foreach (string filename in new[] { "OniMcpConfig.json", "OniMcpRestartIntent.json", "AnotherModConfig.json" })
                {
                    string path = Path.Combine(directory, filename);
                    string text = "preserve:" + directory + ":" + filename;
                    File.WriteAllText(path, text);
                    originals.Add(path, text);
                }
            Application.persistentDataPath = persistent;
            OniMcpPaths.Initialize(mod, typeof(Program).Assembly);
            string publicPersistent = Path.Combine(persistent, "PublicFork.OniMcpConfig.json");
            string publicLocal = Path.Combine(mod, "PublicFork.OniMcpConfig.json");
            string publicIntent = Path.Combine(persistent, "PublicFork.OniMcpRestartIntent.json");
            Check(OniMcpPaths.ModPath == mod, "explicit installed mod directory is retained");
            Check(OniMcpPaths.ConfigPath == publicPersistent, "legacy files are ignored; new public config defaults to persistent directory");
            Check(!File.Exists(publicPersistent), "resolving absent public config does not create or migrate anything");
            Check(OniMcpPaths.RestartIntentPath == publicIntent, "public restart intent does not read the legacy intent");
            Check(!File.Exists(publicIntent), "resolving public restart path does not copy legacy jobs");
            File.WriteAllText(publicLocal, "public-local");
            Check(OniMcpPaths.ConfigPath == publicLocal, "existing public mod-local config is honored despite legacy persistent config");
            File.WriteAllText(publicPersistent, "public-persistent");
            Check(OniMcpPaths.ConfigPath == publicPersistent, "public persistent config takes precedence over public mod-local config");
            Check(File.ReadAllText(publicLocal) == "public-local", "resolving public persistent config leaves local config intact");
            File.Delete(publicPersistent);
            Check(OniMcpPaths.ConfigPath == publicLocal, "public mod-local config remains the public fallback");
            File.Delete(publicLocal);
            Check(OniMcpPaths.ConfigPath == publicPersistent, "absence of both public candidates never falls back to a legacy file");
            Application.persistentDataPath = null;
            Check(OniMcpPaths.ConfigPath == publicLocal, "without Unity persistent directory, default remains public-specific in mod directory");
            Check(OniMcpPaths.RestartIntentPath == Path.Combine(mod, "PublicFork.OniMcpRestartIntent.json"), "restart intent fallback remains public-specific");
            File.WriteAllText(publicLocal, "public-local-fallback");
            Check(OniMcpPaths.ConfigPath == publicLocal, "public local existing config works without persistent directory");
            Check(OniMcpPaths.RestartRelayPath == Path.Combine(mod, "assets", "restart_oni_steam_relay.sh"), "shared asset name stays within the selected mod installation");
            foreach (var file in originals)
                Check(File.ReadAllText(file.Key) == file.Value, "old or unrelated file changed: " + Path.GetFileName(file.Key));
            Check(originals.Count == 6, "both real boundary directories contain preserved non-public fixtures");
            Console.WriteLine("Public path isolation tests passed: " + checks);
        }
        finally
        {
            Application.persistentDataPath = null;
            Directory.Delete(root, true);
        }
    }
}
