using System.IO;
using Tripwire.Core;
using Tripwire.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tripwire.Tests
{
    /// <summary>
    /// Dev helper for the README's picture: one plain, applied trigger (a mirror switch everyone shares) in the
    /// Inspector, nothing staged around it. Prepare (batch mode) writes its script; Run shows it for a screenshot.
    /// TW_LANG=en for the English README. Driven by scripts/readme-screenshot.sh.
    /// </summary>
    public static class DevReadmeShot
    {
        static bool English => System.Environment.GetEnvironmentVariable("TW_LANG") == "en";

        static TripwireTrigger Build()
        {
            Texts.Language = English ? UiLanguage.English : UiLanguage.Japanese;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var world = new GameObject("VRCWorld");
            world.AddComponent<VRC.SDK3.Components.VRCSceneDescriptor>().spawns = new[] { world.transform };
            var mirror = new GameObject("Mirror");
            var button = GameObject.CreatePrimitive(PrimitiveType.Cube);
            button.name = English ? "Mirror Switch" : "鏡のスイッチ";
            var t = button.AddComponent<TripwireTrigger>();
            t.dataVersion = TripwireTrigger.CurrentDataVersion;
            t.comment = English ? "Click to show or hide the mirror, for everyone." : "クリックで鏡を出し入れする。全員で共有。";
            // The starter "a switch everyone shares", as a person would finish it.
            var name = English ? "mirrorOn" : "鏡が出ている";
            t.variables.Add(new KVariable { name = name, typeName = "System.Boolean", synced = true });
            var toggle = new KAction { actionId = ActionCatalog.ToggleVariableId, args = { new KArg { stringValue = name } } };
            t.events.Add(new KEvent { eventId = EventCatalog.InteractId, interactText = English ? "Mirror" : "鏡", actions = { toggle } });
            var show = new KAction { actionId = "GameObject.SetActive", args = { new KArg { source = KArgSource.Objects, objects = { mirror } }, new KArg { source = KArgSource.Variable, name = name } } };
            t.events.Add(new KEvent { eventId = EventCatalog.VariableChangedId, name = name, actions = { show } });
            return t;
        }

        /// <summary>Batch mode: writes the trigger's script, so Run finds it compiled and can show the trigger applied.</summary>
        public static void Prepare()
        {
            var t = Build();
            var state = TripwireCompiler.ApplyAll(new[] { t });
            File.WriteAllText("Logs/readme-source-" + (English ? "en" : "ja") + ".cs", TripwireCompiler.Generate(t).Source);
            Debug.Log("[README] prepared: " + state);
        }

        static ShotWindow window;
        static int frames;
        static double readyAt;

        public static void Run()
        {
            File.Delete("Logs/readme-ready");
            File.Delete("Logs/readme-done");
            var t = Build();
            var state = TripwireCompiler.ApplyAll(new[] { t });
            File.WriteAllText("Logs/readme-state.txt", state.ToString());
            Selection.activeGameObject = null;
            window = ScriptableObject.CreateInstance<ShotWindow>();
            window.trigger = t;
            window.titleContent = new GUIContent("Inspector");
            window.ShowUtility();
            window.position = new Rect(20, 40, 440, 900);
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            frames++;
            if (window != null) window.Repaint();
            // Real time, not frames: under Xvfb the window can take seconds to appear on screen.
            if (readyAt == 0) readyAt = EditorApplication.timeSinceStartup + 8;
            if (frames > 30 && EditorApplication.timeSinceStartup > readyAt && !File.Exists("Logs/readme-ready")) File.WriteAllText("Logs/readme-ready", "");
            if (File.Exists("Logs/readme-done")) { EditorApplication.update -= Tick; EditorApplication.Exit(0); }
        }
    }
}
