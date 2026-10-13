using System;

namespace OniMcp.Tools
{
    internal sealed class MaterialQuantityDiagnostics
    {
        public float? RequiredKg;
        public float? AvailableKg;
        public float? ShortageKg;
        public bool? Satisfied;
        public string Status;

        internal static float SingleSlotRequirement(float[] masses)
        {
            return masses != null && masses.Length == 1 && Known(masses[0]) && masses[0] > 0f
                ? masses[0] : 0f;
        }

        private static bool Known(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
        }

        internal static MaterialQuantityDiagnostics Evaluate(float requiredKg, float availableKg, bool freeBuild)
        {
            var result = new MaterialQuantityDiagnostics
            {
                RequiredKg = Known(requiredKg) && requiredKg > 0f ? (float?)requiredKg : null,
                AvailableKg = Known(availableKg) ? (float?)availableKg : null,
                Status = "unknown"
            };
            if (result.RequiredKg.HasValue && result.AvailableKg.HasValue)
            {
                result.ShortageKg = Math.Max(0f, requiredKg - availableKg);
                result.Satisfied = availableKg >= requiredKg;
                result.Status = result.Satisfied.Value ? "sufficient" : "insufficient";
            }
            if (freeBuild)
            {
                result.Satisfied = true;
                result.Status = "not_required";
            }
            return result;
        }
    }
}
