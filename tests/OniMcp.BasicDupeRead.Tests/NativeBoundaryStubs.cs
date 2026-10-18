using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Klei.AI
{
    public sealed class Amount
    {
        public string Id;
        public string Name;
    }
    public sealed class AmountInstance
    {
        public Amount amount;
        public float value;
    }
    public sealed class Amounts
    {
        public IEnumerable<AmountInstance> ModifierList;
    }
}

public sealed class Health
{
    public float hitPoints;
    public float maxHitPoints;
}

public sealed class KPrefabID
{
    public int InstanceID;
}

public sealed class MinionResume
{
    public Dictionary<string, bool> MasteryBySkillID = new Dictionary<string, bool>();
    public int AvailableSkillpoints;
}

public sealed class MinionIdentity
{
    public readonly Dictionary<Type, object> Components = new Dictionary<Type, object>();
    public Klei.AI.Amounts NativeAmounts;
    public string ProperName;
    public int WorldId;
    public bool ThrowAmounts;
    public bool ThrowHealth;
    public int NativeAmountReads;
    public int InvalidAmountComponentReads;
    public int HealthReads;

    public T GetComponent<T>() where T : class
    {
        if (typeof(T) == typeof(Klei.AI.Amounts))
        {
            InvalidAmountComponentReads++;
            return null;
        }
        if (typeof(T) == typeof(Health))
        {
            HealthReads++;
            if (ThrowHealth) throw new InvalidOperationException("health fixture");
        }
        object value;
        return Components.TryGetValue(typeof(T), out value) ? value as T : null;
    }
    public string GetProperName() { return ProperName; }
    public int GetMyWorldId() { return WorldId; }
}

namespace Klei.AI
{
    public static class ModifiersExtensions
    {
        public static Amounts GetAmounts(this MinionIdentity minion)
        {
            minion.NativeAmountReads++;
            if (minion.ThrowAmounts) throw new InvalidOperationException("amount fixture");
            return minion.NativeAmounts;
        }
    }
}

public sealed class MinionCollection
{
    public List<MinionIdentity> Items = new List<MinionIdentity>();
}

public static class Components
{
    public static MinionCollection LiveMinionIdentities = new MinionCollection();
}

public sealed class Game
{
    public static Game Instance;
}

namespace OniMcp.Core
{
    public sealed class McpToolParameter { }
    public sealed class McpTool
    {
        public string Name;
        public string Group;
        public string Mode;
        public string Risk;
        public List<string> Aliases;
        public bool Hidden;
        public string Description;
        public Dictionary<string, McpToolParameter> Parameters;
        public Func<JObject, CallToolResult> Handler;
    }
    public sealed class CallToolResult
    {
        public bool IsError;
        public string Value;
        public static CallToolResult Text(string value) { return new CallToolResult { Value = value }; }
        public static CallToolResult Error(string value) { return new CallToolResult { Value = value, IsError = true }; }
    }
}

internal sealed class ThrowingAmounts : IEnumerable<Klei.AI.AmountInstance>
{
    public IEnumerator<Klei.AI.AmountInstance> GetEnumerator()
    {
        yield return new Klei.AI.AmountInstance { amount = new Klei.AI.Amount { Id = "Stress", Name = "Stress" }, value = 25f };
        throw new InvalidOperationException("enumeration fixture");
    }
    IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
}
