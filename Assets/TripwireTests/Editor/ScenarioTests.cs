using System;
using System.Collections;
using System.IO;
using System.Linq;
using Tripwire.Editor;
using NUnit.Framework;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

using static Tripwire.Tests.TestSupport;
using Object = UnityEngine.Object;

namespace Tripwire.Tests
{
    /// <summary>
    /// World-like uses that combine features (triggers calling each other, synced variables with conditions, chains of
    /// delayed events, UI → variable → Udon API, player zones), then single features (ScenarioTests.Features.cs), all in
    /// one scene and one ClientSim play session with a single (local, owning) player. Failures are collected, so one
    /// broken feature doesn't hide the others.
    /// </summary>
    public partial class ScenarioTests : PlayModeTest
    {
        const string SceneDir = "Assets/TripwireTests/Temp";
        const string ScenePath = SceneDir + "/Scenarios.unity";

        static T Find<T>(string name) where T : Component => Object.FindObjectsOfType<T>(true).First(c => c.name == name);

        // ---- authoring helpers ----
        static KArg Objs(params Object[] o) { var a = new KArg { source = KArgSource.Objects }; a.objects.AddRange(o); return a; }
        static KArg Var(string name) => new KArg { source = KArgSource.Variable, name = name };
        static KArg Str(string s) => new KArg { stringValue = s };
        static KArg Int(int n) => new KArg { intValue = n };
        static KArg Flt(float f) => new KArg { floatValue = f };
        static KArg Bool(bool b) => new KArg { boolValue = b };
        static KAction Act(string id, params KArg[] args) { var a = new KAction { actionId = id }; a.args.AddRange(args); return a; }
        static KAction SetVar(string v, KArg value) => Act("Variable.Set", Str(v), value);
        static KCondition If(string v, KCompareOp op, KArg value) => new KCondition { variable = v, op = op, value = value };
        static KEvent On(string id, string name = "", params KAction[] actions) { var e = new KEvent { eventId = id, name = name }; e.actions.AddRange(actions); return e; }
        static KEvent When(KEvent e, params KCondition[] c) { e.conditions.AddRange(c); return e; }
        static TripwireTrigger Trigger(GameObject go, params (string name, string type, bool synced)[] vars)
        {
            var t = go.AddComponent<TripwireTrigger>();
            foreach (var (name, type, synced) in vars) t.variables.Add(new KVariable { name = name, typeName = type, synced = synced });
            return t;
        }
        static GameObject Obj(string name, bool active = true) { var g = new GameObject(name); g.SetActive(active); return g; }
        /// <summary>
        /// A small world-space canvas with a VRC Ui Shape, as worlds use them. (A screen-space one gets a screen-sized
        /// collider at the origin, which stops the player zone from seeing the player.)
        /// </summary>
        static GameObject WorldCanvas(string name, Vector3 position)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200, 200);
            rect.localScale = Vector3.one * 0.01f;
            go.transform.position = position;
            go.AddComponent<VRC.SDK3.Components.VRCUiShape>();
            return go;
        }

        static GameObject Box(string name, Vector3 pos) { var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = name; g.transform.position = pos; return g; }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            AddScenarios();
            AddInteractAndCalls();
            AddScriptCall();
            AddUiEvents();
            AddVideo();
            AddListen();
            AddBranches();
            AddTimers();
            AddLoops();
            AddMoreEvents();
            AddCyanGuides();
            AddMoreFlow();
            AddRemote();
            AddDebugTools();
            AddWiderApi();
            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        /// <summary>The scenario objects and triggers, into the open scene (also used by OptimizationAnalysis).</summary>
        internal static void AddScenarios()
        {
            var world = new GameObject("VRCWorld");
            world.AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>().spawns = new[] { world.transform };
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(10, 1, 10);

            // A. Key and door: picking up the key unlocks the door (another trigger); the door then opens, else a sign shows.
            var door = Box("Door", new Vector3(0, 1, 5));
            var panel = Obj("DoorPanel");
            var lockedSign = Obj("LockedSign", false);
            var d = Trigger(door, ("unlocked", "System.Boolean", true));
            d.events.Add(On("Custom", "Unlock", SetVar("unlocked", Bool(true))));
            d.events.Add(When(On("Interact", "", Act("GameObject.ToggleActive", Objs(panel))), If("unlocked", KCompareOp.Equal, Bool(true))));
            d.events.Add(When(On("Interact", "", Act("GameObject.SetActive", Objs(lockedSign), Bool(true))), If("unlocked", KCompareOp.Equal, Bool(false))));
            var key = Box("Key", new Vector3(3, 1, 5));
            key.AddComponent<Rigidbody>().isKinematic = true;
            key.AddComponent<VRC.SDK3.Components.VRCPickup>();
            var k = Trigger(key);
            k.events.Add(On("OnPickup", "", Act("Event.Send", Objs(door), Str("Unlock"), Int(0))));

            // B. Shared score: add / reset; reaching 3 shows the winner sign, going below hides it.
            var board = Obj("ScoreBoard");
            var winner = Obj("Winner", false);
            var s = Trigger(board, ("score", "System.Int32", true));
            s.events.Add(On("Custom", "AddPoint", Act("Variable.Add", Str("score"), Int(1))));
            s.events.Add(On("Custom", "ResetScore", SetVar("score", Int(0))));
            s.events.Add(When(On("OnVariableChanged", "score", Act("GameObject.SetActive", Objs(winner), Bool(true))), If("score", KCompareOp.GreaterOrEqual, Int(3))));
            s.events.Add(When(On("OnVariableChanged", "score", Act("GameObject.SetActive", Objs(winner), Bool(false))), If("score", KCompareOp.Less, Int(3))));
            // The score as text: the variable itself, and inside a sentence.
            var tmp = TripwireModel.ResolveType("TMPro.TextMeshPro");
            var scoreText = new GameObject("ScoreText", tmp);
            var scoreLine = new GameObject("ScoreLine", tmp);
            s.events.Add(On("OnVariableChanged", "score", Act("Text.SetText", Objs(scoreText.GetComponent(tmp)), Var("score")),
                Act("Text.SetText", Objs(scoreLine.GetComponent(tmp)), Str("スコア: {score}点 {{済}}"))));

            // C. Order: Interact → (0.3 s later) Two → Three, each step checking the previous one; event-level delays.
            var seq = Obj("Sequencer");
            var orderOk = Obj("OrderOk", false);
            var late = Obj("LateTarget");
            var q = Trigger(seq, ("step", "System.Int32", false), ("runs", "System.Int32", false));
            q.events.Add(When(On("Custom", "Begin", SetVar("step", Int(1)), Act("Event.SendDelayed", Objs(seq), Str("Two"), Flt(0.3f))), If("step", KCompareOp.Equal, Int(0))));
            q.events.Add(When(On("Custom", "Two", SetVar("step", Int(2)), Act("Event.Send", Objs(seq), Str("Three"), Int(0))), If("step", KCompareOp.Equal, Int(1))));
            q.events.Add(When(On("Custom", "Three", SetVar("step", Int(3)), Act("GameObject.SetActive", Objs(orderOk), Bool(true))), If("step", KCompareOp.Equal, Int(2))));
            var delayed = On("Custom", "Late", Act("GameObject.ToggleActive", Objs(late)), Act("Variable.Add", Str("runs"), Int(1)));
            delayed.delaySeconds = 0.3f;
            q.events.Add(delayed);

            // D. UI → variable → Udon API: a slider sets a float, which drives a light's intensity and a threshold sign.
            var canvas = WorldCanvas("Canvas", new Vector3(-6, 1, 2));
            var slider = new GameObject("Dimmer", typeof(RectTransform), typeof(Slider));
            slider.transform.SetParent(canvas.transform, false);
            var lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.intensity = 1f;
            var bright = Obj("Bright", false);
            var l = Trigger(Obj("LightPanel"), ("level", "System.Single", true));
            l.events.Add(On("UiSliderChanged", "", SetVar("level", new KArg { source = KArgSource.EventParam, name = "value" })));
            l.events[0].uiTarget = slider.GetComponent<Slider>();
            var setIntensity = new KAction { actionId = "Udon.Call", method = "UnityEngineLight.__set_intensity__SystemSingle__SystemVoid" };
            setIntensity.args.Add(Objs(lamp.gameObject));
            setIntensity.args.Add(Var("level"));
            l.events.Add(On("OnVariableChanged", "level", setIntensity));
            l.events.Add(When(On("OnVariableChanged", "level", Act("GameObject.SetActive", Objs(bright), Bool(true))), If("level", KCompareOp.Greater, Flt(0.5f))));
            l.events.Add(When(On("OnVariableChanged", "level", Act("GameObject.SetActive", Objs(bright), Bool(false))), If("level", KCompareOp.LessOrEqual, Flt(0.5f))));

            // E. Player zone: entering lights a lamp for the local player, leaving turns it off.
            var zone = Box("Zone", new Vector3(20, 1, 0));
            zone.transform.localScale = new Vector3(4, 4, 4);
            zone.GetComponent<BoxCollider>().isTrigger = true;
            var zoneLamp = Obj("ZoneLamp", false);
            var z = Trigger(zone);
            z.events.Add(On("OnPlayerTriggerEnter", "", Act("GameObject.SetActive", Objs(zoneLamp), Bool(true))));
            z.events.Add(On("OnPlayerTriggerExit", "", Act("GameObject.SetActive", Objs(zoneLamp), Bool(false))));
        }

        static string TextOf(string name)
        {
            var tmp = TripwireModel.ResolveType("TMPro.TextMeshPro");
            var c = Root(name).GetComponent(tmp);
            return (string)tmp.GetProperty("text").GetValue(c);
        }

        static UdonBehaviour Ub(string name) => Root(name).GetComponent<UdonBehaviour>();

        static T Get<T>(string trigger, string variable)
        {
            Assert.IsTrue(Ub(trigger).TryGetProgramVariable("v_" + variable, out T value), trigger + "." + variable);
            return value;
        }

        // Static, not captured: closures inside a test coroutine that spans play mode can come back null.
        static System.Collections.Generic.List<string> failures;
        static void Check(bool ok, string what) { if (!ok) failures.Add(what); }

        static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        /// <summary>Steps through nested coroutines (a check yielding Ready(...)), yielding only the frames.</summary>
        static IEnumerator Flatten(IEnumerator root)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                if (!stack.Peek().MoveNext()) { stack.Pop(); continue; }
                if (stack.Peek().Current is IEnumerator inner) stack.Push(inner);
                else yield return stack.Peek().Current;
            }
        }

        static IEnumerator Seconds(float s)
        {
            var end = Time.realtimeSinceStartup + s;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        [UnityTest]
        public IEnumerator WorldLikeScenarios()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying && Root("Door") == null)
            {
                BuildScene();
                TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying)
            {
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()), "every scenario trigger compiles");
                Assert.AreEqual(TripwireCompiler.State.UpToDate, TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers()), "applying again changes nothing");
                CheckUiWiring();
                Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError(), "generated U# compiles");
                EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            }

            NoDomainReloadOnPlay();
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;

            // ClientSim spawns the local player a few frames in; every behaviour must have run Start.
            for (int i = 0; i < 600 && !Utilities.IsValid(Networking.LocalPlayer); i++) yield return null;
            var all = Object.FindObjectsOfType<UdonBehaviour>();
            for (int i = 0; i < 600 && !all.All(ub => Flag(ub, "_isReady") && Flag(ub, "_hasDoneStart")); i++) yield return null;
            failures = new System.Collections.Generic.List<string>();

            // A. Key and door.
            Ub("Door").SendCustomEvent("_interact");
            yield return null;
            Check(Root("LockedSign").activeSelf, "A: locked door shows the sign");
            Check(Root("DoorPanel").activeSelf, "A: locked door does not open");
            Ub("Key").SendCustomEvent("_onPickup");
            yield return null;
            Check(Get<bool>("Door", "unlocked"), "A: picking up the key unlocked the door (another trigger's event)");
            Ub("Door").SendCustomEvent("_interact");
            yield return null;
            Check(!Root("DoorPanel").activeSelf, "A: unlocked door opens");

            // B. Score.
            for (int i = 1; i <= 3; i++)
            {
                Ub("ScoreBoard").SendCustomEvent("AddPoint");
                yield return null;
                Check(Get<int>("ScoreBoard", "score") == i, "B: score is " + i + " after " + i + " adds (got " + Get<int>("ScoreBoard", "score") + ")");
                Check(Root("Winner").activeSelf == (i >= 3), "B: winner sign at score " + i);
            }
            Check(TextOf("ScoreText") == "3", "B: score shown as text (" + TextOf("ScoreText") + ")");
            Check(TextOf("ScoreLine") == "スコア: 3点 {済}", "B: score inside a sentence (" + TextOf("ScoreLine") + ")");
            Ub("ScoreBoard").SendCustomEvent("ResetScore");
            yield return null;
            Check(Get<int>("ScoreBoard", "score") == 0 && !Root("Winner").activeSelf, "B: reset clears the score and the sign");

            // C. Order and delays.
            Ub("Sequencer").SendCustomEvent("Begin");
            Ub("Sequencer").SendCustomEvent("Begin"); // a second press during the chain is ignored (step is no longer 0)
            yield return null;
            Check(Get<int>("Sequencer", "step") == 1 && !Root("OrderOk").activeSelf, "C: chain waits for the delay");
            yield return Seconds(0.6f);
            Check(Get<int>("Sequencer", "step") == 3 && Root("OrderOk").activeSelf, "C: delayed step and the immediate step after it ran in order (step " + Get<int>("Sequencer", "step") + ")");
            Ub("Sequencer").SendCustomEvent("Late");
            Ub("Sequencer").SendCustomEvent("Late");
            yield return null;
            Check(Root("LateTarget").activeSelf && Get<int>("Sequencer", "runs") == 0, "C: delayed event has not run yet");
            yield return Seconds(0.6f);
            Check(Get<int>("Sequencer", "runs") == 2 && Root("LateTarget").activeSelf, "C: both delayed runs happened (runs " + Get<int>("Sequencer", "runs") + ")");

            // D. Slider → variable → light.
            var slider = Find<Slider>("Dimmer");
            var lamp = Root("Lamp").GetComponent<Light>();
            slider.value = 0.8f;
            yield return null;
            Check(Mathf.Approximately(lamp.intensity, 0.8f) && Root("Bright").activeSelf, "D: slider 0.8 → intensity " + lamp.intensity + ", bright sign on");
            slider.value = 0.2f;
            yield return null;
            Check(Mathf.Approximately(lamp.intensity, 0.2f) && !Root("Bright").activeSelf, "D: slider 0.2 → intensity " + lamp.intensity + ", bright sign off");


            // E. Player zone.
            var local = Networking.LocalPlayer;
            Check(local != null, "E: ClientSim has a local player");
            if (local != null)
            {
                local.TeleportTo(Root("Zone").transform.position, Quaternion.identity);
                // Wait in time, not frames: trigger events come from physics steps (every 0.02 s), and ten fast frames
                // can pass without one.
                yield return Seconds(0.5f);
                Check(Root("ZoneLamp").activeSelf, "E: entering the zone lit the lamp (colliders near the player: "
                    + string.Join(",", Physics.OverlapSphere(local.GetPosition(), 1.5f).Select(c => c.name)) + ")");
                local.TeleportTo(new Vector3(0, 0, -5), Quaternion.identity);
                yield return Seconds(0.5f);
                Check(!Root("ZoneLamp").activeSelf, "E: leaving the zone turned it off");
            }

            // F–J. Single features.
            foreach (var part in new Func<Action<bool, string>, IEnumerator>[] { CheckInteractAndCalls, CheckScriptCall, CheckUiEvents, CheckVideo, CheckListen, CheckBranches, CheckTimers, CheckLoops, CheckMoreEvents, CheckCyanGuides, CheckMoreFlow, CheckRemote, CheckDebugTools, CheckWiderApi })
            {
                var steps = Flatten(part(Check));
                while (steps.MoveNext()) yield return steps.Current;
            }

            foreach (var ub in all) if (ub != null) Check(!Flag(ub, "_hasError"), ub.name + ": program did not halt");
            CollectionAssert.IsEmpty(failures);
            yield return new ExitPlayMode();
        }
    }
}
