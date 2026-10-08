using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;

namespace Tripwire.Editor
{
    /// <summary>
    /// The trigger Inspector. Layout: status, variables, then one card per event: "when" → "what to do", with the
    /// rarely needed settings (who runs it, delay, conditions) folded under "Advanced settings" behind a one-line summary.
    /// </summary>
    [CustomEditor(typeof(TripwireTrigger))]
    internal sealed partial class TripwireTriggerEditor : UnityEditor.Editor
    {
        TripwireTrigger t;
        GeneratedProgram generated;
        bool? variablesExpanded;
        readonly HashSet<KEvent> detailsOpen = new HashSet<KEvent>();

        static GUIStyle cardStyle, slimCardStyle, innerCardStyle, smallLabel, captionLabel, boldLabel, eventButton;

        static string T(string en, string ja) => Texts.T(en, ja);

        void OnEnable()
        {
            t = (TripwireTrigger)target;
            TripwireMigration.Upgrade(t);
            TripwireFolds.EnsureIds(t);
            TripwireTrace.Changed += Repaint; // the cards' "last ran" during Play
        }

        void OnDisable() => TripwireTrace.Changed -= Repaint;

        /// <summary>For the screenshot harness: show an event's advanced settings.</summary>
        internal void OpenDetails(KEvent e) => detailsOpen.Add(e);

        static void InitStyles()
        {
            if (cardStyle != null) return;
            cardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 8, 6, 8), margin = new RectOffset(0, 0, 2, 6) };
            slimCardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(6, 6, 3, 3), margin = new RectOffset(0, 0, 1, 2) };
            innerCardStyle = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(8, 6, 5, 6), margin = new RectOffset(16, 0, 2, 4) };
            smallLabel = new GUIStyle(EditorStyles.miniLabel);
            smallLabel.normal.textColor = Muted(0.6f, 0.34f);
            captionLabel = new GUIStyle(EditorStyles.miniLabel) { wordWrap = true };
            captionLabel.normal.textColor = Muted(0.58f, 0.36f);
            boldLabel = new GUIStyle(EditorStyles.boldLabel);
            eventButton = new GUIStyle(EditorStyles.popup) { fontStyle = FontStyle.Bold, fixedHeight = RowHeight, margin = new RectOffset(4, 4, 0, 0) };
            InitRowStyles();
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            if (Event.current.type == EventType.DragUpdated) dropLine = null; // set again by the card under the mouse, if any
            Undo.RecordObject(t, T("Edit Tripwire Trigger", "Tripwire を編集"));
            // Every pass, Repaint included: drawing may fill in defaults during Layout (about 1 ms with 70 events).
            // In a batch: scene-wide lookups (which variables other triggers set) once per pass, not once per variable.
            using (TripwireModel.Batch()) generated = TripwireCompiler.Generate(t);

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 104;
            DrawHeader();
            if (TripwireMigration.IsNewer(t))
            {
                // Shown, not editable: saving it from this version would drop what it doesn't know.
                EditorGUILayout.HelpBox(TripwireMigration.NewerMessage, MessageType.Error);
                using (new EditorGUI.DisabledScope(true)) { DrawVariables(); DrawEvents(); }
                EditorGUIUtility.labelWidth = prevLabelWidth;
                return;
            }
            if (EditorApplication.isPlaying)
            {
                // Play mode: live values and event buttons; editing waits until Play stops (it would be lost).
                DrawPlaying();
                using (new EditorGUI.DisabledScope(true))
                {
                    DrawVariables();
                    EditorGUILayout.Space(4);
                    DrawEvents();
                }
                EditorGUIUtility.labelWidth = prevLabelWidth;
                return;
            }
            DrawStatus();
            HandleSelectionKeys();
            EditorGUI.BeginChangeCheck();
            DrawVariables();
            EditorGUILayout.Space(4);
            DrawEvents();
            if (EditorGUI.EndChangeCheck()) Changed();
            DrawDropLine();
            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        void Changed() => Changed(t);

        /// <summary>Marks a trigger edited (also another one, e.g. the source of a dragged card) and re-applies it.</summary>
        static void Changed(TripwireTrigger trigger)
        {
            TripwireMigration.MarkCurrent(trigger);
            TripwireFolds.EnsureIds(trigger); // new cards
            EditorUtility.SetDirty(trigger);
            if (PrefabUtility.IsPartOfPrefabInstance(trigger)) PrefabUtility.RecordPrefabInstancePropertyModifications(trigger);
            ScheduleQuickApply(trigger);
        }

        /// <summary>Edits made from menus and popups (outside OnInspectorGUI's change check), named in the Undo history.</summary>
        void Edit(Action change, string undoName = null)
        {
            Undo.RecordObject(t, undoName ?? T("Edit Tripwire Trigger", "Tripwire を編集"));
            if (undoName != null) Undo.SetCurrentGroupName(undoName);
            change();
            Changed();
            Repaint();
        }

        // When the program's script already exists, attaching/binding is instant: do it right away.
        static void ScheduleQuickApply(TripwireTrigger trigger)
        {
            // The apply joins the edit's undo step: one Ctrl+Z takes back the edit, not just the apply.
            int group = Undo.GetCurrentGroup();
            EditorApplication.delayCall += () =>
            {
                if (trigger == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
                var g = TripwireCompiler.Generate(trigger);
                if (!g.HasErrors && TripwireCompiler.FindGeneratedType(g.ClassName) != null)
                {
                    TripwireCompiler.ApplyAll(new List<TripwireTrigger> { trigger });
                    Undo.CollapseUndoOperations(group);
                }
            };
        }

        // ---------------- status ----------------

        /// <summary>Opens the note on the whole trigger.</summary>
        void NoteButton()
        {
            if (string.IsNullOrEmpty(t.comment) && GUILayout.Button(new GUIContent(T("Note", "メモ"), T("Add a note to this trigger.", "このトリガーにメモを付けます。")), GUILayout.Width(44), GUILayout.Height(statusHeight)))
                editingComment.Add(t);
        }

        float statusHeight = 36;

        /// <summary>
        /// The state message. It and the buttons beside it (<paramref name="buttons"/> wide in all) share one height: the
        /// buttons' 36, or more when the message wraps.
        /// </summary>
        void StatusBox(string message, MessageType type, float buttons)
        {
            var icon = type == MessageType.Error ? "console.erroricon" : type == MessageType.Info ? "console.infoicon" : null;
            var content = icon != null ? new GUIContent(message, EditorGUIUtility.IconContent(icon).image) : new GUIContent(message);
            float width = EditorGUIUtility.currentViewWidth - buttons - 40; // the Inspector's margins and the scrollbar
            if (Event.current.type == EventType.Layout) statusHeight = Mathf.Max(36, EditorStyles.helpBox.CalcHeight(content, width));
            // MinWidth: Japanese has no spaces, so Unity would take the whole message as the narrowest it can wrap to.
            GUILayout.Label(content, EditorStyles.helpBox, GUILayout.Height(statusHeight), GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
        }

        void DrawStatus()
        {
            var state = TripwireCompiler.GetState(t, generated);
            using (new EditorGUILayout.HorizontalScope())
            {
                switch (state)
                {
                    case TripwireCompiler.State.UpToDate:
                        StatusBox(T("Applied. Play runs exactly this.", "反映済み。Play するとこのとおりに動きます。"), MessageType.Info, 52 + 44);
                        CodeButton();
                        NoteButton();
                        break;
                    case TripwireCompiler.State.HasErrors:
                        int n = generated.Diagnostics.Count(d => d.Severity == Severity.Error);
                        // A no-break space keeps the number with its word when the message wraps.
                        // The first problem itself, so the banner says what is wrong even when its card is out of view.
                        var first = generated.Diagnostics.First(d => d.Severity == Severity.Error);
                        var firstText = Sentences(first.Message).FirstOrDefault() ?? first.Message;
                        StatusBox(n == 1 ? firstText : T(n + "\u00A0problems. First: ", n + "\u00A0か所に問題があります。最初の問題: ") + firstText, MessageType.Error, 84 + 44);
                        if (GUILayout.Button(new GUIContent(T("Show them", "問題の\nカードを開く"), T("Open the cards with problems and fold the others.", "問題のあるカードだけを開き、ほかはたたみます。")), GUILayout.Width(84), GUILayout.Height(statusHeight)))
                        {
                            for (int i = 0; i < t.events.Count; i++)
                            {
                                int ei = i;
                                TripwireFolds.SetExpanded(t.events[i], generated.Diagnostics.Any(d => d.Event == ei));
                            }
                            // The variables fold too unless a problem is there, so the problem cards come up near the top.
                            variablesExpanded = generated.Diagnostics.Any(d => d.Variable >= 0 && d.Severity == Severity.Error);
                        }
                        NoteButton();
                        break;
                    case TripwireCompiler.State.Blocked:
                        StatusBox(TripwireCompiler.BlockedReason ?? "", MessageType.Error, 80 + 44);
                        if (GUILayout.Button(T("Console", "Console\nを開く"), GUILayout.Width(80), GUILayout.Height(statusHeight)))
                            EditorApplication.ExecuteMenuItem("Window/General/Console");
                        NoteButton();
                        break;
                    case TripwireCompiler.State.ApplyFailed:
                        StatusBox(TripwireCompiler.FailureOf(t) ?? "", MessageType.Error, 80 + 44);
                        if (GUILayout.Button(T("Try again", "もう一度\n反映"), GUILayout.Width(80), GUILayout.Height(statusHeight)))
                            EditorApplication.delayCall += () => TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
                        NoteButton();
                        break;
                    case TripwireCompiler.State.NeedsScripts when TripwireCompiler.ResumePending || EditorApplication.isCompiling:
                        // A new program shape: Unity compiles the script before it can be attached; nothing to press.
                        StatusBox(TripwireCompiler.PlayPending
                            ? T("Tripwire made a new script. When Unity has compiled it, applying continues and Play starts by itself.", "Tripwire がスクリプトを作りました。Unity のコンパイルが終わると、反映が続き、Play も自動で始まります。")
                            : T("Tripwire made a new script. When Unity has compiled it, applying continues by itself.", "Tripwire がスクリプトを作りました。Unity のコンパイルが終わると、自動で反映が続きます。"), MessageType.Info, 44);
                        NoteButton();
                        break;
                    default:
                        StatusBox(T("Not applied yet. It is applied automatically before Play and builds.", "まだ反映していません。Play\u00A0やビルドの前に自動で反映されます。"), MessageType.None, 80 + 44);
                        using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
                            if (GUILayout.Button(T("Apply now", "今すぐ反映"), GUILayout.Width(80), GUILayout.Height(statusHeight)))
                                EditorApplication.delayCall += () => TripwireCompiler.ApplyAll(TripwireCompiler.SceneTriggers());
                        NoteButton();
                        break;
                }
            }
            var note = CommentField(t, t.comment);
            if (note != t.comment) { t.comment = note; Changed(); }
            DrawDiagnostics(d => d.Event < 0 && d.Variable < 0);

            // Play-mode testing (ClientSim) and uploading need the scene's VRCWorld.
            if (TripwireSetup.MissingWorld(t.gameObject.scene))
                Fix(TripwireSetup.MissingWorldMessage, T("Add VRCWorld", "VRCWorld を置く"), () => TripwireSetup.AddWorld(t.gameObject.scene));

            // Cards and actions this version doesn't know (made with a newer one, or from a removed add-on): one button
            // removes them all, instead of deleting each by hand before the trigger can be applied again.
            int unknown = t.events.Count(e => EventCatalog.Get(e.eventId) == null)
                        + t.events.Sum(e => TripwireModel.FlatActions(e.actions).Count(a => ActionCatalog.Get(a.actionId) == null));
            if (unknown > 0)
                Fix(T(unknown + " card(s) or action(s) are unknown to this version of Tripwire. Update Tripwire to keep them, or remove them.",
                      "この版の Tripwire が知らないカードやアクションが " + unknown + " 個あります。残すなら Tripwire を更新し、要らなければ外してください。"),
                    T("Remove them", "外す"), () =>
                    {
                        Undo.RecordObject(t, T("Remove them", "外す"));
                        t.events.RemoveAll(e => EventCatalog.Get(e.eventId) == null);
                        foreach (var e in t.events) RemoveUnknown(e.actions);
                    });
        }

        static void RemoveUnknown(List<KAction> actions)
        {
            actions.RemoveAll(a => a == null || ActionCatalog.Get(a.actionId) == null);
            foreach (var a in actions) { RemoveUnknown(a.thenActions); RemoveUnknown(a.elseActions); }
        }

        void DrawDiagnostics(Func<Diagnostic, bool> where)
        {
            foreach (var d in generated.Diagnostics.Where(where)) DiagnosticBox(d.Message, d.Severity == Severity.Error ? MessageType.Error : MessageType.Warning);
        }

        static readonly HashSet<string> openMessages = new HashSet<string>();

        /// <summary>
        /// A message of three or more sentences shows the first (what is wrong) and the last (what to do); the reason in
        /// between opens on click. Stacked warnings stay readable.
        /// </summary>
        void DiagnosticBox(string message, MessageType type)
        {
            var sentences = Sentences(message);
            if (sentences.Count < 3) { EditorGUILayout.HelpBox(message, type); return; }
            bool open = openMessages.Contains(message);
            EditorGUILayout.HelpBox(open ? message + "  ▴" : sentences[0] + " " + sentences[sentences.Count - 1] + T("  ▸ why", "  ▸ 理由"), type);
            var r = GUILayoutUtility.GetLastRect();
            EditorGUIUtility.AddCursorRect(r, MouseCursor.Link);
            if (Event.current.type == EventType.MouseDown && r.Contains(Event.current.mousePosition))
            {
                if (!openMessages.Add(message)) openMessages.Remove(message);
                Event.current.Use();
                Repaint();
            }
        }

        /// <summary>Sentences of a message, Japanese (。) or English (". ").</summary>
        static List<string> Sentences(string message)
        {
            var list = new List<string>();
            int start = 0;
            for (int i = 0; i < message.Length; i++)
            {
                bool end = message[i] == '。' || (message[i] == '.' && (i + 1 == message.Length || message[i + 1] == ' ') && i > 0 && !char.IsDigit(message[i - 1]));
                if (!end) continue;
                list.Add(message.Substring(start, i + 1 - start).Trim());
                start = i + 1;
            }
            if (start < message.Length && message.Substring(start).Trim().Length > 0) list.Add(message.Substring(start).Trim());
            return list;
        }

        bool HasDiagnostics(Func<Diagnostic, bool> where) => generated.Diagnostics.Any(where);

        /// <summary>Opens the generated UdonSharp: plain code people can read (and learn from).</summary>
        void CodeButton()
        {
            var path = TripwireCompiler.OutputDir + "/" + generated.ClassName + ".cs";
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            using (new EditorGUI.DisabledScope(script == null))
                if (GUILayout.Button(new GUIContent(T("Code", "コード"), T("Open the generated UdonSharp (rewritten on every apply).", "生成された UdonSharp を開きます（反映のたびに作り直されます）。")), GUILayout.Width(52), GUILayout.Height(statusHeight)))
                    AssetDatabase.OpenAsset(script);
        }

    }
}
