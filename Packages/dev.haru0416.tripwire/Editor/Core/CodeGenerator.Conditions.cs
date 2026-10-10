using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Conditions: the tests that let an event or a block run, written as cheaply as the Udon VM allows.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            /// <summary>
            /// The test for a list of conditions: true when they hold (holds) or when they don't (!holds), so an event can
            /// return early and a block can branch. Null when there are none (or all are in error, reported).
            /// </summary>
            string ConditionTest(List<Condition> conditions, bool any, int ev, int act, bool holds)
            {
                var parts = new List<string>();
                for (int c = 0; c < conditions.Count; c++)
                {
                    var cond = conditions[c];
                    VariableDecl v;
                    if (cond.Variable == null || !vars.TryGetValue(cond.Variable, out v)) { Error(MissingVariable(cond.Variable), ev, act, cond: c); continue; }
                    bool ordered = cond.Op != CompareOp.Equal && cond.Op != CompareOp.NotEqual;
                    bool number = !v.Type.IsArray && (v.Kind == ValueKind.Int || v.Kind == ValueKind.Float);
                    if (ordered && !number) { Error(Texts.T("Only number variables can be compared with < or >.", "大小を比べられるのは数の変数だけです。"), ev, act, cond: c); continue; }
                    if (!v.Type.HasEquality) { Error(Texts.T("Variables of type " + Texts.TypeName(v.Type) + " cannot be compared.", Texts.TypeName(v.Type) + " の変数は条件に使えません。"), ev, act, cond: c); continue; }
                    // On/off against true / false is written as x or !x (see Test): no value to read.
                    bool flag = v.Kind == ValueKind.Bool && !v.Type.IsArray && !ordered && cond.Value?.Source == ArgSource.Constant && cond.Value.Constant is bool;
                    string rhs = flag ? null : ValueExpr(v.Type, cond.Value, ev, act, -1, c);
                    if (rhs == null && !flag) continue;
                    // A negated condition holds exactly when the plain one fails.
                    bool wantHold = holds != cond.Negate;
                    parts.Add(Test(v, cond, rhs, wantHold));
                }
                if (parts.Count == 0) return null;
                // All must hold: holds = a && b, fails = !a || !b. Any one holds: holds = a || b, fails = !a && !b.
                bool joinWithOr = holds == any;
                return string.Join(joinWithOr ? " || " : " && ", parts);
            }

            /// <summary>
            /// The expression that is true when a condition holds (or, with !hold, when it doesn't), without a separate
            /// '!' where that is exact (each '!' or comparison is one EXTERN on the Udon VM). Float and array comparisons
            /// other than == / != keep '!(...)': NaN would invert differently.
            /// </summary>
            string Test(VariableDecl v, Condition cond, string rhs, bool hold)
            {
                var f = FieldOf(v.Name);
                bool eq = cond.Op == CompareOp.Equal || cond.Op == CompareOp.NotEqual;
                if (v.Kind == ValueKind.Bool && !v.Type.IsArray && eq && cond.Value?.Source == ArgSource.Constant && cond.Value.Constant is bool b)
                    return (b == (cond.Op == CompareOp.Equal)) == hold ? f : "!" + f;
                if (hold) return f + " " + OpText(cond.Op) + " " + rhs;
                if ((v.Kind == ValueKind.Int || eq) && !v.Type.IsArray && v.Kind != ValueKind.Float)
                    return f + " " + OpText(Inverse(cond.Op)) + " " + rhs;
                return "!(" + f + " " + OpText(cond.Op) + " " + rhs + ")";
            }

            static CompareOp Inverse(CompareOp op)
            {
                switch (op)
                {
                    case CompareOp.Equal: return CompareOp.NotEqual;
                    case CompareOp.NotEqual: return CompareOp.Equal;
                    case CompareOp.Less: return CompareOp.GreaterOrEqual;
                    case CompareOp.LessOrEqual: return CompareOp.Greater;
                    case CompareOp.Greater: return CompareOp.LessOrEqual;
                    default: return CompareOp.Less;
                }
            }

            static string OpText(CompareOp op)
            {
                switch (op)
                {
                    case CompareOp.NotEqual: return "!=";
                    case CompareOp.Less: return "<";
                    case CompareOp.LessOrEqual: return "<=";
                    case CompareOp.Greater: return ">";
                    case CompareOp.GreaterOrEqual: return ">=";
                    default: return "==";
                }
            }
        }
    }
}
