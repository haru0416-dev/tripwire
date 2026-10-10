using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.Udon;
using Object = UnityEngine.Object;

namespace Tripwire
{
    /// <summary>
    /// Authoring data for one trigger: event → action lists edited in the Inspector.
    /// Editor-only: the editor compiles it to an UdonSharp behaviour on the same GameObject
    /// and removes this component from builds.
    /// </summary>
    [AddComponentMenu("Tripwire/Tripwire Trigger")]
    [DisallowMultipleComponent]
    public class TripwireTrigger : MonoBehaviour, VRC.SDKBase.IEditorOnly
    {
        /// <summary>The format this trigger's data was saved in; the editor upgrades older data when it loads it.</summary>
        public const int CurrentDataVersion = 2;
        /// <summary>0: saved before versions existed (the same format as 1). 2 added who can use a card and saved variables.</summary>
        public int dataVersion;

        public List<KVariable> variables = new List<KVariable>();
        public List<KEvent> events = new List<KEvent>();

        void Reset() => dataVersion = CurrentDataVersion;

        /// <summary>A note for people reading the trigger; not used by the generated code.</summary>
        [TextArea] public string comment = "";

        /// <summary>Synced variables are sent continuously (smoothed on arrival) instead of when they change.</summary>
        public bool continuousSync;
        [Tooltip("The UdonBehaviour generated from this trigger. Managed by Tripwire.")]
        public UdonBehaviour generated;
        [Tooltip("Class name of the generated program. Managed by Tripwire.")]
        public string generatedClass;
    }

    // Mirrors of Tripwire.Core enums (Core is editor-only), converted by number. Saved in scenes: never renumber,
    // only add new values at the end.
    public enum KValueType { Bool = 0, Int = 1, Float = 2, String = 3, Vector3 = 4 }
    public enum KBroadcast { Local = 0, All = 1, Owner = 2 }
    public enum KPlayerFilter { Anyone = 0, LocalPlayer = 1, OtherPlayers = 2 }
    /// <summary>Who may set the card off (checked on their own screen, before anything is sent).</summary>
    public enum KGate { Anyone = 0, Owner = 1, Master = 2, InstanceOwner = 3, InList = 4, NotInList = 5 }
    public enum KCompareOp { Equal = 0, NotEqual = 1, Less = 2, LessOrEqual = 3, Greater = 4, GreaterOrEqual = 5 }
    public enum KArgSource { Constant = 0, Objects = 1, Variable = 2, EventParam = 3, Self = 4, LocalPlayer = 5 }

    [Serializable]
    public class KVariable
    {
        public string name = "";
        /// <summary>Full C# type name of any type Udon can hold in a variable, e.g. "System.Int32", "UnityEngine.GameObject[]".</summary>
        public string typeName = "";
        /// <summary>Legacy basic type, used only when <see cref="typeName"/> is empty (data saved before typeName existed).</summary>
        public KValueType type;
        public bool synced;
        /// <summary>Starts from its initial value every time an event runs (like a local variable).</summary>
        public bool temporary;
        /// <summary>Saved for each player (VRChat's PlayerData) and given back when they come again.</summary>
        public bool saved;
        /// <summary>The name it is saved under; empty means the variable's name (kept when the variable is renamed).</summary>
        public string saveKey = "";
        /// <summary>Constant initial value, or objects (several for array types) assigned in the Inspector.</summary>
        public KArg initial = new KArg();
    }

    [Serializable]
    public class KEvent
    {
        public string eventId = "Interact";
        /// <summary>Custom event name, or the variable an OnVariableChanged event watches.</summary>
        public string name = "";
        public KBroadcast broadcast;
        public float delaySeconds;
        public KPlayerFilter playerFilter = KPlayerFilter.LocalPlayer;
        /// <summary>Who may set this card off; InList / NotInList use <see cref="gateList"/>.</summary>
        public KGate gate;
        public TripwirePlayerList gateList;
        /// <summary>Hover text for Interact; empty uses VRChat's default ("Use").</summary>
        public string interactText = "";
        /// <summary>UI events: the Button / Toggle / Slider whose event runs this (wired by Tripwire on apply).</summary>
        public Object uiTarget;
        /// <summary>"Notified by another script": the object whose U# script notifies (name holds the callback name).</summary>
        public Object listenTarget;
        /// <summary>"Notified by another script": id of the script's registration method; empty when not registering.</summary>
        public string listenMethod = "";
        /// <summary>Arguments of the registration method other than the trigger itself.</summary>
        public List<KArg> listenArgs = new List<KArg>();
        /// <summary>Method called on the script just before registering (set by asset presets), or empty.</summary>
        public string listenPrepare = "";
        /// <summary>The user chose to set this notification up by hand instead of from an asset preset.</summary>
        public bool listenByHand;
        /// <summary>Timer events: seconds (a random time between min and max when max is larger), repeat, start at load.</summary>
        public float timerMin = 1f;
        public float timerMax = 1f;
        public bool timerRepeat = true;
        public bool timerAutoStart = true;
        public List<KCondition> conditions = new List<KCondition>();
        /// <summary>True: run when any one condition holds; false: all must hold.</summary>
        public bool conditionsMatchAny;
        public List<KAction> actions = new List<KAction>();
        /// <summary>Stable id of the card, for editor state such as folding (copies get a new one).</summary>
        public string id = "";
        /// <summary>Data from before folding moved to the editor: read once, then unused.</summary>
        public bool expanded = true;
        /// <summary>A note for people reading the trigger; not used by the generated code.</summary>
        public string comment = "";
    }

    [Serializable]
    public class KCondition
    {
        public string variable = "";
        public KCompareOp op;
        public KArg value = new KArg();
        /// <summary>"Not": holds when the comparison does not.</summary>
        public bool negate;
    }

    [Serializable]
    public class KAction
    {
        public string actionId = "";
        public List<KArg> args = new List<KArg>();
        /// <summary>Call Udon API: the Udon node name of the member (stable across SDK updates while exposed).</summary>
        public string method = "";
        /// <summary>Call Udon API: variable that receives the return value (empty: discard).</summary>
        public string resultVariable = "";
        /// <summary>A note for people reading the trigger; not used by the generated code.</summary>
        public string comment = "";

        // "If" blocks (Flow.If): conditions, then the actions for each outcome. SerializeReference lets the type
        // contain itself without Unity's by-value depth limit.
        public List<KCondition> conditions = new List<KCondition>();
        public bool matchAny;
        [SerializeReference] public List<KAction> thenActions = new List<KAction>();
        [SerializeReference] public List<KAction> elseActions = new List<KAction>();
    }

    /// <summary>One argument. Which fields matter depends on <see cref="source"/> and the parameter type.</summary>
    [Serializable]
    public class KArg
    {
        public KArgSource source;
        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue = "";
        public Vector3 vectorValue;
        /// <summary>Vector2 (xy), Color (rgba) and Quaternion (Euler xyz) constants.</summary>
        public Vector4 vector4Value;
        public List<Object> objects = new List<Object>();
        /// <summary>Variable name or event parameter name.</summary>
        public string name = "";
    }
}
