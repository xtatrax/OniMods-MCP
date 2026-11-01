using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    // Reads only existing database/owned-item caches. Native eligibility remains authoritative.
    internal static class BuildingFacadeReader
    {
        private static readonly FieldInfo ExistingDb = BindField(typeof(Db), "_Instance", typeof(Db), true);
        private static readonly FieldInfo InventoryData = BindInventory();
        private static readonly FieldInfo ItemsByType = InventoryData == null ? null
            : BindField(InventoryData.FieldType, "ItemsByType", null, false);

        internal sealed class Observation
        {
            internal bool? Unlocked;
            internal string Name;
            internal string ReasonCode;
            internal string Status { get { return !Unlocked.HasValue ? "unknown" : Unlocked.Value ? "unlocked" : "locked"; } }
            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    ["status"] = Status,
                    ["reasonCode"] = ReasonCode == null ? (object)JValue.CreateNull() : ReasonCode,
                    ["scope"] = "existing_native_permit_and_owned_inventory"
                };
            }
        }

        private static FieldInfo BindField(Type type, string name, Type expectedType, bool isStatic)
        {
            try
            {
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
                return field != null && field.DeclaringType == type && field.IsStatic == isStatic
                    && (expectedType == null || field.FieldType == expectedType) ? field : null;
            }
            catch { return null; }
        }

        private static FieldInfo BindInventory()
        {
            var field = BindField(typeof(KleiItems), "InventoryData", null, true);
            return field != null && field.FieldType.FullName == "KleiItems+Inventory" ? field : null;
        }

        private static bool OwnedInventoryReady(out string reason)
        {
            reason = null;
            if (InventoryData == null || ItemsByType == null) { reason = "owned_inventory_binding_unavailable"; return false; }
            try
            {
                object inventory = InventoryData.GetValue(null);
                if (inventory == null || ItemsByType.GetValue(inventory) == null)
                { reason = "owned_inventory_cache_missing"; return false; }
                return true;
            }
            catch { reason = "owned_inventory_cache_read_failed"; return false; }
        }

        internal static Observation Read(string facadeId)
        {
            var result = new Observation { Name = facadeId };
            if (ExistingDb == null) { result.ReasonCode = "database_binding_unavailable"; return result; }
            try
            {
                var db = ExistingDb.GetValue(null) as Db;
                if (db == null) { result.ReasonCode = "database_cache_missing"; return result; }
                var permits = db.Permits;
                if (permits == null || permits.resources == null) { result.ReasonCode = "permit_catalog_missing"; return result; }
                var permit = permits.TryGet(facadeId);
                if (permit == null) { result.ReasonCode = "permit_resource_missing"; return result; }
                var facades = permits.BuildingFacades;
                if (facades != null && facades.resources != null)
                {
                    var facade = facades.TryGet(facadeId);
                    if (facade != null) result.Name = ToolUtil.CleanName(facade.Name);
                }
                // Universal permits do not need owned inventory. This guard mirrors
                // only the native branch precondition; IsUnlocked decides eligibility.
                if (permit.Rarity != Database.PermitRarity.Universal)
                {
                    string reason;
                    if (!OwnedInventoryReady(out reason)) { result.ReasonCode = reason; return result; }
                }
                result.Unlocked = permit.IsUnlocked();
                return result;
            }
            catch { result.ReasonCode = "permit_read_failed"; return result; }
        }
    }
}
