using System;
using System.Collections.Generic;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector: event cards, advanced settings (who / delay / conditions), notification setup.
    internal sealed partial class TripwireTriggerEditor
    {
        void DrawEvents()
        {
            // Section title, with expand / collapse all on its right.
            SectionHeader(T("When → what to do", "いつ → 何をする") + (t.events.Count > 0 ? "  " + t.events.Count : ""), () =>
            {
                if (t.events.Count < 2) return;
                if (GUILayout.Button(new GUIContent(T("Expand all", "すべて開く"), T("Alt+click a card's arrow does the same.", "カードの矢印を Alt+クリックでも同じです。")), EditorStyles.miniButtonLeft, GUILayout.ExpandWidth(false)))
                    t.events.ForEach(x => TripwireFolds.SetExpanded(x, true));
                if (GUILayout.Button(new GUIContent(T("Collapse all", "すべてたたむ")), EditorStyles.miniButtonRight, GUILayout.ExpandWidth(false)))
                    t.events.ForEach(x => TripwireFolds.SetExpanded(x, false));
            });
            SelectionBar();
            if (t.events.Count == 0) DrawEmptyGuide();
            for (int i = 0; i < t.events.Count; i++)
            {
                using (var card = new EditorGUILayout.VerticalScope(cardStyle))
                {
                    DrawEvent(i);
                    if (i < t.events.Count)
                    {
                        DropOnEvent(card.rect, t.events[i]); // an action dropped on the card, not on one of its actions
                        DropOnCard(card.rect, t.events, i);
                        SelectedFrame(card.rect, t.events[i]);
                        Stripe(card.rect, CategoryColor(EventCatalog.Get(t.events[i].eventId)?.Category));
                    }
                    // A card with problems keeps a red (error) or orange (warning) frame, even when folded.
                    int ei = i;
                    var severity = generated.Diagnostics.Where(d => d.Event == ei).Select(d => (Severity?)d.Severity).Max();
                    if (severity != null && Event.current.type == EventType.Repaint)
                        Frame(card.rect, severity == Severity.Error ? ErrorColor : WarningColor);
                    if (EditorApplication.isPlaying && i < t.events.Count) FlashIfJustRun(card.rect, i);
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var r = GUILayoutUtility.GetRect(new GUIContent("x"), GUI.skin.button, GUILayout.Height(26), GUILayout.ExpandWidth(true));
                if (GUI.Button(r, T("+ Add a \"when\" (event)", "＋ いつ（イベント）を追加")))
                    PopupWindow.Show(r, new CatalogPicker(CatalogPicker.Events(), Texts.EventCategories, id => Edit(() => t.events.Add(new KEvent { eventId = id }), T("Add Event", "イベントを追加"))));
                PasteButton(t.events, 26);
            }
        }

        static readonly Color ErrorColor = new Color(0.9f, 0.25f, 0.2f), WarningColor = new Color(0.95f, 0.65f, 0.1f);

        static void Frame(Rect r, Color c)
        {
            const float w = 2f;
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - w, r.width, w), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, w, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - w, r.y, w, r.height), c);
        }

        /// <summary>"→ 3 things · 2 conditions · for everyone · after 1s" for a folded card.</summary>
        static string FoldedSummary(KEvent e)
        {
            var parts = new List<string> { T("→ " + e.actions.Count + (e.actions.Count == 1 ? " action" : " actions"), "→ アクション " + e.actions.Count + " 個") };
            if (e.conditions.Count > 0) parts.Add(T(e.conditions.Count + (e.conditions.Count == 1 ? " condition" : " conditions"), "条件 " + e.conditions.Count));
            if (e.broadcast != KBroadcast.Local) parts.Add(Texts.BroadcastChoices[(int)e.broadcast]);
            if (e.delaySeconds > 0f) parts.Add(T(e.delaySeconds + "s later", e.delaySeconds + " 秒後"));
            return string.Join(T(" · ", "・"), parts);
        }

        /// <summary>Error / warning counts of an event (its actions included), as console icons with numbers.</summary>
        void ProblemBadges(int ei)
        {
            int errors = generated.Diagnostics.Count(d => d.Event == ei && d.Severity == Severity.Error);
            int warnings = generated.Diagnostics.Count(d => d.Event == ei && d.Severity == Severity.Warning);
            // Hovering a badge lists the problems, so a folded card says what is wrong without opening it.
            string List(Severity s) => string.Join("\n", generated.Diagnostics.Where(d => d.Event == ei && d.Severity == s).Select(d => "・" + d.Message).Distinct().Take(5));
            // Clicking a badge opens the card, where each problem sits next to the field it is about.
            if (errors > 0 && GUILayout.Button(new GUIContent(errors.ToString(), EditorGUIUtility.IconContent("console.erroricon.sml").image, List(Severity.Error)), EditorStyles.label, GUILayout.ExpandWidth(false)))
                TripwireFolds.SetExpanded(t.events[ei], true);
            if (warnings > 0 && GUILayout.Button(new GUIContent(warnings.ToString(), EditorGUIUtility.IconContent("console.warnicon.sml").image, List(Severity.Warning)), EditorStyles.label, GUILayout.ExpandWidth(false)))
                TripwireFolds.SetExpanded(t.events[ei], true);
        }

        void DrawEvent(int i)
        {
            var e = t.events[i];
            var spec = EventCatalog.Get(e.eventId);

            using (new EditorGUILayout.HorizontalScope())
            {
                DragHandle(t.events, e, spec != null ? Texts.EventName(spec) : e.eventId);
                var fold = GUILayoutUtility.GetRect(14, 20, GUILayout.Width(14));
                // The Inspector draws in hierarchy mode, which moves foldout arrows left, onto the drag handle.
                bool hierarchy = EditorGUIUtility.hierarchyMode;
                EditorGUIUtility.hierarchyMode = false;
                bool wasExpanded = TripwireFolds.IsExpanded(e);
                bool expanded = EditorGUI.Foldout(fold, wasExpanded, GUIContent.none, true);
                EditorGUIUtility.hierarchyMode = hierarchy;
                if (expanded != wasExpanded)
                {
                    // Alt+click opens or closes every card (as in PlayMaker's action list).
                    if (Event.current.alt) t.events.ForEach(x => TripwireFolds.SetExpanded(x, expanded));
                    TripwireFolds.SetExpanded(e, expanded);
                }
                Tag(T("When", "いつ"), CategoryColor(spec?.Category));
                if (EventIcon(spec) != null) IconSlot(EventIcon(spec), CategoryColor(spec.Category));
                var title = spec == null ? T("Unknown event: ", "不明なイベント: ") + e.eventId : Texts.EventName(spec);
                if (spec?.UsesName == true && !string.IsNullOrEmpty(e.name))
                    title += "  —  " + e.name;
                // The title may shrink (clipped) so the summary, badges and menu always fit.
                var r = GUILayoutUtility.GetRect(GUIContent.none, eventButton, GUILayout.MinWidth(60), GUILayout.ExpandWidth(true));
                if (GUI.Button(r, new GUIContent(title, spec != null ? Texts.EventDescription(spec) : null), eventButton))
                {
                    var ev = e;
                    PopupWindow.Show(r, new CatalogPicker(CatalogPicker.Events(), Texts.EventCategories, id => Edit(() => ev.eventId = id, T("Change Event", "イベントを変更"))));
                }
                if (!TripwireFolds.IsExpanded(e))
                {
                    // Folded: what the card does, in one line.
                    var summary = new GUIContent(FoldedSummary(e));
                    GUILayout.Label(summary, smallLabel, GUILayout.MaxWidth(smallLabel.CalcSize(summary).x), GUILayout.MinWidth(30));
                }
                ProblemBadges(ei: i);
                ItemMenu(t.events, i, T("event", "イベント"));
            }
            if (EditorApplication.isPlaying) DrawLastRun(i, spec);
            e.comment = CommentField(e, e.comment);
            if (!TripwireFolds.IsExpanded(e)) return;

            if (spec != null)
            {
                if (spec.Shape == EventShape.Custom)
                    e.name = EditorGUILayout.TextField(new GUIContent(T("Name", "呼ばれる名前"), T("Other triggers and scripts call this name.", "ほかのトリガーやスクリプトはこの名前で呼びます。")), e.name);
                if (spec.Shape == EventShape.VariableChanged)
                    e.name = VariablePopup(T("Variable", "見張る変数"), e.name, _ => true);
                if (spec.HasInteractText)
                    e.interactText = EditorGUILayout.TextField(new GUIContent(T("Hover text", "表示する文字"), T("Shown when pointing at the object.", "オブジェクトを指したときに出る文字。")), e.interactText);
                if (spec.PlayerParam != null)
                    e.playerFilter = PlayerFilterPopup(spec, e.playerFilter);
                if (spec.Shape == EventShape.Listen) DrawListen(e);
                if (spec.Shape == EventShape.Timer) DrawTimer(e);
                if (spec.Shape == EventShape.Ui)
                {
                    var uiType = TripwireModel.ResolveType(spec.UiType);
                    e.uiTarget = EditorGUILayout.ObjectField(T("UI element", "対象の UI"), e.uiTarget, uiType ?? typeof(Object), true);
                }
            }
            if (!EditorApplication.isPlaying) DrawEventFixes(e, spec);
            int ei = i;
            DrawDiagnostics(d => d.Event == ei && d.Action < 0 && d.Condition < 0);

            GUILayout.Label(T("What to do", "何をする"), smallLabel);
            var flat = TripwireModel.FlatActions(e.actions);
            DrawActionList(i, spec, e.actions, a => flat.IndexOf(a));

            DrawDetails(i, e);
        }

        KPlayerFilter PlayerFilterPopup(EventSpec spec, KPlayerFilter current)
        {
            // Display order: me, others, anyone (the enum is Anyone, LocalPlayer, OtherPlayers).
            var order = new[] { KPlayerFilter.LocalPlayer, KPlayerFilter.OtherPlayers, KPlayerFilter.Anyone };
            var labels = Texts.Japanese ? new[] { "自分", "自分以外", "誰でも" } : new[] { "Me", "Others", "Anyone" };
            string label;
            switch (spec.Id)
            {
                case "OnPlayerTriggerEnter": label = T("Who enters", "誰が入ったら"); break;
                case "OnPlayerTriggerExit": label = T("Who leaves the area", "誰が出たら"); break;
                case "OnPlayerJoined": label = T("Who joins", "誰が参加したら"); break;
                case "OnPlayerLeft": label = T("Who leaves the instance", "誰が退出したら"); break;
                default: label = T("Whose", "誰のとき"); break;
            }
            int idx = Math.Max(0, Array.IndexOf(order, current));
            return order[EditorGUILayout.Popup(label, idx, labels)];
        }

        // ---------------- advanced settings (who / delay / conditions) ----------------

        void DrawDetails(int ei, KEvent e)
        {
            bool open = detailsOpen.Contains(e) || HasDiagnostics(d => d.Event == ei && d.Action < 0 && d.Condition >= 0);
            var summary = Texts.BroadcastChoices[(int)e.broadcast] + "・"
                          + (e.delaySeconds > 0f ? T(e.delaySeconds + "s later", e.delaySeconds + " 秒後") : T("immediately", "すぐ")) + "・"
                          + (e.conditions.Count == 0 ? T("no conditions", "条件なし") : T(e.conditions.Count + " condition(s)", "条件 " + e.conditions.Count + " つ"));
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(16);
                bool now = EditorGUILayout.Foldout(open, new GUIContent(T("Advanced settings", "詳細設定") + "    " + summary), true);
                if (now != open) { if (now) detailsOpen.Add(e); else detailsOpen.Remove(e); }
                open = now;
            }
            if (!open) return;

            using (new EditorGUILayout.VerticalScope(innerCardStyle))
            {
                GUILayout.Label(T("Whose screen runs it", "誰の画面で実行する"));
                e.broadcast = (KBroadcast)GUILayout.Toolbar((int)e.broadcast, Texts.BroadcastChoices);
                GUILayout.Label(T("With shared variables the result reaches everyone anyway. Usually leave this as is.",
                                  "変数を「同期する」にしていれば、変えた結果は自動で全員に届きます。ふつうはこのままで大丈夫です。"), captionLabel);
                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(T("Delay", "遅らせる"), GUILayout.Width(100));
                    e.delaySeconds = Mathf.Max(0f, EditorGUILayout.FloatField(e.delaySeconds, GUILayout.Width(60)));
                    GUILayout.Label(T("seconds", "秒後に実行"));
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.Space(4);

                GUILayout.Label(T("Conditions (run only when they hold)", "条件（満たすときだけ実行）"));
                e.conditionsMatchAny = DrawConditions(ei, -1, e.conditions, e.conditionsMatchAny);
            }
        }

        /// <summary>The end of a condition row: "…のとき" or "…でないとき" ("" / "(not)" in English).</summary>
        static bool NotPopup(bool negate, string ja, string jaNot) =>
            EditorGUILayout.Popup(negate ? 1 : 0, Texts.Japanese ? new[] { ja, jaNot } : new[] { "", "(not)" }, GUILayout.MinWidth(40), GUILayout.MaxWidth(Texts.Japanese ? 96 : 56)) == 1;

        static readonly string[] NumberOpsJa = { "と同じ", "と違う", "より小さい", "以下", "より大きい", "以上" };
        static readonly string[] NumberOpsEn = { "equals", "differs from", "less than", "at most", "greater than", "at least" };

        /// <summary>
        /// A list of conditions with an "all / any one" switch (when there are two or more) and an add button. act is the
        /// action number of an If block, or -1 for the event's own conditions. Returns the any-one setting.
        /// </summary>
        bool DrawConditions(int ei, int act, List<KCondition> conditions, bool matchAny)
        {
            if (conditions.Count > 1)
                matchAny = GUILayout.Toolbar(matchAny ? 1 : 0, new[] { T("All of them", "すべて満たす"), T("Any one", "どれか 1 つ満たす") }) == 1;
            for (int c = 0; c < conditions.Count; c++)
            {
                if (DrawCondition(conditions, c)) { Changed(); GUIUtility.ExitGUI(); }
                int ci = c;
                DrawDiagnostics(d => d.Event == ei && d.Action == act && d.Condition == ci);
            }
            if (GUILayout.Button(T("+ Add condition", "＋ 条件を追加"), EditorStyles.miniButton, GUILayout.Width(110)))
                conditions.Add(new KCondition { variable = t.variables.FirstOrDefault(IsLiteralVar)?.name ?? "" });
            return matchAny;
        }

        /// <summary>One condition as a sentence: [variable] が [value] のとき. True when removed.</summary>
        bool DrawCondition(List<KCondition> conditions, int c)
        {
            var cond = conditions[c];
            using (new EditorGUILayout.HorizontalScope())
            {
                cond.variable = VariablePopupInline(cond.variable, v => IsLiteralVar(v));
                var type = TripwireModel.VariableType(t, cond.variable);
                if (type != null && type.Kind == ValueKind.Bool && !type.IsArray)
                {
                    // [var] が [オン/オフ] のとき
                    GUILayout.Label(T("is", "が"), GUILayout.ExpandWidth(false));
                    cond.value.source = KArgSource.Constant;
                    cond.op = KCompareOp.Equal;
                    cond.value.boolValue = EditorGUILayout.Popup(cond.value.boolValue ? 0 : 1, Texts.Japanese ? new[] { "オン", "オフ" } : new[] { "On", "Off" }, GUILayout.MinWidth(40), GUILayout.MaxWidth(64)) == 0;
                    cond.negate = NotPopup(cond.negate, "のとき", "でないとき");
                }
                else if (type != null)
                {
                    bool number = !type.IsArray && (type.Kind == ValueKind.Int || type.Kind == ValueKind.Float);
                    GUILayout.Label(T("", "が"), GUILayout.ExpandWidth(false));
                    ValueSourceField(type, cond.value);
                    var ops = Texts.Japanese ? NumberOpsJa : NumberOpsEn;
                    if (!number) ops = ops.Take(2).ToArray();
                    int op = Math.Min((int)cond.op, ops.Length - 1);
                    cond.op = (KCompareOp)EditorGUILayout.Popup(op, ops, GUILayout.MinWidth(48), GUILayout.MaxWidth(84));
                    cond.negate = NotPopup(cond.negate, "とき", "でないとき");
                }
                if (GUILayout.Button(new GUIContent("×", T("Delete condition", "条件を削除")), EditorStyles.miniButton, GUILayout.Width(22)))
                {
                    conditions.RemoveAt(c);
                    return true;
                }
            }
            return false;
        }

        // ---------------- event kinds with settings of their own ----------------

        /// <summary>A timer event: name (for Start / Stop Timer), seconds or a random range, repeat, start at load.</summary>
        void DrawTimer(KEvent e)
        {
            e.name = EditorGUILayout.TextField(new GUIContent(T("Timer name", "タイマーの名前"), T("\"Start Timer\" and \"Stop Timer\" pick the timer by this name.", "「タイマーを始める」「タイマーを止める」はこの名前で選びます。")), e.name);
            bool random = e.timerMax > e.timerMin;
            bool narrow = EditorGUIUtility.currentViewWidth < 400;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(T("Every", "間隔"));
                int indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;
                e.timerMin = Mathf.Max(0.01f, EditorGUILayout.FloatField(e.timerMin, GUILayout.MinWidth(32), GUILayout.MaxWidth(52)));
                if (random)
                {
                    GUILayout.Label("～", GUILayout.ExpandWidth(false));
                    e.timerMax = Mathf.Max(e.timerMin, EditorGUILayout.FloatField(e.timerMax, GUILayout.MinWidth(32), GUILayout.MaxWidth(52)));
                }
                else e.timerMax = e.timerMin;
                GUILayout.Label(T("seconds", "秒"), GUILayout.ExpandWidth(false));
                if (!narrow) RandomToggle();
                EditorGUI.indentLevel = indent;
            }
            // A narrow Inspector has no room for it on the same line.
            if (narrow)
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUIUtility.labelWidth + 2);
                    RandomToggle();
                }

            void RandomToggle()
            {
                bool r = GUILayout.Toggle(random, T("Random", "ランダム"), GUILayout.ExpandWidth(false));
                if (r != random) e.timerMax = r ? e.timerMin * 2f : e.timerMin;
            }
            e.timerRepeat = EditorGUILayout.Popup(T("Runs", "実行"), e.timerRepeat ? 0 : 1,
                Texts.Japanese ? new[] { "くり返す", "一度だけ" } : new[] { "Repeatedly", "Once" }) == 0;
            e.timerAutoStart = EditorGUILayout.Toggle(new GUIContent(T("Start right away", "最初から動かす"),
                T("Off: only \"Start Timer\" starts it.", "オフにすると「タイマーを始める」で始めるまで動きません。")), e.timerAutoStart);
        }

        /// <summary>"Notified by another script": the notifying object, how to register, and the name it calls back.</summary>
        void DrawListen(KEvent e)
        {
            e.listenTarget = EditorGUILayout.ObjectField(new GUIContent(T("Notified by", "通知元"),
                T("The object with the U# script (video player, pen...) that sends the notification.", "通知を送ってくる UdonSharp スクリプト（動画プレイヤーやペンなど）が付いたオブジェクト。")),
                e.listenTarget, typeof(GameObject), true);
            if (e.listenTarget is Component comp) e.listenTarget = comp.gameObject;

            var found = e.listenTarget != null ? UdonSharpApi.ScriptsOn(e.listenTarget) : new List<Type>();
            var scripts = found.Where(UdonSharpApi.IsReachable).ToList();

            // Known assets: pick what happened from a list; the registration and the name are filled in.
            var presetScript = scripts.FirstOrDefault(s => PresetResolver.Notifications(s).Count > 0);
            if (presetScript != null)
            {
                var preset = PresetResolver.For(presetScript);
                var notes = PresetResolver.Notifications(presetScript);
                var matched = PresetResolver.Matching(e, presetScript);
                var labels = notes.Select(n => new GUIContent(T(n.En, n.Ja), n.Callback)).ToList();
                labels.Add(new GUIContent(T("Set up by hand…", "自分で設定する…")));
                int current = matched != null && !e.listenByHand ? notes.IndexOf(matched) : labels.Count - 1;
                int picked = EditorGUILayout.Popup(new GUIContent(T("What happened", "何が起きたとき"), T(preset.NameEn, preset.NameJa)), current, labels.ToArray());
                if (picked != current)
                {
                    e.listenByHand = picked >= notes.Count;
                    if (!e.listenByHand) PresetResolver.Apply(e, presetScript, notes[picked]);
                }
                if (picked < notes.Count)
                {
                    GUILayout.Label(T(preset.NameEn + " · called back as ", preset.NameJa + " ・ 届く名前: ") + e.name, captionLabel);
                    return;
                }
            }

            var options = new List<(string id, string label)> { ("", T("Don't register (set it up in the script's Inspector)", "登録しない（スクリプトの Inspector にこのトリガーを入れる）")) };
            foreach (var s in scripts)
                foreach (var r in UdonSharpApi.Registrations(s))
                    options.Add((r.Id, (scripts.Count > 1 ? s.Name + "." : "") + r.Method + "(" + string.Join(", ", r.Params.Select((p, k) => k == r.SelfIndex ? T("this trigger", "このトリガー") : Texts.TypeName(p.Type) + " " + p.Name)) + ")"));
            if (e.listenTarget != null && options.Count == 1)
                GUILayout.Label(T("No registration method found on this object's U# scripts.", "このオブジェクトの UdonSharp スクリプトに、登録用のメソッドが見つかりません。"), captionLabel);

            int idx = Math.Max(0, options.FindIndex(o => o.id == (e.listenMethod ?? "")));
            if (options.Count > 1 || !string.IsNullOrEmpty(e.listenMethod))
            {
                if (idx == 0 && !string.IsNullOrEmpty(e.listenMethod)) options.Add((e.listenMethod, T("(missing) ", "（見つかりません）") + e.listenMethod));
                idx = options.FindIndex(o => o.id == (e.listenMethod ?? ""));
                idx = EditorGUILayout.Popup(new GUIContent(T("Register with", "登録方法"), T("Called once when the world starts so the script knows to notify this trigger.", "ワールド開始時に一度呼び、このトリガーへ通知してもらえるようにします。")),
                    Math.Max(0, idx), options.Select(o => new GUIContent(o.label)).ToArray());
                if (options[idx].id != e.listenMethod) { e.listenMethod = options[idx].id; e.listenArgs.Clear(); e.listenPrepare = ""; }
            }

            var reg = UdonSharpApi.GetRegistration(TripwireModel.ListenScriptType(e), e.listenMethod);
            if (reg != null)
            {
                while (e.listenArgs.Count < reg.Params.Count) e.listenArgs.Add(new KArg());
                for (int k = 0; k < reg.Params.Count; k++)
                {
                    if (k == reg.SelfIndex) continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.PrefixLabel("  " + ObjectNames.NicifyVariableName(reg.Params[k].Name));
                        ValueSourceField(reg.Params[k].Type, e.listenArgs[k]);
                    }
                }
            }

            e.name = EditorGUILayout.TextField(new GUIContent(T("Callback name", "届く名前"),
                T("The exact name the script calls (see its documentation), e.g. _TvPlay.", "相手のスクリプトが呼ぶ名前を、そのまま書きます（例: _TvPlay）。名前は相手の説明書やソースで確かめてください。")), e.name);
        }
    }
}
