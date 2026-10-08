using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>
    /// What the triggers did during Play, newest first: each event that ran, and each one its conditions stopped (with
    /// the condition and the values). Click a row to select the object. Tools > Tripwire > Event History, or the
    /// button in a trigger's Inspector during Play.
    /// </summary>
    internal sealed class TripwireHistoryWindow : EditorWindow
    {
        Vector2 scroll;
        bool selectedOnly;
        string search = "";
        static GUIStyle ran, stopped, detail, time, row;

        [MenuItem(TripwireMenus.History, false, 1002)]
        public static void Open() => GetWindow<TripwireHistoryWindow>().Show();

        void OnEnable()
        {
            titleContent = new GUIContent(Texts.T("Event History", "実行の記録"), TripwireTriggerEditor.LogoMark);
            TripwireTrace.Changed += Repaint;
            Selection.selectionChanged += Repaint;
        }

        void OnDisable()
        {
            TripwireTrace.Changed -= Repaint;
            Selection.selectionChanged -= Repaint;
        }

        static void InitStyles()
        {
            if (row != null) return;
            row = new GUIStyle(EditorStyles.label) { richText = false, padding = new RectOffset(6, 6, 2, 2) };
            ran = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            ran.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.45f, 0.8f, 0.5f) : new Color(0.1f, 0.5f, 0.2f);
            stopped = new GUIStyle(EditorStyles.miniLabel) { fontStyle = FontStyle.Bold };
            stopped.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.95f, 0.65f, 0.3f) : new Color(0.7f, 0.35f, 0.0f);
            detail = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            time = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };
            time.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.4f, 0.4f, 0.4f);
        }

        void OnGUI()
        {
            InitStyles();
            titleContent.text = Texts.T("Event History", "実行の記録");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(Texts.T("Clear", "消す"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) TripwireTrace.Clear();
                selectedOnly = GUILayout.Toggle(selectedOnly, new GUIContent(Texts.T("Selected only", "選んだものだけ"), Texts.T("Only the triggers on the selected objects.", "選んでいるオブジェクトのトリガーだけを出します。")), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false));
                GUILayout.FlexibleSpace();
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.MaxWidth(220));
            }

            var list = TripwireTrace.Records.AsEnumerable().Reverse();
            if (selectedOnly) list = list.Where(r => r.Trigger != null && Selection.gameObjects.Contains(r.Trigger.gameObject));
            if (!string.IsNullOrEmpty(search))
                list = list.Where(r => (r.Path + " " + r.EventName + " " + r.Detail).IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0);
            var rows = list.ToList();

            if (TripwireTrace.Records.Count == 0)
            {
                EditorGUILayout.HelpBox(EditorApplication.isPlaying
                    ? Texts.T("Nothing has happened yet. Each event that runs, and each one its conditions stop, shows here.",
                              "まだ何も起きていません。動いたイベントと、条件で止まったイベントがここに出ます。")
                    : Texts.T("Press Play: each event that runs, and each one its conditions stop, shows here. Events that happen many times a second (every frame, while staying in an area) are left out.",
                              "Play を押すと、動いたイベントと、条件で止まったイベントがここに出ます。毎フレームなど 1 秒に何度も起きるイベントは記録しません。"), MessageType.Info);
                return;
            }
            if (TripwireTrace.Missed > 0)
                EditorGUILayout.HelpBox(Texts.T(TripwireTrace.Missed + " records were too many to read in time and are missing.",
                                                "記録が多すぎて、" + TripwireTrace.Missed + " 件を読みきれませんでした。"), MessageType.Warning);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var r in rows) DrawRow(r);
            EditorGUILayout.EndScrollView();
        }

        void DrawRow(TripwireTrace.Record r)
        {
            var rect = EditorGUILayout.BeginVertical(row);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(r.Time.ToString("0.00") + Texts.T("s", " 秒"), time, GUILayout.Width(64));
                GUILayout.Label(r.Ran ? Texts.T("Ran", "動いた") : Texts.T("Stopped", "止まった"), r.Ran ? ran : stopped, GUILayout.Width(Texts.Japanese ? 52 : 56));
                GUILayout.Label(new GUIContent(r.Path.Split('/').Last(), r.Path), EditorStyles.boldLabel, GUILayout.Width(150));
                GUILayout.Label(r.EventName, EditorStyles.label);
                GUILayout.FlexibleSpace();
            }
            if (!string.IsNullOrEmpty(r.Detail))
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(64 + (Texts.Japanese ? 52 : 56) + 6);
                    GUILayout.Label(r.Detail, detail);
                }
            EditorGUILayout.EndVertical();
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), new Color(0.5f, 0.5f, 0.5f, 0.2f));
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // During Play the object itself; afterwards the one at the same place in the scene.
                var go = r.Trigger != null ? r.Trigger.gameObject : GameObject.Find("/" + r.Path);
                if (go != null) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); }
                Event.current.Use();
            }
        }
    }
}
