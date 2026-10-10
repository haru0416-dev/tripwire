using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Call Udon API: the member's arguments, `out` parameters received by variables, structs changed in place, and
    // the checks for calls that report back to this trigger.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
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

                // A behaviour the call reports back to, when it is this trigger: the events and variables it names must be
                // this trigger's own (VRCTween writes "variableName" by its Udon name, calls "onUpdate" by name...).
                int first = c.Instance != null ? 1 : 0;
                bool backHere = c.Params.Where((x, i) => x.Type.IsBehaviour && call.Args.Count > first + i && call.Args[first + i]?.Source == ArgSource.Self).Any();
                if (backHere && !CheckCallBack(c, call, ev, act)) return;

                var args = new List<string>();
                // `out` / `ref` parameters go through locals declared before the call (an exact type, as C# needs) and
                // are stored after it the way any write is (a synced or watched variable through its setter).
                var before = new List<string>();
                var after = new List<string>();
                bool ok = true;
                for (int i = 0; i < c.Params.Count; i++, k++)
                {
                    if (c.Params[i].Receives)
                    {
                        var output = OutputArgument(c.Params[i], call.Args[k], ev, act, k, "tw_O" + i, before, after);
                        if (output == null) ok = false; else args.Add(output);
                        continue;
                    }
                    var op = AnyOperand(spec, c.Params[i].Type, call.Args[k], ev, act, k, "p" + k);
                    if (op == null) { ok = false; continue; }
                    if (op.ArrayField != null) { Error(Texts.T("'" + c.Params[i].Name + "' takes a single object.", "「" + c.Params[i].Name + "」にはオブジェクトを 1 つだけ入れてください。"), ev, act, k); ok = false; continue; }
                    // A variable named for the call to write (VRCTween's variableName): its Udon name (v_...), not the name shown.
                    if (backHere && c.Params[i].Name == "variableName" && call.Args[k]?.Source == ArgSource.Constant && call.Args[k].Constant is string named && vars.ContainsKey(named))
                        op.Expr = StringLiteral(FieldOf(named));
                    args.Add(AsValue(op.Expr, c.Params[i].Type));
                    if (c.Params[i].Pass == ParamPass.Fill && call.Args[k]?.Source == ArgSource.Variable && vars.TryGetValue(call.Args[k].Name ?? "", out var filled) && NeedsSetter(filled))
                        Warn(FilledInPlace(filled), ev, act, k);
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
                // never reaches others and its change event never runs. A setter or void method of a struct (pos.y = 3,
                // handle.Kill()) works on a copy under UdonSharp, so it needs a variable and is always stored back.
                bool changesStruct = c.ChangesInstance;
                if (changesStruct && call.Args[0]?.Source != ArgSource.Variable)
                {
                    Error(Texts.T("To change part of a " + Texts.TypeName(c.Instance) + ", make the target a variable.", Texts.TypeName(c.Instance) + " の一部を変えるときは、対象を変数にしてください。"), ev, act, 0);
                    return;
                }
                VariableDecl onVar = null;
                if (c.Instance != null && call.Args[0]?.Source == ArgSource.Variable && (c.Kind == CallKind.Set || c.Kind == CallKind.Method)
                    && vars.TryGetValue(call.Args[0].Name ?? "", out var iv) && (NeedsSetter(iv) || changesStruct))
                {
                    if (changesStruct)
                    {
                        // The copy goes back into the variable: it must be of the struct's own type (an Integer variable can
                        // be read as a number, but a number's copy can't go back into it).
                        if (FullTypeName(iv.Type) != FullTypeName(c.Instance))
                        {
                            Error(Texts.T("To change part of a " + Texts.TypeName(c.Instance) + ", the variable must be one.", Texts.TypeName(c.Instance) + " の一部を変えるときは、" + Texts.TypeName(c.Instance) + " の変数を対象にしてください。"), ev, act, 0);
                            return;
                        }
                        onVar = iv;
                        expr = "tw_V" + expr.Substring(target.Length);
                    }
                    // An array's element set in place (SetValue): the variable keeps the same array, so nothing sees a change.
                    // Other values either can't be changed by their methods (strings, numbers) or are objects whose own
                    // state changes, which isn't the variable changing.
                    else if (c.Instance.IsArray && c.Kind == CallKind.Method && c.Member == "SetValue")
                        Warn(FilledInPlace(iv), ev, act);
                }

                string stmt;
                if (!string.IsNullOrEmpty(call.ResultVariable))
                {
                    // C# would keep the return value; the generated code stores the outputs after it. Neither is what
                    // someone means by giving one variable both.
                    if (call.CallOutputs().Skip(1).Contains(call.ResultVariable))
                    {
                        Error(Texts.T("The result and an output both go into '" + call.ResultVariable + "': pick different variables.",
                                      "結果と出力の引数の両方が、変数「" + call.ResultVariable + "」に入ることになっています。別の変数を選んでください。"), ev, act);
                        return;
                    }
                    VariableDecl v;
                    if (c.Returns == null) { Error(Texts.T("This member returns nothing to store.", "これは値を返さないので、変数に入れられません。"), ev, act); return; }
                    if (!vars.TryGetValue(call.ResultVariable, out v)) { Error(MissingVariable(call.ResultVariable), ev, act); return; }
                    if (!IsAssignable(v.Type, c.Returns)) { Error(Texts.T("Returns " + Texts.TypeName(c.Returns) + "; variable '" + v.Name + "' is " + Texts.TypeName(v.Type) + ".", "返ってくるのは " + Texts.TypeName(c.Returns) + " ですが、変数「" + v.Name + "」は " + Texts.TypeName(v.Type) + " です。"), ev, act); return; }
                    if (loopField != null) Warn(Texts.T("With several targets the variable keeps the last target's value.", "対象が複数あるときは、最後の対象の値が変数に残ります。"), ev, act);
                    stmt = SetStatement(v, c.ReturnsEnum != null ? "(int)(" + expr + ")" : expr);
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
                if (before.Count > 0)
                {
                    if (loopField != null) Warn(Texts.T("With several targets the variables keep the last target's values.", "対象が複数あるときは、最後の対象の値が変数に残ります。"), ev, act);
                    stmt = "{ " + string.Join(" ", before) + " " + stmt + " " + string.Join(" ", after) + " }";
                }
                if (onVar != null)
                    stmt = "{ " + TypeName(c.Instance) + " tw_V = " + FieldOf(onVar.Name) + "; " + stmt + " " + SetStatement(onVar, "tw_V") + " }";
                foreach (var written in call.CallOutputs().Distinct())
                    if (vars.TryGetValue(written, out var wv)) WarnSyncedWrite(wv, ev, act);
                EmitStatement(body, loopField, loopType, guards, stmt, loopMayBeNull);
            }

            /// <summary>
            /// The argument for an `out` / `ref` parameter: the local <paramref name="local"/>, declared in
            /// <paramref name="before"/> (from the variable for `ref`) and stored into the variable in <paramref name="after"/>.
            /// Null after an error: the parameter needs a variable whose type the value fits (and, for `ref`, the other way).
            /// </summary>
            string OutputArgument(EventParam prm, ArgValue arg, int ev, int act, int k, string local, List<string> before, List<string> after)
            {
                if (arg == null || arg.Source != ArgSource.Variable || string.IsNullOrEmpty(arg.Name))
                {
                    Error(Texts.T("Pick the variable that receives '" + prm.Name + "'.", "「" + prm.Name + "」を受け取る変数を選んでください。"), ev, act, k);
                    return null;
                }
                if (!vars.TryGetValue(arg.Name, out var v)) { Error(MissingVariable(arg.Name), ev, act, k); return null; }
                bool fits = IsAssignable(v.Type, prm.Type) && (prm.Pass == ParamPass.Out || IsAssignable(prm.Type, v.Type));
                if (!fits)
                {
                    Error(Texts.T("'" + prm.Name + "' is " + Texts.TypeName(prm.Type) + "; variable '" + v.Name + "' is " + Texts.TypeName(v.Type) + ".",
                                  "「" + prm.Name + "」は " + Texts.TypeName(prm.Type) + " ですが、変数「" + v.Name + "」は " + Texts.TypeName(v.Type) + " です。"), ev, act, k);
                    return null;
                }
                before.Add(TypeName(prm.Type) + " " + local + (prm.Pass == ParamPass.Ref ? " = " + FieldOf(v.Name) : "") + ";");
                after.Add(SetStatement(v, local));
                return (prm.Pass == ParamPass.Out ? "out " : "ref ") + local;
            }

            /// <summary>Events a call reports to, by member: the receiver needs at least one of them.</summary>
            static readonly Dictionary<string, string[]> ReportsTo = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "VRC.SDK3.StringLoading.VRCStringDownloader.LoadUrl", new[] { "OnStringLoadSuccess", "OnStringLoadError" } },
                { "VRC.SDK3.Image.VRCImageDownloader.DownloadImage", new[] { "OnImageLoadSuccess", "OnImageLoadError" } },
                { "VRC.Economy.Store.ListPurchases", new[] { "OnListPurchases" } },
                { "VRC.Economy.Store.ListAvailableProducts", new[] { "OnListAvailableProducts" } },
                { "VRC.Economy.Store.ListProductOwners", new[] { "OnListProductOwners" } },
                { "VRC.Economy.Store.SendProductEvent", new[] { "OnProductEvent" } },
                { "VRC.SDK3.Rendering.VRCAsyncGPUReadback.Request", new[] { "OnAsyncGpuReadbackComplete" } },
            };

            /// <summary>
            /// A call reporting back to this trigger: the event it reports to exists, the Custom events it names exist,
            /// the variable it writes exists (and isn't temporary, or synced / watched, which a direct write bypasses).
            /// Network events with values can't come here: Custom events take no values. False stops the action.
            /// </summary>
            bool CheckCallBack(CallSpec c, ActionCall call, int ev, int act)
            {
                if (c.DeclaringType == "VRC.SDK3.UdonNetworkCalling.NetworkCalling" && c.Member == "SendCustomNetworkEvent" && c.Params.Count > 3)
                {
                    Error(Texts.T("A trigger's Custom events take no values, so they can't receive this network event. Send it to an UdonSharp script with a [NetworkCallable] method, or put the values in synced variables.",
                                  "トリガーのカスタムイベントは値を受け取れないので、このネットワークイベントは届きません。[NetworkCallable] のメソッドを持つ UdonSharp のスクリプトに送るか、値を同期する変数に入れてください。"), ev, act);
                    return false;
                }
                if (ReportsTo.TryGetValue(c.DeclaringType + "." + c.Member, out var events) && !p.Events.Any(e => events.Contains(e.EventId)))
                    Warn(Texts.T("The result comes back to this trigger as an event it doesn't have: add " + string.Join(" or ", events) + ".",
                                 "結果はこのトリガーのイベントとして届きますが、そのイベントがありません。「" + string.Join("」か「", events.Select(id => EventCatalog.Get(id) is EventSpec es ? Texts.EventName(es) : id)) + "」を足してください。"), ev, act);
                int first = c.Instance != null ? 1 : 0;
                for (int i = 0; i < c.Params.Count; i++)
                {
                    var arg = first + i < call.Args.Count ? call.Args[first + i] : null;
                    if (c.Params[i].Type.Kind != ValueKind.String || arg?.Source != ArgSource.Constant || !(arg.Constant is string text) || text.Length == 0) continue;
                    var pname = c.Params[i].Name ?? "";
                    if (pname == "variableName")
                    {
                        if (!vars.TryGetValue(text, out var target))
                            Warn(Texts.T("This trigger has no variable named '" + text + "'.", "このトリガーに「" + text + "」という変数はありません。"), ev, act, first + i);
                        else if (target.Temporary)
                        { Error(Texts.T("'" + text + "' is temporary: nothing outside the event can write it.", "変数「" + text + "」は一時的なので、イベントの外からは書き込めません。"), ev, act, first + i); return false; }
                        else if (TweenKind(c.Member) is ValueKind want && (target.Kind != want || target.Type.IsArray))
                        { Error(Texts.T(c.Member + " writes a " + Texts.TypeName(ParamType.Of(want)) + "; '" + text + "' is " + Texts.TypeName(target.Type) + ".", c.Member + " が書き込むのは " + Texts.TypeName(ParamType.Of(want)) + " ですが、変数「" + text + "」は " + Texts.TypeName(target.Type) + " です。"), ev, act, first + i); return false; }
                        else if (target.SaveKey != null)
                            Warn(Texts.T("This writes '" + text + "' directly, so it isn't saved (nor synced, nor its change event run). Set it from the update event instead.",
                                         "変数「" + text + "」に直接書き込むので、保存されません（「変わったとき」も動きません）。途中で呼ぶイベントの中で、「変数を変える」で入れてください。"), ev, act, first + i);
                        else if (NeedsSetter(target))
                            Warn(Texts.T("This writes '" + text + "' directly: other players don't get it and its change event doesn't run. Set it from the update event instead.",
                                         "変数「" + text + "」に直接書き込むので、ほかの人に届かず、「変わったとき」も動きません。途中で呼ぶイベントの中で、「変数を変える」で入れてください。"), ev, act, first + i);
                    }
                    else if (System.Text.RegularExpressions.Regex.IsMatch(pname, "(?i)event|^on[A-Z]"))
                    {
                        var named = p.Events.FirstOrDefault(e => e.EventId == EventCatalog.CustomId && e.Name == text);
                        if (named == null)
                            Warn(Texts.T("This trigger has no Custom event named '" + text + "'.", "このトリガーに「" + text + "」というカスタムイベントはありません。"), ev, act, first + i);
                        // onUpdate is called every frame while the tween runs: sent over the network or delayed, it floods or piles up.
                        else if (pname == "onUpdate" && (named.Broadcast != Broadcast.Local || named.DelaySeconds > 0f))
                            Warn(Texts.T("'" + text + "' is called every frame while the tween runs; set it to Only my screen (Local) with no delay, or it sends or schedules a run every frame.",
                                         "「" + text + "」はトゥイーンの間、毎フレーム呼ばれます。「自分だけ（Local）」で遅らせない設定にしてください。そうしないと、毎フレーム送信や予約が増えます。"), ev, act, first + i);
                    }
                }
                return true;
            }

            /// <summary>The kind VRCTween's TweenX writes into its variableName, or null for other members.</summary>
            static ValueKind? TweenKind(string member) => member switch
            {
                "TweenFloat" => ValueKind.Float, "TweenInt" => ValueKind.Int, "TweenColor" => ValueKind.Color, "TweenVector3" => ValueKind.Vector3, _ => (ValueKind?)null,
            };

            static string FilledInPlace(VariableDecl v) =>
                Texts.T("Changing what '" + v.Name + "' holds this way doesn't count as a change: other players don't get it and its change event doesn't run. Use Set Variable.",
                        "変数「" + v.Name + "」の中身をここで書き換えても変化として扱われず、ほかの人に届かず、「変わったとき」も動きません。「変数を変える」を使ってください。");
        }
    }
}
