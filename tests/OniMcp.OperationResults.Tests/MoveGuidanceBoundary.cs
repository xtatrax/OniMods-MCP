using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using OniMcp.Core;
namespace OniMcp.Tools
{
    // Registry membership and native cached objects are input boundaries.
    // The full guidance/dispatchers, parser, edit/preflight and serializer are linked.
    internal static class MoveGuidanceBoundary
    {
        internal static bool Enabled;
        internal static int OperationDispatches,MoveDispatches;
        internal static Func<JObject,MinionIdentity> Resolver;
        internal static JObject SelectorArguments;
        internal static bool TryGetOperation(string name,out McpTool tool)
        {
            tool=name=="world_editor"?WorldEditorTools.ControlWorldEditor():name=="dupes_control"?DuplicantTools.ControlDupes():name=="coordinate_control"?CoordinateControlTools.ControlCoordinate():null;
            return tool!=null;
        }
        internal static CallToolResult Move(JObject args) { MoveDispatches++;throw new NotSupportedException("Native movement must not be dispatched by guidance previews"); }
        internal static CallToolResult Unused(JObject args)=>throw new NotSupportedException("Unrelated native action");
    }
    public static partial class OniToolRegistry
    {
        public static bool IsCoordinateTool(string name)=>name=="coordinate_control"||name=="coordinate_gateway"||name=="coordinate_tool";
    }
    public static partial class ToolUtil
    {
        internal static MinionIdentity FindDupe(JObject args) { MoveGuidanceBoundary.SelectorArguments=(JObject)args.DeepClone();return MoveGuidanceBoundary.Resolver==null?null:MoveGuidanceBoundary.Resolver(args); }
        public static string CleanName(string name)=>name;
    }
    public static partial class DuplicantTools
    {
        // Existing DTO helpers are supplied payloads, not copies of their source.
        private static Dictionary<string,object> DupeRef(MinionIdentity dupe)=>dupe.Reference;
        private static Dictionary<string,object> AssignableToDictionary(Assignable item)=>item.Payload;
        public static McpTool ControlSkill()=>new McpTool { Handler=ManagementSkillBoundary.Call };
        public static McpTool ControlDupeCommands()=>new McpTool { Handler=MoveGuidanceBoundary.Move };
        public static McpTool ControlDupeInfo()=>new McpTool { Handler=MoveGuidanceBoundary.Unused };
        public static McpTool ControlPersonalPriority()=>new McpTool { Handler=MoveGuidanceBoundary.Unused };
        public static McpTool ControlHat()=>new McpTool { Handler=MoveGuidanceBoundary.Unused };
        public static McpTool ControlAssignable()=>new McpTool { Handler=MoveGuidanceBoundary.Unused };
    }
    internal static class FacilitySideScreenTools
    {
        internal static McpTool ListMinionTodos()=>new McpTool { Handler=MoveGuidanceBoundary.Unused };
        internal static McpTool ListBionicUpgrades()=>new McpTool { Handler=MoveGuidanceBoundary.Unused };
    }
    internal static partial class Components
    {
        internal static class AssignableItems { internal static readonly List<Assignable> Items=new List<Assignable>(); }
    }
    internal sealed class NativeDupeObject
    {
        internal readonly Dictionary<Type,object> Components=new Dictionary<Type,object>();
        internal T GetComponent<T>() where T:class { object value;return Components.TryGetValue(typeof(T),out value)?value as T:null; }
    }
    internal sealed class MinionIdentity
    {
        internal NativeDupeObject gameObject=new NativeDupeObject();
        internal string Name = null;
        internal Dictionary<string,object> Reference;
        internal readonly AssignableProxy assignableProxy=new AssignableProxy();
        internal string GetProperName()=>Name;
        internal T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
    }
    internal sealed class AssignableProxy { internal NativeDupeObject Object = null;internal NativeDupeObject Get()=>Object; }
    internal sealed class Navigator { internal MoveToLocationMonitor.Instance Monitor;internal T GetSMI<T>() where T:class=>Monitor as T; }
    internal static class MoveToLocationMonitor { internal sealed class Instance { public void CancelChore()=>throw new NotSupportedException(); } }
    internal sealed class MinionResume { internal int AvailableSkillpoints; }
    internal sealed class ChoreConsumer { public void StopChore()=>throw new NotSupportedException(); }
    internal sealed class Chore { public void Cancel()=>throw new NotSupportedException();public void Cancel(string reason)=>throw new NotSupportedException(); }
    internal sealed class MinionBrain { public void CancelChore(string reason)=>throw new NotSupportedException();public void CancelFetches(string reason)=>throw new NotSupportedException(); }
    internal class Assignable
    {
        internal string slotID = null;
        internal Assignee assignee = null;
        internal bool Assigned = false;
        internal string Name = null;
        internal Dictionary<string,object> Payload = null;
        internal bool IsAssigned()=>Assigned;
        internal string GetProperName()=>Name;
    }
    internal sealed class Equippable:Assignable { internal bool isEquipped = false; }
    internal sealed class Assignee { internal string Name = null;internal bool Null=false;internal bool IsNull()=>Null;internal string GetProperName()=>Name; }
    internal sealed class Equipment { internal readonly List<EquipmentSlot> Slots=new List<EquipmentSlot>(); }
    internal sealed class EquipmentSlot { internal SlotDescriptor slot = null;internal Assignable assignable = null;internal bool Assigned=false;internal bool IsAssigned()=>Assigned; }
    internal sealed class SlotDescriptor { internal string Id = null,Name = null; }
    internal sealed class CameraController
    {
        internal static CameraController Instance=>null;
        internal void CameraGoTo(UnityEngine.Vector3 value,float duration,bool force)=>throw new NotSupportedException();
        internal void SnapTo(UnityEngine.Vector3 value,float zoom)=>throw new NotSupportedException();
    }
}
namespace UnityEngine
{
    internal sealed class Camera { internal static Camera main=>null;internal float orthographicSize {get=>throw new NotSupportedException();set=>throw new NotSupportedException();} }
    internal struct Vector3 { internal Vector3(float x,float y,float z) { } }
}
namespace OniMcp.Server { internal static class MainThreadBridge { internal static void Enqueue(Action action)=>throw new NotSupportedException("Unexpected camera action"); } }
