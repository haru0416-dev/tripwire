using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tripwire.Core
{
    // Passes over the finished text of the methods: unread event values dropped, single-use bodies inlined, repeated
    // GetComponent calls shared. They match the generator's own layout: methods at 8 spaces, statements at 12 and 16,
    // event bodies named _Tw_E<n> that start with their condition guard (with or without a history note). A change to
    // how methods are written must keep these passes in step.
    public static partial class CodeGenerator
    {
        sealed partial class Generator
        {
            /// <summary>
            /// An event's values are copied into fields at its entry so the actions can use them. Values no action
            /// reads (also through text like "{player}") would only cost a copy per event: drop the field and copies.
            /// </summary>
            void DropUnreadEventValues()
            {
                var code = methods.ToString();
                foreach (var (field, param) in eventValueFields)
                {
                    var copy = "            " + field + " = " + param + ";\n";
                    int copies = (code.Length - code.Replace(copy, "").Length) / copy.Length;
                    int uses = System.Text.RegularExpressions.Regex.Matches(code, @"\b" + field + @"\b").Count;
                    if (uses > copies) continue;
                    code = code.Replace(copy, "");
                    fields.Replace("        " + fieldTypes[field] + " " + field + ";\n", "");
                }
                methods.Clear().Append(code);
            }

            /// <summary>The generated methods, split at their "        sig\n        {" … "        }\n\n" boundaries.</summary>
            static List<string> SplitMethods(string code)
            {
                var list = new List<string>();
                // The body is optional (lazily): an empty method must not swallow the next one.
                foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(code, @"        [^\s{}][^\n]*\n        \{\n(?:.*?\n)??        \}\n\n", System.Text.RegularExpressions.RegexOptions.Singleline))
                    list.Add(m.Value);
                // Each piece must be one method: a method end inside a piece, or pieces not covering the code, means
                // the code has a shape these tidy-ups don't know; they then leave it alone.
                const string end = "\n        }\n\n";
                if (list.Any(m => m.IndexOf(end, StringComparison.Ordinal) != m.Length - end.Length) || list.Sum(m => m.Length) != code.Length)
                    return null;
                return list;
            }

            /// <summary>
            /// A local event body called from one place only (and without a Return inside) is written into that place,
            /// saving a call per event: "if (fail) return;" becomes "if (!fail) { … }", and the body gets its own braces
            /// so locals of different events can't clash.
            /// </summary>
            void InlineSingleUseBodies()
            {
                var code = methods.ToString();
                if (SplitMethods(code) == null) return; // an unexpected shape: leave it as it is
                for (bool changed = true; changed;)
                {
                    changed = false;
                    var pieces = SplitMethods(code);
                    if (pieces == null) break;
                    foreach (var method in pieces)
                    {
                        var head = System.Text.RegularExpressions.Regex.Match(method, @"^        void (_Tw_E\d+)\(\)\n        \{\n(.*)\n        \}\n\n$", System.Text.RegularExpressions.RegexOptions.Singleline);
                        if (!head.Success) continue;
                        var name = head.Groups[1].Value;
                        var calls = System.Text.RegularExpressions.Regex.Matches(code, @"\b" + name + @"\b");
                        if (calls.Count != 2) continue; // its own declaration and exactly one call
                        var call = System.Text.RegularExpressions.Regex.Match(code, @"\n( +)" + name + @"\(\);\n");
                        if (!call.Success) continue; // called as part of a longer line (a player filter): keep the call
                        var lines = head.Groups[2].Value.Split('\n').ToList();
                        string test = null, stopped = null;
                        var guard = System.Text.RegularExpressions.Regex.Match(lines[0], @"^            if \((.*)\) (?:\{ (if \(" + TraceFlag + @"\) Tw_Trace\(.*\);) return; \}|return;)$");
                        if (guard.Success) { test = guard.Groups[1].Value; stopped = guard.Groups[2].Success ? guard.Groups[2].Value : null; lines.RemoveAt(0); }
                        if (lines.Any(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"\breturn;"))) continue; // a Return would leave the caller
                        var indent = call.Groups[1].Value;
                        var inner = string.Concat(lines.Select(l => indent + "    " + l.Substring(Math.Min(12, l.Length - l.TrimStart().Length)) + "\n"));
                        string block;
                        if (test != null)
                        {
                            // The opposite of the fail test, without double negations where it's easy.
                            var plain = System.Text.RegularExpressions.Regex.IsMatch(test, @"^!?[\w.]+$");
                            var holds = plain ? (test.StartsWith("!") ? test.Substring(1) : "!" + test)
                                      : test.StartsWith("!(") && test.EndsWith(")") && Balanced(test.Substring(2, test.Length - 3)) ? test.Substring(2, test.Length - 3)
                                      : "!(" + test + ")";
                            block = indent + "if (" + holds + ")\n" + indent + "{\n" + inner + indent + "}\n";
                            if (stopped != null) block += indent + "else " + stopped + "\n";
                        }
                        else block = indent + "{\n" + inner + indent + "}\n";
                        code = code.Remove(call.Index + 1, call.Length - 1).Insert(call.Index + 1, block);
                        code = code.Replace(method, "");
                        changed = true;
                        break;
                    }
                }
                methods.Clear().Append(code);
            }

            static bool Balanced(string s)
            {
                int depth = 0;
                foreach (var ch in s)
                {
                    if (ch == '(') depth++;
                    else if (ch == ')' && --depth < 0) return false;
                }
                return depth == 0;
            }

            /// <summary>The same GetComponent&lt;T&gt;() twice or more in a method (a guard and the use): looked up once at its start.</summary>
            void ShareComponentLookups()
            {
                var code = methods.ToString();
                var pieces = SplitMethods(code);
                if (pieces == null) return;
                int n = 0;
                // String literals are skipped (a log text may mention GetComponent).
                const string literal = @"""(?:[^""\\]|\\.)*""";
                foreach (var method in pieces)
                {
                    var found = System.Text.RegularExpressions.Regex.Matches(method, literal + @"|(?<![\w.])GetComponent<([\w.]+)>\(\)").Cast<System.Text.RegularExpressions.Match>()
                        .Where(m => m.Groups[1].Success).GroupBy(m => m.Groups[1].Value).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                    if (found.Count == 0) continue;
                    var updated = method;
                    var open = updated.IndexOf("        {\n", StringComparison.Ordinal) + "        {\n".Length;
                    var decls = "";
                    foreach (var type in found)
                    {
                        var local = "tw_Self" + n++;
                        updated = System.Text.RegularExpressions.Regex.Replace(updated, literal + @"|(?<![\w.])GetComponent<" + System.Text.RegularExpressions.Regex.Escape(type) + @">\(\)",
                            m => m.Value.StartsWith("\"") ? m.Value : local);
                        decls += "            " + type + " " + local + " = GetComponent<" + type + ">();\n";
                    }
                    updated = updated.Insert(open, decls);
                    code = code.Replace(method, updated);
                }
                methods.Clear().Append(code);
            }
        }
    }
}
