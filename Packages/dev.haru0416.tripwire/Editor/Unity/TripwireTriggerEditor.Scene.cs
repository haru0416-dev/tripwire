using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector outside its cards: lines in the Scene view, which events ▶ Run can send in play mode.
    internal sealed partial class TripwireTriggerEditor
    {
        // ---------------- Scene view ----------------

        static readonly Color LineColor = new Color(0.35f, 0.75f, 1f, 0.9f);

        /// <summary>Dotted lines from the trigger to the objects its actions work on.</summary>
        void OnSceneGUI()
        {
            // With several triggers selected, the Scene view calls this once per trigger with `target` set to it.
            var t = target as TripwireTrigger;
            if (t == null || Event.current.type != EventType.Repaint) return;
            var from = t.transform.position;
            var targets = new HashSet<Object>();
            foreach (var e in t.events)
                foreach (var a in TripwireModel.FlatActions(e.actions))
                    foreach (var arg in a.args)
                        if (arg != null && arg.source == KArgSource.Objects)
                            foreach (var o in arg.objects) if (o != null) targets.Add(o);
            using (new Handles.DrawingScope(LineColor))
                foreach (var o in targets)
                {
                    var go = o as GameObject ?? (o as Component)?.gameObject;
                    if (go == null || go == t.gameObject || !go.scene.IsValid()) continue;
                    var to = go.transform.position;
                    Handles.DrawDottedLine(from, to, 4f);
                    Handles.Label(to, go.name, EditorStyles.miniLabel);
                }
        }

        // ---------------- Play mode ----------------

        public override bool RequiresConstantRepaint() => EditorApplication.isPlaying;

        /// <summary>
        /// The Udon event a button can send to run an event block, or null when the event brings values (a player, a
        /// collider...) a button can't supply.
        /// </summary>
        internal static string TryableEntry(KEvent e, EventSpec spec)
        {
            // Start also registers listeners and starts timers: running it again would do that twice.
            if (spec == null || spec.Params.Length > 0 || spec.Id == EventCatalog.StartId) return null;
            switch (spec.Shape)
            {
                case EventShape.Custom:
                    return string.IsNullOrEmpty(e.name) ? null : e.name;
                case EventShape.Override:
                case EventShape.UnityMessage:
                    return CodeGenerator.UdonExportName(spec.Method);
                default:
                    return null;
            }
        }
    }
}
