using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OniMcp.Tools
{
    internal sealed class PrintingPodCandidateObservation
    {
        internal readonly bool? Available;
        internal readonly string Status;
        internal readonly string Reason;
        internal readonly string Source;
        internal readonly int? TotalCount;
        internal readonly int? CarePackageCount;
        internal readonly List<CarePackageInfo> CarePackages;

        internal PrintingPodCandidateObservation(bool? available, string status, string reason, string source,
            int? totalCount = null, int? carePackageCount = null, List<CarePackageInfo> carePackages = null)
        {
            Available = available;
            Status = status;
            Reason = reason;
            Source = source;
            TotalCount = totalCount;
            CarePackageCount = carePackageCount;
            CarePackages = carePackages ?? new List<CarePackageInfo>();
        }

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                ["status"] = Status,
                ["reason"] = Reason,
                ["source"] = Source,
                ["totalCount"] = TotalCount,
                ["carePackageCount"] = CarePackageCount
            };
        }
    }

    // Observe only the existing screen cache. No UI initialization, refresh or eligibility evaluation.
    internal static class PrintingPodCandidateObserver
    {
        private const string ContainerSource = "ImmigrantScreen.CharacterSelectionController.containers";

        internal static PrintingPodCandidateObservation Read(Telepad target)
        {
            var immigration = Immigration.Instance;
            if (immigration == null)
                return new PrintingPodCandidateObservation(null, "unknown", "immigration_unavailable", "Immigration.Instance");
            if (!immigration.ImmigrantsAvailable)
                return new PrintingPodCandidateObservation(false, "no_ready_offers", "no_ready_offers",
                    "Immigration.ImmigrantsAvailable", 0, 0);
            if (target == null)
                return new PrintingPodCandidateObservation(true, "unknown", "target_unavailable", "Telepad");

            var screen = ImmigrantScreen.instance;
            if (screen == null)
                return new PrintingPodCandidateObservation(true, "not_materialized", "immigrant_screen_unavailable", "ImmigrantScreen.instance");
            if (screen.Telepad != target)
                return new PrintingPodCandidateObservation(true, "unknown", "screen_target_mismatch", "ImmigrantScreen.Telepad");

            object cached;
            try
            {
                var field = typeof(CharacterSelectionController).GetField("containers",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null)
                    return new PrintingPodCandidateObservation(true, "unknown", "candidate_field_unavailable", ContainerSource);
                cached = field.GetValue(screen);
            }
            catch (Exception)
            {
                return new PrintingPodCandidateObservation(true, "unknown", "candidate_collection_unreadable", ContainerSource);
            }
            if (cached == null)
                return new PrintingPodCandidateObservation(true, "not_materialized", "candidate_collection_missing", ContainerSource);
            var containers = cached as IEnumerable<ITelepadDeliverableContainer>;
            if (containers == null)
                return new PrintingPodCandidateObservation(true, "unknown", "candidate_collection_type_unavailable", ContainerSource);

            List<ITelepadDeliverableContainer> snapshot;
            try { snapshot = containers.ToList(); }
            catch (Exception)
            {
                return new PrintingPodCandidateObservation(true, "unknown", "candidate_enumeration_failed", ContainerSource);
            }
            if (snapshot.Count == 0)
                return new PrintingPodCandidateObservation(true, "not_materialized", "candidate_collection_empty", ContainerSource);

            var carePackages = new List<CarePackageInfo>();
            bool pending = false;
            foreach (var container in snapshot)
            {
                if (container == null || container is UnityEngine.Object native && native == null)
                    return new PrintingPodCandidateObservation(true, "unknown", "null_candidate_entry", ContainerSource);
                var carePackage = container as CarePackageContainer;
                if (carePackage == null)
                    continue;
                var info = carePackage.Info;
                if (info == null)
                    pending = true;
                else
                    carePackages.Add(info);
            }
            if (pending)
                return new PrintingPodCandidateObservation(true, "generation_pending", "care_package_info_missing", "CarePackageContainer.Info");
            return new PrintingPodCandidateObservation(true, "materialized", "native_cache_observed", ContainerSource,
                snapshot.Count, carePackages.Count, carePackages);
        }
    }
}
