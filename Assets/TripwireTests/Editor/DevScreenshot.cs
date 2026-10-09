using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tripwire.Tests
{
    // Dev helper: open the trigger Inspector in a window (non-batchmode editor under Xvfb) and step through states;
    // an outside script screenshots each state (handshake via Logs/shot-ready-N / Logs/shot-done-N).
    public static class DevScreenshot
    {
        static int step, frames;
        static ShotWindow window;

        public static void Run()
        {
            foreach (var f in Directory.GetFiles("Logs", "shot-*")) File.Delete(f);
            Texts.Language = UiLanguage.Japanese;
            // The action picker opens on "★": two starred entries and two recent picks (the developer's own are put back on exit).
            savedFavorites = EditorPrefs.GetString("Tripwire.Favorites.Actions", null);
            savedRecent = EditorPrefs.GetString("Tripwire.Recent.Actions", null);
            EditorPrefs.SetString("Tripwire.Favorites.Actions", "GameObject.ToggleActive\nText.SetText");
            EditorPrefs.SetString("Tripwire.Recent.Actions", "AudioSource.Play\nVideo.PlayUrl\nGameObject.ToggleActive");
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var mirror = new GameObject("Mirror");
            var chime = new GameObject("Chime").AddComponent<AudioSource>();
            var lightA = new GameObject("Light_A");
            var lightB = new GameObject("Light_B");
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = "Button";
            var t = button.AddComponent<TripwireTrigger>();

            var open = new KVariable { name = "扉が開いている", typeName = "System.Boolean", synced = true };
            t.variables.Add(open);
            var lights = new KVariable { name = "照明", typeName = "UnityEngine.GameObject[]" };
            lights.initial.source = KArgSource.Objects;
            lights.initial.objects.Add(lightA);
            lights.initial.objects.Add(lightB);
            t.variables.Add(lights);

            var click = new KEvent { eventId = "Interact", interactText = "鏡を切り替える" };
            click.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { mirror } } } });
            // A third-party-like U# script, used through "use another script".
            var counterType = TripwireModel.ResolveType("TripwireTestCounter");
            var counter = new GameObject("ScoreBoard");
            if (counterType != null && UdonSharpEditor.UdonSharpEditorUtility.GetUdonSharpProgramAsset(counterType) != null)
            {
                UdonSharpEditor.UdonSharpUndo.AddComponent(counter, counterType);
                var add = UdonSharpApi.Members(counterType).First(c => c.Member == "Add");
                click.actions.Add(new KAction
                {
                    actionId = ActionCatalog.ScriptCallId,
                    method = add.UdonName,
                    args = { new KArg { source = KArgSource.Objects, objects = { counter } }, new KArg { source = KArgSource.Constant, intValue = 1 } },
                });
            }
            // "Notified by another script", ProTV-style registration.
            var notifierType = TripwireModel.ResolveType("TripwireTestNotifier");
            if (notifierType != null && UdonSharpEditor.UdonSharpEditorUtility.GetUdonSharpProgramAsset(notifierType) != null)
            {
                var tv = new GameObject("TV");
                UdonSharpEditor.UdonSharpUndo.AddComponent(tv, notifierType);
                var notified = new KEvent { eventId = "ScriptNotified", name = "_TvPlay", listenTarget = tv,
                    listenMethod = UdonSharpApi.Registrations(notifierType).First(r => r.Method == "_RegisterListener").Id };
                notified.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { lightA } } } });
                t.events.Add(notified);
            }
            // A real asset with a preset (when scripts/fetch-test-assets.sh installed it).
            var protvPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/dev.architech.protv/Simple (ProTV).prefab");
            var tvType = TripwireModel.ResolveType("ArchiTech.ProTV.TVManager");
            if (protvPrefab != null && tvType != null)
            {
                var protv = (GameObject)PrefabUtility.InstantiatePrefab(protvPrefab);
                var tvScript = protv.GetComponentsInChildren<UdonSharp.UdonSharpBehaviour>(true).First(b => tvType.IsAssignableFrom(b.GetType()));
                var tvEvent = new KEvent { eventId = "ScriptNotified", listenTarget = tvScript.gameObject };
                PresetResolver.Apply(tvEvent, tvScript.GetType(), PresetResolver.Notifications(tvScript.GetType()).First(n => n.Callback == "_TvMediaEnd"));
                tvEvent.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { lightB } } } });
                var volume = PresetResolver.Operations(tvScript.GetType()).First(o => o.En.StartsWith("Set volume"));
                tvEvent.actions.AddRange(PresetResolver.Actions(tvScript.GetType(), volume, new List<Object> { tvScript.gameObject }));
                t.events.Insert(0, tvEvent);
            }
            // A text with nothing entered yet: shows how to insert a value.
            var label = new GameObject("ScoreLabel", TripwireModel.ResolveType("TMPro.TextMeshPro"));
            var showScore = new KEvent { eventId = "OnVariableChanged", name = "扉が開いている" };
            showScore.actions.Add(new KAction { actionId = "Text.SetText", args = { new KArg { source = KArgSource.Objects, objects = { label.GetComponent(TripwireModel.ResolveType("TMPro.TextMeshPro")) } }, new KArg() } });
            t.events.Add(click);
            var enter = new KEvent { eventId = "OnPlayerTriggerEnter", playerFilter = KPlayerFilter.LocalPlayer };
            enter.actions.Add(new KAction { actionId = "AudioSource.Play", args = { new KArg { source = KArgSource.Objects, objects = { chime } } } });
            t.events.Add(enter);
            var door = new KEvent { eventId = "Interact" };
            door.actions.Add(new KAction { actionId = "Variable.Toggle", args = { new KArg { stringValue = "扉が開いている" } } });
            door.conditions.Add(new KCondition { variable = "扉が開いている", op = KCompareOp.Equal, value = new KArg { boolValue = false } });
            t.events.Add(door);

            // An If block: with the variable on, toggle the mirror; otherwise play the chime and show a sign.
            var ifBlock = new KAction { actionId = ActionCatalog.IfId };
            ifBlock.conditions.Add(new KCondition { variable = "扉が開いている", op = KCompareOp.Equal, value = new KArg { boolValue = true } });
            ifBlock.thenActions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { mirror } } } });
            ifBlock.elseActions.Add(new KAction { actionId = "AudioSource.Play", args = { new KArg { source = KArgSource.Objects, objects = { chime } } } });
            var branch = new KEvent { eventId = "Interact" };
            branch.actions.Add(ifBlock);
            t.events.Insert(0, branch);
            t.events.Insert(0, showScore);
            // Folded cards (one-line summary) and cards with problems (frame + count).
            foreach (var ev in t.events.Where(x => x.eventId == "Interact" || x.eventId == "ScriptNotified")) ev.expanded = false;
            var broken = new KEvent { eventId = "Custom", name = "", expanded = false };
            broken.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects } } });
            t.events.Insert(1, broken);
            t.comment = "入口のボタン。鏡と扉をまとめて操作する。";
            showScore.comment = "扉の状態を看板に出す";
            showScore.actions[0].comment = "看板は ScoreLabel";
            var empty = new KEvent { eventId = "OnPlayerJoined", expanded = true };
            empty.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects } } });
            t.events.Insert(2, empty);
            // A timer with a random interval, first.
            var timer = new KEvent { eventId = "Timer", name = "まばたき", timerMin = 3f, timerMax = 6f };
            timer.actions.Add(new KAction { actionId = "GameObject.ToggleActive", args = { new KArg { source = KArgSource.Objects, objects = { mirror } } } });
            t.events.Insert(0, timer);
            var stop = new KAction { actionId = "Timer.Stop", args = { new KArg { stringValue = "まばたき" } } };
            t.events.First(x => x.actions.Any(y => y.actionId == ActionCatalog.IfId)).actions.First().elseActions.Add(stop);
            // A loop inside the timer: show every object of a list.
            var each = new KAction { actionId = ActionCatalog.ForEachId, args = { new KArg { stringValue = "看板たち" }, new KArg { stringValue = "看板" }, new KArg { stringValue = "" } } };
            each.thenActions.Add(new KAction { actionId = "GameObject.SetActive", args = { new KArg { source = KArgSource.Variable, name = "看板" }, new KArg { boolValue = true } } });
            t.variables.Add(new KVariable { name = "看板たち", typeName = "UnityEngine.GameObject[]" });
            t.variables.Add(new KVariable { name = "看板", typeName = "UnityEngine.GameObject" });
            t.variables.Add(new KVariable { name = "回数", typeName = "System.Int32", temporary = true });
            timer.actions.Add(each);
            // The If block card first and open.
            var branchCard = t.events.First(x => x.actions.Any(y => y.actionId == ActionCatalog.IfId));
            t.events.Remove(branchCard);
            branchCard.expanded = true;
            t.events.Insert(1, branchCard);
            // TW_COST=1: an every-frame card that delays and changes a synced variable (warnings from the cost survey), first.
            if (System.Environment.GetEnvironmentVariable("TW_COST") == "1")
            {
                t.variables.Add(new KVariable { name = "点数", typeName = "System.Int32", synced = true });
                var frame = new KEvent { eventId = "Update", delaySeconds = 0.5f };
                frame.actions.Add(new KAction { actionId = "Variable.Add", args = { new KArg { stringValue = "点数" }, new KArg { intValue = 1 } } });
                t.events.Insert(0, frame);
            }
            // TW_LOOP=1: two cards that run each other forever (a variable's change event flipping it, an event calling itself
            // through the trigger's own object), first.
            if (System.Environment.GetEnvironmentVariable("TW_LOOP") == "1")
            {
                var ping = new KEvent { eventId = "Custom", name = "Ping" };
                ping.actions.Add(new KAction { actionId = "Event.Send", args = { new KArg { source = KArgSource.Objects, objects = { t.gameObject } }, new KArg { stringValue = "Ping" }, new KArg() } });
                t.events.Insert(0, ping);
                var flip = new KEvent { eventId = "OnVariableChanged", name = "扉が開いている" };
                flip.actions.Add(new KAction { actionId = "Variable.Toggle", args = { new KArg { stringValue = "扉が開いている" } } });
                t.events.Insert(0, flip);
            }
            // TW_API=1: the wider Udon API (an out parameter, a callback to this trigger, an enum as a number, part of a
            // position) and the Calculate / Get Component actions.
            if (System.Environment.GetEnvironmentVariable("TW_API") == "1")
            {
                t.events.Clear(); t.variables.Clear();
                foreach (var (n, type) in new[] { ("文字", "System.String"), ("数", "System.Int32"), ("読めた", "System.Boolean"), ("位置", "UnityEngine.Vector3"), ("手", "System.Int32"), ("体", "UnityEngine.Rigidbody") })
                    t.variables.Add(new KVariable { name = n, typeName = type });
                KAction Call(CallSpec c, string result, params KArg[] args)
                {
                    var a = new KAction { actionId = ActionCatalog.CallId, method = c.UdonName, resultVariable = result ?? "" };
                    a.args.AddRange(args);
                    return a;
                }
                var parse = UdonApi.All.First(c => c.DeclaringType == "System.Int32" && c.Member == "TryParse" && c.Params.Count == 2);
                var hand = UdonApi.All.First(c => c.DeclaringType == "VRC.SDK3.Components.VRCPickup" && c.Member == "currentHand" && c.Kind == CallKind.Get);
                var setY = UdonApi.All.First(c => c.DeclaringType == "UnityEngine.Vector3" && c.Member == "y" && c.Kind == CallKind.Set);
                var load = UdonApi.All.First(c => c.DeclaringType == "VRC.SDK3.StringLoading.VRCStringDownloader" && c.Member == "LoadUrl");
                var e = new KEvent { eventId = "Interact", interactText = "Try", expanded = true };
                e.actions.Add(Call(parse, "読めた", new KArg { source = KArgSource.Variable, name = "文字" }, new KArg { source = KArgSource.Variable, name = "数" }));
                e.actions.Add(new KAction { actionId = ActionCatalog.CalculateId, args = { new KArg { stringValue = "数" }, new KArg { source = KArgSource.Variable, name = "数" }, new KArg { intValue = 2 }, new KArg { intValue = 3 } } });
                e.actions.Add(Call(setY, null, new KArg { source = KArgSource.Variable, name = "位置" }, new KArg { floatValue = 1.5f }));
                e.actions.Add(Call(hand, "手", new KArg { source = KArgSource.Self }));
                e.actions.Add(new KAction { actionId = ActionCatalog.GetComponentId, args = { new KArg { stringValue = "体" }, new KArg { source = KArgSource.Self }, new KArg { intValue = 1 } } });
                e.actions.Add(Call(load, null, new KArg(), new KArg { source = KArgSource.Self }));
                t.events.Add(e);
            }
            // TW_EMPTY=1: a freshly added trigger (the empty state).
            if (System.Environment.GetEnvironmentVariable("TW_EMPTY") == "1") { t.events.Clear(); t.variables.Clear(); t.comment = ""; }
            // TW_PERF=1: a trigger eight times as large, and the time per Inspector pass written to Logs/inspector-perf.txt.
            if (System.Environment.GetEnvironmentVariable("TW_PERF") == "1")
            {
                var events = t.events.ToList();
                for (int n = 0; n < 7; n++) t.events.AddRange(events.Select(TripwireTriggerEditor.CloneOf<KEvent>));
                foreach (var e in t.events) e.expanded = true;
            }
            window = ScriptableObject.CreateInstance<ShotWindow>();
            window.trigger = t;
            window.titleContent = new GUIContent("Inspector");
            window.ShowUtility();
            // TW_WIDTH=n: a narrower or wider Inspector (default 460).
            window.position = new Rect(20, 40, int.TryParse(System.Environment.GetEnvironmentVariable("TW_WIDTH"), out var width) ? width : 460, 1000);
            EditorApplication.update += Tick;
        }

        static bool waiting;
        static string savedFavorites, savedRecent;

        static void RestorePrefs()
        {
            void Put(string key, string value) { if (value == null) EditorPrefs.DeleteKey(key); else EditorPrefs.SetString(key, value); }
            Put("Tripwire.Favorites.Actions", savedFavorites);
            Put("Tripwire.Recent.Actions", savedRecent);
        }

        static void Tick()
        {
            frames++;
            if (window != null) window.Repaint();
            if (waiting)
            {
                if (File.Exists("Logs/shot-done-" + step)) { waiting = false; step++; frames = 0; }
                return;
            }
            if (frames == 1)
            {
                if (step == 1 && window.trigger.events.Count > 0) window.editor.OpenDetails(window.trigger.events[window.trigger.events.Count - 1]);
                if (step == 2) window.openPicker = true;
                if (step == 3)
                {
                    // The trigger list window.
                    window.Close();
                    TripwireOverviewWindow.Open();
                    EditorWindow.GetWindow<TripwireOverviewWindow>().position = new Rect(20, 40, 460, 400);
                }
                if (step == 4) { RestorePrefs(); EditorApplication.update -= Tick; EditorApplication.Exit(0); return; }
            }
            if (frames >= 90)
            {
                File.WriteAllText("Logs/shot-ready-" + step, window.position.ToString());
                waiting = true;
            }
        }
    }

    public class ShotWindow : EditorWindow
    {
        public TripwireTrigger trigger;
        internal TripwireTriggerEditor editor;
        public bool openPicker;
        Vector2 scroll;
        readonly System.Collections.Generic.List<double> layoutMs = new System.Collections.Generic.List<double>(), repaintMs = new System.Collections.Generic.List<double>();
        bool dumped;

        // TW_MINW=n: the layout entries whose minimum width is over n px (Logs/minwidth.txt), to find what makes a narrow
        // Inspector scroll sideways. The scroll group's own line is the narrowest the whole Inspector can get.
        static void DumpWide(int over)
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            var util = typeof(GUILayoutUtility);
            var current = util.GetField("current", flags).GetValue(null);
            var top = current.GetType().GetField("topLevel", flags).GetValue(current);
            var lines = new List<string>();
            void Walk(object entry, int depth)
            {
                var t = entry.GetType();
                float min = (float)t.GetField("minWidth", flags).GetValue(entry);
                var style = (GUIStyle)t.GetProperty("style", flags).GetValue(entry);
                if (min > over) lines.Add(new string(' ', depth * 2) + t.Name + " min=" + min + " style=" + style?.name + " rect=" + t.GetField("rect", flags).GetValue(entry));
                var entries = t.GetField("entries", flags)?.GetValue(entry) as System.Collections.IList;
                if (entries != null) foreach (var c in entries) Walk(c, depth + 1);
            }
            Walk(top, 0);
            File.WriteAllLines("Logs/minwidth.txt", lines);
        }

        static double Median(System.Collections.Generic.List<double> xs) { var o = xs.OrderBy(x => x).ToList(); return o.Count == 0 ? 0 : o[o.Count / 2]; }

        void OnGUI()
        {
            if (trigger == null) return;
            if (editor == null) editor = (TripwireTriggerEditor)UnityEditor.Editor.CreateEditor(trigger);
            // TW_SCROLL=n: look further down the Inspector.
            if (scroll == Vector2.zero && int.TryParse(System.Environment.GetEnvironmentVariable("TW_SCROLL"), out var down)) scroll.y = down;
            // The Inspector window draws in hierarchy mode (foldout arrows sit further left): draw the same way.
            EditorGUIUtility.hierarchyMode = true;
            scroll = EditorGUILayout.BeginScrollView(scroll);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var kind = Event.current.type;
            using (new EditorGUILayout.VerticalScope(EditorStyles.inspectorDefaultMargins)) // the Inspector's own margins
                editor.OnInspectorGUI();
            if (kind == EventType.Repaint && !dumped && int.TryParse(System.Environment.GetEnvironmentVariable("TW_MINW"), out var over)) { dumped = true; DumpWide(over); }
            if (kind == EventType.Layout || kind == EventType.Repaint)
            {
                (kind == EventType.Layout ? layoutMs : repaintMs).Add(watch.Elapsed.TotalMilliseconds);
                if (layoutMs.Count == 60 && kind == EventType.Layout)
                {
                    // The parts: generating the program, the state check, variable type lookups.
                    double Time(System.Action f) { var w = System.Diagnostics.Stopwatch.StartNew(); for (int i = 0; i < 20; i++) f(); return w.Elapsed.TotalMilliseconds / 20; }
                    var g = TripwireCompiler.Generate(trigger);
                    File.WriteAllText("Logs/inspector-perf.txt", "events " + trigger.events.Count + "\nlayout ms (median of 60): " + Median(layoutMs).ToString("F2") + "\nrepaint ms (median): " + Median(repaintMs).ToString("F2")
                        + "\nGenerate ms: " + Time(() => TripwireCompiler.Generate(trigger)).ToString("F2")
                        + "\n  ToProgram ms: " + Time(() => TripwireModel.ToProgram(trigger)).ToString("F2")
                        + "\n  CodeGenerator.Generate ms: " + Time(() => CodeGenerator.Generate(TripwireModel.ToProgram(trigger))).ToString("F2")
                        + "\n  AddSceneWarnings ms: " + Time(() => TripwireLoops.AddSceneWarnings(trigger, g)).ToString("F2")
                        + "\nGetState ms: " + Time(() => TripwireCompiler.GetState(trigger, g)).ToString("F2")
                        + "\nVariableType x all vars ms: " + Time(() => { foreach (var v in trigger.variables) TripwireModel.VariableType(v); }).ToString("F3") + "\n");
                }
            }
            EditorGUILayout.EndScrollView();
            if (openPicker && Event.current.type == EventType.Repaint)
            {
                openPicker = false;
                var r = new Rect(20, 120, 420, 20);
                PopupWindow.Show(r, new CatalogPicker(CatalogPicker.Actions(), Texts.ActionCategories, _ => { }));
            }
        }
    }
}
