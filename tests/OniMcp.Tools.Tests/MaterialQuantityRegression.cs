using System;
using OniMcp.Tools;

internal static class MaterialQuantityRegression
{
    internal static void Run()
    {
        Check(MaterialQuantityDiagnostics.SingleSlotRequirement(new[] { 100f }) == 100f, "native mass");
        foreach (var masses in new[] { null, new float[0], new[] { 100f, 200f }, new[] { float.NaN }, new[] { float.PositiveInfinity }, new[] { -1f } })
            Check(MaterialQuantityDiagnostics.SingleSlotRequirement(masses) == 0f, "unknown or multi-slot mass");
        var low = MaterialQuantityDiagnostics.Evaluate(100f, 10f, false);
        Check(low.Satisfied == false && low.ShortageKg == 90f && low.Status == "insufficient", "positive shortage");
        var zero = MaterialQuantityDiagnostics.Evaluate(100f, 0f, false);
        Check(zero.Satisfied == false && zero.ShortageKg == 100f, "zero inventory");
        var exact = MaterialQuantityDiagnostics.Evaluate(100f, 100f, false);
        Check(exact.Satisfied == true && exact.ShortageKg == 0f, "exact boundary");
        var abundant = MaterialQuantityDiagnostics.Evaluate(100f, 250f, false);
        Check(abundant.Status == "sufficient" && abundant.ShortageKg == 0f, "surplus");
        foreach (float unknown in new[] { float.NaN, float.PositiveInfinity, -1f })
        {
            var missing = MaterialQuantityDiagnostics.Evaluate(100f, unknown, false);
            Check(missing.Satisfied == null && missing.ShortageKg == null, "missing inventory is not zero");
        }
        var noMass = MaterialQuantityDiagnostics.Evaluate(0f, 10f, false);
        Check(noMass.Satisfied == null && noMass.RequiredKg == null, "missing requirement");
        var free = MaterialQuantityDiagnostics.Evaluate(100f, 10f, true);
        Check(free.Status == "not_required" && free.Satisfied == true && free.ShortageKg == 90f, "explicit free-build exemption");
        Console.WriteLine("Material quantity regression passed.");
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new Exception("Material quantity: " + message);
    }
}
