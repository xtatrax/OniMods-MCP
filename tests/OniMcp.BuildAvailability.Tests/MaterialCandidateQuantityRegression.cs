using System;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;
internal static class MaterialCandidateQuantityRegression
{
    internal static void Run()
    {
        int checks = 0;
        foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1f })
        {
            var candidate = BuildPlanningTools.HostMaterialCandidate(value);
            if (candidate.Property("availableKg") == null || candidate["availableKg"].Type != JTokenType.Null)
                throw new InvalidOperationException("Plan material missing quantity became known zero");
            checks++;
        }
        foreach (float value in new[] { 0f, 835f })
        {
            var candidate = BuildPlanningTools.HostMaterialCandidate(value);
            if ((float)candidate["availableKg"] != value) throw new InvalidOperationException("Known plan material quantity changed");
            checks++;
        }
        Console.WriteLine("Plan material quantity: " + checks + " wire checks PASS");
    }
}
