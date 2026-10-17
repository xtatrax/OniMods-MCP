using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using OniMcp.Core;
using UnityEngine;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class OrdersTools
{
        public static McpTool SetBuildingEnabled()
        {
            return new McpTool
            {
                Name = "buildings_set_enabled",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "building_enable", "building_disable" },
                Hidden = true,
                Description = "互換用エントリー。building_control domain=config action=set_enabled を使用すること。指定した設備を有効または無効にする。ゲーム本来のメニュー処理で複製人間の切替作業を予約・取消する。enabled は現在値、requestedEnabled は予約後の目標値",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["enabled"] = new McpToolParameter { Type = "boolean", Description = "true で有効、false で無効", Required = true }
                }),
                Handler = args =>
                {
                    if (ToolUtil.GetBool(args, "dryRun", false))
                        return CallToolResult.Error("dryRun is not supported; read config state without executing instead");
                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target not found");
                    var button = go.GetComponent<BuildingEnabledButton>();
                    if (button == null)
                        return CallToolResult.Error("Target does not support enabled/disabled state");

                    if (args["enabled"] == null)
                        return CallToolResult.Error("enabled is required");
                    try
                    {
                        var state = NativeBuildingToggle.Set(button, ToolUtil.GetBool(args, "enabled", true), DebugHandler.InstantBuildMode);
                        state["id"] = go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID();
                        state["name"] = ToolUtil.CleanName(go.GetProperName());
                        return CallToolResult.Text(JsonConvert.SerializeObject(state, McpJsonUtil.Settings));
                    }
                    catch (Exception ex)
                    {
                        return CallToolResult.Error("Native building toggle failed: " + ex.GetBaseException().Message);
                    }
                }
            };
        }

        public static McpTool SetBuildingToggle()
        {
            return new McpTool
            {
                Name = "buildings_set_toggle",
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "logic_switch_set", "building_toggle_set" },
                Hidden = true,
                Description = "互換用エントリー。building_control domain=config action=set_toggle を使用すること。論理スイッチなど、プレイヤーによる手動切り替えに対応する設備や自動化スイッチの状態を設定する。対象が IPlayerControlledToggle を実装している場合のみ使用できる。id または x/y を明示し、対象のワールドをアクティブにして実行する。通常の詳細画面ボタン経由で変更し、一時停止中の予約・取消を本体へ委譲する。操作時は詳細画面の対象が変わる場合がある。デュプ作業は不要",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["on"] = new McpToolParameter { Type = "boolean", Description = "true でオン、false でオフ", Required = true }
                }),
                Handler = args =>
                {
                    if (ToolUtil.GetBool(args, "dryRun", false))
                        return CallToolResult.Error("dryRun is not supported; read config state without executing instead");
                    int? requestedId = ToolUtil.GetInt(args, "id");
                    int? requestedX = ToolUtil.GetInt(args, "x");
                    int? requestedY = ToolUtil.GetInt(args, "y");
                    string selectorError = NativePlayerToggleUi.ValidateSelectors(requestedId, requestedX, requestedY);
                    if (selectorError != null)
                        return CallToolResult.Error(selectorError);
                    var go = FindTarget(args);
                    int? requestedWorld = requestedX.HasValue ? ToolUtil.ResolveWorldId(args) : ToolUtil.GetInt(args, "worldId");
                    string query = args["query"]?.ToString()?.Trim();
                    string targetError = NativePlayerToggleUi.ValidateTarget(go, requestedId, requestedX, requestedY,
                        requestedWorld, query, MatchesQuery);
                    if (targetError != null)
                        return CallToolResult.Error(targetError);
                    var toggle = go.GetComponents<Component>().OfType<IPlayerControlledToggle>().FirstOrDefault();
                    if (toggle == null)
                        return CallToolResult.Error("Target does not expose a player-controlled toggle");

                    if (args["on"] == null)
                        return CallToolResult.Error("on is required");
                    if (SpeedControlScreen.Instance == null)
                        return CallToolResult.Error("Native speed control is not ready");
                    bool desired = ToolUtil.GetBool(args, "on", true);
                    bool before = toggle.ToggledOn();
                    bool beforePending = toggle.ToggleRequested;
                    bool paused = SpeedControlScreen.Instance.IsPaused;
                    bool uiRefreshAttempted = false;
                    bool actionAttempted = false;
                    bool uiRetargeted = false;
                    var beforeUiTarget = DetailsScreen.Instance != null ? DetailsScreen.Instance.target : null;
                    var beforeUiSelection = SelectTool.Instance != null ? SelectTool.Instance.selected : null;
                    try
                    {
                        bool clicked = NativeBuildingToggle.SetPlayerToggle(before, beforePending, desired, paused, () =>
                        {
                            uiRetargeted = NativePlayerToggleUi.Click(go, toggle, () => actionAttempted = true,
                                () => uiRefreshAttempted = true);
                        });
                        bool actual = toggle.ToggledOn();
                        bool pending = toggle.ToggleRequested;
                        NativeBuildingToggle.RequirePlayerToggleAccepted(desired, paused, actual, pending);
                        return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                        {
                            ["id"] = go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID(),
                            ["name"] = ToolUtil.CleanName(go.GetProperName()),
                            ["before"] = before,
                            ["beforeToggleRequested"] = beforePending,
                            ["on"] = actual,
                            ["desiredOn"] = desired,
                            ["requestedOn"] = pending ? !actual : actual,
                            ["toggleRequested"] = pending,
                            ["requiresDuplicantWork"] = false,
                            ["completed"] = actual == desired && !pending,
                            ["changed"] = clicked,
                            ["actionAttempted"] = actionAttempted,
                            ["uiRetargeted"] = uiRetargeted,
                            ["executionPath"] = "PlayerControlledToggleSideScreen.toggleButton.SignalClick",
                            ["sideScreenTitleKey"] = toggle.SideScreenTitleKey
                        }, McpJsonUtil.Settings));
                    }
                    catch (Exception ex)
                    {
                        string error = "Native player toggle failed: " + ex.GetBaseException().Message;
                        if (!uiRefreshAttempted)
                            return CallToolResult.Error(error);
                        var result = NativeBuildingToggle.ReadPlayerToggleObservation(desired,
                            () => toggle.ToggledOn(), () => toggle.ToggleRequested);
                        bool? observedUiRetargeted = null;
                        try { observedUiRetargeted = DetailsScreen.Instance != null && SelectTool.Instance != null
                            ? DetailsScreen.Instance.target != beforeUiTarget || SelectTool.Instance.selected != beforeUiSelection
                            : (bool?)null; } catch { }
                        result["error"] = error;
                        result["before"] = before;
                        result["beforeToggleRequested"] = beforePending;
                        result["desiredOn"] = desired;
                        result["actionAttempted"] = actionAttempted;
                        result["uiRefreshAttempted"] = uiRefreshAttempted;
                        result["uiInitializing"] = ex is NativePlayerToggleUi.InitializationPendingException;
                        result["uiRetargeted"] = observedUiRetargeted;
                        result["executionPath"] = "PlayerControlledToggleSideScreen.toggleButton.SignalClick";
                        result["next"] = "Native UI work may already be accepted. Read config state before retrying; unavailable observations are null.";
                        return CallToolResult.Error(JsonConvert.SerializeObject(result, McpJsonUtil.Settings));
                    }
                }
            };
        }

        public static McpTool ConfigureManualDelivery()
        {
            return new McpTool
            {
                Name = "buildings_manual_delivery",
                Hidden = true,
                Group = "buildings",
                Mode = "write",
                Risk = "medium",
                Aliases = new List<string> { "manual_delivery_set", "building_refill_set" },
                Description = "互換用エントリー。orders_control domain=designation action=manual_delivery を優先して使用すること。設備への手動補給や運搬を設定する。補給の一時停止や再開、容量や補給しきい値の設定、即時の運搬要求に対応する",
                Parameters = LookupParams(new Dictionary<string, McpToolParameter>
                {
                    ["paused"] = new McpToolParameter { Type = "boolean", Description = "任意。true で手動補給を一時停止、false で再開", Required = false },
                    ["capacityKg"] = new McpToolParameter { Type = "number", Description = "任意。目標貯蔵量の上限 kg", Required = false },
                    ["refillMassKg"] = new McpToolParameter { Type = "number", Description = "任意。この質量を下回ると補給を要求する kg", Required = false },
                    ["minimumMassKg"] = new McpToolParameter { Type = "number", Description = "任意。1 回の運搬の最小質量 kg", Required = false },
                    ["requestNow"] = new McpToolParameter { Type = "boolean", Description = "運搬を直ちに 1 回要求するか。既定値 false", Required = false }
                }),
                Handler = args =>
                {
                    var go = FindTarget(args);
                    if (go == null)
                        return CallToolResult.Error("Target not found");
                    var delivery = go.GetComponent<ManualDeliveryKG>();
                    if (delivery == null)
                        return CallToolResult.Error("Target does not support manual delivery");

                    if (args["paused"] != null)
                        delivery.Pause(ToolUtil.GetBool(args, "paused", false), "Oni MCP manual delivery setting");
                    float? capacity = ToolUtil.GetFloat(args, "capacityKg");
                    if (capacity.HasValue)
                        delivery.capacity = Math.Max(0f, capacity.Value);
                    float? refill = ToolUtil.GetFloat(args, "refillMassKg");
                    if (refill.HasValue)
                        delivery.refillMass = Math.Max(0f, refill.Value);
                    float? minimum = ToolUtil.GetFloat(args, "minimumMassKg");
                    if (minimum.HasValue)
                        delivery.MinimumMass = Math.Max(0f, minimum.Value);
                    if (ToolUtil.GetBool(args, "requestNow", false))
                        delivery.RequestDelivery();

                    delivery.UpdateDeliveryState();
                    return CallToolResult.Text(JsonConvert.SerializeObject(new Dictionary<string, object>
                    {
                        ["id"] = go.GetComponent<KPrefabID>()?.InstanceID ?? go.GetInstanceID(),
                        ["name"] = ToolUtil.CleanName(go.GetProperName()),
                        ["paused"] = delivery.IsPaused,
                        ["capacityKg"] = Math.Round(delivery.capacity, 3),
                        ["refillMassKg"] = Math.Round(delivery.refillMass, 3),
                        ["minimumMassKg"] = Math.Round(delivery.MinimumMass, 3),
                        ["requestedItemTag"] = delivery.RequestedItemTag.Name
                    }, McpJsonUtil.Settings));
                }
            };
        }

        private static Dictionary<string, object> ObjectResult(GameObject go, string status)
        {
            int cell = Grid.PosToCell(go);
            var kpid = go.GetComponent<KPrefabID>();
            return new Dictionary<string, object>
            {
                ["status"] = status,
                ["id"] = kpid?.InstanceID ?? go.GetInstanceID(),
                ["prefabId"] = kpid?.PrefabTag.Name ?? go.name,
                ["name"] = ToolUtil.CleanName(go.GetProperName()),
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1
            };
        }

        private static Dictionary<string, object> CellResult(int cell, string status)
        {
            return new Dictionary<string, object>
            {
                ["status"] = status,
                ["x"] = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : -1,
                ["y"] = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : -1,
                ["element"] = Grid.IsValidCell(cell) ? Grid.Element[cell].id.ToString() : "",
                ["massKg"] = Grid.IsValidCell(cell) ? Math.Round(Grid.Mass[cell], 3) : 0
            };
        }
}
}
