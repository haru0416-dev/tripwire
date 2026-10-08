using System.Collections.Generic;

namespace Tripwire.Core
{
    public sealed class ActionParam
    {
        public string Name;
        public ParamType Type;
        /// <summary>Constant default for new actions (bool/int/float/string/float[3] or a choice index).</summary>
        public object Default;
        /// <summary>When set, the argument is a constant choice index; <see cref="ChoiceCode"/> is spliced into the template.</summary>
        public string[] Choices;
        public string[] ChoiceCode;
        /// <summary>The argument names a trigger variable (constant string).</summary>
        public bool VariableRef;
        /// <summary>For variable references: what the variable is for (which variables fit, see <see cref="ActionRules.VariableFits"/>).</summary>
        public VariableRole Role;
        /// <summary>The argument names one of the trigger's timer events (constant string).</summary>
        public bool TimerRef;
        /// <summary>The argument names a variable of the trigger in the first argument (constant string).</summary>
        public bool RemoteVariableRef;
        /// <summary>A variable reference that may be left empty (not used).</summary>
        public bool Optional;
        /// <summary>Text shown to people: {name} in the constant inserts a variable or the event's value.</summary>
        public bool Template;

        public ActionParam AsTemplate() { Template = true; return this; }

        public ActionParam(string name, ParamType type, object defaultValue = null)
        {
            Name = name;
            Type = type;
            Default = defaultValue;
        }
    }

    /// <summary>What a variable-reference parameter does with the variable.</summary>
    public enum VariableRole
    {
        /// <summary>The action's own variable (Set / Toggle / Add / Random): the action decides which types fit.</summary>
        Target,
        /// <summary>A loop writes the round (0, 1, 2...) into it.</summary>
        Counter,
        /// <summary>A list a loop walks.</summary>
        List,
        /// <summary>A loop writes each item of the list into it.</summary>
        Item,
    }

    public enum ActionSpecial
    {
        None,
        SetVariable,
        ToggleVariable,
        AddVariable,
        /// <summary>Calls any Udon-exposed API; parameters come from <see cref="ActionCall.Call"/>.</summary>
        Call,
        /// <summary>Runs <see cref="ActionCall.Then"/> or <see cref="ActionCall.Else"/> by <see cref="ActionCall.Conditions"/>.</summary>
        If,
        /// <summary>A random number into a number variable.</summary>
        RandomVariable,
        StartTimer,
        StopTimer,
        /// <summary>Run the block's actions a number of times.</summary>
        Repeat,
        /// <summary>Run the block's actions once for each item of an array variable.</summary>
        ForEach,
        /// <summary>Leave the innermost loop.</summary>
        Break,
        /// <summary>Stop running this event's actions.</summary>
        StopEvent,
        /// <summary>Run the block's actions while its conditions hold.</summary>
        While,
        /// <summary>Skip the rest of this round of the innermost loop.</summary>
        Continue,
        /// <summary>Set a variable of another trigger (through its setter, so sync and change events run there).</summary>
        SetRemoteVariable,
        /// <summary>Read a variable of another trigger into one of this trigger's.</summary>
        GetRemoteVariable,
    }

    public sealed class ActionSpec
    {
        public string Id;
        public string DisplayName;
        public string Category;
        public string Description;
        public ActionParam[] Params = new ActionParam[0];
        /// <summary>
        /// UdonSharp statement(s) with {param} placeholders. When <see cref="EachParam"/> is set and its argument
        /// is a list of objects, the template runs once per valid element with {EachParam} bound to it.
        /// </summary>
        public string Template;
        public string EachParam;
        public ActionSpecial Special;

        // What kind of action this is, derived from Special so every part of the tool agrees.

        /// <summary>Holds other actions (If: Then / Else; loops: Then is the body).</summary>
        public bool HoldsActions => Special == ActionSpecial.If || IsLoop;
        /// <summary>Has conditions (If, While).</summary>
        public bool HasConditions => Special == ActionSpecial.If || Special == ActionSpecial.While;
        /// <summary>Has an Else list (If).</summary>
        public bool HasElse => Special == ActionSpecial.If;
        public bool IsLoop => Special == ActionSpecial.Repeat || Special == ActionSpecial.ForEach || Special == ActionSpecial.While;
        /// <summary>Nothing after it in the same list runs (Break, Continue, Return).</summary>
        public bool EndsFlow => Special == ActionSpecial.Break || Special == ActionSpecial.Continue || Special == ActionSpecial.StopEvent;
    }

    public static class ActionCatalog
    {
        public const string CallId = "Udon.Call";
        /// <summary>Calls a member of another UdonSharp script (a gimmick, a video player...); same mechanics as <see cref="CallId"/>.</summary>
        public const string ScriptCallId = "Script.Call";
        public const string IfId = "Flow.If";
        public const string RepeatId = "Flow.Repeat";
        public const string ForEachId = "Flow.ForEach";
        public const string WhileId = "Flow.While";
        public const string BreakId = "Flow.Break";
        public const string ContinueId = "Flow.Continue";
        public const string StopId = "Flow.Stop";
        public const string TimerStartId = "Timer.Start";
        public const string TimerStopId = "Timer.Stop";
        public const string SetVariableId = "Variable.Set";
        public const string ToggleVariableId = "Variable.Toggle";
        public const string AddVariableId = "Variable.Add";
        public const string RandomVariableId = "Variable.Random";
        public const string LogId = "Debug.Log";
        public const string SendEventId = "Event.Send";
        public const string SetRemoteId = "Trigger.SetVariable";
        public const string GetRemoteId = "Trigger.GetVariable";
        public const string SendEventDelayedId = "Event.SendDelayed";
        const string GO = "UnityEngine.GameObject";
        const string VideoPlayer = "VRC.SDK3.Video.Components.Base.BaseVRCVideoPlayer";
        const string TR = "UnityEngine.Transform";

        static readonly Dictionary<string, ActionSpec> byId = new Dictionary<string, ActionSpec>();
        static readonly List<ActionSpec> all = new List<ActionSpec>();

        public static IReadOnlyList<ActionSpec> All => all;

        static readonly List<ActionSpec> menu = new List<ActionSpec>();
        /// <summary>Actions in menu order, each with its <see cref="ActionSpec.Category"/> from <see cref="Texts.ActionCategories"/>.</summary>
        public static IReadOnlyList<ActionSpec> InMenuOrder => menu;

        static void Layout(string category, params string[] ids)
        {
            foreach (var id in ids)
            {
                var s = byId[id];
                s.Category = category;
                menu.Add(s);
            }
        }

        /// <summary>
        /// Ids that were renamed, old → new, so data saved with the old id keeps working. Ids are saved in scenes:
        /// rename one only by adding it here.
        /// </summary>
        public static readonly Dictionary<string, string> Renamed = new Dictionary<string, string>();

        public static ActionSpec Get(string id)
        {
            if (id == null) return null;
            if (byId.TryGetValue(id, out var spec)) return spec;
            return Renamed.TryGetValue(id, out var now) && byId.TryGetValue(now, out spec) ? spec : null;
        }

        static ActionSpec Add(string id, string category, string display, string description, string template, string each, params ActionParam[] ps)
        {
            var s = new ActionSpec { Id = id, Category = category, DisplayName = display, Description = description, Template = template, EachParam = each, Params = ps };
            byId.Add(id, s);
            all.Add(s);
            return s;
        }

        static ActionParam P(string name, ParamType type, object def = null) => new ActionParam(name, type, def);
        static ActionParam Targets(string unityType)
        {
            var t = ParamType.Objects(unityType);
            t.IsComponent = unityType != GO;
            return new ActionParam("targets", t);
        }
        static ParamType Comp(string unityType)
        {
            var t = ParamType.Object(unityType);
            t.IsComponent = true;
            return t;
        }

        static ActionParam Bool(string name, bool def) => new ActionParam(name, ParamType.Of(ValueKind.Bool), def);
        static ActionParam Float(string name, float def) => new ActionParam(name, ParamType.Of(ValueKind.Float), def);
        static ActionParam Int(string name, int def) => new ActionParam(name, ParamType.Of(ValueKind.Int), def);
        static ActionParam Str(string name, string def) => new ActionParam(name, ParamType.Of(ValueKind.String), def);
        static ActionParam Vec(string name) => new ActionParam(name, ParamType.Of(ValueKind.Vector3), new float[3]);

        static ActionParam Choice(string name, int def, string[] choices, string[] code) =>
            new ActionParam(name, ParamType.Of(ValueKind.Int), def) { Choices = choices, ChoiceCode = code };

        static ActionParam TimerRef() => new ActionParam("timer", ParamType.Of(ValueKind.String), "") { TimerRef = true };

        static ActionParam VarRef(string name, VariableRole role, bool optional = false) =>
            new ActionParam(name, ParamType.Of(ValueKind.String), "") { VariableRef = true, Role = role, Optional = optional };

        /// <summary>Actions that hold other actions: If (Then / Else) and the loops (Then is the loop body).</summary>
        public static bool IsBlock(string id) => Get(id)?.HoldsActions == true;

        static ActionParam VarRef() => new ActionParam("variable", ParamType.Of(ValueKind.String), "") { VariableRef = true };

        static ActionCatalog()
        {
            // GameObject
            Add("GameObject.SetActive", "GameObject", "Set Active", "Show or hide objects.",
                "{targets}.SetActive({active});", "targets", Targets(GO), Bool("active", true));
            Add("GameObject.ToggleActive", "GameObject", "Toggle Active", "Flip each object between shown and hidden.",
                "{targets}.SetActive(!{targets}.activeSelf);", "targets", Targets(GO));

            // Components
            Add("Behaviour.SetEnabled", "Component", "Set Component Enabled", "Enable or disable components (Light, Animator, AudioSource, UdonBehaviour, ...).",
                "{targets}.enabled = {enabled};", "targets", Targets("UnityEngine.Behaviour"), Bool("enabled", true));
            Add("Collider.SetEnabled", "Component", "Set Collider Enabled", "Enable or disable colliders.",
                "{targets}.enabled = {enabled};", "targets", Targets("UnityEngine.Collider"), Bool("enabled", true));
            Add("Renderer.SetEnabled", "Component", "Set Renderer Enabled", "Show or hide renderers without disabling the object.",
                "{targets}.enabled = {enabled};", "targets", Targets("UnityEngine.Renderer"), Bool("enabled", true));

            // Transform
            Add("Transform.SetPosition", "Transform", "Set Position", "Move objects to a world position (local only, not synced).",
                "{targets}.position = {position};", "targets", Targets(TR), Vec("position"));
            Add("Transform.MoveTo", "Transform", "Move To", "Move objects to another object's position and rotation (local only).",
                "{targets}.SetPositionAndRotation({destination}.position, {destination}.rotation);", "targets",
                Targets(TR), P("destination", Comp(TR)));

            // Animator
            Add("Animator.SetTrigger", "Animator", "Animator Set Trigger", "Fire an Animator trigger parameter.",
                "{targets}.SetTrigger({parameter});", "targets", Targets("UnityEngine.Animator"), Str("parameter", ""));
            Add("Animator.SetBool", "Animator", "Animator Set Bool", "Set an Animator bool parameter.",
                "{targets}.SetBool({parameter}, {value});", "targets", Targets("UnityEngine.Animator"), Str("parameter", ""), Bool("value", true));
            Add("Animator.SetInteger", "Animator", "Animator Set Integer", "Set an Animator int parameter.",
                "{targets}.SetInteger({parameter}, {value});", "targets", Targets("UnityEngine.Animator"), Str("parameter", ""), Int("value", 0));
            Add("Animator.SetFloat", "Animator", "Animator Set Float", "Set an Animator float parameter.",
                "{targets}.SetFloat({parameter}, {value});", "targets", Targets("UnityEngine.Animator"), Str("parameter", ""), Float("value", 0f));
            Add("Animator.Play", "Animator", "Animator Play State", "Jump to a state by name.",
                "{targets}.Play({state});", "targets", Targets("UnityEngine.Animator"), Str("state", ""));

            // Audio / effects
            Add("AudioSource.Play", "Audio", "Play Audio", "Play audio sources from the start.",
                "{targets}.Play();", "targets", Targets("UnityEngine.AudioSource"));
            Add("AudioSource.Stop", "Audio", "Stop Audio", "Stop audio sources.",
                "{targets}.Stop();", "targets", Targets("UnityEngine.AudioSource"));
            Add("AudioSource.PlayOneShot", "Audio", "Play Sound Once", "Play a clip once without stopping what the source is playing.",
                "{targets}.PlayOneShot({clip});", "targets", Targets("UnityEngine.AudioSource"), P("clip", ParamType.Object("UnityEngine.AudioClip")));
            Add("ParticleSystem.Play", "Effects", "Play Particles", "Start particle systems.",
                "{targets}.Play();", "targets", Targets("UnityEngine.ParticleSystem"));
            Add("ParticleSystem.Stop", "Effects", "Stop Particles", "Stop particle systems.",
                "{targets}.Stop();", "targets", Targets("UnityEngine.ParticleSystem"));

            // Text
            Add("Text.SetText", "UI", "Set Text", "Change TextMeshPro / TextMeshPro UGUI text.",
                "{targets}.text = {text};", "targets", Targets("TMPro.TMP_Text"), Str("text", "").AsTemplate());

            // Player (local)
            Add("Player.Teleport", "Player", "Teleport Local Player", "Teleport the local player to an object's position and rotation.",
                "Networking.LocalPlayer.TeleportTo({destination}.position, {destination}.rotation);", null,
                P("destination", Comp(TR)));
            Add("Player.SetSpeed", "Player", "Set Local Player Speed", "Walk / run / strafe speed and jump impulse of the local player.",
                "Networking.LocalPlayer.SetWalkSpeed({walk}); Networking.LocalPlayer.SetRunSpeed({run}); Networking.LocalPlayer.SetStrafeSpeed({strafe}); Networking.LocalPlayer.SetJumpImpulse({jump});", null,
                Float("walk", 2f), Float("run", 4f), Float("strafe", 2f), Float("jump", 3f));

            // Pickup / ownership
            Add("Pickup.Drop", "Pickup", "Force Drop", "Make the local player drop these pickups.",
                "{targets}.Drop();", "targets", Targets("VRC.SDKBase.VRC_Pickup"));
            Add("Networking.TakeOwnership", "Network", "Take Ownership", "Make the local player the owner of these objects.",
                "Networking.SetOwner(Networking.LocalPlayer, {targets});", "targets", Targets(GO));

            // Events
            Add(SendEventId, "Event", "Send Event", "Run a Custom event on triggers or any Udon behaviour (by method name).",
                "{targets}.{broadcast}{event});", "targets",
                Targets("VRC.Udon.UdonBehaviour"), Str("event", ""),
                Choice("broadcast", 0, new[] { "Local", "All", "Owner" },
                    new[] { "SendCustomEvent(", "SendCustomNetworkEvent(NetworkEventTarget.All, ", "SendCustomNetworkEvent(NetworkEventTarget.Owner, " }));
            Add(SendEventDelayedId, "Event", "Send Event Delayed", "Run a Custom event locally after some seconds.",
                "{targets}.SendCustomEventDelayedSeconds({event}, {seconds});", "targets",
                Targets("VRC.Udon.UdonBehaviour"), Str("event", ""), Float("seconds", 1f));

            // Variables (code emitted by the generator; templates unused)
            byId[SetVariableId] = Special(SetVariableId, "Set Variable", "Set a trigger variable. Synced variables reach everyone, including late joiners.", ActionSpecial.SetVariable,
                VarRef(), new ActionParam("value", null));
            byId[ToggleVariableId] = Special(ToggleVariableId, "Toggle Variable", "Flip a bool variable.", ActionSpecial.ToggleVariable, VarRef());
            byId[AddVariableId] = Special(AddVariableId, "Add To Variable", "Add to an Integer or Number variable.", ActionSpecial.AddVariable,
                VarRef(), new ActionParam("amount", null));
            byId[RandomVariableId] = Special(RandomVariableId, "Random Number", "Put a random number from min to max (both included for whole numbers) into a number variable.",
                ActionSpecial.RandomVariable, VarRef(), new ActionParam("min", null), new ActionParam("max", null));
            byId[RepeatId] = Special(RepeatId, "Repeat", "Run some actions a number of times.", ActionSpecial.Repeat,
                Int("count", 3), VarRef("counter", VariableRole.Counter, optional: true));
            byId[ForEachId] = Special(ForEachId, "For Each", "Run some actions once for each item of a list variable.", ActionSpecial.ForEach,
                VarRef("list", VariableRole.List), VarRef("item", VariableRole.Item), VarRef("counter", VariableRole.Counter, optional: true));
            byId[WhileId] = Special(WhileId, "While", "Run some actions again and again while conditions hold.", ActionSpecial.While);
            byId[ContinueId] = Special(ContinueId, "Continue", "Skip the rest of this round of the loop and go on with the next.", ActionSpecial.Continue);
            byId[BreakId] = Special(BreakId, "Break", "Stop repeating and carry on after the loop.", ActionSpecial.Break);
            byId[StopId] = Special(StopId, "Return", "Skip the rest of this event's actions.", ActionSpecial.StopEvent);
            byId[TimerStartId] = Special(TimerStartId, "Start Timer", "Start (or restart) a timer event of this trigger.", ActionSpecial.StartTimer, TimerRef());
            byId[TimerStopId] = Special(TimerStopId, "Stop Timer", "Stop a timer event of this trigger.", ActionSpecial.StopTimer, TimerRef());

            // Branches
            byId[IfId] = Special(IfId, "If", "Run some actions when conditions hold and others when they don't.", ActionSpecial.If);
            byId[IfId].Category = "Flow";

            // Any Udon API (parameters depend on the chosen member)
            var call = new ActionSpec { Id = CallId, Category = "Udon API", DisplayName = "Call Udon API", Description = "Call any method or set any property Udon exposes.", Special = ActionSpecial.Call };
            byId[CallId] = call;
            all.Add(call);
            var scriptCall = new ActionSpec { Id = ScriptCallId, Category = "Link", DisplayName = "Use another script", Description = "Call a function of another UdonSharp script (gimmicks, video players...) or change its values.", Special = ActionSpecial.Call };
            byId[ScriptCallId] = scriptCall;
            all.Add(scriptCall);

            // VRChat video players
            Add("Video.PlayUrl", "Video", "Play Video URL", "Load and play a URL (each player may load a new URL at most every 5 seconds).",
                "{targets}.PlayURL({url});", "targets", Targets(VideoPlayer), new ActionParam("url", ParamType.Of(ValueKind.Url), ""));
            Add("Video.LoadUrl", "Video", "Load Video URL", "Load a URL without starting it.",
                "{targets}.LoadURL({url});", "targets", Targets(VideoPlayer), new ActionParam("url", ParamType.Of(ValueKind.Url), ""));
            Add("Video.Play", "Video", "Play Video", "Play (or resume) the loaded video.", "{targets}.Play();", "targets", Targets(VideoPlayer));
            Add("Video.Pause", "Video", "Pause Video", "Pause the video.", "{targets}.Pause();", "targets", Targets(VideoPlayer));
            Add("Video.Stop", "Video", "Stop Video", "Stop the video.", "{targets}.Stop();", "targets", Targets(VideoPlayer));
            Add("Video.SetTime", "Video", "Seek Video", "Jump to a time in seconds.", "{targets}.SetTime({seconds});", "targets", Targets(VideoPlayer), Float("seconds", 0f));
            Add("Video.SetLoop", "Video", "Loop Video", "Turn looping on or off.", "{targets}.Loop = {loop};", "targets", Targets(VideoPlayer), Bool("loop", true));

            // Debug
            Add(LogId, "Debug", "Log", "Write a message to the log.",
                "Debug.Log({message});", null, Str("message", "").AsTemplate());

            Layout("Show", "GameObject.SetActive", "GameObject.ToggleActive", "Collider.SetEnabled", "Renderer.SetEnabled", "Behaviour.SetEnabled");
            Layout("Move", "Transform.SetPosition", "Transform.MoveTo", "Animator.SetTrigger", "Animator.SetBool", "Animator.SetInteger", "Animator.SetFloat", "Animator.Play");
            Layout("SoundFx", "AudioSource.Play", "AudioSource.Stop", "AudioSource.PlayOneShot", "ParticleSystem.Play", "ParticleSystem.Stop");
            Layout("Text", "Text.SetText");
            Layout("Player", "Player.Teleport", "Player.SetSpeed");
            Layout("Pickup", "Pickup.Drop", "Networking.TakeOwnership");
            foreach (var id in new[] { RepeatId, ForEachId, WhileId, BreakId, ContinueId, StopId, TimerStartId, TimerStopId }) byId[id].Category = "Flow";
            Layout("Flow", IfId, RepeatId, ForEachId, WhileId, BreakId, ContinueId, StopId, TimerStartId, TimerStopId);
            Layout("Variable", SetVariableId, ToggleVariableId, AddVariableId, RandomVariableId);
            Layout("Video", "Video.PlayUrl", "Video.LoadUrl", "Video.Play", "Video.Pause", "Video.Stop", "Video.SetTime", "Video.SetLoop");
            // Another trigger's variables (with Send Event, this passes values to its Custom events).
            var trigger = new ActionParam("trigger", ParamType.Object("VRC.Udon.UdonBehaviour")) { };
            trigger.Type.IsComponent = true;
            var remote = new ActionParam("remoteVariable", ParamType.Of(ValueKind.String), "") { RemoteVariableRef = true };
            byId[SetRemoteId] = Special(SetRemoteId, "Set Another Trigger's Variable", "Set a variable of another trigger; its sync and change events run as if it set it itself.",
                ActionSpecial.SetRemoteVariable, trigger, remote, new ActionParam("value", null));
            byId[GetRemoteId] = Special(GetRemoteId, "Read Another Trigger's Variable", "Read a variable of another trigger into one of this trigger's variables.",
                ActionSpecial.GetRemoteVariable, trigger, remote, VarRef("into", VariableRole.Target));
            byId[SetRemoteId].Category = byId[GetRemoteId].Category = "Link";
            Layout("Link", ScriptCallId, SendEventId, SendEventDelayedId, SetRemoteId, GetRemoteId);
            Layout("Advanced", CallId, LogId);
        }

        static ActionSpec Special(string id, string display, string description, ActionSpecial special, params ActionParam[] ps)
        {
            var s = new ActionSpec { Id = id, Category = "Variable", DisplayName = display, Description = description, Special = special, Params = ps };
            all.Add(s);
            return s;
        }
    }
}
