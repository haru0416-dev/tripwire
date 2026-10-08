using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tripwire.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tripwire.Tests
{
    /// <summary>
    /// Dev helper for scripts/drag-check.sh: shows a trigger Inspector, writes the screen rects of its grips and cards
    /// (Logs/drag-rects-N), and after each real mouse drag (done by the script with xdotool) writes the resulting order
    /// (Logs/drag-state-N). Usage: -executeMethod Tripwire.Tests.DevDragCheck.Run inside xvfb.
    /// </summary>
    public static class DevDragCheck
    {
        static ShotWindow window;
        static int frames, step;
        static bool waiting;

        public static void Run()
        {
            foreach (var f in Directory.GetFiles("Logs", "drag-*")) File.Delete(f);
            Tripwire.Core.Texts.Language = Tripwire.Core.UiLanguage.Japanese;
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var t = new GameObject("DragTrigger").AddComponent<TripwireTrigger>();
            // Actions of different kinds, so each grip has its own label.
            KEvent Event(string id, string name, bool open, params string[] actions)
            {
                var e = new KEvent { eventId = id, name = name, expanded = open };
                foreach (var a in actions) e.actions.Add(new KAction { actionId = a, args = { new KArg { source = KArgSource.Objects } } });
                t.events.Add(e);
                return e;
            }
            Event("Interact", "", true, "Text.SetText");
            Event("Start", "", false);
            Event("Custom", "Third", true, "AudioSource.Play", "GameObject.ToggleActive");
            TripwireTriggerEditor.ScreenRects = new Dictionary<string, Rect>();

            window = ScriptableObject.CreateInstance<ShotWindow>();
            window.trigger = t;
            window.titleContent = new GUIContent("Inspector");
            window.ShowUtility();
            window.position = new Rect(20, 40, 460, 1040);
            EditorApplication.update += Tick;
        }

        static string State(TripwireTrigger t) =>
            string.Join(" | ", t.events.Select(e => e.eventId + "[" + string.Join(",", e.actions.Select(a => a.actionId)) + "]"));

        static void Tick()
        {
            frames++;
            if (window != null) window.Repaint();
            if (waiting)
            {
                if (!File.Exists("Logs/drag-done-" + step)) return;
                waiting = false;
                File.WriteAllText("Logs/drag-state-" + step, State(window.trigger));
                step++;
                frames = 0;
                if (step == 5) { EditorApplication.update -= Tick; EditorApplication.Exit(0); }
                return;
            }
            if (frames >= 60)
            {
                File.WriteAllLines("Logs/drag-rects-" + step, TripwireTriggerEditor.ScreenRects.Select(kv =>
                    kv.Key + "\t" + (int)kv.Value.x + "\t" + (int)kv.Value.y + "\t" + (int)kv.Value.width + "\t" + (int)kv.Value.height));
                waiting = true;
            }
        }
    }
}
