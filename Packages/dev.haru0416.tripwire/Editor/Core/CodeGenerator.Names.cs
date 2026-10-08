using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Names: which names users may give events and variables, and how they become identifiers.
    public static partial class CodeGenerator
    {
        // Names a user may not take for Custom events: anything UdonSharpBehaviour/MonoBehaviour already uses.
        static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Start", "Update", "LateUpdate", "FixedUpdate", "PostLateUpdate", "Awake", "OnEnable", "OnDisable", "OnDestroy",
            "OnDeserialization", "OnPreSerialization", "OnPostSerialization", "SendCustomEvent", "SendCustomNetworkEvent",
            "SendCustomEventDelayedSeconds", "SendCustomEventDelayedFrames", "RequestSerialization", "GetProgramVariable",
            "SetProgramVariable", "GetProgramVariableType", "GetUdonTypeID", "GetUdonTypeName", "DisableInteractive",
            "InteractionText", "enabled", "gameObject", "transform", "name", "tag", "GetComponent", "GetComponents",
            "OnTriggerEnter", "OnTriggerExit", "OnTriggerStay", "OnCollisionEnter", "OnCollisionExit", "OnCollisionStay",
        };

        static readonly HashSet<string> CSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const", "continue",
            "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern", "false", "finally",
            "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while",
        };

        static bool IsAsciiIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!(char.IsLetter(s[0]) && s[0] < 128)) return false;
            foreach (char c in s)
                if (!(c < 128 && (char.IsLetterOrDigit(c) || c == '_'))) return false;
            return !CSharpKeywords.Contains(s);
        }

        /// <summary>
        /// Udon's built-in event method names (e.g. OnPlayerTriggerStay, InputJump). A parameterless method with such a
        /// name is compiled by U# as that event, so it can't be a Custom event. Filled by the editor from Udon's
        /// Event_* node definitions; the catalog and <see cref="ReservedNames"/> cover the common ones without it.
        /// </summary>
        public static readonly HashSet<string> UdonEventNames = new HashSet<string>(StringComparer.Ordinal);

        // Simple names the generated code uses in expressions. A method with the same name would shadow them (CS0119).
        static readonly HashSet<string> NamesUsedByGeneratedCode = new HashSet<string>(StringComparer.Ordinal)
        {
            "Networking", "Utilities", "NetworkEventTarget", "Debug", "UnityEngine", "VRC", "TMPro", "System", "UdonSharp",
            "Vector2", "Vector3", "Color", "Quaternion", "VRCPlayerApi", "VRCUrl", "Mathf", "nameof",
        };

        public static string CheckCustomEventName(string name) => CheckCustomEventName(name, null);

        /// <summary>
        /// Why a callback name another script will call is unusable, or null. Unlike Custom names it may start with '_'
        /// (scripts call listeners locally, and '_' keeps the method unreachable over the network).
        /// </summary>
        public static string CheckCallbackName(string name, ICollection<string> moreTaken)
        {
            var core = (name ?? "").TrimStart('_');
            if (core.Length == 0 || !IsAsciiIdentifier(core))
                return Texts.T("Callback names use A-Z, a-z, 0-9 and _ (they must match what the other script calls).",
                               "届く名前は英数字と _ で書いてください（相手のスクリプトが呼ぶ名前と同じにします）。");
            if (BuiltInEventFor(name) != null) return null; // e.g. _onVideoStart: received by the matching Udon event
            if (IsUdonExportName(name))
                return Texts.T("'" + name + "' is the internal name of a Udon event; a method with it would clash with that event.",
                               "「" + name + "」は Udon のイベントの内部名なので使えません（そのイベントとぶつかります）。");
            if (name != core) return (core.StartsWith("Tw_", StringComparison.Ordinal) || core.StartsWith("tw_", StringComparison.Ordinal)) ? Texts.T("Reserved name.", "この名前は使えません。") : null;
            return CheckCustomEventName(name, moreTaken);
        }

        /// <summary>Whether the name has the shape of a Udon event's export (<see cref="UdonExportName"/>); such a method would clash.</summary>
        static bool IsUdonExportName(string name)
        {
            if (name == null || name.Length < 2 || name[0] != '_' || !char.IsLower(name[1])) return false;
            var method = char.ToUpperInvariant(name[1]) + name.Substring(2);
            return ReservedNames.Contains(method) || UdonEventNames.Contains(method) || EventCatalog.All.Any(e => e.Method == method);
        }

        /// <summary>
        /// A callback named like a parameterless Udon event's internal name ("_onVideoStart" for OnVideoStart) is
        /// delivered to that event's method; returns the event, or null.
        /// </summary>
        public static EventSpec BuiltInEventFor(string callback)
        {
            if (callback == null || !callback.StartsWith("_on", StringComparison.Ordinal) || callback.Length < 4) return null;
            var method = "O" + callback.Substring(2);
            var spec = EventCatalog.Get(method);
            return spec != null && spec.Shape == EventShape.Override && spec.Params.Length == 0 ? spec : null;
        }

        /// <summary>Why a Custom event name is unusable, or null if it is fine.</summary>
        /// <param name="moreTaken">Further names the program's own expressions use (roots of called/enum types).</param>
        public static string CheckCustomEventName(string name, ICollection<string> moreTaken)
        {
            if (!IsAsciiIdentifier(name))
                return Texts.T("Custom event names must start with a letter and use only A-Z, a-z, 0-9 and _ (they become method names other scripts call).",
                               "呼ばれる名前は、英字で始めて英数字と _ だけで付けてください（ほかのスクリプトから呼ぶ名前になります）。");
            // An event's id is taken when it becomes a method of that name; the timer event's id ("Timer") never does.
            if (ReservedNames.Contains(name) || (EventCatalog.Get(name) is EventSpec named && named.Shape != EventShape.Timer) || UdonEventNames.Contains(name))
                return Texts.T("'" + name + "' is already used by Udon; pick another name.", "「" + name + "」は Udon が使っている名前です。別の名前にしてください。");
            if (NamesUsedByGeneratedCode.Contains(name) || (moreTaken != null && moreTaken.Contains(name)))
                return Texts.T("'" + name + "' is a type or namespace name the generated code uses; pick another name.", "「" + name + "」は Tripwire が内部で使う名前と重なります。別の名前にしてください。");
            if (name.StartsWith("Tw_", StringComparison.Ordinal) || name.StartsWith("tw_", StringComparison.Ordinal) || name.StartsWith("v_", StringComparison.Ordinal))
                return Texts.T("Names starting with 'Tw_', 'tw_' or 'v_' are reserved for generated code.", "Tw_・tw_・v_ で始まる名前は使えません（Tripwire が内部で使います）。");
            return null;
        }

        /// <summary>Why a variable name is unusable, or null if it is fine.</summary>
        public static string CheckVariableName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Texts.T("Give the variable a name.", "変数に名前を付けてください。");
            if (name.Length > 40) return Texts.T("Variable names can be up to 40 characters.", "変数の名前は 40 文字までです。");
            foreach (char c in name)
                if (char.IsControl(c)) return Texts.T("Variable names cannot contain control characters.", "変数の名前に制御文字は使えません。");
            return null;
        }

        /// <summary>
        /// The identifier a variable gets in generated code. Display names may be anything (日本語 included);
        /// ASCII identifiers are kept as-is (readable code, stable for GetProgramVariable), others are hex-encoded.
        /// </summary>
        public static string Ident(string variable)
        {
            if (IsAsciiIdentifier(variable)) return variable;
            var sb = new StringBuilder("uni_");
            foreach (char c in variable ?? "") sb.Append(((int)c).ToString("x4"));
            return sb.ToString();
        }

        public static string FieldOf(string variable) => "v_" + Ident(variable);
        /// <summary>Where another trigger puts a value for this variable, and the method it then calls to take it.</summary>
        public static string InboxOf(string variable) => "tw_In_" + Ident(variable);
        public static string TakeMethodOf(string variable) => "_Tw_Take_" + Ident(variable);

        /// <summary>Generated class names: this plus a hash of the source.</summary>
        public const string ClassPrefix = "Tripwire_";

        /// <summary>The method U# exports for a Udon event (Interact → _interact).</summary>
        public static string UdonExportName(string method) => "_" + char.ToLowerInvariant(method[0]) + method.Substring(1);

        /// <summary>The generated method a UI element's event calls (via UdonBehaviour.SendCustomEvent) for event block <paramref name="eventIndex"/>.</summary>
        public const string UiMethodPrefix = "Tw_Ui";
        public static string UiMethod(int eventIndex) => UiMethodPrefix + eventIndex;

        /// <summary>A member name for Type.Member (C# enum members can't be keywords, so none are checked).</summary>
        static bool IsEnumMemberName(string s) =>
            !string.IsNullOrEmpty(s) && (char.IsLetter(s[0]) || s[0] == '_') && s.All(c => c < 128 && (char.IsLetterOrDigit(c) || c == '_'));
    }
}
