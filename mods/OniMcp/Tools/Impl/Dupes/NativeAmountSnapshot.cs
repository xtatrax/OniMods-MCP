using System;
using System.Collections.Generic;

namespace OniMcp.Tools
{
    internal static class NativeAmountSnapshot
    {
        internal static Dictionary<string, object> Read(float raw, Func<float> min, Func<float> max, Func<float> delta)
        {
            var errors = new List<string>();
            object value = ReadValue(() => raw, "value", errors);
            object minimum = ReadValue(min, "minimum", errors);
            object maximum = ReadValue(max, "maximum", errors);
            object change = ReadValue(delta, "attributeDeltaPerSecond", errors);
            double? ratio = null;
            if (value != null && minimum != null && maximum != null && (double)maximum > (double)minimum)
                ratio = ((double)value - (double)minimum) / ((double)maximum - (double)minimum);
            return new Dictionary<string, object>
            {
                ["value"] = value, ["minimum"] = minimum, ["maximum"] = maximum,
                ["attributeDeltaPerSecond"] = change, ["rangeFraction"] = ratio,
                ["available"] = errors.Count == 0, ["errors"] = errors,
                ["source"] = "AmountInstance.value/GetMin()/GetMax()/GetDelta()",
                ["deltaScope"] = "Amount attribute only; not necessarily the total change from monitor/storage operations."
            };
        }

        private static object ReadValue(Func<float> read, string name, List<string> errors)
        {
            try
            {
                if (read == null) { errors.Add(name + ":reader_missing"); return null; }
                float value = read();
                if (float.IsNaN(value) || float.IsInfinity(value)) { errors.Add(name + ":non_finite"); return null; }
                return (double)value;
            }
            catch (Exception ex) { errors.Add(name + ":" + ex.GetType().Name); return null; }
        }
    }
}
