using System;
using System.Linq;
using Tripwire.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tripwire.Editor
{
    // The trigger Inspector during Play: values to read and change, events to run, and on each card when it last ran or
    // what stopped it (from TripwireTrace).
    internal sealed partial class TripwireTriggerEditor
    {
        static readonly Color RanColor = new Color(0.35f, 0.8f, 0.45f), StoppedColor = new Color(0.95f, 0.6f, 0.2f);
        const float FlashSeconds = 0.8f;

        /// <summary>Live values of the trigger's variables (changeable), and buttons that run its events as if they happened.</summary>
        void DrawPlaying()
        {
            EditorGUILayout.HelpBox(T("Playing: change a value to try it, ▶ runs an event, and each card says when it last ran or what stopped it. Edit the cards after stopping Play.",
                                      "Play 中です。値を書き換えて試したり、▶ でイベントを起こしたりできます。各カードに、最後に動いた時刻や止まった理由が出ます。カードの編集は Play を止めてから行います。"), MessageType.Info);
            if (GUILayout.Button(new GUIContent(T("Open the event history", "実行の記録を開く"), T("Every event that ran or was stopped during this Play, in order.", "この Play で動いたイベントと止まったイベントを、順に並べて見られます。")), EditorStyles.miniButton))
                TripwireHistoryWindow.Open();
            var ub = t.generated;
            if (ub == null) { EditorGUILayout.LabelField(T("Not applied: nothing is running.", "反映されていないので、動いていません。")); return; }
            if (t.variables.Count > 0)
            {
                GUILayout.Label(T("Variables now", "変数のいまの値"), smallLabel);
                foreach (var v in t.variables)
                {
                    if (v.temporary) { EditorGUILayout.LabelField(v.name, T("(temporary: only while an event runs)", "（一時的: イベントが動いている間だけ）")); continue; }
                    var field = CodeGenerator.FieldOf(v.name);
                    ub.TryGetProgramVariable(field, out object value);
                    var type = TripwireModel.VariableType(t, v.name);
                    if (LiveField(v.name, type, value, out object edited)) TripwireTrace.SetValue(t, v, edited);
                }
            }
            var runnable = t.events.Select((e, i) => (e, i, entry: TryableEntry(e, EventCatalog.Get(e.eventId)))).Where(x => x.entry != null).ToList();
            if (runnable.Count == 0) return;
            GUILayout.Label(T("Run an event", "イベントを実行"), smallLabel);
            foreach (var (e, _, entry) in runnable)
                using (new EditorGUILayout.HorizontalScope())
                {
                    var spec = EventCatalog.Get(e.eventId);
                    GUILayout.Label(Texts.EventName(spec) + (spec.UsesName && !string.IsNullOrEmpty(e.name) ? "  —  " + e.name : ""));
                    if (GUILayout.Button(new GUIContent(T("▶ Run", "▶ 実行"), T("Run it now, as if it happened (it doesn't run if its conditions aren't met).", "このイベントが起きたときと同じように、いま動かします（条件を満たさないときは動きません）。")),
                            EditorStyles.miniButton, GUILayout.Width(64)))
                        ub.SendCustomEvent(entry);
                }
        }

        /// <summary>A field for a live value of a simple type (read-only otherwise). True when the person changed it.</summary>
        static bool LiveField(string name, ParamType type, object value, out object edited)
        {
            edited = value;
            if (type == null || type.IsArray)
            {
                EditorGUILayout.LabelField(name, Shown(value));
                return false;
            }
            EditorGUI.BeginChangeCheck();
            // Numbers and text take effect on Enter (or leaving the field), not on every key: each change runs the change events.
            switch (type.Kind)
            {
                case ValueKind.Bool: edited = EditorGUILayout.Toggle(name, value is bool b && b); break;
                case ValueKind.Int: edited = EditorGUILayout.DelayedIntField(name, value is int n ? n : 0); break;
                case ValueKind.Float: edited = EditorGUILayout.DelayedFloatField(name, value is float f ? f : 0f); break;
                case ValueKind.String: edited = EditorGUILayout.DelayedTextField(name, value as string ?? ""); break;
                case ValueKind.Vector3: edited = EditorGUILayout.Vector3Field(name, value is Vector3 v3 ? v3 : Vector3.zero); break;
                case ValueKind.Color: edited = EditorGUILayout.ColorField(name, value is Color c ? c : Color.white); break;
                default:
                    EditorGUILayout.LabelField(name, Shown(value));
                    EditorGUI.EndChangeCheck();
                    return false;
            }
            return EditorGUI.EndChangeCheck();
        }

        static string Shown(object value)
        {
            switch (value)
            {
                case null: return T("(none)", "（なし）");
                case bool b: return b ? T("On", "オン") : T("Off", "オフ");
                case Object o: return o != null ? o.name : T("(none)", "（なし）");
                case Array a: return T(a.Length + " items", a.Length + " 個");
                case float f: return f.ToString("0.###");
                default: return value.ToString();
            }
        }

        /// <summary>Under a card's title during Play: when it last ran (and how often), or what stopped it.</summary>
        void DrawLastRun(int i, EventSpec spec)
        {
            InitPlayingStyles();
            var (last, runs) = TripwireTrace.Of(t, i);
            string text;
            GUIStyle style = captionLabel;
            if (spec != null && spec.Frequent) text = T("Not recorded (happens many times a second).", "1 秒に何度も起きるので、記録しません。");
            else if (last == null) text = T("Hasn't run yet.", "まだ動いていません。");
            else if (last.Ran)
            {
                text = T("Ran at " + last.Time.ToString("0.00") + "s (" + runs + (runs == 1 ? " time)" : " times)"), last.Time.ToString("0.00") + " 秒に動いた（" + runs + " 回目）")
                       + (string.IsNullOrEmpty(last.Detail) ? "" : T(": ", ": ") + last.Detail);
                style = lastRanLabel;
            }
            else
            {
                text = last.Time.ToString("0.00") + T("s: ", " 秒: ") + last.Detail;
                style = lastStoppedLabel;
            }
            // The cards are drawn read-only during Play; this line stays readable.
            bool enabled = GUI.enabled;
            GUI.enabled = true;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(20);
                GUILayout.Label(text, style);
            }
            GUI.enabled = enabled;
        }

        static GUIStyle lastRanLabel, lastStoppedLabel;

        static void InitPlayingStyles()
        {
            if (lastRanLabel != null) return;
            lastRanLabel = new GUIStyle(captionLabel);
            lastRanLabel.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.5f, 0.85f, 0.55f) : new Color(0.1f, 0.45f, 0.2f);
            lastStoppedLabel = new GUIStyle(captionLabel);
            lastStoppedLabel.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.98f, 0.7f, 0.35f) : new Color(0.65f, 0.3f, 0.0f);
        }

        /// <summary>A card's frame lights up for a moment when the event runs (green) or is stopped (orange).</summary>
        void FlashIfJustRun(Rect card, int i)
        {
            var (last, _) = TripwireTrace.Of(t, i);
            if (last == null || Event.current.type != EventType.Repaint) return;
            float age = Time.time - last.Time;
            if (age < 0f || age > FlashSeconds) return;
            var c = last.Ran ? RanColor : StoppedColor;
            c.a = 1f - age / FlashSeconds;
            Frame(card, c);
        }
    }
}
