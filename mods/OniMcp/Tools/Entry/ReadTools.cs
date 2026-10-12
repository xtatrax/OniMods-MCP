using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    internal static class ReadTools
    {
        internal static McpTool ControlRead()
        {
            return new McpTool
            {
                Name = "read_control",
                Group = "read",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "state_read_control", "query_control" },
Tags = new List<string> { "read", "state", "world", "area", "buildings", "resources", "infrastructure", "database", "knowledge" },
Description = "Unified read entrypoint. Routes current state, world cells/maps/search, area handles, buildings, resources, and infrastructure. First agent call can use domain=state action=current. For local infrastructure ports use domain=infrastructure action=nearby_ports x/y/radius kind=power|liquid|gas|logic|rail|all.",
                Parameters = Parameters(),
                Handler = HandleRead
            };
        }

        private static Dictionary<string, McpToolParameter> Parameters()
        {
            return new Dictionary<string, McpToolParameter>
            {
                ["domain"] = new McpToolParameter
                {
                    Type = "string",
Description = "state, world, area, buildings, resources, infrastructure, or database (loaded Codex).",
                    Required = true,
EnumValues = new List<string> { "state", "world", "area", "buildings", "resources", "infrastructure", "database", "knowledge" }
                },
                ["action"] = new McpToolParameter
                {
                    Type = "string",
Description = "state=current/current_state/overview; world=cell_info/cell_detail/text_map/search/layout_candidates/reachable_area; area=define/get/list/blocks/merge/forget; buildings=list/summary; resources=inventory/food/search_items/pins/set_pin; infrastructure=power_summary/power_ports/ports/nearby_ports/rooms/room_types. database=query/list/search/categories/page; room_types returns conditions and effects for all defined room types, including types not established in the colony.",
                    Required = true
                },
                ["kind"] = new McpToolParameter
                {
                    Type = "string",
                    Description = "Subtype filter. For infrastructure action=ports: all, power, liquid, gas, logic, rail. database reads the loaded Codex cache; guide remains disabled.",
                    Required = false,
                    EnumValues = new List<string> { "all", "power", "liquid", "gas", "logic", "rail", "conduit", "database", "guide" }
                },
                ["type"] = new McpToolParameter { Type = "string", Description = "Alias subtype filter used by some child actions.", Required = false },
                ["id"] = new McpToolParameter { Type = "string", Description = "Object/resource/area id depending on domain.", Required = false },
                ["query"] = new McpToolParameter { Type = "string", Description = "Search or filter query.", Required = false },
                ["target"] = new McpToolParameter { Type = "string", Description = "Alias for query.", Required = false },
                ["search"] = new McpToolParameter { Type = "string", Description = "Alias for query.", Required = false },
                ["pattern"] = new McpToolParameter { Type = "string", Description = "world search sequence pattern, e.g. Dirt-Oxygen or 粉砂岩-泥土-氧气.", Required = false },
                ["sequence"] = new McpToolParameter { Type = "string", Description = "Alias sequence/pattern text for search-like actions.", Required = false },
                ["category"] = new McpToolParameter { Type = "string", Description = "Optional category filter for resources/buildings; database uses an exact category ID.", Required = false },
                ["detail"] = new McpToolParameter { Type = "string", Description = "brief/full/compact detail level depending on child action.", Required = false },
                ["foodDetail"] = new McpToolParameter { Type = "string", Description = "resources/food: summary (default totals only), types (food breakdown), full (item samples and evidence).", Required = false, EnumValues = new List<string> { "summary", "types", "full" } },
                ["profile"] = new McpToolParameter { Type = "string", Description = "Map/snapshot output profile such as scan, minimal, or standard.", Required = false },
                ["format"] = new McpToolParameter { Type = "string", Description = "Requested output format, e.g. json, markdown, edit, raw.", Required = false },
                ["view"] = new McpToolParameter { Type = "string", Description = "Requested map/infrastructure view such as power, temperature, oxygen, liquid_conduits.", Required = false },
                ["compact"] = new McpToolParameter { Type = "boolean", Description = "Use compact token-saving map/table output when supported.", Required = false },
                ["syncView"] = new McpToolParameter { Type = "boolean", Description = "When reading map views, also switch the in-game overlay for livestream visibility.", Required = false },
                ["includeItems"] = new McpToolParameter { Type = "boolean", Description = "Include pickupables/dropped items when supported.", Required = false },
                ["includeCriteria"] = new McpToolParameter { Type = "boolean", Description = "For infrastructure rooms, include current room criteria text. room_types always includes definition criteria and effects.", Required = false },
                ["includeNeutral"] = new McpToolParameter { Type = "boolean", Description = "Include Neutral rooms/types. Default false for rooms, true for room_types.", Required = false },
                ["includeBuildings"] = new McpToolParameter { Type = "boolean", Description = "Include building anchors/details when supported.", Required = false },
                ["includeDupes"] = new McpToolParameter { Type = "boolean", Description = "Include duplicants when supported.", Required = false },
                ["includeContent"] = new McpToolParameter { Type = "boolean", Description = "database: include cached text and known panel fields. Default true for query, false for list/search.", Required = false },
                ["offset"] = new McpToolParameter { Type = "integer", Description = "database: metadata row offset; action=page uses UTF-16 character offsets returned by nextOffset.", Required = false },
                ["pageSize"] = new McpToolParameter { Type = "integer", Description = "database page fragment size, default 1000, max 2000. Join text fragments then parse JSON.", Required = false },
                ["revision"] = new McpToolParameter { Type = "string", Description = "database page: pass the first page revision to reject content changes between pages.", Required = false },
                ["maxChars"] = new McpToolParameter { Type = "integer", Description = "database: max returned text characters per entry, default 6000, max 20000.", Required = false },
                ["includeDetails"] = new McpToolParameter { Type = "boolean", Description = "Include detailed records for summary actions.", Required = false },
                ["includeReachability"] = new McpToolParameter { Type = "boolean", Description = "For world cell_info/cell_detail, include duplicant reachability to the target cell.", Required = false },

 ["includeInfrastructure"] = new McpToolParameter { Type = "boolean", Description = "For state/current, include compact infrastructure ports/lines overview. Default false to save tokens.", Required = false },
 ["infrastructureKind"] = new McpToolParameter { Type = "string", Description = "For state/current includeInfrastructure: all, power, liquid, gas, logic, rail.", Required = false },
 ["infrastructureLimit"] = new McpToolParameter { Type = "integer", Description = "For state/current includeInfrastructure: max buildings to summarize. Default 40.", Required = false },
 ["includeLogs"] = new McpToolParameter { Type = "boolean", Description = "For state/current, include compact Player.log error tail. Default false to save tokens.", Required = false },
 ["includeLogErrors"] = new McpToolParameter { Type = "boolean", Description = "Alias for includeLogs; returns only suspicious recent log lines.", Required = false },
 ["logLimit"] = new McpToolParameter { Type = "integer", Description = "For includeLogs: tail lines scanned from Player.log. Default 160, max 500.", Required = false },
 ["includeBlueprints"] = new McpToolParameter { Type = "boolean", Description = "For infrastructure ports, include construction blueprints. Default true.", Required = false },
                ["worldId"] = new McpToolParameter { Type = "integer", Description = "World id. Defaults to active world or area-bound world.", Required = false },
                ["x"] = new McpToolParameter { Type = "integer", Description = "Cell X for point queries or alias for x1 in area actions.", Required = false },
                ["y"] = new McpToolParameter { Type = "integer", Description = "Cell Y for point queries or alias for y1 in area actions.", Required = false },
                ["x1"] = new McpToolParameter { Type = "integer", Description = "Area lower/left X.", Required = false },
                ["y1"] = new McpToolParameter { Type = "integer", Description = "Area lower Y.", Required = false },
                ["x2"] = new McpToolParameter { Type = "integer", Description = "Area upper/right X.", Required = false },
                ["y2"] = new McpToolParameter { Type = "integer", Description = "Area upper Y.", Required = false },
                ["areaId"] = new McpToolParameter { Type = "string", Description = "Reusable area handle.", Required = false },
                ["areaIds"] = new McpToolParameter { Type = "array", Description = "Area handle list for merge-like actions.", Required = false },
                ["label"] = new McpToolParameter { Type = "string", Description = "Optional area label.", Required = false },
                ["blockWidth"] = new McpToolParameter { Type = "integer", Description = "area blocks target width.", Required = false },
                ["blockHeight"] = new McpToolParameter { Type = "integer", Description = "area blocks target height.", Required = false },
                ["maxCells"] = new McpToolParameter { Type = "integer", Description = "Maximum map/area cells to return.", Required = false },
                ["limit"] = new McpToolParameter { Type = "integer", Description = "Maximum returned items.", Required = false },

                ["radius"] = new McpToolParameter { Type = "integer", Description = "For infrastructure nearby_ports, scan radius around x/y. Default 8, max 80.", Required = false },
                ["direction"] = new McpToolParameter { Type = "string", Description = "Search direction when supported.", Required = false },
                ["matchMode"] = new McpToolParameter { Type = "string", Description = "Search matching mode when supported.", Required = false },
                ["dryRun"] = new McpToolParameter { Type = "boolean", Description = "Preview-only flag for child actions that support it.", Required = false },
                ["confirm"] = new McpToolParameter { Type = "boolean", Description = "Required for read_control resource write actions such as set_pin.", Required = false }
            };
        }

        private static CallToolResult HandleRead(JObject args)
        {
            args = args ?? new JObject();
            string domain = (args["domain"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            string action = (args["action"]?.ToString() ?? string.Empty).Trim().ToLowerInvariant();
            OniMcp.Support.OniMcpLog.Debug($"[OniMcp] read_control: domain={domain}, action={action}");

switch (domain)
{
case "state":
case "current":
case "overview":
return CurrentStateReadTools.ReadCurrent(args);
case "world":
if (action == "current" || action == "current_state" || action == "overview")
return CurrentStateReadTools.ReadCurrent(args);
return WorldAnalysisTools.ReadWorldControl().Handler(args);
                case "area":
                    return AreaTools.ControlArea().Handler(args);
                case "buildings":
                    return GameControlTools.ControlBuildingsRead().Handler(args);
                case "resources":
                    return InventoryTools.ControlResources().Handler(args);
                case "infrastructure":
                    if (action == "ports" || action == "utility_ports" || action == "all_ports" || action == "nearby_ports" || action == "ports_nearby")
                        return InfrastructurePortReadTools.ReadPorts(args);
                    return PowerAndRoomTools.InfrastructureReadControl().Handler(args);
                case "knowledge":
                    if (args["kind"]?.ToString() == "guide") return ForwardKnowledge(args);
                    return CodexReadTools.Read(args);
                case "database":
                    return CodexReadTools.Read(args);
                case "guide":
                case "mechanics":
                case "mechanic":
                    return ForwardKnowledge(args);
                default:
                    return CallToolResult.Error("domain must be state, world, area, buildings, resources, infrastructure, or database");
            }
        }

        private static CallToolResult ForwardKnowledge(JObject args)
        {
            return CallToolResult.Error("The legacy guide/mechanics domain remains disabled. Use database for the loaded Codex cache.");
        }
    }
}
