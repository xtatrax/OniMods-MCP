using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class InventoryTools
    {
        public static McpTool ControlResourcePin()
        {
            return new McpTool
            {
                Name = "resource_pin_control",
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "resources_pin_control", "resources_notification_control" },
                Tags = new List<string> { "resources", "inventory", "pin", "notification", "allresources" },
                Description = "資源パネルの固定/通知の統合ツール: action=list で照会。action=set で pinned/notify を設定。書き込みには confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["action"] = new McpToolParameter { Type = "string", Description = "list または set", Required = true, EnumValues = new List<string> { "list", "set" } },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "action=list の場合、資源の tag または名前で絞り込み", Required = false },
                    ["includeUnpinned"] = new McpToolParameter { Type = "boolean", Description = "action=list の場合、固定も通知もされていない発見済み資源を含めるか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "action=list の場合の最大返却数。既定値は 100、上限は 500", Required = false },
                    ["resource"] = new McpToolParameter { Type = "string", Description = "action=set の場合の資源の tag、prefabId、または名前。例: Water、Oxygen、Dirt", Required = false },
                    ["pinned"] = new McpToolParameter { Type = "boolean", Description = "action=set の場合、資源パネルに固定するか。省略時は変更しない", Required = false },
                    ["notify"] = new McpToolParameter { Type = "boolean", Description = "action=set の場合、資源通知を有効にするか。省略時は変更しない", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "action=set の場合、資源パネルの切り替え設定の変更を確認するため、true が必須", Required = false }
                },
                Handler = args =>
                {
                    string action = (args["action"]?.ToString() ?? "").Trim().ToLowerInvariant();
                    if (action == "list")
                        return ListResourcePins().Handler(args);
                    if (action == "set")
                        return SetResourcePin().Handler(args);
                    return CallToolResult.Error("action must be list or set");
                }
            };
        }

        public static McpTool ListResourcePins()
        {
            return new McpTool
            {
                Name = "resources_pins_list",
                Hidden = true,
                Group = "resources",
                Mode = "read",
                Risk = "none",
                Aliases = new List<string> { "resources_pins", "resources_notifications_list", "resource_pins_list" },
                Tags = new List<string> { "resources", "inventory", "pin", "notification", "allresources" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。read_control domain=resources action=pins を使用してください",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["query"] = new McpToolParameter { Type = "string", Description = "資源の tag または名前で絞り込み", Required = false },
                    ["includeUnpinned"] = new McpToolParameter { Type = "boolean", Description = "固定も通知もされていない発見済み資源を含めるか。既定値は false", Required = false },
                    ["limit"] = new McpToolParameter { Type = "integer", Description = "返す最大件数。既定は 100、最大 500", Required = false }
                },
                Handler = args =>
                {
                    var inventory = GetWorldInventory(args, out var worldId, out var error);
                    if (inventory == null)
                        return CallToolResult.Error(error);

                    string query = args["query"]?.ToString();
                    bool includeUnpinned = TryGetBool(args, "includeUnpinned", false);
                    int limit = ClampLimit(args, 100, 500);
                    var tags = ResourcePinTags(inventory, includeUnpinned)
                        .Where(tag => MatchesTag(tag, query))
                        .OrderBy(tag => tag.ProperNameStripLink())
                        .Take(limit)
                        .Select(tag => ResourcePinInfo(inventory, tag))
                        .ToList();

                    var result = new Dictionary<string, object>
                    {
                        ["worldId"] = worldId,
                        ["includeUnpinned"] = includeUnpinned,
                        ["returned"] = tags.Count,
                        ["resources"] = tags
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        public static McpTool SetResourcePin()
        {
            return new McpTool
            {
                Name = "resources_pin_set",
                Hidden = true,
                Group = "resources",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "set_resource_pin", "resources_notification_set", "resource_pin_set" },
                Tags = new List<string> { "resources", "inventory", "pin", "notification", "allresources" },
                Description = "非推奨の警告: 旧ツールは 0.3.0 で削除予定。read_control domain=resources action=set_pin を使用してください。confirm=true が必要",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["resource"] = new McpToolParameter { Type = "string", Description = "資源の tag、prefabId、または名前。例: Water、Oxygen、Dirt", Required = true },
                    ["pinned"] = new McpToolParameter { Type = "boolean", Description = "資源パネルに固定するか。省略時は変更しない", Required = false },
                    ["notify"] = new McpToolParameter { Type = "boolean", Description = "資源通知を有効にするか。省略時は変更しない", Required = false },
                    ["worldId"] = new McpToolParameter { Type = "integer", Description = "ワールド ID。既定は現在アクティブなワールド", Required = false },
                    ["confirm"] = new McpToolParameter { Type = "boolean", Description = "資源パネルの切り替え設定の変更を確認するため、true が必須", Required = true }
                },
                Handler = args =>
                {
                    if (!ToolUtil.GetBool(args, "confirm", false))
                        return CallToolResult.Error("confirm=true is required to change resource pin/notification settings");

                    var inventory = GetWorldInventory(args, out var worldId, out var error);
                    if (inventory == null)
                        return CallToolResult.Error(error);

                    string resource = args["resource"]?.ToString();
                    var tag = ResolveResourceTag(inventory, resource);
                    if (!tag.IsValid)
                        return CallToolResult.Error("Resource tag not found");

                    var before = ResourcePinInfo(inventory, tag);
                    if (args["pinned"] != null)
                        SetTagPresence(inventory.pinnedResources, tag, ToolUtil.GetBool(args, "pinned", false));
                    if (args["notify"] != null)
                        SetTagPresence(inventory.notifyResources, tag, ToolUtil.GetBool(args, "notify", false));

                    if (PinnedResourcesPanel.Instance != null)
                        PinnedResourcesPanel.Instance.Refresh();

                    var result = new Dictionary<string, object>
                    {
                        ["worldId"] = worldId,
                        ["resource"] = tag.Name,
                        ["before"] = before,
                        ["after"] = ResourcePinInfo(inventory, tag)
                    };

                    return CallToolResult.Text(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                }
            };
        }

        private static IEnumerable<Tag> ResourcePinTags(WorldInventory inventory, bool includeUnpinned)
        {
            var tags = new HashSet<Tag>();
            if (inventory.pinnedResources != null)
                foreach (var tag in inventory.pinnedResources)
                    tags.Add(tag);
            if (inventory.notifyResources != null)
                foreach (var tag in inventory.notifyResources)
                    tags.Add(tag);

            if (includeUnpinned && DiscoveredResources.Instance != null)
            {
                foreach (var category in GameTags.MaterialCategories)
                    foreach (var tag in DiscoveredResources.Instance.GetDiscoveredResourcesFromTag(category))
                        tags.Add(tag);
                foreach (var category in GameTags.CalorieCategories)
                    foreach (var tag in DiscoveredResources.Instance.GetDiscoveredResourcesFromTag(category))
                        tags.Add(tag);
                foreach (var category in GameTags.UnitCategories)
                    foreach (var tag in DiscoveredResources.Instance.GetDiscoveredResourcesFromTag(category))
                        tags.Add(tag);
            }

            return tags.Where(tag => tag.IsValid);
        }

        private static Dictionary<string, object> ResourcePinInfo(WorldInventory inventory, Tag tag)
        {
            var result = new Dictionary<string, object>
            {
                ["tag"] = tag.Name,
                ["name"] = tag.ProperNameStripLink(),
                ["pinned"] = inventory.pinnedResources != null && inventory.pinnedResources.Contains(tag),
                ["notify"] = inventory.notifyResources != null && inventory.notifyResources.Contains(tag)
            };

            return result;
        }

        private static Tag ResolveResourceTag(WorldInventory inventory, string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Tag.Invalid;

            string q = query.Trim();
            var candidates = ResourcePinTags(inventory, includeUnpinned: true)
                .Concat(Components.Pickupables.Items
                    .Where(item => item != null && item.KPrefabID != null)
                    .Select(item => item.KPrefabID.PrefabTag))
                .Where(tag => tag.IsValid)
                .Distinct()
                .ToList();

            var exact = candidates.FirstOrDefault(tag => EqualsIgnoreCase(tag.Name, q) || EqualsIgnoreCase(tag.ProperNameStripLink(), q));
            if (exact.IsValid)
                return exact;

            return candidates.FirstOrDefault(tag => Contains(tag.Name, q) || Contains(tag.ProperNameStripLink(), q));
        }

        private static bool MatchesTag(Tag tag, string query)
        {
            return string.IsNullOrWhiteSpace(query)
                || Contains(tag.Name, query)
                || Contains(tag.ProperNameStripLink(), query);
        }

        private static void SetTagPresence(List<Tag> tags, Tag tag, bool enabled)
        {
            if (tags == null)
                return;

            if (enabled)
            {
                if (!tags.Contains(tag))
                    tags.Add(tag);
            }
            else
            {
                tags.Remove(tag);
            }
        }
    }
}
