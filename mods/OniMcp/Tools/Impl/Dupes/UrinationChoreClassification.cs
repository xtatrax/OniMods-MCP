using System;
using System.Collections.Generic;
using OniMcp.Support;

namespace OniMcp.Tools
{
    internal sealed class UrinationChoreEvidence
    {
        internal string ChoreType;
        internal bool IsAccidentChore;
        internal bool? AccidentRunning;
        internal bool? TargetIsToiletUse;
        internal bool? ProviderIsToiletUse;
        internal bool? WorkerUsingTargetToilet;
        internal bool? IsMoving;
    }

    internal static class UrinationChoreClassification
    {
        internal static Dictionary<string, object> Classify(UrinationChoreEvidence evidence)
        {
            if (!evidence.IsAccidentChore
                && !string.Equals(evidence.ChoreType, "Pee", StringComparison.Ordinal)
                && !string.Equals(evidence.ChoreType, "BreakPee", StringComparison.Ordinal))
                return null;

            string category = "unknown";
            string phase = "unknown";
            string reason = "insufficient_evidence";
            string key = "chores.urination.unknown";
            string fallback = "Urination-related: undetermined";
            // A Pee chore type alone is shared by urgent toilet use and accidents.
            // Only an active PeeChore.running state proves an actual accident.
            if (evidence.IsAccidentChore)
            {
                if (evidence.AccidentRunning == true)
                {
                    category = "incontinence";
                    phase = "active";
                    reason = "active_pee_chore_state";
                    key = "chores.urination.incontinence";
                    fallback = "Incontinence";
                }
                else
                    reason = "accident_state_unconfirmed";
            }
            else if (evidence.TargetIsToiletUse == true || evidence.ProviderIsToiletUse == true)
            {
                category = "toilet";
                reason = evidence.TargetIsToiletUse == true ? "toilet_use_target" : "toilet_use_provider";
                phase = "moving_or_using";
                key = "chores.urination.toilet";
                fallback = "Moving to / using toilet";
                if (evidence.WorkerUsingTargetToilet == true)
                {
                    phase = "using";
                    key = "chores.urination.using";
                    fallback = "Using toilet";
                }
                else if (evidence.IsMoving == true)
                {
                    phase = "moving";
                    key = "chores.urination.moving";
                    fallback = "Moving to toilet";
                }
            }
            return new Dictionary<string, object>
            {
                ["category"] = category,
                ["phase"] = phase,
                ["displayName"] = McpLocalization.Get(key, fallback),
                ["reasonCode"] = reason,
                ["evidence"] = new Dictionary<string, object>
                {
                    ["isAccidentChore"] = evidence.IsAccidentChore,
                    ["accidentRunning"] = evidence.AccidentRunning,
                    ["targetIsToiletUse"] = evidence.TargetIsToiletUse,
                    ["providerIsToiletUse"] = evidence.ProviderIsToiletUse,
                    ["workerUsingTargetToilet"] = evidence.WorkerUsingTargetToilet,
                    ["isMoving"] = evidence.IsMoving
                }
            };
        }
    }
}
