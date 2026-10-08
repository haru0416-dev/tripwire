using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Actions: catalog templates, Udon API / script calls, variable actions.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            /// <summary>An If block: `if (conditions) { then } else { else }`, nested actions indented one level.</summary>
            void EmitIf(int ev, EventSpec spec, int act, ActionCall call, StringBuilder body)
            {
                if (call.Conditions.Count == 0) { Error(Texts.T("Add a condition.", "条件を入れてください。"), ev, act); return; }
                // Numbers: the block, then its Then actions, then its Else actions (as ActionCall.Flatten counts them).
                var then = Nested(ev, spec, call.Then, act + 1);
                int elseNumber = act + 1 + call.Then.Where(x => x != null).Sum(x => x.NumberedCount);
                // "Otherwise if": an Else holding just one If block chains as `else if`.
                var elseItems = call.Else.Where(x => x != null).ToList();
                if (then.Length > 0 && elseItems.Count == 1 && elseItems[0].ActionId == ActionCatalog.IfId)
                {
                    var test1 = ConditionTest(call.Conditions, call.MatchAny, ev, act, holds: true);
                    var chained = new StringBuilder();
                    EmitAction(ev, spec, elseNumber, elseItems[0], chained);
                    if (test1 == null) return;
                    const string ind = "            ";
                    body.Append(ind).Append("if (").Append(test1).Append(")\n").Append(ind).Append("{\n").Append(then).Append(ind).Append("}\n");
                    if (chained.Length > 0) body.Append(ind).Append("else ").Append(chained.ToString().Substring(ind.Length));
                    return;
                }
                var otherwise = Nested(ev, spec, call.Else, elseNumber);
                bool onlyElse = then.Length == 0 && otherwise.Length > 0;
                var test = ConditionTest(call.Conditions, call.MatchAny, ev, act, holds: !onlyElse);
                if (test == null) return;
                if (then.Length == 0 && otherwise.Length == 0) { Warn(Texts.T("Nothing in Then or Else.", "Then にも Else にも何も入っていません。"), ev, act); return; }
                const string indent = "            ";
                body.Append(indent).Append("if (").Append(test).Append(")\n").Append(indent).Append("{\n").Append(onlyElse ? otherwise : then).Append(indent).Append("}\n");
                if (!onlyElse && otherwise.Length > 0)
                    body.Append(indent).Append("else\n").Append(indent).Append("{\n").Append(otherwise).Append(indent).Append("}\n");
            }

            /// <summary>How many loops enclose the action being generated (Break needs one).</summary>
            int loopDepth;

            /// <summary>
            /// A loop variable chosen in the Inspector: a non-synced variable of a fitting type (each step sets it, which
            /// would otherwise send every step over the network). Returns its name ("" when optional and not chosen), or null after an error.
            /// </summary>
            string LoopVariable(ActionCall call, int argIndex, ParamType from, int ev, int act, bool optional)
            {
                var arg = call.Args.Count > argIndex ? call.Args[argIndex] : null;
                var name = arg?.Source == ArgSource.Constant ? arg.Constant as string : null;
                if (string.IsNullOrEmpty(name))
                {
                    if (!optional) Error(Texts.T("Pick a variable.", "変数を選んでください。"), ev, act, argIndex);
                    return optional ? "" : null;
                }
                if (!vars.TryGetValue(name, out var v)) { Error(MissingVariable(name), ev, act, argIndex); return null; }
                if (!IsAssignable(v.Type, from)) { Error(TypeMismatch(v, from), ev, act, argIndex); return null; }
                if (v.Synced) { Error(Texts.T("Use a variable that is not synced (it changes at every step).", "ループの回ごとに変わるので、同期しない変数を使ってください。"), ev, act, argIndex); return null; }
                return v.Name;
            }

            /// <summary>Repeat (a count) or For Each (an array variable): a for loop over the block's Then actions.</summary>
            void EmitLoop(int ev, EventSpec spec, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                const string indent = "            ";
                string i = "tw_L" + act, head, steps = "";
                if (a.Special == ActionSpecial.Repeat)
                {
                    var count = ValueExpr(ParamType.Of(ValueKind.Int), call.Args[0], ev, act, 0);
                    var counter = LoopVariable(call, 1, ParamType.Of(ValueKind.Int), ev, act, optional: true);
                    if (count == null || counter == null) return;
                    head = indent + "int tw_N" + act + " = " + count + ";\n" + indent + "for (int " + i + " = 0; " + i + " < tw_N" + act + "; " + i + "++)\n";
                    if (counter != "") steps = indent + "    " + SetStatement(vars[counter], i) + "\n";
                }
                else
                {
                    var listArg = call.Args[0];
                    var listName = listArg?.Source == ArgSource.Constant ? listArg.Constant as string : null;
                    if (string.IsNullOrEmpty(listName) || !vars.TryGetValue(listName, out var list)) { Error(Texts.T("Pick a list variable.", "リストの変数を選んでください。"), ev, act, 0); return; }
                    var element = ElementTypeOf(list.Type);
                    if (element == null) { Error(Texts.T("Pick a list variable.", "リストの変数を選んでください。"), ev, act, 0); return; }
                    var item = LoopVariable(call, 1, element, ev, act, optional: false);
                    var counter = LoopVariable(call, 2, ParamType.Of(ValueKind.Int), ev, act, optional: true);
                    if (item == null || counter == null) return;
                    if (counter != "" && counter == item) { Error(Texts.T("Use different variables for the item and the round.", "「中身を入れる変数」と「何回目か」には別の変数を選んでください。"), ev, act, 2); return; }
                    // A copy of the list, so changing the variable inside the loop doesn't change what is being walked.
                    var arr = "tw_A" + act;
                    head = indent + TypeName(list.Type) + " " + arr + " = " + FieldOf(list.Name) + ";\n"
                         + indent + "if (" + arr + " != null)\n" + indent + "for (int " + i + " = 0; " + i + " < " + arr + ".Length; " + i + "++)\n";
                    steps = indent + "    " + SetStatement(vars[item], arr + "[" + i + "]") + "\n";
                    if (counter != "") steps += indent + "    " + SetStatement(vars[counter], i) + "\n";
                }
                loopDepth++;
                var inner = Nested(ev, spec, call.Then, act + 1);
                loopDepth--;
                if (inner.Length == 0) { Warn(Texts.T("Nothing to repeat in this block.", "くり返すアクションが入っていません。"), ev, act); return; }
                body.Append(head).Append(indent).Append("{\n").Append(steps).Append(inner).Append(indent).Append("}\n");
            }

            /// <summary>
            /// While: a while loop over the block's Then actions. Warns when nothing inside can end it (no action
            /// changes a variable the conditions read, no Break / Return): Udon halts a behaviour that loops forever.
            /// </summary>
            void EmitWhile(int ev, EventSpec spec, int act, ActionCall call, StringBuilder body)
            {
                if (call.Conditions.Count == 0) { Error(Texts.T("Add a condition.", "条件を入れてください。"), ev, act); return; }
                var test = ConditionTest(call.Conditions, call.MatchAny, ev, act, holds: true);
                loopDepth++;
                var inner = Nested(ev, spec, call.Then, act + 1);
                loopDepth--;
                if (test == null) return;
                if (inner.Length == 0) { Warn(Texts.T("Nothing to repeat in this block.", "くり返すアクションが入っていません。"), ev, act); return; }
                // Both sides of each condition can be variables.
                var read = new HashSet<string>(call.Conditions.Select(c => c.Variable)
                    .Concat(call.Conditions.Where(c => c.Value?.Source == ArgSource.Variable).Select(c => c.Value.Name)));
                bool EndsIt(List<ActionCall> actions, bool insideInnerLoop) => actions.Any(x =>
                {
                    var s = x == null ? null : ActionCatalog.Get(x.ActionId);
                    if (s == null) return false;
                    if (s.Special == ActionSpecial.StopEvent) return true;
                    if (s.Special == ActionSpecial.Break && !insideInnerLoop) return true; // a nested loop's Leave leaves that loop only
                    if (x.ResultVariable != null && read.Contains(x.ResultVariable)) return true;
                    // Parameters that write a variable (not a list a loop only reads).
                    for (int k = 0; k < s.Params.Length && k < x.Args.Count; k++)
                        if (s.Params[k].VariableRef && s.Params[k].Role != VariableRole.List && x.Args[k]?.Constant is string name && read.Contains(name)) return true;
                    return s.HoldsActions && (EndsIt(x.Then, insideInnerLoop || s.IsLoop) || EndsIt(x.Else, insideInnerLoop || s.IsLoop));
                });
                bool canEnd = EndsIt(call.Then, false);
                if (!canEnd)
                    Warn(Texts.T("Nothing inside changes what the conditions check, so this may never end (Udon then stops the whole trigger).",
                                 "中で条件の変数を変えていないので、終わらないかもしれません（終わらないままだと、このトリガーは以後動かなくなります）。"), ev, act);
                const string indent = "            ";
                body.Append(indent).Append("while (").Append(test).Append(")\n").Append(indent).Append("{\n").Append(inner).Append(indent).Append("}\n");
            }

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
                    stmt = SetStatement(into, "(" + TypeName(call.RemoteType) + ")" + target.Expr + ".GetProgramVariable(\"" + FieldOf(name) + "\")");
                }
                body.Append("            ");
                AppendGuarded(body, "", target.Guards, stmt);
            }

            /// <summary>Statements of nested actions, indented one more level.</summary>
            StringBuilder Nested(int ev, EventSpec spec, List<ActionCall> actions, int firstNumber)
            {
                var inner = new StringBuilder();
                EmitActions(ev, spec, actions, firstNumber, inner);
                var indented = new StringBuilder();
                foreach (var line in inner.ToString().Split('\n'))
                    if (line.Length > 0) indented.Append("    ").Append(line).Append('\n');
                return indented;
            }

            /// <summary>A list of actions, numbered from firstNumber by position (a block counts its contents too).</summary>
            void EmitActions(int ev, EventSpec spec, List<ActionCall> actions, int firstNumber, StringBuilder body)
            {
                int n = firstNumber;
                ActionSpec ender = null;
                foreach (var a in actions)
                {
                    if (a == null) continue;
                    if (ender != null)
                        Warn(Texts.T("Never runs: \"" + Texts.ActionName(ender) + "\" comes before it.", "前に「" + Texts.ActionName(ender) + "」があるので、このアクションは実行されません。"), ev, n);
                    EmitAction(ev, spec, n, a, body);
                    if (ender == null && ActionCatalog.Get(a.ActionId) is ActionSpec s && s.EndsFlow) ender = s;
                    n += a.NumberedCount;
                }
            }

            /// <summary>Saved inputs that don't match the action as this version defines it (data from another version).</summary>
            static string ArgCountMismatch() =>
                Texts.T("This action's inputs don't match this version of Tripwire (saved by another version?). Pick the action again to rebuild them.",
                        "このアクションの入力欄が、この版の Tripwire と合いません（ほかの版で保存されたのかもしれません）。アクションを選び直すと、入力欄が作り直されます。");

            void EmitAction(int ev, EventSpec spec, int act, ActionCall call, StringBuilder body)
            {
                var a = ActionCatalog.Get(call.ActionId);
                if (a == null) { Error(Texts.T("This version of Tripwire has no action '" + call.ActionId + "' (made with a newer version?). Update Tripwire, or remove the action.", "この版の Tripwire には「" + call.ActionId + "」というアクションがありません（新しい版で作られたのかもしれません）。Tripwire を更新するか、このアクションを外してください。"), ev, act); return; }
                if (a.Special == ActionSpecial.If) { EmitIf(ev, spec, act, call, body); return; }
                if (a.Special == ActionSpecial.Call) { EmitCall(ev, spec, act, call, body); return; }
                if (call.Args.Count != a.Params.Length) { Error(ArgCountMismatch(), ev, act); return; }
                if (call.ActionId == ActionCatalog.SendEventId || call.ActionId == ActionCatalog.SendEventDelayedId)
                    if (!CheckEventName(ev, act, call)) return;

                switch (a.Special)
                {
                    case ActionSpecial.None:
                        break; // a catalog template, below
                    case ActionSpecial.StartTimer:
                    case ActionSpecial.StopTimer:
                        EmitTimerAction(ev, act, a, call, body);
                        return;
                    case ActionSpecial.Repeat:
                    case ActionSpecial.ForEach:
                        EmitLoop(ev, spec, act, a, call, body);
                        return;
                    case ActionSpecial.While:
                        EmitWhile(ev, spec, act, call, body);
                        return;
                    case ActionSpecial.Break:
                    case ActionSpecial.Continue:
                        if (loopDepth == 0) { Error(Texts.T("Put this inside a loop.", "「" + Texts.ActionName(a) + "」は、ループの中に入れてください。"), ev, act); return; }
                        body.Append(a.Special == ActionSpecial.Break ? "            break;\n" : "            continue;\n");
                        return;
                    case ActionSpecial.StopEvent:
                        body.Append("            return;\n");
                        return;
                    case ActionSpecial.SetRemoteVariable:
                    case ActionSpecial.GetRemoteVariable:
                        EmitRemoteVariable(ev, spec, act, a, call, body);
                        return;
                    case ActionSpecial.SetVariable:
                    case ActionSpecial.ToggleVariable:
                    case ActionSpecial.AddVariable:
                    case ActionSpecial.RandomVariable:
                        EmitVariableAction(ev, spec, act, a, call, body);
                        return;
                    default:
                        // A new kind of action needs its own case here (rather than falling into another kind's code).
                        Error(Texts.T("This version of Tripwire can't make this action yet (a Tripwire bug: please report it). Another action can be applied.", "この版の Tripwire は、このアクションをまだ作れません（Tripwire の不具合なので報告してください）。別のアクションにすれば反映できます。"), ev, act);
                        return;
                }

                var subst = new Dictionary<string, string>(StringComparer.Ordinal);
                var guards = new List<string>();
                string loopField = null, loopType = null;
                bool loopMayBeNull = false;
                bool ok = true;

                for (int k = 0; k < a.Params.Length; k++)
                {
                    var prm = a.Params[k];
                    var arg = call.Args[k];
                    if (arg == null) { Error(Texts.T("Missing argument '" + prm.Name + "'.", "「" + Texts.Param(prm.Name) + "」が入っていません。"), ev, act, k); ok = false; continue; }

                    if (prm.Choices != null)
                    {
                        int idx = arg.Source == ArgSource.Constant && arg.Constant is int n ? n : -1;
                        if (idx < 0 || idx >= prm.ChoiceCode.Length) { Error(Texts.T("Pick one of: " + string.Join(", ", prm.Choices) + ".", "選択肢から選んでください。"), ev, act, k); ok = false; continue; }
                        subst[prm.Name] = prm.ChoiceCode[idx];
                        continue;
                    }

                    var op = AnyOperand(spec, prm.Type, arg, ev, act, k, prm.Name, scalarIfSingle: a.EachParam == prm.Name, template: prm.Template);
                    if (op == null) { ok = false; continue; }

                    if (op.ArrayField != null)
                    {
                        if (a.EachParam != prm.Name) { Error(Texts.T("'" + Texts.Param(prm.Name) + "' takes a single object.", "「" + Texts.Param(prm.Name) + "」にはオブジェクトを 1 つだけ入れてください。"), ev, act, k); ok = false; continue; }
                        loopField = op.ArrayField;
                        loopType = TypeName(prm.Type.Element());
                        loopMayBeNull = op.ArrayMayBeNull;
                        subst[prm.Name] = "tw_T";
                    }
                    else
                    {
                        subst[prm.Name] = op.Expr;
                        foreach (var gx in op.Guards)
                            if (!guards.Contains(gx)) guards.Add(gx);
                    }
                }
                if (!ok) return;

                EmitStatement(body, loopField, loopType, guards, Fill(a.Template, subst), loopMayBeNull);
            }

            /// <summary>Emit a statement, run once per valid element of <paramref name="loopField"/> (bound as tw_T) if given.</summary>
            static void EmitStatement(StringBuilder body, string loopField, string loopType, List<string> guards, string stmt, bool loopMayBeNull = false)
            {
                var indent = "            ";
                if (loopField != null)
                {
                    if (loopMayBeNull) body.Append(indent).Append("if (").Append(loopField).Append(" != null)\n");
                    // Length read once (each read is an EXTERN on the Udon VM; about 4% of a 64-element loop when measured).
                    body.Append(indent).Append("for (int tw_I = 0, tw_N = ").Append(loopField).Append(".Length; tw_I < tw_N; tw_I++)\n");
                    body.Append(indent).Append("{\n");
                    body.Append(indent).Append("    ").Append(loopType).Append(" tw_T = ").Append(loopField).Append("[tw_I];\n");
                    body.Append(indent).Append("    if (!Utilities.IsValid(tw_T)) continue;\n");
                    AppendGuarded(body, indent + "    ", guards, stmt);
                    body.Append(indent).Append("}\n");
                }
                else
                {
                    AppendGuarded(body, indent, guards, stmt);
                }
            }

            Operand AnyOperand(EventSpec spec, ParamType type, ArgValue arg, int ev, int act, int k, string fieldName, bool scalarIfSingle = false, bool template = false)
            {
                if (arg == null) { Error(Texts.T("Missing argument.", "入力が足りません。"), ev, act, k); return null; }
                if (type.Kind == ValueKind.Object) return ObjectOperand(spec, type, arg, ev, act, k, fieldName, scalarIfSingle);
                if (type.Kind == ValueKind.Player) return PlayerOperand(spec, arg, ev, act, k);
                var expr = ValueExpr(type, arg, ev, act, k, template: template);
                return expr == null ? null : new Operand { Expr = expr };
            }

            void EmitCall(int ev, EventSpec spec, int act, ActionCall call, StringBuilder body)
            {
                var c = call.Call;
                if (c == null) { Error(Texts.T("Pick an Udon API member.", "呼び出すものを選んでください。"), ev, act); return; }
                int expected = (c.Instance != null ? 1 : 0) + c.Params.Count;
                if (call.Args.Count != expected) { Error(ArgCountMismatch(), ev, act); return; }

                var guards = new List<string>();
                string loopField = null, loopType = null;
                bool loopMayBeNull = false;
                string target = c.DeclaringType;
                int k = 0;
                if (c.Instance != null)
                {
                    var op = c.Instance.Kind == ValueKind.Object && call.Args[0] != null
                        ? ObjectOperand(spec, c.Instance, call.Args[0], ev, act, 0, "p0", scalarIfSingle: true)
                        : AnyOperand(spec, c.Instance, call.Args[0], ev, act, 0, "p0");
                    if (op == null) return;
                    if (op.ArrayField != null)
                    {
                        loopField = op.ArrayField;
                        loopType = TypeName(c.Instance.Element());
                        loopMayBeNull = op.ArrayMayBeNull;
                        target = "tw_T";
                    }
                    else
                    {
                        // `(float)v_i.ToString()` / `-2f.CompareTo(x)` would bind wrongly: wrap anything that is not a plain name.
                        target = IsPlainName(op.Expr) ? op.Expr : "(" + op.Expr + ")";
                        guards.AddRange(op.Guards);
                    }
                    k = 1;
                }

                var args = new List<string>();
                bool ok = true;
                for (int i = 0; i < c.Params.Count; i++, k++)
                {
                    var op = AnyOperand(spec, c.Params[i].Type, call.Args[k], ev, act, k, "p" + k);
                    if (op == null) { ok = false; continue; }
                    if (op.ArrayField != null) { Error(Texts.T("'" + c.Params[i].Name + "' takes a single object.", "「" + c.Params[i].Name + "」にはオブジェクトを 1 つだけ入れてください。"), ev, act, k); ok = false; continue; }
                    args.Add(AsValue(op.Expr, c.Params[i].Type));
                    foreach (var gx in op.Guards)
                        if (!guards.Contains(gx)) guards.Add(gx);
                }
                if (!ok) return;

                if (c.Kind == CallKind.Ctor && CheckConstantConstruction != null && call.Args.All(x => x != null && x.Source == ArgSource.Constant))
                {
                    var problem = CheckConstantConstruction(c, call.Args);
                    if (problem != null) { Error(problem, ev, act); return; }
                }

                string expr;
                switch (c.Kind)
                {
                    case CallKind.Ctor: expr = "new " + c.DeclaringType + "(" + string.Join(", ", args) + ")"; break;
                    case CallKind.Get: expr = target + "." + c.Member; break;
                    case CallKind.Set: expr = target + "." + c.Member + " = " + args[0]; break;
                    default: expr = target + "." + c.Member + "(" + string.Join(", ", args) + ")"; break;
                }

                // A call that may change the variable it is made on (a property set, a method of a struct): a variable
                // that goes through a setter (synced or watched) must get the changed value through it, or the change
                // never reaches others and its change event never runs.
                VariableDecl onVar = null;
                if (c.Instance != null && call.Args[0]?.Source == ArgSource.Variable && (c.Kind == CallKind.Set || c.Kind == CallKind.Method)
                    && vars.TryGetValue(call.Args[0].Name ?? "", out var iv) && NeedsSetter(iv))
                {
                    if (IsStructKind(c.Instance.Kind) && !c.Instance.IsArray)
                    {
                        onVar = iv;
                        expr = "tw_V" + expr.Substring(target.Length);
                    }
                    // An array's element set in place (SetValue): the variable keeps the same array, so nothing sees a change.
                    // Other values either can't be changed by their methods (strings, numbers) or are objects whose own
                    // state changes, which isn't the variable changing.
                    else if (c.Instance.IsArray && c.Kind == CallKind.Method && c.Member == "SetValue")
                        Warn(Texts.T("Changing what '" + iv.Name + "' holds this way doesn't count as a change: other players don't get it and its change event doesn't run. Use Set Variable.",
                                     "変数「" + iv.Name + "」の中身をここで書き換えても変化として扱われず、ほかの人に届かず、「変わったとき」も動きません。「変数を変える」を使ってください。"), ev, act);
                }

                string stmt;
                if (!string.IsNullOrEmpty(call.ResultVariable))
                {
                    VariableDecl v;
                    if (c.Returns == null) { Error(Texts.T("This member returns nothing to store.", "これは値を返さないので、変数に入れられません。"), ev, act); return; }
                    if (!vars.TryGetValue(call.ResultVariable, out v)) { Error(MissingVariable(call.ResultVariable), ev, act); return; }
                    if (!IsAssignable(v.Type, c.Returns)) { Error(Texts.T("Returns " + Texts.TypeName(c.Returns) + "; variable '" + v.Name + "' is " + Texts.TypeName(v.Type) + ".", "返ってくるのは " + Texts.TypeName(c.Returns) + " ですが、変数「" + v.Name + "」は " + Texts.TypeName(v.Type) + " です。"), ev, act); return; }
                    if (loopField != null) Warn(Texts.T("With several targets the variable keeps the last target's value.", "対象が複数あるときは、最後の対象の値が変数に残ります。"), ev, act);
                    stmt = SetStatement(v, expr);
                }
                else if (c.Kind == CallKind.Get || c.Kind == CallKind.Ctor)
                {
                    Error(Texts.T("Pick a variable to store the value in.", "結果を入れる変数を選んでください。"), ev, act);
                    return;
                }
                else
                {
                    stmt = expr + ";";
                }
                if (onVar != null)
                    stmt = "{ " + TypeName(c.Instance) + " tw_V = " + FieldOf(onVar.Name) + "; " + stmt + " " + SetStatement(onVar, "tw_V") + " }";
                EmitStatement(body, loopField, loopType, guards, stmt, loopMayBeNull);
            }

            static bool IsStructKind(ValueKind k) => k == ValueKind.Vector2 || k == ValueKind.Vector3 || k == ValueKind.Color || k == ValueKind.Quaternion;

            /// <summary>
            /// "This object" as an UdonBehaviour value: `this` is the U# class, not UdonBehaviour, so it goes through
            /// Component (as UdonSharp allows). As the target of a call (`this.SendCustomEvent`) it stays `this`.
            /// </summary>
            static string AsValue(string expr, ParamType want) =>
                expr == "this" && want != null && want.UnityType == "VRC.Udon.UdonBehaviour" && !want.IsArray ? "((VRC.Udon.UdonBehaviour)(UnityEngine.Component)this)" : expr;

            /// <summary>
            /// The event name of Send Event: an empty name calls nothing; over the network, a name starting with '_' is
            /// refused by VRChat; sent to this trigger, it should be one of its Custom events. False stops the action.
            /// </summary>
            bool CheckEventName(int ev, int act, ActionCall call)
            {
                if (call.Args.Count < 2 || call.Args[1] == null || call.Args[1].Source != ArgSource.Constant) return true;
                var name = (call.Args[1].Constant as string ?? "").Trim();
                if (name.Length == 0) { Error(Texts.T("Enter the name of the event to run.", "呼ぶイベントの名前を入れてください。"), ev, act, 1); return false; }
                bool network = call.ActionId == ActionCatalog.SendEventId && call.Args.Count > 2 && call.Args[2]?.Constant is int b && (Broadcast)b != Broadcast.Local;
                if (network && name.StartsWith("_"))
                    Warn(Texts.T("VRChat refuses network events whose name starts with '_', so sending '" + name + "' this way does nothing. Rename it, or send it to Only my screen (Local).",
                                 "名前が _ で始まるイベントは、ネットワーク越しには呼べないので、この送り方では「" + name + "」は動きません。名前を変えるか、「自分だけ（Local）」で送ってください。"), ev, act, 1);
                if (call.Args[0]?.Source == ArgSource.Self && !p.Events.Any(e => e.EventId == EventCatalog.CustomId && e.Name == name))
                    Warn(Texts.T("This trigger has no Custom event named '" + name + "'.", "このトリガーに「" + name + "」というカスタムイベントはありません。"), ev, act, 1);
                return true;
            }

            static bool IsPlainName(string expr) =>
                expr.Length > 0 && (char.IsLetter(expr[0]) || expr[0] == '_') && expr.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '.');

            static void AppendGuarded(StringBuilder body, string indent, List<string> guards, string stmt)
            {
                if (guards.Count == 0)
                {
                    body.Append(indent).Append(stmt).Append('\n');
                    return;
                }
                body.Append(indent).Append("if (").Append(string.Join(" && ", guards.Select(x => "Utilities.IsValid(" + x + ")"))).Append(") ").Append(stmt).Append('\n');
            }

            static string Fill(string template, Dictionary<string, string> subst)
            {
                var sb = new StringBuilder();
                int i = 0;
                while (i < template.Length)
                {
                    char c = template[i];
                    if (c == '{')
                    {
                        int end = template.IndexOf('}', i);
                        var key = template.Substring(i + 1, end - i - 1);
                        sb.Append(subst[key]);
                        i = end + 1;
                    }
                    else { sb.Append(c); i++; }
                }
                return sb.ToString();
            }

            /// <summary>Start / Stop Timer: refer to a timer event of this trigger by its name.</summary>
            void EmitTimerAction(int ev, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                var name = call.Args.Count > 0 && call.Args[0]?.Source == ArgSource.Constant ? call.Args[0].Constant as string : null;
                int timer = p.Events.FindIndex(e => e.EventId == EventCatalog.TimerId && e.Timer != null && !string.IsNullOrEmpty(e.Name) && e.Name == name);
                if (timer < 0) { Error(Texts.T("Pick one of this trigger's timers (give the timer event a name).", "このトリガーのタイマーを選んでください（タイマーのイベントに名前を付けると、ここで選べます）。"), ev, act, 0); return; }
                if (a.Special == ActionSpecial.StartTimer)
                    body.Append("            tw_TimerOn").Append(timer).Append(" = true;\n            Tw_Schedule").Append(timer).Append("();\n");
                else
                    body.Append("            tw_TimerOn").Append(timer).Append(" = false;\n");
            }

            void EmitVariableAction(int ev, EventSpec spec, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                var nameArg = call.Args[0];
                var name = nameArg != null && nameArg.Source == ArgSource.Constant ? nameArg.Constant as string : null;
                VariableDecl v;
                if (name == null || !vars.TryGetValue(name, out v)) { Error(MissingVariable(name), ev, act, 0); return; }
                if (v.Synced && p.Events[ev].EventId == EventCatalog.DeserializationId)
                    Warn(Texts.T("Changing a synced variable when sync arrives sends it again, and every receiver does the same: the sync never settles.",
                                 "同期データを受け取ったときに同期する変数を変えると、その値がまた送られ、受け取った人がまた変えるので、送受信が終わらなくなります。"), ev, act);
                else if (v.Synced && p.Events[ev].EventId == EventCatalog.VariableChangedId && vars.TryGetValue(p.Events[ev].Name ?? "", out var watched) && watched.Synced)
                    // A synced variable's change block also runs on every receiver (received sync counts as a change).
                    Warn(Texts.T("This block also runs for every player who receives '" + watched.Name + "'. Changing the synced variable '" + v.Name + "' here makes each of them change it again (adding or toggling once per player) and take ownership. Change it in the event that changes '" + watched.Name + "' instead.",
                                 "同期する変数「" + watched.Name + "」が変わったときのブロックは、受け取った全員の環境でも動きます。ここで同期する変数「" + v.Name + "」を変えると、全員がそれぞれ変えるので値が人数分ずれ、オーナーの取り合いにもなります。「" + watched.Name + "」を変えているイベントの側で、あわせて変えてください。"), ev, act);
                else if (v.Synced && p.Events[ev].Broadcast != Broadcast.Local)
                    Warn(Texts.T("Setting a synced variable from a broadcast event makes every player take ownership at once, so players end up with different values; set it from Only my screen (Local); the sync reaches everyone by itself.", "「全員（All）」で同期した変数を変えると、全員が同時にオーナーになろうとして、値が人によって食い違います。同期した変数は「自分だけ（Local）」で変えてください（変化は自動で全員に届きます）。"), ev, act);

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
