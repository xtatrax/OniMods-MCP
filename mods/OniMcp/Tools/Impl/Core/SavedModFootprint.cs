using System;
using System.Collections.Generic;
using System.Threading;
using KMod;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    // Only normal native load hooks produce this cache. Reads run on the Unity dispatcher.
    // In particular, a read never opens a save or calls MatchFootprint.
    internal static class SavedModFootprint
    {
        internal const int CaptureLimit = 256;
        internal const int StringLimit = 2048;
        internal const int WorldCaptureLimit = 128;
        private static long latestToken;
        [ThreadStatic] private static LoadCall current;
        private static Snapshot snapshot;
        private static string lastStatus = "unobserved";
        internal static string BindingStatus = "not_installed";

        internal sealed class LoadCall
        {
            internal LoadCall Previous;
            internal SaveLoader Owner;
            internal string Filename;
            internal long Token;
            internal int Bodies, Depth, Observations;
            internal bool BodySucceeded, Invalid, Closed;
            internal WeakReference Game;
            internal string Status = "missing_observation";
            internal LabelRow[] Rows;
        }

        internal sealed class BodyCall
        {
            internal LoadCall Load;
            internal bool Closed;
        }

        private sealed class Snapshot
        {
            internal WeakReference Game, Loader;
            internal string Filename, ActiveAtLoad;
            internal long Token;
            internal LabelRow[] Rows;
        }

        internal sealed class LabelRow
        {
            internal string Id, Title;
            internal long Version;
            internal Label.DistributionPlatform Platform;
        }

        internal static LoadCall BeginFile(SaveLoader owner, string filename)
        {
            snapshot = null;
            lastStatus = "loading";
            if (current != null) current.Invalid = true;
            var call = new LoadCall
            {
                Previous = current, Owner = owner, Filename = filename,
                Token = Interlocked.Increment(ref latestToken), Invalid = current != null
            };
            current = call;
            return call;
        }

        internal static BodyCall BeginBody(SaveLoader owner)
        {
            var call = current;
            if (call == null) return null;
            call.Bodies++;
            call.Depth++;
            if (call.Bodies != 1 || call.Depth != 1 || !ReferenceEquals(owner, call.Owner))
                call.Invalid = true;
            return new BodyCall { Load = call };
        }

        // The transpiler duplicates the existing active_mods argument at its one verified callsite.
        // Native labels are structs; retain only bounded scalar copies, never game objects.
        internal static void Observe(List<Label> labels)
        {
            var call = current;
            if (call == null) return;
            try
            {
                call.Observations++;
                if (call.Depth != 1 || call.Observations != 1)
                {
                    call.Invalid = true;
                    return;
                }
                if (labels == null) { call.Status = "missing_labels"; return; }
                if (labels.Count > CaptureLimit) { call.Status = "capture_limit"; return; }
                var rows = new LabelRow[labels.Count];
                for (int i = 0; i < rows.Length; i++)
                {
                    Label label = labels[i];
                    if ((label.id != null && label.id.Length > StringLimit) ||
                        (label.title != null && label.title.Length > StringLimit))
                    { call.Status = "capture_limit"; return; }
                    rows[i] = new LabelRow { Id = label.id, Title = label.title,
                        Version = label.version, Platform = label.distribution_platform };
                }
                call.Rows = rows;
                call.Status = "observed";
            }
            catch { call.Rows = null; call.Status = "capture_failed"; }
        }

        internal static void EndBody(BodyCall body, bool succeeded, Exception exception)
        {
            if (body == null) return;
            var call = body.Load;
            if (body.Closed) { call.Invalid = true; return; }
            body.Closed = true;
            if (!ReferenceEquals(call, current) || call.Closed || call.Depth != 1) call.Invalid = true;
            call.Depth--;
            call.BodySucceeded = succeeded && exception == null;
            if (!call.BodySucceeded) call.Status = "load_failed";
            else
            {
                // Native prefab loading may establish Game after the file prefix.
                // The successful body has already invoked Game.Instance.Load.
                try { call.Game = new WeakReference(Game.Instance); }
                catch { call.Invalid = true; call.Status = "loaded_game_unavailable"; }
            }
        }

        internal static void EndFile(LoadCall call, bool succeeded, Exception exception)
        {
            if (call == null) { snapshot = null; lastStatus = "capture_failed"; return; }
            if (call.Closed || !ReferenceEquals(current, call))
            {
                snapshot = null; lastStatus = "ambiguous_load"; call.Invalid = true;
                if (current != null) current.Invalid = true;
                return;
            }
            call.Closed = true;
            try
            {
                if (call.Token != Interlocked.Read(ref latestToken)) return;
                snapshot = null;
                if (!succeeded || exception != null || !call.BodySucceeded)
                { lastStatus = "load_failed"; return; }
                if (call.Invalid || call.Bodies != 1 || call.Depth != 0 || call.Observations != 1)
                { lastStatus = "ambiguous_load"; return; }
                if (call.Rows == null) { lastStatus = call.Status; return; }
                var game = Game.Instance;
                var loader = SaveLoader.Instance;
                if (loader == null || !ReferenceEquals(loader, call.Owner))
                { lastStatus = "loader_owner_changed"; return; }
                if (game == null || call.Game == null || call.Game.Target == null)
                { lastStatus = "loaded_game_unavailable"; return; }
                if (!ReferenceEquals(game, call.Game.Target))
                { lastStatus = "loaded_game_changed"; return; }
                snapshot = new Snapshot { Game = new WeakReference(game), Loader = new WeakReference(loader),
                    Filename = call.Filename, ActiveAtLoad = SaveLoader.GetActiveSaveFilePath(),
                    Token = call.Token, Rows = call.Rows };
                lastStatus = "observed";
            }
            catch { snapshot = null; lastStatus = "capture_failed"; }
            finally { if (ReferenceEquals(current, call)) current = call.Previous; }
        }

        internal static CallToolResult Read(JObject args)
        {
            JObject value;
            try
            {
                var game = Game.Instance;
                var loader = SaveLoader.Instance;
                var captured = snapshot;
                if (BindingStatus != "installed") value = Missing("binding_unavailable");
                else if (game == null || loader == null)
                { snapshot = null; lastStatus = "unobserved"; value = Missing("context_unavailable"); }
                else if (captured == null) value = Missing(lastStatus);
                else if (!ReferenceEquals(captured.Game.Target, game) || !ReferenceEquals(captured.Loader.Target, loader))
                { snapshot = null; lastStatus = "unobserved"; value = Missing(lastStatus); }
                else if (game.IsLoading() || Grid.CellCount <= 0) value = Missing("context_unavailable");
                else value = Present(captured, args ?? new JObject());
            }
            catch { value = Missing("context_unavailable"); }
            return CallToolResult.Text(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
        }

        private static JObject Missing(string status)
        {
            return new JObject
            {
                ["available"] = false, ["status"] = status,
                ["bindingStatus"] = BindingStatus,
                ["message"] = McpLocalization.Get("save_status.unavailable"),
                ["total"] = null, ["matched"] = null, ["returned"] = null, ["savedLabels"] = null
            };
        }

        private static JObject Present(Snapshot captured, JObject args)
        {
            JToken contextFlag = args["includeSaveContext"];
            if (contextFlag != null && contextFlag.Type != JTokenType.Boolean)
                return Missing("invalid_save_context_flag");
            string query = (args["query"]?.ToString() ?? string.Empty).Trim();
            if (query.Length > StringLimit) return Missing("query_limit");
            int limit = ToolUtil.ClampLimit(args, 20, 100);
            int matched = 0;
            var rows = new JArray();
            for (int i = 0; i < captured.Rows.Length; i++)
            {
                LabelRow row = captured.Rows[i];
                if (query.Length != 0 &&
                    (row.Id == null || row.Id.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) &&
                    (row.Title == null || row.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)) continue;
                matched++;
                if (rows.Count >= limit) continue;
                rows.Add(new JObject { ["index"] = i, ["id"] = row.Id, ["title"] = row.Title,
                    ["version"] = row.Version, ["distributionPlatform"] = row.Platform.ToString(),
                    ["distributionPlatformValue"] = (int)row.Platform });
            }
            string active = SaveLoader.GetActiveSaveFilePath();
            var result = new JObject
            {
                ["available"] = true, ["status"] = "observed", ["bindingStatus"] = BindingStatus,
                ["message"] = McpLocalization.Get("save_status.observed"),
                ["source"] = "SaveLoader.Load(IReader)/SaveFileRoot.active_mods/MatchFootprint_argument",
                ["idSemantics"] = "serialized_label_id_not_mod_static_id",
                ["loadToken"] = captured.Token, ["loadedSourceFilename"] = captured.Filename,
                ["activeSaveAtLoad"] = captured.ActiveAtLoad, ["activeSaveFile"] = active,
                ["activeSaveChangedSinceLoad"] = !string.Equals(active, captured.ActiveAtLoad, StringComparison.Ordinal),
                ["total"] = captured.Rows.Length, ["matched"] = matched, ["returned"] = rows.Count,
                ["truncated"] = rows.Count < matched, ["savedLabels"] = rows
            };
            if (contextFlag != null && contextFlag.Value<bool>())
                result["currentSaveContext"] = ReadCurrentContext();
            return result;
        }

        // Explicit opt-in: current native cache fields, separate from the loaded labels above.
        // Use the same component lookup as native save migration, with no arbitrary Name override.
        private static JObject ReadCurrentContext()
        {
            try
            {
                var saveGame = SaveGame.Instance;
                var manager = ClusterManager.Instance;
                if (saveGame == null || manager == null) return MissingContext("context_unavailable");
                string name = saveGame.BaseName;
                var worlds = manager.WorldContainers;
                if (worlds == null) return MissingContext("worlds_unavailable");
                if (worlds.Count > WorldCaptureLimit || (name != null && name.Length > StringLimit))
                    return MissingContext("capture_limit");
                var rows = new JArray();
                for (int i = 0; i < worlds.Count; i++)
                {
                    var world = worlds[i];
                    if (world == null) return MissingContext("world_unavailable");
                    bool discovered = world.IsDiscovered;
                    bool moduleInterior = world.IsModuleInterior;
                    var entity = discovered && !moduleInterior
                        ? world.GetComponent<ClusterGridEntity>() : null;
                    bool supported = entity != null && entity.GetType() == typeof(AsteroidGridEntity);
                    string rawName = supported ? ((AsteroidGridEntity)entity).Name : null;
                    if (rawName != null && rawName.Length > StringLimit) return MissingContext("capture_limit");
                    rows.Add(new JObject
                    {
                        ["worldId"] = world.id, ["isDiscovered"] = discovered,
                        ["isModuleInterior"] = moduleInterior,
                        ["clusterNameAvailable"] = supported, ["clusterName"] = rawName,
                        ["clusterNameSource"] = supported ? "AsteroidGridEntity.Name" : null
                    });
                }
                return new JObject
                {
                    ["available"] = true, ["status"] = "observed",
                    ["source"] = "current_native_SaveGame/ClusterManager/WorldContainer/AsteroidGridEntity",
                    ["message"] = McpLocalization.Get("save_status.current_context"),
                    ["colonyName"] = name, ["worldCount"] = rows.Count, ["worlds"] = rows,
                    ["discoverySemantics"] = "WorldContainer.IsDiscovered_including_native_debug_override"
                };
            }
            catch { return MissingContext("context_unavailable"); }
        }

        private static JObject MissingContext(string status)
        {
            return new JObject
            {
                ["available"] = false, ["status"] = status,
                ["message"] = McpLocalization.Get("save_status.current_context"),
                ["colonyName"] = null, ["worldCount"] = null, ["worlds"] = null
            };
        }
    }
}
