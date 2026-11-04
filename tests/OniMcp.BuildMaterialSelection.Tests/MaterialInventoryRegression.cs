using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class MaterialInventoryRegression
{
    private static int checks, cases;
    internal static void Run()
    {
        var previous = McpLocalization.LanguageProvider;
        McpLocalization.LanguageProvider = () => "en";
        try
        {
            // Keep this first: exact old production source compiles against the
            // same fixtures, then fails 1670 versus 835 before new DTO checks.
            Case("registered floor mass is already in native total", () => {
                var d = Setup(835f);
                Boundary.Pickup("MatA", 30f, false, false, 2);
                Boundary.Pickup("MatA", 5f, false, false, 2);
                Boundary.Pickup("MatA", 800f, false, false, 2);
                return d;
            }, r => {
                Equal(r["selectedAvailableKg"], 835f, "floor stock must not be doubled");
                Equal(r["selected"]["availableKg"], 835f, "selected stock equals UI stock");
                var n = Native(r);
                Equal(n["ownWorldTotalKg"], 835f, "native own total");
                Equal(n["ownWorldAvailableKg"], 835f, "native own available");
                Equal(n["uiAvailableKg"], 835f, "native related UI amount");
                Check((int)n["relatedWorldCount"] == 1, "one related world");
                Evidence(n);
            });
            Case("native construction demand is separate from floor mass", () => {
                var d = Setup(835f); World.materialNeeds[new Tag("MatA")] = 200f;
                Boundary.Pickup("MatA", 835f, false, false, 2); return d;
            }, r => {
                Equal(r["selectedAvailableKg"], 635f, "native demand deducted once");
                Equal(Native(r)["ownWorldTotalKg"], 835f, "gross amount retained");
                Equal(Native(r)["ownWorldAvailableKg"], 635f, "own UI available");
                Equal(Native(r)["ownWorldMaterialNeedsKg"], 200f, "cached construction demand distinct");
                Check((bool)r["satisfied"], "single-slot quantity sufficient"); Evidence(Native(r));
            });
            Case("native demand larger than total", () => {
                var d = Setup(30f); World.materialNeeds[new Tag("MatA")] = 80f; return d;
            }, r => {
                Check(!(bool)r["valid"], "zero UI stock retains usual material refusal");
                Equal(r["selectedAvailableKg"], 0f, "known native clamp zero");
                Equal(r["shortageKg"], 100f, "single slot shortage");
                Check(r["quantityStatus"].ToString() == "insufficient", "known zero is insufficient");
                Equal(Native(r)["ownWorldTotalKg"], 30f, "gross remains nonzero");
                Equal(Native(r)["ownWorldMaterialNeedsKg"], 80f, "demand not confused with clamped deduction");
            });
            Case("same-parent worlds match native material UI scope", Related, r => {
                Equal(r["selectedAvailableKg"], 480f, "80 own plus 400 related, foreign excluded");
                Equal(Native(r)["ownWorldTotalKg"], 100f, "own gross kept separate");
                Equal(Native(r)["ownWorldAvailableKg"], 80f, "own available kept separate");
                Check((int)Native(r)["relatedWorldCount"] == 2, "only same-parent worlds counted");
                Evidence(Native(r));
            });
            Case("foreign unready world does not poison native scope", () => {
                var d = Related(); ClusterManager.Instance.Worlds[2].worldInventory.HasValidCount = false; return d;
            }, r => Equal(r["selectedAvailableKg"], 480f, "unrelated readiness excluded"));
            Case("same-parent unready cache does not fabricate zero", () => {
                var d = Related(); ClusterManager.Instance.Worlds[1].worldInventory.HasValidCount = false; return d;
            }, r => {
                Unknown(r, "related_world_inventory_initializing");
                Equal(Native(r)["ownWorldTotalKg"], 100f, "own known evidence retained");
                Equal(Native(r)["ownWorldAvailableKg"], 80f, "own known free retained");
            });
            foreach (string reason in new[] { "cluster_manager_missing", "world_missing", "world_inventory_missing",
                "world_inventory_initializing", "material_needs_missing", "native_inventory_read_failed" })
            {
                string expected = reason;
                Case("unknown native state " + reason, () => {
                    var d = Setup(100f);
                    switch(expected) {
                        case "cluster_manager_missing": ClusterManager.Instance = null; break;
                        case "world_missing": ClusterManager.Instance.Worlds.Clear(); break;
                        case "world_inventory_missing": World.worldInventory = null; break;
                        case "world_inventory_initializing": World.worldInventory.HasValidCount = false; break;
                        case "material_needs_missing": World.materialNeeds = null; break;
                        case "native_inventory_read_failed": World.worldInventory.ThrowOnRead = true; break;
                    }
                    return d;
                }, r => Unknown(r, expected));
            }
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, -1f })
            {
                float captured = invalid;
                Case("invalid total is not known zero " + invalid, () => Setup(captured), r => Unknown(r, "native_quantity_invalid"));
                Case("invalid demand is not known zero " + invalid, () => {
                    var d = Setup(100f); World.materialNeeds[new Tag("MatA")] = captured; return d;
                }, r => Unknown(r, "native_material_needs_invalid"));
            }
            Case("related invalid native amount does not disappear in sum", () => {
                var d = Related(); ClusterManager.Instance.Worlds[1].worldInventory.Amounts["MatA"] = float.NaN; return d;
            }, r => Unknown(r, "related_native_quantity_invalid"));
            Case("auto all-unknown cache has unknown guidance", () => {
                var d = Setup(100f); World.worldInventory.HasValidCount = false; return d;
            }, r => {
                Check(!(bool)r["valid"] && r["inventoryState"].ToString() == "unknown", "auto unknown refused");
                Check(r["error"].ToString() == McpLocalization.Get("build_materials.inventory_unknown"), "auto unknown is not empty stock guidance");
                Check(r["selectedAvailableKg"].Type == JTokenType.Null, "auto unknown stock stays null");
            }, "auto");
            Case("multi-slot does not borrow single material total", () => {
                var d = Setup(1000f); d.Mass = new[] { 100f, 200f }; return d;
            }, r => {
                Check(r["requiredKg"].Type == JTokenType.Null && r["satisfied"].Type == JTokenType.Null,
                    "multi-slot requirements remain unevaluated");
                Check(r["quantityStatus"].ToString() == "unknown", "multi-slot remains unknown");
            });
            Case("unknown material beyond output limit stays unknown", ManyZeroAndUnknown, r => {
                Check(!(bool)r["valid"] && r["inventoryState"].ToString() == "unknown", "whole-catalog unknown kept");
                Check(r["inventoryReasonCode"].ToString() == "native_quantity_invalid", "unknown beyond first twenty retained");
                Check(((JArray)r["candidateMaterials"]).Count == 20, "candidate output remains bounded");
                Check(r["error"].ToString() == McpLocalization.Get("build_materials.inventory_unknown"), "not mistaken for all known zero");
            }, "auto");
            Listing("known positive auto excludes zero and unknown", () => {
                var d = Setup(0f); World.worldInventory.Amounts["MatB"] = 20f; World.worldInventory.Amounts["MatZ"] = float.NaN; return d;
            }, true, r => {
                Check(r["autoMaterial"].ToString() == "MatB", "auto only positive known stock");
                Check(r["inventoryState"].ToString() == "partial", "mixed inventory remains partial");
                var unknown = ((JArray)r["materials"])[2];
                Check(unknown["availableKg"].Type == JTokenType.Null && unknown["inventoryState"].ToString() == "unknown", "row unknown retained");
            });
            Listing("includeUnavailable never makes zero an auto choice", () => {
                var d = Setup(0f); Boundary.SetAllMasses(0f); return d;
            }, true, r => {
                Check(r["autoMaterial"].ToString() == "unavailable", "no zero stock auto choice");
                Check(r["autoMaterialReason"].ToString() == McpLocalization.Get("build_materials.none_available"), "known empty guidance");
                Check(r["inventoryState"].ToString() == "known", "known zero differs from missing");
            });
            Listing("hidden unknown rows retain listing state", () => {
                var d = Setup(100f); World.worldInventory.HasValidCount = false; return d;
            }, false, r => {
                Check((int)r["returned"] == 0 && r["inventoryState"].ToString() == "unknown", "no rows does not mean empty inventory");
                Check(r["autoMaterialReason"].ToString() == McpLocalization.Get("build_materials.inventory_unknown"), "unknown listing guidance");
            });
            Listing("unknown beyond listing limit still blocks empty conclusion", ManyZeroAndUnknown, true, r => {
                Check((int)r["returned"] == 20, "listing bounded");
                Check(r["inventoryState"].ToString() == "partial" && r["autoMaterial"].ToString() == "unavailable", "unknown remains even outside listed rows");
                Check(r["autoMaterialReason"].ToString() == McpLocalization.Get("build_materials.inventory_unknown"), "full list unknown guidance");
                Check(r["transportableKg"].Type == JTokenType.Null && r["transportStatus"].ToString() == "not_evaluated", "listing transport not invented");
            }, 20);
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                McpLocalization.LanguageProvider = () => language;
                Case("MCP prose independent from native name " + language, () => {
                    var d = Setup(100f); Boundary.Names["MatA"] = "native material name"; return d;
                }, r => {
                    Check(r["next"].ToString() == McpLocalization.Get("build_materials.next_sufficient"), "localized next matches runtime catalog");
                    Check(r["inventoryScope"].ToString() == McpLocalization.Get("build_materials.inventory_scope"), "localized inventory scope");
                    Check(r["quantityScope"].ToString() == McpLocalization.Get("build_materials.quantity_scope"), "localized quantity scope");
                    Check(r["selected"]["name"].ToString() == "native material name", "native name unchanged");
                    Evidence(Native(r));
                });
            }
            UnitCases();
            Console.WriteLine("Native material quantity: " + checks + " checks / " + cases + " cases PASS (real selector/reader/DTO, native inputs substituted)");
        }
        finally { McpLocalization.LanguageProvider = previous; }
    }

    internal static void RunUnitsOnly()
    {
        McpLocalization.LanguageProvider = () => "en";
        UnitCases();
        Console.WriteLine("Native material units boundary: " + checks + " checks PASS");
    }
    private static BuildingDef NonMassMaterial()
    {
        var def = Setup(200f);
        Boundary.AddMaterial(def, "Ghost", "Non-mass item", 1000000f);
        ElementLoader.Elements.Remove("Ghost");
        var kpid = new KPrefabID { PrefabTag = new Tag("Ghost") };
        kpid.Tags.Add(new Tag("Metal"));
        Assets.Prefabs[new Tag("Ghost")] = new UnityEngine.GameObject().Add(kpid);
        return def;
    }
    private static void UnitCases()
    {
        // The exact pre-unit-guard selector+reader pair compiles, then exposes
        // Ghost as kilograms in this first listing case. It must fail here.
        Listing("non-element units never enter material kg list", NonMassMaterial, true, r => {
            foreach (var row in (JArray)r["materials"])
                Check(row["tag"].ToString() != "Ghost", "non-element units must not be exposed as kg");
            Check(r["autoMaterial"].ToString() == "MatA", "non-element high count is not auto material");
        });
        Case("auto excludes non-element unit stock", NonMassMaterial,
            r => Check(r["selected"]["tag"].ToString() == "MatA", "auto selects known mass element"), "auto");
        Case("explicit non-element refused as incompatible", NonMassMaterial, r => {
            Check(!(bool)r["valid"] && r["selected"].Type == JTokenType.Null, "explicit non-element is not a selectable material");
            Check(r["selectedAvailableKg"].Type == JTokenType.Null, "no kg quantity assigned to non-element units");
        }, "Ghost");
        Boundary.Reset(); NonMassMaterial(); Boundary.InventoryReads = 0;
        var observation = NativeMaterialInventoryReader.Read(0, new Tag("Ghost"));
        Check(observation.State == "unknown" && observation.ReasonCode == "material_is_not_mass_element",
            "reader independently rejects unit-based item");
        Check(observation.UiAvailableKg == null && Boundary.InventoryReads == 0, "non-element reader does not read native unit stock");
    }
    private static WorldContainer World => ClusterManager.Instance.Worlds[0];
    private static BuildingDef Setup(float total)
    {
        var d = Boundary.BaseDefinition(); World.worldInventory.Amounts["MatA"] = total; return d;
    }
    private static BuildingDef Related()
    {
        var d = Setup(100f); World.ParentWorldId = 0; World.materialNeeds[new Tag("MatA")] = 20f;
        var related = new WorldContainer { id = 1, ParentWorldId = 0, worldInventory = new WorldInventory() };
        related.worldInventory.Amounts["MatA"] = 500f; related.materialNeeds[new Tag("MatA")] = 100f;
        var foreign = new WorldContainer { id = 2, ParentWorldId = 2, worldInventory = new WorldInventory() };
        foreign.worldInventory.Amounts["MatA"] = 9000f;
        ClusterManager.Instance.Worlds[1] = related; ClusterManager.Instance.Worlds[2] = foreign;
        return d;
    }
    private static BuildingDef ManyZeroAndUnknown()
    {
        var d = Setup(0f); Boundary.SetAllMasses(0f);
        for (int i = 0; i < 22; i++) Boundary.AddMaterial(d, "Zero" + i.ToString("D2"), "Known zero", 0f);
        Boundary.AddMaterial(d, "ZZUnknown", "Unknown stock", float.NaN);
        return d;
    }
    private static void Listing(string name, Func<BuildingDef> setup, bool includeUnavailable, Action<JObject> assert, int limit = 50)
    {
        Boundary.Reset(); var def = setup(); var before = Boundary.SnapshotInputs(def);
        var wire = JObject.Parse(JsonConvert.SerializeObject(BuildPlanningTools.HostMaterialListing(def, includeUnavailable, limit), McpJsonUtil.Settings));
        assert(wire); Check(before == Boundary.SnapshotInputs(def), name + ": cached inputs unchanged");
        Check(Boundary.ForbiddenCalls == 0, name + ": no write or initialization"); cases++;
    }
    private static void Case(string name, Func<BuildingDef> setup, Action<JObject> assert, string material = "MatA")
    {
        Boundary.Reset(); var def = setup(); string before = Boundary.SnapshotInputs(def);
        var value = BuildPlanningTools.HostSelect(def, material, 0);
        var wire = JObject.Parse(JsonConvert.SerializeObject(value, McpJsonUtil.Settings));
        assert(wire); Check(before == Boundary.SnapshotInputs(def), name + ": native caches and objects unchanged");
        Check(Boundary.ForbiddenCalls == 0, name + ": no native/UI initialization or write"); cases++;
    }
    private static JObject Native(JObject value)
    {
        var result = value["selected"]?["nativeInventory"] as JObject;
        Check(result != null, "selected native evidence exists"); return result;
    }
    private static void Evidence(JObject value)
    {
        Check(value["uiAvailableSource"].ToString() == "WorldInventory.GetAmount(tag,true)", "ordinary UI source explicit");
        Check(value["uiScope"].ToString() == "same_parent_worlds_after_native_material_needs", "related-world scope explicit");
        Check(value["transportableKg"].Type == JTokenType.Null && value["transportStatus"].ToString() == "not_evaluated", "no transport quantity invented");
        Check(!(bool)value["fetchReservationsEvaluated"], "construction demand is not a per-object fetch reservation");
    }
    private static void Unknown(JObject value, string reason)
    {
        Check(!(bool)value["valid"], "unknown availability refuses ordinary selection");
        Check(value["selectedAvailableKg"].Type == JTokenType.Null && value["selected"]["availableKg"].Type == JTokenType.Null,
            "missing or invalid stock remains null in summary and selected record");
        Check(value["satisfied"].Type == JTokenType.Null && value["shortageKg"].Type == JTokenType.Null,
            "unknown stock is neither sufficient nor a known shortage");
        Check(value["quantityStatus"].ToString() == "unknown", "unknown quantity status");
        Check(value["inventoryState"].ToString() == "unknown" && value["inventoryReasonCode"].ToString() == reason,
            "readiness reason retained");
        Check(Native(value)["reasonCode"].ToString() == reason, "selected evidence preserves same reason");
        Evidence(Native(value));
    }
    private static void Equal(JToken actual, float expected, string message)
    { Check(actual != null && actual.Type != JTokenType.Null && (float)actual == expected, message + ": expected " + expected + "; actual " + actual); }
    private static void Check(bool ok, string message)
    { checks++; if (!ok) throw new InvalidOperationException("Native material quantity: " + message); }
}
