using System.Collections.Generic;
using System.Text;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static string ReadBuriedMarker(int cell)
        {
            if (!Grid.IsValidCell(cell) || !Grid.IsWorldValidCell(cell)) return "unavailable";
            if (!IsReadableMapCell(cell)) return "unobserved";
            if (!Grid.Solid[cell]) return "not_solid";
            var visualizer = Game.Instance == null ? null : Game.Instance.GetComponent<EntombedItemVisualizer>();
            if (visualizer == null) return "unavailable";
            return visualizer.IsEntombedItem(cell) ? "present" : "absent";
        }

        private static void AppendBuriedMarkerSummary(StringBuilder sb, int xMin, int xMax, int yMin, int yMax)
        {
            int count = 0;
            var samples = new List<string>();
            for (int y = yMax; y >= yMin; y--)
                for (int x = xMin; x <= xMax; x++)
                    if (ReadBuriedMarker(Grid.XYToCell(x, y)) == "present")
                    {
                        count++;
                        if (samples.Count < 32) samples.Add("(" + x + "," + y + ")");
                    }
            sb.AppendLine("- Buried spawn markers: visible solid cells=" + count
                + "; cells=" + (samples.Count == 0 ? "none" : string.Join(",", samples.ToArray()))
                + "; omitted=" + (count - samples.Count)
                + ". Source=EntombedItemVisualizer.IsEntombedItem; contents unknown; no spawn/reveal action is performed.");
        }
    }
}
