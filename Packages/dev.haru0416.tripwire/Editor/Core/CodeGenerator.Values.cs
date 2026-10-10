using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Values and operands: constants, variables, objects, players, event parameters.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            /// <summary>The kind each constant field was declared with (a field is declared once).</summary>
            readonly Dictionary<string, ValueKind> constantFields = new Dictionary<string, ValueKind>(StringComparer.Ordinal);

            /// <summary>
            /// A value typed in the Inspector: a field named after where the value is (so the name doesn't change with it),
            /// filled by the editor (<see cref="ConstantsInFields"/>); the literal for kinds Udon can't serialize, or
            /// with the setting off.
            /// </summary>
            string ConstantExpr(ValueKind kind, object value, string place)
            {
                if (!ConstantsInFields || !FieldKind(kind)) return Literal(kind, value);
                var field = "tw_C" + place.Replace('-', 'm'); // no action or event (-1, -3) would put '-' in the name
                if (constantFields.TryGetValue(field, out var declared) && declared != kind) field += "_" + kind;
                if (!constantFields.ContainsKey(field))
                {
                    constantFields[field] = kind;
                    fields.Append("        public ").Append(TypeName(ParamType.Of(kind))).Append(' ').Append(field).Append(";\n");
                    Result.Bindings.Add(new FieldBinding { Field = field, Kind = BindingKind.Constant, Constant = ConstantValue(kind, value) });
                }
                return field;
            }

            /// <summary>Where an argument's value is, as part of a field name: event, action, argument (and condition).</summary>
            static string PlaceOf(int ev, int act, int argIndex, int cErr) => ev + "_" + act + "_" + argIndex + (cErr >= 0 ? "_c" + cErr : "");

            /// <summary>
            /// Text with {name} placeholders: each becomes that variable's (or this event's value's) text. {{ and }} are
            /// literal braces.
            /// </summary>
            string TextTemplate(string text, int ev, int act, int argIndex, int cErr)
            {
                var parts = new List<string>();
                var literal = new StringBuilder();
                var spec = EventCatalog.Get(p.Events[ev].EventId);
                // The text between the placeholders: constants of their own (the n-th piece of this text).
                string Piece() { var piece = ConstantExpr(ValueKind.String, literal.ToString(), PlaceOf(ev, act, argIndex, cErr) + "_t" + parts.Count); literal.Clear(); return piece; }
                bool startsWithText = !(text.Length > 0 && text[0] == '{' && !(text.Length > 1 && text[1] == '{'));
                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if ((c == '{' || c == '}') && i + 1 < text.Length && text[i + 1] == c) { literal.Append(c); i++; continue; }
                    if (c == '}') { Error(Texts.T("A '}' without '{'. Write }} for the character itself.", "「{」のない「}」があります。文字として書くときは }} にしてください。"), ev, act, argIndex, cond: cErr); return null; }
                    if (c != '{') { literal.Append(c); continue; }
                    int end = text.IndexOf('}', i + 1);
                    if (end < 0) { Error(Texts.T("A '{' without '}'. Write {{ for the character itself.", "「}」で閉じていない「{」があります。文字として書くときは {{ にしてください。"), ev, act, argIndex, cond: cErr); return null; }
                    // The exact name first (variable names may have spaces at the ends), then without them.
                    var name = text.Substring(i + 1, end - i - 1);
                    if (!vars.ContainsKey(name) && !(spec != null && spec.Params.Any(x => x.Name == name))) name = name.Trim();
                    ArgValue source;
                    if (vars.ContainsKey(name)) source = ArgValue.Var(name);
                    else if (spec != null && spec.Params.Any(x => x.Name == name)) source = ArgValue.Param(name);
                    else if (BuiltInText(name) is string builtIn)
                    {
                        if (literal.Length > 0) parts.Add(Piece());
                        parts.Add(builtIn);
                        i = end;
                        continue;
                    }
                    else
                    {
                        Error(Texts.T("{" + name + "}: no variable or event value has that name. Write {{ and }} for braces themselves.",
                                      "{" + name + "}: その名前の変数（またはイベントの値）がありません。{ } を文字として書くときは {{ }} にしてください。"), ev, act, argIndex, cond: cErr);
                        return null;
                    }
                    var expr = ValueExpr(ParamType.Of(ValueKind.String), source, ev, act, argIndex, cErr);
                    if (expr == null) return null;
                    if (literal.Length > 0) parts.Add(Piece());
                    parts.Add(expr);
                    i = end;
                }
                if (literal.Length > 0 || parts.Count == 0) parts.Add(Piece());
                // Start from text so '+' is string concatenation even when the first part is a converted value.
                if (!startsWithText) parts.Insert(0, "\"\"");
                return string.Join(" + ", parts);
            }

            /// <summary>
            /// Values any text can show without a variable (a variable or event value of the same name comes first):
            /// how many are in the instance, the local player's name, the time (HH:mm).
            /// </summary>
            static string BuiltInText(string name)
            {
                switch (name)
                {
                    case "プレイヤー数": case "playerCount": return "VRCPlayerApi.GetPlayerCount().ToString()";
                    case "自分の名前": case "myName": return "(Utilities.IsValid(Networking.LocalPlayer) ? Networking.LocalPlayer.displayName : \"\")";
                    case "時刻": case "time": return "System.DateTime.Now.ToString(\"HH:mm\")";
                    default: return null;
                }
            }

            /// <summary>Expression for a non-object value: constant or variable.</summary>
            /// <param name="template">Text shown to people (Set Text, Log): {name} in a constant inserts a value. Elsewhere
            /// braces stay literal (event names, conditions, format strings like TMP's "{0:1}").</param>
            string ValueExpr(ParamType want, ArgValue arg, int ev, int act = -1, int argIndex = -1, int cErr = -1, bool template = false)
            {
                if (want.Kind == ValueKind.Int && !want.IsArray && NarrowIntRange(want.UnityType) is var range && range != null)
                {
                    // A narrower integer parameter (byte, short...): an int value, cast.
                    if (arg != null && arg.Source == ArgSource.Variable && arg.Name != null && vars.TryGetValue(arg.Name, out var vd)
                        && vd.Type?.Kind == ValueKind.Other && vd.Type.UnityType == want.UnityType)
                        return ValueExpr(ParamType.OtherType(want.UnityType), arg, ev, act, argIndex, cErr); // a variable of that very type
                    if (arg != null && arg.Source == ArgSource.Constant)
                    {
                        // Old or hand-edited data may hold text, NaN or a huge float here: an error, not an exception.
                        long n;
                        try { n = Convert.ToInt64(arg.Constant ?? 0, CultureInfo.InvariantCulture); }
                        catch (Exception x) when (x is FormatException || x is OverflowException || x is InvalidCastException) { n = long.MinValue; }
                        if (n < range.Value.min || n > range.Value.max)
                        {
                            Error(Texts.T("Enter a number from " + range.Value.min + " to " + range.Value.max + ".", range.Value.min + "〜" + range.Value.max + " の数を入れてください。"), ev, act, argIndex, cond: cErr);
                            return null;
                        }
                    }
                    var inner = ValueExpr(ParamType.Of(ValueKind.Int), arg, ev, act, argIndex, cErr);
                    return inner == null ? null : "((" + want.UnityType + ")(" + inner + "))";
                }
                if (arg == null) { Error(Texts.T("Fill in this value.", "この欄に値を入れてください。"), ev, act, argIndex, cond: cErr); return null; }
                switch (arg.Source)
                {
                    case ArgSource.Constant:
                        if (!IsConstantKindCompatible(want.Kind, arg.Constant)) { Error(Texts.T("Needs a value of type " + Texts.TypeName(want) + ".", "ここに値を入れてください（" + Texts.TypeName(want) + "）。"), ev, act, argIndex, cond: cErr); return null; }
                        if (!EnumMemberExists(want, arg.Constant)) { Error(Texts.T(Texts.TypeName(want) + " has no '" + arg.Constant + "': pick another.", Texts.TypeName(want) + " に「" + arg.Constant + "」はありません。選び直してください。"), ev, act, argIndex, cond: cErr); return null; }
                        if (want.Kind == ValueKind.Url && !want.IsArray)
                        {
                            // No VRCUrl literal exists in Udon: a field the editor fills with this URL.
                            if (string.IsNullOrWhiteSpace((string)arg.Constant)) Warn(Texts.T("No URL entered.", "URL が入っていません。"), ev, act, argIndex);
                            var urlField = ("tw_Url" + ev + "_" + act + "_" + argIndex + (cErr >= 0 ? "_c" + cErr : "")).Replace('-', 'm'); // no action or event (-1, -3) would put '-' in the name
                            if (declaredArgFields.Add(urlField))
                            {
                                fields.Append("        public VRCUrl ").Append(urlField).Append(";\n");
                                Result.Bindings.Add(new FieldBinding { Field = urlField, Kind = BindingKind.ActionArg, Event = ev, Action = act, Arg = argIndex, UnityType = "VRC.SDKBase.VRCUrl", UrlValue = (string)arg.Constant });
                            }
                            return urlField;
                        }
                        if (template && want.Kind == ValueKind.String && !want.IsArray && arg.Constant is string text && text.IndexOfAny(new[] { '{', '}' }) >= 0)
                            return TextTemplate(text, ev, act, argIndex, cErr);
                        return want.Kind == ValueKind.Enum ? Literal(want, arg.Constant) : ConstantExpr(want.Kind, arg.Constant, PlaceOf(ev, act, argIndex, cErr));
                    case ArgSource.Variable:
                        VariableDecl v;
                        if (arg.Name == null || !vars.TryGetValue(arg.Name, out v)) { Error(MissingVariable(arg.Name), ev, act, argIndex, cond: cErr); return null; }
                        if (!IsAssignable(want, v.Type))
                        {
                            // Text takes any value that reads as text (a number, a player's name...).
                            if (want.Kind == ValueKind.String && !want.IsArray && TextOf(v.Type, FieldOf(v.Name)) is string asText) return asText;
                            Error(TypeMismatch(v, want), ev, act, argIndex, cond: cErr);
                            return null;
                        }
                        // Without the cast an int variable picks the int overload (e.g. Random.Range(int, int)).
                        return want.Kind == ValueKind.Float && v.Kind == ValueKind.Int ? "(float)" + FieldOf(v.Name) : FieldOf(v.Name);
                    case ArgSource.EventParam:
                    {
                        var spec = EventCatalog.Get(p.Events[ev].EventId);
                        var ep = spec?.Params.FirstOrDefault(x => x.Name == arg.Name);
                        bool asText = ep != null && !IsAssignable(want, ep.Type) && want.Kind == ValueKind.String && !want.IsArray && TextOf(ep.Type, "x") != null;
                        if (ep == null || (!IsAssignable(want, ep.Type) && !asText)) { Error(Texts.T("This event has no such value.", "このイベントには、その名前の値がありません。"), ev, act, argIndex, cond: cErr); return null; }
                        if (!EventValueUsable(ev, act, argIndex, cErr)) return null;
                        return asText ? TextOf(ep.Type, ArgFieldFor(ev, spec, ep.Name)) : ArgFieldFor(ev, spec, ep.Name);
                    }
                    default:
                        Error(Texts.T("Expected a value or a variable.", "値か変数を選んでください。"), ev, act, argIndex, cond: cErr);
                        return null;
                }
            }

            sealed class Operand
            {
                public string Expr;
                /// <summary>Array (bound field or variable) holding several objects; the action loops over it.</summary>
                public string ArrayField;
                /// <summary>The array itself may be null (a variable), so the loop needs a guard.</summary>
                public bool ArrayMayBeNull;
                /// <summary>Expressions that must pass Utilities.IsValid, in order, before the action may touch this operand.</summary>
                public List<string> Guards = new List<string>();

                public Operand Guarded() { Guards.Add(Expr); return this; }

                /// <summary>Guards for passing or storing the value: null itself is fine, what it is read from must exist.</summary>
                public List<string> GuardsOfSources() => Guards.Where(g => g != Expr).ToList();
            }

            /// <param name="scalarIfSingle">The operand is looped over (an action's targets): one assigned object becomes a
            /// plain field instead of a one-element array, which saves the loop on the Udon VM.</param>
            Operand ObjectOperand(EventSpec spec, ParamType want, ArgValue arg, int ev, int act, int argIndex, string paramName, bool scalarIfSingle = false)
            {
                switch (arg.Source)
                {
                    case ArgSource.Objects:
                    {
                        // A field the editor fills with the assigned objects.
                        var field = "tw_A" + ev + "_" + act + "_" + paramName;
                        void Bound(bool array)
                        {
                            fields.Append("        public ").Append(want.UnityType).Append(array ? "[] " : " ").Append(field).Append(";\n");
                            Result.Bindings.Add(new FieldBinding { Field = field, Kind = BindingKind.ActionArg, Event = ev, Action = act, Arg = argIndex, UnityType = want.UnityType, IsArray = array });
                        }
                        if (want.IsArray && !(scalarIfSingle && arg.ObjectCount == 1))
                        {
                            if (arg.ObjectCount == 0) Warn(Texts.T("No objects assigned; this action does nothing.", "対象が入っていないので、何も起きません。"), ev, act, argIndex);
                            Bound(true);
                            return new Operand { ArrayField = field };
                        }
                        if (arg.ObjectCount != 1) { Error(Texts.T("Assign exactly one object.", "オブジェクトを 1 つ入れてください。"), ev, act, argIndex); return null; }
                        Bound(false);
                        return new Operand { Expr = field }.Guarded();
                    }
                    case ArgSource.Self:
                        if (want.UnityType == "UnityEngine.GameObject") return new Operand { Expr = "gameObject" };
                        if (want.UnityType == "UnityEngine.Transform") return new Operand { Expr = "transform" };
                        if (want.UnityType == ParamType.Behaviour) return new Operand { Expr = "this" };
                        // GetComponent on a non-component type throws at runtime, which halts the whole behaviour.
                        if (!want.IsComponent) { Error(Texts.T("'This GameObject' cannot provide a " + Texts.TypeName(want) + ".", "「このオブジェクト」は " + Texts.TypeName(want) + " として使えません。"), ev, act, argIndex); return null; }
                        return new Operand { Expr = "GetComponent<" + want.UnityType + ">()" }.Guarded();
                    case ArgSource.EventParam:
                    {
                        var ep = spec.Params.FirstOrDefault(x => x.Name == arg.Name);
                        if (ep == null || ep.Type.Kind != ValueKind.Object) { Error(Texts.T("This event has no object parameter '" + arg.Name + "'.", "このイベントには、その値がありません。"), ev, act, argIndex); return null; }
                        if (!EventValueUsable(ev, act, argIndex)) return null;
                        var src = ArgFieldFor(ev, spec, ep.Name);
                        var op = new Operand { Expr = src }.Guarded();
                        if (ep.Type.UnityType == want.UnityType) return op;
                        if (want.UnityType == "UnityEngine.GameObject") { op.Expr = src + ".gameObject"; return op; }
                        if (want.UnityType == "UnityEngine.Transform") { op.Expr = src + ".transform"; return op; }
                        if (!want.IsComponent) { Error(Texts.T("The event's " + ep.Name + " cannot provide a " + Texts.TypeName(want) + ".", "「" + Texts.EventValueName(spec, ep.Name) + "」は " + Texts.TypeName(want) + " として使えません。"), ev, act, argIndex); return null; }
                        op.Expr = src + ".GetComponent<" + want.UnityType + ">()";
                        return op.Guarded();
                    }
                    case ArgSource.Variable:
                        return VariableOperand(want, arg, ev, act, argIndex);
                    default:
                        Error(Texts.T("Expected objects for '" + paramName + "'.", "オブジェクトを入れてください。"), ev, act, argIndex);
                        return null;
                }
            }

            /// <summary>A variable used where objects, a player or any non-literal value is expected.</summary>
            Operand VariableOperand(ParamType want, ArgValue arg, int ev, int act, int argIndex)
            {
                VariableDecl v;
                if (arg.Name == null || !vars.TryGetValue(arg.Name, out v)) { Error(MissingVariable(arg.Name), ev, act, argIndex); return null; }
                var f = FieldOf(v.Name);
                bool nullable = v.Kind == ValueKind.Object || v.Kind == ValueKind.Player;
                if (want.IsArray && (want.Kind == ValueKind.Object || want.Kind == ValueKind.Player))
                {
                    // A target list: an array variable is looped over, a single one is used as one target.
                    if (v.Type.IsArray && IsAssignable(want.Element(), v.Type.Element()))
                        return new Operand { ArrayField = f, ArrayMayBeNull = true };
                    if (!v.Type.IsArray && IsAssignable(want.Element(), v.Type))
                        return new Operand { Expr = f }.Guarded();
                }
                else if (IsAssignable(want, v.Type))
                {
                    var op = new Operand { Expr = f };
                    return nullable && !v.Type.IsArray ? op.Guarded() : op;
                }
                Error(TypeMismatch(v, want), ev, act, argIndex);
                return null;
            }

            /// <summary>An event's values exist only where it fired: refused unless Local; a delay may overwrite them.</summary>
            bool EventValueUsable(int ev, int act, int argIndex, int cErr = -1)
            {
                var e = p.Events[ev];
                if (e.Broadcast != Broadcast.Local) { Error(LocalOnlyParam(), ev, act, argIndex, cond: cErr); return false; }
                if (e.DelaySeconds > 0f) Warn(DelayedParam(), ev, act, argIndex);
                return true;
            }

            Operand PlayerOperand(EventSpec spec, ArgValue arg, int ev, int act, int argIndex)
            {
                switch (arg.Source)
                {
                    case ArgSource.LocalPlayer:
                        return new Operand { Expr = "Networking.LocalPlayer" }.Guarded();
                    case ArgSource.EventParam:
                    {
                        var ep = spec.Params.FirstOrDefault(x => x.Name == arg.Name);
                        if (ep == null || ep.Type.Kind != ValueKind.Player) { Error(Texts.T("This event has no player parameter '" + arg.Name + "'.", "このイベントには、そのプレイヤーの値がありません。"), ev, act, argIndex); return null; }
                        if (!EventValueUsable(ev, act, argIndex)) return null;
                        return new Operand { Expr = ArgFieldFor(ev, spec, ep.Name) }.Guarded();
                    }
                    case ArgSource.Variable:
                        return VariableOperand(ParamType.Of(ValueKind.Player), arg, ev, act, argIndex);
                    default:
                        Error(Texts.T("Expected a player.", "プレイヤーを選んでください。"), ev, act, argIndex);
                        return null;
                }
            }
        }
    }
}
