using System.Collections.Generic;
using System.Text;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static string ReadBuriedMarker(int cell)
        {
            return ReadBuriedMarker(cell, out bool _);
        }

        private static string ReadBuriedMarker(int cell, out bool readable)
        {
            readable = false;
            try
            {
                if (!Grid.IsValidCell(cell) || !Grid.IsWorldValidCell(cell)) return "unavailable";
                if (!IsReadableMapCell(cell)) return "unobserved";
                readable = true;
                if (!Grid.Solid[cell]) return "not_solid";
                var visualizer = Game.Instance == null ? null : Game.Instance.GetComponent<EntombedItemVisualizer>();
                if (visualizer == null) return "unavailable";
                return visualizer.IsEntombedItem(cell) ? "present" : "absent";
            }
            catch { return "unavailable"; }
        }

        private sealed class BuriedMarkerCoverage
        {
            internal int Requested;
            internal int Readable;
            internal int Present;
            internal int Absent;
            internal int NotSolid;
            internal int Unobserved;
            internal int Unavailable;
            internal readonly List<string> Samples = new List<string>();
            internal int Omitted => Present - Samples.Count;
            internal string Availability => Requested == 0 || NotSolid == Requested ? "not_applicable"
                : Unobserved == Requested ? "unobserved"
                : Unobserved + Unavailable == 0 ? "observed"
                : Present + Absent + NotSolid > 0 ? "partial" : "unavailable";
            internal int? MarkerCount => Availability == "observed" ? Present : (int?)null;
            internal string SampleCells => Samples.Count > 0 ? string.Join(",", Samples.ToArray())
                : Availability == "not_applicable" ? "not_applicable" : MarkerCount.HasValue ? "none" : "unknown";
        }

        private static BuriedMarkerCoverage ReadBuriedMarkerCoverage(int xMin, int xMax, int yMin, int yMax)
        {
            var result = new BuriedMarkerCoverage();
            for (int y = yMax; y >= yMin; y--)
                for (int x = xMin; x <= xMax; x++)
                {
                    result.Requested++;
                    string state = ReadBuriedMarker(Grid.XYToCell(x, y), out bool readable);
                    if (readable) result.Readable++;
                    switch (state)
                    {
                        case "present":
                            result.Present++;
                            if (result.Samples.Count < 32) result.Samples.Add("(" + x + "," + y + ")");
                            break;
                        case "absent": result.Absent++; break;
                        case "not_solid": result.NotSolid++; break;
                        case "unobserved": result.Unobserved++; break;
                        default: result.Unavailable++; break;
                    }
                }
            return result;
        }

        private static void AppendBuriedMarkerSummary(StringBuilder sb, int xMin, int xMax, int yMin, int yMax)
        {
            var result = ReadBuriedMarkerCoverage(xMin, xMax, yMin, yMax);
            sb.AppendLine(McpLocalization.Format("buried_markers.summary", null,
                result.Availability, result.MarkerCount.HasValue ? (object)result.MarkerCount.Value : "null",
                result.Present, result.Requested, result.Readable, result.Absent, result.NotSolid,
                result.Unobserved, result.Unavailable, result.SampleCells, result.Omitted));
        }
    }
}
