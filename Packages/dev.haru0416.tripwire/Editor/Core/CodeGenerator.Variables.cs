using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Trigger variables: fields, setters with change detection, sync; and the actions that write them.
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
                    // UdonSharp refuses the rest (VRCTweenHandle, DateTime, enums...), which stops every script from compiling.
                    if (v.Synced && CanSync != null && v.Type != null && !CanSync(v.Type))
                    { Error(Texts.T(Texts.TypeName(v.Type) + " can't be synced. Sync numbers, text, positions or colors instead.", Texts.TypeName(v.Type) + " は同期できません。数・文字・位置・色などを同期してください。"), variable: i); continue; }
                    if (v.Temporary && (v.Synced || !CanBeTemporary(v.Type)))
                    { Error(Texts.T("Only unsynced values with a typed-in initial value (on/off, numbers, text...) can be temporary.", "一時的にできるのは、同期しない値の変数（オン/オフ・数・文字など）だけです。"), variable: i); continue; }
                    if (v.Synced && p.ContinuousSync && ElementTypeOf(v.Type) != null) // any list (object lists and int[] alike)
                    { Error(Texts.T("Lists can't be sent continuously; send them when they change.", "リストは「常に送り続ける」同期にできません。「変えたときに送る」にしてください。"), variable: i); continue; }
                    if (v.Temporary && v.External)
                    { Error(Texts.T("Another trigger sets this variable, so it can't be temporary.", "ほかのトリガーが変えているので、一時的にはできません。"), variable: i); continue; }
                    if (v.Initial != null && (!IsConstantKindCompatible(v.Kind, v.Initial) || !EnumMemberExists(v.Type, v.Initial))) { Error(Texts.T("Initial value does not match the variable type.", "最初の値が変数の型と合いません。"), variable: i); continue; }
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

            // ---- actions that write a variable ----

            /// <summary>
            /// Set / Read Another Trigger's Variable. Setting puts the value in the other trigger's inbox and calls its
            /// take method, so the change goes through its own setter (sync, change events); reading takes the field.
            /// </summary>
            void EmitRemoteVariable(int ev, EventSpec spec, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                var target = ObjectOperand(spec, a.Params[0].Type, call.Args[0], ev, act, 0, a.Params[0].Name);
                if (target == null) return;
                var name = call.Args[1]?.Source == ArgSource.Constant ? call.Args[1].Constant as string : null;
                if (string.IsNullOrEmpty(name) || call.RemoteType == null)
                { Error(Texts.T("Pick one of that trigger's variables.", "ほかのトリガーの変数を選んでください。"), ev, act, 1); return; }
                if (call.RemoteTemporary)
                { Error(Texts.T("That variable is temporary (only its own trigger can use it).", "その変数は一時的なので、そのトリガーの中でしか使えません。"), ev, act, 1); return; }
                string stmt;
                if (a.Special == ActionSpecial.SetRemoteVariable)
                {
                    var value = AnyOperand(spec, call.RemoteType, call.Args[2], ev, act, 2, "value");
                    if (value == null) return;
                    target.Guards.AddRange(value.GuardsOfSources());
                    // Braced: the validity guard must cover both statements.
                    stmt = "{ " + target.Expr + ".SetProgramVariable(\"" + InboxOf(name) + "\", " + value.Expr + "); " + target.Expr + ".SendCustomEvent(\"" + TakeMethodOf(name) + "\"); }";
                }
                else
                {
                    var intoName = call.Args[2]?.Source == ArgSource.Constant ? call.Args[2].Constant as string : null;
                    if (string.IsNullOrEmpty(intoName) || !vars.TryGetValue(intoName, out var into)) { Error(Texts.T("Pick a variable to read into.", "読んだ値を入れる変数を選んでください。"), ev, act, 2); return; }
                    if (!IsAssignable(into.Type, call.RemoteType)) { Error(TypeMismatch(into, call.RemoteType), ev, act, 2); return; }
                    WarnSyncedWrite(into, ev, act);
                    stmt = SetStatement(into, "(" + TypeName(call.RemoteType) + ")" + target.Expr + ".GetProgramVariable(\"" + FieldOf(name) + "\")");
                }
                body.Append("            ");
                AppendGuarded(body, "", target.Guards, stmt);
            }

            /// <summary>
            /// Calculate: A op B for the variable's type. Vectors and colors take a number for × and ÷; text only joins
            /// (+). Whole-number ÷ and % by zero give 0, since the error would stop the whole trigger.
            /// </summary>
            string CalculateExpr(VariableDecl v, ActionCall call, int ev, int act)
            {
                if (v.Type.IsArray || !Calculable(v.Kind)) { Error(Texts.T("Calculate into a number, position, color or text variable.", "計算の結果を入れられるのは、数・位置・色・文字の変数です。"), ev, act, 0); return null; }
                var op = call.Args.Count > 2 && call.Args[2]?.Constant is int i ? (ActionCatalog.CalcOp)i : ActionCatalog.CalcOp.Add;
                if (v.Kind == ValueKind.String && op != ActionCatalog.CalcOp.Add) { Error(Texts.T("Text can only be joined (+).", "文字は「+」でつなげることしかできません。"), ev, act, 2); return null; }
                if (op == ActionCatalog.CalcOp.Remainder && v.Kind != ValueKind.Int && v.Kind != ValueKind.Float) { Error(Texts.T("% works on numbers only.", "「%」（余り）は数にしか使えません。"), ev, act, 2); return null; }
                var a = ValueExpr(v.Type, call.Args.Count > 1 ? call.Args[1] : null, ev, act, 1);
                var b = ValueExpr(CalculateOperandB(v.Type, op), call.Args.Count > 3 ? call.Args[3] : null, ev, act, 3);
                if (a == null || b == null) return null;
                switch (op)
                {
                    case ActionCatalog.CalcOp.Add: return "(" + a + ") + (" + b + ")";
                    case ActionCatalog.CalcOp.Subtract: return "(" + a + ") - (" + b + ")";
                    case ActionCatalog.CalcOp.Multiply: return "(" + a + ") * (" + b + ")";
                    default:
                        var sign = op == ActionCatalog.CalcOp.Divide ? " / " : " % ";
                        if (v.Kind != ValueKind.Int) return "(" + a + ")" + sign + "(" + b + ")";
                        if (call.Args[3]?.Source == ArgSource.Constant && call.Args[3].Constant is int zero && zero == 0) { Error(Texts.T("Dividing by 0.", "0 で割っています。"), ev, act, 3); return null; }
                        return "((" + b + ") == 0 ? 0 : (" + a + ")" + sign + "(" + b + "))";
                }
            }

            /// <summary>Get Component: `v = source.GetComponent<T>()` (or InChildren / InParent), T being the variable's type.</summary>
            void EmitGetComponent(int ev, EventSpec spec, int act, ActionCall call, StringBuilder body)
            {
                var name = call.Args.Count > 0 ? call.Args[0]?.Constant as string : null;
                if (name == null || !vars.TryGetValue(name, out var v)) { Error(MissingVariable(name), ev, act, 0); return; }
                if (v.Type.IsArray || v.Kind != ValueKind.Object || !v.Type.IsComponent)
                { Error(Texts.T("Pick a variable of a component type (Rigidbody, AudioSource...).", "コンポーネントの型の変数（Rigidbody、AudioSource など）を選んでください。"), ev, act, 0); return; }
                var source = ObjectOperand(spec, ParamType.Object("UnityEngine.GameObject"), call.Args.Count > 1 ? call.Args[1] : null, ev, act, 1, "p1");
                if (source == null) return;
                if (source.ArrayField != null) { Error(Texts.T("Get it from one object.", "取り出すもとのオブジェクトは 1 つにしてください。"), ev, act, 1); return; }
                var where = call.Args.Count > 2 && call.Args[2]?.Constant is int w ? w : 0;
                var method = where == 1 ? "GetComponentInChildren" : where == 2 ? "GetComponentInParent" : "GetComponent";
                var target = IsPlainName(source.Expr) ? source.Expr : "(" + source.Expr + ")";
                AppendGuarded(body, "            ", source.Guards, SetStatement(v, target + "." + method + "<" + v.Type.UnityType + ">()"));
            }

            /// <summary>
            /// Writing a synced variable where every player does it at once: when sync arrives, in a synced variable's
            /// change block (which runs on every receiver), or from a broadcast event. Checked for every way an action
            /// writes a variable (variable actions, a call's result and outputs, a struct changed in place, reading
            /// another trigger's variable).
            /// </summary>
            void WarnSyncedWrite(VariableDecl v, int ev, int act)
            {
                if (v == null || !v.Synced) return;
                if (p.Events[ev].EventId == EventCatalog.DeserializationId)
                    Warn(Texts.T("Changing a synced variable when sync arrives sends it again, and every receiver does the same: the sync never settles.",
                                 "同期データを受け取ったときに同期する変数を変えると、その値がまた送られ、受け取った人がまた変えるので、送受信が終わらなくなります。"), ev, act);
                else if (p.Events[ev].EventId == EventCatalog.VariableChangedId && vars.TryGetValue(p.Events[ev].Name ?? "", out var watched) && watched.Synced)
                    // A synced variable's change block also runs on every receiver (received sync counts as a change).
                    Warn(Texts.T("This block also runs for every player who receives '" + watched.Name + "'. Changing the synced variable '" + v.Name + "' here makes each of them change it again (adding or toggling once per player) and take ownership. Change it in the event that changes '" + watched.Name + "' instead.",
                                 "同期する変数「" + watched.Name + "」が変わったときのブロックは、受け取った全員の環境でも動きます。ここで同期する変数「" + v.Name + "」を変えると、全員がそれぞれ変えるので値が人数分ずれ、オーナーの取り合いにもなります。「" + watched.Name + "」を変えているイベントの側で、あわせて変えてください。"), ev, act);
                else if (p.Events[ev].Broadcast != Broadcast.Local)
                    Warn(Texts.T("Setting a synced variable from a broadcast event makes every player take ownership at once, so players end up with different values; set it from Only my screen (Local); the sync reaches everyone by itself.", "「全員（All）」で同期した変数を変えると、全員が同時にオーナーになろうとして、値が人によって食い違います。同期した変数は「自分だけ（Local）」で変えてください（変化は自動で全員に届きます）。"), ev, act);
            }

            void EmitVariableAction(int ev, EventSpec spec, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                var nameArg = call.Args[0];
                var name = nameArg != null && nameArg.Source == ArgSource.Constant ? nameArg.Constant as string : null;
                VariableDecl v;
                if (name == null || !vars.TryGetValue(name, out v)) { Error(MissingVariable(name), ev, act, 0); return; }
                WarnSyncedWrite(v, ev, act);

                var f = FieldOf(v.Name);
                string arg;
                switch (a.Special)
                {
                    case ActionSpecial.SetVariable:
                    {
                        var valueArg = call.Args[1];
                        if (v.Type.IsArray)
                        {
                            // A whole array: dragged objects (bound field) or another array variable — never one element.
                            if (valueArg != null && valueArg.Source == ArgSource.Objects && v.Kind == ValueKind.Object)
                            {
                                var bound = ObjectOperand(spec, v.Type, valueArg, ev, act, 1, "value");
                                if (bound == null) return;
                                arg = bound.ArrayField;
                                break;
                            }
                            VariableDecl other;
                            if (valueArg == null || valueArg.Source != ArgSource.Variable || valueArg.Name == null || !vars.TryGetValue(valueArg.Name, out other))
                            { Error(Texts.T("Set a list from objects or another list variable.", "リストには、オブジェクトか、同じ型のリストの変数を入れてください。"), ev, act, 1); return; }
                            if (!IsAssignable(v.Type, other.Type)) { Error(TypeMismatch(other, v.Type), ev, act, 1); return; }
                            arg = FieldOf(other.Name);
                            break;
                        }
                        // Any other type: constants, variables, a dragged object, event parameters. Assigning null is fine,
                        // but expressions that dereference something (event collider → .gameObject) keep that guard.
                        var op = AnyOperand(spec, v.Type, valueArg, ev, act, 1, "value");
                        if (op == null) return;
                        body.Append("            ");
                        var deref = op.GuardsOfSources();
                        AppendGuarded(body, "", deref, SetStatement(v, AsValue(op.Expr, v.Type)));
                        return;
                    }
                    case ActionSpecial.ToggleVariable:
                        if (v.Kind != ValueKind.Bool) { Error(Texts.T("Only bool variables can be toggled.", "切り替えられるのはオン/オフの変数だけです。"), ev, act, 0); return; }
                        arg = "!" + f;
                        break;
                    case ActionSpecial.RandomVariable:
                    {
                        if ((v.Kind != ValueKind.Int && v.Kind != ValueKind.Float) || v.Type.IsArray) { Error(Texts.T("Random numbers go into an Integer or Number variable.", "ランダムな数を入れられるのは数の変数だけです。"), ev, act, 0); return; }
                        var min = ValueExpr(v.Type, call.Args[1], ev, act, 1);
                        var max = ValueExpr(v.Type, call.Args[2], ev, act, 2);
                        if (min == null || max == null) return;
                        // Whole numbers include the maximum, as people expect from "1 to 6"; UnityEngine.Random.Range(int, int) excludes it.
                        if (v.Kind == ValueKind.Int && call.Args[2]?.Source == ArgSource.Constant && call.Args[2].Constant is int hi)
                        {
                            if (hi == int.MaxValue) { Error(Texts.T("The maximum is too large.", "最大が大きすぎます。"), ev, act, 2); return; }
                            if (call.Args[1]?.Source == ArgSource.Constant && call.Args[1].Constant is int lo && lo > hi)
                            { Error(Texts.T("The minimum is larger than the maximum.", "最小が最大より大きくなっています。"), ev, act, 1); return; }
                        }
                        arg = v.Kind == ValueKind.Int ? "UnityEngine.Random.Range(" + min + ", (" + max + ") + 1)" : "UnityEngine.Random.Range(" + min + ", " + max + ")";
                        break;
                    }
                    case ActionSpecial.Calculate:
                        arg = CalculateExpr(v, call, ev, act);
                        if (arg == null) return;
                        break;
                    case ActionSpecial.AddVariable:
                        if (v.Kind != ValueKind.Int && v.Kind != ValueKind.Float) { Error(Texts.T("Only Integer or Number variables can be added to.", "足せるのは数の変数だけです。"), ev, act, 0); return; }
                        var amount = ValueExpr(v.Type, call.Args[1], ev, act, 1);
                        if (amount == null) return;
                        arg = f + " + " + amount;
                        break;
                    default:
                        Error(Texts.T("This version of Tripwire can't make this action yet (a Tripwire bug: please report it). Another action can be applied.", "この版の Tripwire は、このアクションをまだ作れません（Tripwire の不具合なので報告してください）。別のアクションにすれば反映できます。"), ev, act);
                        return;
                }
                body.Append("            ").Append(SetStatement(v, arg)).Append('\n');
            }
        }
    }
}
