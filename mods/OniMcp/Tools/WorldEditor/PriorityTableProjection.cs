using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OniMcp.Tools
{
    internal static class PriorityTableProjection
    {
        private const int MaximumColumns = 18;

        internal static List<string> CollectColumns(IEnumerable<JObject> rows)
        {
            var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "name", "dupe", "dupeName", "duplicant", "id", "instanceId"
            };
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var columns = new List<string>();
            foreach (var row in rows ?? Enumerable.Empty<JObject>())
            {
                if (row == null)
                    continue;
                var priorities = row["priorities"] as JArray;
                if (priorities != null)
                {
                    foreach (var entry in priorities.OfType<JObject>())
                    {
                        string id = GroupId(entry);
                        if (!string.IsNullOrEmpty(id) && seen.Add(id))
                            columns.Add(id);
                    }
                    continue;
                }
                foreach (var property in row.Properties())
                {
                    if (skip.Contains(property.Name))
                        continue;
                    var values = property.Value as JObject;
                    if (values != null)
                    {
                        foreach (var value in values.Properties())
                            if (seen.Add(value.Name))
                                columns.Add(value.Name);
                    }
                    else if (property.Value.Type != JTokenType.Array && seen.Add(property.Name))
                    {
                        columns.Add(property.Name);
                    }
                }
            }
            return columns.Take(MaximumColumns).ToList();
        }

        internal static JToken ValueToken(JObject row, string column)
        {
            if (row == null || column == null)
                return null;
            var priorities = row["priorities"] as JArray;
            if (priorities != null)
            {
                // Native IDs are unique; preserve the first observation if input repeats one.
                var entry = priorities.OfType<JObject>().FirstOrDefault(item =>
                    string.Equals(GroupId(item), column, StringComparison.Ordinal));
                return entry?["priority"];
            }
            if (row[column] != null)
                return row[column];
            foreach (var values in row.Properties().Select(property => property.Value).OfType<JObject>())
                if (values[column] != null)
                    return values[column];
            return null;
        }

        private static string GroupId(JObject entry)
        {
            var id = entry?["choreGroupId"];
            return id?.Type == JTokenType.String ? (string)id : null;
        }
    }
}
