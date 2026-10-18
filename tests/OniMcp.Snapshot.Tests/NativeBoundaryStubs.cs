using System.Collections.Generic;
using Newtonsoft.Json.Linq;

// Native boundaries only. Snapshot metrics, alerts, result projection, watch,
// delta handling and localization are linked from production source.
namespace UnityEngine
{
    public sealed class GameObject
    {
        public string GetProperName() { return "sample"; }
    }
}
internal static class Grid
{
    internal static int[] WorldIdx { get; } = new int[1000];
    internal static bool IsValidCell(int cell) { return cell >= 0 && cell < WorldIdx.Length; }
    internal static bool IsWorldValidCell(int cell) { return IsValidCell(cell); }
    internal static int CellColumn(int cell) { return cell % 100; }
    internal static int CellRow(int cell) { return cell / 100; }
}
internal sealed class ClusterManager
{
    internal static ClusterManager Instance { get; set; }
    internal int activeWorldId { get; set; }
    internal WorldContainer GetWorld(int id) { return null; }
}
internal sealed class WorldContainer
{
    internal AlertManager AlertManager { get; set; }
}
internal sealed class AlertManager
{
    internal bool IsRedAlertToggledOn() { return false; }
    internal bool IsRedAlert() { return false; }
    internal bool IsYellowAlert() { return false; }
    internal bool IsOn() { return false; }
}
internal static class Components
{
    internal static MinionCollection LiveMinionIdentities { get; } = new MinionCollection();
}
internal sealed class MinionCollection
{
    internal int Count { get; set; }
}
namespace OniMcp.Tools
{
    internal static class ToolUtil
    {
        internal static string CleanName(string value) { return value; }
        internal static bool GetBool(JObject args, string key, bool fallback)
        { return args[key] == null ? fallback : (bool)args[key]; }
    }
}
