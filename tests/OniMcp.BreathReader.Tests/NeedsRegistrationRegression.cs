using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Tools;

internal static class NeedsRegistrationRegression
{
    private static readonly string[] CandidateKeys = { "stamina", "calories", "bladder" };
    private static readonly string[] CandidateIds = { "Stamina", "Calories", "Bladder" };
    private static readonly string[] CommonIds = { "Breath", "Stress", "Temperature" };
    private static readonly string[] BaseIds = { "HitPoints", "ImmuneLevel", "Breath", "Stress", "Toxicity", "Temperature", "Decor", "RadiationBalance" };
    private static readonly string[] BionicIds = { "BionicOil", "BionicGunk", "BionicInternalBattery", "BionicOxygenTank" };
    private static int checks;
    private static string current;

    internal static void Run()
    {
        ActualHandlerIntegration();
        OrdinaryAndStockBionic();
        PresentInstancesWin();
        MissingAndMalformed();
        OwnerAndLifecycleBoundaries();
        EvidenceResetAndRisk();
        Console.WriteLine(JsonConvert.SerializeObject(new { status = "pass", suite = "native_needs_registration", checks, additionalWholeProductLinks = 2, sourceExtraction = false, nativeBoundarySubstituted = true }));
    }

    private static void ActualHandlerIntegration()
    {
        foreach (bool bionic in new[] { true, false })
        {
            current = "compiled_explicit_registration_projection_" + bionic;
            var f = new Fixture(bionic);
            var compact = StatusItem(true, "compact");
            Check(compact.SelectTokens("$..registrationEvidence").Count() == 3, "compiled_environment_registration_evidence_missing: explicit compact requires exactly three proofs");
            foreach (string key in CandidateKeys)
            {
                var e = compact["needsEvidence"][key];
                Check(e["registrationEvidence"] != null, "compiled_environment_registration_evidence_missing:" + key);
                Check((string)e["status"] == (bionic ? "not_applicable" : "observed"), "explicit projection does not change native applicability");
                Check(bionic ? compact["needs"][key].Type == JTokenType.Null : compact["needs"][key].Type != JTokenType.Null, "legacy numeric/null values stay distinct");
            }
            var basic = StatusItem(null, null);
            Check(!basic["needsEvidence"].SelectTokens("$..registrationEvidence").Any()
                && !basic.SelectTokens("$..registrationEvidence").Any(), "compiled_default_registration_evidence_leak");
            SameDiagnosticProjection(basic, compact);
            var falseWithFull = StatusItem(false, "full");
            Check(!falseWithFull.SelectTokens("$..registrationEvidence").Any(), "detailMode alone does not enable proof projection");
            SameDiagnosticProjection(basic, falseWithFull);
            Check(f.Instance("Breath").DisplayReads == 0 && f.Instance("Breath").MaxReads == 0, "default and compact do not opt into Breath formatter or maximum");
            var full = StatusItem(true, "full");
            Check(full.SelectTokens("$..registrationEvidence").Count() == 3, "explicit full requires exactly three registration proofs");
            SameDiagnosticProjection(basic, full);
            foreach (string key in CandidateKeys)
                Check(JToken.DeepEquals(compact["needsEvidence"][key]["registrationEvidence"], full["needsEvidence"][key]["registrationEvidence"]), "compact/full proof contents agree");
            Check((bool)basic["needsAssessmentComplete"] && basic["unknownNeeds"].Count() == 0, "classification and coverage do not depend on proof projection");
            if (bionic)
                Check((string)basic["risk"] != "ok" && basic["reasons"].Any(x => (string)x == "bionic_needs_unavailable"), "omitting proof does not hide missing native monitors");
            Check(Db.GetCalls == 0 && NativeCanary.ForbiddenCalls == 0, "actual whole Environment remains passive");

            current = "direct_serializer_registration_opt_in_" + bionic;
            var n = NativeKeyNeedsRead.Read(f.Dupe);
            var noProof = JObject.FromObject(n.EvidenceDictionary());
            var withProof = JObject.FromObject(n.EvidenceDictionary(true));
            Check(!noProof.SelectTokens("$..registrationEvidence").Any(), "EvidenceDictionary default omits nested proofs");
            Check(withProof.SelectTokens("$..registrationEvidence").Count() == 3, "EvidenceDictionary explicit true includes only three proofs");
            Check(JToken.DeepEquals(noProof, WithoutRegistrationProof(withProof)), "direct evidence status/source/reason/unit are projection-independent");
            foreach (string key in CandidateKeys)
            {
                var defaultReading = JObject.FromObject(n[key].ToDictionary());
                var explicitReading = JObject.FromObject(n[key].ToDictionary(true));
                Check(defaultReading["registrationEvidence"] == null && explicitReading["registrationEvidence"] != null, "NeedReading serializer also defaults to no proof");
                explicitReading.Remove("registrationEvidence");
                Check(JToken.DeepEquals(defaultReading, explicitReading), "NeedReading other fields do not change");
            }
        }
    }

    private static JObject StatusItem(bool? includeDetails, string detailMode)
    {
        var args = new JObject { ["action"] = "status_check" };
        if (includeDetails.HasValue) args["includeDetails"] = includeDetails.Value;
        if (detailMode != null) args["detailMode"] = detailMode;
        var r = DuplicantTools.ControlDupeInfo().Handler(args);
        Check(!r.IsError && r.Content.Count == 1, "actual status handler succeeds");
        return (JObject)JToken.Parse(r.Content[0].Text)["items"][0];
    }

    private static JObject WithoutRegistrationProof(JToken evidence)
    {
        var result = (JObject)evidence.DeepClone();
        foreach (string key in CandidateKeys)
        {
            var item = (JObject)result[key];
            if (item != null) item.Remove("registrationEvidence");
        }
        return result;
    }

    private static void SameDiagnosticProjection(JObject basic, JObject detailed)
    {
        foreach (string key in new[] { "needs", "needsAssessmentComplete", "unknownNeeds", "risk", "reasons", "bionicNeedsAssessmentComplete" })
            Check(JToken.DeepEquals(basic[key], detailed[key]), "proof projection preserves " + key);
        Check(JToken.DeepEquals(basic["needsEvidence"], WithoutRegistrationProof(detailed["needsEvidence"])), "only three known nested proof members differ in needsEvidence");
    }

    private static void OrdinaryAndStockBionic()
    {
        current = "ordinary_native_profile";
        var f = new Fixture(false);
        var n = Read(f);
        for (int i = 0; i < CandidateKeys.Length; i++)
        {
            Check(n[CandidateKeys[i]].Status == "observed", "ordinary candidate is observed");
            var e = Registration(n, CandidateKeys[i]);
            Check((bool)e["configured"] && (int)e["configuredOccurrences"] == 1 && (int)e["instanceCount"] == 1, "ordinary positive registration evidence");
            Check(!(bool)e["stockBionicProfileVerified"] && (string)e["reasonCode"] == "native_standard_model", "ordinary model is not the bionic negative profile");
            Check(e["profileInstantiatedCount"] == null, "unattempted stock profile does not report measured0");
        }
        Check(n.IsComplete && n.UnknownFields.Length == 0, "ordinary six observed needs cover the assessment");
        current = "stock_bionic_profile";
        f = new Fixture(true); n = Read(f);
        foreach (string key in CandidateKeys) NotApplicable(n, key);
        Check(n.IsComplete && n.UnknownFields.Length == 0, "three witnessed exclusions and three real values cover common needs");
        Check(n.RiskWithCoverage(new List<string>()) == "ok", "complete common coverage may be normal only in declared local scope");
        foreach (string key in new[] { "breath", "stress", "bodyTemperature" })
            Check(n[key].Status == "observed" && !Evidence(n, key).ContainsKey("registrationEvidence"), "noncandidate keeps actual value without added applicability projection");
        Check(f.Instance("HitPoints").ValueReads == 0 && f.Instance("BionicOil").ValueReads == 0, "profile witnesses identity without reading unrelated values");
        current = "native_runtime_disease_addition";
        f = new Fixture(true); f.AddExtra(new Amount { Id = "FixtureDisease", Name = "fixture native disease amount" }, 4);
        n = Read(f); foreach (string key in CandidateKeys) NotApplicable(n, key);
        current = "configured_order_not_policy";
        f = new Fixture(true); f.Modifiers.initialAmounts.Reverse();
        n = Read(f); foreach (string key in CandidateKeys) NotApplicable(n, key);
    }

    private static void PresentInstancesWin()
    {
        for (int i = 0; i < CandidateKeys.Length; i++)
        {
            string key = CandidateKeys[i], id = CandidateIds[i];
            foreach (bool knownModel in new[] { true, false })
            {
                current = "ordinary_or_unknown_model_real0_" + id;
                var ordinary = new Fixture(false);
                if (!knownModel) ordinary.Dupe.model = new Tag { Name = "FixtureUnknownModel" };
                ordinary.Instance(id).Raw = 0;
                var observed = Read(ordinary);
                Check(observed[key].Status == "observed" && observed[key].Value == 0, "real0 survives ordinary or unknown model");
            }
            foreach (float value in new[] { 0f, 17.1269f, -2f })
            {
                current = "present_candidate_" + id + "_" + value;
                var f = new Fixture(true); f.Add(id, value);
                var n = Read(f);
                Check(n[key].Status == "observed" && n[key].Value == value, "real canonical instance wins even when not initially configured");
                Check(!(bool)Registration(n, key)["configured"] && (int)Registration(n, key)["instanceCount"] == 1, "added runtime instance remains separate from initial configuration");
                Check((bool)Evidence(n, key)["available"] && n[key].Reason == null, "finite0 is observed, not missing");
            }
            foreach (float bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                current = "invalid_present_candidate_" + id;
                var f = new Fixture(true); f.Add(id, bad); var n = Read(f);
                Unknown(n, key, "non_finite_value");
                Check((int)Registration(n, key)["instanceCount"] == 1, "nonfinite real instance is not hidden as absence");
            }
            current = "present_value_exception_" + id;
            var throwing = new Fixture(true); throwing.Add(id, 0).ThrowValue = true;
            Unknown(Read(throwing), key, "read_failed");
            current = "present_units_exception_" + id;
            throwing = new Fixture(true); throwing.Add(id, 4); throwing.DefinitionsById[id].ThrowUnits = true;
            Unknown(Read(throwing), key, "read_failed");
            current = "registered_but_missing_" + id;
            var missing = new Fixture(true); missing.Modifiers.initialAmounts.Add(id);
            Unknown(Read(missing), key, "registered_amount_missing");
            current = "same_name_is_not_same_definition_" + id;
            var mismatch = new Fixture(true); mismatch.AddExtra(new Amount { Id = id, Name = id }, 66);
            Unknown(Read(mismatch), key, "amount_definition_mismatch");
            current = "duplicate_runtime_candidate_" + id;
            var duplicate = new Fixture(true); duplicate.Add(id, 1); duplicate.Add(id, 99);
            Unknown(Read(duplicate), key, "amount_ambiguous");
            current = "wrong_candidate_definition_id_" + id;
            var wrong = new Fixture(true); wrong.DefinitionsById[id].Id = "Other" + id;
            Unknown(Read(wrong), key, "amount_definition_unavailable");
        }
        current = "same_localized_name_decoy_ignored";
        var decoy = new Fixture(true); decoy.AddExtra(new Amount { Id = "FixtureOtherAmount", Name = "Stamina" }, 777);
        NotApplicable(Read(decoy), "stamina");
    }

    private static void MissingAndMalformed()
    {
        var modifications = new Dictionary<string, Action<Fixture>>
        {
            ["null_initial"] = f => f.Modifiers.initialAmounts = null,
            ["short_profile"] = f => f.Modifiers.initialAmounts.RemoveAt(0),
            ["extra_configured"] = f => f.Modifiers.initialAmounts.Add("FixtureOther"),
            ["duplicate_initial"] = f => f.Modifiers.initialAmounts[0] = f.Modifiers.initialAmounts[1],
            ["empty_initial"] = f => f.Modifiers.initialAmounts[0] = "",
            ["null_initial_id"] = f => f.Modifiers.initialAmounts[0] = null,
            ["null_runtime_entry"] = f => f.Dupe.Amounts.ModifierList.Add(null),
            ["null_runtime_definition"] = f => f.Dupe.Amounts.ModifierList.Add(new AmountInstance()),
            ["empty_runtime_id"] = f => f.AddExtra(new Amount { Id = "", Name = "bad" }, 0),
            ["duplicate_unrelated_runtime"] = f => f.AddExtra(f.DefinitionsById["HitPoints"], 100),
            ["registered_profile_not_instantiated"] = f => f.Remove("HitPoints"),
            ["last_registered_profile_not_instantiated"] = f => f.Remove("BionicOxygenTank"),
            ["profile_definition_missing"] = f => f.Database.Amounts.HitPoints = null,
            ["profile_definition_wrong_reference"] = f => f.Database.Amounts.HitPoints = new Amount { Id = "HitPoints" },
            ["model_unknown"] = f => f.Dupe.model = new Tag { Name = "FixtureUnknownModel" },
            ["model_invalid"] = f => f.Dupe.model = default(Tag),
            ["model_standard_with_bionic_profile"] = f => f.Dupe.model = GameTags.Minions.Models.Standard,
            ["prefab_missing"] = f => f.Dupe.Parts.Remove(typeof(KPrefabID)),
            ["prefab_standard_with_bionic_model"] = f => f.Id.PrefabTag = GameTags.Minions.Models.Standard,
            ["prefab_hash_alias_consumer_mismatch"] = f => f.Id.PrefabTag = new Tag { Name = "FixtureAlias", FixtureEqualityHash = GameTags.Minions.Models.Bionic.GetHashCode() }
        };
        foreach (var pair in modifications)
        {
            current = pair.Key;
            var f = new Fixture(true); pair.Value(f); var n = Read(f);
            foreach (string key in CandidateKeys)
            {
                Check(n[key].Status == "unknown" && !n[key].IsResolved, "unproven absence remains unknown: " + pair.Key);
                Check(!(bool)Registration(n, key)["stockBionicProfileVerified"], "malformed/profile conflict cannot witness N/A");
                Check(Registration(n, key)["profileInstantiatedCount"] == null, "failed stock profile does not report an unmeasured0 or partial prefix count");
            }
            Check(!n.IsComplete, "unproven candidate keeps assessment incomplete");
        }
        foreach (string absent in new[] { "database", "definitions", "runtime_amounts", "runtime_list", "generic_owner" })
        {
            current = "absent_" + absent;
            var f = new Fixture(true);
            if (absent == "database") Db.FixtureSet(null);
            if (absent == "definitions") f.Database.Amounts = null;
            if (absent == "runtime_amounts") f.Modifiers.amounts = null;
            if (absent == "runtime_list") f.Dupe.Amounts.ModifierList = null;
            if (absent == "generic_owner") f.Dupe.Parts.Remove(typeof(Modifiers));
            var n = Read(f);
            foreach (string key in CandidateKeys)
                Check(n[key].Status == "unknown" && !Evidence(n, key).ContainsKey("registrationEvidence"), "unavailable common owner/cache has no fabricated evidence");
        }
        current = "missing_breath_stays_unknown_on_bionic";
        var breathMissing = new Fixture(true); breathMissing.Remove("Breath");
        var unknown = Read(breathMissing);
        Unknown(unknown, "breath", "amount_missing");
        foreach (string key in CandidateKeys) Check(unknown[key].Status == "unknown", "partial instantiated profile blocks candidate exclusion");
        current = "missing_candidate_definition";
        var definitionMissing = new Fixture(true); definitionMissing.Database.Amounts.Stamina = null;
        Unknown(Read(definitionMissing), "stamina", "amount_definition_unavailable");
    }

    private static void OwnerAndLifecycleBoundaries()
    {
        foreach (string mode in new[] { "dupe_not_initialized", "dupe_not_spawned", "modifier_not_initialized", "modifier_not_spawned", "identity_modifier_mismatch", "minion_component_absent" })
        {
            current = mode;
            var f = new Fixture(true); f.Instance("Breath").Raw = 20;
            if (mode == "dupe_not_initialized") f.Dupe.FixtureInitialized = false;
            if (mode == "dupe_not_spawned") f.Dupe.FixtureSpawned = false;
            if (mode == "modifier_not_initialized") f.Modifiers.FixtureInitialized = false;
            if (mode == "modifier_not_spawned") f.Modifiers.FixtureSpawned = false;
            if (mode == "identity_modifier_mismatch") f.Dupe.modifiers = new Modifiers();
            if (mode == "minion_component_absent") f.Dupe.Parts.Remove(typeof(MinionModifiers));
            var n = Read(f);
            foreach (string key in CandidateKeys) Check(n[key].Status == "unknown", "unverified lifecycle/negative owner cannot exempt needs");
            Check(n.Breath == 20, "profile uncertainty cannot erase actual low breath");
            var reasons = new List<string>(); n.AddRiskReasons(reasons);
            Check(n.RiskWithCoverage(reasons, false) == "critical" && reasons.Contains("low_breath"), "actual low breath outranks profile/monitor uncertainty");
        }
        current = "generic_modifiers_value_owner";
        var generic = new Fixture(true);
        generic.Dupe.Parts.Remove(typeof(MinionModifiers));
        var valueOwner = new Modifiers { amounts = generic.Dupe.Amounts, initialAmounts = new List<string>(generic.Modifiers.initialAmounts) };
        valueOwner.FixtureOwner = generic.Dupe.gameObject;
        generic.Dupe.Parts[typeof(Modifiers)] = valueOwner; generic.Dupe.modifiers = valueOwner;
        generic.Instance("Breath").Raw = 0;
        var g = Read(generic);
        Check(g.Breath == 0 && g["breath"].Status == "observed", "valid generic Modifiers preserves real0 without MinionModifiers");
        foreach (string key in CandidateKeys) Check(g[key].Status == "unknown", "generic owner is not stock negative-profile proof");
        current = "wrong_gameobject_owner";
        var wrongOwner = new Fixture(true); wrongOwner.Modifiers.FixtureOwner = new UnityEngine.GameObject();
        var result = Read(wrongOwner);
        foreach (string key in CandidateKeys) Unknown(result, key, "amount_owner_unavailable");
        current = "foreign_amounts_container";
        var foreignAmounts = new Fixture(true); foreignAmounts.Dupe.Amounts.FixtureOwner = new UnityEngine.GameObject();
        result = Read(foreignAmounts);
        foreach (string key in new[] { "stamina", "calories", "stress", "bladder", "breath", "bodyTemperature" })
        {
            Unknown(result, key, "amount_owner_mismatch");
            Check(!Evidence(result, key).ContainsKey("registrationEvidence"), "foreign Amounts cannot supply values or negative applicability");
        }
        current = "different_amounts_reference";
        var wrongAmounts = new Fixture(true); wrongAmounts.Modifiers.amounts = new Amounts();
        result = Read(wrongAmounts);
        foreach (string key in CandidateKeys) Unknown(result, key, "amounts_unavailable");
        current = "destroyed_generic_component";
        var destroyed = new Fixture(true); destroyed.Modifiers.FixtureDestroyed = true;
        result = Read(destroyed);
        foreach (string key in CandidateKeys) Unknown(result, key, "amount_owner_unavailable");
        current = "owner_changes_during_native_value_getter";
        var changed = new Fixture(true);
        changed.Instance("Breath").FixtureOnValueRead = () => changed.Dupe.Parts[typeof(Modifiers)] = new Modifiers();
        result = NativeKeyNeedsRead.Read(changed.Dupe);
        foreach (string key in CandidateKeys)
        {
            Unknown(result, key, "amount_owner_changed");
            Check(!Evidence(result, key).ContainsKey("registrationEvidence"), "post-owner failure clears earlier registration claims");
        }
        Check(result.Breath == null, "post-owner failure clears stale value rather than mixing owners");
        current = "container_owner_changes_during_value_getter";
        changed = new Fixture(true);
        changed.Instance("Breath").FixtureOnValueRead = () => changed.Dupe.Amounts.FixtureOwner = new UnityEngine.GameObject();
        result = NativeKeyNeedsRead.Read(changed.Dupe);
        foreach (string key in CandidateKeys) Unknown(result, key, "amount_owner_changed");
        Check(result.Breath == null, "post-container-owner change cannot retain prior-owner scalar");
    }

    private static void EvidenceResetAndRisk()
    {
        current = "fresh_profile_change_not_cached";
        var f = new Fixture(true); var n = Read(f); NotApplicable(n, "stamina");
        f.Modifiers.initialAmounts.Add("Stamina");
        n = Read(f); Unknown(n, "stamina", "registered_amount_missing");
        f.Add("Stamina", 0); n = Read(f);
        Check(n["stamina"].Status == "observed" && n.Stamina == 0, "later real0 replaces negative evidence");
        current = "NeedReading_retry_resets_evidence";
        f = new Fixture(true); n = Read(f); var reading = n["stamina"];
        reading.Read(() => null);
        Unknown(n, "stamina", "amount_missing");
        Check(!Evidence(n, "stamina").ContainsKey("registrationEvidence"), "Read clears prior applicability evidence");
        reading.Read(() => new RawNeedSample { Value = 0, Unit = "Flat" });
        Check(reading.Status == "observed" && reading.Value == 0, "Read can observe0 after unknown");
        current = "candidate_only_negative_setter";
        n["breath"].Read(() => null);
        n["breath"].SetRegistrationEvidence(new Dictionary<string, object>(), true);
        Unknown(n, "breath", "amount_missing");
        foreach (string reason in new[] { "bionic_battery_empty", "bionic_oxygen_empty", "bionic_oil_empty", "bionic_gunk_urgent" })
        {
            current = "known_risk_" + reason;
            f = new Fixture(true); n = Read(f); var reasons = new List<string> { reason };
            Check(n.RiskWithCoverage(reasons, false) == (reason.EndsWith("battery_empty", StringComparison.Ordinal) || reason.EndsWith("oxygen_empty", StringComparison.Ordinal) ? "critical" : "warning"), "known risk is not erased by applicability or missing monitor");
            Check(reasons.Contains("bionic_needs_unavailable"), "monitor completeness remains independent");
        }
        current = "resolved_common_needs_not_all_bionic_monitors";
        f = new Fixture(true); n = Read(f); var empty = new List<string>();
        Check(n.RiskWithCoverage(empty, false) == "unknown" && empty.Contains("bionic_needs_unavailable"), "not-applicable common needs cannot imply all bionic resources normal");
    }

    private static KeyNeeds Read(Fixture f)
    {
        var initial = f.ValueOwner == null || f.ValueOwner.initialAmounts == null ? null : f.ValueOwner.initialAmounts.ToArray();
        var runtime = f.Dupe.Amounts == null || f.Dupe.Amounts.ModifierList == null ? null : f.Dupe.Amounts.ModifierList.ToArray();
        var model = f.Dupe.model;
        int forbidden = NativeCanary.ForbiddenCalls, lazy = Db.GetCalls, smi = f.Dupe.SmiReads;
        var result = NativeKeyNeedsRead.Read(f.Dupe);
        Check(NativeCanary.ForbiddenCalls == forbidden && Db.GetCalls == lazy, "no initializer/lazyDB fallback calls");
        Check(f.Dupe.SmiReads == smi, "registration reader creates or reads no monitors");
        if (initial != null) Check(initial.SequenceEqual(f.ValueOwner.initialAmounts), "initial registration list remains unchanged");
        if (runtime != null) Check(runtime.SequenceEqual(f.Dupe.Amounts.ModifierList), "runtime registration list remains unchanged");
        Check(f.Dupe.model == model, "model is not repaired");
        foreach (var instance in f.CreatedInstances)
            Check(instance.MaxReads == 0 && instance.DisplayReads == 0 && instance.MinReads == 0 && instance.DeltaReads == 0, "registration adds no max/formatter/min/delta calls");
        return result;
    }

    private static void NotApplicable(KeyNeeds n, string key)
    {
        Check(n[key].Status == "not_applicable" && n[key].Reason == "native_initial_amount_not_configured", "only witnessed stock absence becomes N/A");
        Check(n[key].Value == null && n.ToDictionary()[key] == null && n[key].IsResolved, "N/A keeps legacy null and is distinct from unresolved");
        var e = Evidence(n, key); var r = Registration(n, key);
        Check(!(bool)e["available"] && (string)e["source"] == NeedReading.RegistrationSource, "N/A uses registration provenance, not numeric success");
        Check((bool)r["stockBionicProfileVerified"] && !(bool)r["configured"] && (int)r["configuredOccurrences"] == 0 && (int)r["instanceCount"] == 0, "profile and candidate absence are explicit");
        Check((int)r["profileRegisteredCount"] == 12 && (int)r["profileInstantiatedCount"] == 12, "fully instantiated stock profile is independently witnessed");
    }

    private static void Unknown(KeyNeeds n, string key, string reason)
    {
        Check(n[key].Status == "unknown" && n[key].Value == null && !n[key].IsResolved, "unavailable stays unresolved: " + key);
        Check(n[key].Reason == reason, "bounded reason preserved: " + key + " expected " + reason + " got " + n[key].Reason);
    }

    private static Dictionary<string, object> Evidence(KeyNeeds n, string key) { return (Dictionary<string, object>)n.EvidenceDictionary(true)[key]; }
    private static Dictionary<string, object> Registration(KeyNeeds n, string key) { return (Dictionary<string, object>)Evidence(n, key)["registrationEvidence"]; }
    private static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) throw new InvalidOperationException("Native registration " + current + ": " + message);
    }

    private sealed class Fixture
    {
        internal readonly MinionIdentity Dupe = new MinionIdentity();
        internal readonly MinionModifiers Modifiers = new MinionModifiers();
        internal readonly KPrefabID Id = new KPrefabID { InstanceID = 702 };
        internal readonly Dictionary<string, Amount> DefinitionsById = new Dictionary<string, Amount>(StringComparer.Ordinal);
        internal readonly List<AmountInstance> CreatedInstances = new List<AmountInstance>();
        internal readonly Db Database;
        internal Modifiers ValueOwner { get { return Dupe.GetComponent<Modifiers>(); } }

        internal Fixture(bool bionic)
        {
            var definitions = new Database.Amounts();
            foreach (var field in typeof(Database.Amounts).GetFields())
            {
                var definition = new Amount { Id = field.Name, Name = "native fixture " + field.Name };
                field.SetValue(definitions, definition); DefinitionsById.Add(field.Name, definition);
            }
            Dupe.model = bionic ? GameTags.Minions.Models.Bionic : GameTags.Minions.Models.Standard;
            Id.PrefabTag = Dupe.model;
            Dupe.Amounts.FixtureOwner = Dupe.gameObject;
            Modifiers.FixtureOwner = Dupe.gameObject; Modifiers.amounts = Dupe.Amounts; Dupe.modifiers = Modifiers;
            Dupe.Parts[typeof(Modifiers)] = Modifiers; Dupe.Parts[typeof(MinionModifiers)] = Modifiers;
            Dupe.Parts[typeof(KPrefabID)] = Id; Dupe.Parts[typeof(Navigator)] = new Navigator();
            foreach (string id in BaseIds.Concat(bionic ? BionicIds : CandidateIds))
            {
                Modifiers.initialAmounts.Add(id);
                Add(id, id == "Breath" ? 180 : id == "Temperature" ? 310 : id == "Calories" ? 2000000 : 60);
            }
            Database = new Db { Amounts = definitions }; Db.FixtureSet(Database);
            ToolUtil.Selected = Dupe;
            Components.LiveMinionIdentities.Items.Clear(); Components.LiveMinionIdentities.Items.Add(Dupe);
        }

        internal AmountInstance Add(string id, float value) { return AddExtra(DefinitionsById[id], value); }
        internal AmountInstance AddExtra(Amount definition, float value)
        {
            var instance = new AmountInstance { amount = definition, Raw = value };
            CreatedInstances.Add(instance); Dupe.Amounts.ModifierList.Add(instance);
            if (!Dupe.NativeAmounts.ContainsKey(definition)) Dupe.NativeAmounts.Add(definition, instance);
            return instance;
        }
        internal AmountInstance Instance(string id) { return Dupe.Amounts.ModifierList.First(x => x != null && x.amount != null && x.amount.Id == id); }
        internal void Remove(string id)
        {
            Dupe.Amounts.ModifierList.RemoveAll(x => x != null && x.amount != null && x.amount.Id == id);
            var keys = Dupe.NativeAmounts.Keys.Where(x => x.Id == id).ToArray();
            foreach (var key in keys) Dupe.NativeAmounts.Remove(key);
        }
    }
}
