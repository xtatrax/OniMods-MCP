using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

// Most game tool factories and the Unity overlay are replaced. Navigation and
// English metadata overrides are linked from production; native camera execution
// is poisoned. Registries, route tables, middleware and serialization are real.
namespace OniMcp.Tools
{
    internal static class GameStubs
    {
        internal static int HandlerCalls;
        internal static bool FailNextFactory;

        internal static McpTool Tool(string name)
        {
            if (FailNextFactory)
            {
                FailNextFactory = false;
                throw new InvalidOperationException("factory failure");
            }

            var tool = new McpTool
            {
                Name = name,
                Mode = "execute",
                Aliases = new List<string> { "legacy_" + name },
                Parameters = new Dictionary<string, McpToolParameter>
                {
                    ["query"] = new McpToolParameter { Type = "string" }
                },
                Handler = arguments =>
                {
                    HandlerCalls++;
                    if ((string)arguments["query"] == "throw")
                        throw new InvalidOperationException("handler failure");
                    if ((string)arguments["query"] == "error")
                        return CallToolResult.Error("handler error");
                    if ((string)arguments["query"] == "null")
                        return null;
                    return CallToolResult.Text(new JObject
                    {
                        ["tool"] = name,
                        ["arguments"] = arguments.DeepClone()
                    }.ToString());
                }
            };
            // Test-only metadata fixture: the real server factory is not linked
            // here. Runtime tools/list must separately accept its actual schema.
            if (name == "server_control")
            {
                tool.Parameters["calls"] = new McpToolParameter
                {
                    Type = "array",
                    Items = new SchemaProperty
                    {
                        Type = "object",
                        Properties = new Dictionary<string, SchemaProperty>
                        {
                            ["tool"] = new SchemaProperty
                            {
                                Type = "string",
                                Enum = new List<object> { "read_control", "building_control" }
                            },
                            ["args"] = new SchemaProperty
                            {
                                Type = "object",
                                Properties = new Dictionary<string, SchemaProperty>
                                {
                                    ["filters"] = new SchemaProperty
                                    {
                                        Type = "array",
                                        Items = new SchemaProperty
                                        {
                                            Type = "object",
                                            Properties = new Dictionary<string, SchemaProperty>
                                            {
                                                ["mode"] = new SchemaProperty
                                                {
                                                    Type = "string",
                                                    Enum = new List<object> { "default", "strict" }
                                                },
                                                ["fixtureOptions"] = new SchemaProperty
                                                {
                                                    Type = "object",
                                                    Enum = new List<object>
                                                    {
                                                        new JObject { ["nested"] = new JObject { ["values"] = new JArray("original") } }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                tool.Parameters["items"] = new McpToolParameter
                {
                    Type = "array",
                    Items = new SchemaProperty { Type = "object" }
                };
            }
            else
                tool.Parameters["untouchedFixture"] = new McpToolParameter { Type = "array" };
            return tool;
        }
    }

    internal static class ToolCallSpeechOverlay
    {
        internal static int Presentations;
        internal static void ShowNearPlayerMouse(string description) { Presentations++; }
        internal static void NotifyMissingDescription(string toolName) { }
    }

    internal static class ServerControlEntryTools { internal static McpTool ControlServer() => GameStubs.Tool("server_control"); }
    internal static class WorldEditorTools { internal static McpTool ControlWorldEditor() => GameStubs.Tool("world_editor"); }
    internal static class CameraTools
    {
        internal static int NativeBoundaryCalls;
        internal static McpTool ControlCamera()
        {
            NativeBoundaryCalls++;
            throw new InvalidOperationException("Metadata-only host invoked native camera execution.");
        }
    }
    internal static class BuildingControlTools { internal static McpTool ControlBuilding() => GameStubs.Tool("building_control"); }
    internal static class GameControlEntryTools { internal static McpTool ControlGame() => GameStubs.Tool("game_control"); }
    internal static class OrdersControlEntryTools { internal static McpTool ControlOrders() => GameStubs.Tool("orders_control"); }
    internal static class BenchmarkTools { internal static McpTool Benchmark() => GameStubs.Tool("benchmark"); }
    internal static class ColonyTools { internal static McpTool ControlColony() => GameStubs.Tool("colony_control"); }
    internal static class DuplicantTools { internal static McpTool ControlDupes() => GameStubs.Tool("dupes_control"); }
    internal static class ReadTools { internal static McpTool ControlRead() => GameStubs.Tool("read_control"); }
    internal static class SearchControlTools { internal static McpTool ControlSearch() => GameStubs.Tool("search_control"); }
    internal static class CoordinateControlTools { internal static McpTool ControlCoordinate() => GameStubs.Tool("coordinate_control"); }
}

// Default test state has no loaded colony and no Printing Pod intervention.
public sealed class Game
{
    public static Game Instance { get; set; }
    public bool IsLoading() => false;
}

public sealed class Immigration
{
    public static Immigration Instance { get; set; }
    public bool ImmigrantsAvailable { get; set; }
}

namespace OniMcp.Config
{
    public sealed class OniMcpOptions
    {
        public static OniMcpOptions Current { get; } = new OniMcpOptions();
        public bool RequirePrintingPodSelection { get; set; }
    }
}
