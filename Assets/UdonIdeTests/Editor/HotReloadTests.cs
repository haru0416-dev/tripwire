using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UdonSharp;
using UdonSharpEditor;
using UdonSharp.Compiler;
using UnityEditor;
using UdonBridge;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using VRC.SDKBase;
using VRC.Udon;

namespace UdonIde.Tests
{
    /// <summary>
    /// In play mode (ClientSim): change a U# file on disk, hot reload, and check the running behaviour runs the new
    /// code with its field values kept — without a Unity C# compile or domain reload.
    /// </summary>
    public class HotReloadTests
    {
        const string Script = "Assets/UdonIdeTests/HotSample/Counter.cs";
        const string V1 = "using UdonSharp;\nusing UnityEngine;\n\n[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]\npublic class Counter : UdonSharpBehaviour\n{\n    [UdonSynced] public int count;\n    public string label = \"v1\";\n\n    public void Bump()\n    {\n        count += 1;\n        RequestSerialization();\n    }\n}\n";
        // v2: Bump adds 10, a new field counts bumps, label's initial value changes (the running value must stay "v1").
        const string V2 = "using UdonSharp;\nusing UnityEngine;\n\n[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]\npublic class Counter : UdonSharpBehaviour\n{\n    [UdonSynced] public int count;\n    public string label = \"v2\";\n    int bumps;\n\n    public void Bump()\n    {\n        count += 10;\n        bumps++;\n        RequestSerialization();\n    }\n}\n";
        // v3: does not compile; the running program must stay v2.
        static readonly string V3 = V2.Replace("count += 10;", "count += \"x\";");
        // v4: compiles but fails at run time (index out of range): Udon halts the behaviour.
        static readonly string V4 = V2.Replace("count += 10;", "int[] none = new int[0]; none[count] = 1;");

        static int sentinel;
        static bool Flag(UdonBehaviour ub, string f) => (bool)typeof(UdonBehaviour).GetField(f, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ub);
        static UdonBehaviour Ub() => Object.FindObjectsOfType<UdonBehaviour>(true).First(u => u.name == "Counter");
        static T Var<T>(string name) { Assert.IsTrue(Ub().TryGetProgramVariable(name, out T v), name); return v; }

        static double lastSaveSeconds;

        /// <summary>Writes the script and waits until the watcher has hot reloaded it (or 15 s pass).</summary>
        static IEnumerator Save(string text)
        {
            int before = HotReload.Attempts;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            File.WriteAllText(Script, text);
            while (HotReload.Attempts == before && sw.Elapsed.TotalSeconds < 15) yield return null;
            lastSaveSeconds = sw.Elapsed.TotalSeconds;
            Assert.AreNotEqual(before, HotReload.Attempts, "the save was picked up");
            File.AppendAllText("Logs/hot.txt", $"  save to swap: {lastSaveSeconds * 1000:F0} ms\n");
        }

        bool optionsWere;
        EnterPlayModeOptions optionsWas;

        [SetUp] public void RememberPlayModeOptions() { optionsWere = EditorSettings.enterPlayModeOptionsEnabled; optionsWas = EditorSettings.enterPlayModeOptions; }

        // The project's play mode settings are shared with Tripwire's tests: put them back.
        [TearDown] public void RestorePlayModeOptions() { EditorSettings.enterPlayModeOptionsEnabled = optionsWere; EditorSettings.enterPlayModeOptions = optionsWas; }

        [UnityTest]
        public IEnumerator SwapsTheRunningProgram()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying)
            {
                File.WriteAllText(Script, V1);
                AssetDatabase.Refresh();
                yield return new RecompileScripts(false);
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var program = AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>("Assets/UdonIdeTests/HotSample/Counter.asset");
                if (program == null)
                {
                    program = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
                    program.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(Script);
                    AssetDatabase.CreateAsset(program, "Assets/UdonIdeTests/HotSample/Counter.asset");
                }
                UdonSharpCompilerV1.CompileSync();
                Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError(), "v1 compiles");
                UdonSharpUndo.AddComponent(new GameObject("Counter"), program.sourceCsScript.GetClass());
                // A world: ClientSim starts only with a scene descriptor, and spawns the local player.
                var spawn = new GameObject("Spawn").transform;
                var descriptor = new GameObject("Descriptor").AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>();
                descriptor.spawns = new[] { spawn };
                new GameObject("Floor").AddComponent<BoxCollider>().size = new Vector3(20, 0.1f, 20);
                Directory.CreateDirectory("Assets/UdonIdeTests/HotSample");
                EditorSceneManager.SaveScene(scene, "Assets/UdonIdeTests/HotSample/Hot.unity");
                EditorSettings.enterPlayModeOptionsEnabled = true;
                EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            }
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            for (int i = 0; i < 600 && !(Flag(Ub(), "_isReady") && Flag(Ub(), "_hasDoneStart")); i++) yield return null;
            Assert.IsTrue(Flag(Ub(), "_hasDoneStart"), "Counter started");
            for (int i = 0; i < 600 && !Utilities.IsValid(Networking.LocalPlayer); i++) yield return null;
            Assert.IsTrue(Utilities.IsValid(Networking.LocalPlayer), "ClientSim spawned the local player");

            for (int i = 0; i < 3; i++) Ub().SendCustomEvent("Bump");
            Assert.AreEqual(3, Var<int>("count"), "v1 adds 1");

            sentinel = 42; // a domain reload would reset it
            yield return Save(V2); // saved as an editor would; the session's watcher picks it up
            var r = HotReload.LastResult;
            File.AppendAllText("Logs/hot.txt", $"v2: compiled={r.Compiled} swapped={r.Swapped} kept={r.Kept} dropped={r.Dropped} compile={r.CompileMs:F0}ms swap={r.SwapMs:F1}ms {string.Join("; ", r.Notes)}\n");
            Assert.IsTrue(r.Compiled, "v2 compiles");
            Assert.AreEqual(1, r.Swapped, "one behaviour swapped");
            yield return null;

            File.AppendAllText("Logs/hot.txt", $"before refresh: id={Ub().GetInstanceID()}\n");
            Assert.AreEqual(3, Var<int>("count"), "count kept across the swap");
            Assert.AreEqual("v1", Var<string>("label"), "running value kept, not the new initial value");
            Ub().SendCustomEvent("Bump");
            Assert.AreEqual(13, Var<int>("count"), "v2 adds 10");
            Assert.AreEqual(1, Var<int>("bumps"), "new field works");
            Assert.AreEqual(42, sentinel, "no domain reload");

            // Worst case: something refreshes the assets anyway (a person pressing Ctrl+R). The C# compile may run,
            // but the domain must not reload during play.
            var managedBefore = Ub();
            AssetDatabase.Refresh();
            yield return null;
            File.AppendAllText("Logs/hot.txt", $"same managed object after refresh: {ReferenceEquals(managedBefore, Ub())}\n");
            {
                var ub = Ub();
                var prog = (VRC.Udon.Common.Interfaces.IUdonProgram)typeof(UdonBehaviour).GetField("_program", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(ub);
                File.AppendAllText("Logs/hot.txt", $"after refresh: ubs={Object.FindObjectsOfType<UdonBehaviour>(true).Length} id={ub.GetInstanceID()} initialized={Flag(ub, "_initialized")} program={(prog == null ? "null" : string.Join(",", ((System.Collections.Generic.IEnumerable<string>)prog.SymbolTable.GetSymbols()).Where(x => !x.StartsWith("__"))))} enabled={ub.enabled}\n");
            }
            Assert.AreEqual(42, sentinel, "no domain reload after a refresh during play");
            Assert.AreEqual(13, Var<int>("count"), "state intact after a refresh during play");

            yield return Save(V3);
            var bad = HotReload.LastResult;
            File.AppendAllText("Logs/hot.txt", $"v3 (broken): compiled={bad.Compiled} swapped={bad.Swapped} compile={bad.CompileMs:F0}ms {string.Join("; ", bad.Notes)}\n");
            Assert.IsFalse(bad.Compiled, "v3 doesn't compile");
            Ub().SendCustomEvent("Bump");
            Assert.AreEqual(23, Var<int>("count"), "still v2 after a broken save");

            // A run-time error halts the behaviour; saving the fix brings it back.
            yield return Save(V4);
            LogAssert.ignoreFailingMessages = true;
            Ub().SendCustomEvent("Bump");
            yield return null;
            Assert.IsTrue(Flag(Ub(), "_hasError"), "v4 halted the behaviour (the case being tested)");
            yield return Save(V2);
            Assert.IsFalse(Flag(Ub(), "_hasError"), "the fix clears the halt");
            Assert.IsTrue(Ub().enabled, "and switches the behaviour back on");
            int beforeFix = Var<int>("count");
            Ub().SendCustomEvent("Bump");
            Assert.AreEqual(beforeFix + 10, Var<int>("count"), "the fixed code runs");
            File.AppendAllText("Logs/hot.txt", "halted by an error, then revived by the fix: " + string.Join("; ", HotReload.LastResult.Notes) + "\n");

            // Several saves in a row, as typing and saving would make.
            for (int k = 0; k < 5; k++)
            {
                yield return Save(V2.Replace("count += 10;", "count += " + (100 + k) + ";"));
                var again = HotReload.LastResult;
                File.AppendAllText("Logs/hot.txt", $"save {k}: swapped={again.Swapped} compile={again.CompileMs:F0}ms swap={again.SwapMs:F1}ms\n");
            }
            int before = Var<int>("count");
            Ub().SendCustomEvent("Bump");
            Assert.AreEqual(before + 104, Var<int>("count"), "last save runs");

            Assert.IsTrue(Networking.IsOwner(Ub().gameObject), "still the owner (networking intact)");
            File.AppendAllText("Logs/hot.txt", "clientsim: local player " + Networking.LocalPlayer.displayName + "\n");
            yield return new ExitPlayMode();
            File.WriteAllText(Script, V1);
            File.AppendAllText("Logs/hot.txt", "exited play mode\n");
        }
    }
}
