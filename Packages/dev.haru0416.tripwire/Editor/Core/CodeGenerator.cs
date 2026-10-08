using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    public enum Severity { Warning, Error }

    public sealed class Diagnostic
    {
        public Severity Severity;
        public string Message;
        /// <summary>-1 when not applicable.</summary>
        public int Event = -1, Action = -1, Arg = -1, Variable = -1, Condition = -1;

        public override string ToString()
        {
            var at = new List<string>();
            if (Variable >= 0) at.Add("variable " + Variable);
            if (Event >= 0) at.Add("event " + Event);
            if (Condition >= 0) at.Add("condition " + Condition);
            if (Action >= 0) at.Add("action " + Action);
            if (Arg >= 0) at.Add("arg " + Arg);
            return Severity + (at.Count > 0 ? " (" + string.Join(", ", at) + ")" : "") + ": " + Message;
        }
    }

    /// <summary>Where the editor finds the objects for a generated field.</summary>
    public enum BindingKind
    {
        /// <summary>An action argument: event Event, action Action (numbered as ActionCall.Flatten), argument Arg.</summary>
        ActionArg,
        /// <summary>A variable's initial value: variable Variable.</summary>
        VariableInitial,
        /// <summary>A UI event's element: event Event.</summary>
        UiElement,
        /// <summary>The script a listen event registers with: event Event.</summary>
        ListenTarget,
    }

    /// <summary>A serialized field of the generated behaviour that the editor fills with dragged-in references.</summary>
    public sealed class FieldBinding
    {
        public string Field;
        public BindingKind Kind;
        public int Event = -1, Action = -1, Arg = -1;
        public int Variable = -1;
        /// <summary>A VRCUrl field: the URL text the editor stores into it (instead of object references).</summary>
        public string UrlValue;
        public string UnityType;
        public bool IsArray;
    }

    public sealed class GeneratedProgram
    {
        public string ClassName;
        public string Source;
        /// <summary>"NoVariableSync", "Manual" or "Continuous" (UdonSharp BehaviourSyncMode).</summary>
        public string SyncMode;
        public string InteractText;
        public List<FieldBinding> Bindings = new List<FieldBinding>();
        public List<Diagnostic> Diagnostics = new List<Diagnostic>();
        public bool HasErrors => Diagnostics.Any(d => d.Severity == Severity.Error);
    }

    /// <summary>Turns a <see cref="TriggerProgram"/> into one UdonSharpBehaviour class.</summary>
    public static partial class CodeGenerator
    {
        /// <summary>
        /// Skips the final tidy-up (bodies written into their single caller, shared component lookups), so tests can
        /// check each event's code on its own. Never set by the editor.
        /// </summary>
        public static bool KeepBodiesSeparate;

        public const string Namespace = "Tripwire.Generated";
        const string ClassPlaceholder = "__TRIPWIRE_CLASS__";

        public static GeneratedProgram Generate(TriggerProgram program)
        {
            var g = new Generator(program);
            g.Run();
            var result = g.Result;
            var hash = Fnv1a64(result.Source);
            result.ClassName = ClassPrefix + hash.ToString("x16");
            result.Source = result.Source.Replace(ClassPlaceholder, result.ClassName);
            return result;
        }

        internal static ulong Fnv1a64(string s)
        {
            ulong h = 14695981039346656037UL;
            foreach (char c in s)
            {
                h ^= c;
                h *= 1099511628211UL;
            }
            return h;
        }

        sealed partial class Generator
        {
            readonly TriggerProgram p;
            public readonly GeneratedProgram Result = new GeneratedProgram();
            readonly StringBuilder fields = new StringBuilder();
            readonly StringBuilder methods = new StringBuilder();
            readonly Dictionary<string, VariableDecl> vars = new Dictionary<string, VariableDecl>(StringComparer.Ordinal);
            readonly HashSet<string> declaredArgFields = new HashSet<string>(StringComparer.Ordinal);
            /// <summary>Fields holding an event's values (copied at the entry), dropped again when nothing reads them.</summary>
            readonly List<(string field, string param)> eventValueFields = new List<(string, string)>();
            /// <summary>The declared type of each event-value field (to remove its declaration again).</summary>
            readonly Dictionary<string, string> fieldTypes = new Dictionary<string, string>(StringComparer.Ordinal);

            public Generator(TriggerProgram program) { p = program; }

            /// <summary>A variable the action names but the trigger doesn't have (deleted or renamed), by name.</summary>
            static string MissingVariable(string name) => string.IsNullOrEmpty(name)
                ? Texts.T("Pick a variable.", "変数を選んでください。")
                : Texts.T("There is no variable '" + name + "' (deleted or renamed?). Pick another one.", "変数「" + name + "」がありません（消したか、名前を変えましたか）。選び直してください。");

            static string TypeMismatch(VariableDecl v, ParamType want) =>
                Texts.T("Variable '" + v.Name + "' is " + Texts.TypeName(v.Type) + ", expected " + Texts.TypeName(want) + ".",
                        "変数「" + v.Name + "」は " + Texts.TypeName(v.Type) + " なので、ここ（" + Texts.TypeName(want) + "）には使えません。");

            static string LocalOnlyParam() =>
                Texts.T("Event parameters only exist on the client where the event fired; set who runs it to Only my screen (Local).",
                        "イベントの情報（入った人など）は、起きた人の画面にしかありません。「自分だけ（Local）」にしてください。");

            static string DelayedParam() =>
                Texts.T("With a delay, a later event can replace this before the actions run.",
                        "遅らせている間に次のイベントが起きると、この情報が上書きされることがあります。");

            void Error(string msg, int ev = -1, int act = -1, int arg = -1, int variable = -1, int cond = -1) =>
                Result.Diagnostics.Add(new Diagnostic { Severity = Severity.Error, Message = msg, Event = ev, Action = act, Arg = arg, Variable = variable, Condition = cond });

            void Warn(string msg, int ev = -1, int act = -1, int arg = -1, int variable = -1, int cond = -1) =>
                Result.Diagnostics.Add(new Diagnostic { Severity = Severity.Warning, Message = msg, Event = ev, Action = act, Arg = arg, Variable = variable, Condition = cond });

            public void Run()
            {
                DeclareVariables();
                var specs = new EventSpec[p.Events.Count];
                for (int i = 0; i < p.Events.Count; i++)
                    specs[i] = ResolveEvent(i);

                WarnLoops(specs);
                EmitEntries(specs);
                for (int i = 0; i < p.Events.Count; i++)
                    if (specs[i] != null) EmitEventBody(i, specs[i]);
                EmitVariableMethods();
                EmitTraceSupport();
                DropUnreadEventValues();
                if (!KeepBodiesSeparate)
                {
                    InlineSingleUseBodies();
                    ShareComponentLookups();
                }

                bool anySynced = vars.Values.Any(v => v.Synced);
                Result.SyncMode = !anySynced ? "NoVariableSync" : p.ContinuousSync ? "Continuous" : "Manual";

                var sb = new StringBuilder();
                sb.Append("// <auto-generated>\n");
                sb.Append("// Generated by Tripwire Trigger from a TripwireTrigger component. Edits here are overwritten.\n");
                sb.Append("// </auto-generated>\n");
                // Actions placed after Break / Return are unreachable (and get a warning in the Inspector).
                sb.Append("#pragma warning disable 0162\n");
                sb.Append("using UdonSharp;\nusing UnityEngine;\nusing VRC.SDKBase;\nusing VRC.Udon.Common.Interfaces;\n\n");
                sb.Append("namespace ").Append(Namespace).Append("\n{\n");
                sb.Append("    [UdonBehaviourSyncMode(BehaviourSyncMode.").Append(Result.SyncMode).Append(")]\n");
                sb.Append("    public class ").Append(ClassPlaceholder).Append(" : UdonSharpBehaviour\n    {\n");
                sb.Append(fields);
                if (fields.Length > 0 && methods.Length > 0) sb.Append('\n');
                sb.Append(methods);
                sb.Append("    }\n}\n");
                Result.Source = sb.ToString();
            }

        }
    }
}
