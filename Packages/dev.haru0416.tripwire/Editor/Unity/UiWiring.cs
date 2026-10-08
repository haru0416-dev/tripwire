using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using VRC.Udon;

namespace Tripwire.Editor
{
    /// <summary>
    /// Connects UI events (Button.onClick, Toggle/Slider.onValueChanged) to a trigger's generated behaviour:
    /// a persistent call UdonBehaviour.SendCustomEvent("Tw_Ui{i}") — the one Udon call VRChat allows from UI.
    /// Tripwire owns only listeners of that exact shape (target = the trigger's behaviour, argument Tw_Ui*); others are left alone.
    /// </summary>
    internal static class UiWiring
    {
        const string Method = "SendCustomEvent";

        // The serialized field names are Unity's and not uniform (Toggle's has no "m_"): keep them exact.
        static (UnityEventBase evt, string property) EventAndProperty(Component ui)
        {
            switch (ui)
            {
                case Button b: return (b.onClick, "m_OnClick");
                case Toggle t: return (t.onValueChanged, "onValueChanged");
                case Slider s: return (s.onValueChanged, "m_OnValueChanged");
                default: return (null, null);
            }
        }

        static bool IsWired(Component ui) => EventAndProperty(ui).evt != null;

        public static Component UiOf(KEvent e, EventSpec spec)
        {
            if (spec == null || spec.Shape != EventShape.Ui || e.uiTarget == null) return null;
            return TripwireModel.Coerce(e.uiTarget, TripwireModel.ResolveType(spec.UiType)) as Component;
        }

        /// <summary>Make every UI event of the trigger call its generated method, and remove calls that no longer match.</summary>
        public static void Wire(TripwireTrigger t)
        {
            var ub = t.generated;
            if (ub == null) return;

            var wanted = new Dictionary<Component, HashSet<string>>();
            for (int i = 0; i < t.events.Count; i++)
            {
                var ui = UiOf(t.events[i], EventCatalog.Get(t.events[i].eventId));
                if (ui == null) continue;
                HashSet<string> set;
                if (!wanted.TryGetValue(ui, out set)) wanted[ui] = set = new HashSet<string>();
                set.Add(CodeGenerator.UiMethod(i));
            }

            // Every UI element in the trigger's scene (or the prefab being edited), so a re-pointed or deleted UI event
            // also loses its old call. Only elements that call this behaviour or should are opened for editing.
            foreach (var ui in Candidates(t.gameObject.scene).Concat(wanted.Keys).Distinct())
            {
                wanted.TryGetValue(ui, out var want);
                if (want == null && !Calls(ui, ub)) continue;
                Sync(ui, ub, want ?? new HashSet<string>());
            }
        }

        /// <summary>The scene's buttons, toggles and sliders; for the prefab being edited, its own (FindObjectsOfType doesn't see it).</summary>
        static Component[] Candidates(UnityEngine.SceneManagement.Scene scene) => TripwireModel.PerScene("ui", scene, () =>
        {
            var prefab = TripwireCompiler.EditedPrefabRoot();
            IEnumerable<Selectable> all = prefab != null && prefab.scene == scene
                ? prefab.GetComponentsInChildren<Selectable>(true)
                : Object.FindObjectsOfType<Selectable>(true).Where(c => c.gameObject.scene == scene);
            return all.Where(IsWired).Cast<Component>().ToArray();
        });

        /// <summary>Whether the UI element has a persistent call on the behaviour (a cheap look before opening it for editing).</summary>
        static bool Calls(Component ui, UdonBehaviour ub)
        {
            var evt = EventAndProperty(ui).evt;
            if (evt == null) return false;
            for (int i = 0; i < evt.GetPersistentEventCount(); i++) if (evt.GetPersistentTarget(i) == ub) return true;
            return false;
        }

        static void Sync(Component ui, UdonBehaviour ub, HashSet<string> want)
        {
            var (evt, prop) = EventAndProperty(ui);
            if (evt == null) return;

            var so = new SerializedObject(ui);
            var calls = so.FindProperty(prop + ".m_PersistentCalls.m_Calls");
            if (calls == null) return;
            var have = new List<(int index, string arg)>();
            for (int i = 0; i < calls.arraySize; i++)
            {
                var call = calls.GetArrayElementAtIndex(i);
                if (call.FindPropertyRelative("m_Target").objectReferenceValue != ub) continue;
                if (call.FindPropertyRelative("m_MethodName").stringValue != Method) continue;
                var arg = call.FindPropertyRelative("m_Arguments.m_StringArgument").stringValue;
                if (arg != null && arg.StartsWith(CodeGenerator.UiMethodPrefix)) have.Add((i, arg));
            }

            bool changed = false;
            // Keep the first call for each wanted method; remove duplicates and calls nothing wants any more.
            var keep = new HashSet<string>();
            var remove = new List<int>();
            foreach (var h in have)
            {
                if (want.Contains(h.arg) && keep.Add(h.arg)) continue;
                remove.Add(h.index);
            }
            foreach (var index in remove.OrderByDescending(x => x))
            {
                if (!changed) Undo.RecordObject(ui, "Wire Tripwire UI");
                changed = true;
                UnityEventTools.RemovePersistentListener(evt, index);
            }
            foreach (var m in want.Where(m => !keep.Contains(m)))
            {
                if (!changed) Undo.RecordObject(ui, "Wire Tripwire UI");
                changed = true;
                UnityEventTools.AddStringPersistentListener(evt, ub.SendCustomEvent, m);
            }
            if (changed)
            {
                EditorUtility.SetDirty(ui);
                if (PrefabUtility.IsPartOfPrefabInstance(ui)) PrefabUtility.RecordPrefabInstancePropertyModifications(ui);
            }
        }

        /// <summary>VRChat only lets players point at UI on a Canvas that has VRC Ui Shape.</summary>
        public static bool MissingUiShape(Component ui)
        {
            var canvas = ui != null ? ui.GetComponentInParent<Canvas>(true) : null;
            if (canvas == null) return true;
            return canvas.rootCanvas.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null
                   && canvas.GetComponent<VRC.SDK3.Components.VRCUiShape>() == null;
        }
    }
}
