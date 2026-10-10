using System;
using System.Collections;
using System.IO;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Video.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using Object = UnityEngine.Object;

using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    // Single features in the same world and play session as the scenarios: each adds its objects to the scene and
    // checks them in play mode, reporting through check(ok, what) so one failure doesn't hide the others.
    public partial class ScenarioTests
    {
        const string VideoUrl = "https://example.com/tripwire-test.mp4";
        const string CounterScript = "Assets/TripwireTests/Runtime/TripwireTestCounter.cs";
        const string NotifierScript = "Assets/TripwireTests/Runtime/TripwireTestNotifier.cs";

        static Type CounterType => TripwireModel.ResolveType("TripwireTestCounter");
        static Type NotifierType => TripwireModel.ResolveType("TripwireTestNotifier");

        /// <summary>An UdonSharp program asset for a test "third-party" script (created once, compiled now).</summary>
        static void EnsureProgramAsset(string script)
        {
            var assetPath = Path.ChangeExtension(script, ".asset");
            if (AssetDatabase.LoadAssetAtPath<UdonSharpProgramAsset>(assetPath) != null) return;
            var programAsset = ScriptableObject.CreateInstance<UdonSharpProgramAsset>();
            programAsset.sourceCsScript = AssetDatabase.LoadAssetAtPath<MonoScript>(script);
            programAsset.ScriptVersion = UdonSharpProgramVersion.CurrentVersion;
            AssetDatabase.CreateAsset(programAsset, assetPath);
            AssetDatabase.SaveAssets();
        }

        static IEnumerator Ready(params UdonBehaviour[] ubs)
        {
            for (int i = 0; i < 600 && !ubs.All(ub => Flag(ub, "_isReady") && Flag(ub, "_hasDoneStart")); i++) yield return null;
        }

        // ---------------- F. Interact, synced variable, Udon API calls, object variables ----------------

        static void AddInteractAndCalls()
        {
            var target = Obj("F_Target", false);
            var button = Box("F_Button", new Vector3(0, 1, 2));
            var t = Trigger(button, ("isOn", "System.Boolean", true));
            t.events.Add(new KEvent { eventId = "Interact", interactText = "Toggle", actions = { Act("Variable.Toggle", Str("isOn")) } });
            t.events.Add(On("OnVariableChanged", "isOn", Act("GameObject.SetActive", Objs(target), Var("isOn"))));

            var scaled = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            scaled.name = "F_Scaled";
            var ct = Trigger(Box("F_Caller", new Vector3(2, 1, 2)), ("best", "System.Int32", false),
                ("parsed", "System.Int32", true), ("parsedOk", "System.Boolean", false), ("h", "System.Single", false), ("s", "System.Single", false), ("v", "System.Single", false));
            var setScale = new KAction { actionId = "Udon.Call", method = "UnityEngineTransform.__set_localScale__UnityEngineVector3__SystemVoid" };
            setScale.args.Add(Objs(scaled)); // a GameObject, coerced to its Transform
            setScale.args.Add(new KArg { vectorValue = new Vector3(2, 3, 4) });
            var max = new KAction { actionId = "Udon.Call", method = "UnityEngineMathf.__Max__SystemInt32_SystemInt32__SystemInt32", resultVariable = "best" };
            max.args.Add(Var("best"));
            max.args.Add(Int(7));
            // `out` parameters: into a synced variable (through its setter), and three from a void method.
            var parse = new KAction { actionId = "Udon.Call", method = "SystemInt32.__TryParse__SystemString_SystemInt32Ref__SystemBoolean", resultVariable = "parsedOk" };
            parse.args.Add(Str("42"));
            parse.args.Add(Var("parsed"));
            var hsv = new KAction { actionId = "Udon.Call", method = "UnityEngineColor.__RGBToHSV__UnityEngineColor_SystemSingleRef_SystemSingleRef_SystemSingleRef__SystemVoid" };
            hsv.args.Add(new KArg { vector4Value = new Vector4(1, 0, 0, 1) });
            hsv.args.Add(Var("h"));
            hsv.args.Add(Var("s"));
            hsv.args.Add(Var("v"));
            ct.events.Add(On("Interact", "", setScale, max, parse, hsv));

            // Find → object variable → next action's target; an array variable (assigned in the Inspector) as a target list.
            Obj("F_Lamp");
            var pair = new[] { Obj("F_PairA"), Obj("F_PairB") };
            var ch = Trigger(Box("F_Chain", new Vector3(-2, 1, 2)), ("found", "UnityEngine.GameObject", false));
            var pairVar = new KVariable { name = "pair", typeName = "UnityEngine.GameObject[]" };
            pairVar.initial.source = KArgSource.Objects;
            pairVar.initial.objects.AddRange(pair);
            ch.variables.Add(pairVar);
            var find = new KAction { actionId = "Udon.Call", method = "UnityEngineGameObject.__Find__SystemString__UnityEngineGameObject", resultVariable = "found" };
            find.args.Add(Str("F_Lamp"));
            ch.events.Add(On("Interact", "", find, Act("GameObject.SetActive", Var("found"), Bool(false)), Act("GameObject.ToggleActive", Var("pair"))));
        }

        static IEnumerator CheckInteractAndCalls(Action<bool, string> check)
        {
            var button = Root("F_Button");
            // Kept in play mode (stripped only from builds), pointing at the behaviour that runs: its Inspector shows live values.
            var authoring = button.GetComponent<TripwireTrigger>();
            check(authoring != null && authoring.generated == button.GetComponent<UdonBehaviour>(), "F: authoring component kept in play mode, pointing at the running behaviour");
            var ub = button.GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            check(!Root("F_Target").activeSelf, "F: target starts hidden");
            ub.Interact();
            yield return null;
            check(Root("F_Target").activeSelf, "F: interact → synced variable → change event shows the target");
            check(ub.TryGetProgramVariable("v_isOn", out bool on) && on, "F: synced variable set");
            check(Networking.IsOwner(Networking.LocalPlayer, button), "F: interacting player took ownership");
            ub.Interact();
            yield return null;
            check(!Root("F_Target").activeSelf, "F: second interact hides it again");

            var caller = Root("F_Caller").GetComponent<UdonBehaviour>();
            yield return Ready(caller);
            caller.Interact();
            yield return null;
            check(Root("F_Scaled").transform.localScale == new Vector3(2, 3, 4), "F: Udon API call set Transform.localScale");
            check(caller.TryGetProgramVariable("v_best", out int best) && best == 7, "F: Mathf.Max stored into a variable (" + best + ")");
            caller.TryGetProgramVariable("v_parsed", out int parsed);
            caller.TryGetProgramVariable("v_parsedOk", out bool parsedOk);
            check(parsed == 42 && parsedOk, "F: int.TryParse's out parameter went into a synced variable (" + parsed + ", " + parsedOk + ")");
            caller.TryGetProgramVariable("v_s", out float sat);
            caller.TryGetProgramVariable("v_v", out float val);
            check(Mathf.Approximately(sat, 1f) && Mathf.Approximately(val, 1f), "F: Color.RGBToHSV's out parameters went into variables (s " + sat + ", v " + val + ")");

            var chain = Root("F_Chain").GetComponent<UdonBehaviour>();
            yield return Ready(chain);
            chain.Interact();
            yield return null;
            check(chain.TryGetProgramVariable("v_found", out GameObject found) && found == Root("F_Lamp"), "F: Find result stored in an object variable");
            check(!Root("F_Lamp").activeSelf, "F: object variable used as the next action's target");
            check(!Root("F_PairA").activeSelf && !Root("F_PairB").activeSelf, "F: array variable toggled as a target list");
        }

        // ---------------- G. Another U# script ----------------

        static string CounterMember(CallKind kind, string member) =>
            UdonSharpApi.Members(CounterType).First(c => c.Kind == kind && c.Member == member).UdonName;

        static void AddScriptCall()
        {
            EnsureProgramAsset(CounterScript);
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            var lamp = Obj("G_Lamp");
            var counter = new GameObject("G_Counter");
            var proxy = UdonSharpUndo.AddComponent(counter, CounterType);
            CounterType.GetField("lamp").SetValue(proxy, lamp);
            UdonSharpEditorUtility.CopyProxyToUdon(proxy);

            KAction Use(string id, params KArg[] rest)
            {
                var a = new KAction { actionId = ActionCatalog.ScriptCallId, method = id };
                a.args.Add(Objs(counter));
                a.args.AddRange(rest);
                return a;
            }
            var t = Trigger(Box("G_Caller", new Vector3(4, 1, 2)), ("d", "System.Int32", false));
            var doubled = Use(CounterMember(CallKind.Method, "Doubled"));
            doubled.resultVariable = "d";
            t.events.Add(On("Interact", "", Use(CounterMember(CallKind.Method, "Add"), Int(5)), doubled,
                Use(CounterMember(CallKind.Set, "label"), Str("hello")), Use(CounterMember(CallKind.Method, "HideLamp"))));
        }

        static IEnumerator CheckScriptCall(Action<bool, string> check)
        {
            var caller = Root("G_Caller").GetComponent<UdonBehaviour>();
            var counter = Root("G_Counter").GetComponent<UdonBehaviour>();
            yield return Ready(caller, counter);
            caller.Interact();
            yield return null;
            check(counter.TryGetProgramVariable("count", out int count) && count == 5, "G: Add(5) ran on the other script (" + count + ")");
            check(caller.TryGetProgramVariable("v_d", out int d) && d == 10, "G: Doubled() result stored (" + d + ")");
            check(counter.TryGetProgramVariable("label", out string label) && label == "hello", "G: field set on the other script");
            check(!Root("G_Lamp").activeSelf, "G: HideLamp() ran");
            check(!Flag(counter, "_hasError"), "G: the other script did not halt");
        }

        // ---------------- H. UI events ----------------

        static void AddUiEvents()
        {
            var canvas = WorldCanvas("H_Canvas", new Vector3(-6, 1, 6));
            var button = new GameObject("H_PressMe", typeof(RectTransform), typeof(Image), typeof(Button));
            button.transform.SetParent(canvas.transform, false);
            var toggle = new GameObject("H_SwitchMe", typeof(RectTransform), typeof(Toggle));
            toggle.transform.SetParent(canvas.transform, false);
            toggle.GetComponent<Toggle>().isOn = true;
            var target = Obj("H_Target");
            var target2 = Obj("H_Target2");
            var netTarget = Obj("H_NetTarget");

            var t = Trigger(Obj("H_Panel"));
            t.events.Add(new KEvent { eventId = "UiButtonClick", uiTarget = button.GetComponent<Button>(), actions = { Act("GameObject.ToggleActive", Objs(target)) } });
            t.events.Add(new KEvent { eventId = "UiToggleChanged", uiTarget = toggle.GetComponent<Toggle>(),
                actions = { Act("GameObject.SetActive", Objs(target2), new KArg { source = KArgSource.EventParam, name = "value" }) } });
            // Control: an event run for everyone is network-callable, so a network call to it must take effect.
            t.events.Add(new KEvent { eventId = "Custom", name = "Ping", broadcast = KBroadcast.All, actions = { Act("GameObject.ToggleActive", Objs(netTarget)) } });
        }

        static int WiredCalls(UnityEngine.Events.UnityEventBase evt, Object target) =>
            Enumerable.Range(0, evt.GetPersistentEventCount()).Count(i => evt.GetPersistentTarget(i) == target && evt.GetPersistentMethodName(i) == "SendCustomEvent");

        /// <summary>In edit mode, after applying twice: each UI element is wired to the behaviour exactly once.</summary>
        static void CheckUiWiring()
        {
            var ub = Root("H_Panel").GetComponent<TripwireTrigger>().generated;
            NUnit.Framework.Assert.AreEqual(1, WiredCalls(Find<Button>("H_PressMe").onClick, ub), "H: button wired once");
            NUnit.Framework.Assert.AreEqual(1, WiredCalls(Find<Toggle>("H_SwitchMe").onValueChanged, ub), "H: toggle wired once");
        }

        static IEnumerator CheckUiEvents(Action<bool, string> check)
        {
            var live = Root("H_Panel").GetComponent<UdonBehaviour>();
            yield return Ready(live);
            Find<Button>("H_PressMe").onClick.Invoke();
            yield return null;
            check(!Root("H_Target").activeSelf, "H: button press ran the actions");
            Find<Toggle>("H_SwitchMe").isOn = false;
            yield return null;
            check(!Root("H_Target2").activeSelf, "H: toggle value (off) flowed into SetActive");
            Find<Toggle>("H_SwitchMe").isOn = true;
            yield return null;
            check(Root("H_Target2").activeSelf, "H: toggle value (on) flowed into SetActive");

            // Control first: ClientSim delivers network events (Tw_E2 is the network body of "Ping").
            live.SendCustomNetworkEvent(NetworkEventTarget.All, "Tw_E2");
            yield return Frames(10);
            check(!Root("H_NetTarget").activeSelf, "H: control: a network call to a network-callable body runs");
            // A network call to the UI handler is ignored (only the UI may run it).
            live.SendCustomNetworkEvent(NetworkEventTarget.All, CodeGenerator.UiMethod(0));
            yield return Frames(10);
            check(!Root("H_Target").activeSelf, "H: a network call to the UI handler did nothing");
        }

        // ---------------- I. Video player ----------------

        static void AddVideo()
        {
            var target = Obj("I_Target");
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "I_Screen";
            screen.transform.position = new Vector3(6, 1, 2);
            screen.AddComponent<VRCUnityVideoPlayer>();
            var t = Trigger(screen);
            t.events.Add(On("OnVideoEnd", "", Act("GameObject.ToggleActive", Objs(target))));
            t.events.Add(On("Interact", "", Act("Video.SetLoop", new KArg { source = KArgSource.Self }, Bool(true)),
                Act("Video.PlayUrl", new KArg { source = KArgSource.Self }, Str(VideoUrl))));
        }

        static IEnumerator CheckVideo(Action<bool, string> check)
        {
            var screen = Root("I_Screen");
            var ub = screen.GetComponent<UdonBehaviour>();
            var player = screen.GetComponent<VRCUnityVideoPlayer>();
            yield return Ready(ub);
            check(ub.TryGetProgramVariable("tw_Url1_1_1", out VRCUrl url) && url != null && url.Get() == VideoUrl, "I: URL typed in the editor is bound");
            // The SDK delivers video events this way: the player runs them on the UdonBehaviours of its GameObject.
            player.OnVideoEnd();
            yield return null;
            check(!Root("I_Target").activeSelf, "I: OnVideoEnd reached the trigger");
            ub.Interact();
            yield return null;
            check(player.Loop, "I: loop set on the player");
        }

        // ---------------- J. Notifications from another script ----------------

        static void AddListen()
        {
            EnsureProgramAsset(NotifierScript);
            UdonSharp.Compiler.UdonSharpCompilerV1.CompileSync();
            var notifier = new GameObject("J_Notifier");
            UdonSharpEditorUtility.CopyProxyToUdon(UdonSharpUndo.AddComponent(notifier, NotifierType));

            KEvent Listen(string register, string callback, GameObject target, params KArg[] args)
            {
                var e = new KEvent { eventId = "ScriptNotified", name = callback, listenTarget = notifier,
                    listenMethod = UdonSharpApi.Registrations(NotifierType).First(r => r.Method == register).Id };
                e.listenArgs.AddRange(args);
                e.actions.Add(Act("GameObject.ToggleActive", Objs(target)));
                return e;
            }
            var t = Trigger(Obj("J_Listener"));
            t.events.Add(Listen("_RegisterListener", "_TvPlay", Obj("J_ByFixedName")));
            t.events.Add(Listen("_RegisterListener", "_TvPlay", Obj("J_Twice"))); // same registration: must not register twice
            t.events.Add(Listen("_RegisterNamed", "_Chosen", Obj("J_ByChosenName"), Int(7), new KArg(), Str("_Chosen")));
        }

        static IEnumerator CheckListen(Action<bool, string> check)
        {
            var listener = Root("J_Listener").GetComponent<UdonBehaviour>();
            var notifier = Root("J_Notifier").GetComponent<UdonBehaviour>();
            yield return Ready(listener, notifier);
            notifier.SendCustomEvent("_Notify");
            yield return null;
            check(!Root("J_ByFixedName").activeSelf, "J: fixed-name callback (_TvPlay) ran");
            check(!Root("J_Twice").activeSelf, "J: registered once (a second registration would toggle it back)");
            check(!Root("J_ByChosenName").activeSelf, "J: chosen-name callback with registration arguments ran");
            check(!Flag(notifier, "_hasError"), "J: the notifying script did not halt");
        }

        // ---------------- K. Branches: if / else, any-one conditions, nesting, "not" ----------------

        static KAction IfBlock(bool any, KCondition[] conditions, KAction[] then, KAction[] otherwise = null)
        {
            var a = new KAction { actionId = ActionCatalog.IfId, matchAny = any };
            a.conditions.AddRange(conditions);
            a.thenActions.AddRange(then);
            if (otherwise != null) a.elseActions.AddRange(otherwise);
            return a;
        }

        static KCondition Cond(string v, KCompareOp op, KArg value, bool not = false) => new KCondition { variable = v, op = op, value = value, negate = not };

        static void AddBranches()
        {
            var panel = Obj("K_Panel");
            var sign = Obj("K_Sign", false);
            var anyLamp = Obj("K_Any", false);
            var nested = Obj("K_Nested", false);
            var notLamp = Obj("K_Not", false);
            var t = Trigger(Box("K_Door", new Vector3(8, 1, 2)), ("hasKey", "System.Boolean", false), ("count", "System.Int32", false));
            // With the key the door opens; without it a sign shows and the attempt is counted.
            t.events.Add(On("Interact", "", IfBlock(false, new[] { Cond("hasKey", KCompareOp.Equal, Bool(true)) },
                new[] { Act("GameObject.ToggleActive", Objs(panel)) },
                new[] { Act("GameObject.SetActive", Objs(sign), Bool(true)), Act("Variable.Add", Str("count"), Int(1)) })));
            t.events.Add(On("Custom", "GiveKey", SetVar("hasKey", Bool(true))));
            // Event conditions, any one: two failed attempts OR holding the key.
            var check = On("Custom", "Check", Act("GameObject.SetActive", Objs(anyLamp), Bool(true)));
            check.conditionsMatchAny = true;
            check.conditions.Add(Cond("count", KCompareOp.GreaterOrEqual, Int(2)));
            check.conditions.Add(Cond("hasKey", KCompareOp.Equal, Bool(true)));
            t.events.Add(check);
            // Nested blocks, and a negated condition.
            t.events.Add(On("Custom", "Nest",
                IfBlock(false, new[] { Cond("hasKey", KCompareOp.Equal, Bool(true)) },
                    new[] { IfBlock(false, new[] { Cond("count", KCompareOp.Equal, Int(1)) }, new[] { Act("GameObject.SetActive", Objs(nested), Bool(true)) }) }),
                IfBlock(false, new[] { Cond("count", KCompareOp.GreaterOrEqual, Int(5), not: true) }, new[] { Act("GameObject.SetActive", Objs(notLamp), Bool(true)) })));
        }

        static IEnumerator CheckBranches(Action<bool, string> check)
        {
            var door = Root("K_Door").GetComponent<UdonBehaviour>();
            yield return Ready(door);
            door.SendCustomEvent("_interact");
            yield return null;
            check(Root("K_Panel").activeSelf && Root("K_Sign").activeSelf, "K: without the key the else side ran (sign shown, door stays)");
            check(door.TryGetProgramVariable("v_count", out int count) && count == 1, "K: the else side counted the attempt (" + count + ")");
            door.SendCustomEvent("Check");
            yield return null;
            check(!Root("K_Any").activeSelf, "K: any-one conditions: none held yet");
            door.SendCustomEvent("GiveKey");
            door.SendCustomEvent("_interact");
            yield return null;
            check(!Root("K_Panel").activeSelf, "K: with the key the then side ran (door opened)");
            door.SendCustomEvent("Check");
            yield return null;
            check(Root("K_Any").activeSelf, "K: any-one conditions: holding the key was enough");
            door.SendCustomEvent("Nest");
            yield return null;
            check(Root("K_Nested").activeSelf, "K: nested block ran (key held, count is 1)");
            check(Root("K_Not").activeSelf, "K: negated condition (count is not at least 5)");
        }

        // ---------------- L. Timers and random numbers ----------------

        static KEvent TimerEvent(string name, float min, float max, bool repeat, bool auto, params KAction[] actions)
        {
            var e = On("Timer", name, actions);
            e.timerMin = min; e.timerMax = max; e.timerRepeat = repeat; e.timerAutoStart = auto;
            return e;
        }

        static void AddTimers()
        {
            var once = Obj("L_Once", false);
            var late = Obj("L_Late", false);
            var t = Trigger(Box("L_Clock", new Vector3(10, 1, 2)), ("ticks", "System.Int32", false), ("waits", "System.Int32", false), ("dice", "System.Int32", false));
            // Repeating every 0.2 s from the start; "Halt" stops it.
            t.events.Add(TimerEvent("tick", 0.2f, 0.2f, true, true, Act("Variable.Add", Str("ticks"), Int(1))));
            t.events.Add(On("Custom", "Halt", Act("Timer.Stop", Str("tick"))));
            // Once, 0.3 s after start.
            t.events.Add(TimerEvent("once", 0.3f, 0.3f, false, true, Act("GameObject.SetActive", Objs(once), Bool(true))));
            // Not started until "Go"; a random wait between 0.1 and 0.3 s, repeating, counts how often it ran.
            t.events.Add(TimerEvent("later", 0.1f, 0.3f, true, false, Act("Variable.Add", Str("waits"), Int(1)), Act("GameObject.SetActive", Objs(late), Bool(true))));
            t.events.Add(On("Custom", "Go", Act("Timer.Start", Str("later")), Act("Timer.Start", Str("later")))); // twice: still one schedule
            // Restarting a one-shot timer waits a full interval again.
            var fade = Obj("L_Fade", false);
            t.events.Add(TimerEvent("fade", 0.5f, 0.5f, false, false, Act("GameObject.SetActive", Objs(fade), Bool(true))));
            t.events.Add(On("Custom", "Poke", Act("Timer.Start", Str("fade"))));
            // A repeating timer on an object that gets hidden for a while.
            var blink = Trigger(Box("L_Blinker", new Vector3(10, 1, 4)), ("n", "System.Int32", false));
            blink.events.Add(TimerEvent("b", 0.1f, 0.1f, true, true, Act("Variable.Add", Str("n"), Int(1))));
            // Dice: 1 to 6.
            t.events.Add(On("Custom", "Roll", Act("Variable.Random", Str("dice"), Int(1), Int(6))));
        }

        static IEnumerator CheckTimers(Action<bool, string> check)
        {
            var clock = Root("L_Clock").GetComponent<UdonBehaviour>();
            yield return Ready(clock);
            clock.TryGetProgramVariable("v_ticks", out int before);
            yield return Seconds(1.1f);
            clock.TryGetProgramVariable("v_ticks", out int ticks);
            ticks -= before;
            check(ticks >= 3 && ticks <= 7, "L: a 0.2 s timer ran about 5 times in 1.1 s (" + ticks + ")");
            check(Root("L_Once").activeSelf, "L: a one-shot timer ran");
            check(!Root("L_Late").activeSelf, "L: a timer that does not start right away waited");
            clock.SendCustomEvent("Halt");
            clock.TryGetProgramVariable("v_ticks", out int stopped);
            yield return Seconds(0.6f);
            clock.TryGetProgramVariable("v_ticks", out int after);
            check(after == stopped, "L: Stop Timer stopped it (" + stopped + " → " + after + ")");
            clock.SendCustomEvent("Go");
            yield return Seconds(0.65f);
            clock.TryGetProgramVariable("v_waits", out int waits);
            check(Root("L_Late").activeSelf && waits >= 2 && waits <= 7, "L: Start Timer started it, random intervals of 0.1–0.3 s, started twice yet scheduled once (" + waits + ")");
            clock.SendCustomEvent("Poke");
            yield return Seconds(0.3f);
            clock.SendCustomEvent("Poke");
            yield return Seconds(0.35f);
            check(!Root("L_Fade").activeSelf, "L: restarting a timer waits a full interval again (not yet at 0.65 s)");
            yield return Seconds(0.4f);
            check(Root("L_Fade").activeSelf, "L: the restarted timer ran (by 1.05 s)");

            var blinker = Root("L_Blinker").GetComponent<UdonBehaviour>();
            blinker.TryGetProgramVariable("v_n", out int n0);
            Root("L_Blinker").SetActive(false);
            yield return Seconds(0.6f);
            blinker.TryGetProgramVariable("v_n", out int n1);
            Root("L_Blinker").SetActive(true);
            yield return Seconds(0.6f);
            blinker.TryGetProgramVariable("v_n", out int n2);
            UnityEngine.Debug.Log("TW-HIDDEN-TIMER while hidden +" + (n1 - n0) + ", after showing again +" + (n2 - n1));
            check(n2 > n1, "L: the timer runs after the object is shown again (" + n0 + " → " + n1 + " → " + n2 + ")");

            var seen = new System.Collections.Generic.HashSet<int>();
            for (int n = 0; n < 200; n++)
            {
                clock.SendCustomEvent("Roll");
                clock.TryGetProgramVariable("v_dice", out int d);
                seen.Add(d);
            }
            check(seen.Count == 6 && seen.Min() == 1 && seen.Max() == 6, "L: dice from 1 to 6, both ends included (" + string.Join(",", seen.OrderBy(x => x)) + ")");
        }

        // ---------------- M. Loops: repeat, for each, leave the loop, stop here ----------------

        static KAction LoopBlock(string id, KAction[] body, params KArg[] args)
        {
            var a = new KAction { actionId = id };
            a.args.AddRange(args);
            a.thenActions.AddRange(body);
            return a;
        }

        static void AddLoops()
        {
            var items = new[] { Obj("M_A", false), Obj("M_B", false), Obj("M_C", false) };
            var t = Trigger(Box("M_Loops", new Vector3(12, 1, 2)), ("total", "System.Int32", false), ("i", "System.Int32", false), ("item", "UnityEngine.GameObject", false));
            var list = new KVariable { name = "items", typeName = "UnityEngine.GameObject[]" };
            list.initial.source = KArgSource.Objects;
            list.initial.objects.AddRange(items);
            t.variables.Add(list);
            // 0 + 1 + 2 + 3 + 4
            t.events.Add(On("Custom", "Sum", SetVar("total", Int(0)),
                LoopBlock(ActionCatalog.RepeatId, new[] { Act("Variable.Add", Str("total"), Var("i")) }, Int(5), Str("i"))));
            t.events.Add(On("Custom", "ShowAll",
                LoopBlock(ActionCatalog.ForEachId, new[] { Act("GameObject.SetActive", Var("item"), Bool(true)) }, Str("items"), Str("item"), Str(""))));
            // Counts 0, 1, 2, 3 and leaves at 3.
            t.events.Add(On("Custom", "Leave", SetVar("total", Int(0)),
                LoopBlock(ActionCatalog.RepeatId, new[]
                {
                    Act("Variable.Add", Str("total"), Int(1)),
                    IfBlock(false, new[] { Cond("i", KCompareOp.GreaterOrEqual, Int(3)) }, new[] { new KAction { actionId = "Flow.Break" } }),
                }, Int(100), Str("i"))));
            t.events.Add(On("Custom", "Halt", SetVar("total", Int(7)), new KAction { actionId = "Flow.Stop" }, SetVar("total", Int(99))));
            // A list of numbers (an int[] variable, empty here): its item type comes from the real type.
            t.variables.Add(new KVariable { name = "nums", typeName = "System.Int32[]" });
            t.events.Add(On("Custom", "Nums", LoopBlock(ActionCatalog.ForEachId, new[] { Act("Variable.Add", Str("total"), Var("i")) }, Str("nums"), Str("i"), Str(""))));
        }

        static IEnumerator CheckLoops(Action<bool, string> check)
        {
            var ub = Root("M_Loops").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            int Total() { ub.TryGetProgramVariable("v_total", out int n); return n; }
            ub.SendCustomEvent("Sum");
            check(Total() == 10, "M: repeat 5 times, counting 0..4 into a variable (" + Total() + ")");
            ub.SendCustomEvent("ShowAll");
            yield return null;
            check(Root("M_A").activeSelf && Root("M_B").activeSelf && Root("M_C").activeSelf, "M: for each item of an object list");
            ub.SendCustomEvent("Leave");
            check(Total() == 4, "M: leave the loop at the 4th round (" + Total() + ")");
            ub.SendCustomEvent("Halt");
            check(Total() == 7, "M: stop here skips the rest (" + Total() + ")");
            ub.SendCustomEvent("Nums");
            check(Total() == 7, "M: for each over an empty number list does nothing");
            // The Inspector's "▶ 実行" sends the event's entry: run "Sum" that way.
            var loops = Root("M_Loops").GetComponent<TripwireTrigger>();
            var sum = loops.events.First(e => e.name == "Sum");
            ub.SendCustomEvent(TripwireTriggerEditor.TryableEntry(sum, EventCatalog.Get(sum.eventId)));
            check(Total() == 10, "M: running an event from the Inspector's play-mode button (" + Total() + ")");
        }

        // ---------------- N. More Udon events: LateUpdate, FixedUpdate, collisions ----------------

        static void AddMoreEvents()
        {
            var hit = Obj("N_Hit", false);
            var floor = Box("N_Floor", new Vector3(14, 0, 2));
            floor.transform.localScale = new Vector3(3, 0.2f, 3);
            var t = Trigger(floor, ("late", "System.Int32", false), ("fixedSteps", "System.Int32", false), ("what", "UnityEngine.Collision", false));
            t.events.Add(On("LateUpdate", "", Act("Variable.Add", Str("late"), Int(1))));
            t.events.Add(On("FixedUpdate", "", Act("Variable.Add", Str("fixedSteps"), Int(1))));
            // A falling box hits the floor; the collision is the event's value.
            t.events.Add(On("OnCollisionEnter", "", Act("GameObject.SetActive", Objs(hit), Bool(true)), SetVar("what", new KArg { source = KArgSource.EventParam, name = "other" })));
            var box = Box("N_Box", new Vector3(14, 2, 2));
            box.transform.localScale = Vector3.one * 0.5f;
            box.AddComponent<Rigidbody>();
        }

        static IEnumerator CheckMoreEvents(Action<bool, string> check)
        {
            var ub = Root("N_Floor").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            yield return Seconds(1.5f);
            ub.TryGetProgramVariable("v_late", out int late);
            ub.TryGetProgramVariable("v_fixedSteps", out int steps);
            check(late > 10, "N: LateUpdate ran every frame (" + late + ")");
            check(steps > 10, "N: FixedUpdate ran every physics step (" + steps + ")");
            check(Root("N_Hit").activeSelf, "N: OnCollisionEnter ran when the box landed");
            ub.TryGetProgramVariable("v_what", out Collision what);
            check(what != null && what.gameObject.name == "N_Box", "N: the collision (event value) went into a variable (" + (what == null ? "null" : what.gameObject.name) + ")");
        }

        // ---------------- P. CyanTrigger's guide examples, built with Tripwire ----------------
        // (From CyanTrigger's public wiki: what its guides show people making. Checks Tripwire can make the same.)

        static KAction Api(string method, string result, params KArg[] args)
        {
            var a = new KAction { actionId = ActionCatalog.CallId, method = method, resultVariable = result ?? "" };
            a.args.AddRange(args);
            return a;
        }

        static KArg EventValue(string name) => new KArg { source = KArgSource.EventParam, name = name };
        static KArg Me() => new KArg { source = KArgSource.LocalPlayer };
        static KArg EnumArg(string member) => new KArg { stringValue = member };

        static void AddCyanGuides()
        {
            var tmp = System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro");
            var welcome = Obj("P_Welcome");
            welcome.AddComponent(tmp);
            var follower = Obj("P_Follower");
            var picks = new[] { Obj("P_Pick1", false), Obj("P_Pick2", false), Obj("P_Pick3", false) };
            var gate = Obj("P_Gate", false);
            var gated = Obj("P_Gated", false);
            var history = Obj("P_History");
            history.AddComponent(tmp);
            var t = Trigger(Box("P_Guides", new Vector3(16, 1, 2)), ("who", "System.String", false), ("td", "VRC.SDKBase.VRCPlayerApi+TrackingData", false),
                ("head", "UnityEngine.Vector3", false), ("roll", "System.Int32", false), ("pressed", "System.Boolean", false), ("gateOn", "System.Boolean", false),
                ("score", "System.Int32", false), ("prev", "System.Int32", false));

            // P1 player name on join → a sign.
            t.events.Add(On("OnPlayerJoined", "",
                Api("VRCSDKBaseVRCPlayerApi.__get_displayName__SystemString", "who", EventValue("player")),
                Act("Text.SetText", Objs(welcome.GetComponent(tmp)), Str("ようこそ {who}"))));
            // P2 follow the local player's head every frame.
            var follow = On("Update", "",
                Api("VRCSDKBaseVRCPlayerApi.__GetTrackingData__VRCSDKBaseVRCPlayerApiTrackingDataType__VRCSDKBaseVRCPlayerApiTrackingData", "td", Me(), EnumArg("Head")),
                Api("VRCSDKBaseVRCPlayerApiTrackingData.__get_position__UnityEngineVector3", "head", Var("td")),
                Act("Transform.SetPosition", Objs(follower), Var("head")));
            t.events.Add(follow);
            // P3 weighted random: roll 1-3, an else-if chain picks one.
            t.events.Add(On("Custom", "Pick",
                Act("Variable.Random", Str("roll"), Int(1), Int(3)),
                IfBlock(false, new[] { Cond("roll", KCompareOp.Equal, Int(1)) }, new[] { Act("GameObject.SetActive", Objs(picks[0]), Bool(true)) },
                    new[] { IfBlock(false, new[] { Cond("roll", KCompareOp.Equal, Int(2)) }, new[] { Act("GameObject.SetActive", Objs(picks[1]), Bool(true)) },
                        new[] { Act("GameObject.SetActive", Objs(picks[2]), Bool(true)) }) })));
            // P4 key press (compiles; ClientSim can't press keys here).
            t.events.Add(On("Update", "",
                Api("UnityEngineInput.__GetKeyDown__UnityEngineKeyCode__SystemBoolean", "pressed", EnumArg("Space")),
                IfBlock(false, new[] { Cond("pressed", KCompareOp.Equal, Bool(true)) }, new[] { Act("Debug.Log", Str("space")) })));
            // P5 "return if disabled": read activeSelf, stop when off.
            t.events.Add(On("Custom", "Gated",
                Api("UnityEngineGameObject.__get_activeSelf__SystemBoolean", "gateOn", Objs(gate)),
                IfBlock(false, new[] { Cond("gateOn", KCompareOp.Equal, Bool(false)) }, new[] { new KAction { actionId = ActionCatalog.StopId } }),
                Act("GameObject.SetActive", Objs(gated), Bool(true))));
            // P6 the value before a change: keep it in a second variable, updated last.
            t.events.Add(On("OnVariableChanged", "score",
                Act("Text.SetText", Objs(history.GetComponent(tmp)), Str("{prev}→{score}")),
                SetVar("prev", Var("score"))));
            t.events.Add(On("Custom", "Score5", SetVar("score", Int(5))));
            t.events.Add(On("Custom", "Score9", SetVar("score", Int(9))));
        }

        static IEnumerator CheckCyanGuides(Action<bool, string> check)
        {
            var ub = Root("P_Guides").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            yield return Seconds(0.3f);
            string Text(string name) => (string)Root(name).GetComponent(System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro")).GetType().GetProperty("text").GetValue(Root(name).GetComponent(System.Type.GetType("TMPro.TextMeshPro, Unity.TextMeshPro")));
            check(Text("P_Welcome").StartsWith("ようこそ ") && Text("P_Welcome").Length > 5, "P1: the joining player's name on a sign (" + Text("P_Welcome") + ")");
            var head = Networking.LocalPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            check(Vector3.Distance(Root("P_Follower").transform.position, head) < 0.5f, "P2: follows the local player's head (" + Root("P_Follower").transform.position + " vs " + head + ")");
            ub.SendCustomEvent("Pick");
            yield return null;
            int shown = new[] { "P_Pick1", "P_Pick2", "P_Pick3" }.Count(n => Root(n).activeSelf);
            check(shown == 1, "P3: an else-if chain picked exactly one (" + shown + ")");
            ub.SendCustomEvent("Gated");
            yield return null;
            check(!Root("P_Gated").activeSelf, "P5: stopped while the gate object is off");
            Root("P_Gate").SetActive(true);
            ub.SendCustomEvent("Gated");
            yield return null;
            check(Root("P_Gated").activeSelf, "P5: ran once the gate object is on");
            ub.SendCustomEvent("Score5");
            ub.SendCustomEvent("Score9");
            yield return null;
            check(Text("P_History") == "5→9", "P6: the value before the change (" + Text("P_History") + ")");
        }

        // ---------------- Q. Repeat while, skip to the next round, otherwise-if ----------------

        static KAction WhileBlock(KCondition[] conditions, KAction[] body)
        {
            var a = new KAction { actionId = ActionCatalog.WhileId };
            a.conditions.AddRange(conditions);
            a.thenActions.AddRange(body);
            return a;
        }

        static void AddMoreFlow()
        {
            var grades = new[] { Obj("Q_A", false), Obj("Q_B", false), Obj("Q_C", false) };
            var t = Trigger(Box("Q_Flow", new Vector3(18, 1, 2)), ("n", "System.Int32", false), ("total", "System.Int32", false), ("score", "System.Int32", false));
            // 1 + 2 + 4 + 5: count to 5, skipping 3.
            t.events.Add(On("Custom", "Sum", SetVar("n", Int(0)), SetVar("total", Int(0)),
                WhileBlock(new[] { Cond("n", KCompareOp.Less, Int(5)) }, new[]
                {
                    Act("Variable.Add", Str("n"), Int(1)),
                    IfBlock(false, new[] { Cond("n", KCompareOp.Equal, Int(3)) }, new[] { new KAction { actionId = ActionCatalog.ContinueId } }),
                    Act("Variable.Add", Str("total"), Var("n")),
                })));
            // Continuous sync: a float and a position, smoothed.
            var smooth = Trigger(Box("Q_Smooth", new Vector3(18, 1, 4)), ("speed", "System.Single", true), ("spot", "UnityEngine.Vector3", true));
            smooth.continuousSync = true;
            smooth.events.Add(On("Custom", "Move", SetVar("speed", Flt(2.5f))));
            // A temporary variable starts from its initial value every time.
            var tmp = new KVariable { name = "tmp", typeName = "System.Int32", temporary = true };
            t.variables.Add(tmp);
            t.events.Add(On("Custom", "Bump", Act("Variable.Add", Str("tmp"), Int(1)), SetVar("total", Var("tmp"))));
            // 80+ A, otherwise if 50+ B, otherwise C.
            t.events.Add(On("Custom", "Grade", SetVar("score", Int(60)),
                IfBlock(false, new[] { Cond("score", KCompareOp.GreaterOrEqual, Int(80)) }, new[] { Act("GameObject.SetActive", Objs(grades[0]), Bool(true)) },
                    new[] { IfBlock(false, new[] { Cond("score", KCompareOp.GreaterOrEqual, Int(50)) }, new[] { Act("GameObject.SetActive", Objs(grades[1]), Bool(true)) },
                        new[] { Act("GameObject.SetActive", Objs(grades[2]), Bool(true)) }) })));
        }

        static IEnumerator CheckMoreFlow(Action<bool, string> check)
        {
            var ub = Root("Q_Flow").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            ub.SendCustomEvent("Sum");
            ub.TryGetProgramVariable("v_total", out int total);
            check(total == 12, "Q: repeat while n < 5, skipping 3 (" + total + ")");
            ub.SendCustomEvent("Grade");
            yield return null;
            check(!Root("Q_A").activeSelf && Root("Q_B").activeSelf && !Root("Q_C").activeSelf, "Q: otherwise-if picked B for 60");
            var smooth = Root("Q_Smooth").GetComponent<UdonBehaviour>();
            check(smooth.SyncMethod == VRC.SDKBase.Networking.SyncType.Continuous, "Q: continuous sync (" + smooth.SyncMethod + ")");
            smooth.SendCustomEvent("Move");
            smooth.TryGetProgramVariable("v_speed", out float speed);
            check(Mathf.Approximately(speed, 2.5f), "Q: a continuously synced value set (" + speed + ")");
            ub.SendCustomEvent("Bump");
            ub.SendCustomEvent("Bump");
            ub.TryGetProgramVariable("v_total", out int bumped);
            check(bumped == 1, "Q: a temporary variable starts afresh in every event (" + bumped + ")");
        }

        // ---------------- R. Passing values between triggers ----------------

        static void AddRemote()
        {
            var lamp = Obj("R_Lamp", false);
            var b = Trigger(Box("R_B", new Vector3(20, 1, 4)), ("点数", "System.Int32", false));
            b.events.Add(On("OnVariableChanged", "点数", Act("GameObject.SetActive", Objs(lamp), Bool(true))));
            b.events.Add(On("Custom", "Receive", Act("Variable.Add", Str("点数"), Var("点数")))); // doubles what it was given
            var a = Trigger(Box("R_A", new Vector3(20, 1, 2)), ("read", "System.Int32", false));
            var set = new KAction { actionId = ActionCatalog.SetRemoteId, args = { Objs(b.gameObject), Str("点数"), Int(7) } };
            var send = Act("Event.Send", Objs(b.gameObject), Str("Receive"), new KArg { intValue = 0 });
            var get = new KAction { actionId = ActionCatalog.GetRemoteId, args = { Objs(b.gameObject), Str("点数"), Str("read") } };
            a.events.Add(On("Custom", "Go", set, send, get));
        }

        static IEnumerator CheckRemote(Action<bool, string> check)
        {
            var a = Root("R_A").GetComponent<UdonBehaviour>();
            var b = Root("R_B").GetComponent<UdonBehaviour>();
            yield return Ready(a, b);
            a.SendCustomEvent("Go");
            yield return null;
            check(Root("R_Lamp").activeSelf, "R: setting another trigger's variable ran its change event");
            a.TryGetProgramVariable("v_read", out int read);
            check(read == 14, "R: the value was passed to its Custom event, and read back (" + read + ")");
        }

        // ---------------- S. Debug tools: the event history and values changed by hand ----------------

        static void AddDebugTools()
        {
            var t = Trigger(Box("S_Counter", new Vector3(24, 1, 0)), ("count", "System.Int32", true), ("seen", "System.Int32", false));
            t.events.Add(When(On("Interact", "", Act("Variable.Add", Str("count"), Int(1))), If("count", KCompareOp.Less, Int(2))));
            t.events.Add(On("OnVariableChanged", "count", SetVar("seen", Var("count"))));
        }

        static IEnumerator CheckDebugTools(Action<bool, string> check)
        {
            var t = Root("S_Counter").GetComponent<TripwireTrigger>();
            var ub = t.generated;
            yield return Ready(ub);
            TripwireTrace.PollNow(); // turns the notes on
            ub.TryGetProgramVariable(CodeGenerator.TraceFlag, out bool on);
            check(on, "S: the history's notes are on during Play");
            for (int i = 0; i < 3; i++) { ub.SendCustomEvent("_interact"); yield return null; }
            TripwireTrace.PollNow();
            var mine = TripwireTrace.Records.Where(r => r.Trigger == t).ToList();
            var presses = mine.Where(r => r.Event == 0).ToList();
            check(presses.Count == 3 && presses[0].Ran && presses[1].Ran && !presses[2].Ran, "S: two presses ran and the third was stopped (" + string.Join(", ", presses.Select(r => r.Ran)) + ")");
            var why = presses.LastOrDefault()?.Detail ?? "";
            check(why.Contains("count") && why.Contains("2"), "S: the stop names the condition and the value (" + why + ")");
            var changes = mine.Where(r => r.Event == 1).ToList();
            check(changes.Count == 2 && changes[1].Detail.Contains("2"), "S: each change was noted with its value (" + string.Join(" | ", changes.Select(r => r.Detail)) + ")");
            check(TripwireTrace.Of(t, 0).runs == 2, "S: the card counts its runs (" + TripwireTrace.Of(t, 0).runs + ")");

            // Changed by hand: the change event runs (seen follows), and the condition lets the press through again.
            TripwireTrace.SetValue(t, t.variables.First(v => v.name == "count"), 0);
            yield return null;
            ub.TryGetProgramVariable("v_seen", out int seen);
            check(seen == 0, "S: a value changed by hand ran its change event (seen " + seen + ")");
            ub.SendCustomEvent("_interact");
            yield return null;
            ub.TryGetProgramVariable("v_count", out int count);
            check(count == 1, "S: after the change by hand the press runs again (count " + count + ")");
            TripwireTrace.PollNow();
            check(TripwireTrace.Of(t, 0).last?.Ran == true, "S: the card's last record is the new run");
        }

        // ---------------- T. Wider Udon API: a callback to this trigger, Calculate, part of a struct, enums as numbers, Get Component, a tween stopped ----------------

        /// <summary>The Udon name of an offered member (the first that fits), so the scenario doesn't spell long names.</summary>
        static CallSpec Api(string type, string member, Func<CallSpec, bool> fits = null) =>
            UdonApi.All.First(c => c.DeclaringType == type && c.Member == member && (fits == null || fits(c)));

        /// <summary>A plain value for each parameter: this trigger for a callback, a name, a short time, the first enum member.</summary>
        static KAction CallWith(CallSpec c, string result, params KArg[] first)
        {
            var a = new KAction { actionId = "Udon.Call", method = c.UdonName, resultVariable = result ?? "" };
            a.args.AddRange(first);
            foreach (var p in c.Params.Skip(a.args.Count - (c.Instance != null ? 1 : 0)))
                a.args.Add(p.Type.Kind == ValueKind.Object && p.Type.UnityType == "VRC.Udon.UdonBehaviour" ? new KArg { source = KArgSource.Self }
                    : p.Type.Kind == ValueKind.String ? Str("T_Ping")
                    : p.Type.Kind == ValueKind.Float ? new KArg { floatValue = 0.2f }
                    : p.Type.Kind == ValueKind.Enum ? Str(UdonApi.EnumMembers(TripwireModel.ResolveType(p.Type.UnityType)).First())
                    : new KArg());
            return a;
        }

        static void AddWiderApi()
        {
            var mover = Obj("T_Mover");
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "T_Sphere";
            var t = Trigger(Box("T_Wide", new Vector3(26, 1, 0)), ("pos", "UnityEngine.Vector3", false), ("label", "System.String", false), ("col", "UnityEngine.Collider", false),
                ("shadow", "System.Int32", false), ("pinged", "System.Boolean", false), ("handle", "VRC.SDK3.Components.VRCTweenHandle", false));
            t.variables.First(v => v.name == "pos").initial.vectorValue = new Vector3(1, 2, 3);

            var setY = new KAction { actionId = "Udon.Call", method = Api("UnityEngine.Vector3", "y", c => c.Kind == CallKind.Set).UdonName, args = { Var("pos"), new KArg { floatValue = 5f } } };
            var up = new KAction { actionId = ActionCatalog.CalculateId, args = { Str("pos"), Var("pos"), Int((int)ActionCatalog.CalcOp.Add), new KArg { vectorValue = new Vector3(0, 1, 0) } } };
            var join = new KAction { actionId = ActionCatalog.CalculateId, args = { Str("label"), Str("Hi "), Int((int)ActionCatalog.CalcOp.Add), Str("there") } };
            var getCol = new KAction { actionId = ActionCatalog.GetComponentId, args = { Str("col"), Objs(sphere), Int(0) } };
            var shadow = CallWith(Api("UnityEngine.Renderer", "shadowCastingMode", c => c.Kind == CallKind.Get), "shadow", Objs(sphere));
            var delayed = CallWith(Api("VRC.SDK3.Components.VRCTween", "DelayedCall", c => c.Params.Any(p => p.Type.UnityType == "VRC.Udon.UdonBehaviour")), null);
            var tween = Api("VRC.SDK3.Components.VRCTween", "TweenPosition", c => c.Params.Count > 0 && c.Params[0].Type.Kind == ValueKind.Object && c.Params[0].Type.UnityType == "UnityEngine.GameObject"
                                                                                && c.Params.Any(p => p.Type.Kind == ValueKind.Vector3) && c.Returns != null);
            var go = CallWith(tween, "handle", Objs(mover), new KArg { vectorValue = new Vector3(0, 10, 0) });
            go.args[3 - 1].floatValue = 1f; // the duration (the parameter after the target and the position)
            var kill = CallWith(Api("VRC.SDK3.Components.VRCTweenHandle", "Kill"), null, Var("handle"));
            t.events.Add(On("Interact", "", setY, up, join, getCol, shadow, delayed, go, kill));
            t.events.Add(On("Custom", "T_Ping", SetVar("pinged", Bool(true))));
        }

        static IEnumerator CheckWiderApi(Action<bool, string> check)
        {
            var ub = Root("T_Wide").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            ub.Interact();
            yield return null;
            ub.TryGetProgramVariable("v_pos", out Vector3 pos);
            check(pos == new Vector3(1, 6, 3), "T: pos.y set on a variable, then Calculate added (0, 1, 0) (" + pos + ")");
            ub.TryGetProgramVariable("v_label", out string label);
            check(label == "Hi there", "T: Calculate joined text (" + label + ")");
            ub.TryGetProgramVariable("v_col", out Collider col);
            check(col != null && col == Root("T_Sphere").GetComponent<Collider>(), "T: Get Component found the sphere's collider");
            ub.TryGetProgramVariable("v_shadow", out int shadow);
            check(shadow == (int)UnityEngine.Rendering.ShadowCastingMode.On, "T: an enum Udon can't hold came back as its number (" + shadow + ")");
            var end = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < end) yield return null;
            ub.TryGetProgramVariable("v_pinged", out bool pinged);
            check(pinged, "T: VRCTween.DelayedCall called this trigger's Custom event back");
            var y = Root("T_Mover").transform.position.y;
            check(y < 5f, "T: the tween's handle, kept in a variable, stopped it (y " + y + ")");
        }

        // ---------------- U. Handy actions: toggling a collider, Respawn, a random event, a random item, values in text ----------------

        static void AddHandy()
        {
            var t = Trigger(Box("U_Handy", new Vector3(28, 1, 0)), ("picked", "System.Int32", false), ("item", "UnityEngine.GameObject", false));
            var wall = Box("U_Wall", new Vector3(28, 1, 3));
            var ball = Box("U_Ball", new Vector3(30, 1, 0));
            var choices = new[] { Obj("U_A"), Obj("U_B"), Obj("U_C") };
            var tmp = TripwireModel.ResolveType("TMPro.TextMeshPro");
            var signObject = new GameObject("U_Sign", tmp);
            var sign = new KAction { actionId = "Text.SetText", args = { Objs(signObject.GetComponent(tmp)), Str("{プレイヤー数}人 {自分の名前} {時刻}") } };
            t.events.Add(On("Interact", "",
                Act("Collider.ToggleEnabled", Objs(wall.GetComponent<Collider>())),
                Act(ActionCatalog.RespawnId, Objs(ball)),
                new KAction { actionId = ActionCatalog.SendRandomEventId, args = { new KArg { source = KArgSource.Self }, Str("U_One\nU_Two"), Int(0) } },
                Act(ActionCatalog.RandomItemId, Str("item"), Objs(choices)),
                sign));
            t.events.Add(On("Custom", "U_One", SetVar("picked", Int(1))));
            t.events.Add(On("Custom", "U_Two", SetVar("picked", Int(2))));
        }

        static IEnumerator CheckHandy(Action<bool, string> check)
        {
            var ub = Root("U_Handy").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            var ball = Root("U_Ball");
            ball.transform.position = new Vector3(40, 5, 40); // moved during Play
            ub.Interact();
            yield return null;
            check(!Root("U_Wall").GetComponent<Collider>().enabled, "U: the wall's collider was toggled off");
            check(Vector3.Distance(ball.transform.position, new Vector3(30, 1, 0)) < 0.01f, "U: Respawn put the ball back where it started (" + ball.transform.position + ")");
            ub.TryGetProgramVariable("v_picked", out int picked);
            check(picked == 1 || picked == 2, "U: one of the two Custom events ran (" + picked + ")");
            ub.TryGetProgramVariable("v_item", out GameObject item);
            check(item != null && item.name.StartsWith("U_") && item.name.Length == 3, "U: a random item of the list went into the variable (" + (item != null ? item.name : "null") + ")");
            var text = TextOf("U_Sign");
            check(text.StartsWith(VRCPlayerApi.GetPlayerCount() + "人 ") && text.Contains(":"), "U: the text shows the player count, the name and the time (" + text + ")");
            ub.Interact();
            yield return null;
            check(Root("U_Wall").GetComponent<Collider>().enabled, "U: toggled back on");
        }

        // ---------------- V. Who can use it: a list, not in a list, the master, the instance's creator ----------------

        static void AddGates()
        {
            var list = ScriptableObject.CreateInstance<TripwirePlayerList>();
            list.names.Add("Nobody Here");
            Directory.CreateDirectory("Assets/TripwireTests/Temp");
            AssetDatabase.CreateAsset(list, "Assets/TripwireTests/Temp/Gate List.asset");
            KEvent Click(KGate gate) => new KEvent { eventId = "Interact", gate = gate, gateList = list, actions = { Act("Variable.Add", Str("runs"), Int(1)) } };
            Trigger(Box("V_InList", new Vector3(32, 1, 0)), ("runs", "System.Int32", false)).events.Add(Click(KGate.InList));
            Trigger(Box("V_NotInList", new Vector3(32, 1, 2)), ("runs", "System.Int32", false)).events.Add(Click(KGate.NotInList));
            Trigger(Box("V_Master", new Vector3(32, 1, 4)), ("runs", "System.Int32", false)).events.Add(Click(KGate.Master));
            var creator = Trigger(Box("V_Creator", new Vector3(32, 1, 6)), ("runs", "System.Int32", false));
            creator.events.Add(Click(KGate.InstanceOwner)); // compiles; who created a ClientSim instance isn't checked here
        }

        static IEnumerator CheckGates(Action<bool, string> check)
        {
            var inList = Root("V_InList").GetComponent<UdonBehaviour>();
            var notInList = Root("V_NotInList").GetComponent<UdonBehaviour>();
            var master = Root("V_Master").GetComponent<UdonBehaviour>();
            yield return Ready(inList, notInList, master);
            inList.Interact(); notInList.Interact(); master.Interact();
            yield return null;
            int Runs(UdonBehaviour ub) { ub.TryGetProgramVariable("v_runs", out int n); return n; }
            check(Runs(inList) == 0, "V: a card for the people in a list didn't run for someone not in it");
            check(inList.DisableInteractive, "V: and that player can't point at it");
            check(Runs(notInList) == 1 && !notInList.DisableInteractive, "V: a card for the people not in the list ran");
            check(Runs(master) == 1, "V: a card for the master ran for the master (the only player)");
        }

        // ---------------- W. Saved variables: kept in PlayerData, given back when the data is loaded ----------------

        static void AddSaved()
        {
            var t = Trigger(Box("W_Saved", new Vector3(34, 1, 0)), ("coins", "System.Int32", false));
            t.variables[0].saved = true;
            t.variables[0].saveKey = "tripwire_test_coins";
            t.events.Add(On("Interact", "", Act("Variable.Add", Str("coins"), Int(1))));
            // Every kind that can be saved, so each PlayerData Set / TryGet the generator uses compiles.
            foreach (var (name, type) in new[] { ("flag", "System.Boolean"), ("level", "System.Single"), ("nick", "System.String"), ("spot2", "UnityEngine.Vector2"),
                                                 ("spot", "UnityEngine.Vector3"), ("tint", "UnityEngine.Color"), ("turn", "UnityEngine.Quaternion") })
                t.variables.Add(new KVariable { name = name, typeName = type, saved = true, saveKey = "tripwire_test_" + name });
        }

        static IEnumerator CheckSaved(Action<bool, string> check)
        {
            var ub = Root("W_Saved").GetComponent<UdonBehaviour>();
            yield return Ready(ub);
            var end = Time.realtimeSinceStartup + 3f;
            bool restored = false;
            while (Time.realtimeSinceStartup < end && !(ub.TryGetProgramVariable("tw_Restored", out restored) && restored)) yield return null;
            check(restored, "W: the player's saved data came back (OnPlayerRestored)");
            ub.TryGetProgramVariable("v_coins", out int before);
            ub.Interact();
            yield return null;
            ub.TryGetProgramVariable("v_coins", out int after);
            bool saved = VRC.SDK3.Persistence.PlayerData.TryGetInt(Networking.LocalPlayer, "tripwire_test_coins", out int stored);
            check(after == before + 1 && saved && stored == after, "W: the change was saved (" + before + " → " + after + ", saved " + (saved ? stored.ToString() : "nothing") + ")");
        }

        // ---------------- X. Starter cards, as made by one click, with a text dragged in ----------------

        static void AddStarters()
        {
            var tmp = TripwireModel.ResolveType("TMPro.TextMeshPro");
            foreach (var (name, ja, x) in new[] { ("X_Countdown", "10 からカウントダウン", 0), ("X_Headcount", "いる人数を表示する", 2), ("X_Visits", "来た回数を数える（保存）", 4) })
            {
                var t = Box(name, new Vector3(36, 1, x)).AddComponent<TripwireTrigger>();
                TripwireTriggerEditor.Starters.First(s => s.Ja == ja).Make(t);
                var sign = new GameObject(name + "_Text", tmp).GetComponent(tmp);
                foreach (var a in t.events.SelectMany(e => e.actions).Where(a => a.actionId == "Text.SetText")) a.args[0] = Objs(sign);
            }
        }

        static IEnumerator CheckStarters(Action<bool, string> check)
        {
            var countdown = Root("X_Countdown").GetComponent<UdonBehaviour>();
            var headcount = Root("X_Headcount").GetComponent<UdonBehaviour>();
            var visits = Root("X_Visits").GetComponent<UdonBehaviour>();
            yield return Ready(countdown, headcount, visits);
            string Text(string name) { var c = Root(name + "_Text").GetComponent(TripwireModel.ResolveType("TMPro.TextMeshPro")); return (string)c.GetType().GetProperty("text").GetValue(c); }
            countdown.Interact();
            var end = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < end) yield return null;
            var shown = Text("X_Countdown");
            check(shown == "8" || shown == "9", "X: the countdown went down each second (shows " + shown + " after 2.5 s)");
            check(Text("X_Headcount").StartsWith("1 ") || Text("X_Headcount").StartsWith("1人") , "X: the headcount shows one player (" + Text("X_Headcount") + ")");
            check(Text("X_Visits").Contains("回目") || Text("X_Visits").StartsWith("Visit"), "X: the visits were counted after the saved value came back (" + Text("X_Visits") + ")");
        }
    }
}
