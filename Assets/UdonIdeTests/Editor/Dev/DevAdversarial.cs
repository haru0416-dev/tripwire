using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UdonSharp;
using UdonSharp.Compiler;
using UnityEditor;
using UdonBridge;

namespace UdonIde.Dev
{
    // Adversarial answer key (batch mode): -executeMethod UdonIde.Dev.DevAdversarial.Run [-only <part>]
    // Cases from StageBCases/cases.txt: "=== name", then sections "--- body" / "--- class" (into Door.cs) or
    // "--- file <path>" (whole file). The edited file is always Door.cs; the others are saved for both sides.
    // Truth: UdonSharp's real compile with everything saved. Check: UdonCheck on Door's text, unsaved.
    // Then: typing a complex file 5 characters at a time, and stress inputs. Logs/adversarial.txt (written as it goes).
    public static class DevAdversarial
    {
        const string Door = "Assets/UdonIdeTests/Fixtures/Door.cs";
        static readonly string[] Files = { Door, "Assets/UdonIdeTests/Fixtures/StageB/Helper.cs", "Assets/UdonIdeTests/Fixtures/StageB/BaseDoor.cs", "Assets/UdonIdeTests/Fixtures/StageB/Mode.cs" };
        static readonly Type cacheType = typeof(UdonSharpProgramAsset).Assembly.GetType("UdonSharp.UdonSharpEditorCache");
        const string Log = "Logs/adversarial.txt";

        static void Say(string line) => File.AppendAllText(Log, line + "\n");

        sealed class Case { public string Name; public Dictionary<string, string> Files = new Dictionary<string, string>(); public string Body, Class; }

        static List<Case> Load(string path)
        {
            var cases = new List<Case>();
            Case c = null; string section = null; var buf = new StringBuilder();
            void Flush()
            {
                if (c == null || section == null) return;
                var text = buf.ToString().TrimEnd('\n') + "\n";
                if (section == "body") c.Body = text; else if (section == "class") c.Class = text; else c.Files[section] = text;
                buf.Clear(); section = null;
            }
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.StartsWith("=== ")) { Flush(); c = new Case { Name = line.Substring(4) }; cases.Add(c); continue; }
                if (line.StartsWith("--- ")) { Flush(); var s = line.Substring(4); section = s.StartsWith("file ") ? s.Substring(5) : s; continue; }
                buf.Append(line).Append('\n');
            }
            Flush();
            return cases;
        }

        static string Indent(string code, string pad) => string.Join("\n", code.TrimEnd('\n').Split('\n').Select(l => l.Length == 0 || l.StartsWith("#") ? l : pad + l));

        static string DoorText(Case c, string original)
        {
            if (c.Files.TryGetValue(Door, out var whole)) return whole;
            var t = original;
            if (c.Body != null) t = t.Replace("        RequestSerialization();", "        RequestSerialization();\n" + Indent(c.Body, "        "));
            if (c.Class != null) t = t.Replace("    public GameObject panel;", "    public GameObject panel;\n" + Indent(c.Class, "    "));
            if (!t.Contains("using VRC.SDKBase;")) t = "using VRC.SDKBase;\n" + t;
            return t;
        }

        static string Normalize(string m) => Regex.Replace(UdonSharpCheck.Readable(m), "^(error|warning) (CS\\d+)", "$2");

        /// <summary>Errors of UdonSharp's real compile: (file, line, message).</summary>
        static List<(string file, int line, string message)> Truth()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { DisableLogging = true });
            var cache = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var list = new List<(string, int, string)>();
            foreach (var d in (IEnumerable)cacheType.GetProperty("LastCompileDiagnostics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cache))
            {
                var t = d.GetType();
                if (t.GetField("severity").GetValue(d).ToString() != "Error") continue;
                var file = ((string)t.GetField("file").GetValue(d) ?? "").Replace('\\', '/');
                int at = file.IndexOf("Assets/", StringComparison.Ordinal);
                list.Add((at >= 0 ? file.Substring(at) : file, (int)t.GetField("line").GetValue(d), Normalize((string)t.GetField("message").GetValue(d))));
            }
            return list.Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
        }

        public static void Run()
        {
            File.WriteAllText(Log, "");
            var args = Environment.GetCommandLineArgs();
            int oi = Array.IndexOf(args, "-only");
            var only = oi >= 0 ? args[oi + 1] : "all";
            var originals = Files.ToDictionary(f => f, File.ReadAllText);
            try
            {
                // Door needs a program asset to be one of UdonSharp's roots (the real compile binds roots only).
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/UdonIdeTests/Fixtures/Door.asset") == null)
                {
                    var asset = UnityEngine.ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    asset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(Door);
                    AssetDatabase.CreateAsset(asset, "Assets/UdonIdeTests/Fixtures/Door.asset");
                    UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { DisableLogging = true });
                }
                UdonCheck.Check(Door, originals[Door], out _, out _); // warm
                if (only == "all" || only == "cases") Cases(originals);
                if (only == "all" || only == "typing") Typing(originals);
                if (only == "all" || only == "stress") Stress(originals);
            }
            catch (Exception e) { Say("EXCEPTION " + e); }
            finally
            {
                foreach (var kv in originals) File.WriteAllText(kv.Key, kv.Value);
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { DisableLogging = true });
                Say("done");
                EditorApplication.Exit(0);
            }
        }

        static void Cases(Dictionary<string, string> originals)
        {
            var cases = Load("tests/UdonIde/StageBCases/cases.txt");
            int same = 0, missed = 0, falseAlarm = 0, otherWording = 0;
            var times = new List<double>();
            var summary = new StringBuilder();
            foreach (var c in cases)
            {
                foreach (var kv in originals) File.WriteAllText(kv.Key, kv.Key == Door ? kv.Value : c.Files.TryGetValue(kv.Key, out var t) ? t : kv.Value);
                UdonCheck.Rescan();
                var text = DoorText(c, originals[Door]);
                var sw = Stopwatch.StartNew();
                List<Problem> mine;
                try { mine = UdonCheck.Check(Door, text, out _, out _); }
                catch (Exception e) { mine = new List<Problem> { new Problem { Error = true, Message = "CHECK THREW " + e.GetType().Name + ": " + e.Message } }; }
                times.Add(sw.Elapsed.TotalMilliseconds);
                var ours = mine.Where(p => p.Error).Select(p => (file: p.File ?? Door, line: p.Line, message: Normalize(p.Message))).Distinct().OrderBy(x => x.file).ThenBy(x => x.line).ToList();

                File.WriteAllText(Door, text);
                var truth = Truth();
                truth = truth.Select(x => (x.file == "" ? Door : x.file, x.line, x.message)).OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();

                string verdict;
                if (ours.SequenceEqual(truth)) { verdict = "SAME"; same++; }
                else if (truth.Count == 0 && ours.Count > 0) { verdict = "FALSE ALARM"; falseAlarm++; }
                else if (truth.Count > 0 && ours.Count == 0) { verdict = "MISSED"; missed++; }
                else { verdict = "WORDING/PLACE"; otherWording++; }
                summary.AppendLine($"{verdict,-13} {c.Name}  ({sw.Elapsed.TotalMilliseconds:F0} ms)");
                Say($"{verdict,-13} {c.Name}  ({times.Last():F0} ms)");
                Say("    truth: " + (truth.Count == 0 ? "(none)" : string.Join(" | ", truth.Select(x => (x.file == Door ? "" : x.file + " ") + x.line + " " + x.message))));
                if (verdict != "SAME") Say("    ours : " + (ours.Count == 0 ? "(none)" : string.Join(" | ", ours.Select(x => (x.file == Door ? "" : x.file + " ") + x.line + " " + x.message))));
            }
            foreach (var kv in originals) File.WriteAllText(kv.Key, kv.Value);
            UdonCheck.Rescan();
            times.Sort();
            Say($"\nCASES {cases.Count}: same {same}, missed {missed}, false alarm {falseAlarm}, other wording/place {otherWording}; check median {times[times.Count / 2]:F0} ms, max {times.Max():F0} ms\n");
        }

        /// <summary>Types a complex valid file a few characters at a time, checking each step as the editor would.</summary>
        static void Typing(Dictionary<string, string> originals)
        {
            var full = File.ReadAllText("tests/UdonIde/StageBCases/typing.cs");
            int steps = 0, threw = 0, binderFailed = 0, reachedB = 0;
            var prefixClean = new List<(string what, string text, List<Problem> ours)>();
            var times = new List<double>();
            var firstThrows = new List<string>();
            for (int k = 1; k <= full.Length; k += 5)
            {
                var text = full.Substring(0, Math.Min(k, full.Length));
                var sw = Stopwatch.StartNew();
                try
                {
                    var ps = UdonCheck.Check(Door, text, out _, out _);
                    if (!ps.Any(p => p.Error && p.Source == "C#")) { reachedB++; if (k % 3 == 1) prefixClean.Add(("prefix " + k, text, ps)); }
                    if (ps.Any(p => p.Message.StartsWith("UdonSharp's binder failed") || p.Message.StartsWith("System.") && !p.Message.StartsWith("System.NotSupported"))) 
                    {
                        binderFailed++;
                        if (firstThrows.Count < 6) firstThrows.Add($"at {k}: " + ps.First(p => p.Message.StartsWith("UdonSharp's binder failed") || p.Message.StartsWith("System.")).Message);
                    }
                }
                catch (Exception e) { threw++; if (firstThrows.Count < 6) firstThrows.Add($"at {k}: THREW {e.GetType().Name}: {e.Message}"); }
                times.Add(sw.Elapsed.TotalMilliseconds);
                steps++;
            }
            Say($"TYPING (prefixes) reached UdonSharp's steps in {reachedB} of {steps} checks");

            // States that parse but mean odd things: each line removed, and each identifier cut short.
            var lines = full.Split('\n');
            var variants = new List<(string what, string text)>();
            for (int i = 0; i < lines.Length; i++)
                if (lines[i].Trim().Length > 0) variants.Add(("without line " + (i + 1), string.Join("\n", lines.Where((_, j) => j != i))));
            foreach (Match m in Regex.Matches(full, "\\b[A-Za-z_][A-Za-z0-9_]{3,}\\b"))
                if (m.Index > full.IndexOf("public class", StringComparison.Ordinal)) variants.Add(("cut " + m.Value + " at " + m.Index, full.Remove(m.Index + m.Length / 2, m.Length - m.Length / 2)));
            int vThrew = 0, vInternal = 0, vReached = 0;
            var clean = new List<(string what, string text, List<Problem> ours)>();
            var vTimes = new List<double>();
            var odd = new List<string>();
            foreach (var (what, text) in variants)
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    var ps = UdonCheck.Check(Door, text, out _, out _);
                    if (!ps.Any(p => p.Error && p.Source == "C#")) { vReached++; clean.Add((what, text, ps)); }
                    var internalError = ps.FirstOrDefault(p => p.Source == "U#" && (p.Message.StartsWith("UdonSharp's binder failed") || p.Message.StartsWith("System.") || p.Message.Contains("Exception")));
                    if (internalError.Message != null) { vInternal++; if (odd.Count < 12) odd.Add(what + ": " + internalError.Message); }
                }
                catch (Exception e) { vThrew++; if (odd.Count < 12) odd.Add(what + ": THREW " + e.GetType().Name + ": " + e.Message); }
                vTimes.Add(sw.Elapsed.TotalMilliseconds);
            }
            vTimes.Sort();
            Say($"VARIANTS {variants.Count}: threw {vThrew}, reached UdonSharp's steps {vReached}, UdonSharp internal errors {vInternal}; median {vTimes[vTimes.Count / 2]:F1} ms, max {vTimes.Max():F1} ms");
            foreach (var o in odd) Say("    " + o);

            // Those that reached UdonSharp's steps: the real compile must agree.
            int agree = 0, disagree = 0;
            foreach (var (what, text, ps) in clean.Concat(prefixClean))
            {
                File.WriteAllText(Door, text);
                var truth = Truth().Select(x => (file: x.file == "" ? Door : x.file, x.line, x.message)).OrderBy(x => x.file).ThenBy(x => x.line).ToList();
                var ours = ps.Where(p => p.Error).Select(p => (file: p.File ?? Door, line: p.Line, message: Normalize(p.Message))).Distinct().OrderBy(x => x.file).ThenBy(x => x.line).ToList();
                if (ours.SequenceEqual(truth)) agree++;
                else
                {
                    disagree++;
                    Say($"    DISAGREE {what}\n      truth: {string.Join(" | ", truth.Select(x => x.line + " " + x.message))}\n      ours : {string.Join(" | ", ours.Select(x => x.line + " " + x.message))}");
                }
            }
            File.WriteAllText(Door, originals[Door]);
            Say($"AGAINST THE REAL COMPILE: {agree} agree, {disagree} disagree (of {clean.Count + prefixClean.Count} states that reached UdonSharp's steps)");

            var final = UdonCheck.Check(Door, full, out _, out _);
            times.Sort();
            Say($"TYPING {full.Length} chars, {steps} checks: threw {threw}, UdonSharp internal errors shown {binderFailed}; median {times[times.Count / 2]:F1} ms, p99 {times[(int)(times.Count * 0.99)]:F1} ms, max {times.Max():F1} ms");
            Say("    final text problems: " + (final.Count == 0 ? "(none)" : string.Join(" | ", final.Select(p => p.Line + " " + p.Message))));
            foreach (var t in firstThrows) Say("    " + t);
            Say("");
        }

        static void Stress(Dictionary<string, string> originals)
        {
            var baseText = originals[Door];
            void One(string name, string text)
            {
                Say($"STRESS {name}: starting ({text.Length} chars)");
                var sw = Stopwatch.StartNew();
                var ps = UdonCheck.Check(Door, text, out _, out _);
                var first = sw.Elapsed.TotalMilliseconds;
                sw.Restart();
                UdonCheck.Check(Door, text + " ", out _, out _);
                var next = sw.Elapsed.TotalMilliseconds;
                UdonCheck.UseUdonSharpCheck = false;
                sw.Restart();
                UdonCheck.Check(Door, text + "  ", out _, out _);
                var onlyA = sw.Elapsed.TotalMilliseconds;
                UdonCheck.UseUdonSharpCheck = true;
                Say($"STRESS {name}: C# only {onlyA:F0} ms");
                Say($"STRESS {name}: first {first:F0} ms, next keystroke {next:F0} ms, problems: " + (ps.Count == 0 ? "(none)" : string.Join(" | ", ps.Take(3).Select(p => p.Line + " " + UdonSharpCheck.Readable(p.Message)))));
            }
            One("3000-term sum", baseText.Replace("        RequestSerialization();", "        RequestSerialization();\n        int sum = 0" + string.Concat(Enumerable.Repeat(" + 1", 3000)) + ";"));
            One("300 nested parens", baseText.Replace("        RequestSerialization();", "        RequestSerialization();\n        int deep = " + new string('(', 300) + "1" + new string(')', 300) + ";"));
            One("200 nested ifs", baseText.Replace("        RequestSerialization();", "        RequestSerialization();\n        " + string.Concat(Enumerable.Repeat("if (open) { ", 200)) + "open = false;" + new string('}', 200)));
            var methods = new StringBuilder();
            for (int i = 0; i < 2000; i++) methods.Append($"    public int M{i}(int x) {{ return x + {i}; }}\n");
            One("2000 methods", baseText.Replace("    public GameObject panel;", "    public GameObject panel;\n" + methods));
            One("100 KB string literal", baseText.Replace("        RequestSerialization();", "        RequestSerialization();\n        string big = \"" + new string('a', 100000) + "\";"));
            Say("");
        }
    }
}
