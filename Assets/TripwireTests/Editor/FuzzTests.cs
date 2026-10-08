using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using NUnit.Framework;
using UdonSharp;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Random = System.Random;

using static Tripwire.Tests.TestSupport;

namespace Tripwire.Tests
{
    /// <summary>
    /// Random triggers built from the catalogs, the way the Inspector can build them (any event, action, argument source,
    /// condition, broadcast, delay — valid or not). The oracle: whatever the generator accepts without errors must compile
    /// as C# and UdonSharp. Logs/fuzz-report.txt lists what the generator rejected, by message.
    /// </summary>
    [Explicit("fuzz: run on demand (scripts/run-all-tests.sh runs it in its own batch)")]
    public class FuzzTests
    {
        const string SceneDir = "Assets/TripwireTests/Temp";
        const string ScenePath = SceneDir + "/Fuzz.unity";
        const int Seed = 20261006;
        const int Triggers = 300;

        static readonly string[] VariableTypes =
        {
            "System.Boolean", "System.Int32", "System.Single", "System.String", "UnityEngine.Vector3", "UnityEngine.Vector2",
            "UnityEngine.Color", "UnityEngine.Quaternion", "UnityEngine.GameObject", "UnityEngine.GameObject[]", "UnityEngine.Transform",
            "VRC.SDKBase.VRCPlayerApi", "VRC.SDKBase.VRCUrl", "System.Byte", "System.Int32[]", "UnityEngine.AudioSource",
        };
        static readonly string[] VariableNames = { "a", "count", "isOn", "スコア", "扉_開", "name2", "target", "list", "url", "speed", "x" };

        /// <summary>Objects carrying every component an action can target.</summary>
        static List<GameObject> Pool()
        {
            var pool = new List<GameObject>();
            GameObject With(string name, params Type[] types) { var g = new GameObject(name, types); pool.Add(g); return g; }
            With("P_Audio", typeof(AudioSource));
            With("P_Anim", typeof(Animator));
            With("P_Particles", typeof(ParticleSystem));
            With("P_Light", typeof(Light));
            With("P_Text", TripwireModel.ResolveType("TMPro.TextMeshPro"));
            var pickup = GameObject.CreatePrimitive(PrimitiveType.Cube); pickup.name = "P_Pickup";
            pickup.AddComponent<Rigidbody>().isKinematic = true; pickup.AddComponent<VRC.SDK3.Components.VRCPickup>(); pool.Add(pickup);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = "P_Cube"; pool.Add(cube);
            With("P_Video", typeof(VRC.SDK3.Video.Components.VRCUnityVideoPlayer));
            With("P_Empty");
            return pool;
        }

        static KArg RandomArg(Random r, ParamType type, TripwireTrigger t, EventSpec ev, List<GameObject> pool, List<TripwireTrigger> others)
        {
            var arg = new KArg();
            int pick = r.Next(10);
            bool wild = r.Next(5) == 0; // otherwise only what the Inspector would offer for this type
            var vars = t.variables.Where(v => wild || (type != null && CodeGenerator.IsAssignable(type, TripwireModel.VariableType(v)))).ToList();
            var prms = ev == null ? new List<EventParam>() : ev.Params.Where(p => wild || (type != null && CodeGenerator.IsAssignable(type, p.Type))).ToList();
            if (pick <= 1 && vars.Count > 0) { arg.source = KArgSource.Variable; arg.name = vars[r.Next(vars.Count)].name; return arg; }
            if (pick == 2 && prms.Count > 0) { arg.source = KArgSource.EventParam; arg.name = prms[r.Next(prms.Count)].Name; return arg; }
            if (type != null && (type.Kind == ValueKind.Object || type.Kind == ValueKind.Other))
            {
                if (pick == 3) { arg.source = KArgSource.Self; return arg; }
                arg.source = KArgSource.Objects;
                var want = TripwireModel.ResolveType(type.UnityType);
                List<Object> fits;
                if (want == typeof(VRC.Udon.UdonBehaviour)) fits = others.Select(o => (Object)o.gameObject).ToList();
                else fits = pool.Where(g => want == null || want == typeof(GameObject) || (typeof(Component).IsAssignableFrom(want) && g.GetComponent(want) != null)).Cast<Object>().ToList();
                int n = type.IsArray ? r.Next(0, 3) : 1;
                for (int i = 0; i < n && fits.Count > 0; i++) arg.objects.Add(fits[r.Next(fits.Count)]);
                return arg;
            }
            if (type != null && type.Kind == ValueKind.Player && pick < 6) { arg.source = KArgSource.LocalPlayer; return arg; }
            arg.source = KArgSource.Constant;
            arg.boolValue = r.Next(2) == 0;
            arg.intValue = r.Next(-3, 300);
            arg.floatValue = (float)(r.NextDouble() * 10 - 2);
            arg.stringValue = new[] { "", "hello", "\"quoted\" \\ back", "改行\nあり", "https://example.com/v.mp4", "Ping" }[r.Next(6)];
            arg.vectorValue = new Vector3(r.Next(5), r.Next(5), r.Next(5));
            arg.vector4Value = new Vector4(r.Next(2), r.Next(2), r.Next(2), 1);
            return arg;
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var world = new GameObject("VRCWorld");
            world.AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>().spawns = new[] { world.transform };
            var pool = Pool();
            var canvas = new GameObject("Canvas", typeof(Canvas), typeof(VRC.SDK3.Components.VRCUiShape));
            Component Ui(Type t) { var g = new GameObject("UI_" + t.Name, typeof(RectTransform), t); g.transform.SetParent(canvas.transform, false); return g.GetComponent(t); }
            var uis = new Dictionary<string, Component> { { "UnityEngine.UI.Button", Ui(typeof(Button)) }, { "UnityEngine.UI.Toggle", Ui(typeof(Toggle)) }, { "UnityEngine.UI.Slider", Ui(typeof(Slider)) } };

            var r = new Random(Seed);
            var events = EventCatalog.All.ToList();
            // Not drawn: API calls (UdonApiCompileTests) and actions on another trigger's variables (ScenarioTests R).
            var actions = ActionCatalog.All.Where(a => a.Id != ActionCatalog.CallId && a.Id != ActionCatalog.ScriptCallId
                                                       && a.Special != ActionSpecial.SetRemoteVariable && a.Special != ActionSpecial.GetRemoteVariable).ToList();
            var triggers = new List<TripwireTrigger>();
            for (int n = 0; n < Triggers; n++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = "Fuzz" + n;
                if (r.Next(5) == 0) go.AddComponent<VRC.SDK3.Video.Components.VRCUnityVideoPlayer>();
                triggers.Add(go.AddComponent<TripwireTrigger>());
            }
            foreach (var t in triggers)
            {
                foreach (var name in VariableNames.OrderBy(_ => r.Next()).Take(r.Next(0, 4)))
                {
                    var v = new KVariable { name = name, typeName = VariableTypes[r.Next(VariableTypes.Length)] };
                    var vt = TripwireModel.VariableType(v);
                    v.synced = r.Next(3) == 0 && (r.Next(5) == 0 || (vt != null && vt.Kind != ValueKind.Object && vt.Kind != ValueKind.Player));
                    v.initial = RandomArg(r, TripwireModel.VariableType(v), t, null, pool, triggers);
                    if (v.initial.source != KArgSource.Objects && v.initial.source != KArgSource.Constant) v.initial = new KArg();
                    t.variables.Add(v);
                }
                for (int e = r.Next(1, 4); e > 0; e--)
                {
                    var spec = events[r.Next(events.Count)];
                    var ev = new KEvent
                    {
                        eventId = spec.Id,
                        broadcast = (KBroadcast)r.Next(3),
                        delaySeconds = r.Next(4) == 0 ? (float)r.NextDouble() : 0f,
                        playerFilter = (KPlayerFilter)r.Next(3),
                        interactText = r.Next(2) == 0 ? "押す" : "",
                    };
                    if (spec.Shape == EventShape.Custom) ev.name = new[] { "Ping", "Open", "Do_It", "Reset2" }[r.Next(4)];
                    if (spec.Shape == EventShape.VariableChanged) ev.name = t.variables.Count > 0 ? t.variables[r.Next(t.variables.Count)].name : "";
                    if (spec.Shape == EventShape.Timer)
                    {
                        ev.name = new[] { "blink", "tick", "" }[r.Next(3)];
                        ev.timerMin = new[] { 0.5f, 2f, 0f }[r.Next(3)];
                        ev.timerMax = ev.timerMin + new[] { 0f, 3f }[r.Next(2)];
                        ev.timerRepeat = r.Next(2) == 0;
                        ev.timerAutoStart = r.Next(2) == 0;
                    }
                    if (spec.Shape == EventShape.Listen) ev.name = new[] { "_TvPlay", "OnUSharpVideoPlay", "_onVideoStart", "_OnPlay" }[r.Next(4)];
                    if (spec.Shape == EventShape.Ui && spec.UiType != null && uis.TryGetValue(spec.UiType, out var ui)) ev.uiTarget = ui;
                    KCondition RandomCondition()
                    {
                        var v = t.variables[r.Next(t.variables.Count)];
                        var vt = TripwireModel.VariableType(v);
                        bool ordered = vt != null && !vt.IsArray && (vt.Kind == ValueKind.Int || vt.Kind == ValueKind.Float);
                        var op = (KCompareOp)(ordered || r.Next(5) == 0 ? r.Next(6) : r.Next(2));
                        return new KCondition { variable = v.name, op = op, value = RandomArg(r, vt, t, spec, pool, triggers), negate = r.Next(4) == 0 };
                    }

                    KAction RandomAction(int depth, bool inLoop = false)
                    {
                        // The Inspector lists only fitting variables, so an action with none to pick is mostly re-drawn
                        // (now and then kept, to check the error).
                        for (int tries = 0; ; tries++)
                        {
                            var drawn = DrawAction(depth, inLoop);
                            bool misplaced = (drawn.actionId == ActionCatalog.BreakId || drawn.actionId == ActionCatalog.ContinueId) && !inLoop; // the error is checked now and then
                            if (tries >= 8 || r.Next(5) == 0 || (!misplaced && !drawn.args.Any(x => x.stringValue == "nope"))) return drawn;
                        }
                    }

                    KAction DrawAction(int depth, bool inLoop)
                    {
                        var aspec = actions[r.Next(actions.Count)];
                        var act = new KAction { actionId = aspec.Id };
                        if (aspec.HasConditions) // If, While
                        {
                            // A block with conditions and nested actions (one level deeper at most twice).
                            act.matchAny = r.Next(2) == 0;
                            for (int c = r.Next(1, 3); c > 0 && t.variables.Count > 0; c--) act.conditions.Add(RandomCondition());
                            for (int n = r.Next(0, 3); n > 0 && depth < 2; n--) act.thenActions.Add(RandomAction(depth + 1, inLoop || aspec.IsLoop));
                            if (aspec.HasElse) for (int n = r.Next(0, 2); n > 0 && depth < 2; n--) act.elseActions.Add(RandomAction(depth + 1, inLoop));
                            return act;
                        }
                        foreach (var prm in aspec.Params)
                        {
                            if (prm.VariableRef)
                            {
                                // Toggle takes bool variables, Add numbers (as the Inspector's list does), now and then anything.
                                // The variables the Inspector offers (now and then any, to check the errors).
                                var listType = prm.Role == VariableRole.Item ? TripwireModel.VariableType(t, act.args[0].stringValue) : null;
                                var fit = t.variables.Where(v => r.Next(5) == 0 || ActionRules.VariableFits(aspec, prm, TripwireModel.VariableType(v), v.synced, listType)).ToList();
                                if (prm.Optional && r.Next(2) == 0) { act.args.Add(new KArg { stringValue = "" }); continue; }
                                act.args.Add(new KArg { stringValue = fit.Count > 0 ? fit[r.Next(fit.Count)].name : "nope" });
                                continue;
                            }
                            if (prm.Choices != null) { act.args.Add(new KArg { intValue = r.Next(prm.Choices.Length) }); continue; }
                            if (prm.TimerRef) { act.args.Add(new KArg()); continue; } // filled in once all events exist
                            var type = prm.Type ?? TripwireModel.EffectiveType(t, act, prm);
                            act.args.Add(RandomArg(r, type, t, spec, pool, triggers));
                        }
                        if (ActionCatalog.IsBlock(aspec.Id)) // a loop's body
                            for (int n = r.Next(0, 3); n > 0 && depth < 2; n--) act.thenActions.Add(RandomAction(depth + 1, inLoop: true));
                        return act;
                    }

                    ev.conditionsMatchAny = r.Next(3) == 0;
                    for (int c = r.Next(0, 3); c > 0 && t.variables.Count > 0; c--) ev.conditions.Add(RandomCondition());
                    for (int a = r.Next(1, 5); a > 0; a--) ev.actions.Add(RandomAction(0));
                    t.events.Add(ev);
                }
                // Start / Stop Timer pick one of the trigger's timers (as the popup offers), now and then a missing one.
                bool usesTimers = t.events.SelectMany(x => TripwireModel.FlatActions(x.actions)).Any(a => ActionCatalog.Get(a.actionId)?.Params.FirstOrDefault()?.TimerRef == true);
                if (usesTimers && !t.events.Any(x => x.eventId == "Timer" && x.name != "") && r.Next(5) > 0)
                    t.events.Add(new KEvent { eventId = "Timer", name = "tick", timerAutoStart = false, actions = { new KAction { actionId = "Debug.Log", args = { new KArg { stringValue = "tick" } } } } });
                var timers = t.events.Where(x => x.eventId == "Timer" && x.name != "").Select(x => x.name).ToList();
                foreach (var a in t.events.SelectMany(x => TripwireModel.FlatActions(x.actions)))
                    if (ActionCatalog.Get(a.actionId)?.Params.FirstOrDefault()?.TimerRef == true)
                        a.args[0].stringValue = timers.Count > 0 && r.Next(5) > 0 ? timers[r.Next(timers.Count)] : "nope";
            }
            Directory.CreateDirectory(SceneDir);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        [UnityTest]
        public IEnumerator AcceptedRandomTriggersCompile()
        {
            LogAssert.ignoreFailingMessages = true;
            if (!EditorApplication.isPlaying && Root("Fuzz0") == null)
            {
                BuildScene();
                TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
            }
            yield return new RecompileScripts(false);
            LogAssert.ignoreFailingMessages = true;

            var triggers = TripwireCompiler.SceneTriggers().ToList();
            var state = TripwireCompiler.ApplyAll(triggers);
            var rejected = new Dictionary<string, int>();
            var notAttached = new List<string>();
            int accepted = 0;
            var lines = new List<string>();
            foreach (var t in triggers)
            {
                var g = CodeGenerator.Generate(TripwireModel.ToProgram(t));
                if (!g.HasErrors)
                {
                    accepted++;
                    lines.Add(t.name + " → " + t.generatedClass);
                    if (t.generated == null || UdonSharpEditor.UdonSharpEditorUtility.GetUdonSharpBehaviourType(t.generated)?.Name != t.generatedClass)
                        notAttached.Add(t.name);
                }
                foreach (var d in g.Diagnostics.Where(d => d.Severity == Severity.Error))
                {
                    var key = d.Message.Length > 90 ? d.Message.Substring(0, 90) : d.Message;
                    rejected[key] = rejected.TryGetValue(key, out var c) ? c + 1 : 1;
                }
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/fuzz-report.txt",
                new[] { "triggers " + triggers.Count + ", accepted " + accepted + ", apply state " + state, "", "rejections by message:" }
                    .Concat(rejected.OrderByDescending(kv => kv.Value).Select(kv => kv.Value.ToString().PadLeft(5) + "  " + kv.Key))
                    .Concat(new[] { "", "accepted:" }).Concat(lines));

            Assert.Greater(accepted, Triggers / 4, "enough random triggers are valid to make the check meaningful");
            Assert.AreNotEqual(TripwireCompiler.State.NeedsScripts, state, "accepted triggers' scripts compiled as C# (see Logs for error CS lines)");
            CollectionAssert.IsEmpty(notAttached, "every accepted trigger got its generated behaviour");
            Assert.IsFalse(UdonSharpProgramAsset.AnyUdonSharpScriptHasError(), "accepted triggers compile as UdonSharp (see the log for the Tripwire_ file)");
        }
    }
}
