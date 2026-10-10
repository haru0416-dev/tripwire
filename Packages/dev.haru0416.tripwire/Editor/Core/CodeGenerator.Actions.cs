using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Actions: catalog templates, blocks (If, loops), Send Event, timers. Calls are in CodeGenerator.Calls, variable actions in CodeGenerator.Variables.
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
                    if (x.CallOutputs().Any(read.Contains)) return true;
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

            /// <summary>An input the action has no value for (missing from the saved data).</summary>
            void ReportMissing(ActionParam prm, int ev, int act, int k) =>
                Error(Texts.T("Missing argument '" + prm.Name + "'.", "「" + Texts.Param(prm.Name) + "」が入っていません。"), ev, act, k);

            void EmitAction(int ev, EventSpec spec, int act, ActionCall call, StringBuilder body)
            {
                var a = ActionCatalog.Get(call.ActionId);
                if (a == null) { Error(Texts.T("This version of Tripwire has no action '" + call.ActionId + "' (made with a newer version?). Update Tripwire, or remove the action.", "この版の Tripwire には「" + call.ActionId + "」というアクションがありません（新しい版で作られたのかもしれません）。Tripwire を更新するか、このアクションを外してください。"), ev, act); return; }
                if (a.Special == ActionSpecial.If) { EmitIf(ev, spec, act, call, body); return; }
                if (a.Special == ActionSpecial.Call) { EmitCall(ev, spec, act, call, body); return; }
                if (call.Args.Count != a.Params.Length) { Error(ArgCountMismatch(), ev, act); return; }
                // An input missing from the saved data: templates report each below, the special actions stop here
                // (an optional one, like a loop's counter, is just not used).
                if (a.Special != ActionSpecial.None)
                {
                    bool missing = false;
                    for (int k = 0; k < a.Params.Length; k++)
                        if (call.Args[k] == null && !a.Params[k].Optional) { ReportMissing(a.Params[k], ev, act, k); missing = true; }
                    if (missing) return;
                }
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
                    case ActionSpecial.Calculate:
                        EmitVariableAction(ev, spec, act, a, call, body);
                        return;
                    case ActionSpecial.GetComponent:
                        EmitGetComponent(ev, spec, act, call, body);
                        return;
                    case ActionSpecial.RandomItem:
                        EmitRandomItem(ev, spec, act, call, body);
                        return;
                    case ActionSpecial.SendRandomEvent:
                        EmitSendRandomEvent(ev, spec, act, a, call, body);
                        return;
                    case ActionSpecial.Respawn:
                        EmitRespawn(ev, spec, act, a, call, body);
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
                    if (arg == null) { ReportMissing(prm, ev, act, k); ok = false; continue; }

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

            /// <summary>
            /// "This object" as an UdonBehaviour value: `this` is the U# class, not UdonBehaviour, so it goes through
            /// Component (as UdonSharp allows). As the target of a call (`this.SendCustomEvent`) it stays `this`.
            /// </summary>
            static string AsValue(string expr, ParamType want) =>
                expr == "this" && want != null && want.IsBehaviour ? "((VRC.Udon.UdonBehaviour)(UnityEngine.Component)this)" : expr;

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
                if (network && name.StartsWith("_", StringComparison.Ordinal))
                    Warn(Texts.T("VRChat refuses network events whose name starts with '_', so sending '" + name + "' this way does nothing. Rename it, or send it to Only my screen (Local).",
                                 "名前が _ で始まるイベントは、ネットワーク越しには呼べないので、この送り方では「" + name + "」は動きません。名前を変えるか、「自分だけ（Local）」で送ってください。"), ev, act, 1);
                if (call.Args[0]?.Source == ArgSource.Self && !p.Events.Any(e => e.EventId == EventCatalog.CustomId && e.Name == name))
                    Warn(Texts.T("This trigger has no Custom event named '" + name + "'.", "このトリガーに「" + name + "」というカスタムイベントはありません。"), ev, act, 1);
                return true;
            }

            /// <summary>
            /// Send Random Event: the names (one per line, or separated by commas) in an array, one picked once with
            /// UnityEngine.Random, then sent like Send Event: every target gets the same event.
            /// </summary>
            void EmitSendRandomEvent(int ev, EventSpec spec, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                var text = call.Args.Count > 1 && call.Args[1]?.Source == ArgSource.Constant ? call.Args[1].Constant as string : null;
                var names = RandomEventNames(text);
                if (names.Count == 0) { Error(Texts.T("Enter the names of the events, one per line.", "呼ぶイベントの名前を、1 行に 1 つずつ入れてください。"), ev, act, 1); return; }
                int how = call.Args.Count > 2 && call.Args[2]?.Constant is int b ? b : -1;
                var send = a.Params[2].ChoiceCode;
                if (how < 0 || how >= send.Length) { Error(Texts.T("Pick who runs it.", "誰の画面で動かすかを選んでください。"), ev, act, 2); return; }
                foreach (var name in names)
                {
                    if (how != 0 && name.StartsWith("_", StringComparison.Ordinal))
                        Warn(Texts.T("VRChat refuses network events whose name starts with '_', so '" + name + "' won't run this way.", "名前が _ で始まるイベントは、ネットワーク越しには呼べないので、「" + name + "」は動きません。"), ev, act, 1);
                    if (call.Args[0]?.Source == ArgSource.Self && !p.Events.Any(e => e.EventId == EventCatalog.CustomId && e.Name == name))
                        Warn(Texts.T("This trigger has no Custom event named '" + name + "'.", "このトリガーに「" + name + "」というカスタムイベントはありません。"), ev, act, 1);
                }
                var target = ObjectOperand(spec, a.Params[0].Type, call.Args[0], ev, act, 0, "targets", scalarIfSingle: true);
                if (target == null) return;
                var pick = "tw_Pick" + act;
                body.Append("            string ").Append(pick).Append(" = new string[] { ").Append(string.Join(", ", names.Select(StringLiteral)))
                    .Append(" }[UnityEngine.Random.Range(0, ").Append(names.Count).Append(")];\n");
                if (target.ArrayField != null)
                    EmitStatement(body, target.ArrayField, TypeName(a.Params[0].Type.Element()), new List<string>(), "tw_T." + send[how] + pick + ");", target.ArrayMayBeNull);
                else
                    EmitStatement(body, null, null, target.Guards, (IsPlainName(target.Expr) ? target.Expr : "(" + target.Expr + ")") + "." + send[how] + pick + ");");
            }

            /// <summary>Objects of Respawn actions (their bound fields): where they are at Start is kept for them.</summary>
            readonly List<string> homeFields = new List<string>();

            /// <summary>Whether any Respawn action needs the positions taken at Start (checked before the bodies are written).</summary>
            bool RemembersHomes() => p.Events.Any(e => ActionCall.Flatten(e.Actions).Any(x => x?.ActionId == ActionCatalog.RespawnId));

            /// <summary>
            /// Respawn: each object back where it was at Start. With VRC Object Sync, its own Respawn (after taking
            /// ownership, so everyone sees it); otherwise the remembered position and rotation, its Rigidbody stopped.
            /// A pickup the local player holds is dropped first. Only objects placed in the Inspector have a start.
            /// </summary>
            void EmitRespawn(int ev, EventSpec spec, int act, ActionSpec a, ActionCall call, StringBuilder body)
            {
                var arg = call.Args.Count > 0 ? call.Args[0] : null;
                if (arg == null || arg.Source != ArgSource.Objects)
                {
                    Error(Texts.T("Drag in the objects to put back (their start is taken from the objects placed here).", "戻すオブジェクトを、ドラッグで入れてください（ここに入れた物の、始まったときの位置を覚えておきます）。"), ev, act, 0);
                    return;
                }
                var op = ObjectOperand(spec, a.Params[0].Type, arg, ev, act, 0, "targets");
                if (op?.ArrayField == null) return;
                if (p.Events[ev].Broadcast != Broadcast.Local)
                    Warn(Texts.T("Run on everyone's screen, objects with VRC Object Sync get their ownership fought over. Use Only my screen (Local): Object Sync shows the result to everyone.",
                                 "全員の画面で動かすと、VRC Object Sync の付いた物はオーナーの取り合いになります。「自分だけ（Local）」にしてください（結果は Object Sync が全員に届けます）。"), ev, act);
                var f = op.ArrayField;
                homeFields.Add(f);
                const string i = "            ";
                body.Append(i).Append("for (int tw_I = 0, tw_N = ").Append(f).Append(" == null ? 0 : ").Append(f).Append(".Length; tw_I < tw_N; tw_I++)\n").Append(i).Append("{\n");
                body.Append(i).Append("    UnityEngine.GameObject tw_T = ").Append(f).Append("[tw_I];\n");
                body.Append(i).Append("    if (!Utilities.IsValid(tw_T)) continue;\n");
                body.Append(i).Append("    VRC.SDK3.Components.VRCPickup tw_Pk = tw_T.GetComponent<VRC.SDK3.Components.VRCPickup>();\n");
                body.Append(i).Append("    if (Utilities.IsValid(tw_Pk) && tw_Pk.IsHeld && Utilities.IsValid(tw_Pk.currentPlayer) && tw_Pk.currentPlayer.isLocal) tw_Pk.Drop();\n");
                body.Append(i).Append("    VRC.SDK3.Components.VRCObjectSync tw_Os = tw_T.GetComponent<VRC.SDK3.Components.VRCObjectSync>();\n");
                body.Append(i).Append("    if (Utilities.IsValid(tw_Os)) { Networking.SetOwner(Networking.LocalPlayer, tw_T); tw_Os.Respawn(); continue; }\n");
                body.Append(i).Append("    if (tw_HomeP_").Append(f).Append(" == null || tw_I >= tw_HomeP_").Append(f).Append(".Length) continue;\n");
                body.Append(i).Append("    tw_T.transform.SetPositionAndRotation(tw_HomeP_").Append(f).Append("[tw_I], tw_HomeR_").Append(f).Append("[tw_I]);\n");
                body.Append(i).Append("    UnityEngine.Rigidbody tw_Rb = tw_T.GetComponent<UnityEngine.Rigidbody>();\n");
                body.Append(i).Append("    if (Utilities.IsValid(tw_Rb)) { tw_Rb.velocity = Vector3.zero; tw_Rb.angularVelocity = Vector3.zero; }\n");
                body.Append(i).Append("}\n");
            }

            /// <summary>The positions Respawn goes back to, taken at Start.</summary>
            void EmitHomes()
            {
                if (!RemembersHomes()) return;
                methods.Append("        void Tw_RememberHomes()\n        {\n");
                foreach (var f in homeFields.Distinct())
                {
                    fields.Append("        Vector3[] tw_HomeP_").Append(f).Append(";\n        Quaternion[] tw_HomeR_").Append(f).Append(";\n");
                    methods.Append("            if (").Append(f).Append(" != null)\n            {\n");
                    methods.Append("                tw_HomeP_").Append(f).Append(" = new Vector3[").Append(f).Append(".Length];\n");
                    methods.Append("                tw_HomeR_").Append(f).Append(" = new Quaternion[").Append(f).Append(".Length];\n");
                    methods.Append("                for (int tw_I = 0; tw_I < ").Append(f).Append(".Length; tw_I++)\n");
                    methods.Append("                    if (Utilities.IsValid(").Append(f).Append("[tw_I])) { tw_HomeP_").Append(f).Append("[tw_I] = ").Append(f).Append("[tw_I].transform.position; tw_HomeR_")
                        .Append(f).Append("[tw_I] = ").Append(f).Append("[tw_I].transform.rotation; }\n");
                    methods.Append("            }\n");
                }
                methods.Append("        }\n\n");
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
        }
    }
}
