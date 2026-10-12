using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OniMcp.Tools
{
    public static partial class DuplicantTools
    {
        internal static Dictionary<string, object> DescribeCurrentChore(Chore current, MinionIdentity dupe)
        {
            if (current == null)
                return null;
            var unavailable = new List<string>();
            string rawName = SafeString(() => GameUtil.GetChoreName(current, null),
                SafeString(() => current.GetReportName(), current.GetType().Name));
            string type = current.choreType?.Id ?? current.GetType().Name;
            var target = ReadChoreEvidence(() => current.target?.gameObject, unavailable, "target");
            var provider = ReadChoreEvidence(() => current.provider?.gameObject, unavailable, "provider");
            var targetUse = ReadChoreEvidence(() => target == null ? null : target.GetComponent<ToiletWorkableUse>(), unavailable, "targetToiletUse");
            var providerUse = ReadChoreEvidence(() => provider == null ? null : provider.GetComponent<ToiletWorkableUse>(), unavailable, "providerToiletUse");
            var evidence = new UrinationChoreEvidence
            {
                ChoreType = type,
                IsAccidentChore = current is PeeChore,
                TargetIsToiletUse = target == null || unavailable.Contains("targetToiletUse") ? (bool?)null : targetUse != null,
                ProviderIsToiletUse = provider == null || unavailable.Contains("providerToiletUse") ? (bool?)null : providerUse != null
            };
            if (current is PeeChore accident)
                evidence.AccidentRunning = ReadChoreEvidence(() => accident.smi == null ? (bool?)null
                    : accident.InProgress() && accident.smi.IsInsideState(accident.smi.sm.running), unavailable, "accidentRunning");
            // Inspect this duplicant's worker, never another toilet user's busy flag.
            if (dupe != null && (targetUse != null || providerUse != null))
            {
                evidence.WorkerUsingTargetToilet = ReadChoreEvidence(() =>
                {
                    var worker = dupe.GetComponent<WorkerBase>();
                    if (worker == null) return (bool?)null;
                    var active = worker.GetWorkable();
                    return (bool?)(active != null && (active == targetUse || active == providerUse));
                }, unavailable, "workerUsingTargetToilet");
                evidence.IsMoving = ReadChoreEvidence(() =>
                {
                    var navigator = dupe.GetComponent<Navigator>();
                    return navigator == null ? (bool?)null : navigator.IsMoving();
                }, unavailable, "isMoving");
            }
            var classification = UrinationChoreClassification.Classify(evidence);
            var result = new Dictionary<string, object>
            {
                ["name"] = classification == null ? rawName : classification["displayName"],
                ["rawName"] = rawName,
                ["reportName"] = SafeString(() => current.GetReportName(), current.GetType().Name),
                ["type"] = type,
                ["runtimeType"] = current.GetType().FullName,
                ["groups"] = current.choreType?.groups?.Select(group => group.Id).ToList() ?? new List<string>(),
                ["target"] = ReadChoreEvidence(() => ChoreObjectReference(target), unavailable, "targetReference"),
                ["provider"] = ReadChoreEvidence(() => ChoreObjectReference(provider), unavailable, "providerReference")
            };
            if (classification != null)
                result["urination"] = classification;
            if (unavailable.Count > 0)
                result["unavailableEvidence"] = unavailable;
            return result;
        }

        private static T ReadChoreEvidence<T>(Func<T> read, List<string> unavailable, string name)
        {
            try { return read(); }
            catch { unavailable.Add(name); return default(T); }
        }

        private static Dictionary<string, object> ChoreObjectReference(GameObject obj)
        {
            if (obj == null)
                return null;
            int cell = Grid.PosToCell(obj);
            bool valid = Grid.IsValidCell(cell);
            var prefab = obj.GetComponent<KPrefabID>();
            var building = obj.GetComponent<Building>();
            return new Dictionary<string, object>
            {
                ["id"] = prefab?.InstanceID ?? obj.GetInstanceID(),
                ["prefabId"] = building?.Def?.PrefabID ?? prefab?.PrefabTag.Name ?? obj.name,
                ["name"] = ToolUtil.CleanName(obj.GetProperName()),
                ["cell"] = valid ? (object)cell : null,
                ["x"] = valid ? (object)Grid.CellColumn(cell) : null,
                ["y"] = valid ? (object)Grid.CellRow(cell) : null,
                ["worldId"] = valid && Grid.IsWorldValidCell(cell) ? (object)Grid.WorldIdx[cell] : null
            };
        }
    }
}
