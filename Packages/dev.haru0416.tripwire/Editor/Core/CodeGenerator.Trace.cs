using System;
using System.Collections.Generic;
using System.Linq;

namespace Tripwire.Core
{
    // The editor's event history: each event body notes that it ran, or the values its conditions stopped it on, in a
    // small ring the editor reads during Play. Until the editor turns it on, it costs one bool test where an event runs or
    // stops. Events that happen many times a second are not noted (they would flood the history and cost every frame).
    public static partial class CodeGenerator
    {
        public const string TraceFlag = "tw_Trace", TraceLog = "tw_Log", TraceCount = "tw_LogN";
        const char TracePart = '\u001f', TraceNameValue = '\u001e';

        /// <summary>The method running a watched variable's change events (public for the editor; '_' keeps it off the network).</summary>
        public static string ChangedMethodOf(string variable) => "_Tw_Changed_" + Ident(variable);

        /// <summary>The last received value of a synced, watched variable.</summary>
        public static string PrevFieldOf(string variable) => "tw_Prev_" + Ident(variable);

        /// <summary>One note from the history: event Event ran, or its conditions stopped it; with the values it noted.</summary>
        public sealed class TraceEntry
        {
            public bool Ran;
            public int Event;
            public List<KeyValuePair<string, string>> Values = new List<KeyValuePair<string, string>>();
        }

        /// <summary>A note as the generated code writes it ("r3", "s3" then name/value parts), or null.</summary>
        public static TraceEntry ParseTrace(string note)
        {
            if (string.IsNullOrEmpty(note) || (note[0] != 'r' && note[0] != 's')) return null;
            var parts = note.Split(TracePart);
            if (!int.TryParse(parts[0].Substring(1), out int ev)) return null;
            var entry = new TraceEntry { Ran = note[0] == 'r', Event = ev };
            foreach (var part in parts.Skip(1))
            {
                int at = part.IndexOf(TraceNameValue);
                if (at >= 0) entry.Values.Add(new KeyValuePair<string, string>(part.Substring(0, at), part.Substring(at + 1)));
            }
            return entry;
        }

        sealed partial class Generator
        {
            bool traced;

            /// <summary>Whether event i is noted: not the ones that happen many times a second.</summary>
            bool Traced(EventSpec spec) => !spec.Frequent;

            /// <summary>"if (tw_Trace) Tw_Trace(…);" with the note and the values of the named variables.</summary>
            string TraceStatement(char kind, int ev, IEnumerable<string> variables)
            {
                traced = true;
                var note = StringLiteral(kind + ev.ToString());
                foreach (var name in variables)
                    note += " + " + StringLiteral(TracePart + name + TraceNameValue) + " + " + FieldOf(name);
                return "if (" + TraceFlag + ") Tw_Trace(" + note + ");";
            }

            /// <summary>The note that event i ran; a change event also notes the new value.</summary>
            string TraceRan(int i, EventSpec spec)
            {
                var e = p.Events[i];
                var watched = spec.Shape == EventShape.VariableChanged && vars.TryGetValue(e.Name ?? "", out var v) && !v.Temporary ? new[] { e.Name } : new string[0];
                return TraceStatement('r', i, watched);
            }

            /// <summary>The note that event i's conditions stopped it, with the values they compared.</summary>
            string TraceStopped(int i)
            {
                var names = new List<string>();
                bool Noted(string name) => name != null && vars.TryGetValue(name, out var v) && !v.Temporary;
                foreach (var c in p.Events[i].Conditions)
                {
                    if (Noted(c.Variable)) names.Add(c.Variable);
                    if (c.Value?.Source == ArgSource.Variable && Noted(c.Value.Name)) names.Add(c.Value.Name);
                }
                // Not temporary variables: naming one would declare it in the body, a cost even with the history off.
                return TraceStatement('s', i, names.Distinct());
            }

            /// <summary>The ring the notes go into (the editor gives it its size) and the method writing into it.</summary>
            void EmitTraceSupport()
            {
                if (!traced) return;
                fields.Append("        bool ").Append(TraceFlag).Append(";\n");
                fields.Append("        string[] ").Append(TraceLog).Append(";\n");
                fields.Append("        int ").Append(TraceCount).Append(";\n");
                methods.Append("        void Tw_Trace(string note)\n        {\n");
                methods.Append("            ").Append(TraceLog).Append('[').Append(TraceCount).Append(" % ").Append(TraceLog).Append(".Length] = note;\n");
                methods.Append("            ").Append(TraceCount).Append("++;\n");
                methods.Append("        }\n\n");
            }
        }
    }
}
