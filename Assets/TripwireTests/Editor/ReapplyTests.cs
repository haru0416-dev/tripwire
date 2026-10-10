using System.Collections;
using System.IO;
using System.Linq;
using Tripwire.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    /// <summary>
    /// Editing a trigger changes its program class. The UdonBehaviour must survive that (same instance), so references
    /// to it from elsewhere — here a UI Button's OnClick — keep working, and the new program must be what runs.
    /// </summary>
    public class ReapplyTests : PlayModeTest
    {
        const string SceneDir = "Assets/TripwireTests/Temp";
        const string ScenePath = SceneDir + "/Reapply.unity";
        const string StageKey = "TripwireTest.Reapply.Stage";
        const string IdKey = "TripwireTest.Reapply.Id";

        /// <summary>Another program (one more action): a value alone would only be filled into the same class's field.</summary>
        static void Grow(TripwireTrigger t) => t.events[0].actions.Add(new KAction { actionId = "Variable.Set", args = { new KArg { stringValue = "n" }, new KArg { intValue = 2 } } });

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var world = new GameObject("VRCWorld");
            world.AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>().spawns = new[] { world.transform };
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Counter";
            var t = cube.AddComponent<TripwireTrigger>();
            t.variables.Add(new KVariable { name = "n", type = KValueType.Int });
            var e = new KEvent { eventId = "Interact" };
            e.actions.Add(new KAction { actionId = "Variable.Set", args = { new KArg { stringValue = "n" }, new KArg { intValue = 1 } } });
            t.events.Add(e);
            new GameObject("UiButton").AddComponent<Button>();
            // Sleeper: its generated behaviour is switched off and must stay off across a program change.
            // Copy: gets Counter's trigger pasted over its own (its link then points at Counter's behaviour).
            foreach (var name in new[] { "Sleeper", "Copy" })
            {
                var o = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                o.name = name;
                var u = o.AddComponent<TripwireTrigger>();
                u.variables.Add(new KVariable { name = "n", type = KValueType.Int });
                var ue = new KEvent { eventId = "Interact" };
                ue.actions.Add(new KAction { actionId = "Variable.Set", args = { new KArg { stringValue = "n" }, new KArg { intValue = name == "Sleeper" ? 5 : 7 } } });
                u.events.Add(ue);
            }
            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [UnityTest]
        public IEnumerator EditingKeepsTheUdonBehaviourAndItsReferences()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying && Root("Counter") == null)
            {
                SessionState.SetInt(StageKey, 0);
                BuildScene();
                TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;

            if (!EditorApplication.isPlaying && SessionState.GetInt(StageKey, 0) == 0)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()));
                var t = Root("Counter").GetComponent<TripwireTrigger>();
                var ub = t.generated;
                Assert.IsNotNull(ub);
                var button = Root("UiButton").GetComponent<Button>();
                UnityEventTools.AddStringPersistentListener(button.onClick, ub.SendCustomEvent, "Ping");
                SessionState.SetInt(IdKey, ub.GetInstanceID());
                var firstClass = t.generatedClass;

                var sleeper = Root("Sleeper").GetComponent<TripwireTrigger>();
                UdonSharpEditor.UdonSharpEditorUtility.GetProxyBehaviour(sleeper.generated).enabled = false;
                sleeper.generated.enabled = false;
                Grow(sleeper);
                var copy = Root("Copy").GetComponent<TripwireTrigger>();
                SessionState.SetInt(IdKey + ".Copy", copy.generated.GetInstanceID());
                UnityEditorInternal.ComponentUtility.CopyComponent(t);
                UnityEditorInternal.ComponentUtility.PasteComponentValues(copy);
                Assert.AreSame(ub, copy.generated, "pasting carries the link to Counter's behaviour (the case being tested)");

                Grow(t); // different program → different class
                Grow(copy); // the pasted trigger too, so no trigger keeps the old class (the cleanup below removes it)
                Assert.AreNotEqual(firstClass, TripwireCompiler.Generate(t).ClassName);
                Assert.AreEqual(TripwireCompiler.State.NeedsScripts, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()));
                SessionState.SetInt(StageKey, 1);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;

            if (!EditorApplication.isPlaying && SessionState.GetInt(StageKey, 0) == 1)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()));
                var counter = Root("Counter");
                var t = counter.GetComponent<TripwireTrigger>();
                var ubs = counter.GetComponents<UdonBehaviour>();
                Assert.AreEqual(1, ubs.Length, "exactly one UdonBehaviour after re-apply");
                Assert.AreEqual(SessionState.GetInt(IdKey, 0), ubs[0].GetInstanceID(), "same UdonBehaviour instance kept");
                Assert.AreSame(ubs[0], t.generated);
                Assert.AreEqual(t.generatedClass, TripwireCompiler.Generate(t).ClassName);
                var button = Root("UiButton").GetComponent<Button>();
                Assert.AreSame(ubs[0], button.onClick.GetPersistentTarget(0), "UI reference still points at the behaviour");

                var sleeper = Root("Sleeper").GetComponent<TripwireTrigger>();
                Assert.IsFalse(sleeper.generated.enabled, "a switched-off behaviour stays off after its program changes");
                Assert.IsFalse(UdonSharpEditor.UdonSharpEditorUtility.GetProxyBehaviour(sleeper.generated).enabled);

                var copy = Root("Copy").GetComponent<TripwireTrigger>();
                var copyUbs = Root("Copy").GetComponents<UdonBehaviour>();
                Assert.AreEqual(1, copyUbs.Length, "pasted values: the object's own behaviour is reused, no second one: "
                    + string.Join(", ", copyUbs.Select(u => u.GetInstanceID() + ":" + (u.programSource != null ? AssetDatabase.GetAssetPath(u.programSource) : "null") + (u == copy.generated ? "(linked)" : ""))));
                Assert.AreEqual(SessionState.GetInt(IdKey + ".Copy", 0), copyUbs[0].GetInstanceID());
                Assert.AreSame(copyUbs[0], copy.generated);
                Assert.AreEqual(1, Root("Counter").GetComponents<UdonBehaviour>().Length, "Counter's behaviour left alone");

                // A value changed without an apply (a script, the Debug Inspector): the same class, so only the values
                // show it is not applied; applying fills it in without a script compile.
                t.events[0].actions[0].args[1].intValue = 9;
                Assert.AreEqual(TripwireCompiler.State.NeedsApply, TripwireCompiler.GetState(t, TripwireCompiler.Generate(t)), "a value not filled in yet");
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(new[] { t }));
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.GetState(t, TripwireCompiler.Generate(t)));

                // Applying again with nothing changed leaves the scene saved.
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()));
                Assert.IsFalse(SceneManager.GetActiveScene().isDirty, "an unchanged apply does not mark the scene changed");

                SessionState.SetInt(StageKey, 2);
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            var prevEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            var prevOptions = EditorSettings.enterPlayModeOptions;
            NoDomainReloadOnPlay();
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;

            var live = Root("Counter").GetComponent<UdonBehaviour>();
            for (int i = 0; i < 600 && !(Flag(live, "_isReady") && Flag(live, "_hasDoneStart")); i++) yield return null;
            live.Interact();
            yield return null;
            Assert.IsTrue(live.TryGetProgramVariable("v_n", out int n) && n == 2, "the new program runs (n=" + n + ")");
            Assert.IsFalse(Flag(live, "_hasError"));

            yield return new ExitPlayMode();
            EditorSettings.enterPlayModeOptionsEnabled = prevEnabled;
            EditorSettings.enterPlayModeOptions = prevOptions;
            SessionState.EraseInt(StageKey);
            SessionState.EraseInt(IdKey);
            SessionState.EraseInt(IdKey + ".Copy");

            // The class from before the edit is no longer used by any saved scene: cleanup removes it, keeps the current one.
            var current = Root("Counter").GetComponent<TripwireTrigger>().generatedClass;
            var before = Directory.GetFiles(TripwireCompiler.OutputDir, "Tripwire_*.cs").Length;
            int deleted = TripwireCompiler.DeleteUnusedScripts(new string[0]);
            AssetDatabase.Refresh();
            Assert.GreaterOrEqual(deleted, 1, "stale class deleted");
            Assert.IsTrue(File.Exists(TripwireCompiler.OutputDir + "/" + current + ".cs"), "class in use kept");
            Assert.AreEqual(before - deleted, Directory.GetFiles(TripwireCompiler.OutputDir, "Tripwire_*.cs").Length);
            yield return new RecompileScripts(false);
        }
    }
}
