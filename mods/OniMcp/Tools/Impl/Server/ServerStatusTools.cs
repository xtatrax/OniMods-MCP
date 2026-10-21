using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Config;
using OniMcp.Core;
using OniMcp.Server;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class ServerTools
    {
        public static McpTool GetMcpStatus()
        {
            return new McpTool
            {
                Name = "server_status",
                Group = "server",
                Mode = "read",
                Risk = "none",
                Hidden = true,
                Aliases = new List<string> { "get_mcp_status" },
                Description = "旧名との互換用。server_diagnostics_control action=status を推奨",
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["includePatches"] = new McpToolParameter { Type = "boolean", Description = McpLocalization.Get("patch_registration.include_patches_description"), Required = false }
                },
                Handler = args =>
                {
                    var includeToken = args?["includePatches"];
                    if (includeToken != null && includeToken.Type != JTokenType.Boolean)
                        return CallToolResult.Error(McpLocalization.Get("patch_registration.invalid_include_patches"));
                    bool includePatches = includeToken?.Value<bool>() ?? false;
                    var server = McpHttpServer.Instance;
                    var status = new Dictionary<string, object>
                    {
                        ["loaded"] = server != null,
                        ["endpoint"] = server?.EndpointUrl,
                        ["port"] = server?.Port ?? 0,
                        ["configPath"] = OniMcpOptions.ConfigPath,
                        ["toolCount"] = OniToolRegistry.GetVisibleTools().Count,
                        ["listedToolCount"] = OniToolRegistry.GetDefaultToolInfoCount(),
                        ["toolsListMode"] = "core",
                        ["discovery"] = "tools/list and catalog return the public aggregate tools needed for normal play; hidden legacy aliases are internal compatibility only."
                    };

                    if (includePatches) status["patchRegistration"] = OwnedHarmonyPatchAudit.Read();
                    return CallToolResult.Text(JsonConvert.SerializeObject(status, McpJsonUtil.Settings));
                }
            };
        }
    }
}
