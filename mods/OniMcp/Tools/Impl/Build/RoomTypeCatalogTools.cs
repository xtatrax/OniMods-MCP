using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    internal static class RoomTypeCatalogTools
    {
        internal static CallToolResult ReadRoomTypes(JObject args)
        {
            // Wait for the colony room system before touching the game database.
            if (Game.Instance?.roomProber == null)
                return CallToolResult.Error("Load a colony before reading room type definitions.");

            var roomTypes = Db.Get()?.RoomTypes?.resources;
            if (roomTypes == null)
                return CallToolResult.Error("Room type definitions are not initialized.");

            args = args ?? new JObject();
            string typeFilter = args["type"]?.ToString()?.Trim();
            string query = args["query"]?.ToString()?.Trim();
            bool includeNeutral = ToolUtil.GetBool(args, "includeNeutral", true);
            int limit = Math.Max(1, Math.Min(ToolUtil.GetInt(args, "limit") ?? 80, 300));
            int matched = 0;
            var types = new List<Dictionary<string, object>>();

            foreach (var roomType in roomTypes)
            {
                if (roomType == null)
                    continue;
                string id = roomType.Id;
                string name = ToolUtil.CleanName(roomType.Name);
                if (!includeNeutral && string.Equals(id, "Neutral", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!Matches(id, name, typeFilter) || !Matches(id, name, query))
                    continue;

                matched++;
                if (types.Count >= limit)
                    continue;

                types.Add(new Dictionary<string, object>
                {
                    ["id"] = id,
                    ["name"] = name,
                    ["categoryId"] = roomType.category?.Id,
                    ["categoryName"] = ToolUtil.CleanName(roomType.category?.Name),
                    ["description"] = roomType.description,
                    ["criteria"] = roomType.GetCriteriaString(),
                    ["effectText"] = roomType.GetRoomEffectsString(),
                    ["effect"] = roomType.effect,
                    ["effects"] = roomType.effects,
                    ["priority"] = roomType.priority
                });
            }

            return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
            {
                ["source"] = "game_database",
                ["scope"] = "room_type_definitions",
                ["type"] = typeFilter,
                ["query"] = query,
                ["includeNeutral"] = includeNeutral,
                ["returned"] = types.Count,
                ["matched"] = matched,
                ["truncated"] = matched - types.Count,
                ["roomTypes"] = types,
                ["note"] = "Definitions include room types not established in this colony. Criteria and effects use the loaded game's localized text; this is not a diagnosis of a specific cavity. Use oni://rooms/list for existing rooms."
            }, McpJsonUtil.Settings));
        }

        private static bool Matches(string id, string name, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || (id != null && id.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                || (name != null && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
