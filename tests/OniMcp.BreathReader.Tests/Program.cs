using System;
using System.Collections.Generic;
using System.Reflection;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class Program
{
    private static int checks;
    private static string currentCase;
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0].StartsWith("--route-probe=", StringComparison.Ordinal))
            {
                var f = new Fixture();
                string route = args[0].Substring("--route-probe=".Length);
                currentCase = "route_" + route;
                var details = route == "needs" ? Needs(f) : Full(f);
                Check(details != null, "explicit " + route + " must expose Breath details");
                Check(f.Def.Lookups == 1 && f.Instance.MaxReads == 1 && f.Instance.DisplayReads == 1, "explicit route must reach native lookup/max/formatter");
            }
            else
            {
                Run();
                NeedsRegistrationRegression.Run();
            }
            Check(NativeCanary.ForbiddenCalls == 0 && Db.GetCalls == 0, "lazy database/component/write canaries remain zero");
            Console.WriteLine(JsonConvert.SerializeObject(new { status = "pass", checks, fullProductFiles = 4, sourceExtraction = false, forbidden = NativeCanary.ForbiddenCalls, lazyDbCalls = Db.GetCalls }));
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(JsonConvert.SerializeObject(new { status = "fail", checks, currentCase, exception = ex.GetType().Name, message = ex.Message }));
            return 1;
        }
    }

    private static void Run()
    {
        currentCase = "known_zero_and_modified_maximum";
        var f = new Fixture(); f.Instance.Raw = 0;
        var r = Needs(f);
        Check(r != null, "explicit needs must expose Breath details");
        Check((double)r["value"] == 0 && (double)r["maximum"] == 200, "zero and individual maximum survive");
        Check((string)r["displayValue"] == "native-marker-41", "native formatter output is not constructed or normalized");
        Check((string)r["nativeUnits"] == "Flat" && r["rawUnit"].Type == JTokenType.Null, "native Flat and unknown physical unit remain distinct");
        Check((string)r["rawUnitUnavailableReason"] == "native_physical_unit_not_exposed", "unknown physical unit has a reason");
        Check((bool)r["available"] && (string)r["status"] == "observed", "known zero is an observed result");
        Check(r["minimum"] == null && r["attributeDeltaPerSecond"] == null && r["rangeFraction"] == null, "no new min/delta/fraction projection");
        NumericSources(r, true, true);
        NoMinDelta(f);
        Check(f.Def.Lookups == 1 && f.Instance.DisplayReads == 1, "one explicit fixed lookup and formatter read");

        currentCase = "full_status_route";
        f = new Fixture(); r = Full(f);
        Check(r != null && (string)r["displayValue"] == f.Instance.Display, "full status reaches actual summary and reader");
        Check(f.Def.Lookups == 1 && f.Instance.MaxReads == 1 && f.Instance.DisplayReads == 1, "full status opts in once");
        NoMinDelta(f);

        currentCase = "raw_and_existing_rounding";
        f = new Fixture(); f.Instance.Raw = 35.1269f; r = Needs(f);
        var n = new KeyNeeds(); n["breath"].Read(() => new RawNeedSample { Value = f.Instance.Raw, Unit = "Flat" });
        Check((double)r["value"] == (double)f.Instance.Raw && (double)n.ToDictionary()["breath"] == 35.13d, "detail keeps native float while existing diagnostics round separately");
        Check((double)r["maximum"] == 200, "maximum does not become base 100 or percent");

        foreach (float raw in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            currentCase = "raw_nonfinite_" + raw;
            f = new Fixture(); f.Instance.Raw = raw; r = Needs(f);
            Check(r["value"].Type == JTokenType.Null && (double)r["maximum"] == 200, "nonfinite raw is unknown but known maximum survives");
            Check(r["displayValue"].Type == JTokenType.Null && f.Instance.DisplayReads == 0, "invalid raw does not call formatter");
            Check((string)r["unavailableReasons"]["value"] == "non_finite_value" && (string)r["status"] == "partial", "nonfinite raw has partial evidence");
            NumericSources(r, false, true); NoMinDelta(f);
        }
        currentCase = "raw_getter_exception";
        f = new Fixture(); f.Dupe.Amounts.ModifierList.Clear(); f.Instance.ThrowValue = true; r = Needs(f);
        Check(r["value"].Type == JTokenType.Null && (double)r["maximum"] == 200 && f.Instance.DisplayReads == 0, "reader value failure retains independent max and skips display");
        Check((string)r["unavailableReasons"]["value"] == "read_failed:InvalidOperationException", "value failure reason is explicit");

        foreach (float maximum in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            currentCase = "maximum_nonfinite_" + maximum;
            f = new Fixture(); f.Instance.Maximum = maximum; r = Needs(f);
            Check((double)r["value"] == (double)f.Instance.Raw && r["maximum"].Type == JTokenType.Null, "invalid maximum cannot erase raw");
            Check(f.Instance.DisplayReads == 0 && (string)r["unavailableReasons"]["maximum"] == "non_finite_value", "invalid maximum blocks formatting with reason");
            NumericSources(r, true, false); NoMinDelta(f);
        }
        currentCase = "maximum_exception";
        f = new Fixture(); f.Instance.ThrowMaximum = true; r = Needs(f);
        Check((double)r["value"] == (double)f.Instance.Raw && r["maximum"].Type == JTokenType.Null && f.Instance.DisplayReads == 0, "max failure keeps known raw and does not format");
        Check((string)r["unavailableReasons"]["maximum"] == "read_failed:InvalidOperationException", "max failure reason is explicit");
        foreach (float max in new[] { 0f, -5f })
        {
            currentCase = "finite_maximum_" + max;
            f = new Fixture(); f.Instance.Maximum = max; r = Needs(f);
            Check((double)r["maximum"] == max && f.Instance.DisplayReads == 1, "finite max is observed without a new validity policy");
        }

        foreach (string display in new string[] { null, "" })
        {
            currentCase = "empty_formatter";
            f = new Fixture(); f.Instance.Display = display; r = Needs(f);
            Check(r["displayValue"].Type == JTokenType.Null && (string)r["unavailableReasons"]["displayValue"] == "empty_native_display", "missing display has its own reason");
            Check((double)r["value"] == (double)f.Instance.Raw && (double)r["maximum"] == 200, "missing display preserves raw/max");
        }
        currentCase = "formatter_exception";
        f = new Fixture(); f.Instance.ThrowDisplay = true; r = Needs(f);
        Check((string)r["unavailableReasons"]["displayValue"] == "read_failed:InvalidOperationException" && r["fieldSources"]["displayValue"].Type == JTokenType.Null, "formatter failure has no successful source");
        Check((double)r["value"] == (double)f.Instance.Raw && (double)r["maximum"] == 200, "formatter exception preserves numbers");
        currentCase = "units_exception";
        f = new Fixture(); f.Def.ThrowUnits = true; r = Needs(f);
        Check(r["nativeUnits"].Type == JTokenType.Null && (string)r["unavailableReasons"]["nativeUnits"] == "read_failed:InvalidOperationException", "units failure does not invent Flat");
        Check((double)r["value"] == (double)f.Instance.Raw && (string)r["displayValue"] == f.Instance.Display, "independent unit failure preserves raw/native display");

        foreach (string missing in new[] { "db", "definitions", "breath", "instance", "lookup_exception" })
        {
            currentCase = "missing_" + missing; f = new Fixture();
            if (missing == "db") Db.FixtureSet(null);
            if (missing == "definitions") f.Database.Amounts = null;
            if (missing == "breath") f.Database.Amounts.Breath = null;
            if (missing == "instance") f.Dupe.NativeAmounts.Clear();
            if (missing == "lookup_exception") f.Def.ThrowLookup = true;
            r = Needs(f);
            foreach (string field in new[] { "value", "maximum", "nativeUnits", "displayValue" })
                Check(r[field].Type == JTokenType.Null && r["fieldSources"][field].Type == JTokenType.Null && r["unavailableReasons"][field] != null, "missing lookup keeps " + field + " unknown");
            Check((string)r["status"] == "unknown" && !(bool)r["available"] && f.Instance.MaxReads == 0 && f.Instance.DisplayReads == 0, "missing lookup never invokes max/formatter");
            NoMinDelta(f);
        }
        currentCase = "fixed_definition_identity";
        f = new Fixture(); var decoy = new Amount { Id = "Breath", Name = "Breath" };
        f.Dupe.NativeAmounts.Add(decoy, new AmountInstance { amount = decoy, Raw = 999 });
        r = Needs(f);
        Check((double)r["value"] == (double)f.Instance.Raw && decoy.Lookups == 0, "same ID/name decoy cannot replace fixed native definition");
        f.Dupe.NativeAmounts.Clear(); r = Needs(f);
        Check(r["value"].Type == JTokenType.Null && (string)r["status"] == "unknown", "a later missing read cannot reuse previous success");

        foreach (string mode in new[] { "default", "full_without_include", "compact", "detail" })
        {
            currentCase = "non_optin_" + mode; f = new Fixture();
            var args = new JObject { ["action"] = mode == "detail" ? "detail" : "status_check" };
            if (mode == "full_without_include") args["detailMode"] = "full";
            if (mode == "compact") { args["includeDetails"] = true; args["detailMode"] = "compact"; }
            Call(args);
            Check(f.Def.Lookups == 0 && f.Instance.MaxReads == 0 && f.Instance.DisplayReads == 0, "non opt-in has no added lookup/max/formatter: " + mode);
            NoMinDelta(f);
        }
        currentCase = "explicit_bionic_missing_normal_amount";
        f = new Fixture(); f.Id.PrefabTag = new Tag { Name = "BionicMinion" }; f.Dupe.NativeAmounts.Clear(); f.Dupe.Amounts.ModifierList.Clear();
        r = Needs(f);
        Check((string)r["status"] == "unknown" && r["value"].Type == JTokenType.Null, "bionic name alone does not infer not-applicable");
        Check(f.Def.Lookups == 1 && f.Instance.MaxReads == 0 && f.Instance.DisplayReads == 0, "explicit missing normal amount looks up once without numeric getters");
        currentCase = "default_bionic_reader";
        f = new Fixture(); f.Id.PrefabTag = new Tag { Name = "BionicMinion" };
        var method = typeof(DuplicantTools).GetMethod("NativeBionicNeeds", BindingFlags.NonPublic | BindingFlags.Static);
        Check(method != null, "full product bionic member is compiled");
        method.Invoke(null, new object[] { f.Dupe, true });
        Check(f.Def.Lookups == 0 && f.Instance.MaxReads == 0 && f.Instance.DisplayReads == 0, "legacy bionic default does not gain normal breath getters");
    }

    private static JObject Needs(Fixture fixture)
    {
        var result = Call(new JObject { ["action"] = "needs" });
        return result[0]?["amountDetails"]?["Breath"] as JObject;
    }
    private static JObject Full(Fixture fixture)
    {
        var result = Call(new JObject { ["action"] = "status_check", ["includeDetails"] = true, ["detailMode"] = "full" });
        return result["items"]?[0]?["needsDetail"]?["amountDetails"]?["Breath"] as JObject;
    }
    private static JToken Call(JObject args)
    {
        var result = DuplicantTools.ControlDupeInfo().Handler(args);
        Check(!result.IsError && result.Content.Count == 1, "real control Handler returns one successful payload");
        string text = result.Content[0].Text;
        Check(text.IndexOf("NaN", StringComparison.Ordinal) < 0 && text.IndexOf("Infinity", StringComparison.Ordinal) < 0, "serializer cannot leak nonfinite numbers");
        return JToken.Parse(text);
    }
    private static void NumericSources(JObject r, bool value, bool maximum)
    {
        Check(value ? (string)r["fieldSources"]["value"] == "AmountInstance.value" : r["fieldSources"]["value"].Type == JTokenType.Null, "value source follows actual observation");
        Check(maximum ? (string)r["fieldSources"]["maximum"] == "AmountInstance.GetMax()" : r["fieldSources"]["maximum"].Type == JTokenType.Null, "max source follows actual observation");
    }
    private static void NoMinDelta(Fixture f) { Check(f.Instance.MinReads == 0 && f.Instance.DeltaReads == 0 && f.Dupe.InvalidComponentReads == 0, "normal detail adds no min/delta/component path"); }
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new InvalidOperationException(message); }
    private sealed class Fixture
    {
        internal readonly Amount Def = new Amount();
        internal readonly AmountInstance Instance;
        internal readonly MinionIdentity Dupe = new MinionIdentity();
        internal readonly KPrefabID Id = new KPrefabID { InstanceID = 701, PrefabTag = new Tag { Name = "Minion" } };
        internal readonly Db Database;
        internal Fixture()
        {
            Instance = new AmountInstance { amount = Def };
            Dupe.NativeAmounts.Add(Def, Instance); Dupe.Amounts.ModifierList.Add(Instance);
            Dupe.Parts.Add(typeof(KPrefabID), Id); Dupe.Parts.Add(typeof(Navigator), new Navigator());
            Database = new Db { Amounts = new Database.Amounts { Breath = Def, Stress = new Amount { Id = "Stress" } } };
            Db.FixtureSet(Database); ToolUtil.Selected = Dupe;
            Components.LiveMinionIdentities.Items.Clear(); Components.LiveMinionIdentities.Items.Add(Dupe);
        }
    }
}
