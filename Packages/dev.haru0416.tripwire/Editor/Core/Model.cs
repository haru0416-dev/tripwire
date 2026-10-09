// Engine-independent description of one trigger component.
// The Unity side converts its serialized data into these types; the code generator only sees these.
// Must compile both inside Unity (C# 9, no init/records) and under plain dotnet for tests.
using System.Collections.Generic;
using System.Linq;

namespace Tripwire.Core
{
    public enum ValueKind
    {
        Bool,
        Int,
        Float,
        String,
        Vector3,
        /// <summary>A UnityEngine.Object (GameObject, Component, asset). See <see cref="ParamType.UnityType"/>.</summary>
        Object,
        Player,
        // Appended later: keep existing values stable (the Unity side mirrors the first five).
        Vector2,
        Color,
        /// <summary>Entered and stored as Euler angles (float[3]); emitted as Quaternion.Euler.</summary>
        Quaternion,
        /// <summary>A Udon-exposed enum (<see cref="ParamType.UnityType"/> is its C# name); constants are member names.</summary>
        Enum,
        /// <summary>
        /// Any other type Udon can hold in a variable (arrays of values, DataList, Vector4, ...), by C# name in
        /// <see cref="ParamType.UnityType"/>. No constants: values come from variables.
        /// </summary>
        Other,
        /// <summary>
        /// VRC.SDKBase.VRCUrl. VRChat forbids building URLs at runtime, so a constant URL becomes a serialized field
        /// the editor fills (FieldBinding.UrlValue).
        /// </summary>
        Url,
    }

    /// <summary>Type of an action parameter, variable or event parameter.</summary>
    public sealed class ParamType
    {
        public const string Behaviour = "VRC.Udon.UdonBehaviour";

        public ValueKind Kind;
        /// <summary>One UdonBehaviour: a trigger or script to call, or the receiver a call reports back to.</summary>
        public bool IsBehaviour => Kind == ValueKind.Object && UnityType == Behaviour && !IsArray;
        /// <summary>C# type name for <see cref="ValueKind.Object"/> / <see cref="ValueKind.Enum"/>, e.g. "UnityEngine.GameObject".</summary>
        public string UnityType;
        /// <summary>An array of this kind (any kind). Object parameters that are arrays apply the action to each target.</summary>
        public bool IsArray;
        /// <summary>
        /// Object type derives from UnityEngine.Component, so it can be looked up on a GameObject (GetComponent).
        /// Assets such as AudioClip or Material are not: "This GameObject" and event-object conversions must not apply.
        /// </summary>
        public bool IsComponent;
        /// <summary>The C# type has == (reference types, or structs that define it). Change detection needs it.</summary>
        public bool HasEquality = true;

        public ParamType(ValueKind kind, string unityType = null, bool isArray = false)
        {
            Kind = kind;
            UnityType = unityType;
            IsArray = isArray;
        }

        public static ParamType Of(ValueKind kind) => new ParamType(kind);
        public static ParamType Objects(string unityType) => new ParamType(ValueKind.Object, unityType, true);
        public static ParamType Object(string unityType) => new ParamType(ValueKind.Object, unityType, false);
        public static ParamType OtherType(string csharpName, bool hasEquality = true) => new ParamType(ValueKind.Other, csharpName) { HasEquality = hasEquality };

        /// <summary>Same element type, ignoring <see cref="IsArray"/>.</summary>
        public ParamType Element() => new ParamType(Kind, UnityType, false) { IsComponent = IsComponent, HasEquality = HasEquality };

        public override string ToString() =>
            Kind == ValueKind.Object || Kind == ValueKind.Enum || Kind == ValueKind.Other
                ? UnityType.Substring(UnityType.LastIndexOf('.') + 1) + (IsArray ? "[]" : "")
                : Kind.ToString();
    }

    /// <summary>Who runs the actions when the event fires on one client.</summary>
    public enum Broadcast
    {
        /// <summary>Only the client where the event fired.</summary>
        Local,
        /// <summary>Every client in the instance (SendCustomNetworkEvent All).</summary>
        All,
        /// <summary>The owner of this object (SendCustomNetworkEvent Owner).</summary>
        Owner,
    }

    /// <summary>For events that carry a player: which players' events are let through.</summary>
    public enum PlayerFilter
    {
        Anyone,
        LocalPlayer,
        OtherPlayers,
    }

    public enum CompareOp
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual,
    }

    public sealed class TriggerProgram
    {
        public List<VariableDecl> Variables = new List<VariableDecl>();
        public List<EventBlock> Events = new List<EventBlock>();
        /// <summary>Synced variables are sent continuously (Continuous sync) instead of when they change (Manual).</summary>
        public bool ContinuousSync;
    }

    public sealed class VariableDecl
    {
        public string Name;
        public ParamType Type = ParamType.Of(ValueKind.Bool);
        /// <summary>Shorthand for basic types.</summary>
        public ValueKind Kind
        {
            get => Type.Kind;
            set => Type = ParamType.Of(value);
        }
        /// <summary>Synced to all players (and late joiners) via manual sync. The editor offers it only for syncable types.</summary>
        public bool Synced;
        /// <summary>Constant initial value for constant-capable kinds; null means the type's default.</summary>
        public object Initial;
        /// <summary>Other triggers set it ("Set Another Trigger's Variable"): the generated class takes values from outside.</summary>
        public bool External;
        /// <summary>Back to its initial value at the start of every event that uses it (a local, in effect).</summary>
        public bool Temporary;
        /// <summary>With continuous sync: smoothed between updates (the editor knows which types Udon can interpolate).</summary>
        public bool Interpolate;
    }

    public sealed class EventBlock
    {
        /// <summary>Id from <see cref="EventCatalog"/>.</summary>
        public string EventId;
        /// <summary>Custom event name, watched variable, listen callback, or timer name.</summary>
        public string Name;
        public Broadcast Broadcast;
        public float DelaySeconds;
        public PlayerFilter PlayerFilter;
        /// <summary>Interact only: hover text.</summary>
        public string InteractText;
        /// <summary>Ui events: whether a UI element is assigned (bound into the generated field tw_Ui{i}).</summary>
        public bool HasUiSource;
        /// <summary>"Notified by another script" events: how the trigger subscribes. Name is the callback method name.</summary>
        public ListenSpec Listen;
        /// <summary>Timer events: when and how often they fire. Name is the timer's name (for Start / Stop Timer).</summary>
        public TimerSpec Timer;
        /// <summary>All must hold, checked on the client that runs the actions.</summary>
        public List<Condition> Conditions = new List<Condition>();
        /// <summary>True: the event runs when any one condition holds. False: all must hold.</summary>
        public bool MatchAny;
        public List<ActionCall> Actions = new List<ActionCall>();
    }

    /// <summary>
    /// Subscribing to another script's notifications: at Start, call RegisterMethod on the target (one parameter receives
    /// the trigger itself); the script later calls the callback by name with SendCustomEvent.
    /// </summary>
    public sealed class ListenSpec
    {
        /// <summary>C# type of the notifying script.</summary>
        public string TargetType;
        public bool HasTarget;
        /// <summary>Null: no registration call (the user lists the trigger in the script's Inspector).</summary>
        public string RegisterMethod;
        /// <summary>Parameters of the registration method, in order.</summary>
        public List<EventParam> Params = new List<EventParam>();
        /// <summary>Index of the parameter that receives the trigger (`this`).</summary>
        public int SelfIndex;
        /// <summary>How many leading parameters to pass; later ones have defaults (or are `params`) and are omitted.</summary>
        public int PassCount;
        /// <summary>Values for the passed parameters other than SelfIndex (same indices as Params; SelfIndex unused).</summary>
        public List<ArgValue> Args = new List<ArgValue>();
        /// <summary>Parameterless method called on the target just before registering (VideoTXL's _EnsureInit), or null.</summary>
        public string Prepare;
        /// <summary>Blocks with the same key (same object, method and arguments) register only once.</summary>
        public string RegistrationKey;
    }

    /// <summary>A timer event fires MinSeconds..MaxSeconds (random between them) after starting, once or repeatedly.</summary>
    public sealed class TimerSpec
    {
        public float MinSeconds = 1f;
        public float MaxSeconds = 1f;
        public bool Repeat = true;
        /// <summary>Start when the trigger starts; otherwise only the "Start Timer" action starts it.</summary>
        public bool AutoStart = true;
    }

    public sealed class Condition
    {
        public string Variable;
        public CompareOp Op;
        public ArgValue Value;
        /// <summary>"Not": the condition holds when the comparison does not.</summary>
        public bool Negate;
    }

    public sealed class ActionCall
    {
        /// <summary>Id from <see cref="ActionCatalog"/>.</summary>
        public string ActionId;
        public List<ArgValue> Args = new List<ArgValue>();
        /// <summary>Call and Script Call: the member. Args are [instance,] then parameters.</summary>
        public CallSpec Call;
        /// <summary>For calls with a return value: variable that receives it (optional for methods).</summary>
        public string ResultVariable;

        /// <summary>The variables a call writes: the result, then the variables given to `out` / `ref` parameters.</summary>
        public IEnumerable<string> CallOutputs()
        {
            if (!string.IsNullOrEmpty(ResultVariable)) yield return ResultVariable;
            if (Call == null) yield break;
            // A struct variable the call changes (pos.y = 3) gets the changed copy back.
            if (Call.ChangesInstance && Args.Count > 0 && Args[0] != null && Args[0].Source == ArgSource.Variable && !string.IsNullOrEmpty(Args[0].Name)) yield return Args[0].Name;
            int first = Call.Instance != null ? 1 : 0;
            for (int i = 0; i < Call.Params.Count; i++)
            {
                var arg = first + i < Args.Count ? Args[first + i] : null;
                if (Call.Params[i].Receives && arg != null && arg.Source == ArgSource.Variable && !string.IsNullOrEmpty(arg.Name)) yield return arg.Name;
            }
        }
        /// <summary>Set / Read Another Trigger's Variable: the type of that variable (the editor looks it up), or null.</summary>
        public ParamType RemoteType;
        /// <summary>The other trigger's variable is temporary (private there): it can't be set or read from outside.</summary>
        public bool RemoteTemporary;

        // ---- "If" blocks (ActionSpecial.If) ----
        public List<Condition> Conditions = new List<Condition>();
        /// <summary>True: any one condition is enough. False: all must hold.</summary>
        public bool MatchAny;
        public List<ActionCall> Then = new List<ActionCall>();
        public List<ActionCall> Else = new List<ActionCall>();

        /// <summary>
        /// The actions of a list in the order diagnostics and bindings number them: each action, then (for an If block)
        /// its Then actions, then its Else actions, depth first. Without If blocks this is the list itself.
        /// </summary>
        public static List<ActionCall> Flatten(IEnumerable<ActionCall> actions) => ActionRules.Flatten(actions, a => a.ActionId, a => a.Then, a => a.Else);

        /// <summary>Only blocks (If, loops) have contents; Then / Else on other actions are ignored.</summary>
        public bool IsBlock => ActionCatalog.IsBlock(ActionId);

        /// <summary>How many numbers this action takes: itself plus a block's contents.</summary>
        public int NumberedCount => 1 + (IsBlock ? Then.Concat(Else).Where(x => x != null).Sum(x => x.NumberedCount) : 0);
    }

    public enum CallKind
    {
        Method,
        /// <summary>Property or field read: `x.name`.</summary>
        Get,
        /// <summary>Property or field write: `x.name = value`.</summary>
        Set,
        /// <summary>Constructor: `new T(...)`, stored into a variable.</summary>
        Ctor,
    }

    /// <summary>One Udon-exposed API member, described in C# terms.</summary>
    public sealed class CallSpec
    {
        /// <summary>Udon node name, e.g. "UnityEngineTransform.__Rotate__UnityEngineVector3__SystemVoid". Stable id.</summary>
        public string UdonName;
        /// <summary>C# declaring type, e.g. "UnityEngine.Transform".</summary>
        public string DeclaringType;
        /// <summary>Method, property or field name (without get_/set_).</summary>
        public string Member;
        public CallKind Kind;
        /// <summary>Null for static members. Object instances are arrays (applied to each target).</summary>
        public ParamType Instance;
        public List<EventParam> Params = new List<EventParam>();
        /// <summary>Null when void.</summary>
        public ParamType Returns;
        /// <summary>
        /// The C# enum the member really returns when Udon can't hold it in a variable (VRCPickup.currentHand): the
        /// value is stored as its number (Returns is Int).
        /// </summary>
        public string ReturnsEnum;
        /// <summary>The instance is a struct (Vector3, Color, VRCTweenHandle, DateTime...): a setter or a void method changes a copy.</summary>
        public bool InstanceIsStruct;

        /// <summary>
        /// The call changes the struct it is made on (pos.y = 3, v.Normalize(), handle.Kill()): it needs a variable as the
        /// target, which gets the changed copy back. Calls that only read (returning a value, or handing values back
        /// through `out`) change nothing.
        /// </summary>
        public bool ChangesInstance =>
            Instance != null && !Instance.IsArray
            && (InstanceIsStruct || Instance.Kind == ValueKind.Vector2 || Instance.Kind == ValueKind.Vector3 || Instance.Kind == ValueKind.Color || Instance.Kind == ValueKind.Quaternion)
            && (Kind == CallKind.Set || (Kind == CallKind.Method && Returns == null && !Params.Any(p => p.Receives)));

        public string Display()
        {
            var shortType = DeclaringType.Substring(DeclaringType.LastIndexOf('.') + 1);
            switch (Kind)
            {
                case CallKind.Get: return shortType + "." + Member + " (get)";
                case CallKind.Set: return shortType + "." + Member + " (set)";
                case CallKind.Ctor:
                    var cs = new List<string>();
                    foreach (var p in Params) cs.Add(Shown(p));
                    return "new " + shortType + "(" + string.Join(", ", cs) + ")";
                default:
                    var ps = new List<string>();
                    foreach (var p in Params) ps.Add(Shown(p));
                    return shortType + "." + Member + "(" + string.Join(", ", ps) + ")";
            }
        }

        static string Shown(EventParam p) => (p.Pass == ParamPass.Out ? "out " : p.Pass == ParamPass.Ref ? "ref " : "") + p.Type + " " + p.Name;
    }

    public enum ArgSource
    {
        /// <summary>A literal value typed in the Inspector.</summary>
        Constant,
        /// <summary>Scene/asset references dragged in the Inspector; become a serialized field.</summary>
        Objects,
        Variable,
        /// <summary>A parameter of the firing event (e.g. the player who entered).</summary>
        EventParam,
        Self,
        /// <summary>The local player (Networking.LocalPlayer).</summary>
        LocalPlayer,
    }

    public sealed class ArgValue
    {
        public ArgSource Source;
        /// <summary>Constant: bool / int / float / string, float[2..4] for vectors and colors, string for enums and URLs.</summary>
        public object Constant;
        /// <summary>Objects: how many references are assigned (null entries included).</summary>
        public int ObjectCount;
        /// <summary>Variable name or event parameter name.</summary>
        public string Name;

        public static ArgValue Const(object value) => new ArgValue { Source = ArgSource.Constant, Constant = value };
        public static ArgValue Objs(int count) => new ArgValue { Source = ArgSource.Objects, ObjectCount = count };
        public static ArgValue Var(string name) => new ArgValue { Source = ArgSource.Variable, Name = name };
        public static ArgValue Param(string name) => new ArgValue { Source = ArgSource.EventParam, Name = name };
        public static ArgValue SelfObject() => new ArgValue { Source = ArgSource.Self };
        public static ArgValue Local() => new ArgValue { Source = ArgSource.LocalPlayer };
    }
}
