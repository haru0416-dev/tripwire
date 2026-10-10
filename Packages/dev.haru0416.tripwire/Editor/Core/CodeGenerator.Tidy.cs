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
            /// so locals of different events can't clash. The methods are split once and edited as pieces, with plain
            /// string searches: re-splitting and regex counting the whole code per inlined body grew with the square of
            /// the trigger's size (a quarter second for 70 events under the editor's Mono).
            /// </summary>
            void InlineSingleUseBodies()
            {
                var pieces = SplitMethods(methods.ToString());
                if (pieces == null) return; // an unexpected shape: leave it as it is
                for (int mi = 0; mi < pieces.Count; mi++)
                {
                    var method = pieces[mi];
                    var head = BodyHead.Match(method);
                    if (!head.Success) continue;
                    var name = head.Groups[1].Value;
                    // Its own declaration and exactly one call, in another method.
                    int uses = 0, caller = -1;
                    for (int pi = 0; pi < pieces.Count && uses <= 2; pi++)
                    {
                        int n = CountName(pieces[pi], name);
                        uses += n;
                        if (n > 0 && pi != mi) caller = pi;
                    }
                    if (uses != 2 || caller < 0) continue;
                    var callerText = pieces[caller];
                    int lineStart = CallLine(callerText, name, out var indent);
                    if (lineStart < 0) continue; // called as part of a longer line (a player filter): keep the call
                    var lines = head.Groups[2].Value.Split('\n').ToList();
                    string test = null, stopped = null;
                    var guard = BodyGuard.Match(lines[0]);
                    if (guard.Success) { test = guard.Groups[1].Value; stopped = guard.Groups[2].Success ? guard.Groups[2].Value : null; lines.RemoveAt(0); }
                    if (lines.Any(l => CountName(l, "return;") > 0)) continue; // a Return would leave the caller
                    var inner = string.Concat(lines.Select(l => indent + "    " + l.Substring(Math.Min(12, l.Length - l.TrimStart().Length)) + "\n"));
                    string block;
                    if (test != null)
                    {
                        // The opposite of the fail test, without double negations where it's easy.
                        var plain = PlainTest.IsMatch(test);
                        var holds = plain ? (test.StartsWith("!", StringComparison.Ordinal) ? test.Substring(1) : "!" + test)
                                  : test.StartsWith("!(", StringComparison.Ordinal) && test.EndsWith(")", StringComparison.Ordinal) && Balanced(test.Substring(2, test.Length - 3)) ? test.Substring(2, test.Length - 3)
                                  : "!(" + test + ")";
                        block = indent + "if (" + holds + ")\n" + indent + "{\n" + inner + indent + "}\n";
                        if (stopped != null) block += indent + "else " + stopped + "\n";
                    }
                    else block = indent + "{\n" + inner + indent + "}\n";
                    int lineEnd = lineStart + indent.Length + name.Length + "();\n".Length;
                    pieces[caller] = callerText.Remove(lineStart, lineEnd - lineStart).Insert(lineStart, block);
                    pieces.RemoveAt(mi);
                    // Carry on at the next piece: inlining moves the calls inside a body but adds or removes no other, so
                    // the bodies already passed stay as they were (looking again from the start was square in the bodies).
                    mi--;
                }
                methods.Clear().Append(string.Concat(pieces));
            }

            static readonly System.Text.RegularExpressions.Regex BodyHead =
                new System.Text.RegularExpressions.Regex(@"^        void (_Tw_E\d+)\(\)\n        \{\n(.*)\n        \}\n\n$", System.Text.RegularExpressions.RegexOptions.Singleline);
            static readonly System.Text.RegularExpressions.Regex BodyGuard =
                new System.Text.RegularExpressions.Regex(@"^            if \((.*)\) (?:\{ (if \(" + TraceFlag + @"\) Tw_Trace\(.*\);) return; \}|return;)$");
            static readonly System.Text.RegularExpressions.Regex PlainTest = new System.Text.RegularExpressions.Regex(@"^!?[\w.]+$");

            /// <summary>A word character as regex \w and \b see it: letters, combining marks, digits, connectors, ZWNJ / ZWJ.</summary>
            static bool IsWordChar(char c)
            {
                if (c == '\u200C' || c == '\u200D') return true;
                switch (char.GetUnicodeCategory(c))
                {
                    case System.Globalization.UnicodeCategory.UppercaseLetter: case System.Globalization.UnicodeCategory.LowercaseLetter:
                    case System.Globalization.UnicodeCategory.TitlecaseLetter: case System.Globalization.UnicodeCategory.ModifierLetter:
                    case System.Globalization.UnicodeCategory.OtherLetter: case System.Globalization.UnicodeCategory.NonSpacingMark:
                    case System.Globalization.UnicodeCategory.DecimalDigitNumber: case System.Globalization.UnicodeCategory.ConnectorPunctuation:
                        return true;
                    default:
                        return false;
                }
            }

            /// <summary>How often <paramref name="name"/> occurs as a whole word (as \b…\b would match it).</summary>
            static int CountName(string text, string name)
            {
                int n = 0;
                for (int i = text.IndexOf(name, StringComparison.Ordinal); i >= 0; i = text.IndexOf(name, i + 1, StringComparison.Ordinal))
                {
                    bool before = i == 0 || !IsWordChar(text[i - 1]) || !IsWordChar(name[0]);
                    int after = i + name.Length;
                    bool behind = after >= text.Length || !IsWordChar(text[after]) || !IsWordChar(name[name.Length - 1]);
                    if (before && behind) n++;
                }
                return n;
            }

            /// <summary>Where the line that is just "&lt;indent&gt;name();" starts (after its newline), or -1.</summary>
            static int CallLine(string text, string name, out string indent)
            {
                indent = null;
                var call = name + "();\n";
                for (int i = text.IndexOf(call, StringComparison.Ordinal); i >= 0; i = text.IndexOf(call, i + 1, StringComparison.Ordinal))
                {
                    int s = i;
                    while (s > 0 && text[s - 1] == ' ') s--;
                    if (s < i && s > 0 && text[s - 1] == '\n') { indent = text.Substring(s, i - s); return s; }
                }
                return -1;
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
                            m => m.Value.StartsWith("\"", StringComparison.Ordinal) ? m.Value : local);
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
