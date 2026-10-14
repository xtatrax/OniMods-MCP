using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        // Validate every token before any grouped native order/placement handler can run.
        private static bool ValidateMapEditInput(JObject args, List<MapEditCell> changes, out string error)
        {
            error = null;
            if (args["priority"] != null
                && (args["priority"].Type != JTokenType.Integer
                    || !int.TryParse(args["priority"].ToString(), out int requestedPriority)
                    || requestedPriority < 1 || requestedPriority > 9))
            {
                error = "priority must be an integer from 1 through 9.";
                return false;
            }
            foreach (var change in changes)
            {
                if (!MapTextReadPolicy.Inside(change.X, change.Y, Grid.WidthInCells, Grid.HeightInCells)
                    || !IsReadableMapCell(Grid.XYToCell(change.X, change.Y)) || change.FromToken == "?")
                {
                    error = "Map edit targets an unknown, hidden, or out-of-world cell; no order was issued.";
                    return false;
                }
                error = MapMaterialSelectionError(change.ToToken);
                if (error != null) return false;
                string kind = ParseOrderAction(change.ToToken);
                if (IsOrderAction(kind) && (!ParseBuildToken(change.ToToken, out _, out _, out string material)
                    || material != null))
                {
                    error = "Invalid order token: " + change.ToToken + ". Priority must be 1 through 9; orders do not take materials.";
                    return false;
                }
            }
            return true;
        }
    }
}
