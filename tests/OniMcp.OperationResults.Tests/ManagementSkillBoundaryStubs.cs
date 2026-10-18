using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;

namespace OniMcp.Tools
{
    // The native skill list is supplied data, not a host implementation of skill rules.
    internal static class ManagementSkillBoundary
    {
        internal static string Text;
        internal static int Calls;
        internal static JObject Arguments;
        internal static JObject ReceivedReference;
        internal static bool MutateArguments;

        internal static void Reset(string text, bool mutateArguments = false)
        {
            Text = text;
            Calls = 0;
            Arguments = null;
            ReceivedReference = null;
            MutateArguments = mutateArguments;
        }

        internal static CallToolResult Call(JObject args)
        {
            if ((string)args["domain"] != "skill" || (string)args["action"] != "list")
                throw new NotSupportedException("Only the native skill-list boundary is available.");
            Calls++;
            Arguments = (JObject)args.DeepClone();
            ReceivedReference = args;
            if (MutateArguments)
            {
                args["id"] = -123;
                var query = args["query"] as JObject;
                if (query != null) query["format"] = "changed by native fixture";
                var limit = args["limit"] as JArray;
                if (limit != null) limit.Add("changed by native fixture");
            }
            return CallToolResult.Text(Text);
        }
    }

    public static partial class WorldEditorTools
    {
        internal static CallToolResult ReadManagementForTest(JObject args, string relative = "management/skills.md")
            => ReadManagementMarkdown(args, "/active/" + relative, relative);
        internal static bool WantsManagementJsonForTest(JObject args) => WantsManagementJson(args);

        // Only to compile unrelated duplicant-file rendering in the linked full source.
        // Any accidental execution of that game/screen path must fail this host test.
        private static IEnumerable<UnusedManagementDupe> LiveDupes() => throw new NotSupportedException();
        private static string GetDupeDetailFileName(UnusedManagementDupe dupe) => throw new NotSupportedException();
    }

    internal sealed class UnusedManagementDupe
    {
        internal string GetProperName() => throw new NotSupportedException();
        internal T GetComponent<T>() => throw new NotSupportedException();
        internal int GetMyWorldId() => throw new NotSupportedException();
        internal UnusedManagementTransform transform => throw new NotSupportedException();
    }

    internal sealed class UnusedManagementTransform
    {
        internal object GetPosition() => throw new NotSupportedException();
    }

}
