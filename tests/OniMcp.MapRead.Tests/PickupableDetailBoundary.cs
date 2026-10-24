using System.Text;
using Newtonsoft.Json.Linq;

// Existing MapRead boundary substitutes GetProperName, native cached inputs,
// Grid.PosToCell and argument reads. No product grouping or mass output is copied.
namespace UnityEngine
{
    public sealed partial class GameObject { public Transform transform = new(); }
}
namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        internal static string PickupableDetailForTests(int cell, JObject args)
        {
            var text = new StringBuilder();
            AppendCellPickupDetailSnapshot(text, cell, args);
            return text.ToString();
        }
        internal static void RunPickupableDetailRegressions() => PickupableDetailRegression.Run();
    }
}
