using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using OniMcp.Support;

namespace OniMcp.Tools
{
    /// <summary>
    /// MCP Tool レジストリ
    /// AI に公開するゲーム操作ツールを一元管理する
    /// </summary>
    public static class OniToolRegistry
    {
        private static readonly Dictionary<string, McpTool> _tools = new Dictionary<string, McpTool>();
        private static readonly Dictionary<string, McpTool> _internalOperations = new Dictionary<string, McpTool>();
        private static readonly Dictionary<string, string> _aliases = new Dictionary<string, string>();
        private static readonly HashSet<string> DefaultPublicToolNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "building_control",
            "game_control",
            "navigation_control",
            "orders_control",
            "benchmark",
            "server_control",
            "world_editor",
        };
        private static bool _initialized;
        private static readonly OniToolRegistryCache ToolCache = new OniToolRegistryCache();
        private static List<McpToolInfo> _cachedCoreToolInfos, _cachedAllToolInfos;

        /// <summary>
        /// すべてのツールを初期化する
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;

            Register(CoreToolEnglishDescriptions.Apply(ServerControlEntryTools.ControlServer()));
            Register(CoreToolEnglishDescriptions.Apply(WorldEditorTools.ControlWorldEditor()));
            Register(CoreToolEnglishDescriptions.Apply(NavigationControlTools.ControlNavigation()));
            Register(CoreToolEnglishDescriptions.Apply(BuildingControlTools.ControlBuilding()));
            Register(CoreToolEnglishDescriptions.Apply(GameControlEntryTools.ControlGame()));
            Register(CoreToolEnglishDescriptions.Apply(OrdersControlEntryTools.ControlOrders()));
            Register(BenchmarkTools.Benchmark());
            RegisterInternal(ColonyTools.ControlColony());
            RegisterInternal(DuplicantTools.ControlDupes());
            RegisterInternal(ReadTools.ControlRead());
            RegisterInternal(SearchControlTools.ControlSearch());
            RegisterInternal(CoordinateControlTools.ControlCoordinate());
            BuildToolInfoCache();
            _initialized = true;
        }

        private static void Register(McpTool tool)
        {
            ToolMetadata.ApplyDefaults(tool);
            _tools[tool.Name] = tool;
            foreach (var alias in tool.Aliases)
            {
                if (!string.IsNullOrEmpty(alias))
                    _aliases[alias] = tool.Name;
            }

            ToolCache.Clear();
            _cachedCoreToolInfos = null;
            _cachedAllToolInfos = null;
        }

        private static void RegisterInternal(McpTool operation)
        {
            ToolMetadata.ApplyDefaults(operation);
            _internalOperations[operation.Name] = operation;
        }

        internal static bool TryGetOperation(string name, out McpTool operation)
        {
            operation = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;
            return TryGetTool(name, out operation) || _internalOperations.TryGetValue(name, out operation);
        }

        public static List<McpTool> GetTools()
        {
            return ToolCache.GetTools(_tools.Values);
        }

        public static List<McpTool> GetVisibleTools()
        {
            return ToolCache.GetVisibleTools(_tools.Values, _tools.Values.Where(t => !t.Hidden));
        }

        public static bool TryGetTool(string name, out McpTool tool)
        {
            tool = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (_tools.TryGetValue(name, out tool))
                return true;

            string canonicalName;
            return _aliases.TryGetValue(name, out canonicalName) && _tools.TryGetValue(canonicalName, out tool);
        }

        /// <summary>
        /// Tool のメタデータを取得する（既定では tools/list でトークン消費の少ない公開エントリーポイントを提供する）
        /// </summary>
        public static List<McpToolInfo> GetToolInfos(bool includeAll = false)
        {
            var cached = includeAll ? _cachedAllToolInfos : _cachedCoreToolInfos;
            if (cached != null)
                return McpToolInfoLocalization.Localize(cached, McpLocalization.CurrentLanguage);

            BuildToolInfoCache();
            cached = includeAll ? _cachedAllToolInfos : _cachedCoreToolInfos;
            if (cached != null)
                return McpToolInfoLocalization.Localize(cached, McpLocalization.CurrentLanguage);

            return McpToolInfoLocalization.Localize(BuildToolInfos(includeAll), McpLocalization.CurrentLanguage);
        }

        private static void BuildToolInfoCache()
        {
            ToolCache.Ensure(_tools.Values);
            _cachedCoreToolInfos = BuildToolInfos(includeAll: false);
            _cachedAllToolInfos = BuildToolInfos(includeAll: true);
        }

        private static List<McpToolInfo> BuildToolInfos(bool includeAll)
        {
            var infos = new List<McpToolInfo>();
            ToolCache.Ensure(_tools.Values);
            foreach (var tool in ToolCache.GetVisibleSnapshot()
                .Where(tool => !tool.Hidden && (includeAll || DefaultPublicToolNames.Contains(tool.Name))))
            {
                var properties = new Dictionary<string, SchemaProperty>();
                var required = new List<string>();

                if (tool.Parameters != null)
                {
                    foreach (var param in tool.Parameters)
                    {
                        if (param.Key == ToolCallMiddleware.TaskDescriptionParameter)
                            continue;

                        if (IsCoordinateParameter(param.Key) && !IsCoordinateTool(tool.Name))
                            continue;

                        properties[param.Key] = new SchemaProperty
                        {
                            Type = param.Value.Type,
                            Description = param.Value.Description,
                            Enum = param.Value.SchemaEnumValues,
                            McpHeader = param.Value.McpHeader,
                            Items = param.Value.Items
                        };
                        if (param.Value.Required)
                            required.Add(param.Key);
                    }
                }

                properties[ToolCallMiddleware.TaskDescriptionParameter] = new SchemaProperty
                {
                    Type = "string",
                    Description = "Required for every tool call: briefly describe what you are doing so it can be shown near the player's mouse in-game."
                };
                required.Add(ToolCallMiddleware.TaskDescriptionParameter);

                infos.Add(new McpToolInfo
                {
                    Name = tool.Name,
                    Description = ToolMetadata.FormatDescription(tool),
                    Execution = new ToolExecution { TaskSupport = "optional" },
                    InputSchema = new InputSchema
                    {
                        Properties = properties,
                        Required = required.Count > 0 ? required : null
                    }
                });
            }
            return infos;
        }

        public static int GetDefaultToolInfoCount()
        {
            return _cachedCoreToolInfos?.Count ?? _tools.Values.Count(tool => DefaultPublicToolNames.Contains(tool.Name));
        }

        /// <summary>
        /// 指定した Tool を呼び出す
        /// </summary>
        public static CallToolResult CallTool(string name, JObject arguments)
        {
            return CallToolCore(name, arguments, false);
        }

        internal static CallToolResult CallToolFromWorldEditor(string name, JObject arguments, bool allowValidatedCoordinates)
        {
            if (string.IsNullOrWhiteSpace(name))
                return CallToolResult.Error(McpLocalization.Get("errors.operation_name_required", "Operation name is required."));
            if (_tools.ContainsKey(name) || _aliases.ContainsKey(name))
                return CallToolCore(name, arguments, allowValidatedCoordinates);
            if (!_internalOperations.TryGetValue(name, out var operation))
                return CallToolResult.Error(McpLocalization.Format("errors.operation_not_found", "Operation not found: {0}", name));
            return CallOperationCore(operation, arguments, allowValidatedCoordinates);
        }

        private static CallToolResult CallOperationCore(McpTool operation, JObject arguments, bool allowValidatedCoordinates)
        {
            var notifications = ToolCallMiddleware.DrainNotifications();
            try
            {
                if (!ToolCallMiddleware.TryGetTaskDescription(arguments, out var taskDescription))
                    return ToolCallMiddleware.MissingTaskDescription(operation.Name, notifications);
                ToolCallMiddleware.PresentTaskDescription(taskDescription);
                if (!allowValidatedCoordinates && operation.Name != "coordinate_control" && ContainsCoordinateArguments(arguments))
                    return ToolCallMiddleware.Inject(CallToolResult.Error(McpLocalization.Get("errors.validated_coordinates", "Raw coordinate arguments require a validated world-editor semantic command.")), notifications);
                return ToolCallMiddleware.Inject(operation.Handler(arguments ?? new JObject()), notifications);
            }
            catch (Exception ex)
            {
                return ToolCallMiddleware.Inject(CallToolResult.Error(McpLocalization.Format("errors.operation_execution", "Internal operation error: {0}", ex.Message)), notifications);
            }
        }

        internal static bool HasCoordinateArguments(JToken token)
        {
            return ContainsCoordinateArguments(token);
        }

        private static CallToolResult CallToolCore(string name, JObject arguments, bool allowValidatedCoordinates)
        {
            var middlewareNotifications = ToolCallMiddleware.DrainNotifications();
            if (string.IsNullOrWhiteSpace(name))
                return ToolCallMiddleware.Inject(CallToolResult.Error(McpLocalization.Get("errors.tool_name_required", "Tool name is required.")), middlewareNotifications);
            bool usedLegacyAlias = false;
            if (!_tools.TryGetValue(name, out var tool))
            {
                if (!_aliases.TryGetValue(name, out var canonicalName) || !_tools.TryGetValue(canonicalName, out tool))
                    return ToolCallMiddleware.Inject(CallToolResult.Error(McpLocalization.Format("errors.tool_not_found", "Tool not found: {0}", name)), middlewareNotifications);
                usedLegacyAlias = true;
            }

            try
            {
                if (!ToolCallMiddleware.TryGetTaskDescription(arguments, out var taskDescription))
                {
                    var missingTaskResult = ToolCallMiddleware.MissingTaskDescription(tool.Name, middlewareNotifications);
                    return usedLegacyAlias
                        ? ToolMetadata.AddLegacyAliasDeprecationWarning(missingTaskResult, name, tool.Name)
                        : missingTaskResult;
                }

                ToolCallMiddleware.PresentTaskDescription(taskDescription);

                if (!allowValidatedCoordinates && !IsCoordinateTool(tool.Name) && ContainsCoordinateArguments(arguments))
                {
                    var coordinateResult = ToolCallMiddleware.Inject(CallToolResult.Error(McpLocalization.Get("errors.coordinates", "Coordinate arguments are only supported by coordinate_control; use semantic query/target/areaId inputs for this tool.")), middlewareNotifications);
                    return usedLegacyAlias
                        ? ToolMetadata.AddLegacyAliasDeprecationWarning(coordinateResult, name, tool.Name)
                        : coordinateResult;
                }

                var result = ToolCallMiddleware.Inject(tool.Handler(arguments ?? new JObject()), middlewareNotifications);
                return usedLegacyAlias
                    ? ToolMetadata.AddLegacyAliasDeprecationWarning(result, name, tool.Name)
                    : result;
            }
            catch (Exception ex)
            {
                var errorResult = ToolCallMiddleware.Inject(CallToolResult.Error(McpLocalization.Format("errors.tool_execution", "Tool execution error: {0}", ex.Message)), middlewareNotifications);
                return usedLegacyAlias
                    ? ToolMetadata.AddLegacyAliasDeprecationWarning(errorResult, name, tool.Name)
                    : errorResult;
            }
        }

        internal static bool IsCoordinateTool(string name)
        {
            return string.Equals(name, "coordinate_control", StringComparison.Ordinal)
                || string.Equals(name, "world_editor", StringComparison.Ordinal);
        }

        internal static bool IsCoordinateParameter(string name)
        {
            switch (name)
            {
                case "x":
                case "y":
                case "x1":
                case "y1":
                case "x2":
                case "y2":
                case "dx":
                case "dy":
                case "cell":
                case "cells":
                case "points":
                case "anchors":
                    return true;
                default:
                    return false;
            }
        }

        private static bool ContainsCoordinateArguments(JToken token)
        {
            if (token == null)
                return false;

            if (token.Type == JTokenType.Object)
            {
                foreach (var property in ((JObject)token).Properties())
                {
                    if (IsCoordinateParameter(property.Name) || ContainsCoordinateArguments(property.Value))
                        return true;
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (var item in (JArray)token)
                {
                    if (ContainsCoordinateArguments(item))
                        return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Tool の定義
    /// </summary>
    public class McpTool
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Group { get; set; }
        public string Mode { get; set; }
        public string Risk { get; set; }
        public bool Hidden { get; set; }
        public List<string> Aliases { get; set; }
        public List<string> Tags { get; set; }
        public Dictionary<string, McpToolParameter> Parameters { get; set; }
        public Func<JObject, CallToolResult> Handler { get; set; }
    }

    public class McpToolParameter
    {
        public string Type { get; set; }
        public string Description { get; set; }
        public bool Required { get; set; }
        public List<string> EnumValues { get; set; }
        public string McpHeader { get; set; }
        public SchemaProperty Items { get; set; }

        public List<object> SchemaEnumValues
        {
            get
            {
                if (EnumValues == null)
                    return null;

                var values = new List<object>();
                foreach (var value in EnumValues)
                {
                    if (Type == "integer")
                    {
                        int intValue;
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out intValue))
                        {
                            values.Add(intValue);
                            continue;
                        }
                    }
                    else if (Type == "number")
                    {
                        double numberValue;
                        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out numberValue))
                        {
                            values.Add(numberValue);
                            continue;
                        }
                    }

                    values.Add(value);
                }

                return values;
            }
        }
    }
}
