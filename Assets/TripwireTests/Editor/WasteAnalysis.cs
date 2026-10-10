using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Tripwire.Tests
{
    /// <summary>
    /// What repeats in the generated code and what it costs: triggers vs the classes they become vs the classes they
    /// would become with their constants moved into fields (the "shape"); the program parts every class carries; the
    /// time an edit takes when it changes the code (a constant) and when it doesn't (a dragged-in object). Two scenes:
    /// the scenario world (every trigger different on purpose) and a repetitive one (10 copies each of four everyday
    /// buttons, differing in their target or in one constant). Writes Logs/waste-report.txt.
    /// Run on demand: -testFilter Tripwire.Tests.WasteAnalysis
    /// </summary>
    [Explicit("analysis")]
    public class WasteAnalysis
    {
        const string Report = "Logs/waste-report.txt";
        const string Copies = "Waste_";

        // Timings survive the domain reloads in between in SessionState.
        static void Mark(string key) => SessionState.SetString("Tripwire.Waste." + key, System.DateTime.UtcNow.Ticks.ToString());
        static double Since(string key) => (System.DateTime.UtcNow.Ticks - long.Parse(SessionState.GetString("Tripwire.Waste." + key, "0"))) / 10000.0;
        static void Line(string text) { File.AppendAllText(Report, text + "\n"); Debug.Log("[waste] " + text); }

        static TripwireTrigger Button(string name, KAction action)
        {
            var t = new GameObject(name).AddComponent<TripwireTrigger>();
            var e = new KEvent { eventId = "Interact" };
            e.actions.Add(action);
            t.events.Add(e);
            return t;
        }

        static KArg Objs(Object o) => new KArg { source = KArgSource.Objects, objects = { o } };

        /// <summary>Four everyday buttons, ten of each: a toggle (target differs), a label, a spot, an animator speed (one constant differs).</summary>
        static void AddRepetitive()
        {
            var tmp = TripwireModel.ResolveType("TMPro.TextMeshPro");
            for (int i = 0; i < 10; i++)
            {
                Button(Copies + "Toggle" + i, new KAction { actionId = "GameObject.ToggleActive", args = { Objs(new GameObject("Lamp" + i)) } });
                Button(Copies + "Label" + i, new KAction { actionId = "Text.SetText", args = { Objs(new GameObject("Sign" + i).AddComponent(tmp)), new KArg { stringValue = "Room " + (i + 1) } } });
                Button(Copies + "Spot" + i, new KAction { actionId = "Transform.SetPosition", args = { Objs(new GameObject("Seat" + i).transform), new KArg { vectorValue = new Vector3(i, 0, 2) } } });
                Button(Copies + "Speed" + i, new KAction { actionId = "Animator.SetFloat", args = { Objs(new GameObject("Fan" + i).AddComponent<Animator>()), new KArg { stringValue = "Speed" }, new KArg { floatValue = 0.5f + i * 0.1f } } });
            }
        }

        // Constants masked: string, number and bool literals. What's left is the code's shape, shared by triggers that
        // differ only in values (a lower bound for "constants in fields": some literals must stay in the code).
        static readonly Regex Literals = new Regex(@"""(?:[^""\\]|\\.)*""|(?<![\w.])-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?f?(?![\w.])|\btrue\b|\bfalse\b");
        static string Shape(string source) => Literals.Replace(source.Substring(source.IndexOf(": UdonSharpBehaviour", System.StringComparison.Ordinal)), "#");

        static void CountRepeats(string label, IList<TripwireTrigger> triggers)
        {
            var programs = triggers.Select(t => TripwireCompiler.Generate(t)).Where(g => !g.HasErrors).ToList();
            int classes = programs.Select(g => g.ClassName).Distinct().Count();
            int shapes = programs.Select(g => Shape(g.Source)).Distinct().Count();
            Line($"{label}: {triggers.Count} triggers, {programs.Count} without errors -> {classes} classes now, {shapes} shapes (classes if constants were fields)");
        }

        /// <summary>Applies the scene: UpToDate, or NeedsScripts when a script reload must come first (yield RecompileScripts then).</summary>
        static TripwireCompiler.State Apply() => TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());

        static double CompileUdonSharp()
        {
            var w = Stopwatch.StartNew();
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            return w.Elapsed.TotalMilliseconds;
        }

        static int GeneratedClasses() => Directory.Exists(TripwireCompiler.OutputDir) ? Directory.GetFiles(TripwireCompiler.OutputDir, CodeGenerator.ClassPrefix + "*.cs").Length : 0;

        [UnityTest]
        public IEnumerator WhatRepeatsAndWhatAnEditCosts()
        {
            LogAssert.ignoreFailingMessages = true;
            if (SessionState.GetInt("Tripwire.Waste.Step", 0) == 0)
            {
                File.WriteAllText(Report, "");
                ScenarioTests.BuildScene();
                CountRepeats("scenario world", TripwireCompiler.SceneTriggers());
                SessionState.SetInt("Tripwire.Waste.Step", 1);
                Mark("apply");
                Apply();
            }
            // RecompileScripts only works yielded from the test itself (not from a nested enumerator).
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            if (SessionState.GetInt("Tripwire.Waste.Step", 0) == 1)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, Apply());
                Line($"apply the scenario world from nothing: {Since("apply") / 1000:F1} s ({GeneratedClasses()} classes)");
                CompileUdonSharp(); // warm
                Line($"UdonSharp compile, {GeneratedClasses()} classes: {CompileUdonSharp() / 1000:F2} s");

                // The program parts every class carries: a trigger with one click that logs.
                var one = Button(Copies + "Minimal", new KAction { actionId = ActionCatalog.LogId, args = { new KArg { stringValue = "hi" } } });
                AddRepetitive();
                CountRepeats("repetitive buttons", TripwireCompiler.SceneTriggers().Where(t => t.name.StartsWith(Copies) && t != one).ToList());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                SessionState.SetInt("Tripwire.Waste.Step", 2);
                Mark("apply");
                Apply();
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            if (SessionState.GetInt("Tripwire.Waste.Step", 0) == 2)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, Apply());
                Line($"apply 41 more triggers: {Since("apply") / 1000:F1} s");
                Line($"UdonSharp compile, {GeneratedClasses()} classes: {CompileUdonSharp() / 1000:F2} s");

                // Program size: each distinct class, and the minimal one.
                var all = TripwireCompiler.SceneTriggers();
                var assets = all.Select(t => t.generated).Where(b => b != null).Select(b => b.programSource as UdonSharpProgramAsset)
                    .Where(a => a != null).Distinct().ToList();
                long bytes = 0, heap = 0, consts = 0;
                foreach (var a in assets)
                {
                    var p = a.GetRealProgram();
                    bytes += p.ByteCode.Length;
                    heap += p.Heap.GetHeapCapacity();
                    consts += p.SymbolTable.GetSymbols().Count(s => s.StartsWith("__const"));
                }
                Line($"programs: {assets.Count} distinct, {bytes / 1024.0:F0} KB bytecode, {heap} heap slots of which {consts} constants");
                var minimal = all.First(t => t.name == Copies + "Minimal").generated.programSource as UdonSharpProgramAsset;
                var mp = minimal.GetRealProgram();
                Line($"a one-click log trigger: {mp.ByteCode.Length} bytes bytecode, {mp.Heap.GetHeapCapacity()} heap slots ({string.Join(", ", mp.SymbolTable.GetSymbols().Where(s => !s.StartsWith("__")).OrderBy(s => s))})");
                foreach (var (label, kind) in new[] { ("label buttons", "Label"), ("spot buttons", "Spot"), ("toggle buttons", "Toggle") })
                {
                    var group = all.Where(t => t.name.StartsWith(Copies + kind)).Select(t => t.generated.programSource as UdonSharpProgramAsset).ToList();
                    Line($"{label}: 10 triggers, {group.Distinct().Count()} programs, {group.Distinct().Sum(a => (long)a.GetRealProgram().ByteCode.Length) / 1024.0:F1} KB bytecode in all");
                }

                // An edit that doesn't change the code: another target object.
                var toggle = all.First(t => t.name == Copies + "Toggle0");
                toggle.events[0].actions[0].args[0].objects[0] = new GameObject("Lamp0b");
                var w = Stopwatch.StartNew();
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(new[] { toggle }));
                Line($"edit a target object, apply: {w.Elapsed.TotalMilliseconds:F0} ms (no compile)");

                // One label's text: a script compile when the text is in the code, a field to fill when it isn't.
                var label0 = all.First(t => t.name == Copies + "Label0");
                label0.events[0].actions[0].args[1].stringValue = "Room 1 (changed)";
                w.Restart();
                var state = TripwireCompiler.ApplyAll(new[] { label0 });
                if (state == TripwireCompiler.State.UpToDate)
                {
                    Line($"edit a label's text, apply: {w.Elapsed.TotalMilliseconds:F0} ms (no compile)");
                    // The new text is in the behaviour (what VRChat runs), in the field the code reads it from.
                    var field = TripwireCompiler.Generate(label0).Bindings.Single(b => b.Kind == BindingKind.Constant && "Room 1 (changed)".Equals(b.Constant)).Field;
                    Assert.IsTrue(label0.generated.publicVariables.TryGetVariableValue(field, out string stored) && stored == "Room 1 (changed)", field + " holds " + stored);
                    Line($"  the behaviour's {field} now holds \"{stored}\"");
                    SessionState.EraseInt("Tripwire.Waste.Step");
                }
                else
                {
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                    SessionState.SetInt("Tripwire.Waste.Step", 3);
                    Mark("edit");
                }
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            if (SessionState.GetInt("Tripwire.Waste.Step", 0) == 3)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, Apply());
                Line($"edit a label's text, apply: {Since("edit") / 1000:F1} s (C# compile and reload, then UdonSharp)");
                SessionState.EraseInt("Tripwire.Waste.Step");
            }
        }
    }
}
