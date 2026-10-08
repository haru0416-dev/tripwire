using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // "Notified by another script" events: callback methods and the registration at Start.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            bool NeedsRegistration() =>
                p.Events.Any(e => e.EventId == EventCatalog.ScriptNotifiedId && e.Listen != null && e.Listen.RegisterMethod != null && e.Listen.HasTarget);

            /// <summary>Callback methods, the registration at Start, and a Start method when the trigger has none.</summary>
            void EmitListenEntries(EventSpec[] specs, HashSet<string> emittedEventIds)
            {
                // Callbacks mapped to Udon events whose method the loop above did not emit (no such event block).
                foreach (var g in Enumerable.Range(0, specs.Length)
                             .Where(j => specs[j] != null && specs[j].Shape == EventShape.Listen && BuiltInEventFor(p.Events[j].Name) != null)
                             .GroupBy(j => BuiltInEventFor(p.Events[j].Name).Method))
                {
                    if (emittedEventIds.Contains(g.Key)) continue;
                    methods.Append("        public override void ").Append(g.Key).Append("()\n        {\n");
                    if (g.Key == EventCatalog.DeserializationId) methods.Append(UseSyncReceived());
                    foreach (var j in g) methods.Append("            ").Append(Dispatch(j)).Append('\n');
                    methods.Append("        }\n\n");
                }

                // Plain callbacks: one public method per name.
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < specs.Length; i++)
                {
                    if (specs[i] == null || specs[i].Shape != EventShape.Listen || BuiltInEventFor(p.Events[i].Name) != null) continue;
                    var name = p.Events[i].Name;
                    if (!names.Add(name)) continue;
                    methods.Append("        public void ").Append(name).Append("()\n        {\n");
                    // Scripts notify listeners locally; a network call (possible for names without '_') is ignored.
                    if (!name.StartsWith("_", StringComparison.Ordinal))
                        methods.Append("            if (VRC.SDK3.UdonNetworkCalling.NetworkCalling.InNetworkCall) return;\n");
                    for (int j = i; j < specs.Length; j++)
                        if (specs[j] != null && specs[j].Shape == EventShape.Listen && p.Events[j].Name == name && BuiltInEventFor(name) == null)
                            methods.Append("            ").Append(Dispatch(j)).Append('\n');
                    methods.Append("        }\n\n");
                }

                if (!NeedsRegistration()) return;
                var body = new StringBuilder();
                var keys = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < p.Events.Count; i++)
                {
                    var e = p.Events[i];
                    var l = e.Listen;
                    if (specs[i] == null || specs[i].Shape != EventShape.Listen || l == null || l.RegisterMethod == null || !l.HasTarget) continue;
                    if (!keys.Add(l.RegistrationKey ?? ("#" + i))) continue; // same object + method + arguments: once
                    var field = "tw_Listen" + i;
                    fields.Append("        public ").Append(l.TargetType).Append(' ').Append(field).Append(";\n");
                    Result.Bindings.Add(new FieldBinding { Field = field, Kind = BindingKind.ListenTarget, Event = i, UnityType = l.TargetType, IsArray = false });
                    var args = new List<string>();
                    bool ok = true;
                    for (int k = 0; k < l.PassCount && k < l.Params.Count; k++)
                    {
                        if (k == l.SelfIndex) { args.Add("this"); continue; }
                        // Registration runs at Start, outside any event: a temporary variable only exists inside one.
                        var given = k < l.Args.Count ? l.Args[k] : null;
                        if (given != null && given.Source == ArgSource.Variable && vars.TryGetValue(given.Name ?? "", out var tv) && tv.Temporary)
                        { Error(Texts.T("A temporary variable can't be passed when registering (registering happens at Start, outside the events).", "一時的な変数は登録の値には使えません（登録は Start のときに、イベントの外で行うため）。"), i, ListenArgsAction, k); ok = false; break; }
                        var value = ValueExpr(l.Params[k].Type, k < l.Args.Count ? l.Args[k] : null, i, ListenArgsAction, k);
                        if (value == null) { ok = false; break; }
                        args.Add(value);
                    }
                    if (!ok) continue;
                    body.Append("            if (Utilities.IsValid(").Append(field).Append("))\n            {\n");
                    if (!string.IsNullOrEmpty(l.Prepare) && IsAsciiIdentifier(l.Prepare.TrimStart('_')))
                        body.Append("                ").Append(field).Append('.').Append(l.Prepare).Append("();\n");
                    body.Append("                ").Append(field).Append('.').Append(l.RegisterMethod).Append('(').Append(string.Join(", ", args)).Append(");\n");
                    body.Append("            }\n");
                }
                methods.Append("        void Tw_Register()\n        {\n").Append(body).Append("        }\n\n");
            }
        }
    }
}
