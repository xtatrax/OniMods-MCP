using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class BuildPlanningTools
    {
        private static Dictionary<string, object> PreviewDiscoveryUnknown(string prefabId, int x, int y, string reason)
        {
            return ErrorResult(prefabId, x, y, OniMcp.Support.McpLocalization.Get("placement_candidates.discovery_unknown"),
                new Dictionary<string, object>
                {
                    ["reasonCode"] = reason, ["assessmentState"] = "unknown",
                    ["scope"] = "request_local_existing_object_snapshot"
                });
        }

        // One synchronous candidate window only. Never shared with execution or another request.
        private sealed class PlacementPreviewReadContext
        {
            private readonly Game game;
            private readonly BuildingDef definition;
            private readonly JObject arguments;
            private readonly int worldId;
            private readonly bool freeBuild;
            private BuildingAvailabilityReader.Observation availability;
            private MaterialSelection material;
            private FacadeSelection facade;
            private bool facadeRead;
            private List<BuildingComplete> completed;
            private List<Constructable> constructables;
            internal string DiscoveryError { get; private set; }
            internal string MaterialReadinessReason { get; private set; }

            internal PlacementPreviewReadContext(BuildingDef definition, JObject args, int worldId)
            {
                game = Game.Instance;
                this.definition = definition;
                arguments = (JObject)args.DeepClone();
                this.worldId = worldId;
                freeBuild = IsFreeBuildContext();
            }

            internal bool Matches(BuildingDef def, JObject args)
            {
                return args != null && IsDryRun(args) && ReferenceEquals(game, Game.Instance)
                    && ReferenceEquals(definition, def) && worldId == ToolUtil.ResolveWorldId(args)
                    && freeBuild == IsFreeBuildContext() && JToken.DeepEquals(arguments, args);
            }

            internal BuildingAvailabilityReader.Observation Availability()
            {
                if (availability == null) availability = BuildingAvailabilityReader.Read(definition);
                return availability;
            }

            internal MaterialSelection Material()
            {
                if (material == null)
                {
                    MaterialReadinessReason = ObserveMaterialReadiness();
                    material = MaterialReadinessReason == null
                        ? SelectElements(definition, arguments["material"]?.ToString(), worldId)
                        : MaterialSelection.Invalid(OniMcp.Support.McpLocalization.Format(
                            "placement_candidates.material_inventory_unknown", null, MaterialReadinessReason),
                            arguments["material"]?.ToString(), new List<BuildMaterialInfo>(), new List<BuildMaterialInfo>());
                    material.RequiredKg = RequiredMaterialKg(definition);
                }
                return material.CloneForPreview();
            }

            private string ObserveMaterialReadiness()
            {
                if (freeBuild) return null;
                try
                {
                    var manager = ClusterManager.Instance;
                    if (manager == null) return "cluster_manager_missing";
                    var world = manager.GetWorld(worldId >= 0 ? worldId : manager.activeWorldId);
                    if (world == null) return "world_missing";
                    if (world.worldInventory == null) return "world_inventory_missing";
                    return null;
                }
                catch { return "world_inventory_read_failed"; }
            }

            internal FacadeSelection Facade()
            {
                if (!facadeRead)
                {
                    facade = ResolveFacade(definition, arguments["facade"]?.ToString() ?? arguments["facadeId"]?.ToString());
                    facadeRead = true;
                }
                return facade;
            }

            internal IEnumerable<BuildingComplete> CompletedBuildings()
            {
                if (completed == null)
                {
                    try { completed = Components.BuildingCompletes.Items.ToList(); }
                    catch { DiscoveryError = "completed_snapshot_failed"; completed = new List<BuildingComplete>(); }
                }
                return completed;
            }

            internal IEnumerable<GameObject> CompletedObjects()
            {
                return CompletedBuildings().Select(value => value?.gameObject);
            }

            internal IEnumerable<Constructable> Constructables()
            {
                if (constructables == null)
                {
                    try
                    {
                        constructables = UnityEngine.Object.FindObjectsByType<Constructable>(FindObjectsSortMode.None)
                            .Where(value => value != null && value.gameObject != null
                                && ToolUtil.GameObjectMatchesWorld(value.gameObject, worldId)).ToList();
                    }
                    catch { DiscoveryError = "constructable_snapshot_failed"; constructables = new List<Constructable>(); }
                }
                return constructables;
            }
        }
    }
}
