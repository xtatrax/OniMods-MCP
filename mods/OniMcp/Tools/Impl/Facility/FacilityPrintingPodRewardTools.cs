using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class FacilitySideScreenTools
    {
        private const int PrintingPodRewardPriority = 7;

        private static GameObject FindTelepadTarget(JObject args)
        {
            var explicitTarget = FindBuildingTarget(args, target => target.GetComponent<Telepad>() != null);
            if (explicitTarget != null)
                return explicitTarget;

            bool constrained = ToolUtil.GetInt(args, "id").HasValue
                || ToolUtil.GetInt(args, "x").HasValue
                || ToolUtil.GetInt(args, "y").HasValue
                || !string.IsNullOrWhiteSpace(args["query"]?.ToString())
                || !string.IsNullOrWhiteSpace(args["target"]?.ToString())
                || !string.IsNullOrWhiteSpace(args["name"]?.ToString())
                || HasRectInput(args);
            if (constrained)
                return null;

            int worldId = ToolUtil.GetInt(args, "worldId") ?? (ClusterManager.Instance?.activeWorldId ?? -1);
            var telepad = Components.Telepads.Items
                .FirstOrDefault(item => item != null && (worldId < 0 || item.gameObject.GetMyWorldId() == worldId))
                ?? Components.Telepads.Items.FirstOrDefault(item => item != null);
            return telepad == null ? null : telepad.gameObject;
        }

        private static CallToolResult ClaimPrintingReward(JObject args, Telepad telepad, Dictionary<string, object> before)
        {
            if (Immigration.Instance == null)
                return CallToolResult.Error("Immigration system not available");

            // Closed screens can retain old containers. Never treat those as a new offer.
            if (!Immigration.Instance.ImmigrantsAvailable)
                return CallToolResult.Error("No printing pod rewards available right now");

            var rewards = CurrentCarePackages().ToList();
            bool initializedScreenForClaim = false;
            if (rewards.Count == 0)
            {
                return CallToolResult.Error("No current claimable care-package reward is materialized yet. This tool no longer opens the immigrant screen automatically; wait for the reward list to appear or use action=open_immigrants manually.");
            }

            var selected = ResolvePrintingReward(args, rewards);
            if (selected == null)
                return CallToolResult.Error("No current claimable care-package reward matched. If the printing pod offers duplicants, use action=open_immigrants for manual UI selection.");

            var reward = CarePackageInfoDictionary(selected, rewards.IndexOf(selected));
            var priorityPlan = PrintingRewardPriorityPlan(telepad, reward);
            if (ToolUtil.GetBool(args, "dryRun", false))
            {
                return JsonResult(new Dictionary<string, object>
                {
                    ["dryRun"] = true,
                    ["before"] = before,
                    ["selectedReward"] = reward,
                    ["priorityPlan"] = priorityPlan,
                    ["printingRewards"] = PrintingRewardStatus(telepad),
                    ["initializedScreenForClaim"] = initializedScreenForClaim
                });
            }

            if (!ToolUtil.GetBool(args, "confirm", false))
                return CallToolResult.Error("confirm=true required to claim printing pod care package");

            var screen = ImmigrantScreen.instance;
            if (screen == null || screen.Telepad != telepad)
                return CallToolResult.Error("Open the target printing pod's immigrant screen before claiming a care package.");
            var selectedField = typeof(CharacterSelectionController).GetField("selectedDeliverables",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var containersField = typeof(CharacterSelectionController).GetField("containers",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var proceed = typeof(ImmigrantScreen).GetMethod("OnProceed",
                BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            var selectedDeliverables = selectedField?.GetValue(screen) as List<ITelepadDeliverable>;
            if (selectedDeliverables == null || containersField == null || proceed == null)
                return CallToolResult.Error("Native printing pod selection/cleanup is unavailable; use the game UI.");

            // Use the native confirmation path. Direct delivery + Show(false) leaves
            // controller containers alive, so InitializeContainers reuses stale offers.
            // Replace any existing UI selection with this one verified care package.
            foreach (var previous in selectedDeliverables.ToArray())
                screen.RemoveDeliverable(previous);
            screen.AddDeliverable(selected);
            if (!screen.IsSelected(selected))
                return CallToolResult.Error("The native printing pod screen did not accept the selected care package.");
            proceed.Invoke(screen, null);
            int remainingContainers = (containersField.GetValue(screen) as System.Collections.ICollection)?.Count ?? -1;
            bool screenClosed = !screen.gameObject.activeSelf;
            return JsonResult(new Dictionary<string, object>
            {
                ["claimed"] = true,
                ["before"] = before,
                ["after"] = TelepadInfo(telepad, includeVictory: false),
                ["selectedReward"] = reward,
                ["priorityAction"] = priorityPlan["priorityAction"],
                ["nextActions"] = priorityPlan["nextActions"],
                ["printingRewards"] = PrintingRewardStatus(telepad),
                ["initializedScreenForClaim"] = initializedScreenForClaim,
                ["screenClosed"] = screenClosed,
                ["deliveryPath"] = "native_immigrant_screen",
                ["remainingScreenContainers"] = remainingContainers,
                ["screenCleanupConfirmed"] = remainingContainers == 0
            });
        }

        private static CallToolResult RejectPrintingRewards(JObject args, Telepad telepad, Dictionary<string, object> before)
        {
            if (Immigration.Instance == null || !Immigration.Instance.ImmigrantsAvailable)
                return CallToolResult.Error("No printing pod rewards available right now");
            var screen = ImmigrantScreen.instance;
            if (screen == null || screen.Telepad != telepad || !screen.gameObject.activeSelf)
                return CallToolResult.Error("Open the target printing pod's immigrant screen before rejecting all offers.");
            var reject = typeof(ImmigrantScreen).GetMethod("OnRejectionConfirmed",
                BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            var containers = typeof(CharacterSelectionController).GetField("containers",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (reject == null || containers?.GetValue(screen) == null)
                return CallToolResult.Error("Native printing pod rejection/cleanup is unavailable; use the game UI.");
            if (ToolUtil.GetBool(args, "dryRun", false))
                return JsonResult(new Dictionary<string, object> { ["dryRun"] = true, ["action"] = "reject_all", ["before"] = before });
            if (!ToolUtil.GetBool(args, "confirm", false))
                return CallToolResult.Error("confirm=true required to reject all printing pod offers");

            // The same confirmation path as the native Reject All dialog: reject once,
            // destroy all candidate containers, close both screens, and restore audio.
            reject.Invoke(screen, null);
            int remaining = (containers.GetValue(screen) as System.Collections.ICollection)?.Count ?? -1;
            return JsonResult(new Dictionary<string, object>
            {
                ["rejectedAll"] = !Immigration.Instance.ImmigrantsAvailable,
                ["before"] = before,
                ["after"] = TelepadInfo(telepad, includeVictory: false),
                ["printingRewards"] = PrintingRewardStatus(telepad),
                ["deliveryPath"] = "native_immigrant_screen_rejection",
                ["screenClosed"] = !screen.gameObject.activeSelf,
                ["remainingScreenContainers"] = remaining,
                ["screenCleanupConfirmed"] = remaining == 0
            });
        }

        private static Dictionary<string, object> PrintingRewardStatus(Telepad telepad)
        {
            var immigration = Immigration.Instance;
            bool available = immigration != null && immigration.ImmigrantsAvailable;
            var rewards = (available ? CurrentCarePackages() : Enumerable.Empty<CarePackageInfo>())
                .Select((item, index) => CarePackageInfoDictionary(item, index))
                .ToList();

            var result = new Dictionary<string, object>
            {
                ["available"] = available,
                ["timeRemainingSeconds"] = immigration == null ? (object)null : Math.Round(ToolUtil.SafeFloat(immigration.GetTimeRemaining()), 1),
                ["timeRemainingCycles"] = immigration == null ? (object)null : Math.Round(ToolUtil.SafeFloat(immigration.GetTimeRemaining() / 600f), 3),
                ["rewardCount"] = rewards.Count,
                ["rewards"] = rewards,
                ["claimSupport"] = rewards.Count > 0 ? "care_package_only" : "none_currently_safe",
                ["dupeClaimSupport"] = "open_immigrants UI only; automatic duplicant selection is intentionally not claimed by action=claim",
                ["notes"] = rewards.Count > 0
                    ? "Only currently materialized care-package choices are listed."
                    : "No current care-package containers are materialized. This tool will not open or initialize the immigrant screen during list/status or claim."
            };
            var policy = OniMcp.Config.PrintingPodSelectionPolicy.Describe(
                OniMcp.Config.OniMcpOptions.Current.RequirePrintingPodSelection,
                immigration == null ? (bool?)null : available);
            if (policy != null)
                result["selectionPolicy"] = policy;
            return result;
        }

        private static CarePackageInfo ResolvePrintingReward(JObject args, List<CarePackageInfo> rewards)
        {
            if (rewards == null || rewards.Count == 0)
                return null;

            int? rewardIndex = ToolUtil.GetInt(args, "rewardIndex") ?? ToolUtil.GetInt(args, "itemIndex");
            if (rewardIndex.HasValue)
                return rewardIndex.Value >= 0 && rewardIndex.Value < rewards.Count ? rewards[rewardIndex.Value] : null;

            string query = FirstNonEmpty(args["itemId"], args["query"], args["target"], args["name"]);
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (var reward in rewards)
                {
                    var info = CarePackageInfoDictionary(reward, rewards.IndexOf(reward));
                    if (MatchesQuery(info, query))
                        return reward;
                }

                return null;
            }

            return rewards[0];
        }

        private static IEnumerable<CarePackageInfo> CurrentCarePackages()
        {
            var field = typeof(CarePackageContainer).GetField("containers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            var value = field == null ? null : field.GetValue(null) as IEnumerable<ITelepadDeliverableContainer>;
            if (value == null)
                return new List<CarePackageInfo>();

            return value
                .OfType<CarePackageContainer>()
                .Where(container => container != null && container.Info != null)
                .Select(container => container.Info)
                .ToList();
        }

        private static Dictionary<string, object> CarePackageInfoDictionary(CarePackageInfo info, int index)
        {
            var prefab = info == null || string.IsNullOrWhiteSpace(info.id) ? null : Assets.GetPrefab(info.id);
            return new Dictionary<string, object>
            {
                ["index"] = index,
                ["kind"] = "care_package",
                ["id"] = info?.id,
                ["prefabId"] = info?.id,
                ["name"] = prefab == null ? info?.id : ToolUtil.CleanName(prefab.GetProperName()),
                ["quantity"] = info == null ? (object)null : Math.Round(ToolUtil.SafeFloat(info.quantity), 3),
                ["facadeId"] = info?.facadeID,
                ["requirementMet"] = info?.requirement == null ? (object)null : SafeRequirement(info.requirement),
                ["claimable"] = true
            };
        }

        private static string FirstNonEmpty(params JToken[] values)
        {
            foreach (var value in values)
            {
                string text = value?.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }

            return null;
        }

        private static bool SafeRequirement(Func<bool> requirement)
        {
            try
            {
                return requirement == null || requirement();
            }
            catch
            {
                return false;
            }
        }

        private static Dictionary<string, object> PrintingRewardPriorityPlan(Telepad telepad, Dictionary<string, object> reward)
        {
            int cell = telepad == null ? -1 : Grid.PosToCell(telepad);
            int x = Grid.IsValidCell(cell) ? Grid.CellColumn(cell) : 0;
            int y = Grid.IsValidCell(cell) ? Grid.CellRow(cell) : 0;
            int worldId = telepad == null ? (ClusterManager.Instance?.activeWorldId ?? 0) : telepad.gameObject.GetMyWorldId();
            var sweepArgs = RewardAreaArgs("sweep", x, y, worldId);
            var priorityArgs = RewardAreaArgs("set_area", x, y, worldId);
            priorityArgs["domain"] = "priority";

            return new Dictionary<string, object>
            {
                ["priority"] = PrintingPodRewardPriority,
                ["reason"] = "printing_pod_reward_should_be_swept_before_survival_builds_depend_on_it",
                ["reward"] = reward,
                ["priorityAction"] = new Dictionary<string, object>
                {
                    ["tool"] = "orders_control",
                    ["arguments"] = priorityArgs
                },
                ["nextActions"] = new[]
                {
                    new Dictionary<string, object>
                    {
                        ["kind"] = "sweep_reward",
                        ["tool"] = "orders_control",
                        ["arguments"] = sweepArgs
                    },
                    new Dictionary<string, object>
                    {
                        ["kind"] = "set_reward_area_priority",
                        ["tool"] = "orders_control",
                        ["arguments"] = priorityArgs
                    }
                }
            };
        }

        private static Dictionary<string, object> RewardAreaArgs(string action, int x, int y, int worldId)
        {
            return new Dictionary<string, object>
            {
                ["domain"] = "area",
                ["action"] = action,
                ["x1"] = Math.Max(0, x - 1),
                ["y1"] = Math.Max(0, y - 1),
                ["x2"] = x + 1,
                ["y2"] = y + 1,
                ["worldId"] = worldId,
                ["priority"] = PrintingPodRewardPriority,
                ["dryRun"] = true,
                ["detail"] = true
            };
        }
    }
}
