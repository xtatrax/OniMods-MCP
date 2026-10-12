using System;
using System.Collections.Generic;
using OniMcp.Support;
using OniMcp.Tools;

internal static class UrinationChoreRegression
{
    internal static void Run()
    {
        var original = McpLocalization.LanguageProvider;
        try
        {
            McpLocalization.LanguageProvider = () => "ja";
            Check(new UrinationChoreEvidence { ChoreType = "Pee", TargetIsToiletUse = true, IsMoving = true },
                "toilet", "moving", "トイレへ移動");
            Check(new UrinationChoreEvidence { ChoreType = "Pee", TargetIsToiletUse = true, WorkerUsingTargetToilet = true },
                "toilet", "using", "トイレを使用");
            Check(new UrinationChoreEvidence { ChoreType = "BreakPee", ProviderIsToiletUse = true, WorkerUsingTargetToilet = true },
                "toilet", "using", "トイレを使用");
            Check(new UrinationChoreEvidence { ChoreType = "Pee", IsAccidentChore = true, AccidentRunning = true },
                "incontinence", "active", "失禁");
            // Both paths have the same raw Pee type; absent target/state evidence
            // must never turn the ambiguous game translation into an accident.
            Check(new UrinationChoreEvidence { ChoreType = "Pee" }, "unknown", "unknown", "排尿関連・未判別");
            Check(new UrinationChoreEvidence { ChoreType = "BreakPee", TargetIsToiletUse = false, ProviderIsToiletUse = false },
                "unknown", "unknown", "排尿関連・未判別");
            Check(new UrinationChoreEvidence { ChoreType = "Pee", TargetIsToiletUse = true },
                "toilet", "moving_or_using", "トイレへ移動／使用");
            Check(new UrinationChoreEvidence { ChoreType = "Pee", TargetIsToiletUse = true, IsMoving = true, WorkerUsingTargetToilet = true },
                "toilet", "using", "トイレを使用");
            // Class identity without its actual running state is not sufficient.
            foreach (bool? state in new bool?[] { null, false })
                Check(new UrinationChoreEvidence { ChoreType = "Pee", IsAccidentChore = true, AccidentRunning = state, TargetIsToiletUse = true },
                    "unknown", "unknown", "排尿関連・未判別");
            Check(new UrinationChoreEvidence { ChoreType = "Pee", IsAccidentChore = true, AccidentRunning = true, TargetIsToiletUse = true },
                "incontinence", "active", "失禁");
            foreach (string type in new[] { "CleanToilet", "Mop", "Eat", "お漏らし", "pee", null })
                Assert(UrinationChoreClassification.Classify(new UrinationChoreEvidence { ChoreType = type, TargetIsToiletUse = true }) == null,
                    "Unrelated chores and translated names must remain unchanged: " + type);
            var evidence = new UrinationChoreEvidence { ChoreType = "Pee", TargetIsToiletUse = true, IsMoving = true };
            var descriptions = new HashSet<string>();
            foreach (string language in new[] { "en", "ja", "zh-Hans" })
            {
                McpLocalization.LanguageProvider = () => language;
                var result = UrinationChoreClassification.Classify(evidence);
                Assert((string)result["category"] == "toilet" && (string)result["phase"] == "moving", "Classification must not depend on language");
                descriptions.Add((string)result["displayName"]);
                var observed = (Dictionary<string, object>)result["evidence"];
                Assert((bool?)observed["targetIsToiletUse"] == true && observed["accidentRunning"] == null,
                    "Preserve the known evidence and missing accident-state evidence");
            }
            Assert(descriptions.Count == 3, "All languages must have a distinct display label");
        }
        finally { McpLocalization.LanguageProvider = original; }
        Console.WriteLine("PASS: urination classification distinguishes toilet movement/use, active accident, and insufficient evidence");
    }

    private static void Check(UrinationChoreEvidence evidence, string category, string phase, string display)
    {
        var result = UrinationChoreClassification.Classify(evidence);
        Assert(result != null && (string)result["category"] == category && (string)result["phase"] == phase
            && (string)result["displayName"] == display, "Unexpected classification: " + display);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Urination regression: " + message);
    }
}
