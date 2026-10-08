using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>
    /// Every trigger in the open scenes with its state (problems, not applied, ready), its note and what it does; click
    /// to select it. Tools > Tripwire > Trigger List, or "一覧" in a trigger's Inspector.
    /// </summary>
    internal sealed class TripwireOverviewWindow : EditorWindow
    {
        [MenuItem(TripwireMenus.List, false, 1001)]
        public static void Open()
        {
            GetWindow<TripwireOverviewWindow>().Show();
        }

        // Also when Unity reopens the window from a saved layout.
        void OnEnable() => titleContent = new GUIContent(Texts.T("Triggers", "トリガー一覧"), TripwireTriggerEditor.LogoMark);

        sealed class Row
        {
            public TripwireTrigger Trigger;
            public TripwireCompiler.State State;
            public int Errors, Warnings;
            public string Summary;
        }

        List<Row> rows = new List<Row>();
        double refreshedAt = -10;
        string search = "";
        Vector2 scroll;
        /// <summary>The rows drawn, chosen in the Layout pass so every pass of a frame draws the same rows.</summary>
        List<Row> shown = new List<Row>();

        void OnHierarchyChange() { refreshedAt = -10; lastStamp = 0; Repaint(); }
        void OnInspectorUpdate() => Repaint(); // states change as triggers are edited (refresh is throttled)
        void OnFocus() { refreshedAt = -10; lastStamp = 0; }
        long lastStamp;
        double fullAt;

        void Refresh()
        {
            // Generating every trigger is cheap but not free: at most once a second.
            if (EditorApplication.timeSinceStartup - refreshedAt < 1) return;
            refreshedAt = EditorApplication.timeSinceStartup;
            // Nothing edited since the last refresh (every change raises its object's dirty count): keep the rows.
            var triggers = TripwireCompiler.SceneTriggers().Where(t => t != null).ToList();
            long stamp = triggers.Count;
            foreach (var t in triggers)
                stamp = stamp * 31 + EditorUtility.GetDirtyCount(t) + t.GetInstanceID() + (t.generated != null ? t.generated.GetInstanceID() * 7L + EditorUtility.GetDirtyCount(t.generated) : 0);
            // Objects a trigger points at can be deleted without touching it: a full refresh every 10 s catches that.
            if (stamp == lastStamp && rows != null && EditorApplication.timeSinceStartup - fullAt < 10) return;
            fullAt = EditorApplication.timeSinceStartup;
            lastStamp = stamp;
            using (TripwireModel.Batch())
            rows = triggers.Select(t =>
            {
                var g = TripwireCompiler.Generate(t);
                var events = t.events.Select(e => EventCatalog.Get(e.eventId)).Where(s => s != null).Select(Texts.EventName).Distinct().Take(3);
                return new Row
                {
                    Trigger = t,
                    State = TripwireCompiler.GetState(t, g),
                    Errors = g.Diagnostics.Count(d => d.Severity == Severity.Error),
                    Warnings = g.Diagnostics.Count(d => d.Severity == Severity.Warning),
                    Summary = string.Join(Texts.T(", ", "、"), events) + (t.events.Count > 3 ? " …" : ""),
                };
            }).OrderBy(r => r.State == TripwireCompiler.State.HasErrors || r.State == TripwireCompiler.State.ApplyFailed || r.State == TripwireCompiler.State.Blocked ? 0 : 1).ThenBy(r => r.Trigger.name).ToList();
        }

        void OnGUI()
        {
            if (Event.current.type == EventType.Layout) Refresh();
            bool layout = Event.current.type == EventType.Layout;
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
                if (GUILayout.Button(Texts.T("Apply all", "すべて反映"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)) && !EditorApplication.isPlaying)
                    EditorApplication.delayCall += () => TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
            }
            if (layout) shown = rows.Where(r => r.Trigger != null && (search.Length == 0 || r.Trigger.name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0
                                                                || (r.Trigger.comment ?? "").IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (shown.Count == 0)
            {
                EditorGUILayout.HelpBox(Texts.T("No triggers in the open scenes. Add a Tripwire Trigger component to an object.",
                                                "開いているシーンにトリガーがありません。オブジェクトに Tripwire Trigger を付けてください。"), MessageType.Info);
                return;
            }
            // Vertical only: long summaries wrap instead of widening the list.
            // What the open scenes need besides triggers (once per scene, not on every trigger).
            foreach (var scene in shown.Select(r => r.Trigger.gameObject.scene).Distinct())
                if (TripwireSetup.MissingWorld(scene))
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox(scene.name + ": " + TripwireSetup.MissingWorldMessage, MessageType.Warning);
                        if (GUILayout.Button(Texts.T("Add VRCWorld", "VRCWorld を置く"), GUILayout.Width(110), GUILayout.Height(38))) TripwireSetup.AddWorld(scene);
                    }
            scroll = EditorGUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
            foreach (var r in shown)
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    GUILayout.Label(StateIcon(r), GUILayout.Width(22), GUILayout.Height(20));
                    using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(r.Trigger.name, EditorStyles.boldLabel, GUILayout.ExpandWidth(false)))
                            {
                                Selection.activeObject = r.Trigger.gameObject;
                                EditorGUIUtility.PingObject(r.Trigger.gameObject);
                            }
                            GUILayout.Label(StateText(r), EditorStyles.miniLabel);
                        }
                        var note = (r.Trigger.comment ?? "").Split('\n')[0];
                        GUILayout.Label(r.Summary + (note.Length > 0 ? "  —  " + note : ""), EditorStyles.wordWrappedMiniLabel);
                    }
                }
            EditorGUILayout.EndScrollView();
        }

        /// <summary>The state in words beside the name (the icon alone said it only on hover).</summary>
        static string StateText(Row r)
        {
            switch (r.State)
            {
                case TripwireCompiler.State.HasErrors: return Texts.T(r.Errors + " problem(s)", "問題 " + r.Errors + " 件");
                case TripwireCompiler.State.ApplyFailed: return Texts.T("couldn't apply", "反映できません");
                case TripwireCompiler.State.Blocked: return Texts.T("waiting for the project to compile", "プロジェクトのコンパイル待ち");
                case TripwireCompiler.State.UpToDate: return r.Warnings > 0 ? Texts.T("applied, " + r.Warnings + " warning(s)", "反映済み・注意 " + r.Warnings + " 件") : Texts.T("applied", "反映済み");
                default: return Texts.T("not applied yet", "まだ反映していません");
            }
        }

        static GUIContent StateIcon(Row r)
        {
            switch (r.State)
            {
                case TripwireCompiler.State.HasErrors:
                    return new GUIContent(EditorGUIUtility.IconContent("console.erroricon.sml").image, Texts.T(r.Errors + " problem(s)", r.Errors + " か所に問題があります"));
                case TripwireCompiler.State.UpToDate:
                    return r.Warnings > 0
                        ? new GUIContent(EditorGUIUtility.IconContent("console.warnicon.sml").image, Texts.T(r.Warnings + " warning(s)", "注意 " + r.Warnings + " 件"))
                        : new GUIContent(EditorGUIUtility.IconContent("TestPassed").image, Texts.T("Applied", "反映済み"));
                case TripwireCompiler.State.ApplyFailed:
                    return new GUIContent(EditorGUIUtility.IconContent("console.erroricon.sml").image, TripwireCompiler.FailureOf(r.Trigger));
                case TripwireCompiler.State.Blocked:
                    return new GUIContent(EditorGUIUtility.IconContent("console.erroricon.sml").image, TripwireCompiler.BlockedReason);
                default:
                    return new GUIContent(EditorGUIUtility.IconContent("console.infoicon.sml").image, Texts.T("Not applied yet (applied before Play and builds)", "まだ反映していません（Play とビルドの前に反映されます）"));
            }
        }
    }
}
