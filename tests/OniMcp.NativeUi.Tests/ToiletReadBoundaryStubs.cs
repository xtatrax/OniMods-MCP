using System;
using System.Collections.Generic;
using UnityEngine;

// Only native objects and the shared metadata/row formatters are fixture boundaries.
internal class StateMachine
{
    internal class BaseState { public string name; }
    internal class Instance
    {
        public int CurrentStateReads;
        public virtual BaseState GetCurrentState() { CurrentStateReads++; return null; }
    }
}
internal sealed class Chore { }
internal class Toilet : Component
{
    private readonly int serializedFlushes;
    private readonly int serializedMax;
    public int MasterUsedReads, MasterMaxReads, SetterCalls, RawReads, LazyReads, FactoryCalls;
    public bool ThrowUsed, ThrowMax, ThrowRaw;
    public bool PoisonLazyGetter = true;
    public StateMachine.Instance Cache;
    public Toilet(int used = 0, int max = 15) { serializedFlushes = used; serializedMax = max; }
    public int SerializedFlushesForTest { get { return serializedFlushes; } }
    public int SerializedMaxForTest { get { return serializedMax; } }
    public int FlushesUsed
    {
        get { MasterUsedReads++; if (ThrowUsed) throw new InvalidOperationException("master_used_fault"); return serializedFlushes; }
        set { SetterCalls++; throw new InvalidOperationException("forbidden_flushes_setter"); }
    }
    public int maxFlushes
    {
        get { MasterMaxReads++; if (ThrowMax) throw new InvalidOperationException("master_max_fault"); return serializedMax; }
    }
    public virtual StateMachine.Instance GetSMI()
    {
        RawReads++;
        if (ThrowRaw) throw new InvalidOperationException("raw_cache_fault");
        return Cache;
    }
    public StatesInstance smi
    {
        get
        {
            LazyReads++;
            if (PoisonLazyGetter) throw new InvalidOperationException("forbidden_lazy_smi_getter");
            if (Cache == null) { FactoryCalls++; Cache = new StatesInstance(this); }
            return (StatesInstance)Cache;
        }
    }
    internal sealed class States
    {
        public StateMachine.BaseState full = new StateMachine.BaseState { name = "full" };
        public StateMachine.BaseState other = new StateMachine.BaseState { name = "not_full" };
        public StateMachine.BaseState earlyclean = new StateMachine.BaseState { name = "earlyclean" };
    }
    internal class StatesInstance : StateMachine.Instance
    {
        private readonly Toilet owner;
        public States sm = new States();
        public StateMachine.BaseState CurrentState;
        public int OwnerReads, RemainingReads, SoiledReads, CloggedReads, ChoreReads, StartCalls, GoToCalls, RefreshCalls;
        public bool ThrowOwner, ThrowState, ThrowRemaining, ThrowSoiled, ThrowClogged, ThrowChore;
        public bool Clogged;
        public Chore CleanChoreForTest;
        public StatesInstance(Toilet target) { owner = target; CurrentState = sm.other; }
        public Toilet master
        {
            get { OwnerReads++; if (ThrowOwner) throw new InvalidOperationException("owner_fault"); return owner; }
        }
        public override StateMachine.BaseState GetCurrentState()
        {
            CurrentStateReads++;
            if (ThrowState) throw new InvalidOperationException("current_state_fault");
            return CurrentState;
        }
        public int GetFlushesRemaining()
        {
            RemainingReads++;
            if (ThrowRemaining) throw new InvalidOperationException("remaining_fault");
            return owner.maxFlushes - owner.FlushesUsed;
        }
        public bool IsSoiled
        {
            get { SoiledReads++; if (ThrowSoiled) throw new InvalidOperationException("soiled_fault"); return owner.FlushesUsed > 0; }
        }
        public bool IsCloggedWithGunk
        {
            get { CloggedReads++; if (ThrowClogged || sm == null) throw new IndexOutOfRangeException("parameter_context_fault"); return Clogged; }
        }
        public Chore cleanChore
        {
            get { ChoreReads++; if (ThrowChore) throw new InvalidOperationException("chore_fault"); return CleanChoreForTest; }
        }
        public void StartSM() { StartCalls++; throw new InvalidOperationException("forbidden_start"); }
        public void GoTo(StateMachine.BaseState state) { GoToCalls++; throw new InvalidOperationException("forbidden_transition"); }
        public void Refresh() { RefreshCalls++; throw new InvalidOperationException("forbidden_refresh"); }
    }
}
internal sealed class InheritedToilet : Toilet
{
    public int OverrideCalls;
    public InheritedToilet(int used = 0, int max = 15) : base(used, max) { }
    public override StateMachine.Instance GetSMI() { OverrideCalls++; throw new InvalidOperationException("derived_raw_getter_must_not_run"); }
}
internal sealed class InheritedToiletState : Toilet.StatesInstance
{
    public InheritedToiletState(Toilet owner) : base(owner) { }
    public override StateMachine.BaseState GetCurrentState() { CurrentStateReads++; throw new InvalidOperationException("derived_state_reader_must_not_run"); }
}
internal sealed class ForeignToiletState : StateMachine.Instance { }
namespace STRINGS
{
    internal static class UI
    {
        internal static class USERMENUACTIONS
        {
            internal static class CLEANTOILET { public static string NAME = "Native toilet clean"; }
        }
    }
}
namespace OniMcp.Tools
{
    public static partial class MaintenanceActionTools
    {
        private static Dictionary<string, object> TargetInfo(GameObject target)
        {
            return new Dictionary<string, object>
            {
                ["id"] = target.Id, ["name"] = target.name, ["prefabId"] = "Outhouse", ["fixtureMetadata"] = true
            };
        }
        private static Dictionary<string, object> ActionInfo(string key, string title, string category,
            string componentType, bool canExecute, Dictionary<string, object> state)
        {
            return new Dictionary<string, object>
            {
                ["actionKey"] = key, ["title"] = title, ["category"] = category,
                ["componentType"] = componentType, ["canExecute"] = canExecute, ["state"] = state
            };
        }
        internal static Dictionary<string, object> ReadToiletInfoForTest(Toilet toilet) { return ToiletInfo(toilet); }
        internal static bool MatchesToiletQueryForTest(Dictionary<string, object> info, string query)
        {
#if TOILET_LEGACY_COMPARISON
            return string.IsNullOrWhiteSpace(query)
                || Newtonsoft.Json.JsonConvert.SerializeObject(info).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
#else
            return MatchesMaintenanceQueryWithToiletAlias(info, query);
#endif
        }
        internal static Dictionary<string, object> ReadToiletActionForTest(Toilet toilet)
        {
#if TOILET_LEGACY_COMPARISON
            return ActionInfo("clean_toilet", STRINGS.UI.USERMENUACTIONS.CLEANTOILET.NAME,
                "maintenance", "Toilet", CanCleanToilet(toilet), ToiletInfo(toilet));
#else
            return ToiletActionInfo(toilet);
#endif
        }
    }
}
