using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;
using UdonBridge;

namespace UdonIde
{
    public partial class CodeEditorWindow
    {
        /// <summary>The other tabs' unsaved text goes into every compilation in place of their files.</summary>
        void PushUnsaved() => UdonCheck.SetUnsaved(docs.Where(d => d.dirty && d != Current && d.buffer != null).ToDictionary(d => d.path, d => d.buffer.Text));

        void ScheduleCheck()
        {
            pendingCheck?.Pause();
            pendingCheck = rootVisualElement.schedule.Execute(CheckNow).StartingIn(300);
        }

        // One background worker (UdonSharp's steps can take a second); the newest text wins.
        // Under `pending` (main thread writes, worker reads):
        readonly object pending = new object();
        string wantedText, wantedPath;
        bool wantedHasAsset;
        int wantedGeneration;
        System.Threading.Tasks.Task worker;
        bool workerRunning;
        // Main thread only:
        int shownGeneration;
        bool lastFinal;
        IVisualElementScheduledItem poll;
        // Final: both checks, or the C# one alone when it found errors.
        readonly System.Collections.Concurrent.ConcurrentQueue<(int Generation, List<Problem> Problems, bool Final, double Ms, int Files)> results =
            new System.Collections.Concurrent.ConcurrentQueue<(int, List<Problem>, bool, double, int)>();

        // For the dev harnesses.
        internal double LastCallMs, LastCheckMs;
        internal int LastProblemCount;
        internal bool CheckDone => shownGeneration == wantedGeneration && lastFinal;

        public void CheckNow()
        {
            if (path == null) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            PushUnsaved();
            lock (pending)
            {
                wantedText = view.Text;
                wantedPath = path;
                wantedHasAsset = UdonSharpCheck.HasProgramAsset(path); // a Unity API: asked here, on the main thread
                UdonSharpCheck.Prepare();
                wantedGeneration++;
                lastFinal = false;
                // Not Task.IsCompleted: a worker that has decided to stop isn't complete yet for a moment.
                if (!workerRunning) { workerRunning = true; worker = System.Threading.Tasks.Task.Run(Work); }
            }
            if (poll == null) poll = rootVisualElement.schedule.Execute(Show).Every(30); else poll.Resume();
            LastCallMs = watch.Elapsed.TotalMilliseconds;
        }

        void Work()
        {
            int done = 0;
            while (true)
            {
                string text, file; bool hasAsset; int generation;
                lock (pending)
                {
                    if (wantedGeneration == done) { workerRunning = false; return; }
                    text = wantedText; file = wantedPath; hasAsset = wantedHasAsset; generation = wantedGeneration;
                }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var found = UdonCheck.CheckCSharp(file, text, out var compilation, out var tree);
                    int files = compilation.SyntaxTrees.Length;
                    bool more = !found.Any(p => p.Error);
                    results.Enqueue((generation, found, !more, watch.Elapsed.TotalMilliseconds, files));
                    lock (pending) if (wantedGeneration != generation) { done = generation; continue; } // newer text: skip UdonSharp's steps
                    if (more)
                    {
                        var all = new List<Problem>(found);
                        all.AddRange(UdonCheck.CheckUdonSharp(compilation, tree, file, hasAsset));
                        results.Enqueue((generation, all, true, watch.Elapsed.TotalMilliseconds, files));
                    }
                }
                catch (Exception e)
                {
                    results.Enqueue((generation, new List<Problem> { new Problem { Error = true, Source = "IDE", Message = "チェックに失敗しました: " + e.Message } }, true, watch.Elapsed.TotalMilliseconds, 0));
                }
                done = generation;
            }
        }

        void Show()
        {
            while (results.TryDequeue(out var r))
            {
                if (r.Generation < shownGeneration || r.Generation != wantedGeneration) continue; // superseded
                shownGeneration = r.Generation;
                lastFinal = r.Final;
                LastCheckMs = r.Ms;
                LastProblemCount = r.Problems.Count;
                problems = r.Problems;
                view.Problems = r.Problems;
                view.MarkDirtyRepaint();
                ShowPanel();
                int errors = r.Problems.Count(p => p.Error), warnings = r.Problems.Count - errors;
                SetStatus($"{r.Files} ファイルを {r.Ms:F0} ms で確認   " + (errors + warnings == 0 ? (r.Final ? "問題なし" : "") : $"エラー {errors}  注意 {warnings}")
                          + (r.Final ? "" : "   UdonSharp で確認中…") + (Current?.readOnly == true ? "   読み取り専用" : ""));
            }
            if (CheckDone) poll?.Pause();
        }
    }
}
