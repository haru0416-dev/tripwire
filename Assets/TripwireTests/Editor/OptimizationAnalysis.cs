using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    /// <summary>
    /// Generated code vs. what an experienced U# author would write by hand, for the same behaviour: Udon program size
    /// (instructions, EXTERN calls, heap slots) and time per event on the Udon VM. Writes Logs/optimization-report.txt
    /// and disassemblies under Logs/disasm/. Run on demand: -testFilter Tripwire.Tests.OptimizationAnalysis
    /// </summary>
    [Explicit("analysis")]
    public class OptimizationAnalysis : PlayModeTest
    {
        const string SceneDir = "Assets/TripwireTests/Temp";
        const string ScenePath = SceneDir + "/Optimization.unity";
        const string HandDir = SceneDir + "/Hand";
        const int Many = 64, CallsPerRound = 200, Rounds = 9;

        // Hand-written equivalents of the scenario triggers (ScenarioTests) plus a 64-object toggle.
        static readonly Dictionary<string, string> Hand = new Dictionary<string, string>
        {
            ["HandDoor"] = @"using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class HandDoor : UdonSharpBehaviour
{
    public GameObject panel, lockedSign;
    [UdonSynced] bool unlocked;

    public void Unlock()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        unlocked = true;
        RequestSerialization();
    }

    public override void Interact()
    {
        if (unlocked) panel.SetActive(!panel.activeSelf);
        else lockedSign.SetActive(true);
    }
}
",
            ["HandScore"] = @"using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class HandScore : UdonSharpBehaviour
{
    public GameObject winner;
    public TMPro.TMP_Text label, line;
    [UdonSynced, FieldChangeCallback(nameof(Score))] int score;

    public int Score
    {
        get => score;
        set
        {
            score = value;
            winner.SetActive(value >= 3);
            label.text = value.ToString();
            line.text = ""スコア: "" + value.ToString() + ""点 {済}"";
        }
    }

    public void AddPoint()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Score = score + 1;
        RequestSerialization();
    }

    public void ResetScore()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Score = 0;
        RequestSerialization();
    }
}
",
            ["HandLight"] = @"using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class HandLight : UdonSharpBehaviour
{
    public Slider slider;
    public Light lamp;
    public GameObject bright;
    [UdonSynced, FieldChangeCallback(nameof(Level))] float level;

    public float Level
    {
        get => level;
        set { level = value; lamp.intensity = value; bright.SetActive(value > 0.5f); }
    }

    public void OnSlider()
    {
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        Level = slider.value;
        RequestSerialization();
    }
}
",
            ["HandApi"] = @"using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class HandApi : UdonSharpBehaviour
{
    public string text = ""42"";
    int n;
    bool ok;
    Vector3 pos;
    string label;

    public void Run()
    {
        ok = int.TryParse(text, out n);
        n = n * 2;
        pos.y = 1.5f;
        pos = pos + Vector3.up;
        label = ""n = "" + n;
    }
}
",
            ["HandMany"] = @"using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class HandMany : UdonSharpBehaviour
{
    public GameObject[] targets;

    public void Flip()
    {
        foreach (var t in targets) t.SetActive(!t.activeSelf);
    }
}
",
        };

        static T Find<T>(string name) where T : Component => Object.FindObjectsOfType<T>(true).First(c => c.name == name);

        static void WriteHand()
        {
            Directory.CreateDirectory(HandDir);
            foreach (var kv in Hand) File.WriteAllText(HandDir + "/" + kv.Key + ".cs", kv.Value);
            AssetDatabase.Refresh();
        }

        static Type TypeNamed(string name) => TypeCache.GetTypesDerivedFrom<UdonSharpBehaviour>().First(t => t.Name == name);

        static void BuildScene()
        {
            foreach (var name in Hand.Keys)
            {
                var assetPath = HandDir + "/" + name + ".asset";
                if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null) continue;
                var programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                programAsset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(HandDir + "/" + name + ".cs");
                programAsset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
                AssetDatabase.CreateAsset(programAsset, assetPath);
            }
            AssetDatabase.SaveAssets();
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            ScenarioTests.AddScenarios();

            // The generated 64-object toggle.
            var manyTargets = Enumerable.Range(0, Many).Select(i => new GameObject("GenMany_t" + i)).ToArray();
            var many = new GameObject("GenMany").AddComponent<TripwireTrigger>();
            var flip = new KEvent { eventId = "Custom", name = "Flip" };
            var toggle = new KAction { actionId = "GameObject.ToggleActive" };
            toggle.args.Add(new KArg { source = KArgSource.Objects, objects = manyTargets.Cast<Object>().ToList() });
            flip.actions.Add(toggle);
            many.events.Add(flip);

            // The wider API: an `out` parameter, Calculate, part of a position, text joined.
            var api = new GameObject("GenApi").AddComponent<TripwireTrigger>();
            foreach (var (name, type) in new[] { ("text", "System.String"), ("n", "System.Int32"), ("ok", "System.Boolean"), ("pos", "UnityEngine.Vector3"), ("label", "System.String") })
                api.variables.Add(new KVariable { name = name, typeName = type });
            api.variables[0].initial.stringValue = "42";
            KArg V(string name) => new KArg { source = KArgSource.Variable, name = name };
            var run = new KEvent { eventId = "Custom", name = "Run" };
            run.actions.Add(new KAction { actionId = "Udon.Call", method = "SystemInt32.__TryParse__SystemString_SystemInt32Ref__SystemBoolean", resultVariable = "ok", args = { V("text"), V("n") } });
            run.actions.Add(new KAction { actionId = ActionCatalog.CalculateId, args = { new KArg { stringValue = "n" }, V("n"), new KArg { intValue = (int)ActionCatalog.CalcOp.Multiply }, new KArg { intValue = 2 } } });
            run.actions.Add(new KAction { actionId = "Udon.Call", method = UdonApi.All.First(c => c.DeclaringType == "UnityEngine.Vector3" && c.Member == "y" && c.Kind == CallKind.Set).UdonName, args = { V("pos"), new KArg { floatValue = 1.5f } } });
            run.actions.Add(new KAction { actionId = ActionCatalog.CalculateId, args = { new KArg { stringValue = "pos" }, V("pos"), new KArg { intValue = (int)ActionCatalog.CalcOp.Add }, new KArg { vectorValue = Vector3.up } } });
            run.actions.Add(new KAction { actionId = ActionCatalog.CalculateId, args = { new KArg { stringValue = "label" }, new KArg { stringValue = "n = " }, new KArg { intValue = 0 }, V("n") } });
            api.events.Add(run);

            // Size only: four synced variables nothing sets (their setters are dead code) and one click.
            var unused = new GameObject("GenUnusedSynced").AddComponent<TripwireTrigger>();
            foreach (var (name, type) in new[] { ("b", "System.Boolean"), ("i", "System.Int32"), ("f", "System.Single"), ("s", "System.String") })
                unused.variables.Add(new KVariable { name = name, typeName = type, synced = true });
            var click = new KEvent { eventId = "Interact" };
            click.actions.Add(new KAction { actionId = "GameObject.SetActive", args = { new KArg { source = KArgSource.Objects, objects = { manyTargets[0] } }, new KArg { boolValue = true } } });
            unused.events.Add(click);

            // Hand-written counterparts with their own targets.
            object Add(string type, string go)
            {
                var proxy = UdonSharpUndo.AddComponent(new GameObject(go), TypeNamed(type));
                return proxy;
            }
            void Set(object proxy, string field, object value) => proxy.GetType().GetField(field).SetValue(proxy, value);
            var door = Add("HandDoor", "HandDoor");
            Set(door, "panel", new GameObject("HandPanel"));
            Set(door, "lockedSign", new GameObject("HandSign"));
            var score = Add("HandScore", "HandScore");
            Set(score, "winner", new GameObject("HandWinner"));
            // The same two texts the generated score board writes.
            var tmpType = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            Set(score, "label", new GameObject("HandScoreLabel").AddComponent(tmpType));
            Set(score, "line", new GameObject("HandScoreLine").AddComponent(tmpType));
            var canvas = Root("Canvas");
            var slider = new GameObject("HandDimmer", typeof(RectTransform), typeof(Slider));
            slider.transform.SetParent(canvas.transform, false);
            var light = Add("HandLight", "HandLight");
            Set(light, "slider", slider.GetComponent<Slider>());
            Set(light, "lamp", new GameObject("HandLamp").AddComponent<Light>());
            Set(light, "bright", new GameObject("HandBright"));
            var handMany = Add("HandMany", "HandMany");
            Set(handMany, "targets", Enumerable.Range(0, Many).Select(i => new GameObject("HandMany_t" + i)).ToArray());
            var handApi = Add("HandApi", "HandApi");
            foreach (var p in new[] { door, score, light, handMany, handApi }) UdonSharpEditorUtility.CopyProxyToUdon((UdonSharpBehaviour)p);

            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        // ---- static metrics ----
        static readonly string[] Ops = { "NOP", "PUSH", "POP", "?", "JUMP_IF_FALSE", "JUMP", "EXTERN", "ANNOTATION", "JUMP_INDIRECT", "COPY" };

        struct Size { public int Instructions, Externs, Heap, Bytes; }

        static Size Measure(UdonSharpProgramAsset asset, string name)
        {
            IUdonProgram program = asset.GetRealProgram();
            var code = program.ByteCode;
            uint U(int i) => (uint)(code[i] << 24 | code[i + 1] << 16 | code[i + 2] << 8 | code[i + 3]);
            var size = new Size { Bytes = code.Length, Heap = (int)program.Heap.GetHeapCapacity() };
            var sb = new StringBuilder();
            var entries = program.EntryPoints;
            for (int pc = 0; pc < code.Length;)
            {
                foreach (var e in entries.GetExportedSymbols())
                    if (entries.GetAddressFromSymbol(e) == (uint)pc) sb.AppendLine(e + ":");
                uint op = U(pc);
                size.Instructions++;
                bool hasArg = op == 1 || op == 4 || op == 5 || op == 6 || op == 8;
                if (op == 6) { size.Externs++; sb.AppendLine("    EXTERN " + program.Heap.GetHeapVariable(U(pc + 4))); }
                else sb.AppendLine("    " + (op < Ops.Length ? Ops[op] : "OP" + op));
                pc += hasArg ? 8 : 4;
            }
            Directory.CreateDirectory("Logs/disasm");
            File.WriteAllText("Logs/disasm/" + name + ".txt", sb.ToString());
            return size;
        }

        static UdonSharpProgramAsset ProgramOf(GameObject go)
        {
            var ub = go.GetComponent<UdonBehaviour>();
            return (UdonSharpProgramAsset)ub.programSource;
        }

        // ---- timing ----
        static double Round(UdonBehaviour ub, string ev, Action before = null)
        {
            var sw = new Stopwatch();
            for (int i = 0; i < CallsPerRound; i++)
            {
                before?.Invoke();
                sw.Start();
                ub.SendCustomEvent(ev);
                sw.Stop();
            }
            return sw.Elapsed.TotalMilliseconds * 1000.0 / CallsPerRound; // µs per call
        }

        static double Median(IEnumerable<double> xs) { var a = xs.OrderBy(x => x).ToArray(); return a[a.Length / 2]; }

        /// <summary>Time per event, generated vs hand-written (outside the test's iterator: no captured state across play mode).</summary>
        static List<string> Timings(Dictionary<string, UdonBehaviour> ubs, Slider genSlider, Slider handSlider)
        {
            int step = 0;
            Action genMove = () => genSlider.SetValueWithoutNotify((step++ % 10) / 10f);
            Action handMove = () => handSlider.SetValueWithoutNotify((step++ % 10) / 10f);
            var cases = new (string label, string gen, string genEvent, string hand, string handEvent, Action genBefore, Action handBefore)[]
            {
                ("Door Interact (unlocked)", "Door", "_interact", "HandDoor", "_interact", null, null),
                ("Score AddPoint (synced + change event)", "ScoreBoard", "AddPoint", "HandScore", "AddPoint", null, null),
                ("Slider -> synced float -> light", "LightPanel", CodeGeneratorUi(), "HandLight", "OnSlider", genMove, handMove),
                ("Toggle 64 objects", "GenMany", "Flip", "HandMany", "Flip", null, null),
                ("TryParse out, Calculate, pos.y, text", "GenApi", "Run", "HandApi", "Run", null, null),
            };
            var timing = new List<string> { "", "## Time per event on the Udon VM (us, median of " + Rounds + " rounds x " + CallsPerRound + " calls)", "case                                      gen      hand    ratio" };
            foreach (var c in cases)
            {
                Round(ubs[c.gen], c.genEvent, c.genBefore); // warm-up
                Round(ubs[c.hand], c.handEvent, c.handBefore);
                var g = new List<double>();
                var h = new List<double>();
                for (int r = 0; r < Rounds; r++)
                {
                    g.Add(Round(ubs[c.gen], c.genEvent, c.genBefore));
                    h.Add(Round(ubs[c.hand], c.handEvent, c.handBefore));
                }
                timing.Add($"{c.label,-40} {Median(g),7:F1}  {Median(h),7:F1}   {Median(g) / Median(h),5:F2}");
            }
            return timing;
        }

        static string CodeGeneratorUi() => Tripwire.Core.CodeGenerator.UiMethod(0);

        [UnityTest]
        public IEnumerator GeneratedVersusHandWritten()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying && !File.Exists(HandDir + "/HandDoor.cs")) WriteHand();
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying && Root("GenMany") == null)
            {
                BuildScene();
                TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            var report = new List<string>();
            if (!EditorApplication.isPlaying)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()));
                Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError());
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

                // The editor's list of Udon API members, built from scratch (the first Inspector or Apply pays this once).
                var allField = typeof(UdonApi).GetField("all", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                allField.SetValue(null, null);
                var build = Stopwatch.StartNew();
                int members = UdonApi.All.Count;
                build.Stop();
                report.Add($"## Udon API list: {members} members built in {build.Elapsed.TotalMilliseconds:F0} ms");
                report.Add("");
                report.Add("## Program size (generated vs hand-written)");
                report.Add("pair                 instr(gen/hand)   extern(gen/hand)   heap(gen/hand)");
                foreach (var (gen, hand) in new[] { ("Door", "HandDoor"), ("ScoreBoard", "HandScore"), ("LightPanel", "HandLight"), ("GenMany", "HandMany"), ("GenApi", "HandApi") })
                {
                    var g = Measure(ProgramOf(Root(gen)), "gen_" + gen);
                    var h = Measure(ProgramOf(Root(hand)), "hand_" + hand);
                    report.Add($"{gen,-20} {g.Instructions,6} / {h.Instructions,-6}   {g.Externs,6} / {h.Externs,-6}    {g.Heap,6} / {h.Heap,-6}");
                }
                var u = Measure(ProgramOf(Root("GenUnusedSynced")), "gen_GenUnusedSynced");
                report.Add($"{"GenUnusedSynced",-20} {u.Instructions,6}            {u.Externs,6}             {u.Heap,6}   (size only: 4 synced variables nothing sets)");
                File.WriteAllLines("Logs/optimization-report.txt", report);
            }

            NoDomainReloadOnPlay();
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;

            var names = new[] { "Door", "HandDoor", "ScoreBoard", "HandScore", "LightPanel", "HandLight", "GenMany", "HandMany", "GenApi", "HandApi" };
            var missing = names.Where(n => Root(n) == null || Root(n).GetComponent<UdonBehaviour>() == null).ToList();
            Assert.IsEmpty(missing, "objects with an UdonBehaviour in play mode; roots: " + string.Join(",", SceneManager.GetActiveScene().GetRootGameObjects().Select(g => g.name).Where(n => !n.Contains("_t"))));
            var ubs = names.ToDictionary(n => n, n => Root(n).GetComponent<UdonBehaviour>());
            for (int i = 0; i < 600 && !ubs.Values.All(ub => Flag(ub, "_isReady") && Flag(ub, "_hasDoneStart")); i++) yield return null;
            foreach (var kv in ubs) Assert.IsNotNull(kv.Value, kv.Key + " has an UdonBehaviour");
            ubs["Door"].SendCustomEvent("Unlock");
            ubs["HandDoor"].SendCustomEvent("Unlock");
            yield return null;

            var genSlider = Root("Canvas")?.GetComponentsInChildren<Slider>(true).FirstOrDefault(x => x != null && x.name == "Dimmer");
            var handSlider = Root("Canvas")?.GetComponentsInChildren<Slider>(true).FirstOrDefault(x => x != null && x.name == "HandDimmer");
            Assert.IsNotNull(genSlider, "Dimmer");
            Assert.IsNotNull(handSlider, "HandDimmer");
            // In Play the editor turns on each trigger's notes for the Event History (off in VRChat): timed both ways.
            var traced = Timings(ubs, genSlider, handSlider);
            traced[1] = traced[1].Replace("## Time per event", "## With the Event History's notes on (editor Play only): time per event");
            foreach (var ub in ubs.Values)
                if (ub.TryGetProgramVariable(Tripwire.Core.CodeGenerator.TraceFlag, out bool _)) ub.SetProgramVariable(Tripwire.Core.CodeGenerator.TraceFlag, false);
            var timing = Timings(ubs, genSlider, handSlider);
            timing[1] = timing[1].Replace("## Time per event", "## Notes off, as in VRChat: time per event");
            timing.AddRange(traced);
            foreach (var kv in ubs) Assert.IsFalse(Flag(kv.Value, "_hasError"), kv.Key + " halted");
            // Same end state: both score boards counted every call.
            Assert.AreEqual(ubs["ScoreBoard"].GetProgramVariable("v_score"), ubs["HandScore"].GetProgramVariable("score"), "both score boards counted the same");
            File.AppendAllLines("Logs/optimization-report.txt", timing);
            Debug.Log("[TripwireTest] " + string.Join("\n", timing));
            yield return new ExitPlayMode();
        }
    }
}
