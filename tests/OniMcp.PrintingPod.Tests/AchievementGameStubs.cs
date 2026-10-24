using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
using UnityEngine;

// Native boundary scaffolding only. No achievement read/projector is stubbed here.
internal static class AchievementNativePoison
{
    internal static int Evaluations, UiActions, StatusConstructions;
    internal static Exception Evaluation(string method)
    {
        Evaluations++;
        return new InvalidOperationException("achievement_evaluation_forbidden:" + method);
    }
    internal static Exception Ui(string method)
    {
        UiActions++;
        return new InvalidOperationException("unrelated_native_action_forbidden:" + method);
    }
}

namespace Database
{
    public abstract class ColonyAchievementRequirement
    {
        public bool shouldUpdateNameAndDescription;
        public abstract bool Success();
        public virtual bool Fail() { throw AchievementNativePoison.Evaluation("Fail"); }
        public virtual string GetProgress(bool complete) { throw AchievementNativePoison.Evaluation("GetProgress"); }
    }
    public sealed class PoisonAchievementRequirement : ColonyAchievementRequirement
    {
        public override bool Success() { throw AchievementNativePoison.Evaluation("Success"); }
    }
    public class ColonyAchievement
    {
        public string Id, Name, description;
        public bool Disabled, isVictoryCondition, ValidForSave = true;
        public List<ColonyAchievementRequirement> requirementChecklist = new List<ColonyAchievementRequirement>();
        public bool IsValidForSave() => ValidForSave;
    }
    public class ColonyAchievements
    {
        public List<ColonyAchievement> resources = new List<ColonyAchievement>();
    }
}

public class Db
{
    private static Db _Instance;
    public static Db Instance { get => _Instance; set => _Instance = value; }
    public static int GetCalls;
    public static bool ThrowOnGet = true;
    public Database.ColonyAchievements ColonyAchievements = new Database.ColonyAchievements();
    public static Db Get()
    {
        GetCalls++;
        if (ThrowOnGet) throw new InvalidOperationException("unexpected_definition_lookup");
        return Instance;
    }
}

public class ColonyAchievementStatus
{
    public bool success, failed;
    public ColonyAchievementStatus(string achievementId) { AchievementNativePoison.StatusConstructions++; }
    public void UpdateAchievement() { throw AchievementNativePoison.Evaluation("UpdateAchievement"); }
}
public class ColonyAchievementTracker
{
    public Dictionary<string, ColonyAchievementStatus> achievements = new Dictionary<string, ColonyAchievementStatus>();
    public List<string> achievementsToDisplay = new List<string>();
    public bool IsAchievementUnlocked(Database.ColonyAchievement achievement)
    {
        throw AchievementNativePoison.Evaluation("IsAchievementUnlocked");
    }
}
public class SaveGame
{
    public static SaveGame Instance;
    public ColonyAchievementTracker ColonyAchievementTracker;
}
public class Game
{
    public static Game Instance;
    public void Trigger(int hash) { throw AchievementNativePoison.Ui("Game.Trigger"); }
}
public class GameClock
{
    public static GameClock Instance;
    public int Cycle = 40;
    public int GetCycle() => Cycle;
}
public partial class Telepad
{
    public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
    public float GetTimeRemaining() => Immigration.Instance?.GetTimeRemaining() ?? 0f;
}
public partial class ImmigrantScreen
{
    public static void InitializeImmigrantScreen(Telepad telepad) { throw AchievementNativePoison.Ui("InitializeImmigrantScreen"); }
}
public class Operational { public bool IsOperational; }
public class GameFlowManager
{
    public static GameFlowManager Instance;
    public bool IsGameOver() => false;
}
public class ManagementMenu
{
    public static ManagementMenu Instance;
    public bool HasActiveResearch;
    public bool CheckHasResearchCenter() => false;
    public void ToggleSkills() { throw AchievementNativePoison.Ui("ToggleSkills"); }
    public void ToggleResearch() { throw AchievementNativePoison.Ui("ToggleResearch"); }
}
public static partial class Components
{
    public static class MinionResumes { public static List<MinionResume> Items = new List<MinionResume>(); }
}
public class MinionResume
{
    public int TotalSkillPointsGained, SkillsMastered;
    public bool HasTag(object tag) => false;
}
public static class GameTags { public static readonly object Dead = new object(); }
public class LoreBearer
{
    public bool hideLore, useDefaultLore, BeenSearched;
    public string poiOverrideLoreUnlockId, content, SidescreenButtonText, SidescreenButtonTooltip;
    public bool SidescreenButtonInteractable() => false;
    public int GetSideScreenSortOrder() => 0;
    public void OnSidescreenButtonPressed() { throw AchievementNativePoison.Ui("LoreBearer.OnSidescreenButtonPressed"); }
}
public class PauseScreen { public static PauseScreen Instance; public Transform transform = new Transform(); }
public static class MainMenu
{
    public static void ActivateRetiredColoniesScreenFromData(GameObject parent, object data)
    {
        throw AchievementNativePoison.Ui("ActivateRetiredColoniesScreenFromData");
    }
}
public static class RetireColonyUtility
{
    public static object GetCurrentColonyRetiredColonyData()
    {
        throw AchievementNativePoison.Ui("GetCurrentColonyRetiredColonyData");
    }
}
namespace UnityEngine
{
    public class Transform { public Transform parent; public GameObject gameObject; }
}
namespace OniMcp.Tools
{
    // Descriptor and unrelated selector scaffolding allows both whole production files to compile.
    public class McpTool
    {
        public string Name, Description, Group, Mode, Risk;
        public bool Hidden;
        public List<string> Aliases, Tags;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public class McpToolParameter
    {
        public string Type, Description;
        public bool Required;
        public List<string> EnumValues;
    }
    public static partial class ToolUtil
    {
        public static Dictionary<string, int> GetRect(JObject args) => null;
        public static int ResolveWorldId(JObject args) => GetInt(args, "worldId") ?? 0;
        public static int ClampLimit(JObject args, int fallback, int max) => Math.Max(1, Math.Min(max, GetInt(args, "limit") ?? fallback));
    }
    public static class LoreReadTools
    {
        public static object SnapshotUnlocks() { throw AchievementNativePoison.Ui("SnapshotUnlocks"); }
        public static object ReadNewUnlocks(object before) { throw AchievementNativePoison.Ui("ReadNewUnlocks"); }
        public static void AddLegacyContent(Dictionary<string, object> result, bool searched, object content)
        {
            throw AchievementNativePoison.Ui("AddLegacyContent");
        }
    }
    public static partial class FacilitySideScreenTools
    {
        // Only opens the actual private production method for assertions; no copied read logic.
        internal static List<Dictionary<string, object>> VictoryRowsForAchievementTest() => VictoryConditionInfos();
        private static Dictionary<string, McpToolParameter> RectParams(Dictionary<string, McpToolParameter> p) => p;
        private static Dictionary<string, McpToolParameter> LookupParams(Dictionary<string, McpToolParameter> p) => p;
        private static CallToolResult ListObjectTargets(JObject args, Func<GameObject, bool> predicate, Func<GameObject, Dictionary<string, object>> selector, string payloadKey)
        {
            throw AchievementNativePoison.Ui("ListObjectTargets");
        }
        private static GameObject FindObjectTarget(JObject args, Func<GameObject, bool> predicate)
        {
            throw AchievementNativePoison.Ui("FindObjectTarget");
        }
        private static bool MatchesTarget(GameObject target, Dictionary<string, int> rect, int worldId) => target != null;
        private static Dictionary<string, object> TargetInfo(GameObject target) => new Dictionary<string, object> { ["name"] = target.GetProperName() };
    }
}
