using System;

namespace OniMcp.Tools
{
    // A cell-temperature heuristic, not a heat-transfer or body-temperature simulation.
    // Native state is required: low mass does not turn gas/liquid into vacuum.
    internal static class AmbientTemperaturePolicy
    {
        internal static string Status(string state, double temperatureC)
        {
            if (string.Equals(state, "vacuum", StringComparison.OrdinalIgnoreCase))
                return "not_applicable_vacuum";
            if ((state != "gas" && state != "liquid" && state != "solid")
                || double.IsNaN(temperatureC) || double.IsInfinity(temperatureC))
                return "unknown";
            return "cell_temperature_only";
        }

        internal static bool WarnForDupe(string state, double temperatureC) =>
            Status(state, temperatureC) == "cell_temperature_only"
            && (temperatureC < -20d || temperatureC > 60d);

        internal static string MapBand(string state, double temperatureC)
        {
            string status = Status(state, temperatureC);
            if (status != "cell_temperature_only") return status;
            if (temperatureC >= 72d) return "danger_hot";
            if (temperatureC <= -20d) return "danger_cold";
            if (temperatureC > 40d) return "hot";
            if (temperatureC < 10d) return "cold";
            return "dupe_comfort";
        }

        internal static bool SuggestThermalRead(string state, double temperatureC)
        {
            string band = MapBand(state, temperatureC);
            return band == "hot" || band == "cold" || band == "danger_hot" || band == "danger_cold";
        }

        internal static string MapText(string state, double temperatureC)
        {
            switch (MapBand(state, temperatureC))
            {
                case "not_applicable_vacuum": return "対象外（真空）; セル温度から周囲媒体の熱危険を判定しない。呼吸・体温・帰路は別途確認";
                case "unknown": return "未取得; 元素状態または温度を確認できない";
                case "danger_hot": return "危険-やけどのリスク; comfort=10~40°C, caution=40~72°C";
                case "danger_cold": return "危険-低温のリスク; comfort=10~40°C, caution=-20~10°C";
                case "hot": return "やや高温; comfort=10~40°C";
                case "cold": return "やや低温; comfort=10~40°C";
                default: return "適温; comfort=10~40°C";
            }
        }
    }
}
