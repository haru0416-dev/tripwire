using System;
using System.Collections.Generic;
using System.Linq;

namespace Tripwire.Core
{
    // Trigger variables: fields, setters with change detection, sync.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            void DeclareVariables()
            {
                for (int i = 0; i < p.Variables.Count; i++)
                {
                    var v = p.Variables[i];
                    var problem = CheckVariableName(v.Name);
                    if (problem != null) { Error(problem, variable: i); continue; }
                    if (vars.Values.Any(x => Ident(x.Name) == Ident(v.Name) && x.Name != v.Name))
                    { Error(Texts.T("Variable name '" + v.Name + "' clashes with another one in generated code.", "変数名「" + v.Name + "」がほかの変数と区別できません。別の名前にしてください。"), variable: i); continue; }
                    if (vars.ContainsKey(v.Name)) { Error(Texts.T("Duplicate variable name '" + v.Name + "'.", "変数名「" + v.Name + "」が重複しています。"), variable: i); continue; }
                    if (v.Type == null || ((v.Kind == ValueKind.Object || v.Kind == ValueKind.Enum || v.Kind == ValueKind.Other) && string.IsNullOrEmpty(v.Type.UnityType)))
                    { Error(Texts.T("Pick a type for this variable.", "変数の型を選んでください。"), variable: i); continue; }
                    if (v.Synced && (v.Kind == ValueKind.Object || v.Kind == ValueKind.Player))
                    { Error(Texts.T("Objects and players cannot be synced; sync a value that identifies them instead.", "オブジェクトやプレイヤーは同期できません。代わりに、それを見分ける番号などを同期してください。"), variable: i); continue; }
                    if (v.Temporary && (v.Synced || !CanBeTemporary(v.Type)))
                    { Error(Texts.T("Only unsynced values with a typed-in initial value (on/off, numbers, text...) can be temporary.", "一時的にできるのは、同期しない値の変数（オン/オフ・数・文字など）だけです。"), variable: i); continue; }
                    if (v.Synced && p.ContinuousSync && ElementTypeOf(v.Type) != null) // any list (object lists and int[] alike)
                    { Error(Texts.T("Lists can't be sent continuously; send them when they change.", "リストは「常に送り続ける」同期にできません。「変えたときに送る」にしてください。"), variable: i); continue; }
                    if (v.Temporary && v.External)
                    { Error(Texts.T("Another trigger sets this variable, so it can't be temporary.", "ほかのトリガーが変えているので、一時的にはできません。"), variable: i); continue; }
                    if (v.Initial != null && !IsConstantKindCompatible(v.Kind, v.Initial)) { Error(Texts.T("Initial value does not match the variable type.", "最初の値が変数の型と合いません。"), variable: i); continue; }
                    vars.Add(v.Name, v);

                    var type = TypeName(v.Type);
                    string init = null;
                    if (HasLiteral(v.Kind) && !v.Type.IsArray) init = Literal(v.Kind, v.Initial ?? DefaultOf(v.Kind));
                    else if (v.Kind == ValueKind.Enum && v.Initial != null && !v.Type.IsArray) init = Literal(v.Type, v.Initial);
                    // Temporary variables are locals of each event body that uses them (declared there), not fields.
                    if (v.Temporary) continue;
                    fields.Append("        ").Append(!v.Synced ? "" : v.Interpolate && p.ContinuousSync ? "[UdonSynced(UdonSyncMode.Linear)] " : "[UdonSynced] ").Append("public ").Append(type).Append(' ').Append(FieldOf(v.Name));
                    if (init != null) fields.Append(" = ").Append(init);
                    fields.Append(";\n");
                    // Always bound, even with nothing assigned: clearing the Inspector value must clear the field too.
                    if (v.Kind == ValueKind.Url && !v.Type.IsArray && v.Initial is string url && url.Length > 0)
                        Result.Bindings.Add(new FieldBinding { Field = FieldOf(v.Name), Kind = BindingKind.VariableInitial, Variable = i, UnityType = "VRC.SDKBase.VRCUrl", UrlValue = url });
                    if (v.Kind == ValueKind.Object)
                        Result.Bindings.Add(new FieldBinding { Field = FieldOf(v.Name), Kind = BindingKind.VariableInitial, Variable = i, UnityType = v.Type.UnityType, IsArray = v.Type.IsArray });
                    if (TracksReceivedChange(v) && v.Type.HasEquality)
                    {
                        fields.Append("        ").Append(type).Append(" tw_Prev_").Append(Ident(v.Name));
                        if (init != null) fields.Append(" = ").Append(init);
                        fields.Append(";\n");
                    }
                }
            }

            /// <summary>Whether an "On Variable Changed" event watches the variable.</summary>
            bool Watched(VariableDecl v) => !v.Temporary && p.Events.Any(e => e.EventId == EventCatalog.VariableChangedId && e.Name == v.Name);

            /// <summary>
            /// Synced or watched variables go through Tw_Set_ (ownership, serialization, change events); any other
            /// variable is a plain field, set directly.
            /// </summary>
            bool NeedsSetter(VariableDecl v) => v.Synced || Watched(v);

            /// <summary>Variables whose Tw_Set_ some code calls. Only those get one: bodies are written before the setters.</summary>
            readonly HashSet<string> settersCalled = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>The statement that sets a variable.</summary>
            string SetStatement(VariableDecl v, string expr)
            {
                if (!NeedsSetter(v)) return FieldOf(v.Name) + " = " + expr + ";";
                settersCalled.Add(v.Name);
                return "Tw_Set_" + Ident(v.Name) + "(" + expr + ");";
            }

            /// <summary>Synced variables whose received changes must be detected (someone watches them).</summary>
            bool TracksReceivedChange(VariableDecl v) => v.Synced && Watched(v);

            static object DefaultOf(ValueKind k)
            {
                switch (k)
                {
                    case ValueKind.Bool: return false;
                    case ValueKind.Int: return 0;
                    case ValueKind.Float: return 0f;
                    case ValueKind.String: return "";
                    case ValueKind.Vector2: return new float[2];
                    case ValueKind.Color: return new float[4];
                    default: return new float[3];
                }
            }

            void EmitVariableMethods()
            {
                foreach (var v in vars.Values.Where(x => x.External))
                {
                    // Another trigger puts the value here and calls this (a local SendCustomEvent; the '_' keeps it off the network).
                    fields.Append("        public ").Append(TypeName(v.Type)).Append(' ').Append(InboxOf(v.Name)).Append(";\n");
                    methods.Append("        public void ").Append(TakeMethodOf(v.Name)).Append("()\n        {\n            ")
                        .Append(SetStatement(v, InboxOf(v.Name))).Append("\n        }\n\n");
                }
                foreach (var v in vars.Values)
                {
                    if (!NeedsSetter(v)) continue;
                    bool watched = Watched(v);
                    var type = TypeName(v.Type);
                    var f = FieldOf(v.Name);
                    if (settersCalled.Contains(v.Name))
                    {
                        methods.Append("        void Tw_Set_").Append(Ident(v.Name)).Append('(').Append(type).Append(" value)\n        {\n");
                        // Without == (some structs) every set counts as a change.
                        if (v.Type.HasEquality) methods.Append("            if (").Append(f).Append(" == value) return;\n");
                        if (v.Synced)
                        {
                            methods.Append("            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);\n");
                            methods.Append("            ").Append(f).Append(" = value;\n");
                            if (watched && v.Type.HasEquality) methods.Append("            tw_Prev_").Append(Ident(v.Name)).Append(" = value;\n");
                            methods.Append("            RequestSerialization();\n");
                        }
                        else
                        {
                            methods.Append("            ").Append(f).Append(" = value;\n");
                        }
                        if (watched) methods.Append("            ").Append(ChangedMethodOf(v.Name)).Append("();\n");
                        methods.Append("        }\n\n");
                    }
                    // The change method stays even with no setter: received sync calls it. Public (with '_', so not over the
                    // network): the editor runs it when a value is changed by hand during Play.
                    if (!watched) continue;

                    methods.Append("        public void ").Append(ChangedMethodOf(v.Name)).Append("()\n        {\n");
                    for (int i = 0; i < p.Events.Count; i++)
                    {
                        var e = p.Events[i];
                        if (e.EventId == EventCatalog.VariableChangedId && e.Name == v.Name)
                            methods.Append("            ").Append(Dispatch(i)).Append('\n');
                    }
                    methods.Append("        }\n\n");
                }

                var synced = vars.Values.Where(TracksReceivedChange).ToList();
                if (synced.Count > 0)
                {
                    // Received sync: change events for synced variables, in OnDeserialization, or in Tw_SyncReceived when an
                    // "On Deserialization" block already wrote that method (entries are emitted before this).
                    methods.Append(deserializationEmitted ? "        void Tw_SyncReceived()\n        {\n" : "        public override void OnDeserialization()\n        {\n");
                    foreach (var v in synced)
                    {
                        if (!v.Type.HasEquality)
                        {
                            methods.Append("            ").Append(ChangedMethodOf(v.Name)).Append("();\n");
                            continue;
                        }
                        methods.Append("            if (").Append(FieldOf(v.Name)).Append(" != tw_Prev_").Append(Ident(v.Name)).Append(")\n            {\n");
                        methods.Append("                tw_Prev_").Append(Ident(v.Name)).Append(" = ").Append(FieldOf(v.Name)).Append(";\n");
                        methods.Append("                ").Append(ChangedMethodOf(v.Name)).Append("();\n            }\n");
                    }
                    methods.Append("        }\n\n");
                }
            }
        }
    }
}
