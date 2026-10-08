using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    /// <summary>
    /// An apply that can't finish says so, with the reason, instead of "applied" or an endless "waiting":
    /// a project that doesn't compile, and a field that can't be filled.
    /// </summary>
    public class ApplyTrustTests
    {
        const string Dir = "Assets/TripwireTests/Temp";
        const string Broken = Dir + "/TwBrokenScript.cs";
        const string StageKey = "TripwireTest.Trust.Stage";

        /// <summary>A value no earlier run used: its program shape has no script yet (the generated folder outlives runs).</summary>
        static int FreshValue(string key)
        {
            int v = SessionState.GetInt(key, 0);
            if (v == 0) SessionState.SetInt(key, v = 100000 + (System.Environment.TickCount & 0x3FFFFFFF) % 800000);
            return v;
        }

        static TripwireTrigger MakeTrigger(int value)
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var t = new GameObject("Trusted").AddComponent<TripwireTrigger>();
            t.variables.Add(new KVariable { name = "n", typeName = "System.Int32" });
            var e = new KEvent { eventId = "Interact" };
            // A value no other test uses: a program shape whose script doesn't exist yet.
            e.actions.Add(new KAction { actionId = ActionCatalog.SetVariableId, args = { new KArg { stringValue = "n" }, new KArg { intValue = value } } });
            t.events.Add(e);
            return t;
        }

        [UnityTest]
        public IEnumerator AProjectThatDoesNotCompileBlocksWithTheReason()
        {
            LogAssert.ignoreFailingMessages = true;
            if (SessionState.GetInt(StageKey, 0) == 0)
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(Broken, "class TwBrokenScript { int x = ; }\n");
                AssetDatabase.ImportAsset(Broken);
                SessionState.SetInt(StageKey, 1);
            }
            yield return new RecompileScripts(true, false);
            LogAssert.ignoreFailingMessages = true;

            if (SessionState.GetInt(StageKey, 0) == 1)
            {
                var t = MakeTrigger(FreshValue("TripwireTest.Trust.Value1"));
                var state = TripwireCompiler.ApplyAll(new[] { t });
                Assert.AreEqual(TripwireCompiler.State.Blocked, state, "not an endless wait");
                Assert.AreEqual(TripwireCompiler.State.Blocked, TripwireCompiler.GetState(t, TripwireCompiler.Generate(t)), "the Inspector shows it");
                StringAssert.Contains("Console", TripwireCompiler.BlockedReason, "the reason says where to look");

                // Fixed: the apply continues by itself after the reload.
                AssetDatabase.DeleteAsset(Broken);
                SessionState.SetInt(StageKey, 2);
            }
            yield return new RecompileScripts(true, true);
            LogAssert.ignoreFailingMessages = true;
            for (int i = 0; i < 30 && TripwireCompiler.ResumePending; i++) yield return null;

            SessionState.EraseInt(StageKey);
            SessionState.EraseInt("TripwireTest.Trust.Value1");
            var after = Root("Trusted").GetComponent<TripwireTrigger>();
            Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.GetState(after, TripwireCompiler.Generate(after)), "applied once the project compiles");
        }

        const string BadUdon = Dir + "/TwBadUdon.cs";
        const string UdonStageKey = "TripwireTest.Trust.UdonStage";

        [UnityTest]
        public IEnumerator AnUdonSharpErrorInAnotherScriptNamesThatScript()
        {
            LogAssert.ignoreFailingMessages = true;
            if (SessionState.GetInt(UdonStageKey, 0) == 0)
            {
                // Valid C#, but not UdonSharp (File isn't exposed to Udon): U# fails, Unity's compile doesn't.
                Directory.CreateDirectory(Dir);
                File.WriteAllText(BadUdon, "using UdonSharp;\npublic class TwBadUdon : UdonSharpBehaviour\n{\n    void Start() { System.IO.File.ReadAllText(\"x\"); }\n}\n");
                AssetDatabase.ImportAsset(BadUdon);
                SessionState.SetInt(UdonStageKey, 1);
            }
            yield return new RecompileScripts(true, true);
            LogAssert.ignoreFailingMessages = true;

            if (SessionState.GetInt(UdonStageKey, 0) == 1)
            {
                UdonBridge.UdonSharpPrograms.EnsureProgramAsset(BadUdon);
                SessionState.SetInt(UdonStageKey, 2);
                // A new shape: written now, attached after the reload (where U# compiles and meets TwBadUdon).
                TripwireCompiler.ApplyAll(new[] { MakeTrigger(FreshValue("TripwireTest.Trust.Value2")) });
                yield return new RecompileScripts(true, true);
            }
            LogAssert.ignoreFailingMessages = true;
            for (int i = 0; i < 30 && TripwireCompiler.ResumePending; i++) yield return null;

            var t = Root("Trusted").GetComponent<TripwireTrigger>();
            var state = TripwireCompiler.ApplyAll(new[] { t });
            var reason = TripwireCompiler.BlockedReason;
            SessionState.EraseInt(UdonStageKey);
            SessionState.EraseInt("TripwireTest.Trust.Value2");
            AssetDatabase.DeleteAsset(BadUdon);
            AssetDatabase.DeleteAsset(Path.ChangeExtension(BadUdon, ".asset"));
            Assert.AreEqual(TripwireCompiler.State.Blocked, state, "blocked, not 'Tripwire failed'");
            StringAssert.Contains("TwBadUdon.cs", reason, "the reason names the script to fix");
        }

        [UnityTest]
        public IEnumerator AFieldThatCannotBeFilledIsNotApplied()
        {
            LogAssert.ignoreFailingMessages = true;
            if (Root("Trusted") == null && TripwireCompiler.ApplyAll(new[] { MakeTrigger(1) }) == TripwireCompiler.State.NeedsScripts)
                yield return new RecompileScripts(true, true);
            LogAssert.ignoreFailingMessages = true;
            for (int i = 0; i < 30 && TripwireCompiler.ResumePending; i++) yield return null;
            var t = Root("Trusted").GetComponent<TripwireTrigger>();
            Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(new[] { t }), "the plain shape is applied");

            // A binding to a type the project no longer has (a removed package).
            var g = TripwireCompiler.Generate(t);
            g.Bindings.Add(new FieldBinding { Field = "tw_Missing", UnityType = "No.Such.Type", Kind = BindingKind.VariableInitial, Variable = 0 });
            var bind = typeof(TripwireCompiler).GetMethod("Bind", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.IsFalse((bool)bind.Invoke(null, new object[] { t, g }), "reported as failed");
            StringAssert.Contains("No.Such.Type", TripwireCompiler.FailureOf(t));
            Assert.AreEqual(TripwireCompiler.State.ApplyFailed, TripwireCompiler.GetState(t, TripwireCompiler.Generate(t)), "not shown as applied");

            Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(new[] { t }), "a clean apply clears it");
            Assert.IsNull(TripwireCompiler.FailureOf(t));
        }
    }
}
