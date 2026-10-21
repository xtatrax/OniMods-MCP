using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace OniMcp.Support
{
    internal static class OwnedHarmonyPatchAudit
    {
        private const int RowLimit = 128;
        private const string Source = "Harmony.GetAllPatchedMethods/GetPatchInfo";
        private const string Scope = "patch_methods_from_this_assembly";

        private sealed class Group
        {
            internal MethodBase Original;
            internal string Kind;
            internal MethodInfo Method;
            internal int Count;
            internal readonly HashSet<string> Owners = new HashSet<string>(StringComparer.Ordinal);
        }

        private sealed class Unavailable : Exception
        {
            internal readonly string Reason;
            internal Unavailable(string reason) { Reason = reason; }
        }

        internal static Dictionary<string, object> Read()
        {
            try
            {
                var assembly = typeof(OwnedHarmonyPatchAudit).Assembly;
                var originals = Harmony.GetAllPatchedMethods();
                if (originals == null) return Missing("method_list_unavailable");
                var seen = new HashSet<MethodBase>();
                var groups = new Dictionary<System.Tuple<MethodBase, string, MethodInfo>, Group>();
                foreach (MethodBase original in originals)
                {
                    if (original == null) return Missing("original_method_unavailable");
                    if (!seen.Add(original)) return Missing("original_method_duplicate");
                    var info = Harmony.GetPatchInfo(original);
                    if (info == null) return Missing("patch_info_unavailable");
                    Collect(groups, assembly, original, "prefix", info.Prefixes);
                    Collect(groups, assembly, original, "postfix", info.Postfixes);
                    Collect(groups, assembly, original, "transpiler", info.Transpilers);
                    Collect(groups, assembly, original, "finalizer", info.Finalizers);
                }
                var ordered = groups.Values
                    .OrderBy(group => MethodSortKey(group.Original), StringComparer.Ordinal)
                    .ThenBy(group => group.Kind, StringComparer.Ordinal)
                    .ThenBy(group => MethodSortKey(group.Method), StringComparer.Ordinal).ToList();
                var rows = ordered.Take(RowLimit).Select(group => new Dictionary<string, object>
                {
                    ["original"] = MethodDescription(group.Original),
                    ["kind"] = group.Kind,
                    ["patch"] = MethodDescription(group.Method),
                    ["count"] = group.Count,
                    ["owners"] = group.Owners.OrderBy(owner => owner, StringComparer.Ordinal).ToList()
                }).ToList();
                return new Dictionary<string, object>
                {
                    ["available"] = true, ["source"] = Source, ["scope"] = Scope, ["reasonCode"] = null,
                    ["totalEntries"] = ordered.Sum(group => group.Count),
                    ["distinctGroups"] = ordered.Count,
                    ["duplicateGroups"] = ordered.Count(group => group.Count >= 2),
                    ["owners"] = ordered.SelectMany(group => group.Owners).Distinct(StringComparer.Ordinal)
                        .OrderBy(owner => owner, StringComparer.Ordinal).ToList(),
                    ["returned"] = rows.Count, ["limit"] = RowLimit, ["rows"] = rows,
                    ["truncated"] = ordered.Count > RowLimit
                };
            }
            catch (Unavailable error) { return Missing(error.Reason); }
            catch { return Missing("scan_failed"); }
        }

        private static void Collect(Dictionary<System.Tuple<MethodBase, string, MethodInfo>, Group> groups,
            Assembly assembly, MethodBase original, string kind, IEnumerable<Patch> patches)
        {
            if (patches == null) throw new Unavailable("patch_list_unavailable");
            foreach (Patch patch in patches)
            {
                if (patch == null) throw new Unavailable("patch_entry_unavailable");
                // GetMethod can execute a patch factory. Only inspect registration metadata.
                MethodInfo method = patch.PatchMethod;
                if (method == null) throw new Unavailable("patch_method_unavailable");
                if (method.Module.Assembly != assembly) continue;
                if (string.IsNullOrEmpty(patch.owner)) throw new Unavailable("owner_unavailable");
                var key = System.Tuple.Create(original, kind, method);
                Group group;
                if (!groups.TryGetValue(key, out group))
                {
                    group = new Group { Original = original, Kind = kind, Method = method };
                    groups.Add(key, group);
                }
                group.Count++;
                group.Owners.Add(patch.owner);
            }
        }

        private static Dictionary<string, object> MethodDescription(MethodBase method)
        {
            if (method.DeclaringType == null) throw new Unavailable("declaring_type_unavailable");
            return new Dictionary<string, object>
            {
                ["type"] = TypeName(method.DeclaringType),
                ["name"] = method.Name,
                ["signature"] = Signature(method)
            };
        }

        private static string MethodSortKey(MethodBase method)
        {
            if (method.DeclaringType == null) throw new Unavailable("declaring_type_unavailable");
            return TypeName(method.DeclaringType) + "." + method.Name + ":" + Signature(method);
        }

        private static string Signature(MethodBase method)
        {
            var info = method as MethodInfo;
            return TypeName(info == null ? typeof(void) : info.ReturnType) + "("
                + string.Join(",", method.GetParameters().Select(parameter => TypeName(parameter.ParameterType))) + ")";
        }

        private static string TypeName(Type type)
        {
            if (type.IsByRef) return TypeName(type.GetElementType()) + "&";
            if (type.IsPointer) return TypeName(type.GetElementType()) + "*";
            if (type.IsArray) return TypeName(type.GetElementType()) + "[" + new string(',', type.GetArrayRank() - 1) + "]";
            if (type.IsGenericParameter) return type.Name;
            if (type.IsGenericType)
            {
                string name = type.GetGenericTypeDefinition().FullName;
                if (name == null) throw new Unavailable("type_name_unavailable");
                name = Regex.Replace(name, @"`\d+", "");
                return name + "<" + string.Join(",", type.GetGenericArguments().Select(TypeName)) + ">";
            }
            string result = type.FullName;
            if (result == null) throw new Unavailable("type_name_unavailable");
            return result;
        }

        private static Dictionary<string, object> Missing(string reason)
        {
            return new Dictionary<string, object>
            {
                ["available"] = false, ["source"] = Source, ["scope"] = Scope, ["reasonCode"] = reason,
                ["totalEntries"] = null, ["distinctGroups"] = null, ["duplicateGroups"] = null,
                ["owners"] = null, ["returned"] = null, ["limit"] = RowLimit,
                ["rows"] = null, ["truncated"] = null
            };
        }
    }
}
