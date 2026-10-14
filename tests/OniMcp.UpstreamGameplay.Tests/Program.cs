using System;
using OniMcp.Tools;
internal static class Program
{
    private static int assertions;
    private static void Main()
    {
        FarmingUprootRegression.Run();
        ExistingMaterialReaderRegression.Run();
        TestExistingMaterialPolicy();
        TestSweepEligibilityPolicy();
        Console.WriteLine("Upstream gameplay policy regression checks passed: " + assertions);
    }
    private static void TestExistingMaterialPolicy()
    {
        Check(BuildPlanningExistingMaterialPolicy.RequestMatchesExisting(null, null, null, false),
            "omitted material must preserve existing-placement idempotency");
        Check(BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("auto", null, null, false),
            "auto material must preserve existing-placement idempotency");
        Check(BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("default", null, null, false),
            "default material must preserve existing-placement idempotency");
        Check(BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("IgneousRock", "IgneousRock", "Igneous Rock", false),
            "explicit canonical material must match the existing construction material");
        Check(BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("Igneous Rock", "IgneousRock", "Igneous Rock", false),
            "explicit proper material name must match the existing construction material");
        Check(BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("BuildableRaw", "IgneousRock", "Igneous Rock", true),
            "explicit material category must accept an existing material in that category");
        Check(!BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("Granite", "IgneousRock", "Igneous Rock", false),
            "different explicit material must not be treated as already satisfied");
        Check(!BuildPlanningExistingMaterialPolicy.RequestMatchesExisting("Granite", null, null, false),
            "unknown existing material must fail closed for an explicit request");
        Check(BuildPlanningExistingMaterialPolicy.SelectedMaterialMatchesExisting("Granite", "Granite"),
            "resolved explicit material must match the existing tag exactly");
        Check(!BuildPlanningExistingMaterialPolicy.SelectedMaterialMatchesExisting("Granite", "IgneousRock"),
            "resolved different material must be rejected");
    }

    private static void TestSweepEligibilityPolicy()
    {
        SweepHandlerRegression.Run();
        Check(SweepEligibilityPolicy.RejectionReason(false, false) == "no_clearable",
            "pickupables without Clearable remain rejected");
        Check(SweepEligibilityPolicy.RejectionReason(true, false) == "not_clearable",
            "non-clearable pickupables must be rejected before sweep designation or priority changes");
        Check(SweepEligibilityPolicy.RejectionReason(true, true) == null,
            "clearable pickupables remain eligible for the existing sweep path");
    }

    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException(description);
    }
}
