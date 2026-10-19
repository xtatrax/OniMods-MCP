using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;

namespace OniMcp.Tools
{
    public static partial class WorldEditorTools
    {
        private static bool TryParseCommandKeyValues(string line, out JObject arguments, out string error)
        {
            return TryParseCommandKeyValues(line, out arguments, out _, out error);
        }

        private static bool TryParseCommandKeyValues(
            string line, out JObject arguments, out List<string> tokens, out string error)
        {
            arguments = null;
            if (!TryTokenizeCommand(line, out tokens, out error))
                return false;

            var parsed = new JObject();
            foreach (string token in tokens.Skip(1))
            {
                int eq = token.IndexOf('=');
                if (eq <= 0)
                    continue;
                string key = token.Substring(0, eq).Trim();
                string value = token.Substring(eq + 1).Trim();
                if (value.StartsWith("[", StringComparison.Ordinal) || value.StartsWith("{", StringComparison.Ordinal))
                {
                    if (!TryReadCommandJson(value, out JToken structured))
                    {
                        error = InvalidCommandJson(key);
                        return false;
                    }
                    // Arrays retain their JSON types. Object payloads keep the existing string contract.
                    parsed[key] = structured is JArray ? structured : new JValue(value);
                    continue;
                }

                // Keep legacy scalar inference, including quoted booleans/integers and comma lists.
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2);
                if (bool.TryParse(value, out bool boolean))
                    parsed[key] = boolean;
                else if (int.TryParse(value, out int integer))
                    parsed[key] = integer;
                else
                    parsed[key] = value;
            }
            arguments = parsed;
            return true;
        }

        private static bool TryReadCommandJson(string value, out JToken parsed)
        {
            parsed = null;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(value)))
                {
                    // JSON strings must remain strings even when their text resembles an ISO date.
                    reader.DateParseHandling = DateParseHandling.None;
                    var token = JToken.ReadFrom(reader);
                    if (reader.Read() || !(token is JArray || token is JObject))
                        return false;
                    parsed = token;
                    return true;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static bool TryTokenizeCommand(string line, out List<string> tokens, out string error)
        {
            tokens = new List<string>();
            error = null;
            var current = new StringBuilder();
            var closers = new Stack<char>();
            bool quoted = false;
            bool escaped = false;
            bool structured = false;
            int equals = -1;

            foreach (char c in line ?? string.Empty)
            {
                if (quoted)
                {
                    current.Append(c);
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
                        quoted = false;
                    continue;
                }
                if (c == '"')
                {
                    quoted = true;
                    current.Append(c);
                    continue;
                }
                if (char.IsWhiteSpace(c) && closers.Count == 0)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    equals = -1;
                    structured = false;
                    continue;
                }

                // Only unquoted JSON at the start of a key's value changes token boundaries.
                // Bare semantic subjects and ordinary strings may contain literal brackets.
                if (c == '=' && equals < 0)
                    equals = current.Length;
                else if (equals >= 0 && current.Length == equals + 1 && (c == '[' || c == '{'))
                    structured = true;

                if (structured)
                {
                    if (c == '[' || c == '{')
                        closers.Push(c == '[' ? ']' : '}');
                    else if (c == ']' || c == '}')
                    {
                        if (closers.Count == 0 || closers.Pop() != c)
                        {
                            error = InvalidCommandJson(current.ToString().Substring(0, equals));
                            return false;
                        }
                    }
                }
                current.Append(c);
            }

            if (structured && (quoted || closers.Count > 0))
            {
                error = InvalidCommandJson(current.ToString().Substring(0, equals));
                return false;
            }
            if (quoted)
            {
                error = McpLocalization.Get("command_parsing.unterminated_quote");
                return false;
            }
            if (current.Length > 0)
                tokens.Add(current.ToString());
            return true;
        }

        private static string InvalidCommandJson(string key)
        {
            return McpLocalization.Format("command_parsing.invalid_json", null, key);
        }
    }
}
