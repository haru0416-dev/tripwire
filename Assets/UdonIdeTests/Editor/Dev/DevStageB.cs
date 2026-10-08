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
using UnityEngine;

namespace UdonIde.Dev
{
    // Answer key for stage B (batch mode): -executeMethod UdonIde.Dev.DevStageB.Run
    // Each case puts one construct into Door.cs. The truth is UdonSharp's real compile of that text saved to disk;
    // the check is UdonCheck on the same text, unsaved. Errors are compared as (line, message). Logs/stageb.txt.
    public static class DevStageB
    {
        const string Path_ = "Assets/UdonIdeTests/Fixtures/Door.cs";

        static readonly (string name, string where, string code)[] Cases =
        {
            ("ok", "body", "int n = 1; n++;"),
            ("try/catch", "body", "try { open = true; } catch { open = false; }"),
            ("lambda", "body", "System.Action a = () => { open = true; };"),
            ("List<int>", "body", "var list = new System.Collections.Generic.List<int>();"),
            ("File.Exists", "body", "System.IO.File.Exists(\"x\");"),
            ("local function", "body", "int Local() => 1; Local();"),
            ("goto", "body", "goto end; end: open = true;"),
            ("interpolation", "body", "var s = $\"{open}\"; Debug.Log(s);"),
            ("is pattern", "body", "object o = panel; if (o is GameObject g) Debug.Log(g);"),
            ("typeof", "body", "var t = typeof(Door); Debug.Log(t);"),
            ("nullable", "body", "int? n = null; Debug.Log(n);"),
            ("checked", "body", "int n = checked(1 + 2); Debug.Log(n);"),
            ("throw", "body", "throw new System.Exception(\"x\");"),
            ("yield-like foreach", "body", "foreach (var c in \"abc\") Debug.Log(c);"),
            ("dictionary", "body", "var d = new System.Collections.Generic.Dictionary<string, int>();"),
            ("linq", "body", "var a = new int[3]; Debug.Log(System.Linq.Enumerable.Count(a));"),
            ("generic method", "class", "T Get<T>() { return default; }"),
            ("static field", "class", "static int counter;"),
            ("constructor", "class", "public Door() { }"),
            ("property", "class", "public int Count { get; set; }"),
            ("nested class", "class", "class Inner { }"),
            ("struct", "class", "struct Pair { public int a; }"),
            ("out param", "class", "void Get(out int x) { x = 1; }"),
            ("static method", "class", "static int Twice(int x) { return x * 2; }"),
            ("event", "class", "public event System.Action Changed;"),
            ("operator", "class", "public static Door operator +(Door a, Door b) { return a; }"),
            ("enum", "class", "enum Mode { A, B }"),
            ("params", "class", "void Many(params int[] xs) { }"),
            ("default param", "class", "void Opt(int x = 1) { }"),
            ("transform", "body", "var p = transform.position; Debug.Log(p);"),
            ("GetComponent<T>", "body", "var r = GetComponent<Renderer>(); Debug.Log(r);"),
            ("AddComponent", "body", "gameObject.AddComponent<BoxCollider>();"),
            ("C# error", "body", "int x = \"text\";"),
        };

        static string Insert(string src, string where, string code) =>
            where == "body" ? src.Replace("        RequestSerialization();", "        RequestSerialization();\n        " + code)
                            : src.Replace("    public GameObject panel;", "    public GameObject panel;\n    " + code);

        static readonly Type cacheType = typeof(UdonSharpProgramAsset).Assembly.GetType("UdonSharp.UdonSharpEditorCache");

        /// <summary>The errors UdonSharp's real compile reports for the file, as (line, message).</summary>
        static List<(int line, string message)> Truth()
        {
            UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { DisableLogging = true });
            var cache = cacheType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
            var list = new List<(int, string)>();
            foreach (var d in (IEnumerable)cacheType.GetProperty("LastCompileDiagnostics", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cache))
            {
                var t = d.GetType();
                if (t.GetField("severity").GetValue(d).ToString() != "Error") continue;
                var file = ((string)t.GetField("file").GetValue(d) ?? "").Replace('\\', '/');
                if (!file.EndsWith(Path_) && file != "") continue;
                list.Add(((int)t.GetField("line").GetValue(d), Normalize((string)t.GetField("message").GetValue(d))));
            }
            return list.Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
        }

        // UdonSharp writes Roslyn errors as "error CS0029: ..."; the check writes "CS0029: ...".
        static string Normalize(string m) => Regex.Replace(UdonSharpCheck.Readable(m), "^error (CS\\d+)", "$1");

        public static void Run()
        {
            var report = new StringBuilder();
            var original = File.ReadAllText(Path_);
            try
            {
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/UdonIdeTests/Fixtures/Door.asset") == null)
                {
                    var asset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    asset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(Path_);
                    AssetDatabase.CreateAsset(asset, "Assets/UdonIdeTests/Fixtures/Door.asset");
                }
                UdonCheck.Check(Path_, original, out _, out _); // warm
                int match = 0, total = 0, crashCaught = 0;
                var times = new List<double>();
                foreach (var (name, where, code) in Cases)
                {
                    var text = Insert(original, where, code);
                    var sw = Stopwatch.StartNew();
                    var mine = UdonCheck.Check(Path_, text, out var ms, out _);
                    times.Add(sw.Elapsed.TotalMilliseconds);
                    var ours = mine.Where(p => p.Error).Select(p => (p.Line, Normalize(p.Message))).Distinct().OrderBy(x => x.Item1).ThenBy(x => x.Item2).ToList();
                    UdonCheck.UseUdonSharpCheck = false;
                    var before = UdonCheck.Check(Path_, text, out _, out _).Where(p => p.Error).Select(p => p.Line + " " + Normalize(p.Message)).ToList();
                    UdonCheck.UseUdonSharpCheck = true;

                    File.WriteAllText(Path_, text);
                    var truth = Truth();
                    File.WriteAllText(Path_, original);

                    bool same = ours.SequenceEqual(truth);
                    // UdonSharp crashing (no real message): caught when the check reports an error of its own.
                    bool caught = !same && truth.Count > 0 && truth.All(t => t.Item2.StartsWith("System.NullReferenceException")) && ours.Count > 0;
                    // Roslyn-only errors: UdonSharp reports syntax errors only (others stop its emit); count a C# case as matching when both say "error".
                    total++;
                    if (same) match++;
                    if (caught) crashCaught++;
                    report.AppendLine($"{(same ? "OK  " : caught ? "CRSH" : "DIFF")} {name}  ({sw.Elapsed.TotalMilliseconds:F0} ms)");
                    report.AppendLine("     truth: " + string.Join(" | ", truth.Select(x => x.Item1 + " " + x.Item2)));
                    if (!same) report.AppendLine("     ours : " + string.Join(" | ", ours.Select(x => x.Item1 + " " + x.Item2)));
                    report.AppendLine("     stageA: " + string.Join(" | ", before.Select(UdonSharpCheck.Readable)));
                }
                times.Sort();
                report.Insert(0, $"match {match}/{total} (+{crashCaught} UdonSharp crash caught); check median {times[times.Count / 2]:F0} ms, max {times.Max():F0} ms\n\n");
            }
            catch (Exception e) { report.AppendLine("EXCEPTION " + e); }
            finally
            {
                File.WriteAllText(Path_, original);
                UdonSharpCompilerV1.CompileSync(new UdonSharpCompileOptions { DisableLogging = true });
                File.WriteAllText("Logs/stageb.txt", report.ToString());
                EditorApplication.Exit(0);
            }
        }
    }
}
