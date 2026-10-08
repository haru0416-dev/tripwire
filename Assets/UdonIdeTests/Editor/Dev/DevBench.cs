using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;

namespace UdonIde.Dev
{
    // Benchmark (batch mode): -executeMethod UdonIde.Dev.DevBench.Run [-benchOut name]
    // For 0 / 50 / 200 other U# files: the first check (nothing parsed yet), 20 keystrokes typed one by one (a check
    // each), and 10 completions after "transform.". Times go to Logs/bench-<name>.txt; the problems and completions
    // seen (the correctness gate: must not change with an optimization) to Logs/bench-<name>-output.txt.
    public static class DevBench
    {
        const string File_ = "Assets/UdonIdeTests/Fixtures/Door.cs";

        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-benchOut");
            var name = i >= 0 ? args[i + 1] : "run";
            var times = new StringBuilder();
            // Cold start, in parts (this run's first touches of each).
            var cold = Stopwatch.StartNew();
            UdonCheck.Warm(out var refsMs, out var exposureMs, out var sourcesMs);
            times.AppendLine($"cold: references {refsMs:F0} ms, Udon exposure table {exposureMs:F0} ms, source list {sourcesMs:F0} ms, total {cold.Elapsed.TotalMilliseconds:F0} ms");
            var output = new StringBuilder();
            var source = File.ReadAllText(File_);
            int at = source.IndexOf("RequestSerialization();") + "RequestSerialization();".Length;
            const string typed = "\n        Debug.Log(\"typed\");";
            foreach (var n in new[] { 0, 50, 200, 1000 })
            {
                UdonCheck.LimitFiles = n;
                UdonCheck.Forget();
                var sw = Stopwatch.StartNew();
                var first = UdonCheck.Check(File_, source, out _, out var files);
                times.AppendLine($"files {files}: first check {sw.Elapsed.TotalMilliseconds:F1} ms");
                var keys = new List<double>();
                for (int k = 1; k <= typed.Length; k++)
                {
                    var text = source.Insert(at, typed.Substring(0, k));
                    sw.Restart();
                    var problems = UdonCheck.Check(File_, text, out _, out _);
                    keys.Add(sw.Elapsed.TotalMilliseconds);
                    if (k % 5 == 0 || k == typed.Length) output.AppendLine($"files {files} key {k}: " + string.Join(" | ", problems.Select(p => $"{p.Line}:{p.Column} {p.Source} {p.Message}")));
                }
                keys.Sort();
                times.AppendLine($"files {files}: keystroke check median {keys[keys.Count / 2]:F1} ms, max {keys.Max():F1} ms ({keys.Count} keys)");
                var probe = source.Insert(at, "\n        var probe = transform.x;");
                int dot = probe.IndexOf("transform.") + "transform".Length;
                var comp = new List<double>();
                List<Completion> items = null;
                for (int r = 0; r < 10; r++)
                {
                    var text = probe.Insert(at, r % 2 == 0 ? "" : " "); // a small edit between completions, as typing would make
                    int d = r % 2 == 0 ? dot : dot + 1;
                    sw.Restart();
                    items = UdonCompletion.After(text, d, File_);
                    comp.Add(sw.Elapsed.TotalMilliseconds);
                }
                comp.Sort();
                times.AppendLine($"files {files}: completion median {comp[comp.Count / 2]:F1} ms, max {comp.Max():F1} ms");
                output.AppendLine($"files {files} completion: " + string.Join(", ", items.Select(c => c.Name + (c.Exposed ? "+" : "-"))));
            }
            UdonCheck.LimitFiles = -1;
            Directory.CreateDirectory("Logs");
            File.WriteAllText($"Logs/bench-{name}.txt", times.ToString());
            File.WriteAllText($"Logs/bench-{name}-output.txt", output.ToString());
            UnityEngine.Debug.Log("[bench]\n" + times);
            EditorApplication.Exit(0);
        }
    }
}
