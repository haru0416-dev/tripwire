using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UdonBridge;

namespace UdonIde
{
    public struct Problem
    {
        public int Start, Length, Line, Column;
        public bool Error;
        public string Message, Source;
        /// <summary>Another file the problem is in (null: the edited file).</summary>
        public string File;
    }

    /// <summary>
    /// Checks unsaved text: Roslyn with UdonSharp's settings, then UdonSharp's own check. Other sources are parsed once
    /// and again only when their file changes.
    /// </summary>
    public static class UdonCheck
    {
        static readonly Dictionary<string, (DateTime time, SyntaxTree tree)> trees = new Dictionary<string, (DateTime, SyntaxTree)>();

        const double RescanSeconds = 1;

        // Dev: UdonSharp's check off (the exposure check instead), and only the first N other files (-1: all).
        internal static bool UseUdonSharpCheck = true;
        internal static int LimitFiles = -1;

        public static void Warm(out double referencesMs, out double exposureMs, out double sourcesMs)
        {
            var w = Stopwatch.StartNew();
            _ = UdonSharpSettings.References;
            referencesMs = w.Elapsed.TotalMilliseconds; w.Restart();
            UdonExposure.Warm();
            exposureMs = w.Elapsed.TotalMilliseconds; w.Restart();
            _ = UdonSharpSettings.Sources;
            sourcesMs = w.Elapsed.TotalMilliseconds;
        }

        /// <summary>Look at the other files on disk at the next check.</summary>
        public static void Rescan() { lock (gate) scannedAt = -1; }

        /// <summary>Dev: forget parsed files, as on a fresh editor.</summary>
        internal static void Forget() { lock (gate) { trees.Clear(); LastCompilation = null; LastTree = null; others = null; scannedAt = -1; } }

        static CSharpCompilation LastCompilation;
        static SyntaxTree LastTree;

        // While the other trees stay the same, a keystroke only swaps the edited tree (ReplaceSyntaxTree keeps the rest's work).
        static List<SyntaxTree> others;
        static string othersFor;
        static double scannedAt = -1;
        static readonly Stopwatch clock = Stopwatch.StartNew();

        static List<SyntaxTree> Others(string path, IReadOnlyDictionary<string, string[]> sources, out bool changed)
        {
            changed = false;
            if (others != null && othersFor == path && clock.Elapsed.TotalSeconds - scannedAt < RescanSeconds) return others;
            scannedAt = clock.Elapsed.TotalSeconds;
            var list = new List<SyntaxTree>();
            int taken = 0;
            foreach (var kv in sources.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                if (kv.Key == path) continue;
                if (LimitFiles >= 0 && taken++ >= LimitFiles) break;
                if (overlay.TryGetValue(kv.Key, out var unsaved))
                {
                    if (!overlayTrees.TryGetValue(kv.Key, out var o) || o.text != unsaved)
                        overlayTrees[kv.Key] = o = (unsaved, CSharpSyntaxTree.ParseText(unsaved, UdonSharpSettings.ParseOptions(kv.Value), kv.Key));
                    list.Add(o.tree);
                    continue;
                }
                var time = File.GetLastWriteTimeUtc(kv.Key);
                if (!trees.TryGetValue(kv.Key, out var cached) || cached.time != time)
                {
                    // Deleted while imports are held (play), or locked for a moment: left out.
                    string source;
                    try { source = File.ReadAllText(kv.Key); }
                    catch (IOException) { continue; }
                    catch (UnauthorizedAccessException) { continue; }
                    trees[kv.Key] = cached = (time, CSharpSyntaxTree.ParseText(source, UdonSharpSettings.ParseOptions(kv.Value), kv.Key));
                }
                list.Add(cached.tree);
            }
            changed = others == null || othersFor != path || list.Count != others.Count || list.Where((t, i) => t != others[i]).Any();
            if (changed) { others = list; othersFor = path; }
            return others;
        }

        // The worker and the main thread (hover, completion) share everything above.
        static readonly object gate = new object();

        static Dictionary<string, string> overlay = new Dictionary<string, string>();
        static readonly Dictionary<string, (string text, SyntaxTree tree)> overlayTrees = new Dictionary<string, (string, SyntaxTree)>();

        /// <summary>Unsaved text of open files (path → text), used instead of the files.</summary>
        public static void SetUnsaved(Dictionary<string, string> texts)
        {
            lock (gate)
            {
                bool same = texts.Count == overlay.Count && texts.All(kv => overlay.TryGetValue(kv.Key, out var t) && t == kv.Value);
                if (same) return;
                overlay = new Dictionary<string, string>(texts);
                foreach (var gone in overlayTrees.Keys.Where(k => !overlay.ContainsKey(k)).ToList()) overlayTrees.Remove(gone);
                scannedAt = -1;
            }
        }

        public static (CSharpCompilation compilation, SyntaxTree tree) Compile(string path, string text)
        {
            lock (gate) return CompileLocked(path, text);
        }

        /// <summary>False when the worker holds the files longer than <paramref name="waitMs"/>.</summary>
        public static bool TryCompile(string path, string text, int waitMs, out CSharpCompilation compilation, out SyntaxTree tree)
        {
            compilation = null; tree = null;
            if (!System.Threading.Monitor.TryEnter(gate, waitMs)) return false;
            try { (compilation, tree) = CompileLocked(path, text); return true; }
            finally { System.Threading.Monitor.Exit(gate); }
        }

        static (CSharpCompilation compilation, SyntaxTree tree) CompileLocked(string path, string text)
        {
            if (LastTree != null && LastTree.FilePath == path && LastCompilation != null && others != null && clock.Elapsed.TotalSeconds - scannedAt < RescanSeconds
                && LastTree.GetText().ToString() == text)
                return (LastCompilation, LastTree); // nothing changed (completion right after a check)
            var mine = CSharpSyntaxTree.ParseText(text, UdonSharpSettings.ParseOptionsFor(path), path);
            var rest = Others(path, UdonSharpSettings.Sources, out bool changed);
            var compilation = !changed && LastCompilation != null && LastTree != null && LastTree.FilePath == path
                ? LastCompilation.ReplaceSyntaxTree(LastTree, mine)
                : CSharpCompilation.Create("UdonIdeCheck", new[] { mine }.Concat(rest), UdonSharpSettings.References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            LastCompilation = compilation;
            LastTree = mine;
            return (compilation, mine);
        }

        /// <summary>Roslyn's errors and warnings, with the compilation for UdonSharp's check.</summary>
        public static List<Problem> CheckCSharp(string path, string text, out CSharpCompilation compilation, out SyntaxTree mine)
        {
            (compilation, mine) = Compile(path, text);
            var problems = new List<Problem>();
            foreach (var diag in compilation.GetSemanticModel(mine).GetDiagnostics().Concat(mine.GetDiagnostics()).Distinct())
            {
                if (diag.Severity < DiagnosticSeverity.Warning) continue;
                var span = diag.Location.SourceSpan;
                var pos = diag.Location.GetLineSpan().StartLinePosition;
                problems.Add(new Problem { Start = span.Start, Length = Math.Max(1, span.Length), Line = pos.Line, Column = pos.Character,
                    Error = diag.Severity == DiagnosticSeverity.Error, Message = diag.Id + ": " + diag.GetMessage(), Source = "C#" });
            }
            problems.AddRange(SymbolNav.EventNameProblems(compilation.GetSemanticModel(mine)));
            return problems;
        }

        /// <summary>For text without C# errors; any thread once UdonSharpCheck.Prepare has run (hasProgramAsset: main thread).</summary>
        public static List<Problem> CheckUdonSharp(CSharpCompilation compilation, SyntaxTree mine, string path, bool hasProgramAsset)
        {
            if (!UdonSharpCheck.Available || !UseUdonSharpCheck) return UdonExposure.Check(compilation.GetSemanticModel(mine), mine);
            return UdonSharpCheck.Check(compilation, mine, path, hasProgramAsset).Select(p => new Problem
                { Start = p.Start, Length = p.Length, Line = p.Line, Column = p.Column, Error = p.Error, Message = p.Message, File = p.File, Source = "U#" }).ToList();
        }

        /// <summary>Dev: both checks, on the calling thread.</summary>
        internal static List<Problem> Check(string path, string text, out double milliseconds, out int fileCount)
        {
            var watch = Stopwatch.StartNew();
            var problems = CheckCSharp(path, text, out var compilation, out var mine);
            fileCount = compilation.SyntaxTrees.Length;
            if (!problems.Any(x => x.Error)) problems.AddRange(CheckUdonSharp(compilation, mine, path, UdonSharpCheck.HasProgramAsset(path)));
            milliseconds = watch.Elapsed.TotalMilliseconds;
            return problems;
        }
    }
}
