using System;
using System.Collections.Generic;
using System.Linq;
using Klei.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OniMcp.Support;
using OniMcp.Tools;

internal static class Program
{
    private static int assertions;
    private static void Check(bool condition, string description)
    {
        assertions++;
        if (!condition) throw new Exception(description);
    }
    private static Dictionary<string, object> Part(Dictionary<string, object> read, string key)
    {
        return (Dictionary<string, object>)read[key];
    }
    private static bool HasReason(Dictionary<string, object> evidence, string reason)
    {
        return ((List<string>)evidence["reasonCodes"]).Contains(reason);
    }
    private static MinionIdentity Minion(params AmountInstance[] amounts)
    {
        var minion = new MinionIdentity
        {
            ProperName = "Reader fixture", WorldId = 0,
            NativeAmounts = new Amounts { ModifierList = amounts }
        };
        minion.Components[typeof(KPrefabID)] = new KPrefabID { InstanceID = 101 };
        minion.Components[typeof(Health)] = new Health { hitPoints = 70f, maxHitPoints = 100f };
        return minion;
    }
    private static AmountInstance Amount(string id, string name, float value)
    {
        return new AmountInstance { amount = new Amount { Id = id, Name = name }, value = value };
    }
    private static void ExpectAmountRead(Dictionary<string, object> read, string state, bool available, string description)
    {
        var evidence = Part(read, "amountsEvidence");
        Check((string)evidence["readState"] == state, description + ": state");
        Check((bool)evidence["available"] == available, description + ": coverage");
    }
    private static void ExpectHealth(Dictionary<string, object> read, string state, bool available, string description)
    {
        var health = Part(read, "health");
        Check((string)health["readState"] == state, description + ": state");
        Check((bool)health["available"] == available, description + ": coverage");
    }

    private static void Main()
    {
        McpLocalization.LanguageProvider = () => "en";
        var dupe = Minion(Amount("Stress", "ストレス", 0f), Amount("Breath", "呼吸", 200f), Amount("Temperature", "体温", 310.15f));
        var read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "available", true, "complete native amounts");
        Check((float)Part(read, "amounts")["ストレス"] == 0f, "localized actual zero survives");
        Check((float)Part(read, "amountsById")["Stress"] == 0f, "native ID actual zero survives");
        Check((float)Part(read, "amounts")["呼吸"] == 200f, "raw breath is not normalized as percent");
        Check((float)Part(read, "amountsById")["Temperature"] == 310.15f, "raw temperature is not converted");
        Check((string)Part(read, "amountsEvidence")["source"] == "MinionIdentity.GetAmounts().ModifierList", "actual native amount source");
        Check(((List<string>)Part(read, "amountsEvidence")["reasonCodes"]).Count == 0, "complete native read has no missing reasons");
        Check(dupe.NativeAmountReads == 1 && dupe.InvalidAmountComponentReads == 0, "uses native Modifiers read and never component Amounts");
        ExpectHealth(read, "available", true, "complete native Health");
        Check((float)Part(read, "health")["hitPoints"] == 70f && (float)Part(read, "health")["maxHitPoints"] == 100f, "HP uses Health, independently of Amount ranges");
        Check((string)Part(read, "health")["source"] == "Health.hitPoints/maxHitPoints", "Health native source");
        Check(dupe.HealthReads == 1, "Health read is bounded to one component lookup");

        dupe = Minion(Amount("Stress", "shared", 15f), Amount("BionicGunk", "shared", 8f));
        read = BasicDuplicantVitals.Read(dupe);
        Check(Part(read, "amounts").Count == 1 && (float)Part(read, "amounts")["shared"] == 8f, "legacy localized-name last value behavior remains");
        Check(Part(read, "amountsById").Count == 2, "native IDs do not lose colliding localized names");
        Check((float)Part(read, "amountsById")["Stress"] == 15f && (float)Part(read, "amountsById")["BionicGunk"] == 8f, "colliding names retain distinct raw native values");
        ExpectAmountRead(read, "available", true, "same display names are distinct native amounts");

        dupe = Minion();
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "available", true, "successfully acquired empty collection");
        Check(Part(read, "amounts").Count == 0 && Part(read, "amountsById").Count == 0, "real empty is not populated with defaults");
        dupe.NativeAmounts = null;
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "unavailable", false, "missing native amounts");
        Check(HasReason(Part(read, "amountsEvidence"), "amounts_missing"), "missing amounts explain empty legacy object");
        Check(Part(read, "amountsEvidence")["source"] == null, "missing amounts do not claim an acquired source");
        dupe.NativeAmounts = new Amounts { ModifierList = null };
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "unavailable", false, "missing native collection");
        Check(HasReason(Part(read, "amountsEvidence"), "collection_missing"), "missing collection differs from real empty");
        dupe.ThrowAmounts = true;
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "unavailable", false, "failed native acquisition");
        Check(HasReason(Part(read, "amountsEvidence"), "read_failed"), "native acquisition exception is explicit");
        Check(Part(read, "amounts").Count == 0 && Part(read, "amountsById").Count == 0, "failed acquisition has no manufactured values");
        ExpectHealth(read, "available", true, "failed amounts do not hide independent observed Health");

        dupe = Minion();
        dupe.NativeAmounts.ModifierList = new ThrowingAmounts();
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "unavailable", false, "failed mid-enumeration");
        Check(HasReason(Part(read, "amountsEvidence"), "read_failed"), "enumeration exception is explicit");
        Check(Part(read, "amounts").Count == 0 && Part(read, "amountsById").Count == 0, "interrupted enumeration cannot look complete or retain stale values");

        dupe = Minion(Amount("Stress", "Stress", 0f), null, new AmountInstance(), Amount("Breath", "Breath", 200f));
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "partial", false, "invalid entries beside valid amounts");
        Check(HasReason(Part(read, "amountsEvidence"), "invalid_entry"), "invalid definitions have a reason code");
        Check(((List<string>)Part(read, "amountsEvidence")["reasonCodes"]).Count == 1, "repeated failures use bounded distinct codes");
        Check(Part(read, "amountsById").Count == 2, "invalid entries do not hide observed neighbors");

        foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            dupe = Minion(Amount("Stress", "Stress", invalid), Amount("Breath", "Breath", 0f));
            read = BasicDuplicantVitals.Read(dupe);
            ExpectAmountRead(read, "partial", false, "non-finite amount with known neighbor");
            Check(Part(read, "amounts").ContainsKey("Stress") && Part(read, "amounts")["Stress"] == null, "localized unknown is explicit null");
            Check(Part(read, "amountsById").ContainsKey("Stress") && Part(read, "amountsById")["Stress"] == null, "ID unknown is explicit null");
            Check((float)Part(read, "amountsById")["Breath"] == 0f, "known neighbor zero remains observed");
            Check(HasReason(Part(read, "amountsEvidence"), "non_finite_value"), "non-finite amount reason");
            string json = JsonConvert.SerializeObject(read, McpJsonUtil.Settings);
            var data = JObject.Parse(json);
            Check(data["amounts"]["Stress"].Type == JTokenType.Null && data["amountsById"]["Stress"].Type == JTokenType.Null, "actual serializer retains explicit null keys");
            Check(!json.Contains("NaN") && !json.Contains("Infinity"), "neither view emits non-finite JSON tokens");
            dupe = Minion(Amount("Stress", "Stress", invalid));
            ExpectAmountRead(BasicDuplicantVitals.Read(dupe), "unavailable", false, "no finite amount values");
        }

        dupe = Minion(Amount("Stress", null, 10f), Amount(null, "Breath", 20f));
        read = BasicDuplicantVitals.Read(dupe);
        ExpectAmountRead(read, "partial", false, "incomplete native identities");
        Check((float)Part(read, "amountsById")["Stress"] == 10f, "missing localized name does not discard native-ID value");
        Check((float)Part(read, "amounts")["Breath"] == 20f, "missing native ID does not discard legacy localized value");
        Check(HasReason(Part(read, "amountsEvidence"), "name_missing") && HasReason(Part(read, "amountsEvidence"), "id_missing"), "identity omissions are explicit");

        dupe = Minion(Amount("Stress", "Stress", 0f));
        dupe.Components.Remove(typeof(Health));
        read = BasicDuplicantVitals.Read(dupe);
        ExpectHealth(read, "unavailable", false, "missing Health");
        Check(Part(read, "health")["hitPoints"] == null && Part(read, "health")["maxHitPoints"] == null, "missing HP does not become healthy or zero");
        Check(Part(read, "health")["source"] == null && HasReason(Part(read, "health"), "component_missing"), "missing Health evidence");
        ExpectAmountRead(read, "available", true, "missing Health does not hide independent amounts");
        dupe.ThrowHealth = true;
        read = BasicDuplicantVitals.Read(dupe);
        ExpectHealth(read, "unavailable", false, "Health acquisition failure");
        Check(HasReason(Part(read, "health"), "read_failed"), "Health exception reason");

        foreach (float current in new[] { 0f, -1f, 70f, 150f })
        {
            dupe = Minion();
            ((Health)dupe.Components[typeof(Health)]).hitPoints = current;
            read = BasicDuplicantVitals.Read(dupe);
            ExpectHealth(read, "available", true, "finite raw HP");
            Check((float)Part(read, "health")["hitPoints"] == current, "native HP is not defaulted, clamped or diagnosed");
        }
        foreach (float maximum in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            dupe = Minion();
            ((Health)dupe.Components[typeof(Health)]).maxHitPoints = maximum;
            read = BasicDuplicantVitals.Read(dupe);
            ExpectHealth(read, "partial", false, "invalid or unobserved maximum HP");
            Check((float)Part(read, "health")["hitPoints"] == 70f, "valid current HP survives invalid maximum");
            Check(Part(read, "health")["maxHitPoints"] == null, "invalid max does not become zero or an invented maximum");
            Check(HasReason(Part(read, "health"), maximum == 0f || maximum == -1f ? "invalid_maximum" : "non_finite_maximum"), "invalid maximum evidence");
        }
        foreach (float current in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            dupe = Minion();
            ((Health)dupe.Components[typeof(Health)]).hitPoints = current;
            read = BasicDuplicantVitals.Read(dupe);
            ExpectHealth(read, "partial", false, "unobserved current HP with known maximum");
            Check(Part(read, "health")["hitPoints"] == null && (float)Part(read, "health")["maxHitPoints"] == 100f, "partial Health preserves only observed raw field");
            Check(HasReason(Part(read, "health"), "non_finite_hit_points"), "current HP missing evidence");
            ((Health)dupe.Components[typeof(Health)]).maxHitPoints = float.NaN;
            read = BasicDuplicantVitals.Read(dupe);
            ExpectHealth(read, "unavailable", false, "no finite Health fields");
        }

        read = BasicDuplicantVitals.Read(null);
        ExpectAmountRead(read, "unavailable", false, "missing minion amounts");
        ExpectHealth(read, "unavailable", false, "missing minion Health");

        // Exercise the actual production tool handler and serializer, not a test replica.
        Game.Instance = new Game();
        Components.LiveMinionIdentities.Items.Clear();
        var first = Minion(Amount("Stress", "ストレス", 0f), Amount("Breath", "呼吸", 200f));
        var resume = new MinionResume { AvailableSkillpoints = 3 };
        resume.MasteryBySkillID["Researching1"] = true;
        resume.MasteryBySkillID["Researching2"] = false;
        first.Components[typeof(MinionResume)] = resume;
        var second = Minion(); second.WorldId = 7; second.ProperName = "Bionic fixture";
        second.NativeAmounts = null; second.Components.Remove(typeof(Health)); second.Components.Remove(typeof(KPrefabID));
        Components.LiveMinionIdentities.Items.Add(first);
        Components.LiveMinionIdentities.Items.Add(null);
        Components.LiveMinionIdentities.Items.Add(second);
        var result = ColonyTools.GetDuplicants().Handler(new JObject { ["worldId"] = 7 });
        Check(!result.IsError, "actual list handler succeeds with mixed availability");
        var list = JArray.Parse(result.Value);
        Check(list.Count == 2, "existing list scope remains global and ignores null residents");
        Check((int)list[0]["id"] == 101 && (string)list[0]["name"] == "Reader fixture", "legacy identity contract");
        Check((int)list[0]["availableSkillPoints"] == 3 && list[0]["skillsMastered"].Count() == 1 && (string)list[0]["skillsMastered"][0] == "Researching1", "legacy skills and points contract");
        Check((int)list[0]["worldId"] == 0 && (int)list[1]["worldId"] == 7, "legacy world identity retained");
        Check((int)list[1]["id"] == -1 && (int)list[1]["availableSkillPoints"] == 0, "unrelated legacy fallbacks unchanged");
        Check((float)list[0]["amounts"]["ストレス"] == 0f && (float)list[0]["amountsById"]["Breath"] == 200f, "actual handler exposes both compatible display names and machine IDs");
        Check((bool)list[0]["health"]["available"] && (float)list[0]["health"]["hitPoints"] == 70f, "actual handler serializes observed Health");
        Check(list[1]["health"]["hitPoints"].Type == JTokenType.Null && list[1]["health"]["maxHitPoints"].Type == JTokenType.Null, "actual handler serializes explicit unknown Health");
        Check(!(bool)list[1]["amountsEvidence"]["available"] && (string)list[1]["amountsEvidence"]["readState"] == "unavailable", "actual handler explains missing legacy amounts");
        Check(first.NativeAmountReads == 1 && second.NativeAmountReads == 1 && first.InvalidAmountComponentReads == 0 && second.InvalidAmountComponentReads == 0, "each actual handler row uses only native amount route");
        Check(first.HealthReads == 1 && second.HealthReads == 1, "each actual handler row has one bounded Health acquisition");
        Check(Components.LiveMinionIdentities.Items.Count == 3 && resume.AvailableSkillpoints == 3 && resume.MasteryBySkillID["Researching1"], "reads preserve residents and skills");
        Check(((Health)first.Components[typeof(Health)]).hitPoints == 70f && first.NativeAmounts.ModifierList.First().value == 0f, "reads preserve native health and amounts");
        first.ThrowAmounts = true;
        var retry = JArray.Parse(ColonyTools.GetDuplicants().Handler(new JObject()).Value);
        Check(!retry[0]["amounts"].HasValues && !retry[0]["amountsById"].HasValues && !(bool)retry[0]["amountsEvidence"]["available"], "a failed later read does not reuse previous successful values");
        Components.LiveMinionIdentities.Items.Clear();
        Check(JArray.Parse(ColonyTools.GetDuplicants().Handler(new JObject()).Value).Count == 0, "empty resident list stays empty");
        Game.Instance = null;
        Check(ColonyTools.GetDuplicants().Handler(new JObject()).IsError, "game-uninitialized guard remains before native reads");
        foreach (string language in new[] { "en", "ja", "zh-Hans" })
        {
            string selected = language;
            McpLocalization.LanguageProvider = () => selected;
            var tool = ColonyTools.GetDuplicants();
            Check(tool.Description == McpLocalization.GetForLanguage(language, "warnings.legacy_alias")
                .Replace("{0}", "dupes_list").Replace("{1}", "0.3.0").Replace("{2}", "colony_control domain=read action=dupes"), "runtime description uses the selected prose locale");
            Check(tool.Description.Contains("dupes_list") && tool.Description.Contains("0.3.0")
                && tool.Description.Contains("colony_control domain=read action=dupes"), "localized legacy warning preserves protocol and version");
            var error = tool.Handler(new JObject());
            Check(error.IsError && error.Value == McpLocalization.GetForLanguage(language, "errors.game_not_initialized"), "runtime guard error uses the selected prose locale");
            Check(error.Value != "errors.game_not_initialized", "runtime guard locale never exposes an unread key");
            Check(tool.Name == "dupes_list" && tool.Group == "dupes" && tool.Mode == "read" && tool.Risk == "none" && tool.Hidden, "localized tool keeps protocol identity and read-only mode");
        }
        McpLocalization.LanguageProvider = null;
        Console.WriteLine("Basic duplicant read adapter: " + assertions + " assertions passed");
    }
}
